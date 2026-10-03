namespace GameDiscoveries.Infrastructure.Storage;

/// <summary>
/// Placeholder for future S3-compatible object storage.
/// </summary>
public interface IObjectStorage
{
    Task<string> UploadAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
}
