using System.Security.Claims;
using ClipForge.Components;
using ClipForge.Data;
using ClipForge.Hubs;
using ClipForge.Jobs;
using ClipForge.Services;
using FFMpegCore;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ClipForgeDbContext>(options =>
{
    var provider = builder.Configuration.GetValue("Database:Provider", "Sqlite");
    if (provider == "SqlServer")
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=clipforge.db");
    }
});

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/api/auth/logout";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Hangfire
builder.Services.AddHangfire(config =>
{
    config.UseMemoryStorage(); // Use SQL Server storage in production
});
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue("Processing:MaxConcurrentJobs", 2);
});

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<TemplateService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<BatchProcessingService>();
builder.Services.AddSingleton<VideoProcessingService>();

// SignalR
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// HttpClient for Blazor Server components to call API endpoints
builder.Services.AddScoped(sp =>
{
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var request = httpContextAccessor.HttpContext?.Request;
    var baseUri = $"{request?.Scheme}://{request?.Host}";

    var handler = new HttpClientHandler();
    handler.UseCookies = true;

    // Forward cookies from the current request
    var client = new HttpClient(handler) { BaseAddress = new Uri(baseUri) };

    var cookies = httpContextAccessor.HttpContext?.Request.Headers["Cookie"].ToString();
    if (!string.IsNullOrEmpty(cookies))
    {
        client.DefaultRequestHeaders.Add("Cookie", cookies);
    }

    return client;
});

builder.Services.AddHttpContextAccessor();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure FFmpeg
var ffmpegPath = builder.Configuration["FFmpeg:BinaryFolder"];
if (!string.IsNullOrEmpty(ffmpegPath))
{
    GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);
}

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClipForgeDbContext>();
    db.Database.EnsureCreated();
}

// Create required directories
var uploadPath = builder.Configuration["Storage:UploadPath"] ?? "./uploads";
var outputPath = builder.Configuration["Storage:OutputPath"] ?? "./outputs";
Directory.CreateDirectory(uploadPath);
Directory.CreateDirectory(outputPath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Map endpoints
app.MapControllers();
app.MapHub<ProcessingHub>("/hubs/processing");
app.MapHangfireDashboard("/hangfire");

// Schedule recurring cleanup job
RecurringJob.AddOrUpdate<TempFileCleanupJob>(
    "temp-file-cleanup",
    job => job.CleanupTempFilesAsync(),
    Cron.Daily);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
