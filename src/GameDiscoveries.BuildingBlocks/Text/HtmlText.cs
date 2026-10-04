using System.Net;
using System.Text.RegularExpressions;

namespace GameDiscoveries.BuildingBlocks.Text;

public static partial class HtmlText
{
    public static string? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var decoded = WebUtility.HtmlDecode(value);
        decoded = BrToNewline().Replace(decoded, "\n");
        decoded = StripTags().Replace(decoded, string.Empty);
        return decoded.Trim();
    }

    [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BrToNewline();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex StripTags();
}
