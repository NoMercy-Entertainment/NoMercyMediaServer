# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NoMercy MediaServer is a self-hosted media streaming platform built with .NET 9.0, featuring automatic media encoding, comprehensive library management, and remote streaming capabilities. The project is under active development (work-in-progress).

## Build & Run Commands

```bash
# Build
dotnet restore
dotnet build

# Run server (default ports: internal 7626, external 7626)
dotnet run --project src/NoMercy.Service

# Run with custom options
dotnet run --project src/NoMercy.Service --dev --seed --loglevel=Debug
```

### Startup Options
- `--dev`: Development mode
- `--seed`: Seed database with sample data
- `--loglevel`: Set logging level (Information, Debug, etc.)
- `--internal-port` / `--external-port`: Custom port configuration
- `--sentry` / `--dsn`: Enable Sentry error tracking

## Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/NoMercy.Tests.Database
dotnet test tests/NoMercy.Tests.Queue
dotnet test tests/NoMercy.Tests.Providers
dotnet test tests/NoMercy.Tests.MediaProcessing

# Run by category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"

# With coverage
dotnet test --collect:"XPlat Code Coverage" --settings tests/coverletArgs.runsettings
```

## Architecture

### Service-Oriented Modular Design

```
src/
├── NoMercy.Service/        # ASP.NET Core host, entry point, Kestrel
├── NoMercy.Launcher/       # Launcher UI app
├── NoMercy.Api/            # REST controllers (v1, v2), SignalR hubs
├── NoMercy.Database/       # EF Core contexts (MediaContext, QueueContext), SQLite
├── NoMercy.Encoder/        # FFmpeg abstraction, fluent encoding pipeline
├── NoMercy.Queue/          # Background job processing, cron scheduling
├── NoMercy.MediaProcessing/# File analysis, thumbnails, media organization
├── NoMercy.Providers/      # External APIs (TMDB, TVDB, MusicBrainz, etc.)
├── NoMercy.Data/           # Data repositories
├── NoMercy.Networking/     # Network discovery, port forwarding, UPnP
├── NoMercy.NmSystem/       # System utilities, file operations
├── NoMercy.Helpers/        # Common utilities
├── NoMercy.Setup/          # Application initialization
```

### Data Flow
1. **Ingestion**: FileManager scans directories → MediaAnalysis via FFmpeg → metadata extraction
2. **Processing**: JobQueue manages encoding tasks → FfMpeg wrapper → multi-resolution HLS output
3. **Storage**: SQLite databases + file-based media assets
4. **Delivery**: SignalR hubs (VideoHub, MusicHub) + REST APIs for streaming

### Database Contexts
- **MediaContext**: Primary application data (movies, shows, users, metadata)
- **QueueContext**: Job processing and background tasks
- Both use SQLite with connection pooling and query splitting

### SQLite Query Restrictions
**NEVER** use `g.First()`, `g.Last()`, or element-access patterns inside `GroupBy().Select()` projections in EF Core queries. SQLite does not support the SQL `APPLY` operator that EF Core generates for these patterns, causing `System.InvalidOperationException: Translating this query requires the SQL APPLY operation`.

**Bad** (triggers APPLY):
```csharp
context.Items.GroupBy(r => r.MediaId).Select(g => new Dto
{
    Title = g.First().Title,  // APPLY required
    Count = g.Count()
});
```

**Good** (two-step: flat query + client-side grouping):
```csharp
// Step 1: Flat projection server-side
var rows = await context.Items
    .Select(r => new { r.MediaId, r.Title, r.SourceId })
    .ToListAsync(ct);
// Step 2: Group in memory
var result = rows.GroupBy(r => r.MediaId)
    .Select(g => new Dto { Title = g.First().Title, Count = g.Count() });
```

Also avoid projecting nested `.ToList()` inside `.Select()` (e.g. `GenreIds = m.GenreMovies.Select(g => g.GenreId).ToList()`) — fetch join-table data in a separate query and combine client-side.

### FFmpeg Encoding Pipeline
Uses fluent API pattern:
```csharp
var ffmpeg = new FfMpeg();
var file = ffmpeg.Open("/path/to/media.mkv")
    .AddContainer(new Hls())
    .SetBasePath("/output/")
    .ToFile("playlist.m3u8");
```

Encoding inheritance: `BaseVideo` → `X264`/`X265`/`AV1`, `BaseAudio` → format-specific implementations

### Real-time Communication
- `VideoHub`: Video playback control, progress tracking, device sync
- `MusicHub`: Audio playback, playlist management
- Located in: `src/NoMercy.Api/Controllers/Socket/`

### Key Configuration Files
- `src/NoMercy.Service/Program.cs`: Entry point, command-line parsing
- `src/NoMercy.Service/Configuration/ServiceConfiguration.cs`: DI container setup
- `src/NoMercy.Service/Configuration/ApplicationConfiguration.cs`: Middleware pipeline
- `src/NoMercy.Setup/Start.cs`: Startup sequence initialization

## Code Style Rules

### Naming Conventions
- **Interfaces**: PascalCase with `I` prefix (`IVideoProfile`)
- **Private fields**: camelCase with `_` prefix (`_mediaService`)
- **Private static readonly**: PascalCase
- **Constants**: PascalCase (`private const int MaximumCardsInCarousel = 36;`)
- **Methods/Properties**: PascalCase
- **Local variables**: camelCase
- **Avoid `var`**: Use explicit types

### Class Structure Order
1. Private constants
2. Private readonly fields (with `_` prefix)
3. Properties
4. Constructor
5. Public methods
6. Private helper methods

### Constructor Patterns
Prefer primary constructors (C# 12+) for new code:
```csharp
public class LibrariesController(
    LibraryRepository libraryRepository,
    CollectionRepository collectionRepository,
    HomeRepository homeRepository)
    : BaseController
```

Traditional constructor when more initialization logic is needed:
```csharp
public HomeService(HomeRepository homeRepository, MediaContext mediaContext)
{
    _homeRepository = homeRepository;
    _mediaContext = mediaContext;
}
```

### Async/Await
- All async methods return `Task<T>` or `Task`
- Async method names end with `Async` suffix
- Use `await foreach` for streaming data
- Use `ToListAsync()` for EF Core queries

### Null Handling
Use null-coalescing and null-conditional operators:
```csharp
string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
Logo = movie.Images.FirstOrDefault(i => i.Type == "logo")?.FilePath;
```

Use pattern matching for null checks:
```csharp
if (item.Movie is not null) { }
.Where(image => image is { TvId: not null, Type: "backdrop" })
```

### Property Patterns
Nullable properties:
```csharp
public string? Overview { get; set; }
```

Non-nullable with null-forgiving operator:
```csharp
public string Title { get; set; } = null!;
```

Expression-bodied for simple getters:
```csharp
public bool IsHdr => VideoIsHdr();
```

### Collection Initialization
Use collection expressions (C# 12+):
```csharp
List<Movie> movieData = [];
```

Use target-typed new:
```csharp
List<Library> libraries = new();
Dictionary<string, object?> result = new() { { "id", id } };
```

### LINQ Style
Prefer method syntax:
```csharp
List<GenreRowDto> genres = FetchGenres(genreItems).ToList();
```

Use query syntax with `let` for complex readability:
```csharp
return from genre in genreItems
    let name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name
    select new GenreRowDto { Title = name };
```

### String Handling
Always use string interpolation:
```csharp
string url = $"/swagger/{groupName}/swagger.json";
Link = new($"/movie/{Id}", UriKind.Relative);
```

### Error Handling
- Early return on null/authorization checks
- Return HTTP status codes in controllers, not exceptions
- Log with `Console.WriteLine` in catch blocks

### Comments
- Minimal comments; code should be self-documenting
- When needed, explain "why" not "what"
- Use XML docs (`///`) for public API methods

### Using Statements
File-scoped namespaces:
```csharp
namespace NoMercy.Api.Controllers;

public class BaseController : Controller
```

Order: System → Third-party → Project namespaces

### API Controllers
```csharp
[ApiController]
[Tags("Media Search")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/search")]
public class SearchController : BaseController
```

### JSON Properties
Use `[JsonProperty]` for serialization:
```csharp
[JsonProperty("id")] public dynamic? Id { get; set; }
[JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
```

### Service Registration Pattern
```csharp
services.AddVideoHubServices();
services.AddMusicHubServices();
services.AddCronWorker();
```

## External Provider Integrations

Located in `src/NoMercy.Providers/`:
- **TMDB/TVDB**: Movie/TV metadata
- **MusicBrainz/AcoustID**: Music metadata and fingerprinting
- **FanArt/CoverArt**: Artwork
- **OpenSubtitles**: Subtitles
- **Lrclib/MusixMatch**: Lyrics

All providers implement async patterns with retry logic and rate limiting.

## Dev Container & Security Rules

### Server Access
- The server uses wildcard SSL certs issued per device ID: `*.{device-id}.nomercy.tv`
- DNS records are created by the NoMercy API during server registration
- Device IDs are hardware-derived and differ between host and container
- Always access the server via its registered domain, never via `localhost`
- From inside the container, always use the external URL with proper CA verification:
  ```bash
  curl --cacert ~/.local/share/NoMercy_dev/security/certs/ca.pem https://{external-ip-dashed}.{device-id}.nomercy.tv:7626/...
  ```

### Security - Mandatory
- **NEVER** use `curl -sk`, `--insecure`, or skip certificate verification
- **NEVER** use `localhost` to access the server — always use the proper `*.nomercy.tv` domain
- **NEVER** suggest bypassing SSL warnings or security measures
- **NEVER** override internal server behavior (cert, device ID, DNS, registration) with CLI flags unless explicitly asked
- The cert, device ID, and DNS setup are integral parts of the system — they must not be worked around

### Running in Dev Container
```bash
# Start with host LAN IP so both container and host can access
dotnet run --project src/NoMercy.Service -- --dev --internal-ip <HOST_LAN_IP>
```
- The `--internal-ip` flag is needed because the container's Docker IP (172.17.x.x) isn't routable from the host
- Check the log file for the actual Internal/External addresses after startup
- The external URL (via public IP) is accessible from both host browser and container

## Cross-Platform Deployment

- **Linux**: Systemd services, DEB/RPM/Arch packages
- **Windows**: Registry auto-startup, executable (`NoMercyMediaServer.exe`)
- **macOS**: LaunchAgent plist files

CI/CD via GitHub Actions builds platform-specific executables and packages.
