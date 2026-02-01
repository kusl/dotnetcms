using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

/// <summary>
/// Generates URL-friendly slugs from text.
/// </summary>
public sealed partial class SlugService : ISlugService
{
    /// <inheritdoc />
    public string GenerateSlugOrUuid(string title)
    {
        var slug = GenerateSlug(title);

        return !string.IsNullOrWhiteSpace(slug) ? slug : GenerateUuidSlug();
    }

    /// <inheritdoc />
    public string GenerateUuidSlug()
    {
        return $"post-{Guid.CreateVersion7()}";
    }

    private static string GenerateSlug(string title)
    {
        // Normalize unicode and convert to lowercase
        var normalized = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // Replace spaces and underscores with hyphens
        result = SpacePattern().Replace(result, "-");

        // Remove all non-alphanumeric characters except hyphens
        result = NonAlphanumericPattern().Replace(result, "");

        // Replace multiple hyphens with single hyphen
        result = MultipleHyphenPattern().Replace(result, "-");

        // Trim hyphens from ends
        result = result.Trim('-');

        return result;
    }

    [GeneratedRegex(@"[\s_]+")]
    private static partial Regex SpacePattern();

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonAlphanumericPattern();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphenPattern();
}
