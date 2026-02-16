# Event-Driven Refactoring Opportunities

## HIGH Priority

### 1. EncodeVideoJob - 9+ direct SendToAll calls
**File:** `src/NoMercy.MediaProcessing/Jobs/MediaJobs/EncodeVideoJob.cs`
**Current:** Direct `Networking.Networking.SendToAll("encoder-progress", "dashboardHub", ...)` for each encoding stage
**Event:** `EncodingStageChangedEvent` with stage name, progress, job ID
**Status:** [x] — Replaced all 10 SendToAll calls with `PublishStageAsync` helpers publishing `EncodingStageChangedEvent`

### 2. EncodeMusicJob - Direct SendToAll calls
**File:** `src/NoMercy.MediaProcessing/Jobs/MediaJobs/EncodeMusicJob.cs`
**Current:** Direct `SendToAll` calls mixed with encoding logic
**Event:** Reuse `EncodingStageChangedEvent`
**Status:** [x] — Replaced 2 SendToAll calls with `EncodingStageChangedEvent` publishing

### 3. LibrariesController - Cache invalidation side effects
**File:** `src/NoMercy.Api/Controllers/V1/Dashboard/LibrariesController.cs`
**Current:** `DynamicStaticFilesMiddleware.AddPath/RemovePath` side effects inline
**Event:** `FolderPathAddedEvent` / `FolderPathRemovedEvent`
**Handler:** `FolderPathEventHandler`
**Status:** [x] — 3 middleware calls replaced with event publishing

### 4. Music Controllers - Dual event + delegate pattern
**Files:** `TracksController.cs`, `AlbumsController.cs`, `ArtistsController.cs`
**Current:** Both `_eventBus.PublishAsync` AND `OnLikeEvent?.Invoke` for same action
**Event:** `MusicItemLikedEvent`
**Handler:** `MusicLikeEventHandler`
**Status:** [x] — Removed static OnLikeEvent delegates, replaced with event bus. Cleaned up MusicHub subscriptions.

## MEDIUM Priority

### 5. FileRepository - Inline SendToAll notifications
**File:** `src/NoMercy.MediaProcessing/Files/FileRepository.cs`
**Current:** `SendToAll("Notify", ...)` inline in repository
**Event:** `UserNotificationEvent`
**Handler:** `SignalRNotificationEventHandler`
**Status:** [x] — Replaced 2 SendToAll calls with event publishing

### 6. FileRepository - Direct job.Handle() side effect
**File:** `src/NoMercy.MediaProcessing/Files/FileRepository.cs`
**Current:** `AddMovieJob`/`AddShowJob` executed directly as side effect
**Event:** N/A — kept as-is; downstream code requires synchronous execution
**Status:** [x] — Reviewed; job.Handle() must stay synchronous. Only notifications were replaced.

### 7. UsersController - Direct claims updates
**File:** `src/NoMercy.Api/Controllers/V1/Dashboard/UsersController.cs`
**Current:** `ClaimsPrincipleExtensions.AddUser/RemoveUser/UpdateUser` inline
**Event:** N/A — no side effects found (no SendToAll, no broadcasts). Pure in-memory claims management.
**Status:** [x] — Reviewed; no event-driven refactoring needed.

### 8. PlaylistsController - Missing event on Create
**File:** `src/NoMercy.Api/Controllers/V1/Music/PlaylistsController.cs`
**Current:** Create action had no `LibraryRefreshEvent`. File I/O is acceptable inline for image upload.
**Status:** [x] — Added `LibraryRefreshEvent` to Create. File I/O kept inline (direct user action).

### 9. UserDataController - CRUD without events
**File:** `src/NoMercy.Api/Controllers/V1/Media/UserDataController.cs`
**Current:** RemoveContinue deleted data without publishing events for UI refresh
**Event:** `LibraryRefreshEvent` with `["continue-watching"]` query key
**Status:** [x] — Added event publishing to RemoveContinue. Watched/Favorites are incomplete stubs (no-op).

### 10. Generic LibraryRefreshEvent overuse
**Current:** Used 50+ times for any data change, loses semantic meaning
**Event:** Specific events: `MovieLikedEvent`, `AlbumFavoriteChangedEvent`, etc.
**Status:** [ ] — Deferred. Generic events work correctly; specific events are a future optimization.

## LOW Priority

### 11. Networking.cs - Untracked UPnP discovery
**File:** `src/NoMercy.Networking/Networking.cs` (line 51)
**Current:** `_ = Task.Run(() => NatUtility.StartDiscovery())`
**Event:** `UPnPDiscoveryEvent`
**Status:** [ ]

### 12. ConfigurationChangedEvent - Never published
**Current:** Event class exists but is never published anywhere
**Status:** [ ]

### 13. Admin controllers - No audit trail
**Current:** Library/user CRUD has no audit events
**Event:** `AdminActionEvent`
**Status:** [ ]

## COMPLETED

### FileWatcher - Event-driven rewrite
**Commit:** dc8585c
**Events:** `FileCreatedEvent`, `FileDeletedEvent`, `FileRenamedEvent`
**Handler:** `FileWatcherEventHandler`
**Status:** [x]
