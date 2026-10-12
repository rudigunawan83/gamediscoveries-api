using System.Net;
using FluentAssertions;
using GameDiscoveries.Api.Extensions;

namespace GameDiscoveries.IntegrationTests;

public sealed class ClientNetworkTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("172.18.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.10")]
    [InlineData("::1")]
    [InlineData("::ffff:172.18.0.5")]
    public void Docker_and_loopback_callers_are_private(string address) =>
        ClientNetwork.IsPrivate(IPAddress.Parse(address)).Should().BeTrue();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("2001:4860:4860::8888")]
    public void Public_callers_are_not_private(string address) =>
        ClientNetwork.IsPrivate(IPAddress.Parse(address)).Should().BeFalse();

    [Fact]
    public void Missing_address_is_not_private() =>
        ClientNetwork.IsPrivate(null).Should().BeFalse();
}
