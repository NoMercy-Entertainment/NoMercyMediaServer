using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using NoMercy.Database;
using NoMercy.Storage;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait("Category", "FolderBackend")]
public class FolderBackendControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    // Use the folder seeded by NoMercyApiFactory
    private static readonly Ulid MovieFolderId = NoMercyApiFactory.MovieFolderId;

    public FolderBackendControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    // =========================================================================
    // GET /backends — metadata list
    // =========================================================================

    [Fact]
    public async Task GetBackendTypes_ReturnsAllFiveEntries()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/folders/backends");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(5, root.GetArrayLength());
    }

    [Fact]
    public async Task GetBackendTypes_LocalIsAvailable_S3AndR2AreNot()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/folders/backends");
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);

        Dictionary<string, bool> availability = json
            .RootElement.EnumerateArray()
            .ToDictionary(
                e => e.GetProperty("type").GetString()!,
                e => e.GetProperty("available").GetBoolean()
            );

        Assert.True(availability["local"], "local should be available");
        Assert.True(availability["smb"], "smb should be available");
        Assert.True(availability["nfs"], "nfs should be available");
        Assert.False(availability["s3"], "s3 should not be available");
        Assert.False(availability["r2"], "r2 should not be available");
    }

    [Fact]
    public async Task GetBackendTypes_Unauthenticated_Returns401Or403()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/folders/backends"
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // GET /{id}/backend — read current state
    // =========================================================================

    [Fact]
    public async Task GetBackend_SeededFolder_ReturnsLocalDefault()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend"
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;

        Assert.Equal("local", root.GetProperty("backend_type").GetString());
    }

    [Fact]
    public async Task GetBackend_UnknownFolder_Returns404()
    {
        Ulid unknownId = Ulid.NewUlid();
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/folders/{unknownId}/backend"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBackend_Unauthenticated_Returns401Or403()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend"
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // PUT /{id}/backend — update
    // =========================================================================

    [Fact]
    public async Task PutBackend_ValidLocalConfig_Returns200AndPersists()
    {
        object payload = new { backend_type = "local", backend_config = (object?)null };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal("local", json.RootElement.GetProperty("backend_type").GetString());

        // Verify persisted in DB
        await using MediaContext ctx = new();
        NoMercy.Database.Models.Libraries.Folder? folder = await ctx
            .Folders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == MovieFolderId);

        Assert.NotNull(folder);
        Assert.Equal("local", folder!.BackendType);
    }

    [Fact]
    public async Task PutBackend_LocalWithRootPath_Returns200()
    {
        object payload = new
        {
            backend_type = "local",
            backend_config = new { rootPath = "/mnt/media" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task PutBackend_InvalidBackendType_Returns400()
    {
        object payload = new { backend_type = "ftp" };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_SmbMissingMountPath_Returns400()
    {
        object payload = new
        {
            backend_type = "smb",
            backend_config = new { someOtherField = "value" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_NfsMissingMountPath_Returns400()
    {
        object payload = new { backend_type = "nfs", backend_config = (object?)null };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_S3MissingBucket_Returns400()
    {
        object payload = new { backend_type = "s3", backend_config = new { region = "us-east-1" } };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_S3ValidConfig_Returns200WithWarning()
    {
        object payload = new
        {
            backend_type = "s3",
            backend_config = new { bucket = "my-bucket", region = "us-east-1" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        JsonElement warnings = json.RootElement.GetProperty("warnings");

        Assert.Equal(JsonValueKind.Array, warnings.ValueKind);
        Assert.True(warnings.GetArrayLength() > 0, "Expected at least one warning for s3 backend");

        string? warning = warnings[0].GetString();
        Assert.NotNull(warning);
        Assert.Contains("s3", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not yet implemented", warning, StringComparison.OrdinalIgnoreCase);

        // Restore to local so other tests aren't affected
        await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            new { backend_type = "local", backend_config = (object?)null }
        );
    }

    [Fact]
    public async Task PutBackend_R2ValidConfig_Returns200WithWarning()
    {
        object payload = new
        {
            backend_type = "r2",
            backend_config = new { bucket = "my-r2-bucket", region = "auto" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        JsonElement warnings = json.RootElement.GetProperty("warnings");

        Assert.Equal(JsonValueKind.Array, warnings.ValueKind);
        Assert.True(warnings.GetArrayLength() > 0, "Expected at least one warning for r2 backend");

        // Restore to local so other tests aren't affected
        await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            new { backend_type = "local", backend_config = (object?)null }
        );
    }

    [Fact]
    public async Task PutBackend_UnknownFolder_Returns404()
    {
        Ulid unknownId = Ulid.NewUlid();
        object payload = new { backend_type = "local" };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{unknownId}/backend",
            payload
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_Unauthenticated_Returns401Or403()
    {
        object payload = new { backend_type = "local" };

        HttpResponseMessage response = await _unauthed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/backend",
            payload
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }
}
