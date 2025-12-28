using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyBlog.Infrastructure.Telemetry;

/// <summary>
/// OpenTelemetry log exporter that writes to JSON files.
/// </summary>
public sealed class FileLogExporter : BaseExporter<LogRecord>
{
    private readonly string _directory;
    private readonly string _runId;
    private readonly long _maxFileSizeBytes;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private long _currentFileSize;
    private int _fileNumber;
    private bool _isFirstRecord = true;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>Initializes a new instance of FileLogExporter.</summary>
    public FileLogExporter(string directory, long maxFileSizeBytes = 25 * 1024 * 1024)
    {
        _directory = directory;
        _maxFileSizeBytes = maxFileSizeBytes;
        _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(_directory);
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            lock (_lock)
            {
                EnsureWriter();

                foreach (var record in batch)
                {
                    var obj = new
                    {
                        Timestamp = record.Timestamp.ToString("O"),
                        Level = record.LogLevel.ToString(),
                        Category = record.CategoryName,
                        Message = record.FormattedMessage ?? record.Body,
                        TraceId = record.TraceId.ToString(),
                        SpanId = record.SpanId.ToString(),
                        Exception = record.Exception?.ToString()
                    };

                    var json = JsonSerializer.Serialize(obj, _jsonOptions);
                    var bytes = Encoding.UTF8.GetByteCount(json) + 2;

                    if (_currentFileSize + bytes > _maxFileSizeBytes)
                    {
                        RotateFile();
                    }

                    if (!_isFirstRecord)
                    {
                        _writer!.WriteLine(",");
                    }
                    else
                    {
                        _isFirstRecord = false;
                    }

                    _writer!.Write(json);
                    _currentFileSize += bytes;
                }

                _writer!.Flush();
            }

            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private void EnsureWriter()
    {
        if (_writer is null)
        {
            OpenNewFile();
        }
    }

    private void OpenNewFile()
    {
        var fileName = _fileNumber == 0
            ? $"logs_{_runId}.json"
            : $"logs_{_runId}_{_fileNumber:D3}.json";

        _writer = new StreamWriter(Path.Combine(_directory, fileName), false, Encoding.UTF8);
        _writer.WriteLine("[");
        _currentFileSize = 2;
        _isFirstRecord = true;
    }

    private void RotateFile()
    {
        CloseWriter();
        _fileNumber++;
        OpenNewFile();
    }

    private void CloseWriter()
    {
        if (_writer is not null)
        {
            _writer.WriteLine();
            _writer.WriteLine("]");
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        lock (_lock)
        {
            CloseWriter();
        }
        return true;
    }
}
