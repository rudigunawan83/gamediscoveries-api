using GameDiscoveries.BuildingBlocks.Errors;
using SkiaSharp;

namespace GameDiscoveries.Modules.Users.Features.Avatar;

/// <summary>
/// Re-encodes untrusted uploads into a square JPEG. Decoding and re-encoding
/// drops any non-image payload and all metadata (EXIF location, etc.).
/// </summary>
public static class AvatarImageProcessor
{
    /// <summary>Guards against decompression bombs before pixels are decoded.</summary>
    public const int MaxSourceDimension = 8000;

    public const long MaxSourcePixels = 40_000_000;

    private static readonly SKColor Background = SKColor.Parse("#0E0E14");

    public static async Task<byte[]> NormalizeAsync(Stream source, int size, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        return Normalize(buffer.ToArray(), size);
    }

    private static byte[] Normalize(byte[] bytes, int size)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null
            || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            throw Unsupported();
        }

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width <= 0 || height <= 0)
        {
            throw Unsupported();
        }

        if (width > MaxSourceDimension || height > MaxSourceDimension || (long)width * height > MaxSourcePixels)
        {
            throw new ValidationException($"Image must be at most {MaxSourceDimension}x{MaxSourceDimension} pixels.");
        }

        using var decoded = Decode(codec, size);
        using var oriented = ApplyOrigin(decoded, codec.EncodedOrigin);
        return CropToSquareJpeg(oriented, size);
    }

    private static SKBitmap Decode(SKCodec codec, int size)
    {
        // JPEG/WebP can decode at reduced scale, which keeps memory low for large photos.
        var scale = Math.Min(1f, size * 2f / Math.Min(codec.Info.Width, codec.Info.Height));
        var dimensions = codec.GetScaledDimensions(scale);
        var info = new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw Unsupported();
        }

        return bitmap;
    }

    private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // Same mapping as Skia's SkEncodedOriginToMatrix.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };

        var oriented = new SKBitmap(new SKImageInfo(
            swap ? source.Height : source.Width,
            swap ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType));
        using var canvas = new SKCanvas(oriented);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0);
        return oriented;
    }

    private static byte[] CropToSquareJpeg(SKBitmap source, int size)
    {
        var side = Math.Min(source.Width, source.Height);
        var sourceRect = SKRect.Create((source.Width - side) / 2f, (source.Height - side) / 2f, side, side);

        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Could not allocate avatar surface.");
        surface.Canvas.Clear(Background);
        using (var image = SKImage.FromBitmap(source))
        {
            surface.Canvas.DrawImage(
                image,
                sourceRect,
                SKRect.Create(size, size),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, 85)
            ?? throw new InvalidOperationException("Could not encode avatar.");
        return encoded.ToArray();
    }

    private static ValidationException Unsupported() =>
        new("Upload a JPG, PNG or WebP image.");
}
