using System.IO;
using System.IO.Compression;
using Masuit.Tools.Media;
using SkiaSharp;
using Xunit;

namespace Masuit.Tools.Abstractions.Test.Media;

public class ImageDetectExtTests
{
    private static MemoryStream CreateJpegStream()
    {
        using var bitmap = new SKBitmap(1, 1);
        var ms = new MemoryStream();
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90);
        data.SaveTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    [Fact]
    public void IsImage_ShouldReturnTrueForValidImageFile()
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "test.jpg");
        using var bitmap = new SKBitmap(1, 1);
        using (var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90))
        using (var fs = File.OpenWrite(filePath)) data.SaveTo(fs);

        var fileInfo = new FileInfo(filePath);
        var result = fileInfo.IsImage();

        Assert.True(result);
        File.Delete(filePath);
    }

    [Fact]
    public void IsImage_ShouldReturnTrueForValidImageStream()
    {
        using var ms = CreateJpegStream();
        var result = ms.IsImage();
        Assert.True(result);
    }

    [Fact]
    public void IsImage_ShouldReturnFalseForInvalidImageStream()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);
        writer.Write("This is a test stream.");
        writer.Flush();
        ms.Seek(0, SeekOrigin.Begin);

        var result = ms.IsImage();
        Assert.False(result);
    }

    [Fact]
    public void GetImageType_ShouldReturnCorrectImageFormat()
    {
        using var ms = CreateJpegStream();
        var result = ms.GetImageType();
        Assert.Equal(ImageFormat.Jpg, result);
    }

    [Fact]
    public void GetImageType_ShouldReturnNullForInvalidImageStream()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);
        writer.Write("This is a test stream.");
        writer.Flush();
        ms.Seek(0, SeekOrigin.Begin);

        var result = ms.GetImageType();
        Assert.Null(result);
    }
}
