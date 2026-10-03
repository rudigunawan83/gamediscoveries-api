using System.Diagnostics;

namespace GameDiscoveries.BuildingBlocks.Observability;

public static class ActivitySources
{
    public const string Name = "GameDiscoveries.Api";

    public static readonly ActivitySource Source = new(Name);
}
