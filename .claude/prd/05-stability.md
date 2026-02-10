## 5. Stability

> Items that cause crashes, resource leaks, thread safety violations, or system instability.

### CRIT-01: DbContext Instantiation Anti-Pattern
- **Files**: `src/NoMercy.Data/Repositories/MusicRepository.cs` (20+ instances), `src/NoMercy.Api/Controllers/V1/Media/SearchController.cs` (4 instances), `src/NoMercy.Api/Controllers/V1/Media/HomeController.cs`, `src/NoMercy.Api/Controllers/V1/Media/LibrariesController.cs`
- **Problem**: `new MediaContext()` created outside DI container throughout the codebase (~40+ occurrences)
- **Impact**: Memory leaks, connection pool exhaustion, orphaned DbContexts, no transaction support
- **Example**:
  ```csharp
  // MusicRepository.cs:17 - Creates unmanaged context
  public Task<Artist?> GetArtistAsync(Guid userId, Guid id)
  {
      MediaContext context = new();  // NOT from DI
      return context.Artists...
  }
  ```
- **Fix**: Context-dependent — **DbContext is NOT thread-safe**, so the fix depends on how the code runs:

  | Scenario | Fix |
  |----------|-----|
  | Sequential method (single thread) | Use the DI-injected scoped `MediaContext` |
  | Inside `Task.WhenAll` / parallel branches | Use `IDbContextFactory<MediaContext>` to create one context per branch |
  | Inside `Parallel.ForEach` | Use `IDbContextFactory<MediaContext>` per iteration |
  | Inside `Task.Run()` | Use `IDbContextFactory<MediaContext>` |
  | Background jobs (no DI scope) | Use `IDbContextFactory<MediaContext>` or `IServiceScopeFactory` |

  **CRITICAL WARNING**: Replacing `new MediaContext()` with the injected scoped context inside concurrent code (`Task.WhenAll`, `Parallel.ForEach`, `Task.Run`) will CRASH the server. Each parallel branch MUST have its own context instance.

  **Registration change required**:
  ```csharp
  // In ServiceConfiguration.cs — add factory alongside existing registration:
  services.AddDbContextFactory<MediaContext>(options =>
      options.UseSqlite($"Data Source={AppFiles.MediaDatabase}; Pooling=True"));
  ```

  **Sequential fix** (e.g., MusicRepository single-threaded methods):
  ```csharp
  // Use the constructor-injected context
  public Task<Artist?> GetArtistAsync(Guid userId, Guid id)
  {
      return _mediaContext.Artists...  // Injected, NOT new()
  }
  ```

  **Concurrent fix** (e.g., SearchController with Task.WhenAll):
  ```csharp
  // Inject IDbContextFactory, create per-branch
  Task<List<Artist>> artistsTask = Task.Run(async () =>
  {
      await using MediaContext context = await _contextFactory.CreateDbContextAsync();
      return await context.Artists.Where(...).ToListAsync();
  });
  ```

- **Tests Required**:
  - [ ] Unit test: Verify sequential repository methods use injected context
  - [ ] Unit test: Verify concurrent code paths use factory-created contexts
  - [ ] Integration test: Verify connection pool not exhausted under concurrent load
  - [ ] Integration test: 10 concurrent SearchController requests don't throw
  - [ ] API test: Verify endpoints return correct data after migration

### CRIT-04: Blocking `.Wait()` / `.Result` in Async Code
- **Files**: `src/NoMercy.Api/Controllers/Socket/music/MusicPlaybackService.cs:42,45`, `src/NoMercy.Api/Controllers/Socket/video/VideoPlaybackService.cs:42,55`, `src/NoMercy.Api/Controllers/V1/Media/HomeController.cs:292`, `src/NoMercy.Queue/JobQueue.cs:58,227`
- **Problem**: Using `.Wait()` and `.Result` blocks thread pool threads and causes deadlocks
- **Impact**: Thread starvation under load, potential deadlocks, kills async scalability
- **Example**:
  ```csharp
  // HomeController.cs:290-293 - Synchronous polling loop
  while (!File.Exists(Path.Combine(folder, "video_00002.ts")))
  {
      Task.Delay(1000).Wait();  // BLOCKS thread
  }
  ```

  **IMPORTANT CONTEXT — HomeController Polling Loop**: The `HomeController.cs:290-293` loop **intentionally** waits for the HLS segment file to exist before returning a response to the player. This ensures smooth playback by guaranteeing the first segments are ready. The fix must **preserve this behavior** — the endpoint must still wait for the file. The issue is *how* it waits (blocking a thread pool thread), not *that* it waits.

  **Fix for HomeController specifically**:
  ```csharp
  // Replace blocking wait with async wait — same behavior, no thread blocked
  while (!File.Exists(Path.Combine(folder, "video_00002.ts")))
  {
      await Task.Delay(1000, cancellationToken);  // Non-blocking
  }
  ```
  Optionally add a timeout to prevent infinite waits if encoding fails:
  ```csharp
  using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
  cts.CancelAfter(TimeSpan.FromSeconds(30));
  while (!File.Exists(Path.Combine(folder, "video_00002.ts")))
  {
      await Task.Delay(1000, cts.Token);  // Throws after 30s if file never appears
  }
  ```

- **Fix (general)**: Replace all `.Wait()` / `.Result` with `await`, make methods `async` throughout
- **Tests Required**:
  - [ ] Unit test: Verify no `.Wait()` or `.Result` calls remain (static analysis)
  - [ ] Integration test: HomeController still waits for segment file before returning response
  - [ ] Integration test: HomeController times out gracefully if encoding fails
  - [ ] Integration test: Concurrent playback sessions don't deadlock
  - [ ] Load test: 50 concurrent SignalR connections maintain responsiveness

### CRIT-05: Static DbContext in ClaimsPrincipleExtensions
- **File**: `src/NoMercy.Helpers/ClaimsPrincipleExtensions.cs:10-18`
- **Problem**: Static `MediaContext` and `Users` list loaded once at startup, never refreshed
- **Impact**: New users invisible until restart; static DbContext holds resources forever; race conditions on manual mutation
- **Example**:
  ```csharp
  public static class ClaimsPrincipleExtensions
  {
      private static readonly MediaContext MediaContext = new();  // STATIC - never disposed
      public static readonly List<User> Users = MediaContext.Users.ToList();  // Frozen at startup
  }
  ```
- **Fix**: Replace with DI-injected repository pattern; load users per-request
- **Tests Required**:
  - [ ] Unit test: New user created after startup is accessible
  - [ ] Unit test: Deleted user removed after operation
  - [ ] Integration test: User permissions update without restart

### CRIT-06: Lock on DbContext (Thread Safety Violation)
- **File**: `src/NoMercy.Queue/JobQueue.cs:16,29,56,91,133,162,196`
- **Problem**: `lock (Context)` used on DbContext which is NOT thread-safe
- **Impact**: Data corruption, unpredictable behavior, false sense of thread safety

  **IMPORTANT CONTEXT — Intentional SQLite Write Serialization**: The `lock(Context)` pattern exists because **SQLite only allows one writer at a time**. Without this lock, concurrent `SaveChanges()` calls would cause `SQLITE_BUSY` errors. The lock serializes all database mutations so only one thread writes at a time. This is a **correct solution to a real problem** — the issue is using `Context` as the lock object (confusing) and holding the lock too long.

  **The fix must preserve write serialization** while improving clarity:

  ```csharp
  // WRONG: Remove the lock entirely (causes SQLite BUSY errors)
  // queue.Enqueue(job); // Two threads → SQLITE_BUSY crash

  // RIGHT: Use a proper lock object, keep serialization
  private static readonly object _writeLock = new();

  public void Enqueue(QueueJob job)
  {
      lock (_writeLock)  // Clear intent: serializing writes
      {
          Context.QueueJobs.Add(job);
          Context.SaveChanges();
      }
  }
  ```

  **Long-term**: When queue is decoupled (Phase 6), replace lock with `SemaphoreSlim` for async-compatible write serialization, or use `IDbContextFactory` with per-operation contexts.

- **Fix**: Replace `lock(Context)` with a dedicated `lock(_writeLock)` object; keep write serialization
- **Tests Required**:
  - [ ] Unit test: Concurrent job enqueue operations succeed without SQLITE_BUSY errors
  - [ ] Integration test: Queue under heavy concurrent load processes correctly
  - [ ] Stress test: 100 concurrent dispatches maintain data integrity
  - [ ] Unit test: Lock object is NOT the DbContext instance

### CRIT-07: HttpClient Socket Exhaustion
- **Files**: `src/NoMercy.Providers/Helpers/BaseClient.cs:25,44`, `src/NoMercy.Providers/MusicBrainz/Client/MusicBrainzBaseClient.cs:18,29`, `src/NoMercy.Providers/OpenSubtitles/Client/OpenSubtitlesBaseClient.cs:61`
- **Problem**: Each provider instance creates its own `new HttpClient()` instead of using factory/singleton
- **Impact**: Socket exhaustion under load (TIME_WAIT accumulation), connection pool defeat
- **Additional**: OpenSubtitlesBaseClient.Dispose() throws `NotImplementedException`
- **Revalidation note**: BaseClient already attempts a singleton-like pattern (one client per provider instance, reused across calls). The issue is that provider instances themselves are created frequently, so new HttpClients still accumulate. Severity confirmed but the existing pattern shows intent to reuse.

  **IMPORTANT CONTEXT — Per-Provider Configuration is Intentional**: Each provider (TMDB, TVDB, MusicBrainz, etc.) has different base URLs, API keys, auth headers, and timeouts. Creating separate `HttpClient` instances ensures configuration isolation. The problem is not that there are multiple clients — it's that they're not **reused** across calls to the same provider.

  **The fix must preserve per-provider config isolation**:
  ```csharp
  // WRONG: Single shared HttpClient (loses per-provider headers)
  // WRONG: Static HttpClient with no disposal (DNS caching issues)

  // RIGHT: IHttpClientFactory with named clients (best of both worlds)
  // In ServiceConfiguration.cs:
  services.AddHttpClient("TMDB", client =>
  {
      client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
      client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tmdbApiKey}");
      client.Timeout = TimeSpan.FromMinutes(5);
  });

  services.AddHttpClient("MusicBrainz", client =>
  {
      client.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
      client.DefaultRequestHeaders.Add("User-Agent", "NoMercy/1.0");
  });

  // In provider code:
  public class TmdbClient(IHttpClientFactory factory)
  {
      private readonly HttpClient _client = factory.CreateClient("TMDB");
  }
  ```

  **Also fix**: OpenSubtitlesBaseClient.Dispose() — replace `throw new NotImplementedException()` with proper `_client?.Dispose()`.

- **Fix**: Use `IHttpClientFactory` with named clients per provider; fix OpenSubtitles Dispose
- **Tests Required**:
  - [ ] Unit test: Each provider gets correctly configured HttpClient
  - [ ] Unit test: Provider reuses HttpClient across calls to same provider
  - [ ] Integration test: 100 sequential API calls don't exhaust sockets
  - [ ] Unit test: OpenSubtitles client properly implements IDisposable

### CRIT-08: Fire-and-Forget Tasks Without Await
- **File**: `src/NoMercy.Queue/QueueRunner.cs:44,138,144,183`
- **Problem**: `Task.Run(() => new Thread(() => SpawnWorker(name)).Start())` - tasks created but not tracked; `GetAwaiter()` called without `await`
- **Impact**: Race conditions, uncaught exceptions, orphaned threads

  **IMPORTANT CONTEXT — Intentional Thread Spawning**: The `Task.Run(() => new Thread(() => SpawnWorker(name)).Start())` pattern is **intentional**. It spawns workers in dedicated threads so they don't block further execution. The problem is NOT the thread spawning itself — the problems are:
  1. **No exception handling**: If `SpawnWorker` throws, the exception is swallowed silently
  2. **No lifecycle tracking**: There's no way to know if a worker is alive, crashed, or stalled
  3. **`GetAwaiter()` without `await`**: Creates a continuation that nobody observes

  **The fix must preserve the non-blocking spawn behavior** while adding observability:

  ```csharp
  // Option A: Keep dedicated threads, add tracking + error handling
  var thread = new Thread(() =>
  {
      try
      {
          SpawnWorker(name);
      }
      catch (Exception ex)
      {
          _logger.LogError(ex, "Worker {Name} crashed", name);
          // Optionally restart the worker
      }
  })
  { IsBackground = true, Name = $"QueueWorker-{name}" };
  thread.Start();
  _activeWorkers.TryAdd(name, thread);  // Track for lifecycle management

  // Option B: Use Task.Factory for long-running + tracking
  Task workerTask = Task.Factory.StartNew(
      () => SpawnWorker(name),
      CancellationToken.None,
      TaskCreationOptions.LongRunning,  // Hints scheduler to use dedicated thread
      TaskScheduler.Default);
  _workerTasks.TryAdd(name, workerTask);
  // Fire-and-forget is OK here, but log exceptions:
  workerTask.ContinueWith(t =>
      _logger.LogError(t.Exception, "Worker {Name} crashed", name),
      TaskContinuationOptions.OnlyOnFaulted);
  ```

- **Fix**: Add exception handling and lifecycle tracking to worker spawns; preserve non-blocking behavior
- **Tests Required**:
  - [ ] Unit test: Worker spawn does not block the calling thread
  - [ ] Unit test: Worker crash is logged and observable
  - [ ] Unit test: Active workers are tracked and queryable
  - [ ] Integration test: Failed worker spawn is logged and optionally retried
  - [ ] Integration test: All workers shut down cleanly on server stop

### CRIT-11: FFmpeg Process Resource Leak
- **File**: `src/NoMercy.Encoder/FfMpeg.cs:98-124,126-302`
- **Problem**: Process objects not wrapped in `using`; static dictionary accumulates orphans on exception
- **Impact**: OS handle exhaustion; zombie processes on failure

  **IMPORTANT CONTEXT — Static Process Dictionary is Intentional**: The `Dictionary<int, Process>` exists so that **other parts of the system** (SignalR hubs, API endpoints) can pause, resume, or kill encoding jobs. Without it, once an encoding starts, there's no way to control it. The dictionary enables cross-caller process management.

  **The fix must preserve cross-caller process control** while preventing leaks:
  ```csharp
  // Use ConcurrentDictionary (thread-safe) instead of Dictionary
  private static readonly ConcurrentDictionary<int, Process> FfmpegProcesses = new();

  // Add try-finally to ensure cleanup on failure
  Process ffmpeg = new() { StartInfo = startInfo };
  ffmpeg.Start();
  FfmpegProcesses.TryAdd(ffmpeg.Id, ffmpeg);
  try
  {
      await ffmpeg.WaitForExitAsync(cancellationToken);
  }
  finally
  {
      FfmpegProcesses.TryRemove(ffmpeg.Id, out _);
      if (!ffmpeg.HasExited)
      {
          try { ffmpeg.Kill(entireProcessTree: true); } catch { }
      }
      ffmpeg.Dispose();
  }
  ```

- **Fix**: Replace `Dictionary` with `ConcurrentDictionary`; add try-finally cleanup; preserve cross-caller access
- **Tests Required**:
  - [ ] Unit test: Process is disposed even when exception occurs
  - [ ] Unit test: Static process dictionary cleaned up after job failure
  - [ ] Unit test: Pause/Resume still works via static dictionary
  - [ ] Unit test: Concurrent encoding jobs don't corrupt the dictionary

### HIGH-09: Temporary Files Not Cleaned on Encoding Failure
- **File**: `src/NoMercy.MediaProcessing/Jobs/MediaJobs/EncodeVideoJob.cs:254-268`
- **Problem**: Failed encoding leaves gigabytes of partial output on disk
- **Fix**: Add cleanup in catch block for partial encoding output
- **Tests Required**:
  - [ ] Integration test: Failed encoding job cleans up temp files

### HIGH-10: Async Void Queue Processor
- **File**: `src/NoMercy.Providers/Helpers/Queue.cs:51`
- **Problem**: `async void RunTasks()` means exceptions are unobservable

  **IMPORTANT CONTEXT — Fire-and-Forget by Design**: `RunTasks()` is a background loop that dequeues and executes rate-limited API calls. Once started by `StartQueue()`, it runs independently forever. Converting to `Task`-returning would require the caller to `await` it (blocking on the entire queue lifetime) or use `_ = RunTasks()` (same as current behavior but with extra complexity).

  **The real fix is better exception handling, not changing the return type**:
  ```csharp
  private async void RunTasks()
  {
      while (ShouldRun)
      {
          try
          {
              await Dequeue();
          }
          catch (OperationCanceledException)
          {
              break;  // Graceful shutdown
          }
          catch (Exception ex)
          {
              Logger.Error(ex, "Queue processor error");
              await Task.Delay(1000);  // Back off on error
          }
      }
  }
  ```

- **Fix**: Keep `async void`; add structured exception logging and error back-off
- **Tests Required**:
  - [ ] Unit test: Queue processor exceptions are logged (not swallowed)
  - [ ] Unit test: Queue continues processing after transient error
  - [ ] Unit test: Queue stops cleanly on cancellation

### HIGH-16: Race Condition in Worker Counter
- **File**: `src/NoMercy.Queue/QueueRunner.cs:133-150`
- **Problem**: Non-atomic counter increment; multiple threads can read same value
- **Fix**: Use `Interlocked.Increment()` or proper locking
- **Tests Required**:
  - [ ] Stress test: 20 concurrent worker count updates maintain accuracy

### HIGH-17: Static JobQueue Instance with Single DbContext
- **File**: `src/NoMercy.Queue/JobDispatcher.cs:9`
- **Problem**: `private static readonly JobQueue Queue = new(new());` — one DbContext for entire app lifetime

  **IMPORTANT CONTEXT — Works Due to Lock Serialization**: The single static `QueueContext` is protected by `lock(Context)` in `JobQueue` (see CRIT-06). Since all writes are serialized, the single context doesn't cause corruption. The reasoning: one queue, one database, one context — simple and predictable.

  **Actual problems**:
  1. **Change tracker bloat**: The context accumulates tracked entities over the app's lifetime, slowly consuming memory
  2. **No connection recovery**: If the SQLite connection breaks, the context can't reconnect
  3. **Static = untestable**: Can't mock `JobDispatcher` in tests

  **Short-term fix**:
  ```csharp
  public void Enqueue(QueueJob job)
  {
      lock (_writeLock)
      {
          Context.QueueJobs.Add(job);
          Context.SaveChanges();
          Context.ChangeTracker.Clear();  // Prevent accumulation
      }
  }
  ```

  **Long-term**: Inject via DI when queue is decoupled (Phase 6).

- **Fix**: Short-term: add `ChangeTracker.Clear()` after saves; long-term: inject via DI (Phase 6)
- **Tests Required**:
  - [ ] Integration test: Job dispatch works correctly after 10,000+ dispatches
  - [ ] Memory test: Context doesn't accumulate tracked entities

### HIGH-18: Thread.Sleep Inside Lock
- **File**: `src/NoMercy.Queue/JobQueue.cs:75,119,184`
- **Problem**: `Thread.Sleep(2000)` while holding `lock(Context)` blocks ALL other workers

  **IMPORTANT CONTEXT — Retry Mechanism for SQLite Locking**: The `Thread.Sleep(2000)` inside the lock is a retry mechanism for `SQLITE_BUSY` errors. When SQLite's database is locked by another writer, the code waits 2 seconds then retries (up to 10 attempts). The lock is held during sleep to prevent OTHER threads from also failing on the same busy database.

  **The problem**: This blocks all other workers from doing anything for 2 seconds per retry (up to 20 seconds total). In normal operation it's rare, but under contention it cascades.

  **Safer approach** — release the lock during sleep, use a flag to prevent stampede:
  ```csharp
  catch (Exception e)
  {
      if (e.Source == "Microsoft.EntityFrameworkCore.Relational") return null;
      if (attempt < 5)
      {
          // Release lock during sleep so other operations proceed
          Thread.Sleep(2000 + Random.Shared.Next(500));  // Jitter to prevent stampede
          return ReserveJob(name, currentJobId, attempt + 1);  // Also fix missing return!
      }
      Logger.Queue(e.Message, LogEventLevel.Error);
  }
  ```

  **Long-term**: SQLite's `busy_timeout` pragma can handle this at the database level, avoiding application-level retry entirely.

- **Fix**: Reduce retries (5 max), add jitter, fix missing `return`; consider SQLite `busy_timeout`
- **Tests Required**:
  - [ ] Unit test: Job retry doesn't block other queue operations for more than necessary
  - [ ] Unit test: Retry with jitter prevents thundering herd
  - [ ] Integration test: Concurrent workers survive SQLITE_BUSY gracefully

### HIGH-20: Blocking Property Getter for Network Discovery
- **File**: `src/NoMercy.Networking/Networking.cs:91-103`
- **Problem**: `ExternalIp` getter calls `.Result` on async operation

  **IMPORTANT CONTEXT — Lazy Init from Non-Async Context**: Properties in C# can't be `async`, so the developer used `.Result` as a lazy-initialization fallback. The external IP is needed by domain name generation and other sync code paths.

  **Safest fix** — pre-populate during startup so the getter never blocks:
  ```csharp
  // In Discover() (called during startup):
  public static async Task Discover()
  {
      // ... existing UPnP discovery ...
      _externalIp = await GetExternalIp();  // Populate eagerly
  }

  // Property never blocks now:
  public static string ExternalIp
  {
      get => _externalIp ?? "0.0.0.0";  // Fallback if startup failed
      set => _externalIp = value;
  }
  ```

- **Fix**: Pre-populate external IP during `Discover()` startup; remove `.Result` from getter
- **Tests Required**:
  - [ ] Unit test: ExternalIp returns cached value without blocking
  - [ ] Integration test: External IP populated after Discover() completes

### MED-04: Orphaned DbContext in MusicJob
- **File**: `src/NoMercy.Data/Jobs/MusicJob.cs:14`
- **Revalidation note**: The class already implements `IDisposable` and `IAsyncDisposable` with proper disposal of the `_mediaContext` field. However, the field appears unused in the `Handle()` method — it's created but never queried. The disposal pattern is correct; the waste is in creating an unnecessary context.
- **Fix**: Remove unused `_mediaContext` field entirely (it's disposed but never used)

### MED-05: 60+ Forced GC.Collect() Calls Across Codebase
- **Files**: 20+ files, every job base class, Dispose methods throughout:
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractMediaJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractEncoderJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractMusicFolderJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractLyricJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractFanArtDataJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractReleaseJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractShowExtraDataJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractMediaExraDataJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractMusicDescriptionJob.cs`
  - `src/NoMercy.MediaProcessing/Jobs/MediaJobs/AbstractMusicEncoderJob.cs`
  - `src/NoMercy.MediaProcessing/Images/BaseImageManager.cs`
  - `src/NoMercy.MediaProcessing/Libraries/LibraryRepository.cs`
  - `src/NoMercy.Data/Logic/FileLogic.cs`
  - `src/NoMercy.Data/Logic/LibraryLogic.cs`
  - `src/NoMercy.Data/Jobs/MusicJob.cs`
  - `src/NoMercy.NmSystem/MediaScan.cs`
  - `src/NoMercy.Providers/TMDB/Client/TmdbSeasonClient.cs`

- **Problem**: Every Dispose method forces a full garbage collection:
  ```csharp
  public void Dispose()
  {
      _mediaContext.Dispose();
      GC.Collect();                    // Stop-the-world pause (ALL threads freeze)
      GC.WaitForFullGCComplete();      // Block until complete
      GC.WaitForPendingFinalizers();   // Block again for finalizers
  }
  ```

- **Why this is bad**:
  1. **`GC.Collect()` freezes your ENTIRE app** — every thread pauses while the GC scans all memory. On a server with active video streams, this causes playback stuttering.
  2. **Called hundreds of times during a library scan** — each job's Dispose triggers a full collection. Scanning 1000 songs = 1000 full GC pauses.
  3. **`GC.WaitForFullGCComplete()` + `GC.WaitForPendingFinalizers()` blocks the current thread twice** — the calling thread can't do anything else during this time.
  4. **.NET's GC is already automatic** — it runs on its own when memory pressure exists. Forcing it is almost always worse than letting it decide when to run.
  5. **Hides the real problem** — if memory climbs during scans, it's because objects aren't being disposed properly (missing `using` blocks), not because the GC isn't running often enough.

- **The root cause** (why memory was climbing without GC.Collect):
  - `Image<Rgba32>` from SixLabors.ImageSharp allocates large unmanaged buffers — if not disposed via `using`, they linger until the GC gets around to them
  - `MediaContext` holds a connection pool — if not disposed, connections accumulate
  - `HttpResponseMessage` bodies hold network buffers — must be disposed
  - FFmpeg `Process` objects hold OS handles

  **Fix the root cause, then remove ALL GC calls**:
  ```csharp
  // BEFORE: Allocate, forget to dispose, force GC to clean up
  Image<Rgba32> image = Image.Load<Rgba32>(path);
  // ... process image ...
  // No dispose! Memory climbs → forced GC.Collect() as band-aid

  // AFTER: Proper disposal, GC handles the rest automatically
  using Image<Rgba32> image = Image.Load<Rgba32>(path);
  // ... process image ...
  // Disposed at end of scope → memory freed immediately → no GC.Collect needed
  ```

  ```csharp
  // BEFORE: Job Dispose forces GC
  public void Dispose()
  {
      _mediaContext.Dispose();
      GC.Collect();  // Band-aid for memory not being freed
  }

  // AFTER: Just dispose what you own
  public void Dispose()
  {
      _mediaContext.Dispose();
      // That's it. .NET handles the rest.
  }
  ```

  **The one exception** — `GC.SuppressFinalize(this)` in `ServerRegistrationService.cs` is correct and should stay. It tells the GC "I already cleaned up, don't run my finalizer."

- **Fix**:
  1. Audit all classes with GC.Collect for missing `using` blocks on heavy objects (Image, HttpResponse, Process, Stream)
  2. Add `using` where missing
  3. Remove ALL `GC.Collect()` / `GC.WaitForFullGCComplete()` / `GC.WaitForPendingFinalizers()` calls (60+ occurrences)
  4. Keep `GC.SuppressFinalize(this)` where appropriate (standard IDisposable pattern)
  5. Remove finalizers (`~MusicJob()`) — if Dispose is called properly, finalizers are unnecessary
- **Severity**: Upgraded from Low to **High** — causes app-wide freezes during scans, affects streaming playback
- **Memory Baseline** (must not regress after changes):
  - **Idle**: ~200MB — daily use with no active tasks
  - **Under load**: 2-3GB max — during heavy scanning/encoding

- **Tests Required**:
  - [ ] Memory test: Idle server stays under 250MB after removing GC.Collect calls
  - [ ] Memory test: Library scan of 1000 files peaks under 3GB, returns to ~200MB after completion
  - [ ] Memory test: No memory leak after processing 100 images (Image<Rgba32> properly disposed)
  - [ ] Performance test: No playback stuttering during concurrent library scan
  - [ ] Audit: Every `Image.Load`, `new HttpClient`, `Process.Start` has a corresponding `using` or `Dispose`

---

### DISP-01: Add missing `using` to Image<Rgba32> in hot paths (11 instances)

The root cause of memory growth that GC.Collect was masking. Each `Image<Rgba32>` holds 5-50MB of unmanaged memory. During a library scan with thousands of images, these leak and accumulate.

**Problem**: Image objects returned from methods or created in local variables without `using`. Callers may or may not dispose them.

| # | File | Line | Code | Context |
|---|------|------|------|---------|
| 1 | `src/NoMercy.MediaProcessing/Files/FileManager.cs` | 587 | `Image image = Image.Load(filePath);` | `GetImageDimensions()` — loads full image just to read Width/Height, never disposes |
| 2 | `src/NoMercy.Helpers/Images.cs` | 43 | `return Image.Load<Rgba32>(image);` | `ReadFileStream()` — returns image, caller must dispose |
| 3 | `src/NoMercy.Providers/TMDB/Client/TmdbImageClient.cs` | 43 | `return ... await Image.LoadAsync<Rgba32>(filePath);` | `Download()` — 3 return paths (lines 43, 53, 60) all return unmanaged Image |
| 4 | `src/NoMercy.Providers/TMDB/Client/TmdbImageClient.cs` | 53 | `Image.Load<Rgba32>(await response.Content.ReadAsStreamAsync())` | Double leak — Stream AND Image |
| 5 | `src/NoMercy.Providers/TMDB/Client/TmdbImageClient.cs` | 60 | `return ... Image.Load<Rgba32>(filePath);` | Third return path |
| 6 | `src/NoMercy.Providers/FanArt/Client/FanArtImageClient.cs` | 38 | `return Image.Load<Rgba32>(filePath);` | `Download()` — 2 return paths (38, 50) |
| 7 | `src/NoMercy.Providers/FanArt/Client/FanArtImageClient.cs` | 50 | `return Image.Load<Rgba32>(bytes);` | Second return path |
| 8 | `src/NoMercy.Providers/CoverArt/Client/CoverArtCoverArtClient.cs` | 56 | `return Image.Load<Rgba32>(filePath);` | `Download()` — 2 return paths (56, 68) |
| 9 | `src/NoMercy.Providers/CoverArt/Client/CoverArtCoverArtClient.cs` | 68 | `return Image.Load<Rgba32>(bytes);` | Second return path |
| 10 | `src/NoMercy.Providers/NoMercy/Client/NoMercyImageClient.cs` | 30 | `return Image.Load<Rgba32>(filePath);` | `Download()` — 2 return paths (30, 44) |
| 11 | `src/NoMercy.Providers/NoMercy/Client/NoMercyImageClient.cs` | 44 | `return Image.Load<Rgba32>(bytes);` | Second return path |

**Fix approach**:
- **FileManager.GetImageDimensions()**: Wrap in `using` — only needs Width/Height, image should be disposed immediately
- **Image download methods** (TMDB, FanArt, CoverArt, NoMercy): These return `Image<Rgba32>?` to callers. The fix is two-fold:
  1. Ensure every **caller** of these `Download()` methods wraps the result in `using`
  2. Add `using` to intermediate streams (like `ReadAsStreamAsync()`)
- **Images.ReadFileStream()**: Caller is responsible — verify all callers use `using`

**Tests Required**:
- [ ] Audit test: Scan source for `Image.Load` / `Image.LoadAsync` without `using` in the same scope
- [ ] Audit test: Verify callers of provider `Download()` methods wrap result in `using`

---

### DISP-02: Add missing `using` to HttpResponseMessage (7 instances)

HttpResponseMessage implements IDisposable and holds network buffers. Every API call that doesn't dispose the response leaks memory.

| # | File | Line | Code | Path |
|---|------|------|------|------|
| 1 | `src/NoMercy.Providers/TMDB/Client/TmdbImageClient.cs` | 48 | `HttpResponseMessage response = await httpClient.GetAsync(url);` | Hot — every TMDB image |
| 2 | `src/NoMercy.Providers/FanArt/Client/FanArtImageClient.cs` | 42 | `HttpResponseMessage response = await httpClient.GetAsync(url);` | Hot — every fanart image |
| 3 | `src/NoMercy.Providers/CoverArt/Client/CoverArtCoverArtClient.cs` | 60 | `HttpResponseMessage response = await httpClient.GetAsync(url);` | Hot — every album art |
| 4 | `src/NoMercy.Providers/NoMercy/Client/NoMercyImageClient.cs` | 36 | `HttpResponseMessage response = await httpClient.GetAsync(url);` | Hot — internal images |
| 5 | `src/NoMercy.Providers/Other/KitsuIO.cs` | 17 | `HttpResponseMessage response = await client.GetAsync(...)` | Hot — anime checks |
| 6 | `src/NoMercy.Setup/Binaries.cs` | 71 | `HttpResponseMessage response = await HttpClient.GetAsync(apiUrl);` | Cold — startup |
| 7 | `src/NoMercy.Networking/Certificate.cs` | 109 | `HttpResponseMessage response = await client.GetAsync(serverUrl);` | Cold — cert renewal |

**Fix**: Add `using` to every `HttpResponseMessage` declaration:
```csharp
// Before:
HttpResponseMessage response = await httpClient.GetAsync(url);
// After:
using HttpResponseMessage response = await httpClient.GetAsync(url);
```

**Tests Required**:
- [ ] Audit test: Scan source for `HttpResponseMessage` declarations without `using`

---

### DISP-03: Add missing `using` to TagLib.File / TagFile factory (3 instances + factory)

TagLib.File implements IDisposable and holds file handles. These leak inside `Parallel.ForEach` loops — scanning 1000 songs means 1000 leaked file handles.

| # | File | Line | Code | Context |
|---|------|------|------|---------|
| 1 | `src/NoMercy.NmSystem/Dto/TagFile.cs` | 11 | `FileTag? fileTag = FileTag.Create(path);` | Factory method — creates TagLib.File, leaks inside factory |
| 2 | `src/NoMercy.NmSystem/MediaScan.cs` | 297 | `tagFile = TagFile.Create(file);` | Inside `Parallel.ForEach` — every file scanned |
| 3 | `src/NoMercy.MediaProcessing/Recordings/RecordingManager.cs` | 78 | `TagLib.File tagFile = TagLib.File.Create(file.Path);` | Inside `Parallel.ForEach` — every recording |

**Fix**:
- Fix the `TagFile.Create()` factory to properly handle the inner `FileTag.Create()` with `using` or return ownership clearly
- Wrap `TagFile.Create()` / `TagLib.File.Create()` calls in `using` at all call sites
- Special care in `Parallel.ForEach` — each iteration must dispose independently

**Tests Required**:
- [ ] Audit test: Scan source for `TagFile.Create` / `TagLib.File.Create` without `using`

---

### DISP-04: Add missing `using` to MediaContext, FileStream, Process, Stream (cold paths)

Lower-frequency leaks that should still be fixed for correctness.

| # | File | Line | Object | Context |
|---|------|------|--------|---------|
| 1 | `src/NoMercy.Api/Services/HomeService.cs` | 188 | `MediaContext` (from factory) | Inside LINQ lambda — never disposed |
| 2 | `src/NoMercy.Setup/Auth.cs` | 267 | `FileStream` via `File.OpenWrite()` | Uses `.Close()` instead of `using` |
| 3 | `src/NoMercy.Setup/Auth.cs` | 160 | `HttpResponseMessage` | Token polling loop |
| 4 | `src/NoMercy.Setup/Auth.cs` | 443,445,447 | `Process.Start()` (3x) | Browser launch |
| 5 | `src/NoMercy.Setup/DesktopIconCreator.cs` | 64,71,75,100 | `Process.Start()` (4x) | macOS/Linux shortcut creation |
| 6 | `src/NoMercy.Encoder/FfMpeg.cs` | 497,514 | `Process.Start("kill")` (2x) | Encoding pause/resume |
| 7 | `src/NoMercy.Providers/TMDB/Client/TmdbImageClient.cs` | 53 | `ReadAsStreamAsync()` | Stream passed to Image.Load without dispose |

**Fix**: Add `using` to each declaration. For `Process.Start()`, use:
```csharp
using Process? proc = Process.Start("kill", $"-STOP {process.Id}");
proc?.WaitForExit();
```

**Tests Required**:
- [ ] Audit test: Scan for `Process.Start` without `using`
- [ ] Audit test: Scan for `File.OpenWrite` / `File.OpenRead` / `File.Create` without `using`

---

### MED-15: Non-Volatile Static Booleans
- **File**: `src/NoMercy.Queue/QueueRunner.cs:24,26`
- **Fix**: Use `volatile` keyword or `Interlocked` operations

### 14.1 HttpClient Socket Exhaustion — Per-Provider Breakdown

Every provider creates a `new HttpClient()` per instance. This is the root cause behind CRIT-07 and the single most impactful cross-cutting issue:

| Provider | File | Line | Pattern |
|----------|------|------|---------|
| BaseClient | `Helpers/BaseClient.cs` | 25, 44 | `Client = new()` per instance |
| TMDB | `TMDB/Client/TmdbBaseClient.cs` | 24, 39 | `_client = new()` per instance |
| TVDB | `TVDB/Client/TvdbBaseClient.cs` | 26, 41 | `_client = new()` per instance |
| TVDB | `TvdbBaseClient.cs` | 95 | `new()` in GetToken, **never disposed** |
| MusicBrainz | `MusicBrainz/Client/MusicBrainzBaseClient.cs` | 18, 29 | `_client = new()` per instance |
| AcoustId | `AcoustId/Client/AcoustIdBaseClient.cs` | 15, 27 | `_client = new()` per instance |
| OpenSubtitles | `OpenSubtitles/Client/OpenSubtitlesBaseClient.cs` | 17 | `_client = new()` per instance |
| FanArt | `FanArt/Client/FanArtBaseClient.cs` | 20, 36 | `_client = new()` per instance |
| CoverArt | `CoverArt/Client/CoverArtBaseClient.cs` | 18, 29 | `_client = new()` per instance |
| Lrclib | `Lrclib/Client/LrclibBaseClient.cs` | 18 | `_client = new()` per instance |
| MusixMatch | `MusixMatch/Client/MusixMatchBaseClient.cs` | 19, 33 | `_client = new()` per instance |
| Tadb | `Tadb/Client/TadbBaseClient.cs` | 19, 31 | `_client = new()` per instance |
| TMDB Image | `TMDB/Client/TmdbImageClient.cs` | 47 | New per download |
| FanArt Image | `FanArt/Client/FanArtImageClient.cs` | 39 | New per download, **never disposed** |
| CoverArt Image | `CoverArt/Client/CoverArtCoverArtClient.cs` | 57 | New per download, **never disposed** |
| KitsuIO | `Other/KitsuIO.cs` | 14 | New per call, **never disposed** |

**All paths**: `src/NoMercy.Providers/{Provider}/Client/`

**Fix**: Migrate to `IHttpClientFactory` with named clients per provider. See CRIT-07 for registration pattern.

#### PROV-CRIT-01: TmdbSeasonClient.Dispose() Hides Base + Forces GC
- **File**: `src/NoMercy.Providers/TMDB/Client/TmdbSeasonClient.cs:105-110`
- **Problem**: `new Dispose()` hides `TmdbBaseClient.Dispose()` — base `_client.Dispose()` is NEVER called. Instead forces `GC.Collect()` + `GC.WaitForFullGCComplete()` + `GC.WaitForPendingFinalizers()`, causing stop-the-world pauses.
- **Fix**: Remove `GC.*` calls. Use `override` or call `base.Dispose()`.

#### PROV-CRIT-02: TvdbBaseClient — Login().Wait() in Constructor
- **File**: `src/NoMercy.Providers/TVDB/Client/TvdbBaseClient.cs:23,39`
- **Problem**: `Login().Wait()` blocks the calling thread in a constructor. If called from async code, this causes thread pool starvation and potential deadlock.
- **Fix**: Use a static async factory method instead of constructor-based login.

#### PROV-CRIT-05: OpenSubtitles — .Result on PostAsync
- **File**: `src/NoMercy.Providers/OpenSubtitles/Client/OpenSubtitlesBaseClient.cs:48`
- **Problem**: `_client.PostAsync(url, content).Result.Content.ReadAsStringAsync()` blocks synchronously inside queue lambda.
- **Fix**: Make the lambda async and `await` the operations.

#### PROV-CRIT-06: BaseClient._instance Static Contamination
- **File**: `src/NoMercy.Providers/Helpers/BaseClient.cs:20-24`
- **Problem**: Static `_instance` field is shared across ALL provider subclasses. One provider's singleton contaminates another's.
- **Fix**: Use per-type static dictionaries or eliminate the singleton pattern in favor of DI.

### 14.3 Blocking Async Anti-Patterns (All Providers)

| Provider | File | Line | Pattern | Fix |
|----------|------|------|---------|-----|
| TVDB | `TvdbBaseClient.cs` | 23, 39 | `Login().Wait()` | Async factory |
| TVDB | `TvdbBaseClient.cs` | 109 | `.Result` on SendAsync | `await` |
| MusicBrainz | `MusicBrainzBaseClient.cs` | 73, 79 | `Task.Delay().Wait()` | `await Task.Delay()` |
| AcoustId | `AcoustIdBaseClient.cs` | 98, 104 | `Task.Delay().Wait()` | `await Task.Delay()` |
| Lrclib | `LrclibBaseClient.cs` | 61, 67 | `Task.Delay().Wait()` | `await Task.Delay()` |
| OpenSubtitles | `OpenSubtitlesBaseClient.cs` | 41, 48 | `.Result` on PostAsync | `await` |
| Tadb | `TadbArtistClient.cs` | 16-17 | `.Result` on Get<T>() | Make method async |
| Tadb | `TadbReleaseGroupClient.cs` | 16-17 | `.Result` on Get<T>() | Make method async |
| CacheController | `CacheController.cs` | 43 | `fileLock.Wait()` | `await fileLock.WaitAsync()` |

### 14.7 Disposal Pattern Gaps

| Class | Issue |
|-------|-------|
| `OpenSubtitlesBaseClient` | `Dispose()` throws `NotImplementedException` |
| `TmdbSeasonClient` | `new Dispose()` hides base, forces GC, never calls `base.Dispose()` |
| `AniDbBaseClient` | Non-standard static `Dispose()`, doesn't implement `IDisposable` |
| `NoMercyBaseClient` | Empty class, no disposal |
| Static image download methods | Create and leak HttpClients (FanArt, CoverArt, NoMercy, KitsuIO) |

#### SYS-CRIT-01: DriveMonitor Busy-Wait Spin Loop
- **File**: `src/NoMercy.MediaSources/OpticalMedia/DriveMonitor.cs:572-575`
- **Problem**: `while (!cancellationToken.IsCancellationRequested) { }` — Empty busy-wait loop burns 100% of one CPU core. No `Thread.Sleep`, no `Task.Delay`, no `await`.
- **Fix**: Add `await Task.Delay(100, cancellationToken)` inside loop.

#### SYS-CRIT-02: MediaScan GC.Collect() in Dispose
- **File**: `src/NoMercy.NmSystem/MediaScan.cs:356-369`
- **Problem**: `GC.Collect()` + `GC.WaitForFullGCComplete()` + `GC.WaitForPendingFinalizers()` in Dispose. Forces stop-the-world pause. Class holds no unmanaged resources.
- **Fix**: Remove all `GC.*` calls. If cleanup is needed, dispose specific resources.

#### SYS-CRIT-03: Auth.cs Thread.Sleep in Async Method
- **File**: `src/NoMercy.Setup/Auth.cs:157`
- **Problem**: `Thread.Sleep(deviceData.Interval * 1000)` inside `async Task TokenByDeviceGrant`. Blocks thread pool thread for 5+ seconds per polling iteration.
- **Fix**: `await Task.Delay(deviceData.Interval * 1000)`

### 17.2 High Severity Issues

| ID | File | Line | Issue |
|----|------|------|-------|
| SYS-H01 | `DriveMonitor.cs` | 31-32 | Non-thread-safe static `List<>` and `Dictionary<>` accessed from parallel tasks |
| SYS-H02 | `DriveMonitor.cs` | 406 | `.Wait()` blocking in async method |
| SYS-H03 | `DriveMonitor.cs` | 332,364,563 | `Task.Run()` + `.RunSynchronously()` — contradictory patterns |
| SYS-H04 | `MediaScan.cs` | 325 | `(int)new FileInfo(file).Length` — int overflow for >2GB files (common for media) |
| SYS-H05 | `Shell.cs` | 145 | `.GetAwaiter().GetResult()` blocking pattern — deadlock risk in ASP.NET Core |
| SYS-H06 | `Download.cs` | 25 | Entire file loaded into memory via `ReadAsByteArrayAsync()` — FFmpeg/Whisper models can be 1GB+ |
| SYS-H07 | `LogCache.cs` | 7 | `Dictionary<string, List<LogEntry>?>` accessed concurrently — not thread-safe |
| SYS-H08 | `Url.cs` | 32, 38 | `new HttpClient()` per call + `.Result` blocking — socket exhaustion + deadlock |
| SYS-H09 | `Config.cs` | 9 | Hardcoded OAuth client secret: `"1lHWBazSTHfBpuIzjAI6xnNjmwUnryai"` in source |
| SYS-H10 | `Storage.cs` | 116 | `statvfs` P/Invoke hardcoded to `libc.so.6` — fails on macOS (`libSystem.B.dylib`) |
| SYS-H11 | `Lock.cs` | 28-48 | `process.MainModule.FileName == filePath` — checks executable path, not file locks |
| SYS-H12 | `Auth.cs` | 98, 165 | `.Result` blocking calls in async context |
| SYS-H13 | `Auth.cs` | 252-263 | Recursive `CheckToken()` with `.Wait()` — risks `StackOverflowException` |

#### SYS-H05: Shell.cs .GetAwaiter().GetResult()
- **File**: `src/NoMercy.NmSystem/Shell.cs:145`
- **Problem**: `.GetAwaiter().GetResult()` blocking pattern — deadlock risk in ASP.NET Core
- **Fix**: Make calling methods async and use `await`

#### SYS-H07: LogCache Dictionary not thread-safe
- **File**: `src/NoMercy.NmSystem/LogCache.cs:7`
- **Problem**: `Dictionary<string, List<LogEntry>?>` accessed concurrently — not thread-safe
- **Fix**: Replace with `ConcurrentDictionary`

#### SYS-H08: Url.cs new HttpClient per call
- **File**: `src/NoMercy.NmSystem/Url.cs:32,38`
- **Problem**: `new HttpClient()` per call + `.Result` blocking — socket exhaustion + deadlock
- **Fix**: Use `IHttpClientFactory` or static `HttpClient`; make methods async

---

