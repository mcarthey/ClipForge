# ClipForge - Setup Guide

## Prerequisites

Install the following before running ClipForge:

| Dependency | Version | Purpose |
|------------|---------|---------|
| .NET SDK | 8.0+ | Build and run the application |
| FFmpeg | Any recent | Video processing (ffmpeg + ffprobe) |

### Installing .NET SDK

**Windows:** Download from https://dotnet.microsoft.com/download/dotnet/8.0

**Linux (Ubuntu/Debian):**
```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

**macOS:**
```bash
brew install dotnet-sdk
```

### Installing FFmpeg

**Windows:** Download essentials build from https://www.gyan.dev/ffmpeg/builds/ and extract `ffmpeg.exe` and `ffprobe.exe`.

**Linux (Ubuntu/Debian):**
```bash
sudo apt-get install -y ffmpeg
```

**macOS:**
```bash
brew install ffmpeg
```

---

## Getting Started (Local Development)

### 1. Clone and Build

```bash
git clone <repo-url>
cd ClipForge
dotnet build src/ClipForge/ClipForge.csproj
```

### 2. Run the Application

```bash
dotnet run --project src/ClipForge/ClipForge.csproj
```

The app starts at **http://localhost:5149** by default.

On first run the application will automatically:
- Create a SQLite database file (`clipforge.db`) in the project directory
- Create `./uploads` and `./outputs` directories for file storage
- Start the Hangfire background job server
- Schedule the daily temp file cleanup job

### 3. Register an Account

Navigate to http://localhost:5149/register and create your first account. You'll be logged in automatically and redirected to the home dashboard.

---

## Configuration

All settings live in `src/ClipForge/appsettings.json`. Here are the key sections you may want to change:

### Database

The app defaults to SQLite for local development. To switch to SQL Server for production:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=ClipForge;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
  },
  "Database": {
    "Provider": "SqlServer"
  }
}
```

The database schema is created automatically on startup via `EnsureCreated()`. No manual migration step is needed for first-time setup.

### FFmpeg Binary Path

If FFmpeg is installed system-wide (on your PATH), leave this empty:
```json
{
  "FFmpeg": {
    "BinaryFolder": ""
  }
}
```

If you are bundling FFmpeg binaries with the app (required for SmarterASP deployment), set the path:
```json
{
  "FFmpeg": {
    "BinaryFolder": "./bin/ffmpeg"
  }
}
```

### Storage Paths

```json
{
  "Storage": {
    "UploadPath": "./uploads",
    "OutputPath": "./outputs",
    "MaxFileSizeMB": 500
  }
}
```

These directories are created automatically on startup. Uploaded assets go into `uploads/{userId}/`, and processed videos go into `outputs/`.

### Processing

```json
{
  "Processing": {
    "MaxConcurrentJobs": 2,
    "JobTimeoutMinutes": 30,
    "TempFileRetentionHours": 24
  }
}
```

`MaxConcurrentJobs` controls how many Hangfire workers process videos simultaneously. Keep this low on shared hosting to avoid CPU/memory pressure.

---

## Deploying to SmarterASP.NET

### 1. Prepare FFmpeg Binaries

Download the Windows essentials build from https://www.gyan.dev/ffmpeg/builds/ and place `ffmpeg.exe` and `ffprobe.exe` in `src/ClipForge/bin/ffmpeg/`. Then add to the `.csproj`:

```xml
<ItemGroup>
  <None Update="bin\ffmpeg\ffmpeg.exe" CopyToOutputDirectory="PreserveNewest" />
  <None Update="bin\ffmpeg\ffprobe.exe" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

And update `appsettings.json`:
```json
{
  "FFmpeg": {
    "BinaryFolder": "./bin/ffmpeg"
  }
}
```

### 2. Switch to SQL Server

Update the connection string and provider in `appsettings.json` as shown in the Database section above. Use the MSSQL credentials provided by SmarterASP.

### 3. Switch Hangfire to SQL Server Storage

In `Program.cs`, replace `config.UseMemoryStorage()` with:
```csharp
config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"));
```

This ensures background jobs survive app restarts. The memory storage used in development loses all queued jobs on restart.

### 4. Configure Upload Limits

Create or update `web.config` for IIS to allow large file uploads:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <security>
      <requestFiltering>
        <requestLimits maxAllowedContentLength="524288000" />
      </requestFiltering>
    </security>
    <aspNetCore requestTimeout="00:10:00" />
  </system.webServer>
</configuration>
```

### 5. Publish and Deploy

```bash
dotnet publish src/ClipForge/ClipForge.csproj -c Release -o ./publish
```

Upload the contents of the `./publish` folder to SmarterASP via their control panel or FTP.

### 6. Post-Deployment Checklist

- [ ] Verify the MSSQL connection string is correct
- [ ] Confirm FFmpeg binaries are in the published `bin/ffmpeg/` folder
- [ ] Check that `uploads` and `outputs` directories have write permissions
- [ ] Navigate to `/hangfire` to confirm the dashboard loads and workers are running
- [ ] Register an account and test uploading a small video
- [ ] Create a template, build a project, and process a video
- [ ] Download the processed output

---

## Project Structure

```
src/ClipForge/
├── Components/
│   ├── Layout/             # MainLayout, NavMenu
│   ├── Pages/
│   │   ├── Auth/           # Login, Register
│   │   ├── Assets/         # AssetLibrary (upload, grid, filter, tags)
│   │   ├── Templates/      # TemplateList, TemplateEditor
│   │   ├── Projects/       # ProjectList, TimelineEditor, BatchProcess
│   │   └── Jobs/           # JobDashboard (real-time status)
│   └── Shared/             # AuthInfo component
├── Configuration/          # PlatformDefaults (YouTube, TikTok, Instagram)
├── Controllers/            # REST API endpoints
├── Data/                   # EF Core DbContext
├── Hubs/                   # SignalR ProcessingHub
├── Jobs/                   # Hangfire background jobs
├── Models/                 # Entity models and DTOs
├── Services/               # Business logic layer
├── Program.cs              # App startup and DI configuration
└── appsettings.json        # All configuration
```

---

## Key URLs (When Running)

| URL | Purpose |
|-----|---------|
| `/` | Home dashboard |
| `/assets` | Upload and manage media files |
| `/templates` | Create/edit platform templates |
| `/projects` | Create projects and build timelines |
| `/projects/batch` | Batch process multiple videos |
| `/jobs` | Monitor processing jobs |
| `/hangfire` | Hangfire dashboard (job queue admin) |
| `/api/*` | REST API endpoints |

---

## Troubleshooting

**"FFmpeg not found" errors during processing:**
Ensure FFmpeg is installed and accessible. Run `ffmpeg -version` in your terminal. If using a custom path, verify `FFmpeg:BinaryFolder` in `appsettings.json` points to the directory containing the ffmpeg binary.

**Large file uploads fail:**
The app has a 500MB request size limit set via `[RequestSizeLimit]` on the upload endpoint. For IIS/SmarterASP, you also need the `web.config` `maxAllowedContentLength` setting described above.

**Processing jobs stay "Queued" forever:**
Check that Hangfire workers are running. Visit `/hangfire` to see the dashboard. In development, jobs use in-memory storage and are lost on restart.

**Database errors on startup:**
The app calls `EnsureCreated()` on startup which creates all tables if they don't exist. If you've changed models and need to update an existing database, either delete the SQLite file and restart, or switch to EF Core migrations:
```bash
cd src/ClipForge
dotnet ef migrations add InitialCreate
dotnet ef database update
```
