## 2. Testing Strategy

### Testing Philosophy
- Prefer integration tests over mocks — test real EF Core queries against SQLite
- Prefer SQL verification for database changes — assert generated SQL, not just results
- Prefer concurrency stress tests for queue and worker changes
- Prefer file-system-isolated tests for Setup/Auth flows
- Every change gets tests; no "too small to test" exceptions

### 2.1 Current Coverage

| Area | Tests | Lines | Status |
|------|-------|-------|--------|
| Queue System | ~120 | 2,650 | Well-tested |
| TMDB Provider | ~200 | 4,780 | Well-tested |
| Database Models | 2 | 110 | Minimal |
| Media Processing | 12 | 166 | Minimal |
| EncoderV2 | 0 | 0 | Empty |
| API Controllers | 0 | 0 | None |
| Repositories | 0 | 0 | None |
| SignalR Hubs | 0 | 0 | None |
| Networking | 0 | 0 | None |
| System Utilities | 0 | 0 | None |

### 2.2 Testing Requirements for Every Change

**Every performance fix MUST include:**
1. **Unit test** proving the fix works in isolation
2. **Integration test** proving the fix works with real database/services
3. **Regression test** proving existing API responses are unchanged
4. **Database query test** for any EF Core changes — verify SQL output

**Database query testing pattern:**
```csharp
[Fact]
public async Task GetTvShowDetail_GeneratesExpectedSql()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var repo = new TvShowRepository(context);

    // Act
    var result = await repo.GetTvShowDetailAsync(userId, tvId);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.Seasons);
    Assert.True(result.Seasons.Count > 0);
    // Verify no N+1 queries via query log
}
```

### 2.3 New Test Projects Required

| Project | What It Tests | Priority |
|---------|---------------|----------|
| `NoMercy.Tests.Api` | All REST controllers, auth, pagination | Critical |
| `NoMercy.Tests.Repositories` | All data access, query correctness | Critical |
| `NoMercy.Tests.Hubs` | SignalR hub functionality | High |
| `NoMercy.Tests.Encoder` | FFmpeg command building, HLS generation | High |
| `NoMercy.Tests.Integration` | End-to-end API tests with real DB | High |
| `NoMercy.Tests.Providers.MusicBrainz` | MusicBrainz client | Medium |
| `NoMercy.Tests.Providers.OpenSubtitles` | OpenSubtitles client | Medium |

### 2.4 Test Infrastructure Tasks

| Task ID | Description | Effort |
|---------|-------------|--------|
| TEST-01 | Create `NoMercy.Tests.Api` project with WebApplicationFactory | Medium |
| TEST-02 | Create shared test helpers (auth mock, DB seeding) | Medium |
| TEST-03 | Add controller tests for all Media endpoints | Large |
| TEST-04 | Add controller tests for all Music endpoints | Large |
| TEST-05 | Add controller tests for all Dashboard endpoints | Medium |
| TEST-06 | Add repository tests for all 12 repositories | Large |
| TEST-07 | Add SignalR hub tests (VideoHub, MusicHub) | Medium |
| TEST-08 | Add encoder command builder tests | Medium |
| TEST-09 | Add HLS playlist generation tests | Medium |
| TEST-10 | Add MusicBrainz provider tests | Medium |
| TEST-11 | Add OpenSubtitles provider tests | Medium |
| TEST-12 | Add integration tests with real SQLite database | Large |
| TEST-13 | Add API response snapshot tests (detect breaking changes) | Medium |
| TEST-14 | Set up CI pipeline with test + coverage enforcement | Medium |
| TEST-15 | Add load/stress tests for concurrent scenarios | Large |

---

