using System.IO.Compression;
using ClipForge.Configuration;
using ClipForge.Data;
using ClipForge.Models.DTOs;
using ClipForge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClipForge.Controllers;

[ApiController]
[Route("api/processing-jobs")]
[Authorize]
public class ProcessingJobsController : ControllerBase
{
    private readonly ClipForgeDbContext _context;

    public ProcessingJobsController(ClipForgeDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs()
    {
        var userId = AuthService.GetUserId(User);
        var jobs = await _context.ProcessingJobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.QueuedDate)
            .Select(j => new ProcessingJobDto
            {
                Id = j.Id,
                ProjectId = j.ProjectId,
                Status = j.Status,
                Platform = j.Platform,
                ErrorMessage = j.ErrorMessage,
                QueuedDate = j.QueuedDate,
                StartedDate = j.StartedDate,
                CompletedDate = j.CompletedDate
            })
            .ToListAsync();

        return Ok(jobs);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJob(int id)
    {
        var userId = AuthService.GetUserId(User);
        var job = await _context.ProcessingJobs
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

        if (job == null) return NotFound();

        return Ok(new ProcessingJobDto
        {
            Id = job.Id,
            ProjectId = job.ProjectId,
            Status = job.Status,
            Platform = job.Platform,
            ErrorMessage = job.ErrorMessage,
            QueuedDate = job.QueuedDate,
            StartedDate = job.StartedDate,
            CompletedDate = job.CompletedDate
        });
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var userId = AuthService.GetUserId(User);
        var job = await _context.ProcessingJobs
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId && j.Status == "Completed");

        if (job?.OutputPath == null || !System.IO.File.Exists(job.OutputPath))
            return NotFound();

        var fileName = $"{job.Platform}_{job.CompletedDate:yyyyMMdd}.mp4";
        return PhysicalFile(Path.GetFullPath(job.OutputPath), "video/mp4", fileName);
    }

    [HttpPost("batch-download")]
    public async Task<IActionResult> BatchDownload([FromBody] BatchDownloadDto dto)
    {
        var userId = AuthService.GetUserId(User);
        var jobs = await _context.ProcessingJobs
            .Where(j => dto.JobIds.Contains(j.Id) && j.UserId == userId && j.Status == "Completed")
            .ToListAsync();

        if (!jobs.Any())
            return NotFound(new { error = "No completed jobs found." });

        var zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        try
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var job in jobs)
                {
                    if (job.OutputPath != null && System.IO.File.Exists(job.OutputPath))
                    {
                        var entryName = $"{job.Platform}_{job.Id}.mp4";
                        archive.CreateEntryFromFile(job.OutputPath, entryName);
                    }
                }

                // Add suggested captions
                var captionsEntry = archive.CreateEntry("suggested-captions.txt");
                using var writer = new StreamWriter(captionsEntry.Open());
                foreach (var job in jobs)
                {
                    await writer.WriteLineAsync($"=== {job.Platform} (Job #{job.Id}) ===");
                    await writer.WriteLineAsync(PlatformDefaults.GetSuggestedCaption(job.Platform ?? ""));
                    await writer.WriteLineAsync();
                }
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(zipPath);
            return File(bytes, "application/zip", $"videos-{DateTime.UtcNow:yyyyMMdd}.zip");
        }
        finally
        {
            if (System.IO.File.Exists(zipPath))
                System.IO.File.Delete(zipPath);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJob(int id)
    {
        var userId = AuthService.GetUserId(User);
        var job = await _context.ProcessingJobs
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);

        if (job == null) return NotFound();

        if (job.OutputPath != null && System.IO.File.Exists(job.OutputPath))
            System.IO.File.Delete(job.OutputPath);

        _context.ProcessingJobs.Remove(job);
        await _context.SaveChangesAsync();
        return Ok();
    }
}
