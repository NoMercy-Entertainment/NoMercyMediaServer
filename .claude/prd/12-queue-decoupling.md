## 12. Queue System Decoupling

### 12.1 Current State Analysis

The queue system (`src/NoMercy.Queue/`) is **partially generic** but has critical coupling points:

**Generic components (can extract):**
- `JobQueue` — Only manipulates QueueJob/FailedJob
- `QueueWorker` — Job deserialization + Handle() invocation
- `JobDispatcher` — Serializes and enqueues objects
- `SerializationHelper` — Pure JSON serialization
- `CronExpressionBuilder` — Pure cron construction
- `CronService` — NCrontab wrapper
- `IShouldQueue` — Interface: `Task Handle()`
- Database models — QueueJob, FailedJob, CronJob are schema-only

**Media-server-specific (must extract):**
- `QueueRunner` — Uses `Config.QueueWorkers`, `Config.EncoderWorkers`, stores config in `MediaContext`
- 54 job implementations — All reference `Database.Models`
- Worker counts — Defined in global `Config` class

### 12.2 Three-Layer Architecture

```
NoMercy.Queue.Core (Standalone NuGet Package)
├── Interfaces/
│   ├── IQueueContext          # Abstraction over database
│   ├── IJobSerializer         # Pluggable serialization
│   ├── IConfigurationStore    # External config storage
│   ├── IShouldQueue          # Job contract
│   └── ICronJobExecutor      # Cron job contract
├── Models/
│   ├── QueueJob, FailedJob, CronJob
│   └── QueueConfiguration
├── Services/
│   ├── JobQueue (refactored)
│   ├── JobDispatcher (instance-based, not static)
│   └── QueueRunner (config-injected)
├── Workers/
│   ├── QueueWorker
│   └── CronWorker
└── Serialization/
    └── JsonNetJobSerializer

NoMercy.Queue.Sqlite (EF Core Provider)
├── SqliteQueueContext : IQueueContext
└── Migrations/

NoMercy.Queue.MediaServer (Media-specific)
├── Jobs/ (all 54 job implementations)
├── Configuration/
└── ServiceRegistration
```

### 12.3 Key Breaking Changes

1. `JobQueue` constructor: `QueueContext` → `IQueueContext`
2. `JobDispatcher`: Static class → Instance with DI
3. `QueueRunner`: Global `Config.*Workers` → `QueueConfiguration` record
4. `QueueRunner.SetWorkerCount()`: `MediaContext` → `IConfigurationStore`
5. `SerializationHelper`: Static → `IJobSerializer` via DI

### 12.4 Queue Dispatch Simplification

**Current problem**: Dispatching a new job requires too much ceremony. The `MediaProcessing/Jobs/JobDispatcher.cs` has **14+ generic overloads** like:

```csharp
// Current: Must write a NEW overload per job type
public void DispatchJob<TJob>(Ulid libraryId, Ulid folderId, string id, string inputFile)
    where TJob : AbstractEncoderJob, new()
{
    TJob job = new() { LibraryId = libraryId, FolderId = folderId, Id = id, InputFile = inputFile };
    Queue.JobDispatcher.Dispatch(job, job.QueueName, job.Priority);
}
// ... 13 more overloads with different parameter lists
```

And direct callers duplicate queue/priority that the job already knows:
```csharp
// Current: Queue name and priority duplicated
MusicDescriptionJob job = new(musicBrainzRelease);
JobDispatcher.Dispatch(job, "data", 2);  // "data" and 2 are ALSO on the job class!
```

**The fix**: Jobs already have `QueueName` and `Priority` as abstract properties. Make `IShouldQueue` carry them, then dispatch becomes one line with no generics:

**Step 1 — Extend `IShouldQueue`:**
```csharp
public interface IShouldQueue
{
    string QueueName { get; }
    int Priority { get; }
    Task Handle();
}
```

**Step 2 — Simplify `Queue.JobDispatcher.Dispatch()`:**
```csharp
public static class JobDispatcher
{
    public static void Dispatch(IShouldQueue job)
    {
        QueueJob jobData = new()
        {
            Payload = SerializationHelper.Serialize(job),
            Queue = job.QueueName,
            Priority = job.Priority,
        };
        Queue.Enqueue(jobData);
    }
}
```

**Step 3 — Delete all 14+ overloads from `MediaProcessing/Jobs/JobDispatcher.cs`.**

**Step 4 — All call sites become simple:**
```csharp
// NEW: Just create the job and dispatch. No generics, no duplicate params.
JobDispatcher.Dispatch(new MusicDescriptionJob(musicBrainzRelease));
JobDispatcher.Dispatch(new EncodeVideoJob { LibraryId = id, InputFile = path });
JobDispatcher.Dispatch(new CoverArtImageJob(release));
```

**To create a brand new job, you now only need to:**
1. Create a class implementing `IShouldQueue`
2. Set `QueueName` and `Priority`
3. Implement `Handle()`
4. Call `JobDispatcher.Dispatch(new MyJob(...))`

No generic overloads, no wrapper methods, no parameter duplication.

| Task ID | Description | Effort |
|---------|-------------|--------|
| QDS-01 | Add `QueueName` and `Priority` to `IShouldQueue` interface | Small |
| QDS-02 | Update `Queue.JobDispatcher.Dispatch()` to extract queue/priority from job | Small |
| QDS-03 | Add default `QueueName`/`Priority` to all existing job classes that lack them | Medium |
| QDS-04 | Delete all 14+ generic overloads from `MediaProcessing/Jobs/JobDispatcher.cs` | Small |
| QDS-05 | Update all dispatch call sites to use simplified pattern | Medium |
| QDS-06 | Verify all existing jobs still dispatch and execute correctly | Medium |

### 12.5 Decoupling Implementation Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| QDC-01 | Create `NoMercy.Queue.Core` project | Small |
| QDC-02 | Define `IQueueContext`, `IJobSerializer`, `IConfigurationStore` interfaces | Small |
| QDC-03 | Extract QueueJob, FailedJob, CronJob models to Core | Small |
| QDC-04 | Create `QueueConfiguration` record | Small |
| QDC-05 | Refactor `JobQueue` to accept `IQueueContext` | Medium |
| QDC-06 | Refactor `JobDispatcher` from static to instance class | Medium |
| QDC-07 | Refactor `QueueWorker` to accept `IJobSerializer` | Medium |
| QDC-08 | Refactor `QueueRunner` to accept `QueueConfiguration` | Large |
| QDC-09 | Refactor `CronWorker` to accept `IQueueContext` | Medium |
| QDC-10 | Create `NoMercy.Queue.Sqlite` project | Medium |
| QDC-11 | Implement `SqliteQueueContext` | Medium |
| QDC-12 | Create `NoMercy.Queue.MediaServer` project | Small |
| QDC-13 | Move 54 job implementations to MediaServer project | Large |
| QDC-14 | Create `MediaServerConfigurationStore` | Small |
| QDC-15 | Update all DI registrations | Medium |
| QDC-16 | Verify serialization backward compatibility | Medium |
| QDC-17 | Comprehensive queue testing | Large |

**Estimated total: 14-16 weeks (1 developer)**

### 12.6 Convention over Configuration API

The queue library should work **out of the box with zero configuration** but allow overriding anything:

```csharp
// Zero-config: Works with sensible defaults (internal SQLite, default workers)
services.AddNoMercyQueue();

// Full override: Hook into everything
services.AddNoMercyQueue(options =>
{
    // Storage
    options.DatabasePath = "/custom/path/queue.db";
    options.ContextFactory = () => new CustomQueueContext();

    // Workers
    options.WorkerCounts = new() { ["queue"] = 2, ["encoder"] = 4, ["data"] = 3 };

    // Serialization
    options.Serializer = new CustomSerializer();

    // Lifecycle hooks
    options.OnWorkerStarted = (name) => logger.Info($"Worker {name} started");
    options.OnWorkerStopped = (name) => logger.Info($"Worker {name} stopped");
    options.OnJobFailed = (job, ex) => alertService.Notify(ex);
    options.OnJobCompleted = (job) => metrics.Record(job);
});
```

**Design principles:**
- Defaults for everything — database location, worker counts, serializer, lifecycle hooks
- Internal storage (SQLite in app data dir) unless overridden
- Hook-based extensibility for worker lifecycle and job events
- The library owns its own context and storage by default but accepts external providers
- No dependency on `MediaContext`, `Config`, or any NoMercy-specific class

| Task ID | Description | Effort |
|---------|-------------|--------|
| QCC-01 | Define `NoMercyQueueOptions` class with all configurable properties | Small |
| QCC-02 | Implement `AddNoMercyQueue()` extension with default wiring | Medium |
| QCC-03 | Implement `AddNoMercyQueue(Action<NoMercyQueueOptions>)` overload | Small |
| QCC-04 | Create default internal SQLite provider (no external dependency) | Medium |
| QCC-05 | Wire lifecycle hooks into `QueueWorker` start/stop/fail paths | Medium |
| QCC-06 | Migrate existing `QueueRunner` to use options pattern | Large |

---

