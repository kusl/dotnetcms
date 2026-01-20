namespace MyBlog.Core.Interfaces;

/// <summary>
/// Service interface for generating URL-friendly slugs.
/// </summary>
public interface ISlugService
{
    /// <summary>Generates a slug from a title.</summary>
    string GenerateSlugOrUuid(string title);
}
