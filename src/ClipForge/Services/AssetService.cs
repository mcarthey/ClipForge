using System.Text.Json;
using ClipForge.Data;
using ClipForge.Models;
using ClipForge.Models.DTOs;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace ClipForge.Services;

public class AssetService
{
    private readonly ClipForgeDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssetService> _logger;

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".avi", ".mkv", ".webm" };
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    public AssetService(ClipForgeDbContext context, IConfiguration configuration, ILogger<AssetService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Asset> UploadAssetAsync(IFormFile file, int userId, List<string>? tags = null)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var assetType = GetAssetType(ext);
        if (assetType == null)
            throw new InvalidOperationException($"File type '{ext}' is not supported.");

        var maxSize = _configuration.GetValue<long>("Storage:MaxFileSizeMB", 500) * 1024 * 1024;
        if (file.Length > maxSize)
            throw new InvalidOperationException($"File size exceeds the {maxSize / (1024 * 1024)}MB limit.");

        var uploadPath = _configuration["Storage:UploadPath"] ?? "./uploads";
        var userDir = Path.Combine(uploadPath, userId.ToString());
        Directory.CreateDirectory(userDir);

        var uniqueFilename = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(userDir, uniqueFilename);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        decimal? duration = null;
        string? thumbnailPath = null;

        if (assetType == "Video")
        {
            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(filePath);
                duration = (decimal)mediaInfo.Duration.TotalSeconds;
                thumbnailPath = await GenerateVideoThumbnailAsync(filePath, userDir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract video metadata for {File}", file.FileName);
            }
        }
        else if (assetType == "Image")
        {
            thumbnailPath = await GenerateImageThumbnailAsync(filePath, userDir);
        }

        var asset = new Asset
        {
            UserId = userId,
            Filename = file.FileName,
            StoragePath = filePath,
            Type = assetType,
            Tags = tags != null ? JsonSerializer.Serialize(tags) : null,
            Duration = duration,
            ThumbnailPath = thumbnailPath,
            FileSize = file.Length,
            UploadDate = DateTime.UtcNow
        };

        _context.Assets.Add(asset);
        await _context.SaveChangesAsync();
        return asset;
    }

    public async Task<List<AssetDto>> GetAssetsAsync(int userId, AssetFilterDto? filter = null)
    {
        var query = _context.Assets.Where(a => a.UserId == userId);

        if (filter != null)
        {
            if (!string.IsNullOrEmpty(filter.Type))
                query = query.Where(a => a.Type == filter.Type);
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(a => a.Filename.Contains(filter.Search));
            if (!string.IsNullOrEmpty(filter.Tag))
                query = query.Where(a => a.Tags != null && a.Tags.Contains(filter.Tag));
        }

        return await query.OrderByDescending(a => a.UploadDate)
            .Select(a => MapToDto(a))
            .ToListAsync();
    }

    public async Task<Asset?> GetAssetAsync(int id, int userId)
    {
        return await _context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    }

    public async Task<bool> DeleteAssetAsync(int id, int userId)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (asset == null) return false;

        if (File.Exists(asset.StoragePath))
            File.Delete(asset.StoragePath);
        if (asset.ThumbnailPath != null && File.Exists(asset.ThumbnailPath))
            File.Delete(asset.ThumbnailPath);

        _context.Assets.Remove(asset);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Asset?> UpdateTagsAsync(int id, int userId, List<string> tags)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (asset == null) return null;

        asset.Tags = JsonSerializer.Serialize(tags);
        await _context.SaveChangesAsync();
        return asset;
    }

    private async Task<string> GenerateVideoThumbnailAsync(string videoPath, string outputDir)
    {
        var thumbFilename = $"thumb_{Guid.NewGuid()}.jpg";
        var thumbPath = Path.Combine(outputDir, thumbFilename);

        try
        {
            await FFMpeg.SnapshotAsync(videoPath, thumbPath, new System.Drawing.Size(320, 180), TimeSpan.FromSeconds(1));
        }
        catch
        {
            // If snapshot fails, try at 0 seconds
            await FFMpeg.SnapshotAsync(videoPath, thumbPath, new System.Drawing.Size(320, 180), TimeSpan.Zero);
        }

        return thumbPath;
    }

    private Task<string> GenerateImageThumbnailAsync(string imagePath, string outputDir)
    {
        var thumbFilename = $"thumb_{Guid.NewGuid()}.jpg";
        var thumbPath = Path.Combine(outputDir, thumbFilename);

        using var original = SKBitmap.Decode(imagePath);
        if (original == null) return Task.FromResult(imagePath);

        var ratio = Math.Min(320.0f / original.Width, 180.0f / original.Height);
        var newWidth = (int)(original.Width * ratio);
        var newHeight = (int)(original.Height * ratio);

        using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium);
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        using var stream = File.OpenWrite(thumbPath);
        data.SaveTo(stream);

        return Task.FromResult(thumbPath);
    }

    private static string? GetAssetType(string extension)
    {
        if (AllowedVideoExtensions.Contains(extension)) return "Video";
        if (AllowedImageExtensions.Contains(extension)) return "Image";
        return null;
    }

    public static AssetDto MapToDto(Asset asset)
    {
        return new AssetDto
        {
            Id = asset.Id,
            Filename = asset.Filename,
            Type = asset.Type,
            Tags = asset.Tags != null
                ? JsonSerializer.Deserialize<List<string>>(asset.Tags) ?? new()
                : new(),
            Duration = asset.Duration,
            ThumbnailUrl = asset.ThumbnailPath != null ? $"/api/assets/{asset.Id}/thumbnail" : null,
            FileSize = asset.FileSize,
            UploadDate = asset.UploadDate
        };
    }
}
