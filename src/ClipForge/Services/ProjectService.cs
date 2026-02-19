using System.Text.Json;
using ClipForge.Data;
using ClipForge.Models;
using ClipForge.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ClipForge.Services;

public class ProjectService
{
    private readonly ClipForgeDbContext _context;

    public ProjectService(ClipForgeDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto dto, int userId)
    {
        var project = new Project
        {
            UserId = userId,
            Name = dto.Name,
            TimelineDefinition = JsonSerializer.Serialize(dto.Timeline),
            Status = "Draft",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return MapToDto(project);
    }

    public async Task<ProjectDto?> CreateFromTemplateAsync(CreateFromTemplateDto dto, int userId)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(t => t.Id == dto.TemplateId && t.UserId == userId);
        if (template == null) return null;

        template.LastUsedDate = DateTime.UtcNow;

        var project = new Project
        {
            UserId = userId,
            Name = dto.Name ?? $"From {template.Name} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            TimelineDefinition = template.TimelineDefinition,
            Status = "Draft",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return MapToDto(project);
    }

    public async Task<List<ProjectDto>> GetProjectsAsync(int userId)
    {
        return await _context.Projects
            .Where(p => p.UserId == userId)
            .Include(p => p.ProcessingJobs)
            .OrderByDescending(p => p.ModifiedDate)
            .Select(p => MapToDto(p))
            .ToListAsync();
    }

    public async Task<ProjectDto?> GetProjectAsync(int id, int userId)
    {
        var project = await _context.Projects
            .Include(p => p.ProcessingJobs)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        return project != null ? MapToDto(project) : null;
    }

    public async Task<ProjectDto?> UpdateProjectAsync(int id, UpdateProjectDto dto, int userId)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return null;

        if (dto.Name != null) project.Name = dto.Name;
        if (dto.Timeline != null) project.TimelineDefinition = JsonSerializer.Serialize(dto.Timeline);
        project.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(project);
    }

    public async Task<bool> DeleteProjectAsync(int id, int userId)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return false;

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();
        return true;
    }

    public static ProjectDto MapToDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Timeline = JsonSerializer.Deserialize<TimelineDefinition>(project.TimelineDefinition) ?? new(),
            Status = project.Status,
            CreatedDate = project.CreatedDate,
            ModifiedDate = project.ModifiedDate,
            Jobs = project.ProcessingJobs?.Select(j => new ProcessingJobDto
            {
                Id = j.Id,
                ProjectId = j.ProjectId,
                Status = j.Status,
                Platform = j.Platform,
                ErrorMessage = j.ErrorMessage,
                QueuedDate = j.QueuedDate,
                StartedDate = j.StartedDate,
                CompletedDate = j.CompletedDate
            }).ToList() ?? new()
        };
    }
}
