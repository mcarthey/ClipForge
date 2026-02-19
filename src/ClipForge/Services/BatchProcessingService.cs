using System.Text.Json;
using ClipForge.Data;
using ClipForge.Models;
using ClipForge.Models.DTOs;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ClipForge.Services;

public class BatchProcessingService
{
    private readonly ClipForgeDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<BatchProcessingService> _logger;

    public BatchProcessingService(
        ClipForgeDbContext context,
        IBackgroundJobClient backgroundJobClient,
        ILogger<BatchProcessingService> logger)
    {
        _context = context;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<BatchResultDto> ProcessBatchAsync(BatchProcessDto request, int userId)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.UserId == userId);

        if (template == null)
            throw new InvalidOperationException("Template not found.");

        var jobs = new List<ProcessingJob>();

        foreach (var contentVideoId in request.ContentVideoIds)
        {
            var asset = await _context.Assets
                .FirstOrDefaultAsync(a => a.Id == contentVideoId && a.UserId == userId);
            if (asset == null)
            {
                _logger.LogWarning("Asset {AssetId} not found for user {UserId}, skipping", contentVideoId, userId);
                continue;
            }

            var timeline = JsonSerializer.Deserialize<TimelineDefinition>(template.TimelineDefinition);
            if (timeline == null) continue;

            // Find content-placeholder and replace with actual video asset
            var placeholder = timeline.Segments
                .FirstOrDefault(s => s.Type == "content-placeholder");
            if (placeholder != null)
            {
                placeholder.AssetId = contentVideoId;
                placeholder.Type = "asset";
            }

            var project = new Project
            {
                UserId = userId,
                Name = $"Batch {DateTime.UtcNow:yyyy-MM-dd} - {asset.Filename}",
                TimelineDefinition = JsonSerializer.Serialize(timeline),
                Status = "Draft"
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            var job = new ProcessingJob
            {
                ProjectId = project.Id,
                UserId = userId,
                Platform = template.Platform,
                Status = "Queued"
            };

            _context.ProcessingJobs.Add(job);
            jobs.Add(job);
        }

        await _context.SaveChangesAsync();

        // Enqueue all jobs
        foreach (var job in jobs)
        {
            _backgroundJobClient.Enqueue<Jobs.VideoProcessingJob>(x =>
                x.ProcessVideoAsync(job.Id));
        }

        return new BatchResultDto
        {
            JobIds = jobs.Select(j => j.Id).ToList(),
            TotalCount = jobs.Count
        };
    }
}
