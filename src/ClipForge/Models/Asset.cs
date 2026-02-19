namespace ClipForge.Models;

public class Asset
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Video, Image, Audio
    public string? Tags { get; set; } // JSON array
    public decimal? Duration { get; set; } // seconds, for videos
    public string? ThumbnailPath { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
