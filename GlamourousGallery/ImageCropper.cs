using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;

namespace GlamourousGallery;

public static class ImageCropper
{
    public const float PortraitAspect = 2f / 3f;

    public static void SaveCroppedPng(string sourcePath, string outputPath, float zoom, Vector2 pan)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var source = Image.FromFile(sourcePath);

        var sourceAspect = source.Width / (float)source.Height;
        var safeZoom = MathF.Max(1f, zoom);
        var cropWidth = sourceAspect > PortraitAspect
            ? source.Height * PortraitAspect
            : source.Width;
        var cropHeight = cropWidth / PortraitAspect;
        cropWidth /= safeZoom;
        cropHeight /= safeZoom;

        var maxX = source.Width - cropWidth;
        var maxY = source.Height - cropHeight;
        var x = maxX * (1f + Math.Clamp(pan.X, -1f, 1f)) / 2f;
        var y = maxY * (1f - Math.Clamp(pan.Y, -1f, 1f)) / 2f;
        x = Math.Clamp(x, 0f, source.Width - cropWidth);
        y = Math.Clamp(y, 0f, source.Height - cropHeight);

        var outputWidth = Math.Max(1, (int)MathF.Round(cropWidth));
        var outputHeight = Math.Max(1, (int)MathF.Round(cropHeight));
        using var output = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(output);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var sourceRect = new RectangleF(x, y, cropWidth, cropHeight);
        var outputRect = new RectangleF(0, 0, outputWidth, outputHeight);
        graphics.DrawImage(source, outputRect, sourceRect, GraphicsUnit.Pixel);
        output.Save(outputPath, ImageFormat.Png);
    }
}
