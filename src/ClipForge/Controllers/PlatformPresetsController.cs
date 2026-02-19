using ClipForge.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace ClipForge.Controllers;

[ApiController]
[Route("api/platform-presets")]
public class PlatformPresetsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetPresets()
    {
        var presets = PlatformDefaults.Settings.Select(kvp => new
        {
            Platform = kvp.Key,
            kvp.Value.Width,
            kvp.Value.Height,
            Resolution = $"{kvp.Value.Width}x{kvp.Value.Height}",
            kvp.Value.DefaultCTA,
            kvp.Value.SuggestedHashtags
        });

        return Ok(presets);
    }
}
