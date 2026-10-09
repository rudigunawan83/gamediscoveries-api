using FluentAssertions;
using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Users.Features.Avatar;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace GameDiscoveries.UnitTests.Users;

public sealed class AvatarImageProcessorTests
{
    private const string OneByOneGif = "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

    [Theory]
    [InlineData(SKEncodedImageFormat.Png, 800, 400)]
    [InlineData(SKEncodedImageFormat.Webp, 300, 900)]
    [InlineData(SKEncodedImageFormat.Jpeg, 4000, 3000)]
    public async Task Normalizes_supported_formats_to_square_jpeg(SKEncodedImageFormat format, int width, int height)
    {
        using var source = new MemoryStream(Encode(format, width, height, SKColors.MediumPurple, SKColors.MediumPurple));

        var result = await AvatarImageProcessor.NormalizeAsync(source, 512);

        using var codec = SKCodec.Create(new MemoryStream(result));
        codec.EncodedFormat.Should().Be(SKEncodedImageFormat.Jpeg);
        codec.Info.Width.Should().Be(512);
        codec.Info.Height.Should().Be(512);
    }

    [Fact]
    public async Task Applies_exif_orientation_and_strips_metadata()
    {
        // Top half red, bottom half blue; EXIF orientation 6 rotates 90° clockwise,
        // so red must end up on the right.
        var jpeg = WithExifOrientation(Encode(SKEncodedImageFormat.Jpeg, 600, 300, SKColors.Red, SKColors.Blue), 6);

        var result = await AvatarImageProcessor.NormalizeAsync(new MemoryStream(jpeg), 512);

        using var bitmap = SKBitmap.Decode(result);
        bitmap.GetPixel(100, 256).Blue.Should().BeGreaterThan(200);
        bitmap.GetPixel(412, 256).Red.Should().BeGreaterThan(200);
        System.Text.Encoding.ASCII.GetString(result).Should().NotContain("Exif");
    }

    [Fact]
    public async Task Rejects_non_image_content()
    {
        using var source = new MemoryStream("<svg onload=alert(1)>"u8.ToArray());

        var act = () => AvatarImageProcessor.NormalizeAsync(source, 512);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Rejects_unsupported_image_format()
    {
        using var source = new MemoryStream(Convert.FromBase64String(OneByOneGif));

        var act = () => AvatarImageProcessor.NormalizeAsync(source, 512);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Rejects_oversized_dimensions_before_decoding()
    {
        var png = Encode(SKEncodedImageFormat.Png, AvatarImageProcessor.MaxSourceDimension + 1, 1, SKColors.Red, SKColors.Red);

        var act = () => AvatarImageProcessor.NormalizeAsync(new MemoryStream(png), 512);

        await act.Should().ThrowAsync<ValidationException>();
    }

    private static byte[] Encode(SKEncodedImageFormat format, int width, int height, SKColor top, SKColor bottom)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        using (var paint = new SKPaint { Color = top })
        {
            surface.Canvas.DrawRect(0, 0, width, height / 2f, paint);
        }

        using (var paint = new SKPaint { Color = bottom })
        {
            surface.Canvas.DrawRect(0, height / 2f, width, height / 2f, paint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(format, 95);
        return data.ToArray();
    }

    private static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, (byte)(orientation >> 8), (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }
}

public sealed class AvatarStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gd-avatar-tests-{Guid.NewGuid():N}");
    private readonly AvatarStorage _storage;

    public AvatarStorageTests()
    {
        _storage = new AvatarStorage(
            Options.Create(new AvatarOptions { RootPath = _root }),
            new TestEnvironment(_root));
    }

    [Fact]
    public async Task Saves_and_resolves_stored_file()
    {
        var userId = Guid.NewGuid();

        var url = await _storage.SaveAsync(userId, [1, 2, 3]);

        url.Should().StartWith($"{AvatarStorage.RoutePrefix}/{userId:N}/");
        var fileName = url[(url.LastIndexOf('/') + 1)..];
        _storage.TryGetFilePath(userId, fileName).Should().NotBeNull();
    }

    [Theory]
    [InlineData("../secret.jpg")]
    [InlineData("..\\secret.jpg")]
    [InlineData("avatar.png")]
    [InlineData("0123456789abcdef0123456789abcdef.jpg.exe")]
    public void Rejects_unsafe_file_names(string fileName)
    {
        _storage.TryGetFilePath(Guid.NewGuid(), fileName).Should().BeNull();
    }

    [Fact]
    public async Task Deletes_only_files_owned_by_the_user()
    {
        var owner = Guid.NewGuid();
        var url = "https://api.example.com" + await _storage.SaveAsync(owner, [1]);
        var fileName = url[(url.LastIndexOf('/') + 1)..];

        _storage.DeleteOwned(Guid.NewGuid(), url);
        _storage.TryGetFilePath(owner, fileName).Should().NotBeNull();

        _storage.DeleteOwned(owner, url);
        _storage.TryGetFilePath(owner, fileName).Should().BeNull();
    }

    [Fact]
    public void Ignores_external_avatar_urls()
    {
        var act = () => _storage.DeleteOwned(Guid.NewGuid(), "https://cdn.example.com/a.png");

        act.Should().NotThrow();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
