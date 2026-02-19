# Video Platform Optimizer - Implementation Document

## Project Overview

A web application that automates video editing for multi-platform social media content creation. Users can upload videos/images, arrange them in a timeline, add text overlays, and generate platform-specific versions with appropriate calls-to-action.

### Core Problem
Content creators waste time making small edits to videos for different platforms (YouTube "Subscribe" vs TikTok "Follow", aspect ratios, outros, etc.). This tool automates those variations.

### Target User
Social media content creator with TikTok following expanding to Instagram and YouTube.

## Technology Stack

### Backend
- **Framework**: ASP.NET Core Web API
- **Database**: MSSQL (hosted on SmarterASP)
- **Video Processing**: FFMpegCore (v5.4.0+)
- **Image Processing**: SkiaSharp
- **Background Jobs**: Hangfire (stores state in MSSQL)
- **File Storage**: Local filesystem initially, consider Azure Blob Storage for scale

### Frontend
- **Framework**: Your choice (React, Blazor, or simple MVC)
- **Drag-and-Drop**: SortableJS or equivalent
- **UI**: Responsive, mobile-friendly design

### Deployment
- **Host**: SmarterASP.NET Windows hosting
- **Database**: MSSQL included with hosting
- **Dependencies**: FFmpeg binaries (ffmpeg.exe, ffprobe.exe) deployed with app

## Database Schema

### Users Table
```sql
CREATE TABLE Users (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    DisplayName NVARCHAR(100),
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    LastLoginDate DATETIME2
);
```

### Assets Table
```sql
CREATE TABLE Assets (
    Id INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    Filename NVARCHAR(255) NOT NULL,
    StoragePath NVARCHAR(500) NOT NULL,
    Type NVARCHAR(20) NOT NULL, -- 'Video', 'Image', 'Audio'
    Tags NVARCHAR(MAX), -- JSON array: ["intro", "youtube", "animated"]
    Duration DECIMAL(10,2) NULL, -- seconds, for videos only
    ThumbnailPath NVARCHAR(500),
    FileSize BIGINT,
    UploadDate DATETIME2 DEFAULT GETUTCDATE(),
    INDEX IX_Assets_UserId (UserId),
    INDEX IX_Assets_Type (Type)
);
```

### Templates Table
```sql
CREATE TABLE Templates (
    Id INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    Name NVARCHAR(100) NOT NULL,
    Platform NVARCHAR(50), -- 'YouTube', 'TikTok', 'Instagram', 'Generic'
    TimelineDefinition NVARCHAR(MAX) NOT NULL, -- JSON structure
    IsDefault BIT DEFAULT 0,
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    LastUsedDate DATETIME2,
    INDEX IX_Templates_UserId (UserId),
    INDEX IX_Templates_Platform (Platform)
);
```

### Projects Table
```sql
CREATE TABLE Projects (
    Id INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    Name NVARCHAR(200) NOT NULL,
    TimelineDefinition NVARCHAR(MAX) NOT NULL, -- JSON structure
    Status NVARCHAR(50) DEFAULT 'Draft', -- 'Draft', 'Processing', 'Completed', 'Failed'
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    ModifiedDate DATETIME2 DEFAULT GETUTCDATE(),
    INDEX IX_Projects_UserId (UserId),
    INDEX IX_Projects_Status (Status)
);
```

### ProcessingJobs Table
```sql
CREATE TABLE ProcessingJobs (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL FOREIGN KEY REFERENCES Projects(Id),
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    Status NVARCHAR(50) DEFAULT 'Queued', -- 'Queued', 'Processing', 'Completed', 'Failed'
    Platform NVARCHAR(50),
    OutputPath NVARCHAR(500),
    ErrorMessage NVARCHAR(MAX),
    QueuedDate DATETIME2 DEFAULT GETUTCDATE(),
    StartedDate DATETIME2,
    CompletedDate DATETIME2,
    INDEX IX_ProcessingJobs_UserId (UserId),
    INDEX IX_ProcessingJobs_Status (Status)
);
```

### Analytics Table (Optional - for tracking performance)
```sql
CREATE TABLE Analytics (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ProjectId INT NOT NULL FOREIGN KEY REFERENCES Projects(Id),
    UserId INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
    Platform NVARCHAR(50),
    PostDate DATETIME2,
    Views INT,
    Likes INT,
    Comments INT,
    Notes NVARCHAR(MAX),
    CreatedDate DATETIME2 DEFAULT GETUTCDATE()
);
```

## JSON Schema Definitions

### TimelineDefinition JSON Structure
```json
{
  "segments": [
    {
      "id": "unique-segment-id",
      "type": "video|image|textSlide|asset",
      "assetId": 123, // Reference to Assets table, if type is 'asset'
      "path": "/uploads/video.mp4", // Direct path if not using asset
      "duration": 3.0, // Required for images and textSlides
      "overlayText": "Subscribe for more!",
      "overlayPosition": "bottom-center", // top-left, top-center, top-right, etc.
      "overlayStyle": {
        "fontSize": 48,
        "fontColor": "white",
        "backgroundColor": "black",
        "backgroundOpacity": 0.5
      },
      "order": 0
    }
  ],
  "outputSettings": {
    "resolution": "1080x1920", // 9:16 for shorts
    "fps": 30,
    "videoBitrate": "5000k",
    "audioBitrate": "192k"
  }
}
```

### Asset Tags JSON Structure
```json
["intro", "youtube", "animated", "subscribe-cta", "brand-logo"]
```

## Core Features & Implementation

### 1. Asset Management

**Upload Assets**
- Endpoint: `POST /api/assets/upload`
- Accept: video (mp4, mov, avi), image (png, jpg, gif)
- Generate thumbnail on upload (first frame for video, scaled version for image)
- Extract metadata (duration, dimensions, file size)
- Store in organized folder structure: `/uploads/{userId}/{assetId}/`

**Asset Library UI**
- Grid view with thumbnails
- Filter by type (Video/Image)
- Search/filter by tags
- Multi-select for batch operations
- Inline tag editing

**Implementation Notes:**
```csharp
public class AssetService
{
    public async Task<Asset> UploadAsset(IFormFile file, int userId, List<string> tags)
    {
        // Validate file type and size
        // Generate unique filename
        // Save to disk
        // Extract metadata using FFMpegCore
        var mediaInfo = await FFProbe.AnalyseAsync(filePath);
        // Generate thumbnail
        // Save to database
        // Return Asset object
    }
    
    public async Task<string> GenerateThumbnail(string videoPath, string outputPath)
    {
        await FFMpeg.SnapshotAsync(videoPath, outputPath, 
            new Size(320, 180), TimeSpan.FromSeconds(1));
        return outputPath;
    }
}
```

### 2. Template System

**Create/Edit Template**
- Endpoint: `POST /api/templates`, `PUT /api/templates/{id}`
- Save timeline configuration as JSON
- Mark as default for specific platform
- Include references to assets from library

**Apply Template**
- Endpoint: `POST /api/projects/from-template`
- Load template definition
- Create new project with template structure
- User can modify before processing

**Common Templates:**
- YouTube Standard: Intro → Content → Subscribe CTA
- TikTok Quick: Content → Follow CTA
- Instagram Reel: Content → Profile Link CTA

**Implementation Notes:**
```csharp
public class TemplateService
{
    public async Task<Template> CreateTemplate(CreateTemplateDto dto, int userId)
    {
        var template = new Template
        {
            UserId = userId,
            Name = dto.Name,
            Platform = dto.Platform,
            TimelineDefinition = JsonSerializer.Serialize(dto.Timeline),
            IsDefault = dto.IsDefault
        };
        
        // If IsDefault, unset other defaults for this platform/user
        if (dto.IsDefault)
        {
            await UnsetOtherDefaults(userId, dto.Platform);
        }
        
        await _context.Templates.AddAsync(template);
        await _context.SaveChangesAsync();
        return template;
    }
}
```

### 3. Timeline Editor

**UI Components:**
- Drag-and-drop sortable timeline
- Thumbnail preview for each segment
- Text input field below each segment for overlay text
- Button to add text-only slide
- Button to add asset from library
- Delete/duplicate segment controls

**Key Interactions:**
- Drag to reorder segments
- Click thumbnail to replace from asset library
- Type text to add overlay
- Adjust overlay position (dropdown: top/center/bottom)
- Preview button (shows sequence, not rendered video)

**Frontend State Management:**
```javascript
// Example structure
const timeline = {
  segments: [
    {
      id: 'seg-1',
      type: 'asset',
      assetId: 45,
      thumbnailUrl: '/thumbs/45.jpg',
      overlayText: 'Welcome back!',
      overlayPosition: 'top-center',
      duration: null // from asset
    },
    {
      id: 'seg-2',
      type: 'textSlide',
      text: 'Subscribe for more!',
      duration: 2,
      backgroundColor: '#000000'
    }
  ]
};
```

### 4. Video Processing Pipeline

**Process Flow:**
1. User clicks "Generate" (single or batch)
2. Create ProcessingJob record(s) in database
3. Enqueue job in Hangfire
4. Background worker processes:
   - Generate text slides as images (SkiaSharp)
   - Convert images to video clips (FFMpegCore)
   - Apply text overlays to video segments
   - Concatenate all segments
   - Apply platform-specific settings (resolution, aspect ratio)
5. Update job status
6. Notify user (polling or SignalR)

**FFmpeg Configuration:**
```csharp
public class VideoProcessingService
{
    public VideoProcessingService()
    {
        // Set FFmpeg binary path on startup
        GlobalFFOptions.Configure(options => 
            options.BinaryFolder = Path.Combine(AppContext.BaseDirectory, "bin", "ffmpeg"));
    }
    
    public async Task<string> GenerateTextSlide(string text, int durationSeconds, 
        string outputPath, int width = 1080, int height = 1920)
    {
        // Generate image using SkiaSharp
        var imageBytes = CreateTextImage(text, width, height);
        var imagePath = Path.ChangeExtension(outputPath, ".png");
        await File.WriteAllBytesAsync(imagePath, imageBytes);
        
        // Convert to video
        await FFMpegArguments
            .FromFileInput(imagePath, false, options => options
                .WithDuration(TimeSpan.FromSeconds(durationSeconds)))
            .OutputToFile(outputPath, true, options => options
                .WithVideoCodec("libx264")
                .WithConstantRateFactor(23)
                .WithFrameRate(30))
            .ProcessAsynchronously();
            
        return outputPath;
    }
    
    private byte[] CreateTextImage(string text, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        
        canvas.Clear(SKColors.Black);
        
        var paint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 72,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        
        // Word wrap and center text
        var lines = WrapText(text, paint, width - 100);
        var y = (height - (lines.Count * 80)) / 2;
        
        foreach (var line in lines)
        {
            canvas.DrawText(line, width / 2, y, paint);
            y += 80;
        }
        
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
    
    public async Task<string> AddTextOverlay(string videoPath, string text, 
        string position, string outputPath)
    {
        var (x, y) = GetOverlayPosition(position);
        
        await FFMpegArguments
            .FromFileInput(videoPath)
            .OutputToFile(outputPath, true, options => options
                .WithVideoFilters(filterOptions => filterOptions
                    .DrawText(DrawTextOptions
                        .Create(text, "Arial", 48)
                        .WithParameter("fontcolor", "white")
                        .WithParameter("x", x)
                        .WithParameter("y", y)
                        .WithParameter("box", "1")
                        .WithParameter("boxcolor", "black@0.5")
                        .WithParameter("boxborderw", "5"))))
            .ProcessAsynchronously();
            
        return outputPath;
    }
    
    public async Task<string> ConcatenateVideos(List<string> videoPaths, string outputPath)
    {
        // Create concat file
        var concatFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        var lines = videoPaths.Select(v => $"file '{v}'");
        await File.WriteAllLinesAsync(concatFile, lines);
        
        await FFMpegArguments
            .FromConcatInput(videoPaths)
            .OutputToFile(outputPath, true, options => options
                .WithVideoCodec("libx264")
                .WithAudioCodec("aac")
                .WithConstantRateFactor(23))
            .ProcessAsynchronously();
            
        return outputPath;
    }
    
    private (string x, string y) GetOverlayPosition(string position)
    {
        return position switch
        {
            "top-left" => ("50", "50"),
            "top-center" => ("(w-text_w)/2", "50"),
            "top-right" => ("w-text_w-50", "50"),
            "center" => ("(w-text_w)/2", "(h-text_h)/2"),
            "bottom-center" => ("(w-text_w)/2", "h-150"),
            "bottom-left" => ("50", "h-150"),
            "bottom-right" => ("w-text_w-50", "h-150"),
            _ => ("(w-text_w)/2", "h-150") // default bottom-center
        };
    }
}
```

**Background Job Processing:**
```csharp
public class VideoProcessingJob
{
    private readonly VideoProcessingService _videoService;
    private readonly IDbContext _context;
    
    [AutomaticRetry(Attempts = 0)] // Don't retry, video processing is expensive
    public async Task ProcessVideo(int jobId)
    {
        var job = await _context.ProcessingJobs
            .Include(j => j.Project)
            .FirstOrDefaultAsync(j => j.Id == jobId);
            
        if (job == null) return;
        
        try
        {
            job.Status = "Processing";
            job.StartedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            var timeline = JsonSerializer.Deserialize<TimelineDefinition>(
                job.Project.TimelineDefinition);
            
            var tempFiles = new List<string>();
            
            // Process each segment
            foreach (var segment in timeline.Segments.OrderBy(s => s.Order))
            {
                var segmentPath = await ProcessSegment(segment);
                tempFiles.Add(segmentPath);
            }
            
            // Concatenate all segments
            var outputPath = Path.Combine(
                _configuration["OutputPath"], 
                $"{job.Id}_{job.Platform}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4");
                
            await _videoService.ConcatenateVideos(tempFiles, outputPath);
            
            // Cleanup temp files
            foreach (var file in tempFiles)
            {
                File.Delete(file);
            }
            
            job.Status = "Completed";
            job.CompletedDate = DateTime.UtcNow;
            job.OutputPath = outputPath;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            job.Status = "Failed";
            job.ErrorMessage = ex.Message;
            job.CompletedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
    
    private async Task<string> ProcessSegment(TimelineSegment segment)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
        
        switch (segment.Type)
        {
            case "textSlide":
                await _videoService.GenerateTextSlide(
                    segment.Text, segment.Duration, tempPath);
                break;
                
            case "image":
                var imagePath = GetAssetPath(segment);
                await ConvertImageToVideo(imagePath, segment.Duration, tempPath);
                break;
                
            case "video":
            case "asset":
                var videoPath = GetAssetPath(segment);
                if (!string.IsNullOrEmpty(segment.OverlayText))
                {
                    await _videoService.AddTextOverlay(
                        videoPath, segment.OverlayText, 
                        segment.OverlayPosition, tempPath);
                }
                else
                {
                    File.Copy(videoPath, tempPath);
                }
                break;
        }
        
        return tempPath;
    }
}
```

### 5. Batch Processing

**Batch Endpoint:**
- `POST /api/projects/batch-process`
- Accept: array of content video IDs + template ID
- Create multiple projects, one per video
- Queue all jobs
- Return batch ID for tracking

**UI Flow:**
1. User selects multiple uploaded videos (checkboxes)
2. Selects template to apply
3. Clicks "Process All"
4. Shows progress for each video
5. Download all as ZIP when complete

**Implementation:**
```csharp
public class BatchProcessingService
{
    public async Task<BatchResult> ProcessBatch(BatchProcessRequest request, int userId)
    {
        var template = await _context.Templates
            .FirstOrDefaultAsync(t => t.Id == request.TemplateId && t.UserId == userId);
            
        if (template == null)
            throw new NotFoundException("Template not found");
        
        var jobs = new List<ProcessingJob>();
        
        foreach (var contentVideoId in request.ContentVideoIds)
        {
            // Create project from template, substituting content video
            var timeline = JsonSerializer.Deserialize<TimelineDefinition>(
                template.TimelineDefinition);
                
            // Find "content" placeholder and replace with actual video
            var contentSegment = timeline.Segments
                .FirstOrDefault(s => s.Type == "content-placeholder");
            if (contentSegment != null)
            {
                contentSegment.AssetId = contentVideoId;
                contentSegment.Type = "asset";
            }
            
            var project = new Project
            {
                UserId = userId,
                Name = $"Batch {DateTime.UtcNow:yyyy-MM-dd} - Video {contentVideoId}",
                TimelineDefinition = JsonSerializer.Serialize(timeline),
                Status = "Draft"
            };
            
            await _context.Projects.AddAsync(project);
            await _context.SaveChangesAsync();
            
            // Create processing job
            var job = new ProcessingJob
            {
                ProjectId = project.Id,
                UserId = userId,
                Platform = template.Platform,
                Status = "Queued"
            };
            
            await _context.ProcessingJobs.AddAsync(job);
            jobs.Add(job);
        }
        
        await _context.SaveChangesAsync();
        
        // Enqueue all jobs
        foreach (var job in jobs)
        {
            BackgroundJob.Enqueue<VideoProcessingJob>(x => 
                x.ProcessVideo(job.Id));
        }
        
        return new BatchResult
        {
            JobIds = jobs.Select(j => j.Id).ToList(),
            TotalCount = jobs.Count
        };
    }
}
```

### 6. Download & Export

**Single Video Download:**
- Endpoint: `GET /api/processing-jobs/{id}/download`
- Stream file directly
- Set appropriate headers for download

**Batch Download (ZIP):**
- Endpoint: `GET /api/processing-jobs/batch/{batchId}/download`
- Collect all completed outputs
- Create ZIP in memory or temp file
- Stream to user
- Include suggested captions/hashtags as text file

**Implementation:**
```csharp
public async Task<FileResult> DownloadBatch(List<int> jobIds)
{
    var jobs = await _context.ProcessingJobs
        .Where(j => jobIds.Contains(j.Id) && j.Status == "Completed")
        .ToListAsync();
    
    var zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
    
    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
    {
        foreach (var job in jobs)
        {
            var fileName = Path.GetFileName(job.OutputPath);
            archive.CreateEntryFromFile(job.OutputPath, fileName);
        }
        
        // Add suggested captions
        var captionsEntry = archive.CreateEntry("suggested-captions.txt");
        using (var writer = new StreamWriter(captionsEntry.Open()))
        {
            foreach (var job in jobs)
            {
                await writer.WriteLineAsync($"=== {job.Platform} ===");
                await writer.WriteLineAsync(GetSuggestedCaption(job.Platform));
                await writer.WriteLineAsync();
            }
        }
    }
    
    return File(await File.ReadAllBytesAsync(zipPath), 
        "application/zip", 
        $"videos-{DateTime.UtcNow:yyyyMMdd}.zip");
}

private string GetSuggestedCaption(string platform)
{
    return platform switch
    {
        "YouTube" => "Don't forget to like and subscribe! 🔔\n#youtube #content",
        "TikTok" => "Follow for more! ✨\n#fyp #viral #content",
        "Instagram" => "Link in bio! 💫\n#reels #instagram #content",
        _ => ""
    };
}
```

## Platform-Specific Settings

### Resolution & Aspect Ratios
- **YouTube Shorts**: 1080x1920 (9:16)
- **TikTok**: 1080x1920 (9:16)
- **Instagram Reels**: 1080x1920 (9:16)
- **YouTube Standard**: 1920x1080 (16:9)

### Common CTAs by Platform
- **YouTube**: "Subscribe", "Like and Subscribe", "Hit the bell"
- **TikTok**: "Follow", "Follow for more", "Part 2 in comments"
- **Instagram**: "Link in bio", "Follow", "Save this"

Store these as constants or configuration:
```csharp
public static class PlatformDefaults
{
    public static Dictionary<string, PlatformSettings> Settings = new()
    {
        ["YouTube"] = new PlatformSettings
        {
            Resolution = new Size(1080, 1920),
            DefaultCTA = "Like and Subscribe!",
            SuggestedHashtags = new[] { "#youtube", "#subscribe", "#viral" }
        },
        ["TikTok"] = new PlatformSettings
        {
            Resolution = new Size(1080, 1920),
            DefaultCTA = "Follow for more!",
            SuggestedHashtags = new[] { "#fyp", "#viral", "#foryou" }
        },
        ["Instagram"] = new PlatformSettings
        {
            Resolution = new Size(1080, 1920),
            DefaultCTA = "Link in bio!",
            SuggestedHashtags = new[] { "#reels", "#instagram", "#viral" }
        }
    };
}
```

## Security & Performance Considerations

### File Upload Security
- Validate file types (whitelist extensions)
- Scan for malware (if possible on hosting)
- Limit file sizes (recommend 500MB max per file)
- Store uploaded files outside web root
- Generate unique filenames to prevent collisions

### Rate Limiting
- Limit uploads per user per hour
- Limit concurrent processing jobs per user
- Queue jobs to prevent server overload

### Storage Management
- Implement cleanup job for old temp files
- Delete processed outputs after 30 days (or user download)
- Consider moving to blob storage if disk space becomes an issue

### Authentication & Authorization
- Implement JWT or cookie-based auth
- Ensure users can only access their own assets/projects
- Rate limit login attempts

### Error Handling
- Log all processing errors
- Provide user-friendly error messages
- Implement retry logic for transient failures
- Monitor disk space and job queue length

## Deployment Checklist

### SmarterASP Configuration
1. ✅ MSSQL database created
2. ✅ Connection string in appsettings.json (use environment variables)
3. ✅ FFmpeg binaries (ffmpeg.exe, ffprobe.exe) in /bin/ffmpeg/
4. ✅ Set BinaryFolder path in GlobalFFOptions
5. ✅ Create uploads directory with write permissions
6. ✅ Create outputs directory with write permissions
7. ✅ Hangfire configured with MSSQL storage
8. ✅ Increase request timeout for file uploads (web.config)
9. ✅ Configure max request body size for large uploads

### Post-Deployment Testing
1. Upload test video
2. Create template
3. Generate single video output
4. Test batch processing
5. Download ZIP of multiple outputs
6. Monitor job queue and processing times
7. Check disk space usage
8. Verify temp file cleanup

## Future Enhancements (Post-V1)

### Advanced Features
- **Audio tracks**: Add background music from library
- **Transitions**: Simple fade in/out between segments
- **Filters**: Instagram-style color filters
- **Captions**: Auto-generate from speech-to-text
- **A/B Testing**: Track which outros perform better
- **Scheduling**: Integration with platform scheduling tools
- **Collaboration**: Share templates with team members
- **Mobile app**: Native iOS/Android for on-the-go editing

### Technical Improvements
- Move to cloud blob storage (Azure/AWS S3)
- CDN for faster downloads
- WebSocket/SignalR for real-time job status
- Preview generation (low-res quick preview before full render)
- Distributed processing (multiple workers)
- Cost tracking per user

## API Endpoint Summary

### Authentication
- `POST /api/auth/register` - Create account
- `POST /api/auth/login` - Login
- `POST /api/auth/logout` - Logout

### Assets
- `GET /api/assets` - List user's assets (with filters)
- `POST /api/assets/upload` - Upload new asset
- `DELETE /api/assets/{id}` - Delete asset
- `PUT /api/assets/{id}/tags` - Update asset tags
- `GET /api/assets/{id}/thumbnail` - Get thumbnail

### Templates
- `GET /api/templates` - List user's templates
- `POST /api/templates` - Create template
- `PUT /api/templates/{id}` - Update template
- `DELETE /api/templates/{id}` - Delete template
- `GET /api/templates/{id}` - Get template details

### Projects
- `GET /api/projects` - List user's projects
- `POST /api/projects` - Create new project
- `POST /api/projects/from-template` - Create from template
- `PUT /api/projects/{id}` - Update project
- `DELETE /api/projects/{id}` - Delete project
- `POST /api/projects/{id}/process` - Start processing
- `POST /api/projects/batch-process` - Batch process multiple

### Processing Jobs
- `GET /api/processing-jobs` - List user's jobs
- `GET /api/processing-jobs/{id}` - Get job status
- `GET /api/processing-jobs/{id}/download` - Download output
- `POST /api/processing-jobs/batch-download` - Download multiple as ZIP
- `DELETE /api/processing-jobs/{id}` - Cancel/delete job

### Platform Presets
- `GET /api/platform-presets` - Get default settings for all platforms

## Notes for Implementation

### FFmpeg Binary Deployment
Download FFmpeg essentials build:
- Windows: https://www.gyan.dev/ffmpeg/builds/ (essentials build)
- Extract ffmpeg.exe and ffprobe.exe
- Place in project under /bin/ffmpeg/
- Set "Copy to Output Directory" = "Copy if newer"

### Hangfire Dashboard
Enable dashboard for monitoring:
```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});
```

### Configuration Settings
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;User Id=...;Password=..."
  },
  "FFmpeg": {
    "BinaryFolder": "./bin/ffmpeg",
    "TempFolder": "./temp"
  },
  "Storage": {
    "UploadPath": "./uploads",
    "OutputPath": "./outputs",
    "MaxFileSizeMB": 500
  },
  "Processing": {
    "MaxConcurrentJobs": 2,
    "JobTimeoutMinutes": 30,
    "TempFileRetentionHours": 24
  }
}
```

### Testing Considerations
- Test with various video formats (mp4, mov, avi)
- Test with large files (near max size limit)
- Test concurrent processing
- Test batch processing with many videos
- Monitor memory usage during processing
- Verify cleanup of temp files

## Success Metrics

### V1 Goals
- User can create account and login
- User can upload and manage video/image assets
- User can create and save templates
- User can generate single platform-specific video
- User can batch process multiple videos
- User can download outputs individually or as ZIP
- Processing completes within reasonable time (<5 min for typical video)
- System handles cleanup of temp files automatically

### Performance Targets
- Upload: < 2 minutes for 100MB file
- Processing: < 5 minutes for 2-minute video with 3 segments
- Batch: Process 5 videos within 30 minutes
- Disk usage: Clean temp files within 24 hours
- Uptime: 99% availability

---

## Getting Started

1. Create ASP.NET Core Web API project
2. Install NuGet packages:
   - FFMpegCore
   - SkiaSharp
   - Hangfire
   - Hangfire.SqlServer
   - Microsoft.EntityFrameworkCore.SqlServer
3. Download FFmpeg binaries
4. Set up database schema
5. Configure FFmpeg binary path
6. Implement asset upload endpoint
7. Build timeline editor UI
8. Implement video processing service
9. Set up Hangfire background jobs
10. Deploy to SmarterASP and test

Good luck with the implementation!
