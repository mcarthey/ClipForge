using System.Text.Json;
using ClipForge.Configuration;
using ClipForge.Data;
using ClipForge.Hubs;
using ClipForge.Models;
using ClipForge.Services;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ClipForge.Jobs;

public class VideoProcessingJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VideoProcessingService _videoService;
    private readonly IHubContext<ProcessingHub, IProcessingHubClient> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VideoProcessingJob> _logger;

    public VideoProcessingJob(
        IServiceScopeFactory scopeFactory,
        VideoProcessingService videoService,
        IHubContext<ProcessingHub, IProcessingHubClient> hubContext,
        IConfiguration configuration,
        ILogger<VideoProcessingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _videoService = videoService;
        _hubContext = hubContext;
        _configuration = configuration;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ProcessVideoAsync(int jobId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClipForgeDbContext>();

        var job = await context.ProcessingJobs
            .Include(j => j.Project)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job == null)
        {
            _logger.LogError("Processing job {JobId} not found", jobId);
            return;
        }

        var tempFiles = new List<string>();

        try
        {
            job.Status = "Processing";
            job.StartedDate = DateTime.UtcNow;
            await context.SaveChangesAsync();

            await _hubContext.Clients.Group($"user-{job.UserId}")
                .JobStatusChanged(job.Id, "Processing");

            var timeline = JsonSerializer.Deserialize<TimelineDefinition>(job.Project.TimelineDefinition);
            if (timeline == null)
                throw new InvalidOperationException("Invalid timeline definition.");

            // Get target resolution from platform settings
            var platformSettings = PlatformDefaults.Settings.GetValueOrDefault(job.Platform ?? "YouTube");
            var targetWidth = platformSettings?.Width ?? 1080;
            var targetHeight = platformSettings?.Height ?? 1920;

            // Process each segment
            foreach (var segment in timeline.Segments.OrderBy(s => s.Order))
            {
                var segmentPath = await ProcessSegmentAsync(segment, context, job.UserId, targetWidth, targetHeight);
                tempFiles.Add(segmentPath);
            }

            // Concatenate all segments
            var outputDir = _configuration["Storage:OutputPath"] ?? "./outputs";
            Directory.CreateDirectory(outputDir);

            var outputPath = Path.Combine(outputDir,
                $"{job.Id}_{job.Platform}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4");

            if (tempFiles.Count == 1)
            {
                File.Copy(tempFiles[0], outputPath);
            }
            else
            {
                await _videoService.ConcatenateVideosAsync(tempFiles, outputPath);
            }

            job.Status = "Completed";
            job.CompletedDate = DateTime.UtcNow;
            job.OutputPath = outputPath;

            // Update project status
            job.Project.Status = "Completed";
            job.Project.ModifiedDate = DateTime.UtcNow;

            await context.SaveChangesAsync();

            await _hubContext.Clients.Group($"user-{job.UserId}")
                .JobCompleted(job.Id, job.Platform ?? "Unknown");

            _logger.LogInformation("Job {JobId} completed successfully. Output: {Output}", jobId, outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", jobId);

            job.Status = "Failed";
            job.ErrorMessage = ex.Message;
            job.CompletedDate = DateTime.UtcNow;
            job.Project.Status = "Failed";
            job.Project.ModifiedDate = DateTime.UtcNow;

            await context.SaveChangesAsync();

            await _hubContext.Clients.Group($"user-{job.UserId}")
                .JobStatusChanged(job.Id, "Failed", ex.Message);
        }
        finally
        {
            foreach (var file in tempFiles)
            {
                try { if (File.Exists(file)) File.Delete(file); }
                catch { /* best effort cleanup */ }
            }
        }
    }

    private async Task<string> ProcessSegmentAsync(TimelineSegment segment,
        ClipForgeDbContext context, int userId, int targetWidth, int targetHeight)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");

        switch (segment.Type)
        {
            case "textSlide":
                var text = segment.Text ?? segment.OverlayText ?? "Text";
                var duration = (int)(segment.Duration ?? 3);
                await _videoService.GenerateTextSlideAsync(
                    text, duration, tempPath, targetWidth, targetHeight, segment.BackgroundColor);
                break;

            case "image":
                var imagePath = await ResolveAssetPathAsync(segment, context, userId);
                if (imagePath == null) throw new InvalidOperationException("Image asset not found.");
                await _videoService.ConvertImageToVideoAsync(
                    imagePath, (int)(segment.Duration ?? 3), tempPath, targetWidth, targetHeight);
                break;

            case "video":
            case "asset":
                var videoPath = await ResolveAssetPathAsync(segment, context, userId);
                if (videoPath == null) throw new InvalidOperationException("Video asset not found.");

                if (!string.IsNullOrEmpty(segment.OverlayText))
                {
                    await _videoService.AddTextOverlayAsync(
                        videoPath, segment.OverlayText,
                        segment.OverlayPosition ?? "bottom-center", tempPath);
                }
                else
                {
                    File.Copy(videoPath, tempPath, true);
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown segment type: {segment.Type}");
        }

        return tempPath;
    }

    private async Task<string?> ResolveAssetPathAsync(TimelineSegment segment,
        ClipForgeDbContext context, int userId)
    {
        if (segment.AssetId.HasValue)
        {
            var asset = await context.Assets
                .FirstOrDefaultAsync(a => a.Id == segment.AssetId.Value && a.UserId == userId);
            return asset?.StoragePath;
        }

        return segment.Path;
    }
}
