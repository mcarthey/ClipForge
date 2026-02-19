using ClipForge.Models.DTOs;
using ClipForge.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClipForge.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;
    private readonly BatchProcessingService _batchService;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public ProjectsController(
        ProjectService projectService,
        BatchProcessingService batchService,
        IBackgroundJobClient backgroundJobClient)
    {
        _projectService = projectService;
        _batchService = batchService;
        _backgroundJobClient = backgroundJobClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var userId = AuthService.GetUserId(User);
        var projects = await _projectService.GetProjectsAsync(userId);
        return Ok(projects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(int id)
    {
        var userId = AuthService.GetUserId(User);
        var project = await _projectService.GetProjectAsync(id, userId);
        return project != null ? Ok(project) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
    {
        var userId = AuthService.GetUserId(User);
        var project = await _projectService.CreateProjectAsync(dto, userId);
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
    }

    [HttpPost("from-template")]
    public async Task<IActionResult> CreateFromTemplate([FromBody] CreateFromTemplateDto dto)
    {
        var userId = AuthService.GetUserId(User);
        var project = await _projectService.CreateFromTemplateAsync(dto, userId);
        return project != null
            ? CreatedAtAction(nameof(GetProject), new { id = project.Id }, project)
            : NotFound(new { error = "Template not found." });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] UpdateProjectDto dto)
    {
        var userId = AuthService.GetUserId(User);
        var project = await _projectService.UpdateProjectAsync(id, dto, userId);
        return project != null ? Ok(project) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var userId = AuthService.GetUserId(User);
        var deleted = await _projectService.DeleteProjectAsync(id, userId);
        return deleted ? Ok() : NotFound();
    }

    [HttpPost("{id}/process")]
    public async Task<IActionResult> ProcessProject(int id, [FromQuery] string? platform)
    {
        var userId = AuthService.GetUserId(User);
        var project = await _projectService.GetProjectAsync(id, userId);
        if (project == null) return NotFound();

        using var scope = HttpContext.RequestServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.ClipForgeDbContext>();

        var job = new Models.ProcessingJob
        {
            ProjectId = id,
            UserId = userId,
            Platform = platform ?? "YouTube",
            Status = "Queued"
        };

        context.ProcessingJobs.Add(job);
        await context.SaveChangesAsync();

        _backgroundJobClient.Enqueue<Jobs.VideoProcessingJob>(x =>
            x.ProcessVideoAsync(job.Id));

        return Ok(new ProcessingJobDto
        {
            Id = job.Id,
            ProjectId = job.ProjectId,
            Status = job.Status,
            Platform = job.Platform,
            QueuedDate = job.QueuedDate
        });
    }

    [HttpPost("batch-process")]
    public async Task<IActionResult> BatchProcess([FromBody] BatchProcessDto dto)
    {
        var userId = AuthService.GetUserId(User);
        try
        {
            var result = await _batchService.ProcessBatchAsync(dto, userId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
