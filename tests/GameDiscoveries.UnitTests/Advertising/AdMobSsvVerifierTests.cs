using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameDiscoveries.Modules.Advertising.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameDiscoveries.UnitTests.Advertising;

public sealed class AdMobSsvVerifierTests
{
    private const long KeyId = 3335741209;

    private const string SignedContent =
        "ad_network=5450213213286189855&ad_unit=1234567890&custom_data=tok-1&reward_amount=1"
        + "&reward_item=XP&timestamp=1507770365237823&transaction_id=18fa792de1bca816048293fc71035638"
        + "&user_id=6f2a3c3e-0000-0000-0000-000000000001";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    [Fact]
    public async Task Accepts_callback_signed_by_admob_key_and_reads_fields()
    {
        var verifier = Verifier(_key);

        var callback = await verifier.VerifyAsync("?" + Signed(SignedContent, _key));

        callback.Should().NotBeNull();
        callback!.CustomData.Should().Be("tok-1");
        callback.TransactionId.Should().Be("18fa792de1bca816048293fc71035638");
        callback.UserId.Should().Be("6f2a3c3e-0000-0000-0000-000000000001");
        callback.AdUnit.Should().Be("1234567890");
    }

    [Fact]
    public async Task Rejects_tampered_parameters()
    {
        var query = Signed(SignedContent, _key).Replace("custom_data=tok-1", "custom_data=tok-2");

        (await Verifier(_key).VerifyAsync(query)).Should().BeNull();
    }

    [Fact]
    public async Task Rejects_signature_from_another_key()
    {
        using var attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        (await Verifier(_key).VerifyAsync(Signed(SignedContent, attacker))).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("custom_data=tok-1&transaction_id=t")]
    [InlineData("custom_data=tok-1&signature=%%%&key_id=3335741209")]
    [InlineData("custom_data=tok-1&signature=abc&key_id=not-a-number")]
    public async Task Rejects_unsigned_or_malformed_queries(string query)
    {
        (await Verifier(_key).VerifyAsync(query)).Should().BeNull();
    }

    [Fact]
    public async Task Rejects_unknown_key_id()
    {
        var query = Signed(SignedContent, _key).Replace($"key_id={KeyId}", "key_id=42");

        (await Verifier(_key).VerifyAsync(query)).Should().BeNull();
    }

    private static string Signed(string content, ECDsa key)
    {
        var signature = key.SignData(
            Encoding.UTF8.GetBytes(content),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        return $"{content}&signature={WebEncoders.Base64UrlEncode(signature)}&key_id={KeyId}";
    }

    private static AdMobSsvVerifier Verifier(ECDsa key) =>
        new(new StaticKeys(KeyId, key.ExportSubjectPublicKeyInfo()), NullLogger<AdMobSsvVerifier>.Instance);

    private sealed class StaticKeys(long keyId, byte[] key) : IAdMobVerifierKeyProvider
    {
        public Task<byte[]?> GetKeyAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == keyId ? key : null);
    }
}
