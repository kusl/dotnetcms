namespace MyBlog.Core.Constants;

public static class AppConstants
{
    public const string AuthCookieName = "MyBlog.Auth";

    public const string AdminRole = "Admin";

    public const int DefaultPageSize = 10;

    public const int MaxImageSizeBytes = 5 * 1024 * 1024;

    public static readonly string[] AllowedImageTypes =
        ["image/jpeg", "image/png", "image/gif", "image/webp"];
}
