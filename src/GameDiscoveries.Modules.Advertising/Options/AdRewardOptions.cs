namespace GameDiscoveries.Modules.Advertising.Options;

public sealed class AdRewardOptions
{
    public const string SectionName = "AdRewards";

    /// <summary>Off until an AdMob rewarded ad unit with SSV pointing at this API exists.</summary>
    public bool Enabled { get; set; }

    /// <summary>Rewarded views per user per UTC day.</summary>
    public int DailyLimit { get; set; } = 5;

    public int TicketTtlMinutes { get; set; } = 60;

    public string VerifierKeysUrl { get; set; } = "https://www.gstatic.com/admob/reward/verifier-keys.json";

    /// <summary>When non-empty, callbacks from other ad units are rejected.</summary>
    public string[] AllowedAdUnits { get; set; } = [];
}
