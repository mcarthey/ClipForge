using ClipForge.Models.DTOs;
using ClipForge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClipForge.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly TemplateService _templateService;

    public TemplatesController(TemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplates([FromQuery] string? platform)
    {
        var userId = AuthService.GetUserId(User);
        var templates = await _templateService.GetTemplatesAsync(userId, platform);
        return Ok(templates);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplate(int id)
    {
        var userId = AuthService.GetUserId(User);
        var template = await _templateService.GetTemplateAsync(id, userId);
        return template != null ? Ok(template) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateDto dto)
    {
        var userId = AuthService.GetUserId(User);
        var template = await _templateService.CreateTemplateAsync(dto, userId);
        return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpdateTemplateDto dto)
    {
        var userId = AuthService.GetUserId(User);
        var template = await _templateService.UpdateTemplateAsync(id, dto, userId);
        return template != null ? Ok(template) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        var userId = AuthService.GetUserId(User);
        var deleted = await _templateService.DeleteTemplateAsync(id, userId);
        return deleted ? Ok() : NotFound();
    }
}
