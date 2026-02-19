using System.Text.Json;
using ClipForge.Data;
using ClipForge.Models;
using ClipForge.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ClipForge.Services;

public class TemplateService
{
    private readonly ClipForgeDbContext _context;

    public TemplateService(ClipForgeDbContext context)
    {
        _context = context;
    }

    public async Task<TemplateDto> CreateTemplateAsync(CreateTemplateDto dto, int userId)
    {
        if (dto.IsDefault)
            await UnsetOtherDefaultsAsync(userId, dto.Platform);

        var template = new Template
        {
            UserId = userId,
            Name = dto.Name,
            Platform = dto.Platform,
            TimelineDefinition = JsonSerializer.Serialize(dto.Timeline),
            IsDefault = dto.IsDefault,
            CreatedDate = DateTime.UtcNow
        };

        _context.Templates.Add(template);
        await _context.SaveChangesAsync();
        return MapToDto(template);
    }

    public async Task<List<TemplateDto>> GetTemplatesAsync(int userId, string? platform = null)
    {
        var query = _context.Templates.Where(t => t.UserId == userId);
        if (!string.IsNullOrEmpty(platform))
            query = query.Where(t => t.Platform == platform);

        return await query.OrderByDescending(t => t.CreatedDate)
            .Select(t => MapToDto(t))
            .ToListAsync();
    }

    public async Task<TemplateDto?> GetTemplateAsync(int id, int userId)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        return template != null ? MapToDto(template) : null;
    }

    public async Task<TemplateDto?> UpdateTemplateAsync(int id, UpdateTemplateDto dto, int userId)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (template == null) return null;

        if (dto.Name != null) template.Name = dto.Name;
        if (dto.Platform != null) template.Platform = dto.Platform;
        if (dto.Timeline != null) template.TimelineDefinition = JsonSerializer.Serialize(dto.Timeline);
        if (dto.IsDefault.HasValue)
        {
            if (dto.IsDefault.Value)
                await UnsetOtherDefaultsAsync(userId, template.Platform);
            template.IsDefault = dto.IsDefault.Value;
        }

        await _context.SaveChangesAsync();
        return MapToDto(template);
    }

    public async Task<bool> DeleteTemplateAsync(int id, int userId)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (template == null) return false;

        _context.Templates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task UnsetOtherDefaultsAsync(int userId, string? platform)
    {
        var defaults = await _context.Templates
            .Where(t => t.UserId == userId && t.Platform == platform && t.IsDefault)
            .ToListAsync();

        foreach (var t in defaults)
            t.IsDefault = false;
    }

    private static TemplateDto MapToDto(Template template)
    {
        return new TemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Platform = template.Platform,
            Timeline = JsonSerializer.Deserialize<TimelineDefinition>(template.TimelineDefinition) ?? new(),
            IsDefault = template.IsDefault,
            CreatedDate = template.CreatedDate,
            LastUsedDate = template.LastUsedDate
        };
    }
}
