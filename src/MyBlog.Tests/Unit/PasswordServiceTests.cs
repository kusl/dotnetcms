using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class PasswordServiceTests
{
    private readonly PasswordService _sut = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyHash()
    {
        var hash = _sut.HashPassword("TestPassword123");
        Assert.False(string.IsNullOrEmpty(hash));
    }

    [Fact]
    public void HashPassword_ReturnsDifferentHashForSamePassword()
    {
        var hash1 = _sut.HashPassword("TestPassword123");
        var hash2 = _sut.HashPassword("TestPassword123");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var password = "TestPassword123";
        var hash = _sut.HashPassword(password);

        var result = _sut.VerifyPassword(hash, password);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var hash = _sut.HashPassword("TestPassword123");

        var result = _sut.VerifyPassword(hash, "WrongPassword");

        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        var hash = _sut.HashPassword("TestPassword123");

        var result = _sut.VerifyPassword(hash, "");

        Assert.False(result);
    }
}
