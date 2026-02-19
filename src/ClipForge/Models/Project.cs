namespace ClipForge.Models;

public class Project
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimelineDefinition { get; set; } = string.Empty; // JSON
    public string Status { get; set; } = "Draft"; // Draft, Processing, Completed, Failed
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<ProcessingJob> ProcessingJobs { get; set; } = new List<ProcessingJob>();
}
