using System.Text.RegularExpressions;

namespace GameDiscoveries.Modules.Users.Features.Profile;

public static partial class ProfileRules
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 40;
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 30;

    public static bool IsValidDisplayName(string value) =>
        value.Length is >= DisplayNameMinLength and <= DisplayNameMaxLength
        && value.All(static c => !char.IsControl(c));

    public static bool IsValidUsername(string value) =>
        value.Length is >= UsernameMinLength and <= UsernameMaxLength
        && UsernamePattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();
}
