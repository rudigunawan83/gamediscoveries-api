using System.Net;

namespace GameDiscoveries.Api.Extensions;

/// <summary>
/// The API is only published on the host loopback, so every caller is either nginx on the
/// host or a sibling container (Next.js SSR) on a private Docker network.
/// </summary>
public static class ClientNetwork
{
    public static readonly IReadOnlyList<IPNetwork> Private =
    [
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("::1/128"),
        IPNetwork.Parse("fc00::/7")
    ];

    public static bool IsPrivate(IPAddress? address)
    {
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        foreach (var network in Private)
        {
            if (network.Contains(address)) return true;
        }
        return false;
    }
}
