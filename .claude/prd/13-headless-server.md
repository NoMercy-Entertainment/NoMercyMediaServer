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

---

