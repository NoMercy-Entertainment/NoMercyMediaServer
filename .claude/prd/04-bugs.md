## 4. Bugs

> Items that produce WRONG results: wrong types, inverted logic, data corruption, missing return values.

### CRIT-09: Missing Job Retry Return Value
- **File**: `src/NoMercy.Queue/JobQueue.cs:73-76`
- **Problem**: Recursive `ReserveJob()` call doesn't return the result; function always returns `null`
- **Impact**: Jobs silently fail to be reserved; queue stalls
- **Fix**: Add `return` before recursive call: `return ReserveJob(name, currentJobId, attempt + 1);`
- **Tests Required**:
  - [ ] Unit test: ReserveJob with retry returns job after database lock resolves
  - [ ] Unit test: ReserveJob exceeding max attempts returns null

### CRIT-13: DbContext Registered as Both Scoped and Transient
- **File**: `src/NoMercy.Server/AppConfig/ServiceConfiguration.cs:113-124`
- **Problem**: `AddDbContext<T>()` registers as Scoped, then `AddTransient<T>()` shadows it
- **Impact**: Entity tracking breaks, multiple change trackers per request, SaveChanges misses changes
- **Fix**: Remove `AddTransient<QueueContext>()` and `AddTransient<MediaContext>()` lines
- **Tests Required**:
  - [ ] Integration test: SaveChanges in service layer persists all entity changes
  - [ ] Unit test: DI resolves single DbContext instance per scope

### HIGH-15: Image Controller Logic Bug
- **File**: `src/NoMercy.Api/Controllers/File/ImageController.cs:45`
- **Problem**: `... | true` (bitwise OR with true) makes condition always true
- **Impact**: **Every image request returns the original file without any resizing, caching, or quality adjustment**. The entire image processing pipeline is completely bypassed.

  **LIKELY CAUSE**: Debug bypass accidentally left in. The `| true` forces `emptyArguments = true`.

  ```csharp
  // Current (broken):
  bool emptyArguments = (request.Width is null
      && request.Type is null
      && request.Quality is 100) | true;  // ALWAYS TRUE

  // Fixed:
  bool emptyArguments = request.Width is null
      && request.Type is null
      && request.Quality is 100;
  ```

  **Note**: After removing `| true`, verify that the image processing pipeline (`Images.ResizeMagickNet()`) actually works, since it may have been untested while bypassed.

- **Fix**: Remove `| true`; test that image processing pipeline works correctly
- **Tests Required**:
  - [ ] Unit test: Image resize applies when width/quality params provided
  - [ ] Unit test: Unmodified images served without processing when no params given
  - [ ] Integration test: Processed images are cached to TempImagesPath
  - [ ] Integration test: SVG files bypass processing (separate condition)

### PROV-CRIT-03: TvdbBaseClient — .Result on SendAsync
- **File**: `src/NoMercy.Providers/TVDB/Client/TvdbBaseClient.cs:107-109`
- **Problem**: `await client.SendAsync(httpRequestMessage).Result.Content.ReadAsStringAsync()` — mixes `.Result` (blocking) with `await`.
- **Fix**: `await (await client.SendAsync(httpRequestMessage)).Content.ReadAsStringAsync()`

### PROV-CRIT-04: FanArt — Inverted Client-Key Condition
- **File**: `src/NoMercy.Providers/FanArt/Client/FanArtBaseClient.cs:28-31,44-47`
- **Problem**: `if (string.IsNullOrEmpty(ApiInfo.FanArtClientKey))` adds the header when the key IS empty, skips it when populated. Logic is backwards.
- **Fix**: Change to `if (!string.IsNullOrEmpty(ApiInfo.FanArtClientKey))`

### PROV-H06: AcoustId — Dead Code (Contradictory While Loop)
- **File**: `src/NoMercy.Providers/AcoustId/Client/AcoustIdBaseClient.cs:79-81`
- **Fix**: Remove or fix the dead code path

### PROV-H10: FanArt Image — Stream Consumed Then Reused
- **File**: `src/NoMercy.Providers/FanArt/Client/FanArtImageClient.cs:47,52,54`
- **Problem**: Stream consumed then reused (corrupt images)
- **Fix**: Read stream once, buffer if needed for multiple consumers

### PROV-H12: CoverArt Image — Stream Consumed Then Reused
- **File**: `src/NoMercy.Providers/CoverArt/Client/CoverArtCoverArtClient.cs:64,69,71`
- **Fix**: Read stream once, buffer if needed for multiple consumers

### PROV-H16: NoMercy Image — Response Content Read 3 Times
- **File**: `src/NoMercy.Providers/Other/NoMercyImageClient.cs:43,46,48`
- **Fix**: Read content once and reuse the buffered result

### PMOD-CRIT-01: MusixMatch AlbumName Typed as `long`
- **File**: `src/NoMercy.Providers/MusixMatch/Models/MusixMatchMusixMatchTrack.cs:41`
- **Problem**: `[JsonProperty("album_name")] public long AlbumName { get; set; }` — Album names are strings, not numbers.
- **Fix**: Change to `public string? AlbumName { get; set; }`

### PMOD-CRIT-02: TVDB Properties Missing Setters
- **File**: `src/NoMercy.Providers/TVDB/Models/TvdbAwardsResponse.cs:34` — `ForSeries { get; }` (no `set;`)
- **File**: `src/NoMercy.Providers/TVDB/Models/TvdbCharacterResponse.cs:45` — `Tag { get; }` (no `set;`)
- **Fix**: Add `set;` to both properties.

### PMOD-CRIT-03: TMDB `video` Field Typed as `string?`
- **Files**: `TmdbMovie.cs`, `TmdbCollectionPart.cs`, `TmdbShowOrMovie.cs`
- **Problem**: TMDB API returns `video` as a boolean. Three models type it as `string?`.
- **Fix**: Change all to `bool?`

### DBMOD-CRIT-01: Track.MetadataId Type Mismatch
- **File**: `src/NoMercy.Database/Models/Track.cs` — `MetadataId` is `int?`
- **Problem**: `Metadata.Id` is `Ulid`, but `Track.MetadataId` is declared as `int?`. FK relationship will fail at runtime.
- **Fix**: Change to `Ulid? MetadataId`

### DBMOD-CRIT-02: Library.cs JsonProperty Names ALL SHIFTED
- **File**: `src/NoMercy.Database/Models/Library.cs`
- **Problem**: The `[JsonProperty]` attribute values are shifted — each property's JSON name maps to the WRONG field.
- **Fix**: Realign all `[JsonProperty]` values to match their corresponding properties.

### DBMOD-CRIT-03: QueueJob.Payload Limited to 256 Characters
- **File**: Global convention in `MediaContext.ConfigureConventions`
- **Problem**: `configurationBuilder.Properties<string>().HaveMaxLength(256)` applies to ALL string properties. `QueueJob.Payload` stores serialized job data — 256 chars is far too small.
- **Fix**: Add explicit `[MaxLength(4096)]` on `QueueJob.Payload` (and similar for Overview/Biography fields). Regenerate migrations.

### DBMOD-CRIT-04: Cast.cs Initializes Nullable Navigation to new()
- **File**: `src/NoMercy.Database/Models/Cast.cs`
- **Problem**: Nullable navigation properties initialized to `new()` — null checks will always be true even when no related entity exists.
- **Fix**: Initialize to `null` or remove initializer.

### DBMOD-H01: UserData.TvId Wrong JsonProperty
- **File**: `src/NoMercy.Database/Models/UserData.cs`
- **Problem**: `TvId` has `[JsonProperty("episode_id")]` — wrong JSON mapping
- **Fix**: Change to `[JsonProperty("tv_id")]`

### DBMOD-H02: Network.cs Duplicate JsonProperty
- **File**: `src/NoMercy.Database/Models/Network.cs`
- **Problem**: Duplicate `[JsonProperty("id")]` on collection navigation — serialization conflict

### DateTime Inconsistency (16.3)

| Source | Where Used | Timezone |
|--------|-----------|----------|
| `CURRENT_TIMESTAMP` (SQLite) | `CreatedAt` defaults | **UTC** |
| `DateTime.Now` | `EntityBaseUpdatedAtInterceptor.cs:23` | **Local time** |
| `DateTime.UtcNow` | `QueueJob.CreatedAt`, `FailedJob.FailedAt` | **UTC** |

**Fix**: Standardize to `DateTime.UtcNow` everywhere. Update the interceptor.

### SYS-H14: macOS Cloudflared Architectures SWAPPED
- **File**: `src/NoMercy.NmSystem/Binaries.cs:290-299`
- **Problem**: Arm64 downloads amd64, X64 downloads arm64
- **Fix**: Swap the architecture download URLs

### Auth Inverted Expiration Check (from Server Setup — Section 14)

```csharp
// Auth.cs:58
bool expired = NotBefore == null && expiresInDays >= 0;
// This marks VALID tokens as "expired" — inverted logic!
```

- **Fix**: `bool expired = expiresInDays < 0`

---

