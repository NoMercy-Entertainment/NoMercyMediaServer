## 13. Headless Server & Tray Control UI

### 13.1 Architecture

```
┌─────────────────────┐    Named Pipe / localhost:7627    ┌──────────────────┐
│  NoMercy.Server     │ ◄──────────────────────────────► │  NoMercy.Tray    │
│  (Headless Daemon)  │    Management API                 │  (Desktop UI)    │
│                     │                                   │                  │
│  - No console I/O   │    ┌─────────────────┐           │  - System tray   │
│  - Writes to log    │    │  NoMercy.Cli    │           │  - Log viewer    │
│  - IPC endpoint     │ ◄──┤  (CLI Tool)     │           │  - Start/Stop    │
│  - Service/daemon   │    │  nomercy status │           │  - Settings      │
│                     │    │  nomercy logs   │           │  - Plugin mgmt   │
└─────────────────────┘    └─────────────────┘           └──────────────────┘
```

### 13.2 Server Changes

**Replace all Console output with ILogger:**
```csharp
// BEFORE
Console.WriteLine($"Server started on port {port}");

// AFTER
_logger.LogInformation("Server started on port {Port}", port);
```

**Add Management API (internal only):**
- `GET /manage/status` — Server health, uptime, resource usage
- `GET /manage/logs?tail=100` — Recent log entries (Server-Sent Events for streaming)
- `POST /manage/stop` — Graceful shutdown
- `POST /manage/restart` — Restart server
- `GET /manage/config` — Current configuration
- `PUT /manage/config` — Update configuration
- `GET /manage/plugins` — Plugin status
- `GET /manage/queue` — Queue status

**Platform service registration:**
- **Windows**: `sc.exe` service or Windows Service via `BackgroundService`
- **macOS**: LaunchAgent plist (already supported)
- **Linux**: systemd unit file (already supported)

### 13.3 Tray Application

**Technology options (cross-platform):**
- **Avalonia UI** (recommended) — True cross-platform, native tray support, .NET native
- **.NET MAUI** — Microsoft-backed but macOS/Linux support incomplete
- **Electron** — Heavy but guaranteed cross-platform

**Tray features:**
- System tray icon with status indicator (green/yellow/red)
- Right-click menu: Open Dashboard, View Logs, Restart, Stop, Settings, Quit
- Log viewer window with filtering, search, and tail mode
- Server resource monitor (CPU, RAM, disk, active streams)
- Plugin management UI
- Library management
- Queue status and controls

### 13.4 CLI Tool

```bash
nomercy status              # Show server status
nomercy logs --follow       # Stream logs
nomercy logs --tail 100     # Last 100 log entries
nomercy restart             # Restart server
nomercy stop                # Stop server
nomercy config get          # Show configuration
nomercy config set key val  # Update configuration
nomercy plugin list         # List plugins
nomercy plugin install url  # Install plugin
nomercy queue status        # Queue statistics
```

### 13.5 Implementation Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| HEAD-01 | Replace all `Console.WriteLine` with `ILogger` throughout codebase | Large |
| HEAD-02 | Add file-based structured logging (Serilog file sink) | Small |
| HEAD-03 | Create management API controller (localhost-only, separate port) | Medium |
| HEAD-04 | Implement named pipe IPC for local communication | Medium |
| HEAD-05 | Create Windows Service host (`BackgroundService`) | Medium |
| HEAD-06 | Verify macOS LaunchAgent support | Small |
| HEAD-07 | Verify Linux systemd support | Small |
| HEAD-08 | Create `NoMercy.Tray` project (Avalonia) | Large |
| HEAD-09 | Implement system tray icon with status | Medium |
| HEAD-10 | Implement log viewer window | Medium |
| HEAD-11 | Implement server control UI (start/stop/restart) | Medium |
| HEAD-12 | Implement resource monitor dashboard | Medium |
| HEAD-13 | Create `NoMercy.Cli` project | Medium |
| HEAD-14 | Implement CLI commands | Medium |
| HEAD-15 | Package tray app for Windows (installer) | Medium |
| HEAD-16 | Package tray app for macOS (.app bundle) | Medium |
| HEAD-17 | Package tray app for Linux (AppImage/Flatpak) | Medium |

### 13.6 Additional Work Completed (Outside Original PRD Tasks)

The following improvements were implemented beyond the original task definitions to make the tray app and server fully functional together:

| Task ID | Description | Status |
|---------|-------------|--------|
| TRAY-01 | Server process launching from tray with 3-tier fallback (production binary → dev binary → dotnet run) | Done |
| TRAY-02 | Fix log viewer: strip ANSI escape codes, JSON unescape, color-coded log levels | Done |
| TRAY-03 | Add multi-select and Ctrl+C clipboard copy to log viewer | Done |
| TRAY-04 | Add "Start Server" button to server control and tray context menu | Done |
| TRAY-05 | Unify separate ServerControlWindow + LogViewerWindow into single tabbed MainWindow | Done |
| TRAY-06 | Hide console window when tray app launches server process (`CreateNoWindow = true`) | Done |
| IPC-01 | Remove old server-side `H.NotifyIcon` tray icon — now redundant with Avalonia tray app | Done |
| IPC-02 | Remove console-hide behavior (P/Invoke `GetConsoleWindow`/`ShowWindow`, `VsConsoleWindow`, `ConsoleVisible`, `AppProcessStarted`) | Done |
| IPC-03 | Split `Start.Init()` into `InitEssential()` (Phase 1) + `InitRemaining()` (Phase 2-4) for early IPC availability | Done |
| IPC-04 | Add pre-completed task support to `StartupTaskRunner` for split-phase execution | Done |
| IPC-05 | Move IPC pipe listener startup before auth/networking/registration — IPC available in ~1s | Done |
| INFRA-01 | Fix `DetermineInitialPhase` never being called — server always entered setup-required mode | Done |
| INFRA-02 | Move HTTPS config to per-listener so IPC transport stays plain HTTP | Done |
| INFRA-03 | Add IPC connect timeout (3s) to prevent indefinite hangs | Done |
| INFRA-04 | Remove unused `ManagementPort` config and separate management HTTP listener | Done |
| INFRA-05 | Exempt management routes from HTTPS redirect and setup-mode middleware | Done |
| BOOT-06 | Handle port-in-use error gracefully — detect blocking process, prompt to kill | Done |

**Key architectural change — Early IPC**: The original design had the IPC pipe start after all 4 startup phases completed (including slow network probe, auth, and registration). The tray app couldn't connect for 10-30 seconds after server launch. Now Phase 1 runs first (~1s), the web host starts (IPC available), and Phase 2-4 run concurrently in the background. The tray sees "Starting" status immediately.

**Key removal — Server-side tray icon**: The original `TrayIcon.cs` (162 lines) used `H.NotifyIcon` to create a Windows system tray icon from the server process itself. This is now handled entirely by the separate `NoMercy.Tray` Avalonia application, so the server-side icon, console-hide behavior, and the `H.NotifyIcon` NuGet dependency were all removed.

---

