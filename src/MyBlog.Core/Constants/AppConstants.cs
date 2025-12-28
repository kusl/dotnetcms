namespace MyBlog.Core.Constants;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class AppConstants
{
    /// <summary>The name of the authentication cookie.</summary>
    public const string AuthCookieName = "MyBlog.Auth";

    /// <summary>The admin role claim value.</summary>
    public const string AdminRole = "Admin";

    /// <summary>Default page size for listings.</summary>
    public const int DefaultPageSize = 10;

    /// <summary>Maximum image size in bytes (5MB).</summary>
    public const int MaxImageSizeBytes = 5 * 1024 * 1024;

    /// <summary>Allowed image content types.</summary>
    public static readonly string[] AllowedImageTypes =
        ["image/jpeg", "image/png", "image/gif", "image/webp"];
}
