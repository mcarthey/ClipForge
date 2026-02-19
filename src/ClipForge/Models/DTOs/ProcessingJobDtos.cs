namespace ClipForge.Models.DTOs;

public class ProcessingJobDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime QueuedDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}

public class BatchDownloadDto
{
    public List<int> JobIds { get; set; } = new();
}
