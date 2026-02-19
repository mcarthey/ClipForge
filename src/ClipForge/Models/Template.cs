namespace ClipForge.Models;

public class Template
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Platform { get; set; } // YouTube, TikTok, Instagram, Generic
    public string TimelineDefinition { get; set; } = string.Empty; // JSON
    public bool IsDefault { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedDate { get; set; }

    public User User { get; set; } = null!;
}
