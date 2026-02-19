using Hangfire;

namespace ClipForge.Jobs;

public class TempFileCleanupJob
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TempFileCleanupJob> _logger;

    public TempFileCleanupJob(IConfiguration configuration, ILogger<TempFileCleanupJob> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public Task CleanupTempFilesAsync()
    {
        var retentionHours = _configuration.GetValue("Processing:TempFileRetentionHours", 24);
        var tempDir = Path.GetTempPath();
        var cutoff = DateTime.UtcNow.AddHours(-retentionHours);
        var deletedCount = 0;

        try
        {
            foreach (var file in Directory.GetFiles(tempDir, "*.mp4"))
            {
                if (File.GetCreationTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    deletedCount++;
                }
            }

            foreach (var file in Directory.GetFiles(tempDir, "*.png"))
            {
                if (File.GetCreationTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    deletedCount++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during temp file cleanup");
        }

        _logger.LogInformation("Temp file cleanup completed. Deleted {Count} files", deletedCount);
        return Task.CompletedTask;
    }
}
