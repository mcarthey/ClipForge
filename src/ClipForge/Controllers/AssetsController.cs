using ClipForge.Models.DTOs;
using ClipForge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClipForge.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly AssetService _assetService;

    public AssetsController(AssetService assetService)
    {
        _assetService = assetService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAssets([FromQuery] AssetFilterDto filter)
    {
        var userId = AuthService.GetUserId(User);
        var assets = await _assetService.GetAssetsAsync(userId, filter);
        return Ok(assets);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(524_288_000)] // 500MB
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string? tags)
    {
        var userId = AuthService.GetUserId(User);
        var tagList = !string.IsNullOrEmpty(tags)
            ? tags.Split(',').Select(t => t.Trim()).ToList()
            : null;

        try
        {
            var asset = await _assetService.UploadAssetAsync(file, userId, tagList);
            return Ok(AssetService.MapToDto(asset));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = AuthService.GetUserId(User);
        var deleted = await _assetService.DeleteAssetAsync(id, userId);
        return deleted ? Ok() : NotFound();
    }

    [HttpPut("{id}/tags")]
    public async Task<IActionResult> UpdateTags(int id, [FromBody] UpdateTagsDto dto)
    {
        var userId = AuthService.GetUserId(User);
        var asset = await _assetService.UpdateTagsAsync(id, userId, dto.Tags);
        return asset != null ? Ok(AssetService.MapToDto(asset)) : NotFound();
    }

    [HttpGet("{id}/thumbnail")]
    public async Task<IActionResult> GetThumbnail(int id)
    {
        var userId = AuthService.GetUserId(User);
        var asset = await _assetService.GetAssetAsync(id, userId);

        if (asset?.ThumbnailPath == null || !System.IO.File.Exists(asset.ThumbnailPath))
            return NotFound();

        var contentType = asset.ThumbnailPath.EndsWith(".png") ? "image/png" : "image/jpeg";
        return PhysicalFile(Path.GetFullPath(asset.ThumbnailPath), contentType);
    }
}
