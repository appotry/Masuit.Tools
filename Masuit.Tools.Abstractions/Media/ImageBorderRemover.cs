using Masuit.Tools.Systems;
using SkiaSharp;
using Color = System.Drawing.Color;

// ReSharper disable AccessToDisposedClosure

namespace Masuit.Tools.Media;

/// <summary>
/// 图像边框移除器
/// </summary>
public class ImageBorderRemover
{
    /// <summary>
    /// 容差模式
    /// </summary>
    private ToleranceMode ToleranceMode { get; }

    private int CroppedBorderCount { get; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="mode">容差模式</param>
    /// <param name="croppedBorderCount">达到边框个数则裁剪</param>
    public ImageBorderRemover(ToleranceMode mode, int croppedBorderCount = 2)
    {
        ToleranceMode = mode;
        CroppedBorderCount = croppedBorderCount;
    }

    /// <summary>
    /// 检测图片边框信息（支持多色边框）
    /// </summary>
    /// <param name="imagePath">图片路径</param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>边框检测结果</returns>
    public BorderDetectionResult DetectBorders(string imagePath, int tolerance, int maxLayers = 3, bool useDownscaling = false, int downscaleFactor = 4)
    {
        using var image = SKBitmap.Decode(imagePath);
        return DetectBorders(image, tolerance, maxLayers, useDownscaling, downscaleFactor);
    }

    /// <summary>
    /// 检测图片边框信息（从已加载的图像）
    /// </summary>
    /// <param name="image">已加载的图像</param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>边框检测结果</returns>
    public BorderDetectionResult DetectBorders(SKBitmap image, int tolerance, int maxLayers = 3, bool useDownscaling = false, int downscaleFactor = 4)
    {
        var result = new BorderDetectionResult(CroppedBorderCount)
        {
            ImageWidth = image.Width,
            ImageHeight = image.Height,
            BorderColors = new List<SKColor>(),
            BorderLayers = 0
        };

        byte toleranceValue = (byte) (tolerance * 2.55);

        using var clone = ToGrayscaleBitmap(image);
        var (top, bottom, left, right, layers, colors) = FindContentBordersWithLayers(clone, toleranceValue, maxLayers, useDownscaling, downscaleFactor);

        // 设置内容边界
        result.ContentTop = top;
        result.ContentBottom = bottom;
        result.ContentLeft = left;
        result.ContentRight = right;
        result.BorderLayers = layers;
        result.BorderColors = colors;
        return result;
    }

    /// <summary>
    /// 自动移除图片的多层边框
    /// </summary>
    /// <param name="inputPath">输入图片路径</param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>是否执行了裁剪操作</returns>
    public void RemoveBorders(string inputPath, int tolerance, int maxLayers = 3, bool useDownscaling = false, int downscaleFactor = 4)
    {
        RemoveBorders(inputPath, inputPath, tolerance, maxLayers, useDownscaling, downscaleFactor);
    }

    /// <summary>
    /// 自动移除图片的多层边框
    /// </summary>
    /// <param name="inputPath">输入图片路径</param>
    /// <param name="outputPath">输出图片路径</param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>是否执行了裁剪操作</returns>
    public void RemoveBorders(string inputPath, string outputPath, int tolerance, int maxLayers = 3, bool useDownscaling = false, int downscaleFactor = 4)
    {
        using var image = SKBitmap.Decode(inputPath);
        var cropped = RemoveBorders(image, tolerance, maxLayers, useDownscaling, downscaleFactor);
        if (cropped != null)
        {
            using (cropped)
            {
                using var data = cropped.Encode(SKEncodedImageFormat.Png, 90);
                using var fs = File.OpenWrite(outputPath);
                data.SaveTo(fs);
            }
        }
    }

    /// <summary>
    /// 自动移除图片的多层边框
    /// </summary>
    /// <param name="input">输入图片路径</param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>是否执行了裁剪操作</returns>
    public PooledMemoryStream RemoveBorders(Stream input, int tolerance, int maxLayers = 3, bool useDownscaling = false, int downscaleFactor = 4)
    {
        var detectedFormat = input.GetImageType();
        input.Seek(0, SeekOrigin.Begin);
        using var codec = SKCodec.Create(input);
        using var image = SKBitmap.Decode(codec);
        var cropped = RemoveBorders(image, tolerance, maxLayers, useDownscaling, downscaleFactor);
        var bitmapToSave = cropped ?? image;
        var stream = new PooledMemoryStream();
        var format = detectedFormat switch
        {
            ImageFormat.Jpg => SKEncodedImageFormat.Jpeg,
            ImageFormat.Png => SKEncodedImageFormat.Png,
            ImageFormat.Gif => SKEncodedImageFormat.Gif,
            ImageFormat.Bmp => SKEncodedImageFormat.Bmp,
            ImageFormat.WebP => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png,
        };
        using (var data = bitmapToSave.Encode(format, 90))
        {
            data.SaveTo(stream);
        }

        cropped?.Dispose();
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// 移除边框并返回新的裁剪后的 SKBitmap（如未裁剪则返回 null）
    /// </summary>
    /// <param name="image"></param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>是否执行了裁剪操作</returns>
    public SKBitmap RemoveBorders(SKBitmap image, int tolerance, int maxLayers, bool useDownscaling, int downscaleFactor)
    {
        // 保存原始尺寸用于比较
        int originalWidth = image.Width;
        int originalHeight = image.Height;

        // 使用多层检测方法获取边框信息
        var borderInfo = DetectBorders(image, tolerance, maxLayers, useDownscaling, downscaleFactor);

        if (borderInfo.CanBeCropped)
        {
            int newWidth = borderInfo.ContentRight - borderInfo.ContentLeft + 1;
            int newHeight = borderInfo.ContentBottom - borderInfo.ContentTop + 1;
            if (newWidth > 0 && newHeight > 0 && (newWidth != originalWidth || newHeight != originalHeight))
            {
                var dest = new SKBitmap(newWidth, newHeight);
                image.ExtractSubset(dest, new SKRectI(borderInfo.ContentLeft, borderInfo.ContentTop, borderInfo.ContentLeft + newWidth, borderInfo.ContentTop + newHeight));
                return dest;
            }
        }

        return null;
    }

    /// <summary>
    /// 查找内容边界（支持多层边框检测）
    /// </summary>
    private (int top, int bottom, int left, int right, int layers, List<SKColor> colors) FindContentBordersWithLayers(SKBitmap image, byte tolerance, int maxLayers, bool useDownscaling, int downscaleFactor)
    {
        SKBitmap workingImage;
        float scale = 1f;
        bool isDownscaled = false;

        if (useDownscaling && image.Width > 500 && image.Height > 500)
        {
            int newWidth = image.Width / downscaleFactor;
            int newHeight = image.Height / downscaleFactor;
            scale = (float) image.Width / newWidth;
            workingImage = image.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest));
            isDownscaled = true;
        }
        else
        {
            workingImage = image;
        }

        int width = workingImage.Width;
        int height = workingImage.Height;
        int top = 0, bottom = height - 1, left = 0, right = width - 1;
        int layers = 0;
        var borderColors = new List<SKColor>();

        // 检测多层边框
        for (int layer = 0; layer < maxLayers; layer++)
        {
            bool borderFound = false;
            var results = new (int borderSize, SKColor? color)[4];

            Parallel.Invoke(() =>
            {
                if (top < height / 2)
                {
                    SKColor? layerColor = null;
                    int newTop = DetectLayerBorderTop(workingImage, top, bottom, left, right, tolerance, ref layerColor);
                    results[0] = (newTop - top, layerColor);
                    if (newTop > top) borderFound = true;
                    top = newTop;
                }
            }, () =>
            {
                if (bottom > height / 2)
                {
                    SKColor? layerColor = null;
                    int newBottom = DetectLayerBorderBottom(workingImage, top, bottom, left, right, tolerance, ref layerColor);
                    results[1] = (newBottom - bottom, layerColor);
                    if (newBottom < bottom) borderFound = true;
                    bottom = newBottom;
                }
            }, () =>
            {
                if (left < width / 2)
                {
                    SKColor? layerColor = null;
                    int newLeft = DetectLayerBorderLeft(workingImage, top, bottom, left, right, tolerance, ref layerColor);
                    results[2] = (newLeft - left, layerColor);
                    if (newLeft > left) borderFound = true;
                    left = newLeft;
                }
            }, () =>
            {
                if (right > width / 2)
                {
                    SKColor? layerColor = null;
                    int newRight = DetectLayerBorderRight(workingImage, top, bottom, left, right, tolerance, ref layerColor);
                    results[3] = (newRight - right, layerColor);
                    if (newRight < right) borderFound = true;
                    right = newRight;
                }
            });

            // 收集检测到的边框颜色
            foreach (var (borderSize, color) in results)
            {
                if (color.HasValue && borderSize > 0)
                {
                    borderColors.Add(color.Value);
                }
            }

            if (borderFound)
            {
                layers++;
            }
            else
            {
                break; // 没有检测到更多边框层
            }
        }

        // 如果是缩小采样版本，映射回原图坐标
        if (isDownscaled)
        {
            top = (int) (top * scale);
            bottom = (int) (bottom * scale);
            left = (int) (left * scale);
            right = (int) (right * scale);

            // 确保边界在图像范围内
            top = Clamp(top, 0, image.Height - 1);
            bottom = Clamp(bottom, top, image.Height - 1);
            left = Clamp(left, 0, image.Width - 1);
            right = Clamp(right, left, image.Width - 1);

            // 释放缩小图像
            workingImage.Dispose();
        }

        return (top, bottom, left, right, layers, borderColors);
    }

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    /// <summary>
    /// 检测顶部边框层（优化版）
    /// </summary>
    private int DetectLayerBorderTop(SKBitmap image, int currentTop, int currentBottom, int currentLeft, int currentRight, byte tolerance, ref SKColor? borderColor)
    {
        int newTop = currentTop;
        SKColor? detectedColor = null;
        int sampleCount = Math.Min(50, currentRight - currentLeft + 1);
        int stepX = Math.Max(1, (currentRight - currentLeft) / sampleCount);

        // 从当前顶部开始向下扫描
        for (int y = currentTop; y <= currentBottom; y++)
        {
            SKColor? rowColor = null;
            bool isUniform = true;

            // 采样检查行是否统一颜色
            for (int x = currentLeft; x <= currentRight; x += stepX)
            {
                var px = image.GetPixel(x, y);
                if (!rowColor.HasValue)
                {
                    rowColor = px;
                    continue;
                }

                if (!IsSimilarColor(px, rowColor.Value, tolerance))
                {
                    isUniform = false;
                    break;
                }
            }

            // 如果是统一颜色行
            if (isUniform && rowColor.HasValue)
            {
                // 第一行总是被认为是边框
                if (y == currentTop)
                {
                    detectedColor = rowColor;
                    newTop = y + 1;
                    continue;
                }

                // 后续行必须与第一行颜色相似
                if (detectedColor.HasValue && IsSimilarColor(rowColor.Value, detectedColor.Value, tolerance))
                {
                    newTop = y + 1;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        if (newTop > currentTop)
        {
            borderColor = detectedColor;
            return newTop;
        }

        return currentTop;
    }

    /// <summary>
    /// 检测底部边框层（优化版）
    /// </summary>
    private int DetectLayerBorderBottom(SKBitmap image, int currentTop, int currentBottom, int currentLeft, int currentRight, byte tolerance, ref SKColor? borderColor)
    {
        int newBottom = currentBottom;
        SKColor? detectedColor = null;
        int sampleCount = Math.Min(50, currentRight - currentLeft + 1);
        int stepX = Math.Max(1, (currentRight - currentLeft) / sampleCount);

        for (int y = currentBottom; y >= currentTop; y--)
        {
            SKColor? rowColor = null;
            bool isUniform = true;
            for (int x = currentLeft; x <= currentRight; x += stepX)
            {
                var px = image.GetPixel(x, y);
                if (!rowColor.HasValue)
                {
                    rowColor = px;
                    continue;
                }

                if (!IsSimilarColor(px, rowColor.Value, tolerance))
                {
                    isUniform = false;
                    break;
                }
            }

            if (isUniform && rowColor.HasValue)
            {
                if (y == currentBottom)
                {
                    detectedColor = rowColor;
                    newBottom = y - 1;
                    continue;
                }

                if (detectedColor.HasValue && IsSimilarColor(rowColor.Value, detectedColor.Value, tolerance))
                {
                    newBottom = y - 1;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        if (newBottom < currentBottom)
        {
            borderColor = detectedColor;
            return newBottom;
        }

        return currentBottom;
    }

    /// <summary>
    /// 检测左侧边框层（优化版）
    /// </summary>
    private int DetectLayerBorderLeft(SKBitmap image, int currentTop, int currentBottom, int currentLeft, int currentRight, byte tolerance, ref SKColor? borderColor)
    {
        int newLeft = currentLeft;
        SKColor? detectedColor = null;
        int sampleCount = Math.Min(50, currentBottom - currentTop + 1);
        int stepY = Math.Max(1, (currentBottom - currentTop) / sampleCount);

        for (int x = currentLeft; x <= currentRight; x++)
        {
            SKColor? colColor = null;
            bool isUniform = true;
            for (int y = currentTop; y <= currentBottom; y += stepY)
            {
                var px = image.GetPixel(x, y);
                if (!colColor.HasValue)
                {
                    colColor = px;
                    continue;
                }

                if (!IsSimilarColor(px, colColor.Value, tolerance))
                {
                    isUniform = false;
                    break;
                }
            }

            if (isUniform && colColor.HasValue)
            {
                if (x == currentLeft)
                {
                    detectedColor = colColor;
                    newLeft = x + 1;
                    continue;
                }

                if (detectedColor.HasValue && IsSimilarColor(colColor.Value, detectedColor.Value, tolerance))
                {
                    newLeft = x + 1;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        if (newLeft > currentLeft)
        {
            borderColor = detectedColor;
            return newLeft;
        }

        return currentLeft;
    }

    /// <summary>
    /// 检测右侧边框层（优化版）
    /// </summary>
    private int DetectLayerBorderRight(SKBitmap image, int currentTop, int currentBottom, int currentLeft, int currentRight, byte tolerance, ref SKColor? borderColor)
    {
        int newRight = currentRight;
        SKColor? detectedColor = null;
        int sampleCount = Math.Min(50, currentBottom - currentTop + 1);
        int stepY = Math.Max(1, (currentBottom - currentTop) / sampleCount);

        for (int x = currentRight; x >= currentLeft; x--)
        {
            SKColor? colColor = null;
            bool isUniform = true;
            for (int y = currentTop; y <= currentBottom; y += stepY)
            {
                var px = image.GetPixel(x, y);
                if (!colColor.HasValue)
                {
                    colColor = px;
                    continue;
                }

                if (!IsSimilarColor(px, colColor.Value, tolerance))
                {
                    isUniform = false;
                    break;
                }
            }

            if (isUniform && colColor.HasValue)
            {
                if (x == currentRight)
                {
                    detectedColor = colColor;
                    newRight = x - 1;
                    continue;
                }

                if (detectedColor.HasValue && IsSimilarColor(colColor.Value, detectedColor.Value, tolerance))
                {
                    newRight = x - 1;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        if (newRight < currentRight)
        {
            borderColor = detectedColor;
            return newRight;
        }

        return currentRight;
    }

    /// <summary>
    /// 颜色相似度比较（SIMD优化）
    /// </summary>
    private bool IsSimilarColor(SKColor color1, SKColor color2, byte tolerance)
    {
        switch (ToleranceMode)
        {
            case ToleranceMode.Channel:
                return CompareColors(color1, color2, tolerance, true);
            case ToleranceMode.DeltaE2000:
                return Color.FromArgb(color1.Alpha, color1.Red, color1.Green, color1.Blue).CIE2000(Color.FromArgb(color2.Alpha, color2.Red, color2.Green, color2.Blue)) <= tolerance;
            case ToleranceMode.DeltaE1976:
                return Color.FromArgb(color1.Alpha, color1.Red, color1.Green, color1.Blue).CIE1976(Color.FromArgb(color2.Alpha, color2.Red, color2.Green, color2.Blue)) <= tolerance;
            case ToleranceMode.DeltaE1994:
                return Color.FromArgb(color1.Alpha, color1.Red, color1.Green, color1.Blue).CIE1994(Color.FromArgb(color2.Alpha, color2.Red, color2.Green, color2.Blue)) <= tolerance;
            case ToleranceMode.DeltaECMC:
                return Color.FromArgb(color1.Alpha, color1.Red, color1.Green, color1.Blue).CMC(Color.FromArgb(color2.Alpha, color2.Red, color2.Green, color2.Blue)) <= tolerance;
            case ToleranceMode.EuclideanDistance:
                return CompareWithEuclideanDistance(color1, color2, tolerance, true);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// 比较两个颜色是否在容差范围内相等
    /// </summary>
    /// <param name="color1">第一个颜色</param>
    /// <param name="color2">第二个颜色</param>
    /// <param name="tolerance">容差值 (0-255)</param>
    /// <param name="compareAlpha">是否比较Alpha通道</param>
    /// <returns>是否匹配</returns>
    private static bool CompareColors(SKColor color1, SKColor color2, int tolerance = 10, bool compareAlpha = false)
    {
        if (Math.Abs(color1.Red - color2.Red) > tolerance) return false;
        if (Math.Abs(color1.Green - color2.Green) > tolerance) return false;
        if (Math.Abs(color1.Blue - color2.Blue) > tolerance) return false;
        if (compareAlpha && Math.Abs(color1.Alpha - color2.Alpha) > tolerance) return false;
        return true;
    }

    /// <summary>
    /// 使用欧几里得距离比较颜色
    /// </summary>
    /// <param name="color1">第一个颜色</param>
    /// <param name="color2">第二个颜色</param>
    /// <param name="maxDistance">最大允许距离 (0-442之间)</param>
    /// <param name="compareAlpha">是否包含Alpha通道</param>
    /// <returns>是否匹配</returns>
    private static bool CompareWithEuclideanDistance(SKColor color1, SKColor color2, double maxDistance = 20.0, bool compareAlpha = false)
    {
        double sum = Math.Pow(color1.Red - color2.Red, 2) + Math.Pow(color1.Green - color2.Green, 2) + Math.Pow(color1.Blue - color2.Blue, 2);
        if (compareAlpha) sum += Math.Pow(color1.Alpha - color2.Alpha, 2);
        return Math.Sqrt(sum) <= maxDistance;
    }

    private static SKBitmap ToGrayscaleBitmap(SKBitmap source)
    {
        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        var gray = new SKBitmap(info);
        source.ScalePixels(gray, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        return gray;
    }
}

public static class ImageBorderRemoverExt
{
    /// <summary>
    /// 检测图片边框信息（从已加载的图像）
    /// </summary>
    /// <param name="image">已加载的图像</param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="toleranceMode">容差模式</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>边框检测结果</returns>
    public static BorderDetectionResult DetectBorders(this SKBitmap image, int tolerance, ToleranceMode toleranceMode, int maxLayers = 3, bool useDownscaling = false, int downscaleFactor = 4)
    {
        var remover = new ImageBorderRemover(toleranceMode);
        return remover.DetectBorders(image, tolerance, maxLayers, useDownscaling, downscaleFactor);
    }

    /// <summary>
    /// 自动移除图片的多层边框（仅当至少有两边存在边框时才裁剪）
    /// </summary>
    /// <param name="image"></param>
    /// <param name="tolerance">颜色容差(0-100)，通道模式建议10，ΔE模式建议1-10，欧几里德模式建议(0-442之间)</param>
    /// <param name="toleranceMode">容差模式</param>
    /// <param name="maxLayers">最大检测边框层数，默认3</param>
    /// <param name="cropBorderCount">最少边框数</param>
    /// <param name="useDownscaling">是否使用缩小采样优化性能，默认false，开启可能会导致图片过多裁剪</param>
    /// <param name="downscaleFactor">缩小采样比例(1-10)，默认4</param>
    /// <returns>是否执行了裁剪操作</returns>
    public static SKBitmap RemoveBorders(this SKBitmap image, int tolerance, ToleranceMode toleranceMode, int maxLayers = 3, int cropBorderCount = 2, bool useDownscaling = false, int downscaleFactor = 4)
    {
        var remover = new ImageBorderRemover(toleranceMode, cropBorderCount);
        return remover.RemoveBorders(image, tolerance, maxLayers, useDownscaling, downscaleFactor);
    }
}