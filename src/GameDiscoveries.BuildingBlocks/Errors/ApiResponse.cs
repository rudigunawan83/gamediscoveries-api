using System.Text.Json.Serialization;
using GameDiscoveries.BuildingBlocks.Pagination;

namespace GameDiscoveries.BuildingBlocks.Errors;

public sealed class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("error")]
    public ApiErrorPayload? Error { get; init; }

    [JsonPropertyName("meta")]
    public PaginationMeta? Meta { get; init; }

    public static ApiResponse<T> Ok(T data, PaginationMeta? meta = null) => new()
    {
        Success = true,
        Data = data,
        Error = null,
        Meta = meta
    };

    public static ApiResponse<T> Fail(ApiErrorPayload error) => new()
    {
        Success = false,
        Data = default,
        Error = error,
        Meta = null
    };
}

public sealed class ApiErrorPayload
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("status")]
    public required int Status { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }
}
