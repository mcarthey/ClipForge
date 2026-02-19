using System.Text.Json.Serialization;

namespace ClipForge.Models;

public class TimelineDefinition
{
    [JsonPropertyName("segments")]
    public List<TimelineSegment> Segments { get; set; } = new();

    [JsonPropertyName("outputSettings")]
    public OutputSettings OutputSettings { get; set; } = new();
}

public class TimelineSegment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // video, image, textSlide, asset, content-placeholder

    [JsonPropertyName("assetId")]
    public int? AssetId { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("overlayText")]
    public string? OverlayText { get; set; }

    [JsonPropertyName("overlayPosition")]
    public string? OverlayPosition { get; set; } // top-left, top-center, top-right, center, bottom-left, bottom-center, bottom-right

    [JsonPropertyName("overlayStyle")]
    public OverlayStyle? OverlayStyle { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; } // For textSlide type

    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}

public class OverlayStyle
{
    [JsonPropertyName("fontSize")]
    public int FontSize { get; set; } = 48;

    [JsonPropertyName("fontColor")]
    public string FontColor { get; set; } = "white";

    [JsonPropertyName("backgroundColor")]
    public string BackgroundColor { get; set; } = "black";

    [JsonPropertyName("backgroundOpacity")]
    public double BackgroundOpacity { get; set; } = 0.5;
}

public class OutputSettings
{
    [JsonPropertyName("resolution")]
    public string Resolution { get; set; } = "1080x1920";

    [JsonPropertyName("fps")]
    public int Fps { get; set; } = 30;

    [JsonPropertyName("videoBitrate")]
    public string VideoBitrate { get; set; } = "5000k";

    [JsonPropertyName("audioBitrate")]
    public string AudioBitrate { get; set; } = "192k";
}
