namespace ClipForge.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Template> Templates { get; set; } = new List<Template>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<ProcessingJob> ProcessingJobs { get; set; } = new List<ProcessingJob>();
}
