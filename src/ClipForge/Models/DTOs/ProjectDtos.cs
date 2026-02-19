using System.ComponentModel.DataAnnotations;

namespace ClipForge.Models.DTOs;

public class CreateProjectDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public TimelineDefinition Timeline { get; set; } = new();
}

public class CreateFromTemplateDto
{
    [Required]
    public int TemplateId { get; set; }

    public string? Name { get; set; }
}

public class UpdateProjectDto
{
    public string? Name { get; set; }
    public TimelineDefinition? Timeline { get; set; }
}

public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimelineDefinition Timeline { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public List<ProcessingJobDto> Jobs { get; set; } = new();
}

public class BatchProcessDto
{
    [Required]
    public List<int> ContentVideoIds { get; set; } = new();

    [Required]
    public int TemplateId { get; set; }
}

public class BatchResultDto
{
    public List<int> JobIds { get; set; } = new();
    public int TotalCount { get; set; }
}
