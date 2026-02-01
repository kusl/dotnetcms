namespace MyBlog.Core.Interfaces;

/// <summary>
/// Service interface for generating URL-friendly slugs.
/// </summary>
public interface ISlugService
{
    /// <summary>Generates a slug from a title, falling back to UUID if title produces empty slug.</summary>
    string GenerateSlugOrUuid(string title);

    /// <summary>Generates a guaranteed unique slug using UUIDv7.</summary>
    string GenerateUuidSlug();
}
