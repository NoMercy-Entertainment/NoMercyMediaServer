## 15. Cross-Platform Wallpaper

### 15.1 Current State Analysis

**File**: `src/NoMercy.Helpers/Wallpaper.cs` (242 lines)
**Caller**: `src/NoMercy.Api/Controllers/V1/Dashboard/ServerController.cs:546`
**API**: `POST /api/v1/dashboard/server/wallpaper`

The current implementation is **Windows-only** with hard `[SupportedOSPlatform("windows10.0.18362")]` attribute and three Windows-specific dependencies:

| Dependency | Usage | Issue |
|-----------|-------|-------|
| `Microsoft.Win32.Registry` | Read/write wallpaper config, backup/restore history | Windows Registry API — no cross-platform equivalent |
| `user32.dll` P/Invoke `SystemParametersInfo` | Actually sets the wallpaper | Windows-only DLL |
| `user32.dll` P/Invoke `SetSysColors` | Changes desktop background color | Windows-only DLL |
| `System.Drawing.ColorTranslator` | HTML color → Win32 color int | `System.Drawing` is Windows-only in .NET |

**Performance issue**: The controller endpoint at `ServerController.cs:556-580` loads the full image via `SixLabors.ImageSharp`, resizes it, runs `OctreeQuantizer` to extract the dominant color, then passes it to `Wallpaper.SilentSet()`. This quantization runs **synchronously on the request thread** with no caching — every wallpaper change re-processes the image.

**Architecture issue**: The controller already guards with `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` returning `BadRequest` for non-Windows — this guard would need to be removed or made platform-aware.

### 15.2 Issues Found

| ID | Severity | Description | File:Line |
|----|----------|-------------|-----------|
| WALL-01 | **HIGH** | Windows-only: no Linux or macOS support at all | `Wallpaper.cs:10` |
| WALL-02 | **HIGH** | Synchronous image quantization on request thread — slow for large images | `ServerController.cs:558-576` |
| WALL-03 | **MEDIUM** | No caching of dominant color — re-computed every call for the same image | `ServerController.cs:556-580` |
| WALL-04 | **MEDIUM** | `#pragma warning disable CA1416` suppresses platform warnings instead of fixing them | `ServerController.cs:545-547` |
| WALL-05 | **MEDIUM** | Static mutable state (`_backupState`, `_historyRestored`) — not thread-safe | `Wallpaper.cs:33-34` |
| WALL-06 | **LOW** | `System.Drawing.ColorTranslator` pulls in Windows-only `System.Drawing` dependency | `Wallpaper.cs:117` |
| WALL-07 | **LOW** | Registry key handles not disposed (`using` missing on some `OpenSubKey` calls) | `Wallpaper.cs:83-89,101-107` |

### 15.3 Cross-Platform Strategy

Use the **Strategy pattern** — one interface, three platform-specific implementations, selected at startup via `RuntimeInformation`:

```
IWallpaperService
├── WindowsWallpaperService   ← existing logic (cleaned up)
├── LinuxWallpaperService     ← gsettings / feh / xdg
├── MacWallpaperService       ← osascript / NSWorkspace
└── NullWallpaperService      ← headless/server fallback (no-op)
```

#### 15.3.1 Interface

```csharp
public interface IWallpaperService
{
    bool IsSupported { get; }
    void Set(string imagePath, WallpaperStyle style, string hexColor);
    void SetSilent(string imagePath, WallpaperStyle style, string hexColor);
    void Restore();
}
```

#### 15.3.2 Windows Implementation

Preserve the existing logic but clean up:
- Wrap all `RegistryKey` in `using` statements (WALL-07)
- Remove static mutable state — move `_backupState` into instance field (WALL-05)
- Replace `System.Drawing.ColorTranslator` with manual hex→Win32 color conversion (WALL-06)
- Keep `[SupportedOSPlatform("windows")]` on the class

#### 15.3.3 Linux Implementation

Linux desktop wallpaper has no single API — detect the desktop environment and use the appropriate tool:

| Desktop Environment | Detection | Command |
|--------------------|-----------|---------|
| GNOME / Unity | `$XDG_CURRENT_DESKTOP` contains "GNOME" | `gsettings set org.gnome.desktop.background picture-uri 'file:///path'` |
| KDE Plasma | `$XDG_CURRENT_DESKTOP` contains "KDE" | `qdbus` or DBus script to set wallpaper |
| XFCE | `$XDG_CURRENT_DESKTOP` contains "XFCE" | `xfconf-query -c xfce4-desktop -p /backdrop/.../last-image -s /path` |
| Fallback (X11) | `$DISPLAY` set, no known DE | `feh --bg-fill /path` (requires `feh` installed) |
| Headless/Server | No `$DISPLAY` and no `$WAYLAND_DISPLAY` | Return `IsSupported = false` → `NullWallpaperService` |

**Style mapping** for GNOME `picture-options`:
```
Fill    → "zoom"
Fit     → "scaled"
Stretch → "stretched"
Tile    → "wallpaper"
Center  → "centered"
Span    → "spanned"
```

**Background color**: `gsettings set org.gnome.desktop.background primary-color '#hexcolor'`

#### 15.3.4 macOS Implementation

Use `osascript` (AppleScript) — the most reliable cross-version approach:

```bash
osascript -e 'tell application "Finder" to set desktop picture to POSIX file "/path/to/image"'
```

For macOS 14+ (Sonoma), also works with:
```bash
osascript -e 'tell application "System Events" to tell every desktop to set picture to "/path/to/image"'
```

**Note**: macOS has no direct API for wallpaper style or background color via AppleScript. The OS auto-detects fill mode. If style control is needed later, use the `desktoppr` CLI tool or the `NSWorkspace` API via native interop.

#### 15.3.5 Headless / Server Fallback

```csharp
public class NullWallpaperService : IWallpaperService
{
    public bool IsSupported => false;
    public void Set(string imagePath, WallpaperStyle style, string hexColor) { }
    public void SetSilent(string imagePath, WallpaperStyle style, string hexColor) { }
    public void Restore() { }
}
```

This is used when no display server is detected (Linux server, Docker, CI environments).

### 15.4 Performance Fixes

#### 15.4.1 Async Dominant Color Extraction (WALL-02)

Move the `GetDominantColor` computation off the request thread:

```csharp
private static async Task<string> GetDominantColorAsync(string path)
{
    return await Task.Run(() =>
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(path);
        image.Mutate(x => x
            .Resize(new ResizeOptions
            {
                Sampler = KnownResamplers.NearestNeighbor,
                Size = new(100, 0)
            })
            .Quantize(new OctreeQuantizer
            {
                Options = { MaxColors = 1, Dither = null }
            }));

        return image[0, 0].ToHexString();
    });
}
```

**Also**: Remove the `OrderedDither(1)` when `MaxColors = 1` — dithering with a single color is wasted work.

#### 15.4.2 Color Cache (WALL-03)

Cache the dominant color per image path using `ConcurrentDictionary`:

```csharp
private static readonly ConcurrentDictionary<string, string> ColorCache = new();

private static async Task<string> GetDominantColorCachedAsync(string path)
{
    if (ColorCache.TryGetValue(path, out string? cached))
        return cached;

    string color = await GetDominantColorAsync(path);
    ColorCache.TryAdd(path, color);
    return color;
}
```

Cache invalidation: clear on image file change (FileSystemWatcher) or just use a time-based expiry if images rarely change.

### 15.5 Controller Update

The `ServerController.SetWallpaper` endpoint needs to:
1. Remove the Windows-only guard — use `IWallpaperService.IsSupported` instead
2. Inject `IWallpaperService` via DI
3. Use cached async color extraction
4. Remove `#pragma warning disable CA1416`

```csharp
[HttpPost]
[Route("wallpaper")]
public async Task<IActionResult> SetWallpaper(
    [FromBody] WallpaperRequest request,
    [FromServices] IWallpaperService wallpaperService)
{
    if (!User.IsOwner())
        return UnauthorizedResponse("You do not have permission to set wallpaper");

    if (!wallpaperService.IsSupported)
        return BadRequestResponse("Wallpaper setting is not supported on this platform");

    await using MediaContext mediaContext = new();
    Image? wallpaper = await mediaContext.Images
        .FirstOrDefaultAsync(config => config.FilePath == request.Path);

    if (wallpaper?.FilePath is null)
        return NotFoundResponse("Wallpaper not found");

    string path = Path.Combine(AppFiles.ImagesPath, "original",
        wallpaper.FilePath.Replace("/", ""));

    string color = request.Color ?? await GetDominantColorCachedAsync(path);

    wallpaperService.SetSilent(path, request.Style, color);

    return Ok(new StatusResponseDto<string>
    {
        Status = "ok",
        Message = "Wallpaper set successfully"
    });
}
```

### 15.6 DI Registration

```csharp
// In ServiceConfiguration.cs
public static IServiceCollection AddWallpaperService(this IServiceCollection services)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        services.AddSingleton<IWallpaperService, WindowsWallpaperService>();
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        services.AddSingleton<IWallpaperService, MacWallpaperService>();
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        bool hasDisplay = Environment.GetEnvironmentVariable("DISPLAY") is not null
                       || Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null;
        if (hasDisplay)
            services.AddSingleton<IWallpaperService, LinuxWallpaperService>();
        else
            services.AddSingleton<IWallpaperService, NullWallpaperService>();
    }
    else
        services.AddSingleton<IWallpaperService, NullWallpaperService>();

    return services;
}
```

### 15.7 File Organization

Move from `NoMercy.Helpers` (wrong place — this is application-specific, not generic tooling) into a proper location:

```
src/NoMercy.Helpers/
  ├── Wallpaper.cs           ← DELETE (move out)
  └── WallpaperStyle.cs      ← KEEP (cross-platform enum, genuinely generic)

src/NoMercy.NmSystem/Wallpaper/
  ├── IWallpaperService.cs
  ├── WindowsWallpaperService.cs
  ├── LinuxWallpaperService.cs
  ├── MacWallpaperService.cs
  └── NullWallpaperService.cs
```

**Or** keep it in `NoMercy.Helpers` if the team prefers — the key point is the interface + strategy pattern, not the folder.

### 15.8 Implementation Tasks

| ID | Task | Effort | Priority |
|----|------|--------|----------|
| WALL-IMPL-01 | Create `IWallpaperService` interface | Small | — |
| WALL-IMPL-02 | Refactor existing Windows code into `WindowsWallpaperService` (fix WALL-05, WALL-06, WALL-07) | Medium | — |
| WALL-IMPL-03 | Implement `LinuxWallpaperService` with DE detection | Medium | — |
| WALL-IMPL-04 | Implement `MacWallpaperService` with osascript | Small | — |
| WALL-IMPL-05 | Implement `NullWallpaperService` | Trivial | — |
| WALL-IMPL-06 | Add DI registration with platform detection | Small | — |
| WALL-IMPL-07 | Make `GetDominantColor` async + add color cache (WALL-02, WALL-03) | Small | — |
| WALL-IMPL-08 | Update `ServerController.SetWallpaper` to use DI service (WALL-04) | Small | — |
| WALL-IMPL-09 | Add unit tests for each platform service | Medium | — |

---

