namespace ClipForge.Configuration;

public class PlatformSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string DefaultCTA { get; set; } = string.Empty;
    public string[] SuggestedHashtags { get; set; } = Array.Empty<string>();
}

public static class PlatformDefaults
{
    public static readonly Dictionary<string, PlatformSettings> Settings = new()
    {
        ["YouTube"] = new PlatformSettings
        {
            Width = 1080,
            Height = 1920,
            DefaultCTA = "Like and Subscribe!",
            SuggestedHashtags = new[] { "#youtube", "#subscribe", "#viral" }
        },
        ["YouTube Standard"] = new PlatformSettings
        {
            Width = 1920,
            Height = 1080,
            DefaultCTA = "Like and Subscribe!",
            SuggestedHashtags = new[] { "#youtube", "#subscribe", "#viral" }
        },
        ["TikTok"] = new PlatformSettings
        {
            Width = 1080,
            Height = 1920,
            DefaultCTA = "Follow for more!",
            SuggestedHashtags = new[] { "#fyp", "#viral", "#foryou" }
        },
        ["Instagram"] = new PlatformSettings
        {
            Width = 1080,
            Height = 1920,
            DefaultCTA = "Link in bio!",
            SuggestedHashtags = new[] { "#reels", "#instagram", "#viral" }
        }
    };

    public static string GetSuggestedCaption(string platform)
    {
        return platform switch
        {
            "YouTube" or "YouTube Standard" => "Don't forget to like and subscribe!\n#youtube #content",
            "TikTok" => "Follow for more!\n#fyp #viral #content",
            "Instagram" => "Link in bio!\n#reels #instagram #content",
            _ => ""
        };
    }
}
