# NoMercy MediaServer - AI Coding Assistant Instructions

## Project Overview
NoMercy MediaServer is a self-hosted media streaming platform built with .NET 9.0, featuring automatic media encoding, comprehensive library management, and remote streaming capabilities. The system emphasizes privacy, user ownership, and seamless cross-platform deployment.

## Architecture & Core Components

### Service-Oriented Modular Design
The codebase follows a layered architecture with distinct service boundaries:

- **NoMercy.Server**: Main ASP.NET Core host with Kestrel, handles web API and SignalR hubs
- **NoMercy.Api**: RESTful controllers with versioned endpoints (`/api/v1/`, `/api/v2/`)  
- **NoMercy.Database**: Entity Framework Core with SQLite, dual contexts (`MediaContext`, `QueueContext`)
- **NoMercy.Encoder**: FFmpeg abstraction layer with fluent video/audio encoding pipeline
- **NoMercy.Queue**: Background job processing with cron scheduling and retry logic
- **NoMercy.MediaProcessing**: File analysis, thumbnail generation, and media organization
- **NoMercy.Providers**: External API integrations (TMDB, TVDB, MusicBrainz, etc.)

### Data Flow Architecture
1. **Ingestion**: `FileManager` scans directories → `MediaAnalysis` via FFmpeg → metadata extraction
2. **Processing**: `JobQueue` manages encoding tasks → `FfMpeg` wrapper → multi-resolution HLS output
3. **Storage**: SQLite databases + file-based media assets with hash-based organization
4. **Delivery**: SignalR hubs (`VideoHub`, `MusicHub`) + REST APIs for real-time streaming

## Development Workflows

### Build & Testing Commands
```bash
# Standard build (uses .NET 9.0)
dotnet build

# Run main server with custom ports
dotnet run --project src/NoMercy.Server -- --internal-port=7626 --external-port=443

# Test suites (xUnit-based)
dotnet test tests/NoMercy.Tests.Database    # Database integration tests
dotnet test tests/NoMercy.Tests.Queue       # Job queue and cron tests  
dotnet test tests/NoMercy.Tests.Providers   # External API provider tests
dotnet test tests/NoMercy.Tests.MediaProcessing  # FFmpeg encoding tests
```

### CI/CD Pipeline Architecture
The project uses a sophisticated GitHub Actions workflow with smart change detection:
- **Change Detection**: Only builds on `src/` changes, skips on template/docs changes
- **Matrix Builds**: Linux, Windows, macOS executables with platform-specific naming
- **Package Generation**: DEB, RPM, Arch packages with systemd service integration
- **Artifact Management**: Multi-platform releases with signed packages

## Project-Specific Conventions

### FFmpeg Encoding Pipeline
The encoding system uses a fluent API pattern:
```csharp
// Typical encoding workflow
var ffmpeg = new FfMpeg();
var file = ffmpeg.Open("/path/to/media.mkv")
    .AddContainer(new Hls())  // HLS streaming format
    .SetBasePath("/output/")
    .ToFile("playlist.m3u8");
```

Key encoding classes follow inheritance: `BaseVideo` → `X264`/`X265`/`AV1`, `BaseAudio` → format-specific implementations.

### Service Registration Pattern  
Dependency injection follows modular extension methods in `ServiceConfiguration.cs`:
```csharp
services.AddVideoHubServices();  // Video streaming services
services.AddMusicHubServices();  // Music streaming services  
services.AddCronWorker();        // Background job scheduling
```

### Database Context Separation
- **MediaContext**: Primary application data (movies, shows, users, metadata)
- **QueueContext**: Job processing and background tasks
- Both use SQLite with connection pooling and query splitting for performance

### Real-time Communication
SignalR hubs handle streaming state:
- `VideoHub`: Video playback control, progress tracking, device sync
- `MusicHub`: Audio playback, playlist management, multi-room audio
- State managers maintain shared state across WebSocket connections

## Integration Points & External Dependencies

### Media Provider Integrations
- **TMDB**: Movie/TV metadata with rate limiting and caching patterns
- **MusicBrainz**: Album/artist data with fingerprinting via AcoustID
- **External APIs**: All providers implement async patterns with retry logic

### Cross-Platform Deployment
- **Linux**: Systemd user services, DEB/RPM packages, icon integration
- **Windows**: Registry-based auto-startup, executable naming (`NoMercyMediaServer.exe`)
- **macOS**: LaunchAgent plist files, app bundle structure

### Network Architecture  
- **NAT Detection**: Automatic port forwarding detection with UPnP
- **Cloudflare Tunnel**: Fallback for closed NAT environments (paid feature)
- **SSL Certificates**: Automatic Let's Encrypt integration for external access

## Key Files & Patterns

### Configuration Entry Points
- `Program.cs`: Command-line argument parsing, hosting setup
- `Startup.cs`: Service registration and middleware pipeline
- `ServiceConfiguration.cs`: DI container configuration by feature area
- `ApplicationConfiguration.cs`: Middleware, routing, and static file handling

### Media Processing Core
- `src/NoMercy.Encoder/FfMpeg.cs`: FFmpeg process management and parsing
- `src/NoMercy.MediaProcessing/Files/FileManager.cs`: File analysis and organization
- `src/NoMercy.Queue/JobQueue.cs`: Background task coordination

### API Structure
Controllers follow versioned routing: `src/NoMercy.Api/Controllers/V1/`, with base controller providing common functionality.

## Common Debugging Approaches
- **Database Issues**: Check SQLite file permissions in `AppFiles.MediaDatabase`
- **Encoding Problems**: Verify FFmpeg binary availability in `AppFiles.FfmpegPath`
- **Network Issues**: Test NAT status detection and port forwarding in `ServerRegistrationService`
- **Performance**: Monitor job queue status and cron job execution timing

## Project Context
This is a work-in-progress media server emphasizing user control over their media collections, with automatic encoding to multiple formats and resolutions for optimal streaming across devices.