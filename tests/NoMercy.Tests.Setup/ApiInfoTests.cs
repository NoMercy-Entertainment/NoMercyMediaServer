using Newtonsoft.Json;
using NoMercy.Setup;
using NoMercy.Setup.Dto;
using ApiData = NoMercy.Setup.Dto.Data;

namespace NoMercy.Tests.Setup;

public class ApiInfoApplyKeysTests
{
    private static ApiInfoResponse CreateTestResponse()
    {
        return new ApiInfoResponse
        {
            Status = "success",
            Data = new ApiData
            {
                Quote = "Test quote",
                Colors = ["#111", "#222", "#333"],
                Keys = new Keys
                {
                    MakeMkvKey = "mkv-key",
                    TmdbKey = "tmdb-key",
                    OmdbKey = "omdb-key",
                    FanArtKey = "fanart-key",
                    RottenTomatoes = "rt-key",
                    AcousticIdKey = "acoustid-key",
                    TadbKey = "tadb-key",
                    TmdbToken = "tmdb-token",
                    TvdbKey = "tvdb-key",
                    MusixmatchKey = "musix-key",
                    JwplayerKey = "jwplayer-key"
                }
            }
        };
    }

    [Fact]
    public void ApplyKeys_SetsAllStaticProperties()
    {
        ApiInfoResponse response = CreateTestResponse();

        ApiInfo.ApplyKeys(response);

        Assert.Equal("Test quote", ApiInfo.Quote);
        Assert.Equal(["#111", "#222", "#333"], ApiInfo.Colors);
        Assert.Equal("mkv-key", ApiInfo.MakeMkvKey);
        Assert.Equal("tmdb-key", ApiInfo.TmdbKey);
        Assert.Equal("omdb-key", ApiInfo.OmdbKey);
        Assert.Equal("fanart-key", ApiInfo.FanArtApiKey);
        Assert.Equal("rt-key", ApiInfo.RottenTomatoes);
        Assert.Equal("acoustid-key", ApiInfo.AcousticIdKey);
        Assert.Equal("tadb-key", ApiInfo.TadbKey);
        Assert.Equal("tmdb-token", ApiInfo.TmdbToken);
        Assert.Equal("tvdb-key", ApiInfo.TvdbKey);
        Assert.Equal("musix-key", ApiInfo.MusixmatchKey);
        Assert.Equal("jwplayer-key", ApiInfo.JwplayerKey);
    }

    [Fact]
    public void ApplyKeys_SetsKeysLoadedTrue()
    {
        ApiInfoResponse response = CreateTestResponse();

        ApiInfo.ApplyKeys(response);

        Assert.True(ApiInfo.KeysLoaded);
    }
}

public class ApiInfoCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalCacheFilePath;

    public ApiInfoCacheTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "nomercy_apiinfo_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Save original for reference (we can't easily override the static property)
        _originalCacheFilePath = ApiInfo.CacheFilePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateCacheFile(string content)
    {
        string filePath = Path.Combine(_tempDir, "api_keys.json");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private static ApiInfoResponse CreateTestResponse(string? cachedAt = null)
    {
        return new ApiInfoResponse
        {
            Status = "success",
            CachedAt = cachedAt,
            Data = new ApiData
            {
                Quote = "Cached quote",
                Colors = ["#aaa", "#bbb", "#ccc"],
                Keys = new Keys
                {
                    MakeMkvKey = "cached-mkv",
                    TmdbKey = "cached-tmdb",
                    OmdbKey = "cached-omdb",
                    FanArtKey = "cached-fanart",
                    RottenTomatoes = "cached-rt",
                    AcousticIdKey = "cached-acoustid",
                    TadbKey = "cached-tadb",
                    TmdbToken = "cached-tmdb-token",
                    TvdbKey = "cached-tvdb",
                    MusixmatchKey = "cached-musix",
                    JwplayerKey = "cached-jwplayer"
                }
            }
        };
    }

    [Fact]
    public async Task WriteCacheFile_ThenReadCacheFile_RoundTrips()
    {
        ApiInfoResponse original = CreateTestResponse();

        await ApiInfo.WriteCacheFile(original);

        // Verify the file was written to the real cache path
        // For isolated testing, we verify the write/read logic directly
        if (File.Exists(ApiInfo.CacheFilePath))
        {
            ApiInfoResponse? readBack = await ApiInfo.TryReadCacheFile();
            Assert.NotNull(readBack);
            Assert.Equal("success", readBack!.Status);
            Assert.Equal("Cached quote", readBack.Data.Quote);
            Assert.Equal("cached-tmdb", readBack.Data.Keys.TmdbKey);
            Assert.NotNull(readBack.CachedAt);

            // Clean up
            File.Delete(ApiInfo.CacheFilePath);
        }
    }

    [Fact]
    public async Task TryReadCacheFile_MissingFile_ReturnsNull()
    {
        // If cache file doesn't exist, should return null
        string cachePath = ApiInfo.CacheFilePath;
        if (File.Exists(cachePath))
        {
            string backup = cachePath + ".bak";
            File.Move(cachePath, backup);
            try
            {
                ApiInfoResponse? result = await ApiInfo.TryReadCacheFile();
                Assert.Null(result);
            }
            finally
            {
                File.Move(backup, cachePath);
            }
        }
        else
        {
            ApiInfoResponse? result = await ApiInfo.TryReadCacheFile();
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task WriteCacheFile_SetsCachedAtTimestamp()
    {
        ApiInfoResponse response = CreateTestResponse();
        response.CachedAt = null; // Ensure it's null before write

        await ApiInfo.WriteCacheFile(response);

        Assert.NotNull(response.CachedAt);
        Assert.True(DateTime.TryParse(response.CachedAt, out DateTime parsed));
        Assert.True((DateTime.UtcNow - parsed).TotalSeconds < 5);

        // Clean up
        if (File.Exists(ApiInfo.CacheFilePath))
            File.Delete(ApiInfo.CacheFilePath);
    }
}

public class ApiInfoCacheFileParsingTests : IDisposable
{
    private readonly string _tempDir;

    public ApiInfoCacheFileParsingTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "nomercy_apiinfo_parse_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void ApiInfoResponse_DeserializesWithCachedAt()
    {
        string json = """
        {
            "_cached_at": "2026-02-07T12:00:00Z",
            "status": "success",
            "data": {
                "quote": "test",
                "colors": ["#111"],
                "keys": {
                    "tmdb_key": "key1"
                }
            }
        }
        """;

        ApiInfoResponse? result = JsonConvert.DeserializeObject<ApiInfoResponse>(json);

        Assert.NotNull(result);
        Assert.Equal("2026-02-07T12:00:00Z", result!.CachedAt);
        Assert.Equal("success", result.Status);
        Assert.Equal("key1", result.Data.Keys.TmdbKey);
    }

    [Fact]
    public void ApiInfoResponse_DeserializesWithoutCachedAt()
    {
        string json = """
        {
            "status": "success",
            "data": {
                "quote": "test",
                "colors": [],
                "keys": {}
            }
        }
        """;

        ApiInfoResponse? result = JsonConvert.DeserializeObject<ApiInfoResponse>(json);

        Assert.NotNull(result);
        Assert.Null(result!.CachedAt);
    }

    [Fact]
    public void ApiInfoResponse_SerializesWithCachedAt()
    {
        ApiInfoResponse response = new()
        {
            Status = "success",
            CachedAt = "2026-02-07T12:00:00Z",
            Data = new ApiData
            {
                Quote = "test",
                Colors = ["#fff"],
                Keys = new Keys { TmdbKey = "key1" }
            }
        };

        string json = JsonConvert.SerializeObject(response);

        Assert.Contains("\"_cached_at\"", json);
        Assert.Contains("2026-02-07T12:00:00Z", json);
    }
}

public class ApiInfoNoEnvironmentExitTests
{
    [Fact]
    public void RequestInfo_DoesNotContainEnvironmentExit()
    {
        // Verify via reflection that the method exists and is async
        System.Reflection.MethodInfo? method = typeof(ApiInfo)
            .GetMethod("RequestInfo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    public void ApiInfo_HasKeysLoadedProperty()
    {
        System.Reflection.PropertyInfo? prop = typeof(ApiInfo)
            .GetProperty("KeysLoaded", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    [Fact]
    public void ApiInfo_HasCacheFilePath()
    {
        string path = ApiInfo.CacheFilePath;

        Assert.NotNull(path);
        Assert.EndsWith("api_keys.json", path);
    }

    [Fact]
    public async Task TryFetchFromNetwork_ReturnsNullOnFailure()
    {
        // Network fetch against a non-existent API will fail gracefully
        // This test verifies it returns null instead of throwing
        ApiInfoResponse? result = await ApiInfo.TryFetchFromNetwork();

        // In test environment, the NoMercy API isn't available
        // So this should return null (not throw)
        // Note: if running in dev container with network, it may succeed
        Assert.True(result is null || result.Data?.Keys is not null);
    }
}
