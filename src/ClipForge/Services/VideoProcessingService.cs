using FFMpegCore;
using FFMpegCore.Enums;
using SkiaSharp;

namespace ClipForge.Services;

public class VideoProcessingService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoProcessingService> _logger;

    public VideoProcessingService(IConfiguration configuration, ILogger<VideoProcessingService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateTextSlideAsync(string text, int durationSeconds,
        string outputPath, int width = 1080, int height = 1920, string? backgroundColor = null)
    {
        var imageBytes = CreateTextImage(text, width, height, backgroundColor);
        var imagePath = Path.ChangeExtension(outputPath, ".png");
        await File.WriteAllBytesAsync(imagePath, imageBytes);

        try
        {
            await FFMpegArguments
                .FromFileInput(imagePath, false, options => options
                    .ForceFormat("image2")
                    .WithCustomArgument("-loop 1")
                    .WithDuration(TimeSpan.FromSeconds(durationSeconds)))
                .OutputToFile(outputPath, true, options => options
                    .WithVideoCodec("libx264")
                    .WithConstantRateFactor(23)
                    .WithCustomArgument("-pix_fmt yuv420p")
                    .WithFramerate(30))
                .ProcessAsynchronously();
        }
        finally
        {
            if (File.Exists(imagePath))
                File.Delete(imagePath);
        }

        return outputPath;
    }

    private byte[] CreateTextImage(string text, int width, int height, string? backgroundColor = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        var bgColor = SKColors.Black;
        if (!string.IsNullOrEmpty(backgroundColor))
        {
            SKColor.TryParse(backgroundColor, out bgColor);
        }
        canvas.Clear(bgColor);

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 72,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };

        var lines = WrapText(text, paint, width - 100);
        var lineHeight = paint.TextSize * 1.2f;
        var totalHeight = lines.Count * lineHeight;
        var y = (height - totalHeight) / 2 + paint.TextSize;

        foreach (var line in lines)
        {
            canvas.DrawText(line, width / 2f, y, paint);
            y += lineHeight;
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static List<string> WrapText(string text, SKPaint paint, float maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            var lineWidth = paint.MeasureText(testLine);

            if (lineWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }

    public async Task<string> AddTextOverlayAsync(string videoPath, string text,
        string position, string outputPath)
    {
        var (x, y) = GetOverlayPosition(position);

        // Escape special characters for FFmpeg drawtext
        var escapedText = text
            .Replace("\\", "\\\\")
            .Replace("'", "'\\''")
            .Replace(":", "\\:")
            .Replace("%", "\\%");

        await FFMpegArguments
            .FromFileInput(videoPath)
            .OutputToFile(outputPath, true, options => options
                .WithVideoCodec("libx264")
                .WithConstantRateFactor(23)
                .WithCustomArgument($"-vf \"drawtext=text='{escapedText}':fontsize=48:fontcolor=white:x={x}:y={y}:box=1:boxcolor=black@0.5:boxborderw=5\"")
                .WithCustomArgument("-pix_fmt yuv420p"))
            .ProcessAsynchronously();

        return outputPath;
    }

    public async Task<string> ConvertImageToVideoAsync(string imagePath, int durationSeconds,
        string outputPath, int width = 1080, int height = 1920)
    {
        // Resize image to target resolution first
        var resizedPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");

        try
        {
            using (var original = SKBitmap.Decode(imagePath))
            {
                using var resized = new SKBitmap(width, height);
                using var canvas = new SKCanvas(resized);
                canvas.Clear(SKColors.Black);

                var ratio = Math.Min((float)width / original.Width, (float)height / original.Height);
                var newW = (int)(original.Width * ratio);
                var newH = (int)(original.Height * ratio);
                var offsetX = (width - newW) / 2;
                var offsetY = (height - newH) / 2;

                canvas.DrawBitmap(original, new SKRect(offsetX, offsetY, offsetX + newW, offsetY + newH));

                using var image = SKImage.FromBitmap(resized);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                await using var stream = File.OpenWrite(resizedPath);
                data.SaveTo(stream);
            }

            await FFMpegArguments
                .FromFileInput(resizedPath, false, options => options
                    .ForceFormat("image2")
                    .WithCustomArgument("-loop 1")
                    .WithDuration(TimeSpan.FromSeconds(durationSeconds)))
                .OutputToFile(outputPath, true, options => options
                    .WithVideoCodec("libx264")
                    .WithConstantRateFactor(23)
                    .WithCustomArgument("-pix_fmt yuv420p")
                    .WithFramerate(30))
                .ProcessAsynchronously();
        }
        finally
        {
            if (File.Exists(resizedPath))
                File.Delete(resizedPath);
        }

        return outputPath;
    }

    public async Task<string> ConcatenateVideosAsync(List<string> videoPaths, string outputPath)
    {
        var concatFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");

        try
        {
            var lines = videoPaths.Select(v => $"file '{v}'");
            await File.WriteAllLinesAsync(concatFile, lines);

            await FFMpegArguments
                .FromFileInput(concatFile, false, options => options
                    .ForceFormat("concat")
                    .WithCustomArgument("-safe 0"))
                .OutputToFile(outputPath, true, options => options
                    .WithVideoCodec("libx264")
                    .WithCustomArgument("-acodec aac")
                    .WithConstantRateFactor(23)
                    .WithCustomArgument("-pix_fmt yuv420p"))
                .ProcessAsynchronously();
        }
        finally
        {
            if (File.Exists(concatFile))
                File.Delete(concatFile);
        }

        return outputPath;
    }

    public async Task<string> ResizeVideoAsync(string videoPath, string outputPath, int width, int height)
    {
        await FFMpegArguments
            .FromFileInput(videoPath)
            .OutputToFile(outputPath, true, options => options
                .WithVideoCodec("libx264")
                .WithConstantRateFactor(23)
                .WithCustomArgument($"-vf \"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2\"")
                .WithCustomArgument("-pix_fmt yuv420p"))
            .ProcessAsynchronously();

        return outputPath;
    }

    private static (string x, string y) GetOverlayPosition(string? position)
    {
        return position switch
        {
            "top-left" => ("50", "50"),
            "top-center" => ("(w-text_w)/2", "50"),
            "top-right" => ("w-text_w-50", "50"),
            "center" => ("(w-text_w)/2", "(h-text_h)/2"),
            "bottom-center" => ("(w-text_w)/2", "h-150"),
            "bottom-left" => ("50", "h-150"),
            "bottom-right" => ("w-text_w-50", "h-150"),
            _ => ("(w-text_w)/2", "h-150")
        };
    }
}
