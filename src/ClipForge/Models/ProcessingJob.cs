namespace ClipForge.Models;

public class ProcessingJob
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int UserId { get; set; }
    public string Status { get; set; } = "Queued"; // Queued, Processing, Completed, Failed
    public string? Platform { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime QueuedDate { get; set; } = DateTime.UtcNow;
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
}
