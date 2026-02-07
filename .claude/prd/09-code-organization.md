## 9. Code Organization & Restructuring

### 9.1 Current Problems

#### Problem 1: DTOs Scattered Across 12+ Locations
140+ DTO files with no centralized structure:
- `NoMercy.Api/Controllers/V1/DTO/`
- `NoMercy.Api/Controllers/V1/Dashboard/DTO/`
- `NoMercy.Api/Controllers/V1/Media/DTO/`
- `NoMercy.Api/Controllers/V1/Music/DTO/`
- `NoMercy.Data/Repositories/` (mixed with repos)
- `NoMercy.Data/Logic/`
- `NoMercy.Server/Seeds/Dto/`
- `NoMercy.Networking/Dto/`
- Plus 3 duplicate `FolderDto` classes

#### Problem 2: Business Logic in Controllers Folder
Socket controllers contain services, managers, factories, DTOs, and state management:
```
Controllers/Socket/music/
  ├── MusicDeviceManager.cs       (should be in Services/)
  ├── MusicLikeEventDto.cs        (should be in DTOs/)
  ├── MusicPlaybackService.cs     (should be in Services/)
  ├── MusicPlayerStateManager.cs  (should be in Services/)
  └── ...8 more files
```

#### Problem 3: Namespace Casing Violations
- `NoMercy.Server/services/` (lowercase)
- `NoMercy.Api/Controllers/Socket/music/` (lowercase)
- `NoMercy.Api/Controllers/Socket/video/` (lowercase)

#### Problem 4: 96 Database Models in Single Flat Folder
- `src/NoMercy.Database/Models/` has 96 files with no subdirectory organization

#### Problem 5: Abandoned EncoderV2 Project
- `src/NoMercy.EncoderV2/` exists with only Configuration/Validation folders, no implementation

#### Problem 6: REMOVED — Invalid Finding
- `src/NoMercy.App.zip` is the built frontend web application (NoMercy.App with wwwroot assets). It's bundled as a zip for server distribution — this is intentional.

### 9.2 Proposed Restructured Layout

```
src/
├── NoMercy.Server/                    # Entry point, host configuration
│   ├── Program.cs
│   ├── Configuration/                 # All startup config (renamed from AppConfig)
│   │   ├── ServiceRegistration.cs
│   │   ├── MiddlewarePipeline.cs
│   │   ├── KestrelConfiguration.cs
│   │   └── SwaggerConfiguration.cs
│   ├── Services/                      # Server-hosted services (renamed from services/)
│   │   ├── CloudflareTunnelService.cs
│   │   └── ServerRegistrationService.cs
│   └── Seeds/
│
├── NoMercy.Api/                       # REST + SignalR
│   ├── Controllers/
│   │   ├── V1/
│   │   │   ├── Dashboard/            # Dashboard endpoints
│   │   │   ├── Media/                # Media endpoints (no DTOs here)
│   │   │   └── Music/                # Music endpoints (no DTOs here)
│   │   ├── V2/                       # Future API version
│   │   └── File/                     # File serving
│   ├── Hubs/                          # SignalR hubs ONLY (renamed from Socket/)
│   │   ├── VideoHub.cs
│   │   ├── MusicHub.cs
│   │   ├── DashboardHub.cs
│   │   └── CastHub.cs
│   ├── Services/                      # API-layer services
│   │   ├── Video/
│   │   │   ├── VideoPlaybackService.cs
│   │   │   ├── VideoPlayerStateManager.cs
│   │   │   └── VideoDeviceManager.cs
│   │   ├── Music/
│   │   │   ├── MusicPlaybackService.cs
│   │   │   ├── MusicPlayerStateManager.cs
│   │   │   └── MusicDeviceManager.cs
│   │   ├── HomeService.cs
│   │   └── SetupService.cs
│   ├── DTOs/                          # ALL API DTOs centralized
│   │   ├── Dashboard/
│   │   ├── Media/
│   │   ├── Music/
│   │   └── Common/
│   ├── Middleware/
│   └── Filters/
│
├── NoMercy.Database/                  # EF Core
│   ├── Contexts/
│   │   ├── MediaContext.cs
│   │   └── QueueContext.cs
│   ├── Models/                        # Organized by domain
│   │   ├── Movies/
│   │   ├── TvShows/
│   │   ├── Music/
│   │   ├── Users/
│   │   ├── Libraries/
│   │   ├── Metadata/
│   │   └── Queue/
│   ├── Configurations/                # EF entity configurations
│   └── Migrations/
│
├── NoMercy.Data/                      # Data access layer
│   ├── Repositories/                  # Repository implementations
│   ├── Extensions/                    # Query extensions (ForUser, etc.)
│   └── Jobs/                          # Data processing jobs
│
├── NoMercy.Encoder/                   # FFmpeg abstraction
│   ├── Core/
│   ├── Format/
│   ├── Commands/
│   └── Progress/
│
├── NoMercy.Queue/                     # Job queue (to be decoupled)
│   ├── Core/
│   ├── Workers/
│   ├── Services/
│   └── Extensions/
│
├── NoMercy.Providers/                 # External API providers
│   ├── TMDB/
│   ├── MusicBrainz/
│   ├── OpenSubtitles/
│   └── Shared/                        # Shared client base, rate limiting
│
├── NoMercy.MediaProcessing/           # Media file operations
├── NoMercy.Networking/                # Network discovery, certificates
├── NoMercy.NmSystem/                  # System utilities
├── NoMercy.Helpers/                   # Cross-cutting utilities
├── NoMercy.Setup/                     # Initialization
├── NoMercy.Plugins.Abstractions/      # NEW: Plugin contracts
├── NoMercy.Tray/                      # NEW: System tray application
└── NoMercy.Globals/                   # Global constants
```

### 9.3 Restructuring Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| REORG-01 | Rename `services/` to `Services/` in NoMercy.Server | Small |
| REORG-02 | Rename `Socket/music/` → `Hubs/` + move services to `Services/Music/` | Medium |
| REORG-03 | Rename `Socket/video/` → same pattern for video | Medium |
| REORG-04 | Consolidate all DTOs into `NoMercy.Api/DTOs/` | Large |
| REORG-05 | Remove duplicate FolderDto (keep one canonical version) | Small |
| REORG-06 | Organize 96 database models into domain subfolders | Medium |
| REORG-07 | Remove or complete NoMercy.EncoderV2 | Decision |
| REORG-08 | REMOVED — NoMercy.App.zip is the bundled frontend app, intentional | N/A |
| REORG-09 | Rename `AppConfig/` to `Configuration/` | Small |
| REORG-10 | Create centralized `Extensions/` per project | Medium |
| REORG-11 | Move Swagger config to dedicated folder | Small |

---

