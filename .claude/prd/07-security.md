## 7. Security & Hardening

> Security vulnerabilities, hardcoded secrets, production configuration, and hardening.

### CRIT-14: TypeNameHandling.All Security Vulnerability
- **File**: `src/NoMercy.Queue/SerializationHelper.cs:11-18`
- **Problem**: `TypeNameHandling.All` allows arbitrary type instantiation during deserialization
- **Impact**: Remote code execution if attacker can inject payload into queue database

  **IMPORTANT CONTEXT — Required for Job Deserialization**: The queue system serializes job objects (like `FindMediaFilesJob`, `StorageJob`) into JSON with their full type name, then deserializes them back to call `Handle()`. Without `TypeNameHandling.All`, the deserializer returns a generic `JObject` instead of the actual job class, and the `is IShouldQueue` check fails — **jobs would never execute**.

  Since job payloads are:
  - Created internally by `JobDispatcher.Dispatch()`
  - Stored in a local SQLite database
  - Never exposed to user input or external APIs

  ...the risk is **effectively zero** for a self-hosted media server. This is the foundation of the queue's reliability — you can dispatch ANY `IShouldQueue` implementation and it will deserialize and execute correctly. **DO NOT break this capability.**

  **Optional hardening** (only if you want defense-in-depth, NOT required):

  Since the queue is being decoupled into a **standalone library** (Phase 6), a hardcoded type whitelist (`HashSet<string> AllowedTypes`) is not viable — the library can't know what job types the consuming application will create. The library must stay generic.

  **Library-safe approach** — validate after deserialization, not during:
  ```csharp
  // In the standalone Queue.Core library:
  // The worker already does an IShouldQueue check — that IS the safety gate
  object deserialized = SerializationHelper.Deserialize<object>(job.Payload);

  if (deserialized is not IShouldQueue queueable)
  {
      // Type deserialized but doesn't implement the job interface → reject
      Logger.Error("Job payload deserialized to {Type} which is not IShouldQueue",
          deserialized.GetType().FullName);
      queue.FailJob(job, "Invalid job type");
      return;
  }

  await queueable.Handle();  // Only executes if it's a real job
  ```

  This works because:
  - `TypeNameHandling.All` allows any type to deserialize (needed for the library to be generic)
  - The `is IShouldQueue` check prevents non-job types from executing
  - An attacker would need to craft a type that both exists in the loaded assemblies AND implements `IShouldQueue` — which means it's already a legitimate job type
  - The library doesn't need to know about specific job classes

  **If the consuming app wants stricter control**, they can provide a custom `IJobSerializer` (from Phase 6 decoupling) with their own binder:
  ```csharp
  // In the consuming app (NoMercy.Queue.MediaServer), NOT in the library:
  public class MediaServerJobSerializer : IJobSerializer
  {
      // App-specific whitelist lives HERE, not in the library
  }
  ```

- **Fix**: Keep `TypeNameHandling.All`; the existing `is IShouldQueue` check is the safety gate; optionally add `IJobSerializer` extension point for consuming apps
- **Severity**: Downgraded from Critical to **Low** — internal-only data, self-hosted server, foundational to queue reliability
- **Tests Required**:
  - [ ] Unit test: Job implementing IShouldQueue deserializes and executes correctly
  - [ ] Unit test: Deserialized object that doesn't implement IShouldQueue is rejected (not executed)
  - [ ] Integration test: Existing queued jobs still execute correctly

### HIGH-03: EnableSensitiveDataLogging in Production
- **File**: `src/NoMercy.Database/MediaContext.cs:25`
- **Problem**: Logs all SQL parameter values including potential PII and API keys

  **IMPORTANT CONTEXT — Self-Hosted Server**: This is a personal media server, not a multi-tenant SaaS. The "sensitive data" is the user's own movie titles and user IDs. Without sensitive data logging, SQL logs show `WHERE Id = @p0` instead of `WHERE Id = 123`, making debugging impossible.

  **Pragmatic fix** — make it environment-dependent so it helps during development:
  ```csharp
  if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
      || Config.IsDev)
  {
      options.EnableSensitiveDataLogging();
  }
  ```

- **Fix**: Make conditional on development mode; keep for dev, disable for production
- **Severity**: Downgraded from High to **Medium** — self-hosted context
- **Tests Required**:
  - [ ] Unit test: Production configuration doesn't enable sensitive logging

### HIGH-06: Middleware Ordering Issues
- **File**: `src/NoMercy.Server/AppConfig/ApplicationConfiguration.cs:65-96`
- **Problem**: DeveloperExceptionPage always enabled; compression after auth; access log after auth

  **Context**: The DeveloperExceptionPage being always-on is a development convenience (see HIGH-07 pattern). The middleware ordering mostly works but isn't optimal for CORS pre-flight requests.

  **Optimal ordering**:
  ```
  1. Exception handling (conditional on environment)
  2. HTTPS redirection + HSTS
  3. Response compression (before routing for efficiency)
  4. CORS (before routing for pre-flight)
  5. Routing
  6. Localization
  7. Authentication → Authorization
  8. Access logging
  9. Static files
  10. WebSockets
  ```

- **Fix**: Make DeveloperExceptionPage conditional; reorder compression before auth; reorder CORS before routing
- **Tests Required**:
  - [ ] Integration test: Exception page not served in production mode
  - [ ] Integration test: Compression applied to all responses
  - [ ] Integration test: CORS pre-flight requests succeed

### HIGH-07: SignalR Detailed Errors in Production
- **File**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:274`
- **Problem**: `EnableDetailedErrors = true` always; leaks stack traces to clients

  **IMPORTANT CONTEXT — Development Convenience**: With the project in active development, detailed SignalR errors help debug real-time features (video/music playback, dashboard). The `HubErrorLoggingFilter` shows they're already logging errors server-side.

  ```csharp
  o.EnableDetailedErrors = Config.IsDev;  // Only in dev mode
  ```

- **Fix**: Make conditional on `Config.IsDev`
- **Tests Required**:
  - [ ] Integration test: Production SignalR errors don't include stack traces

### HIGH-14: Kestrel Limits Set to Unlimited
- **File**: `src/NoMercy.Networking/Certificate.cs:22-25`
- **Problem**: `MaxRequestBodySize = null`, `MaxConcurrentConnections = null`

  **IMPORTANT CONTEXT — Intentional for Large Media Streaming**: A media server handles:
  - 4K video uploads (5-50GB per file)
  - Long-running HLS streaming connections (hours)
  - Multiple concurrent streams from different devices

  Default Kestrel limits (128MB body, ~100 connections) are far too restrictive. Setting them to `null` (unlimited) was the developer's workaround.

  **The fix is generous limits, not strict ones**:
  ```csharp
  options.Limits.MaxRequestBodySize = 100L * 1024 * 1024 * 1024;  // 100GB (4K remux support)
  options.Limits.MaxConcurrentConnections = 1000;  // Many streaming clients
  options.Limits.MaxConcurrentUpgradedConnections = 500;  // WebSocket/SignalR limit
  // Leave MaxRequestBufferSize = null (Kestrel manages adaptively)
  ```

  **Note**: If behind a reverse proxy (nginx, Traefik), the proxy should enforce stricter limits. Document this.

- **Fix**: Set generous but finite limits appropriate for a media server
- **Tests Required**:
  - [ ] Integration test: 50GB upload succeeds
  - [ ] Integration test: 100+ concurrent streaming connections work
  - [ ] Documentation: Note reverse proxy should enforce tighter limits if internet-facing

### MED-07: Large SignalR Message Limit (100MB)
- **File**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:275`
- **Fix**: Reduce to 1-10MB with per-operation validation

### MED-17: Hardcoded Configuration in Static Properties
- **File**: `src/NoMercy.NmSystem/Information/Config.cs`
- **Problem**: `TokenClientSecret` hardcoded in source; all config as static properties
- **Fix**: Use `IOptions<T>` pattern; move secrets to environment variables

### MED-18: CORS Configuration
- **File**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:315-342`
- **Revalidation note**: CORS is not wide-open — it uses explicit origin whitelisting (`nomercy.tv` domain, specific local IPs). `AllowAnyMethod()` and `AllowAnyHeader()` are permissive but acceptable for a self-hosted media server API. The hardcoded local IPs (`192.168.2.201:5501-5503`) should be moved to configuration or made conditional on dev mode.
- **Fix**: Make dev IPs conditional on `Config.IsDev`; consider moving allowed origins to configuration file
- **Severity**: Downgraded to **Low** — acceptable for self-hosted server

### MED-20: Memory Cache Configuration
- **File**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:100`
- **Revalidation note**: `AddMemoryCache()` uses default MemoryCache options which include automatic eviction when memory pressure occurs. The default behavior is not "no eviction" — .NET's MemoryCache has built-in compaction. However, configuring explicit size limits would prevent unbounded growth before system-level memory pressure kicks in.
- **Fix**: Add `SizeLimit` configuration (e.g., 500MB) for proactive bound; default eviction is already present
- **Severity**: Downgraded to **Low** — default eviction works, explicit limits are optional improvement

### PROV-H01: SSL Certificate Validation Bypass
| ID | File | Line | Issue |
|----|------|------|-------|
| PROV-H01 | `TmdbImageClient.cs` | 45 | SSL certificate validation bypass: `(_, _, _, _) => true` |

### PROV-H02: Static Mutable Token Without Thread-Safety
| ID | File | Line | Issue |
|----|------|------|-------|
| PROV-H02 | `TvdbBaseClient.cs` | 19 | Static mutable `Token` without thread-safety |

### PROV-H08: Static Mutable AccessToken
| ID | File | Line | Issue |
|----|------|------|-------|
| PROV-H08 | `OpenSubtitlesBaseClient.cs` | 15 | Static mutable `AccessToken` |

### PROV-H14: API Token in Query Params
| ID | File | Line | Issue |
|----|------|------|-------|
| PROV-H14 | `MusixMatchBaseClient.cs` | 63 | API token in query params (logged/cached in plaintext) |

### PROV-M07: API Key Embedded in URL Path
| ID | File | Line | Issue |
|----|------|------|-------|
| PROV-M07 | `TadbBaseClient.cs` | 13 | API key embedded in URL path (visible in logs) |

### PROV-M08: FanArt HTTP Base URL
| ID | File | Line | Issue |
|----|------|------|-------|
| PROV-M08 | `FanArtBaseClient.cs` | 13 | HTTP (not HTTPS) base URL — keys transmitted in clear |

### PROV-M12: Static Mutable Credentials
| ID | File | Line | Issue |
|----|------|------|-------|
| PROV-M12 | `AniDbBaseClient.cs` | 12-14 | Static mutable credentials |

### DBMOD-H03: EnableSensitiveDataLogging Unconditionally Enabled
| ID | File | Issue |
|----|------|-------|
| DBMOD-H03 | `MediaContext.cs:25` | `EnableSensitiveDataLogging()` unconditionally enabled — logs actual parameter values including user emails, IDs |

### DBMOD-H04: Global Cascade Delete on ALL FKs
| ID | File | Issue |
|----|------|-------|
| DBMOD-H04 | `MediaContext.cs:56-59` | Global cascade delete on ALL FKs — deleting a User cascades to ALL UserData, ActivityLogs, Playlists, etc. |

### DBMOD-H05: EntityBaseUpdatedAtInterceptor Only Handles Async
| ID | File | Issue |
|----|------|-------|
| DBMOD-H05 | `EntityBaseUpdatedAtInterceptor.cs` | Only handles async path (`SavingChangesAsync`). Sync `SaveChanges()` skips `UpdatedAt` updates |

### DBMOD-H06: QueueContext Missing Interceptor
| ID | File | Issue |
|----|------|-------|
| DBMOD-H06 | `QueueContext.cs` | Does not register `EntityBaseUpdatedAtInterceptor` — `CronJob.UpdatedAt` never auto-updated |

### SYS-H09: Hardcoded OAuth Client Secret
| ID | File | Line | Issue |
|----|------|------|-------|
| SYS-H09 | `Config.cs` | 9 | Hardcoded OAuth client secret: `"1lHWBazSTHfBpuIzjAI6xnNjmwUnryai"` in source |

### 18.8 Token & Secret Storage — No Hardcoding

**Hardcoded secrets found in the codebase:**

| Secret | File | Line | Risk |
|--------|------|------|------|
| Keycloak client secret `1lHWBazSTHfBpuIzjAI6xnNjmwUnryai` | `src/NoMercy.NmSystem/Information/Config.cs` | 9 | Anyone with source can impersonate server |
| ~~TMDB JWT token~~ | `tests/NoMercy.Tests.Providers/TMDB/TmdbTestBase.cs` | 61 | **NOT a concern** — TMDB tokens are public read-only API keys |

**Rules — No secrets in code, ever:**
1. Production secrets → environment variables or secure store (DPAPI on Windows, keyring on Linux)
2. Test API tokens → loaded from environment variables or `dotnet user-secrets`
3. CI/CD tokens → GitHub Actions secrets, injected at runtime

**Test Token Pattern:**

TMDB tokens are public read-only API keys — hardcoding them in tests is fine. But for **private** API tokens (OAuth, Keycloak, user-specific):

```csharp
// WRONG — private secret hardcoded in source
private const string ClientSecret = "1lHWBazSTHfBpuIzjAI6xnNjmwUnryai";

// RIGHT — loaded from environment or user-secrets
private static string ClientSecret =>
    Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET")
    ?? throw new InvalidOperationException("KEYCLOAK_CLIENT_SECRET not set");
```

**For integration tests that need real API tokens:**
- Tests that call real external APIs should be marked with `[Trait("Category", "Integration")]`
- Integration tests are skipped in CI if required env vars are not set
- Developers set private tokens via `dotnet user-secrets` or `.env` files (gitignored)
- CI pipeline injects tokens via GitHub Secrets

**For unit tests that should NOT need real API tokens:**
- Mock the HTTP responses (use `HttpMessageHandler` mock)
- Never call real external APIs in unit tests
- Current TMDB tests blur the line between unit and integration — many are actually integration tests calling the real TMDB API

**Config file locations** (`src/NoMercy.NmSystem/Information/AppFiles.cs`):

Rename `token.json` → `auth_token.json` to clearly identify its purpose and free the config directory for other config files.

| File | Purpose | Contains |
|------|---------|----------|
| `{ConfigPath}/auth_token.json` | Keycloak OAuth tokens | `access_token`, `refresh_token`, `expires_in` |
| `{ConfigPath}/api_keys.json` | Cached API keys from domain API | TMDB, TVDB, FanArt, etc. + `_cached_at` timestamp |

| Mode | Platform | Config Directory |
|------|----------|-----------------|
| Dev (`--dev`) | Windows | `%LOCALAPPDATA%\NoMercy_dev\config\` |
| Dev (`--dev`) | Linux | `~/.local/share/NoMercy_dev/config/` |
| Production | Windows | `%LOCALAPPDATA%\NoMercy\config\` |
| Production | Linux | `~/.local/share/NoMercy/config/` |

**`auth_token.json` contains real Keycloak bearer tokens — it must NEVER be committed to source control.**

**Rename task** — only 4 references to update:
- `AppFiles.cs:22` — `"token.json"` → `"auth_token.json"` (property stays `TokenFile`)
- `Auth.cs:31,267,282` — no changes needed (already uses `AppFiles.TokenFile`)
- Migration: on startup, if `token.json` exists and `auth_token.json` does not, rename it automatically

**Test isolation problem:**
- `AppFiles` is a static class with non-overridable properties — there's no way to redirect `AppFiles.TokenFile` during tests
- `MovieManagerTests.cs:25-26` already calls `AppFiles.CreateAppFolders()` and `ApiInfo.RequestInfo()` which touch the real filesystem
- If any test ever calls `Auth.Init()`, it reads/writes the **real** `token.json` with actual bearer tokens
- **No `.gitignore` entry for the token file path** — if the repo root ever becomes the app data path (e.g., in a misconfigured test), tokens could be committed

**Required fix — test isolation for `AppFiles`:**
```csharp
// Make AppFiles configurable for testing:
public static class AppFiles
{
    // Allow tests to override the base path
    internal static string? TestOverridePath { get; set; }

    public static string AppPath => TestOverridePath
        ?? (Config.IsDev
            ? Path.Combine(AppDataPath, "NoMercy_dev")
            : Path.Combine(AppDataPath, "NoMercy"));
}

// In test setup:
[Collection("IsolatedFileSystem")]
public class MyTests : IDisposable
{
    private readonly string _tempDir;

    public MyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "NoMercy.Tests", Guid.NewGuid().ToString());
        AppFiles.TestOverridePath = _tempDir;
    }

    public void Dispose()
    {
        AppFiles.TestOverridePath = null;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

This ensures tests never read/write the developer's real `token.json`, certificates, databases, or any other sensitive files.

| Task ID | Description | Effort |
|---------|-------------|--------|
| SEC-01 | Move Keycloak client secret from `Config.cs` to environment variable | Small |
| SEC-02 | Add `[Trait("Category", "Integration")]` to all tests that call real external APIs | Small |
| SEC-03 | Create test helper that loads private tokens from env vars with skip-on-missing | Small |
| SEC-04 | Add `.env.example` file documenting required env vars for test runs | Small |
| SEC-05 | Rotate Keycloak client secret if repo has ever been public | Small |
| SEC-06 | Rename `token.json` → `auth_token.json` in `AppFiles.cs:22`, add auto-migration on startup | Small |

### 18.10 Secret Encryption with Data Protection API

Sensitive data (auth tokens, API keys, client secrets) must be encrypted at rest. Reuse the proven `TokenStore` pattern from the Twitch bot using .NET's `IDataProtectionProvider` (DPAPI).

#### Current State

The codebase has **two** secret storage mechanisms, neither used for auth tokens:

| Mechanism | Location | Used For | Issue |
|-----------|----------|----------|-------|
| `NeoSmart.SecureStore` | `CredentialManager.cs` + `secrets.bin`/`secrets.key` | AniDb credentials only | Requires managing a separate key file |
| Plaintext JSON | `token.json` (soon `auth_token.json`) | Keycloak bearer tokens | **Not encrypted** |
| Plaintext JSON | `api_keys.json` (new cache) | Provider API keys | Not encrypted (acceptable — keys are shared/public) |
| Hardcoded string | `Config.cs:9` | Keycloak client secret | In source code |

#### Target: TokenStore via IDataProtectionProvider

Port the exact pattern from the Twitch bot — DPAPI is framework-native, cross-platform (Windows DPAPI, Linux/macOS key files in `{AppPath}/root/secrets/`), and the EF Core value converter gives transparent encrypt/decrypt:

```csharp
// src/NoMercy.Helpers/TokenStore.cs (NEW — port from Twitch bot)
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace NoMercy.Helpers;

public static class TokenStore
{
    private static IDataProtector? Protector { get; set; }

    public static void Initialize(IServiceProvider serviceProvider)
    {
        if (Protector is not null) return;

        IDataProtectionProvider provider =
            serviceProvider.GetRequiredService<IDataProtectionProvider>();
        Protector = provider.CreateProtector("NoMercy.TokenProtection");
    }

    public static string EncryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;
        if (Protector is null)
            throw new InvalidOperationException("TokenStore not initialized.");
        return Protector.Protect(token);
    }

    public static string DecryptToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;
        if (Protector is null)
            throw new InvalidOperationException("TokenStore not initialized.");
        try { return Protector.Unprotect(token); }
        catch { return token; }  // Graceful fallback for unencrypted values
    }
}
```

#### DI Registration

```csharp
// In ServiceConfiguration.cs
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(AppFiles.SecretsPath))
    .SetApplicationName("NoMercyMediaServer");

// After service provider is built:
TokenStore.Initialize(app.Services);
```

Key ring stored at `{AppPath}/root/secrets/` — the directory already exists (`AppFiles.SecretsPath`).

#### What Gets Encrypted

| Data | File | Encryption | Reason |
|------|------|-----------|--------|
| `access_token` in `auth_token.json` | `auth_token.json` | **Yes — TokenStore** | Real Keycloak bearer token |
| `refresh_token` in `auth_token.json` | `auth_token.json` | **Yes — TokenStore** | Long-lived refresh credential |
| Keycloak client secret | Move from `Config.cs` to encrypted storage | **Yes — TokenStore** | OAuth client credential |
| API keys (TMDB, TVDB, etc.) | `api_keys.json` | No | Shared public keys — not sensitive |
| AniDb credentials | `secrets.bin` | Already encrypted (SecureStore) | Keep existing mechanism |

#### Encrypted auth_token.json Format

```json
{
  "access_token": "CfDJ8N...encrypted...base64...",
  "refresh_token": "CfDJ8N...encrypted...base64...",
  "expires_in": 3600,
  "not_before_policy": 0
}
```

The `DecryptToken` catch block handles the migration gracefully — if it encounters an unencrypted value (from before the upgrade), it returns it as-is. Next write encrypts it. Zero-downtime migration.

#### EF Core Value Converter (for future DB-stored secrets)

If any secrets move into the database (e.g., per-user API keys, plugin credentials):

```csharp
modelBuilder.Entity<Configuration>()
    .Property(e => e.SecureValue)
    .HasConversion(
        v => TokenStore.EncryptToken(v),
        v => TokenStore.DecryptToken(v));
```

Same pattern as the Twitch bot — transparent, no calling code changes.

#### Migration from NeoSmart.SecureStore

The existing `CredentialManager` + `secrets.bin`/`secrets.key` can stay for AniDb credentials (it works). Long-term, consolidate onto DPAPI:

| Phase | Action |
|-------|--------|
| Now | Add `TokenStore` for auth tokens and client secret |
| Later | Migrate `CredentialManager` to use `TokenStore` internally |
| Eventually | Remove `NeoSmart.SecureStore` dependency, single encryption provider |

| Task ID | Description | Effort |
|---------|-------------|--------|
| ENC-01 | Port `TokenStore` class from Twitch bot to `NoMercy.Helpers` | Small |
| ENC-02 | Register `AddDataProtection()` in `ServiceConfiguration.cs` with keys at `SecretsPath` | Small |
| ENC-03 | Call `TokenStore.Initialize()` after service provider built | Small |
| ENC-04 | Encrypt `access_token` and `refresh_token` when writing `auth_token.json` | Small |
| ENC-05 | Decrypt tokens when reading `auth_token.json` (with plaintext fallback for migration) | Small |
| ENC-06 | Move Keycloak client secret from `Config.cs` hardcode to encrypted storage | Small |
| ENC-07 | Verify cross-platform: DPAPI key ring works on Windows, Linux, macOS | Medium |

### 18.11 Database Token Storage — Chicken-and-Egg Analysis

**Question**: Can we store all tokens/secrets in the database instead of JSON files?

#### Current Startup Dependency Chain

```
Program.Main()
  → options.ApplySettings()
  → Start.Init()
      ① UserSettings.TryGetUserSettings()  ← DB read (try/catch, silent fail on first launch)
      ② ApiInfo.RequestInfo()              ← Network only, NO DB dependency
      ③ AppFiles.CreateAppFolders()        ← Filesystem only
      ④ Auth.Init()                        ← Reads token FILE, needs network for Keycloak
      ⑤ Networking.Discover()              ← Network only
      ⑥ DatabaseSeeder.Run()               ← Creates/migrates DB (guaranteed ready after this)
      ⑦ Register.Init()                    ← Needs DB + auth token
      ⑧ Binaries/ChromeCast/etc.           ← Various
  → CreateWebHostBuilder().Build()         ← Kestrel starts, DI container built
```

#### The Chicken-and-Egg Problem

| Token | Needed At | DB Ready At | Chicken-Egg? |
|-------|-----------|-------------|-------------|
| **Keycloak auth token** | Step ④ `Auth.Init()` | Step ⑥ `DatabaseSeeder.Run()` | **YES** — need token before DB exists |
| **API keys** (TMDB etc.) | Step ② `ApiInfo.RequestInfo()` | Step ⑥ | **YES** — need keys before DB exists |
| **Keycloak client secret** | Step ④ `Auth.Init()` | Step ⑥ | **YES** — need secret before DB exists |
| **User data** | Step ⑦ `Register.Init()` | Step ⑥ | No — DB is ready |
| **Server config** (ports etc.) | Step ① `UserSettings` | Step ⑥ | **Partial** — try/catch handles first launch when DB doesn't exist yet. Intent: CLI flags (e.g. `--internal-port`) persist to DB so they're not needed on subsequent boots. After reorder, DB is always ready — try/catch becomes a safety net, not required. |

#### Solution: Reorder Startup + Use DB as Primary Store

The fix is to move database creation **before** auth and API key fetching. SQLite databases auto-create on first connection, and `EnsureCreated()`/`Migrate()` is fast on an empty DB:

```
PROPOSED STARTUP ORDER:
  ① AppFiles.CreateAppFolders()            ← Filesystem (must be first — DB file lives here)
  ② DatabaseSeeder.RunSchemaAndStatic()    ← Migrate + static seeds (Config, Libraries, EncoderProfiles)
  ③ TokenStore.InitializeStandalone()      ← Init DPAPI with DI-less provider (see below)
  ④ UserSettings.TryGetUserSettings()      ← DB read (now guaranteed to work)
  ⑤ Auth.Init()                            ← Load token from DB, refresh if needed
  ⑥ ApiInfo.RequestInfo()                  ← Load from DB cache, fallback to network (needs API keys)
  ⑦ Networking.Discover()                  ← Network
  ⑧ DatabaseSeeder.RunApiSeeds()           ← API-dependent seeds (Languages, Countries, Genres, etc.)
  ⑨ Register.Init()                        ← DB + auth token (both ready)
  ⑩ DatabaseSeeder.RunAuthSeeds()          ← Auth-dependent seeds (UsersSeed — needs Globals.AccessToken)
  ⑪ Binaries/ChromeCast/etc.
```

#### DatabaseSeeder Split

```csharp
public static class DatabaseSeeder
{
    // Phase 1: Schema creation + static seeds (no network needed)
    public static async Task RunSchemaAndStatic()
    {
        MediaContext mediaContext = new();
        await using QueueContext queueContext = new();

        await Migrate(mediaContext);
        await EnsureDatabaseCreated(mediaContext);
        await Migrate(queueContext);
        await EnsureDatabaseCreated(queueContext);

        // Static-only seeds — no API keys, no auth, no network
        await ConfigSeed.Init(mediaContext);
        await LibrariesSeed.Init(mediaContext);
        await EncoderProfilesSeed.Init(mediaContext);
    }

    // Phase 2: Seeds that fetch from external APIs (need API keys from ApiInfo)
    public static async Task RunApiSeeds()
    {
        MediaContext mediaContext = new();

        await LanguagesSeed.Init(mediaContext);
        await CountriesSeed.Init(mediaContext);
        await GenresSeed.Init(mediaContext);           // depends on Languages
        await CertificationsSeed.Init(mediaContext);
        await MusicGenresSeed.Init(mediaContext);

        if (ShouldSeedMarvel)
        {
            Thread thread = new(() => _ = SpecialSeed.Init(mediaContext));
            thread.Start();
        }
    }

    // Phase 3: Seeds that need auth token (run after Register.Init)
    public static async Task RunAuthSeeds()
    {
        MediaContext mediaContext = new();
        await UsersSeed.Init(mediaContext);
    }
}
```

**Key change**: `DatabaseSeeder.Run()` must be **split into two phases** because most seeds fetch data from external APIs.

#### Seed Dependency Analysis

| Seed | Needs API Keys? | Needs Auth? | Needs Network? | Dependencies |
|------|----------------|-------------|----------------|-------------|
| **ConfigSeed** | No | No | No | Static data only (port config) |
| **LibrariesSeed** | No | No | No | Local JSON files only |
| **EncoderProfilesSeed** | No | No | No | Local JSON file / hardcoded defaults |
| **LanguagesSeed** | Yes (`TmdbToken`) | No | Yes | TMDB API |
| **CountriesSeed** | Yes (`TmdbToken`) | No | Yes | TMDB API |
| **GenresSeed** | Yes (`TmdbToken`) | No | Yes | TMDB API + Languages already seeded |
| **CertificationsSeed** | Yes (`TmdbToken`) | No | Yes | TMDB API |
| **MusicGenresSeed** | No | No | Yes | MusicBrainz (public, no auth) |
| **UsersSeed** | Yes | **Yes** (`Globals.AccessToken`) | Yes | Needs `Register.Init()` completed |
| **SpecialSeed** | Yes (`TmdbToken`) | No | Yes | TMDB API + Libraries seeded |

**Three clear groups:**
1. **Schema + static seeds** (ConfigSeed, LibrariesSeed, EncoderProfilesSeed) — no external dependencies at all
2. **API-dependent seeds** (Languages, Countries, Genres, Certifications, MusicGenres, SpecialSeed) — need API keys from `ApiInfo`
3. **Auth-dependent seeds** (UsersSeed) — needs `Globals.AccessToken` set by `Register.Init()`

Only group 1 can safely move before auth and API key fetching. Groups 2 and 3 must stay after.

#### TokenStore Without DI (Pre-Server Bootstrap)

`IDataProtectionProvider` normally comes from the DI container, but the container isn't built until `CreateWebHostBuilder().Build()` (after all setup). Solution — create a standalone protector for bootstrap:

```csharp
// Pre-DI bootstrap (before ASP.NET Core starts)
public static class TokenStore
{
    private static IDataProtector? _protector;

    // Called early in startup, before DI container exists
    public static void InitializeStandalone()
    {
        if (_protector is not null) return;

        IDataProtectionProvider provider = DataProtectionProvider.Create(
            new DirectoryInfo(AppFiles.SecretsPath),
            configuration => configuration.SetApplicationName("NoMercyMediaServer"));
        _protector = provider.CreateProtector("NoMercy.TokenProtection");
    }

    // Called later when DI container is available (uses same key ring, seamless)
    public static void Initialize(IServiceProvider serviceProvider)
    {
        IDataProtectionProvider provider =
            serviceProvider.GetRequiredService<IDataProtectionProvider>();
        _protector = provider.CreateProtector("NoMercy.TokenProtection");
    }

    // ... EncryptToken / DecryptToken unchanged
}
```

Both `DataProtectionProvider.Create(directoryInfo)` and the DI-registered version use the **same key ring directory**, so tokens encrypted during bootstrap can be decrypted after the DI container starts, and vice versa.

#### Database Schema for Tokens

Add a `SecureValue` column to the existing `Configuration` table. Plain config uses `Value`, secrets use `SecureValue`:

```csharp
// Configuration.cs — updated
public class Configuration : Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("key")] public string Key { get; set; } = string.Empty;
    [JsonProperty("value")] public string Value { get; set; } = string.Empty;
    [JsonProperty("modified_by")] public Guid? ModifiedBy { get; set; }

    // NEW — encrypted via EF Core value converter (TokenStore.EncryptToken/DecryptToken)
    public string? SecureValue { get; set; }
}
```

Same pattern as the Twitch bot — the EF Core value converter handles encrypt/decrypt transparently:

```csharp
modelBuilder.Entity<Configuration>()
    .Property(e => e.SecureValue)
    .HasConversion(
        v => TokenStore.EncryptToken(v),
        v => TokenStore.DecryptToken(v));
```

#### What Goes Where

| Data | Storage | Encrypted | Key in Configuration table |
|------|---------|-----------|---------------------------|
| Keycloak access token | DB `SecureValue` | Yes (DPAPI) | `auth:access_token` |
| Keycloak refresh token | DB `SecureValue` | Yes (DPAPI) | `auth:refresh_token` |
| Token expiry | DB `Value` | No | `auth:expires_at` |
| Keycloak client secret | DB `SecureValue` | Yes (DPAPI) | `auth:client_secret` |
| API keys (TMDB etc.) | DB `Value` | No (public keys) | `apikeys:tmdb`, `apikeys:tvdb`, etc. |
| API keys cached_at | DB `Value` | No | `apikeys:cached_at` |
| Server name | DB `Value` | No | `serverName` (already exists) |
| Port config | DB `Value` | No | `internalPort` (already exists) |

#### Migration Path

1. On first launch after upgrade: DB has no token rows yet
2. Check if `auth_token.json` file exists → read tokens → write to DB → delete file
3. Check if `api_keys.json` file exists → read keys → write to DB → delete file
4. All subsequent launches read from DB only
5. JSON files become unnecessary — single source of truth is the database

#### No More JSON Token Files

After this change, the `config/` directory simplifies to:

```
{ConfigPath}/
├── folderRootsSeed.jsonc      ← Seed data (read-only templates)
├── librariesSeed.jsonc        ← Seed data
└── encoderProfilesSeed.jsonc  ← Seed data
```

Auth tokens, API keys, and server config all live in the encrypted `Configuration` table in `media.db`. No plaintext secrets on disk.

| Task ID | Description | Effort |
|---------|-------------|--------|
| DBTOKEN-01 | Split `DatabaseSeeder.Run()` into 3 phases: `RunSchemaAndStatic()`, `RunApiSeeds()`, `RunAuthSeeds()` | Medium |
| DBTOKEN-02 | Implement `TokenStore.InitializeStandalone()` for pre-DI bootstrap | Small |
| DBTOKEN-03 | Add `SecureValue` column to `Configuration` table + migration | Small |
| DBTOKEN-04 | Add EF Core value converter for `SecureValue` | Small |
| DBTOKEN-05 | Refactor `Auth.Init()` to read/write tokens from DB instead of file | Medium |
| DBTOKEN-06 | Refactor `ApiInfo.RequestInfo()` to cache in DB instead of file | Medium |
| DBTOKEN-07 | Add migration logic: JSON files → DB on first upgrade launch | Small |
| DBTOKEN-08 | Remove `auth_token.json` and `api_keys.json` file handling after migration period | Small |
| DBTOKEN-09 | Verify DPAPI key ring is consistent between standalone and DI-registered providers | Medium |

---

