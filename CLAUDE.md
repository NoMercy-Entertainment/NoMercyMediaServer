# NoMercy Media Server

The flagship media server for encoding, organizing, and streaming personal media.

See `.claude/CLAUDE.md` for the full detailed guide (build commands, architecture, security, dev container, etc.)

## Tech Stack
- C# / .NET 10, ASP.NET Core, Entity Framework Core (SQLite)
- SignalR for real-time communication
- MSBuild / dotnet CLI, solution file: `NoMercy.Server.sln`
- Centralized package management: `Directory.Packages.props`
- Testing: xUnit + FluentAssertions + Moq (12 test projects)

## Structure
```
src/
  NoMercy.Service/          # Web host entry point
  NoMercy.Cli/              # CLI entry point
  NoMercy.Launcher/         # Desktop launcher
  NoMercy.Tray/             # System tray (Avalonia)
  NoMercy.App/              # App entry point
  NoMercy.Api/              # Controllers, DTOs, Hubs, Middleware
  NoMercy.Database/         # EF Core models and migrations
  NoMercy.Data/             # Data access layer
  NoMercy.Encoder/          # Media encoding/transcoding
  NoMercy.MediaProcessing/  # Media analysis and processing
  NoMercy.MediaSources/     # Media source providers
  NoMercy.Providers/        # External data providers (TMDB, etc.)
  NoMercy.Networking/       # Network utilities
  NoMercy.NmSystem/         # System-level operations
  NoMercy.Helpers/          # Shared utilities
  NoMercy.Globals/          # Global constants and config
  NoMercy.Events/           # Event system
  NoMercy.Plugins/          # Plugin implementations
  NoMercy.Plugins.Abstractions/  # Plugin interfaces
  NoMercy.Queue.*/          # Job queue system
tests/
  NoMercy.Tests.*/          # Test projects per domain
```

## Code Style (strict)

### Naming
- Projects: PascalCase with dot-separated namespaces (`NoMercy.Api`)
- Classes/Methods/Properties: PascalCase
- Local variables/parameters: camelCase
- Private fields: `_camelCase` prefix (e.g. `_mediaService`)
- Private constants: PascalCase
- Interfaces: PascalCase with `I` prefix (`IVideoProfile`)
- One class per file, filename matches class name

### Modern C# Features (required)
- **Target-typed new**: Always use `new()` when the type is evident from the left side
  ```csharp
  List<Library> libraries = new();
  Dictionary<string, object?> result = new() { { "id", id } };
  ```
- **Collection expressions**: Use `[]` for empty collections
  ```csharp
  List<Movie> movieData = [];
  ```
- **File-scoped namespaces**: Always, never block-scoped
  ```csharp
  namespace NoMercy.Api.Controllers;
  ```
- **Primary constructors**: Prefer for simple DI injection
  ```csharp
  public class LibrariesController(
      LibraryRepository libraryRepository,
      CollectionRepository collectionRepository)
      : BaseController
  ```
- **Pattern matching**: Use `is not null`, `is { Prop: not null }` patterns
  ```csharp
  if (item.Movie is not null) { }
  .Where(image => image is { TvId: not null, Type: "backdrop" })
  ```
- **Null-coalescing/conditional**: Always prefer over explicit null checks
  ```csharp
  string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
  ```
- **String interpolation**: Always, never concatenation or string.Format
  ```csharp
  string url = $"/swagger/{groupName}/swagger.json";
  ```

### Explicitly avoid
- **Do NOT use `var`**: Always use explicit types. The editorconfig enforces this.
- **Do NOT use primary constructors when constructor has logic**: Use traditional constructor with `_field` assignment.
- **Do NOT use `g.First()` inside EF Core `GroupBy().Select()`**: SQLite cannot translate APPLY. Fetch flat, group in-memory.
- **Do NOT nest `.ToList()` inside `.Select()` projections**: Separate query and combine client-side.
- **Do NOT convert to primary constructors when suppressed**: `resharper_convert_to_primary_constructor_highlighting = none` is set deliberately.

### Async
- All async methods return `Task<T>` or `Task`
- Async method names end with `Async` suffix
- Use `await foreach` for streaming data
- Use `ToListAsync()` for EF Core queries

### JSON serialization
- Use `[JsonProperty("snake_case")]` for all serialized properties

## Rules
- Add new NuGet packages to `Directory.Packages.props`, not individual `.csproj` files.
- Database changes require EF Core migrations. Don't modify the database schema manually.
- All new API endpoints need corresponding test coverage in `tests/`.
- GitHub release assets use these names: `nomercy-windows-x64.exe`, `nomercy-linux-x64`, `nomercy_VERSION_amd64.deb`, etc. Don't change asset naming without updating `infra/nomercy-packages` and `apps/nomercy-tv` download URLs.
- Run `dotnet test` before committing changes.
