using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

/// <summary>
/// Integration tests for the ImageRepository.
/// Uses in-memory SQLite for cross-platform compatibility.
/// </summary>
public class ImageRepositoryTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly ImageRepository _sut;
    private readonly User _testUser;

    public ImageRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _sut = new ImageRepository(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_AddsImageToDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("test-image.png");

        var result = await _sut.CreateAsync(image, ct);

        Assert.NotEqual(Guid.Empty, result.Id);
        var saved = await _context.Images.FindAsync([result.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal("test-image.png", saved.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsImage()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("test-image.png");
        await _sut.CreateAsync(image, ct);

        var result = await _sut.GetByIdAsync(image.Id, ct);

        Assert.NotNull(result);
        Assert.Equal("test-image.png", result.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllImagesOrderedByUploadDate()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create images with different upload times
        var image1 = CreateTestImage("first.png");
        image1.UploadedAtUtc = DateTime.UtcNow.AddHours(-2);
        await _sut.CreateAsync(image1, ct);

        var image2 = CreateTestImage("second.png");
        image2.UploadedAtUtc = DateTime.UtcNow.AddHours(-1);
        await _sut.CreateAsync(image2, ct);

        var image3 = CreateTestImage("third.png");
        image3.UploadedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(image3, ct);

        var result = await _sut.GetAllAsync(ct);

        Assert.Equal(3, result.Count);
        // Should be ordered by UploadedAtUtc descending (newest first)
        Assert.Equal("third.png", result[0].FileName);
        Assert.Equal("second.png", result[1].FileName);
        Assert.Equal("first.png", result[2].FileName);
    }

    [Fact]
    public async Task GetAllAsync_WithNoImages_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetAllAsync(ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesImageFromDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("to-delete.png");
        await _sut.CreateAsync(image, ct);

        await _sut.DeleteAsync(image.Id, ct);

        var deleted = await _context.Images.FindAsync([image.Id], ct);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        // Should not throw
        await _sut.DeleteAsync(Guid.NewGuid(), ct);
    }

    [Fact]
    public async Task CreateAsync_StoresImageData()
    {
        var ct = TestContext.Current.CancellationToken;
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        var image = CreateTestImage("data-test.png", imageData);

        await _sut.CreateAsync(image, ct);

        var saved = await _sut.GetByIdAsync(image.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal(imageData, saved.Data);
    }

    [Fact]
    public async Task CreateAsync_PreservesContentType()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("test.jpg");
        image.ContentType = "image/jpeg";

        await _sut.CreateAsync(image, ct);

        var saved = await _sut.GetByIdAsync(image.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal("image/jpeg", saved.ContentType);
    }

    [Fact]
    public async Task CreateAsync_PreservesUserAssociation()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("user-test.png");

        await _sut.CreateAsync(image, ct);

        var saved = await _sut.GetByIdAsync(image.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal(_testUser.Id, saved.UploadedByUserId);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotTrackEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("tracking-test.png");
        await _sut.CreateAsync(image, ct);

        var result = await _sut.GetAllAsync(ct);

        // AsNoTracking should mean entities are not tracked
        Assert.Single(result);
        var entry = _context.Entry(result[0]);
        Assert.Equal(EntityState.Detached, entry.State);
    }

    private Image CreateTestImage(string fileName, byte[]? data = null)
    {
        return new Image
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = "image/png",
            Data = data ?? new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            UploadedByUserId = _testUser.Id,
            UploadedAtUtc = DateTime.UtcNow
        };
    }
}
