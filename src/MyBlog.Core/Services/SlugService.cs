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
    public string GenerateSlug(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

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
