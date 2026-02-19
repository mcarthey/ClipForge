namespace ClipForge.Models.DTOs;

public class AssetDto
{
    public int Id { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public decimal? Duration { get; set; }
    public string? ThumbnailUrl { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadDate { get; set; }
}

public class UpdateTagsDto
{
    public List<string> Tags { get; set; } = new();
}

public class AssetFilterDto
{
    public string? Type { get; set; }
    public string? Search { get; set; }
    public string? Tag { get; set; }
}
