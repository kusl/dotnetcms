using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

public sealed partial class SlugService : ISlugService
{
    public string GenerateSlugOrUuid(string title)
    {
        var slug = GenerateSlug(title);

        return !string.IsNullOrWhiteSpace(slug) ? slug : GenerateUuidSlug();
    }

    public string GenerateUuidSlug() => $"post-{Guid.CreateVersion7()}";

    private static string GenerateSlug(string title)
    {
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

        result = SpacePattern().Replace(result, "-");

        result = NonAlphanumericPattern().Replace(result, "");

        result = MultipleHyphenPattern().Replace(result, "-");

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
