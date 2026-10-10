using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using GameDiscoveries.Modules.Advertising.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameDiscoveries.Modules.Advertising.Services;

/// <summary>Public keys AdMob signs server-side verification callbacks with, by key id.</summary>
public interface IAdMobVerifierKeyProvider
{
    /// <returns>DER SubjectPublicKeyInfo bytes, or null when the key id is unknown.</returns>
    Task<byte[]?> GetKeyAsync(long keyId, CancellationToken cancellationToken = default);
}

public sealed record AdMobCallback(
    string? TransactionId,
    string? CustomData,
    string? UserId,
    string? AdUnit,
    string? RewardAmount);

public interface IAdMobSsvVerifier
{
    /// <summary>
    /// Verifies the ECDSA signature of a raw AdMob SSV query string.
    /// Returns null when the callback is not authentic.
    /// </summary>
    Task<AdMobCallback?> VerifyAsync(string rawQuery, CancellationToken cancellationToken = default);
}

public sealed class AdMobSsvVerifier(
    IAdMobVerifierKeyProvider keys,
    ILogger<AdMobSsvVerifier> logger) : IAdMobSsvVerifier
{
    private const string SignatureMarker = "&signature=";

    public async Task<AdMobCallback?> VerifyAsync(string rawQuery, CancellationToken cancellationToken = default)
    {
        var query = rawQuery.StartsWith('?') ? rawQuery[1..] : rawQuery;

        // AdMob signs everything before "&signature="; signature and key_id are always last.
        var markerIndex = query.IndexOf(SignatureMarker, StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return null;
        }

        var message = query[..markerIndex];
        var parameters = QueryHelpers.ParseQuery(query);
        var signature = parameters.TryGetValue("signature", out var sig) ? sig.ToString() : null;
        var keyIdRaw = parameters.TryGetValue("key_id", out var kid) ? kid.ToString() : null;
        if (string.IsNullOrEmpty(signature) || !long.TryParse(keyIdRaw, out var keyId))
        {
            return null;
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = WebEncoders.Base64UrlDecode(signature);
        }
        catch (FormatException)
        {
            return null;
        }

        var publicKey = await keys.GetKeyAsync(keyId, cancellationToken);
        if (publicKey is null)
        {
            logger.LogWarning("admob_ssv_unknown_key keyId={KeyId}", keyId);
            return null;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            var valid = ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(message),
                signatureBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            if (!valid)
            {
                return null;
            }
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "admob_ssv_verify_failed keyId={KeyId}", keyId);
            return null;
        }

        string? Get(string name) =>
            parameters.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value.ToString())
                ? value.ToString()
                : null;

        return new AdMobCallback(
            Get("transaction_id"),
            Get("custom_data"),
            Get("user_id"),
            Get("ad_unit"),
            Get("reward_amount"));
    }
}

/// <summary>
/// Fetches and caches AdMob verifier keys; refetches when a key id is unknown
/// (key rotation), at most once per minute.
/// </summary>
public sealed class HttpAdMobVerifierKeyProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AdRewardOptions> options,
    TimeProvider timeProvider,
    ILogger<HttpAdMobVerifierKeyProvider> logger) : IAdMobVerifierKeyProvider
{
    public const string HttpClientName = "admob-verifier-keys";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyDictionary<long, byte[]> _keys = new Dictionary<long, byte[]>();
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public async Task<byte[]?> GetKeyAsync(long keyId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        if (_keys.TryGetValue(keyId, out var cached) && now - _fetchedAt < CacheLifetime)
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            var stale = now - _fetchedAt >= CacheLifetime;
            var unknown = !_keys.ContainsKey(keyId);
            if ((stale || unknown) && now - _fetchedAt >= MinRefreshInterval)
            {
                await RefreshAsync(now, cancellationToken);
            }

            return _keys.TryGetValue(keyId, out var key) ? key : null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            var payload = await client.GetFromJsonAsync<KeysPayload>(options.Value.VerifierKeysUrl, cancellationToken);
            var keys = new Dictionary<long, byte[]>();
            foreach (var key in payload?.Keys ?? [])
            {
                if (!string.IsNullOrWhiteSpace(key.Base64))
                {
                    keys[key.KeyId] = Convert.FromBase64String(key.Base64);
                }
            }

            if (keys.Count > 0)
            {
                _keys = keys;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "admob_verifier_keys_fetch_failed");
        }
        finally
        {
            _fetchedAt = now;
        }
    }

    private sealed record KeysPayload([property: JsonPropertyName("keys")] IReadOnlyList<KeyEntry>? Keys);

    private sealed record KeyEntry(
        [property: JsonPropertyName("keyId")] long KeyId,
        [property: JsonPropertyName("base64")] string? Base64);
}
