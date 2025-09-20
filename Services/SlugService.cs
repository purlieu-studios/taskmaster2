using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TaskMaster.Services;

public static class SlugService
{
    public static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "untitled";

        // Convert to lowercase
        var slug = input.ToLowerInvariant();

        // Remove diacritics (accents)
        slug = RemoveDiacritics(slug);

        // Replace spaces and underscores with hyphens
        slug = Regex.Replace(slug, @"[\s_]+", "-");

        // Remove any character that is not alphanumeric or hyphen
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // Remove multiple consecutive hyphens
        slug = Regex.Replace(slug, @"-+", "-");

        // Trim hyphens from start and end
        slug = slug.Trim('-');

        // Ensure it's not empty
        if (string.IsNullOrEmpty(slug))
            slug = "untitled";

        // Truncate if too long
        if (slug.Length > 50)
            slug = slug.Substring(0, 50).TrimEnd('-');

        return slug;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}