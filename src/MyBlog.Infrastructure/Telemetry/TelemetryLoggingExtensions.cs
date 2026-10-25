using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyBlog.Infrastructure.Telemetry;

public static class TelemetryLoggingExtensions
{
    public static OpenTelemetryLoggerOptions AddLocalExporters(
        this OpenTelemetryLoggerOptions options,
        IConfiguration configuration)
    {
        if (configuration.GetValue("Telemetry:EnableFileLogging", true))
        {
            var logsDirectory = TelemetryPathResolver.GetLogsDirectory();
            if (logsDirectory is not null)
            {
                options.AddProcessor(_ => new BatchLogRecordExportProcessor(new FileLogExporter(logsDirectory)));
            }
        }

        if (configuration.GetValue("Telemetry:EnableDatabaseLogging", true))
        {
            options.AddProcessor(sp => new BatchLogRecordExportProcessor(
                new DatabaseLogExporter(sp.GetRequiredService<IServiceScopeFactory>())));
        }

        return options;
    }
}
