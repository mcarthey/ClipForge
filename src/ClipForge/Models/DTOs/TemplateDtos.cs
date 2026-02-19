using System.ComponentModel.DataAnnotations;

namespace ClipForge.Models.DTOs;

public class CreateTemplateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Platform { get; set; }

    [Required]
    public TimelineDefinition Timeline { get; set; } = new();

    public bool IsDefault { get; set; }
}

public class UpdateTemplateDto
{
    public string? Name { get; set; }
    public string? Platform { get; set; }
    public TimelineDefinition? Timeline { get; set; }
    public bool? IsDefault { get; set; }
}

public class TemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public TimelineDefinition Timeline { get; set; } = new();
    public bool IsDefault { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastUsedDate { get; set; }
}
