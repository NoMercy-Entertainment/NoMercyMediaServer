## 14. Initial Server Setup & First-Time Login

> When running as a systemd/Windows service, there is no console window. The server must provide an HTTP-only web-based setup flow for first-time registration and login via Keycloak.

### 14.1 Current Flow (Console-Dependent)

The current startup sequence (`src/NoMercy.Setup/Start.cs`) assumes console access:

```
1. ApiInfo.RequestInfo()          → Fetch shared API keys (HARD EXIT on failure)
2. AppFiles.CreateAppFolders()    → Create directory structure
3. Auth.Init()                    → OAuth login (CONSOLE INPUT REQUIRED)
   ├─ Shows menu: "1. QR code  2. Password"
   ├─ Waits 15s for keypress via Console.ReadKey()
   ├─ Password flow reads char-by-char via Console.ReadKey(true)
   └─ QR code flow displays ASCII art via Console.WriteLine
4. Networking.Discover()          → UPNP discovery (15s delay)
5. Register.Init()                → Register server with API + get SSL certs
6. Binaries.DownloadAll()         → Download FFmpeg, etc.
```

**Problems for headless/service mode:**
- `Console.ReadKey()` blocks or fails when stdin is closed
- `Console.KeyAvailable` always returns false with piped/redirected input
- QR code display requires a terminal
- `Environment.Exit(1)` if API unreachable — no recovery
- No web UI for first-time authentication
- Browser opening (`xdg-open`/`open`) fails without DISPLAY

### 14.2 Token Validation — Current Issues

The current startup auth flow (`src/NoMercy.Setup/Auth.cs`) has **no graceful error handling** — it either crashes or silently continues without authentication:

#### Crash Points (Server Dies on Startup)

| Scenario | Line | Exception |
|----------|------|-----------|
| Corrupted JSON in token.json | `Auth.cs:~284` (`TokenData()`) | `JsonReaderException` — unhandled |
| Malformed JWT string | `Auth.cs:54` (`ReadJwtToken()`) | `SecurityTokenMalformedException` — unhandled |
| Auth public key fetch fails | `Auth.cs:~318` (`AuthKeys()`) | Network exception — unhandled |

#### Silent Failures (Server Starts with Null Token)

| Scenario | Line | Result |
|----------|------|--------|
| Token file empty/missing + refresh fails | `Auth.cs:40-50` | Silent catch → returns without error → `Globals.AccessToken = null` |
| Refresh token expired | `Auth.cs:65` | Silent catch → falls through → may prompt for console login that can't happen in service mode |

#### Logic Bug — Inverted Expiration Check

```csharp
// Auth.cs:58
bool expired = NotBefore == null && expiresInDays >= 0;
//              ↑ null = OK            ↑ >= 0 = NOT expired
// This marks VALID tokens as "expired" — inverted logic!
```

When `NotBefore` is null (normal) AND token has days remaining (valid), this evaluates to `true` ("expired"), triggering unnecessary re-authentication.

#### Required Fix — Defensive Token Validation

The boot sequence must validate tokens **before** parsing them:

```csharp
public static async Task<TokenState> ValidateToken()
{
    // 1. Check file exists and is valid JSON
    if (!File.Exists(AppFiles.TokenFile))
        return TokenState.Missing;

    string json;
    try { json = await File.ReadAllTextAsync(AppFiles.TokenFile); }
    catch { return TokenState.Corrupt; }

    if (string.IsNullOrWhiteSpace(json) || json == "{}")
        return TokenState.Missing;

    // 2. Parse JSON safely
    AuthResponse? tokenData;
    try { tokenData = JsonConvert.DeserializeObject<AuthResponse>(json); }
    catch { return TokenState.Corrupt; }

    if (string.IsNullOrEmpty(tokenData?.AccessToken))
        return TokenState.Missing;

    // 3. Validate JWT format before parsing
    string[] parts = tokenData.AccessToken.Split('.');
    if (parts.Length != 3)
        return TokenState.Corrupt;

    // 4. Parse and check expiration
    try
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(tokenData.AccessToken);

        if (jwt.ValidTo < DateTime.UtcNow.AddDays(5))
            return TokenState.Expired;  // Needs refresh
    }
    catch { return TokenState.Corrupt; }

    // 5. Check refresh token exists
    if (string.IsNullOrEmpty(tokenData.RefreshToken))
        return TokenState.NoRefreshToken;

    return TokenState.Valid;
}

public enum TokenState
{
    Valid,           // Token exists, valid JWT, not expired → Normal startup
    Expired,         // Token exists but expired → Try refresh, fallback to setup UI
    Missing,         // No token file or empty → Enter setup mode
    Corrupt,         // File exists but unparseable → Delete and enter setup mode
    NoRefreshToken   // Access token OK but no refresh → Will expire, warn user
}
```

### 14.2b Target Flow (HTTP-Only, No Console)

The server must boot into a **minimal HTTP setup mode** when no valid authentication exists:

```
BOOT
 ├─ ValidateToken() → returns TokenState
 │   ├─ Valid           → Normal startup (skip setup UI)
 │   ├─ Expired         → Try TokenByRefreshGrant()
 │   │   ├─ Success     → Normal startup
 │   │   └─ Failure     → Enter Setup Mode
 │   ├─ Missing         → Enter Setup Mode
 │   ├─ Corrupt         → Delete token.json → Enter Setup Mode
 │   └─ NoRefreshToken  → Log warning → Normal startup (will need re-auth later)
 │
 └─ SETUP MODE (HTTP-only, no HTTPS yet — no certs acquired)
     ├─ Start minimal Kestrel on http://0.0.0.0:{port}
     ├─ Serve setup web UI at http://{ip}:{port}/setup
     │   ├─ Step 1: Welcome / server name
     │   ├─ Step 2: "Login with NoMercy" button → Keycloak OAuth redirect
     │   ├─ Step 3: OAuth callback → exchange code for tokens → store token.json
     │   ├─ Step 4: Server registers with API → acquires SSL certificate
     │   ├─ Step 5: Success → restart with HTTPS enabled
     │   └─ Error handling: Show clear error messages in browser, retry buttons
     ├─ All other endpoints return 503 "Server is in setup mode"
     └─ Console-less: No stdin/stdout required
```

### 14.3 Setup Web UI Requirements

#### 14.3.1 HTTP-Only Bootstrap
The setup UI **must** run on plain HTTP because:
- No SSL certificate exists yet (acquired during registration)
- The server hasn't authenticated with the domain API yet
- The user needs to access the UI from a browser on the local network

**Security**: This is acceptable because:
- Setup only happens once (first launch)
- Traffic is on the local network only
- The OAuth flow itself redirects to Keycloak over HTTPS
- The callback URL can still be `http://localhost:{port}/sso-callback`

#### 14.3.2 OAuth Flow via Browser
```
User visits: http://{server-ip}:7626/setup
  → Clicks "Login with NoMercy"
  → Browser redirects to: https://auth.nomercy.tv/realms/NoMercyTV/protocol/openid-connect/auth
     ?client_id=nomercy-server
     &redirect_uri=http://{server-ip}:7626/sso-callback
     &response_type=code
     &scope=openid+offline_access+email+profile
  → User logs in / registers on Keycloak
  → Keycloak redirects back to: http://{server-ip}:7626/sso-callback?code={code}
  → Server exchanges code for tokens
  → Server stores tokens in token.json
  → Setup UI shows "Registering server..." progress
  → Server calls Register.Init() + Certificate.RenewSslCertificate()
  → Setup UI shows "Setup complete! Restarting with HTTPS..."
  → Server restarts Kestrel with HTTPS
```

#### 14.3.3 Device Grant Fallback
For scenarios where the browser can't reach the server directly (e.g., Docker without port exposure during setup):
- The setup page should also display a **QR code + device code** (same as current `TokenByDeviceGrant()`)
- User scans QR code on their phone, authenticates on Keycloak mobile
- Server polls for completion and proceeds when authenticated

#### 14.3.4 Setup UI Pages

| Route | Purpose |
|-------|---------|
| `GET /setup` | Main setup page — login button + QR code fallback |
| `GET /sso-callback?code={code}` | OAuth callback — exchanges code, stores tokens |
| `GET /setup/status` | SSE/polling endpoint for setup progress updates |
| `GET /setup/complete` | Success page with server URL |
| `*` (all other routes) | 503 JSON: `{ "status": "setup_required", "setup_url": "/setup" }` |

### 14.4 Registration & Certificate Acquisition

After authentication succeeds, the server must:

1. **Register with domain API** (`POST https://api.nomercy.tv/v1/server/register`)
   - Send: server ID (hardware-derived), server name, IPs, ports, platform, version
   - Receive: confirmation
   - **Must retry on failure** — current code does not retry

2. **Assign server to user** (`POST https://api.nomercy.tv/v1/server/assign`)
   - Send: server ID + user's access token
   - Receive: user data (id, name, email)
   - Create admin user in local SQLite database with `Owner=true`

3. **Acquire SSL certificate** (`GET https://api.nomercy.tv/v1/server/certificate?id={DeviceId}`)
   - Receive: cert.pem, key.pem, ca.pem
   - Write to `{AppData}/root/certs/`
   - Validate certificate is valid and not expired

4. **Restart with HTTPS** — Kestrel must be reconfigured or restarted to load the new certificates

**Each step must be idempotent and retryable.** If step 3 fails, the user should see an error in the setup UI with a "Retry" button, not a crash.

### 14.5 Current Code Issues to Fix

| Issue | File | Line | Fix |
|-------|------|------|-----|
| **Corrupted token.json crashes startup** | `Auth.cs` | ~284 | Wrap in try/catch, enter setup mode |
| **Malformed JWT crashes startup** | `Auth.cs` | 54 | Validate JWT format before `ReadJwtToken()` |
| **Inverted expiration logic** | `Auth.cs` | 58 | Fix: `bool expired = expiresInDays < 0` |
| **Null token → server starts silently** | `Auth.cs` | 40-50 | Enforce token presence or enter setup mode |
| `Environment.Exit(1)` on API failure | `ApiInfo.cs` | 36-73 | Return error, show in setup UI |
| Console menu with `Console.ReadKey()` | `Auth.cs` | 86-117 | Replace with web UI |
| Password flow with `Console.ReadKey(true)` | `Auth.cs` | 193-219 | Remove (browser handles auth) |
| QR code via `Console.WriteLine` | `Auth.cs` | 120-143 | Render in web UI |
| `Thread.Sleep` in device grant polling | `Auth.cs` | 157 | `await Task.Delay()` |
| Recursive `CheckToken()` with `.Wait()` | `Auth.cs` | 252-263 | Loop with `await Task.Delay` |
| `.Result` blocking calls | `Auth.cs` | 98, 165 | `await` |
| Hardcoded client secret | `Config.cs` | 9 | Environment variable or secure store |
| **Hardcoded TMDB JWT in tests** | `TmdbTestBase.cs` | 61 | Load from env var or user-secrets |
| No retry on registration | `Register.cs` | — | Add retry with exponential backoff |
| No retry on certificate fetch | `Certificate.cs` | — | Add retry with exponential backoff |
| Token file read 4 times | `Auth.cs` | 35-38 | Read once, extract all fields |
| Rename `token.json` → `auth_token.json` | `AppFiles.cs` | 22 | Clear naming + auto-migrate old file |
| TempServer only handles auth code | `TempServer.cs` | — | Expand to full setup UI |

### 14.6 Architecture

```
src/NoMercy.Setup/
├── Auth.cs                    → Refactor: Remove all Console.* calls
├── Register.cs                → Add retry logic, idempotent operations
├── TempServer.cs              → Replace: Becomes SetupServer
├── SetupServer.cs (NEW)       → Minimal Kestrel for setup UI
│   ├── Serves static HTML/JS/CSS for setup wizard
│   ├── /setup endpoint
│   ├── /sso-callback endpoint
│   ├── /setup/status SSE endpoint
│   └── Returns 503 for all other routes
├── SetupState.cs (NEW)        → State machine: Unauthenticated → Authenticating → Registering → Complete
└── Start.cs                   → Refactor: Check state before full startup

src/NoMercy.Server/
├── Program.cs                 → Check token validity on boot, enter setup mode if needed
└── AppConfig/
    └── ServiceConfiguration.cs → Conditional: Setup middleware vs. full app middleware
```

### 14.7 Implementation Tasks

| Task ID | Description | Effort | Priority |
|---------|-------------|--------|----------|
| SETUP-01 | Create `SetupState` enum/state machine (Unauthenticated → Authenticated → Registered → CertificateAcquired → Complete) | Small | Critical |
| SETUP-02 | Create `SetupServer` with minimal Kestrel HTTP host | Medium | Critical |
| SETUP-03 | Build setup web UI (HTML/JS/CSS — simple, embedded in binary) | Medium | Critical |
| SETUP-04 | Implement `/setup` page with OAuth login button + QR code fallback | Medium | Critical |
| SETUP-05 | Implement `/sso-callback` handler with token exchange and storage | Small | Critical |
| SETUP-06 | Implement `/setup/status` endpoint for progress updates (SSE or polling) | Small | Critical |
| SETUP-07 | Add 503 middleware for all non-setup routes during setup mode | Small | Critical |
| SETUP-08 | Refactor `Auth.cs` — remove all `Console.*` calls, extract pure OAuth logic | Medium | Critical |
| SETUP-09 | Add retry with exponential backoff to `Register.Init()` | Small | High |
| SETUP-10 | Add retry with exponential backoff to `Certificate.RenewSslCertificate()` | Small | High |
| SETUP-11 | Implement graceful Kestrel restart (HTTP → HTTPS) after cert acquisition | Medium | High |
| SETUP-12 | Remove `Environment.Exit(1)` from `ApiInfo.cs` — return error state | Small | High |
| SETUP-13 | Add `--headless` CLI flag or `NOMERCY_HEADLESS` env var | Small | Medium |
| SETUP-14 | Support pre-mounted `token.json` for container deployments | Small | Medium |
| SETUP-15 | Support `NOMERCY_ACCESS_TOKEN` / `NOMERCY_REFRESH_TOKEN` env vars | Small | Medium |
| SETUP-16 | Certificate renewal background task (check every 24h, renew if <30 days) | Medium | Medium |
| SETUP-17 | End-to-end testing: fresh install → setup UI → login → registration → HTTPS | Large | Critical |

**Note**: The setup web UI should be simple embedded HTML — no SPA framework. A single HTML file with inline CSS/JS, served from an embedded resource. The OAuth redirect does all the heavy lifting via Keycloak's hosted login page.

### 14.9 Network-First Cache for API Keys

`ApiInfo.RequestInfo()` (`src/NoMercy.Setup/ApiInfo.cs:36-73`) fetches 11 API keys from `https://api.nomercy.tv/v1/info`. Currently if this fails: `Environment.Exit(1)` — hard crash, no recovery.

**Required pattern: Network-first with file cache fallback + background retry.**

```
STARTUP
 ├─ Try: GET https://api.nomercy.tv/v1/info
 │   ├─ SUCCESS → Apply keys → Write cache file → Done
 │   └─ FAILURE → Read cache file
 │       ├─ CACHE HIT  → Apply cached keys → Log warning → Start background refresh
 │       └─ CACHE MISS → Enter setup mode (cannot function without keys)
 │
 └─ BACKGROUND (after startup, if using cached keys)
     └─ Retry loop: exponential backoff (30s → 1m → 5m → 15m → 30m cap)
         ├─ SUCCESS → Apply fresh keys → Update cache → Stop retrying
         └─ FAILURE → Keep retrying at capped interval
```

#### Cache File

- **Location**: `{AppFiles.ConfigPath}/api_keys.json` (alongside `auth_token.json`)
- **Contents**: The full `ApiInfoResponse` JSON as received from the API
- **Metadata**: Add `cached_at` timestamp to know how stale the data is

```json
{
  "_cached_at": "2026-02-07T12:00:00Z",
  "status": "success",
  "data": {
    "keys": {
      "tmdb_key": "...",
      "tvdb_key": "...",
      "...": "..."
    },
    "quote": "...",
    "colors": ["#8f00fc", "#705BAD", "#CBAFFF"]
  }
}
```

#### Implementation

```csharp
public static async Task RequestInfo()
{
    // 1. Try network first
    ApiInfoResponse? liveData = await TryFetchFromNetwork();

    if (liveData is not null)
    {
        ApplyKeys(liveData);
        await WriteCacheFile(liveData);
        Logger.Setup("API keys loaded from network");
        return;
    }

    // 2. Network failed — try cache
    ApiInfoResponse? cachedData = await TryReadCacheFile();

    if (cachedData is not null)
    {
        ApplyKeys(cachedData);
        Logger.Setup($"API keys loaded from cache (cached at {cachedData.CachedAt})", LogEventLevel.Warning);
        StartBackgroundRefresh();  // Keep trying in the background
        return;
    }

    // 3. No network, no cache — cannot start
    Logger.Setup("API unreachable and no cached keys available", LogEventLevel.Error);
    // Don't Environment.Exit(1) — enter setup mode or wait for network
}

private static void StartBackgroundRefresh()
{
    _ = Task.Run(async () =>
    {
        int[] backoffSeconds = [30, 60, 300, 900, 1800];  // 30s, 1m, 5m, 15m, 30m
        int attempt = 0;

        while (true)
        {
            int delay = backoffSeconds[Math.Min(attempt, backoffSeconds.Length - 1)];
            await Task.Delay(TimeSpan.FromSeconds(delay));

            ApiInfoResponse? fresh = await TryFetchFromNetwork();
            if (fresh is not null)
            {
                ApplyKeys(fresh);
                await WriteCacheFile(fresh);
                Logger.Setup("API keys refreshed from network");
                return;  // Done — stop retrying
            }

            attempt++;
            Logger.Setup($"API key refresh attempt {attempt} failed, retrying in {delay}s");
        }
    });
}

private static string CacheFilePath =>
    Path.Combine(AppFiles.ConfigPath, "api_keys.json");
```

#### Edge Cases

| Scenario | Behavior |
|----------|----------|
| First ever launch, no cache, API down | Cannot start — show error in setup UI: "NoMercy API unreachable" |
| Normal launch, API down, cache exists | Start with cached keys, refresh in background |
| Normal launch, API returns new keys | Apply new keys, update cache |
| Cache file corrupted | Treat as cache miss, try network only |
| Cache extremely stale (>30 days) | Log warning, use it anyway, refresh aggressively |
| API returns different keys than cache | Apply network keys (source of truth), overwrite cache |
| Background refresh succeeds mid-operation | Apply new keys — providers pick up new values on next instantiation |

#### Why This Matters

The API keys include TMDB, TVDB, MusicBrainz, FanArt, etc. Without them, the server can't:
- Fetch movie/show metadata
- Download artwork
- Match music fingerprints
- Search for subtitles

But these keys **rarely change**. A cached copy from yesterday works fine for months. Crashing the entire server because your API was down for 5 minutes is a disproportionate response.

| Task ID | Description | Effort |
|---------|-------------|--------|
| CACHE-01 | Implement `TryFetchFromNetwork()` with timeout and error handling | Small |
| CACHE-02 | Implement `WriteCacheFile()` / `TryReadCacheFile()` with `_cached_at` metadata | Small |
| CACHE-03 | Implement `StartBackgroundRefresh()` with exponential backoff | Small |
| CACHE-04 | Remove `Environment.Exit(1)` — return state instead | Small |
| CACHE-05 | Add cache staleness warning (>30 days) | Small |
| CACHE-06 | Integration test: API down + cache exists → server starts | Small |

---

