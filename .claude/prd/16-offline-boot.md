## 16. Offline-Resilient Boot

> **Real-world trigger**: Cloudflare went down — server unable to boot. ISP maintenance — server unable to boot. Any network interruption during startup = dead server that cannot serve local content.

### 16.1 The Core Problem

The server **cannot boot without internet**. Every network-dependent step in the startup chain either calls `Environment.Exit(1)`, throws an unhandled exception, or blocks indefinitely — and `Start.RunStartup()` has **zero error handling**:

```csharp
// Start.cs:84-87 — NO try/catch, NO resilience
private static async Task RunStartup(List<TaskDelegate> startupTasks)
{
    foreach (TaskDelegate task in startupTasks) await task.Invoke();
}
```

Any single step throwing = the entire server dies. The server should be capable of serving local, already-cached content (movies, shows, music) even when the internet is completely unavailable.

### 16.2 Complete Failure Map

Every network call during startup, what it hits, and what happens when it fails:

| Step | Method | Target | On Failure | Fatal? | Line |
|------|--------|--------|-----------|--------|------|
| **① ApiInfo.RequestInfo** | GET `v1/info` | api.nomercy.tv (Cloudflare) | `Environment.Exit(1)` | **YES — hard kill** | `ApiInfo.cs:71` |
| **② Auth.AuthKeys** | GET Keycloak `.well-known` | Keycloak server | `throw` — no catch in caller | **YES** | `Auth.cs:318` |
| **③ Auth.TokenByRefreshGrand** | POST Keycloak token endpoint | Keycloak server | Caught silently, returns null | No — but sets null tokens | `Auth.cs:44-50` |
| **④ Auth.Init (final check)** | — | — | `throw new("Failed to get tokens")` | **YES** | `Auth.cs:73` |
| **⑤ Networking.GetExternalIp** | GET `v1/ip` | api.nomercy.tv (Cloudflare) | `throw new("The NoMercy API is not available")` | **YES** | `Networking.cs:135` |
| **⑥ Networking.GetInternalIp** | UDP socket connect to 1.1.1.1 | 1.1.1.1 (Cloudflare DNS) | Socket exception | **YES** — no catch | `Networking.cs:117` |
| **⑦ LanguagesSeed** | GET TMDB languages | api.themoviedb.org | `throw` — no catch in seed | **YES** | LanguagesSeed.cs |
| **⑧ CountriesSeed** | GET TMDB countries | api.themoviedb.org | `throw` — no catch in seed | **YES** | CountriesSeed.cs |
| **⑨ GenresSeed** | GET TMDB genres | api.themoviedb.org | `throw` — no catch in seed | **YES** | GenresSeed.cs |
| **⑩ CertificationsSeed** | GET TMDB certifications | api.themoviedb.org | `throw` — no catch in seed | **YES** | CertificationsSeed.cs |
| **⑪ MusicGenresSeed** | GET MusicBrainz genres | musicbrainz.org | `throw` — no catch in seed | **YES** | MusicGenresSeed.cs |
| **⑫ Register.Init** | POST `register` | api.nomercy.tv (Cloudflare) | `throw` — no catch | **YES** | `Register.cs:54` |
| **⑬ Register.AssignServer** | POST `assign` | api.nomercy.tv (Cloudflare) | `throw` — no catch | **YES** | `Register.cs:76` |
| **⑭ Certificate.RenewSslCertificate** | GET `certificate?id=` | api.nomercy.tv (Cloudflare) | 3 retries → `throw` | **YES** | `Certificate.cs:92-103` |
| **⑮ UsersSeed** | GET `server-users` | api.nomercy.tv (Cloudflare) | `throw` — no catch | **YES** | UsersSeed.cs |
| **⑯ Binaries.DownloadAll** | GET GitHub API releases | api.github.com | Caught, logs, continues | No | `Binaries.cs:35` |

**Result**: **14 out of 16** network steps are fatal. Only `Binaries.DownloadAll` and `Auth.TokenByRefreshGrand` (partially) handle failure gracefully.

### 16.3 Cloudflare Single Point of Failure

**8 of the 14 fatal steps** hit `api.nomercy.tv` which sits behind Cloudflare:

```
ApiInfo.RequestInfo     → api.nomercy.tv  → Cloudflare
Networking.GetExternalIp → api.nomercy.tv  → Cloudflare
Register.Init           → api.nomercy.tv  → Cloudflare
Register.AssignServer   → api.nomercy.tv  → Cloudflare
Certificate.Renew       → api.nomercy.tv  → Cloudflare
UsersSeed               → api.nomercy.tv  → Cloudflare
Networking.GetInternalIp → 1.1.1.1        → Cloudflare DNS
Auth.AuthKeys           → Keycloak        → (may be Cloudflare-proxied)
```

When Cloudflare goes down, **all 8** fail simultaneously. When ISP has maintenance, **all 14** fail.

### 16.4 Design Principle: Degraded Mode

The server must distinguish between two operational modes:

| Mode | Condition | Capability |
|------|-----------|------------|
| **Full mode** | Internet available, all services reachable | Everything works — registration, remote streaming, metadata fetch, cert renewal |
| **Degraded mode** | No internet OR Cloudflare down OR Keycloak down | Local streaming works, cached metadata works, no remote access, no new metadata, background retry to restore full mode |

**Key insight**: A user watching a movie on their local network does not need Cloudflare, Keycloak, TMDB, or MusicBrainz to be online. On a server that has already been set up, the database has all the metadata, the cert is on disk, the auth token is cached — the server should just boot and serve. Restore full functionality in the background when connectivity returns.

### 16.5 What Must Work Offline

| Feature | Required For Offline? | Current State |
|---------|----------------------|---------------|
| Kestrel HTTPS server | **YES** | Needs cert file on disk — works if cert was previously fetched |
| Local media streaming | **YES** | Works if DB exists with media data |
| SignalR hubs | **YES** | Local connections work |
| User authentication | **YES** (cached token) | Currently requires Keycloak to be reachable for public key |
| API key availability | **YES** (cached) | Currently `Environment.Exit(1)` if fetch fails |
| External IP discovery | No | Only needed for remote access |
| Server registration | No | Only needed for remote discovery |
| Certificate renewal | No | Only needed when cert expires (30-day window) |
| Seed data (genres, languages) | No | Only needed on first launch or to update |
| Binary downloads | No | Already handled gracefully |

### 16.6 Fix: Startup Resilience Architecture

#### 16.6.1 Split Startup Into Required vs. Deferrable

```csharp
public static async Task Init(List<TaskDelegate> tasks)
{
    // ── PHASE 1: MUST SUCCEED (no network) ─────────────────────
    await AppFiles.CreateAppFolders();
    await DatabaseSeeder.RunSchemaAndStatic();
    TokenStore.InitializeStandalone();
    UserSettings.ApplySettings();

    // ── PHASE 2: BEST-EFFORT (network, with fallback) ─────────
    bool hasNetwork = await NetworkProbe.CheckConnectivity();

    bool hasApiKeys = await ApiInfo.RequestInfoWithFallback();   // DB cache → network
    bool hasAuth = await Auth.InitWithFallback();                // cached token → refresh → prompt
    bool hasNetworkInfo = await DiscoverWithFallback();          // UPnP → API → cached

    // ── PHASE 3: NETWORK-REQUIRED (skip if offline) ────────────
    if (hasNetwork && hasAuth && hasApiKeys)
    {
        await DatabaseSeeder.RunApiSeeds();      // TMDB, MusicBrainz
        await Register.Init();                   // registration + cert
        await DatabaseSeeder.RunAuthSeeds();     // UsersSeed
    }
    else
    {
        Logger.App("⚠ Starting in DEGRADED MODE — some features unavailable");
        Logger.App("  Network-dependent tasks will retry in background");
        ScheduleDeferredTasks(tasks);
    }

    // ── PHASE 4: ALWAYS BACKGROUND ────────────────────────────
    _ = Task.Run(() => Binaries.DownloadAll());
    _ = Task.Run(() => ChromeCast.Init());
    _ = Task.Run(() => UpdateChecker.StartPeriodicUpdateCheck());
}
```

#### 16.6.2 Network Connectivity Probe

Don't try the full API — just check if we have a route to the internet:

```csharp
public static class NetworkProbe
{
    public static async Task<bool> CheckConnectivity()
    {
        // Fast check — can we resolve DNS and open a TCP connection?
        string[] probeTargets = [
            "api.nomercy.tv",    // Primary
            "1.1.1.1",           // Cloudflare DNS (IP — no DNS needed)
            "8.8.8.8"            // Google DNS (IP — no DNS needed)
        ];

        foreach (string target in probeTargets)
        {
            try
            {
                using TcpClient client = new();
                Task connectTask = client.ConnectAsync(target, 443);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
                {
                    await connectTask; // propagate any exception
                    return true;
                }
            }
            catch { /* try next */ }
        }

        return false;
    }
}
```

#### 16.6.3 ApiInfo — Network-First with DB Cache Fallback

Replace the `Environment.Exit(1)` with a cache-first pattern:

```csharp
public static async Task<bool> RequestInfoWithFallback()
{
    // Try network first
    try
    {
        await RequestInfoFromNetwork();
        await CacheToDatabase();      // persist for offline use
        return true;
    }
    catch (Exception e)
    {
        Logger.Setup($"Network fetch failed: {e.Message}. Trying cache...",
            LogEventLevel.Warning);
    }

    // Fallback to DB cache
    if (LoadFromDatabaseCache())
    {
        Logger.Setup("Loaded API keys from cache (may be stale)");
        return true;
    }

    // No cache — first launch with no internet
    Logger.Setup("No API keys available — server starting in limited mode",
        LogEventLevel.Warning);
    return false;
}
```

**NEVER** call `Environment.Exit(1)` on a network failure. The only valid reason for `Environment.Exit` is an unrecoverable local error (corrupt database, missing runtime, etc.).

#### 16.6.4 Auth — Cached Token Validation Without Keycloak

The current `Auth.Init()` calls `AuthKeys()` which hits Keycloak to get the public key — this fails offline. Fix:

```csharp
public static async Task<bool> InitWithFallback()
{
    // Step 1: Load cached token from DB/file
    LoadCachedTokens();

    if (Globals.Globals.AccessToken is null)
    {
        // No token at all — first launch, need network
        Logger.Auth("No cached token — authentication requires network",
            LogEventLevel.Warning);
        return false;
    }

    // Step 2: Check if token is still valid (local check, no network)
    JwtSecurityTokenHandler handler = new();
    JwtSecurityToken jwt = handler.ReadJwtToken(Globals.Globals.AccessToken);

    if (jwt.ValidTo > DateTime.UtcNow.AddMinutes(5))
    {
        // Token still valid — skip Keycloak entirely
        Logger.Auth("Using cached token (still valid)");
        return true;
    }

    // Step 3: Token expired — try refresh (needs network)
    try
    {
        await AuthKeys();                // get public key from Keycloak
        await TokenByRefreshGrand();     // refresh the token
        return true;
    }
    catch (Exception e)
    {
        Logger.Auth($"Token refresh failed: {e.Message}. Using expired token.",
            LogEventLevel.Warning);
        // Use the expired token anyway — local requests don't validate against Keycloak
        return true;
    }
}
```

**Key insight**: For local-only operation, the JWT token doesn't need to be validated against Keycloak. The server itself issued the token and can trust it for local API requests. Remote access through the domain API will fail anyway if the network is down.

#### 16.6.5 Networking.GetInternalIp — Don't Hit Cloudflare DNS

The current implementation opens a UDP socket to `1.1.1.1:65530` to discover the local IP. This fails when there's no route to Cloudflare:

```csharp
// Current — fails offline
private static string GetInternalIp()
{
    using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
    socket.Connect("1.1.1.1", 65530);  // ← FAILS if no internet
    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
    return endPoint?.Address.ToString() ?? "";
}
```

Fix — use the network interface enumeration as primary, socket trick as fallback:

```csharp
private static string GetInternalIp()
{
    // Primary: enumerate local network interfaces (no network needed)
    try
    {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel) continue;

            foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(addr.Address)) continue;

                return addr.Address.ToString();
            }
        }
    }
    catch { /* fall through to socket method */ }

    // Fallback: UDP socket trick (needs route to internet)
    try
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("1.1.1.1", 65530);
        return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
    }
    catch
    {
        return "127.0.0.1";  // Last resort — localhost
    }
}
```

#### 16.6.6 Networking.GetExternalIp — Cache + Skip

External IP is only needed for remote access. When offline:

```csharp
private static async Task<string> GetExternalIp()
{
    // Try API
    try
    {
        GenericHttpClient apiClient = new(Config.ApiBaseUrl);
        apiClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
        HttpResponseMessage response = await apiClient.SendAsync(HttpMethod.Get, "v1/ip");
        if (response.IsSuccessStatusCode)
        {
            string ip = (await response.Content.ReadAsStringAsync()).Replace(""", "");
            CacheExternalIp(ip);  // persist to DB
            return ip;
        }
    }
    catch { /* fall through */ }

    // Try UPnP device
    if (_device is not null)
    {
        try { return _device.GetExternalIP().ToString(); }
        catch { /* fall through */ }
    }

    // Try DB cache
    string? cached = LoadCachedExternalIp();
    if (cached is not null)
    {
        Logger.Setup($"Using cached external IP: {cached}");
        return cached;
    }

    // No external IP available — not fatal, remote access just won't work
    Logger.Setup("External IP unavailable — remote access disabled", LogEventLevel.Warning);
    return "";
}
```

#### 16.6.7 Certificate — Use Existing Cert, Don't Block Boot

The current flow: `Register.Init()` → `Certificate.RenewSslCertificate()`. If the cert file exists on disk and hasn't expired, this already short-circuits. But if the cert needs renewal and the network is down, it throws.

Fix: **Never let cert renewal block boot.** If the cert file exists (even if expiring soon), use it. Schedule renewal in background.

```csharp
public static async Task RenewSslCertificateWithFallback()
{
    if (ValidateSslCertificate())
    {
        Logger.Certificate("SSL Certificate is valid");
        return;
    }

    if (File.Exists(AppFiles.CertFile))
    {
        // Cert exists but will expire soon — use it, renew in background
        Logger.Certificate("SSL Certificate expiring soon — will renew in background",
            LogEventLevel.Warning);
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            await RenewWithRetry();
        });
        return;
    }

    // No cert at all — this IS fatal for HTTPS
    // Fall back to self-signed cert for local access
    GenerateSelfSignedCert();
    Logger.Certificate("No SSL certificate — using self-signed for local access",
        LogEventLevel.Warning);
    _ = Task.Run(async () => await RenewWithRetry());
}
```

#### 16.6.8 API-Dependent Seeds — Skip When Offline

Seeds that fetch from TMDB/MusicBrainz should be idempotent — they upsert data. If the data already exists from a previous boot, skip the network fetch:

```csharp
// In each seed, add a "has data?" check
public static async Task Init(MediaContext context)
{
    if (await context.Languages.AnyAsync())
    {
        Logger.Setup("Languages already seeded — skipping network fetch");
        return;
    }

    // ... existing fetch + upsert logic
}
```

This means: first boot requires internet (to populate the empty DB), subsequent boots with no internet skip the seeds entirely.

#### 16.6.9 Register.Init — Defer When Offline

Server registration is only needed for remote discovery. When offline:

```csharp
public static async Task InitWithFallback()
{
    try
    {
        await Init();  // existing logic
    }
    catch (Exception e)
    {
        Logger.Register($"Registration failed: {e.Message}. " +
            "Server will operate in local-only mode.", LogEventLevel.Warning);
        ScheduleBackgroundRetry();
    }
}
```

### 16.7 Background Recovery

When the server starts in degraded mode, it should continuously try to restore full functionality:

```csharp
public static class DegradedModeRecovery
{
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30)   // cap
    ];

    public static async Task StartRecoveryLoop(DeferredTasks tasks)
    {
        int attempt = 0;

        while (!tasks.AllCompleted)
        {
            TimeSpan delay = BackoffSchedule[Math.Min(attempt, BackoffSchedule.Length - 1)];
            await Task.Delay(delay);

            bool hasNetwork = await NetworkProbe.CheckConnectivity();
            if (!hasNetwork)
            {
                attempt++;
                Logger.App($"Network still unavailable. Next retry in {delay}");
                continue;
            }

            // Network is back — execute deferred tasks in order
            if (!tasks.ApiKeysLoaded)
            {
                tasks.ApiKeysLoaded = await ApiInfo.RequestInfoWithFallback();
            }

            if (!tasks.Authenticated && tasks.ApiKeysLoaded)
            {
                tasks.Authenticated = await Auth.InitWithFallback();
            }

            if (!tasks.Registered && tasks.Authenticated)
            {
                try
                {
                    await Register.Init();
                    tasks.Registered = true;
                }
                catch { /* retry next loop */ }
            }

            if (!tasks.SeedsRun && tasks.ApiKeysLoaded)
            {
                try
                {
                    await DatabaseSeeder.RunApiSeeds();
                    tasks.SeedsRun = true;
                }
                catch { /* retry next loop */ }
            }

            if (tasks.Registered)
            {
                try
                {
                    await DatabaseSeeder.RunAuthSeeds();
                    tasks.AllCompleted = true;
                    Logger.App("✓ Full mode restored — all deferred tasks completed");
                }
                catch { /* retry next loop */ }
            }

            attempt++;
        }
    }
}
```

### 16.8 Scope: Subsequent Boots Only

First boot without internet is a non-scenario — the user just downloaded the app (which requires internet), and the server needs API data from TMDB/MusicBrainz for anything to work at all. There's nothing to serve without metadata.

**The real problem is exclusively about subsequent boots**: the server has been set up, has a valid cert on disk, has a database full of cached metadata, has media files ready to stream — and then the internet goes down (ISP maintenance, Cloudflare outage, DNS failure) and the server refuses to restart.

Everything in this section targets that scenario: **a previously-working server must survive a reboot during a network outage.**

### 16.9 RunStartup Error Handling

The fundamental fix — `RunStartup` must not let a single failure kill the server:

```csharp
private static async Task RunStartup(List<StartupTask> startupTasks)
{
    foreach (StartupTask task in startupTasks)
    {
        try
        {
            await task.Action.Invoke();
        }
        catch (Exception ex) when (task.CanDefer)
        {
            Logger.Setup($"Startup task '{task.Name}' failed: {ex.Message}. " +
                "Deferring to background.", LogEventLevel.Warning);
            DeferredTasks.Add(task);
        }
        catch (Exception ex) when (!task.CanDefer)
        {
            Logger.Setup($"Required startup task '{task.Name}' failed: {ex.Message}",
                LogEventLevel.Fatal);
            throw;  // Only truly required tasks (filesystem, DB schema) can kill boot
        }
    }
}
```

Each startup task should be tagged with whether it's deferrable:

```csharp
public record StartupTask(string Name, TaskDelegate Action, bool CanDefer);

List<StartupTask> startupTasks =
[
    new("CreateAppFolders",    AppFiles.CreateAppFolders,            CanDefer: false),
    new("SchemaAndStatic",     DatabaseSeeder.RunSchemaAndStatic,    CanDefer: false),
    new("TokenStore",          TokenStore.InitializeStandalone,      CanDefer: false),
    new("UserSettings",        UserSettings.ApplySettings,           CanDefer: false),
    new("ApiInfo",             ApiInfo.RequestInfoWithFallback,      CanDefer: true),
    new("Auth",                Auth.InitWithFallback,                CanDefer: true),
    new("Networking",          Networking.DiscoverWithFallback,      CanDefer: true),
    new("ApiSeeds",            DatabaseSeeder.RunApiSeeds,           CanDefer: true),
    new("Register",            Register.InitWithFallback,            CanDefer: true),
    new("AuthSeeds",           DatabaseSeeder.RunAuthSeeds,          CanDefer: true),
    new("Certificate",         Certificate.RenewWithFallback,        CanDefer: true),
    new("Binaries",            Binaries.DownloadAll,                 CanDefer: true),
    new("ChromeCast",          ChromeCast.Init,                      CanDefer: true),
    new("UpdateChecker",       UpdateChecker.StartPeriodicCheck,     CanDefer: true),
];
```

### 16.10 Certificate: Don't Panic on Expiring Cert

Since we only care about subsequent boots (see 16.8), the cert file **will exist on disk** from the initial setup. The only question is whether it's expiring soon.

The current `ValidateSslCertificate()` returns `false` when the cert is within 30 days of expiry. This triggers renewal, which fails offline, which kills the server. But the cert is still **perfectly valid** — it just expires *soon*.

```csharp
private static bool ValidateSslCertificate()
{
    if (!File.Exists(AppFiles.CertFile))
        return false;

    X509Certificate2 certificate = CombinePublicAndPrivateCerts();

    // Don't call certificate.Verify() if we have no internet —
    // it may fail on OCSP/CRL checks. Just check the expiry date.
    if (certificate.NotAfter > DateTime.Now)
    {
        // Cert still valid — use it
        if (certificate.NotAfter < DateTime.Now.AddDays(30))
        {
            Logger.Certificate($"SSL cert expires {certificate.NotAfter:yyyy-MM-dd} — " +
                "will attempt background renewal");
            ScheduleBackgroundRenewal();
        }
        return true;  // ← KEY CHANGE: don't block boot
    }

    return false;  // Actually expired past its NotAfter date
}
```

This means renewal is attempted 30 days early (current behavior), but if it fails, the server still boots with the existing cert. The cert only becomes a real problem if the network is down for the entire remaining validity period — typically 60-90 days.

### 16.11 Findings Summary

| ID | Severity | Description | File:Line |
|----|----------|-------------|-----------|
| OFFLINE-01 | **CRITICAL** | `Environment.Exit(1)` on API key fetch failure — server dies if api.nomercy.tv unreachable | `ApiInfo.cs:71` |
| OFFLINE-02 | **CRITICAL** | `RunStartup` has zero error handling — any throw kills the server | `Start.cs:84-87` |
| OFFLINE-03 | **CRITICAL** | `Auth.AuthKeys()` throws if Keycloak unreachable, no fallback for cached tokens | `Auth.cs:318` |
| OFFLINE-04 | **CRITICAL** | `Auth.Init()` throws "Failed to get tokens" if refresh fails offline | `Auth.cs:73` |
| OFFLINE-05 | **CRITICAL** | `Register.Init()` throws if api.nomercy.tv unreachable, no deferral | `Register.cs:54` |
| OFFLINE-06 | **CRITICAL** | `Certificate.RenewSslCertificate` blocks boot if cert renewal fails (3 retries → throw) | `Certificate.cs:92-103` |
| OFFLINE-07 | **HIGH** | `GetExternalIp()` throws "NoMercy API not available" — fatal during Discover() | `Networking.cs:135` |
| OFFLINE-08 | **HIGH** | `GetInternalIp()` connects to 1.1.1.1 — fails with no internet route | `Networking.cs:117` |
| OFFLINE-09 | **HIGH** | All 5 API-dependent seeds throw on network failure, no "already seeded" bypass | Seeds/*.cs |
| OFFLINE-10 | **HIGH** | `UsersSeed` throws on api.nomercy.tv failure, no deferral | UsersSeed.cs |
| OFFLINE-11 | **HIGH** | No network connectivity probe — server doesn't know it's offline before trying | `Start.cs` |
| OFFLINE-12 | **MEDIUM** | No degraded mode concept — server is either fully running or dead | Architecture |
| OFFLINE-13 | **MEDIUM** | No background recovery — if startup fails, only a restart can retry | Architecture |
| OFFLINE-14 | **MEDIUM** | `ValidateSslCertificate()` triggers renewal on expiring-but-valid cert — blocks boot when offline | `Certificate.cs:56-67` |
| OFFLINE-15 | **MEDIUM** | Seed data fetched on every boot even if DB already has it — unnecessary network dependency | Seeds/*.cs |
| OFFLINE-16 | **LOW** | No user-facing status for degraded mode (which features are unavailable) | UX |

### 16.12 Implementation Tasks

| ID | Task | Effort | Priority |
|----|------|--------|----------|
| OFFLINE-IMPL-01 | Remove `Environment.Exit(1)` from `ApiInfo.cs` — replace with cache fallback | Small | **P0** |
| OFFLINE-IMPL-02 | Add try/catch with `CanDefer` flag to `RunStartup` | Medium | **P0** |
| OFFLINE-IMPL-03 | Add `NetworkProbe.CheckConnectivity()` early in startup | Small | **P0** |
| OFFLINE-IMPL-04 | Fix `GetInternalIp` to use NetworkInterface enumeration first | Small | **P0** |
| OFFLINE-IMPL-05 | Make `Auth.Init` work with cached tokens when Keycloak unreachable | Medium | **P0** |
| OFFLINE-IMPL-06 | Make `GetExternalIp` non-fatal with cache fallback | Small | **P1** |
| OFFLINE-IMPL-07 | Add "already seeded?" checks to all API-dependent seeds | Small | **P1** |
| OFFLINE-IMPL-08 | Make `Register.Init` deferrable with background retry | Medium | **P1** |
| OFFLINE-IMPL-09 | Make `Certificate.RenewSslCertificate` non-blocking when existing cert on disk | Small | **P1** |
| OFFLINE-IMPL-10 | Fix `ValidateSslCertificate` to not block boot when cert is expiring but still valid | Small | **P1** |
| OFFLINE-IMPL-11 | Implement `DegradedModeRecovery` background loop with exponential backoff | Medium | **P2** |
| OFFLINE-IMPL-12 | Add degraded mode status to dashboard/API (which features are offline) | Small | **P2** |

---

