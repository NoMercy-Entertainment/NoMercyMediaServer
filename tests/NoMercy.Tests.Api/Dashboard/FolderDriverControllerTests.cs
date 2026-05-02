using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Dashboard;

[Trait("Category", "FolderDriver")]
public class FolderDriverControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly HttpClient _authed;
    private readonly HttpClient _unauthed;

    // Use the folder seeded by NoMercyApiFactory
    private static readonly Ulid MovieFolderId = NoMercyApiFactory.MovieFolderId;

    public FolderDriverControllerTests(NoMercyApiFactory factory)
    {
        _authed = factory.CreateClient().AsAuthenticated();
        _unauthed = factory.CreateClient().AsUnauthenticated();
    }

    // =========================================================================
    // GET /drivers — metadata list
    // =========================================================================

    [Fact]
    public async Task GetDriverTypes_ReturnsAllFiveEntries()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/folders/drivers");
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
    public async Task GetDriverTypes_LocalIsAvailable_S3AndR2AreNot()
    {
        HttpResponseMessage response = await _authed.GetAsync("/api/v1/dashboard/folders/drivers");
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
    public async Task GetDriverTypes_Unauthenticated_Returns401Or403()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            "/api/v1/dashboard/folders/drivers"
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // GET /{id}/driver — read current state
    // =========================================================================

    [Fact]
    public async Task GetBackend_SeededFolder_ReturnsLocalDefault()
    {
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver"
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        JsonElement root = json.RootElement;

        Assert.Equal("local", root.GetProperty("driver_type").GetString());
    }

    [Fact]
    public async Task GetBackend_UnknownFolder_Returns404()
    {
        Ulid unknownId = Ulid.NewUlid();
        HttpResponseMessage response = await _authed.GetAsync(
            $"/api/v1/dashboard/folders/{unknownId}/driver"
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBackend_Unauthenticated_Returns401Or403()
    {
        HttpResponseMessage response = await _unauthed.GetAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver"
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }

    // =========================================================================
    // PUT /{id}/driver — update
    // =========================================================================

    [Fact]
    public async Task PutBackend_ValidLocalConfig_Returns200AndPersists()
    {
        object payload = new { driver_type = "local", driver_config = (object?)null };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );

        JsonDocument json = JsonDocument.Parse(body);
        Assert.Equal("local", json.RootElement.GetProperty("driver_type").GetString());

        // Verify persisted in DB
        await using MediaContext ctx = new();
        Folder? folder = await ctx
            .Folders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == MovieFolderId);

        Assert.NotNull(folder);
        Assert.Equal("local", folder!.Driver?.Type);
    }

    [Fact]
    public async Task PutBackend_LocalWithRootPath_Returns200()
    {
        object payload = new
        {
            driver_type = "local",
            driver_config = new { rootPath = "/mnt/media" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            payload
        );
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200, got {(int)response.StatusCode}: {body}"
        );
    }

    [Fact]
    public async Task PutDriver_InvalidDriverType_Returns400()
    {
        object payload = new { driver_type = "ftp" };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_SmbMissingMountPath_Returns400()
    {
        object payload = new
        {
            driver_type = "smb",
            driver_config = new { someOtherField = "value" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_NfsMissingMountPath_Returns400()
    {
        object payload = new { driver_type = "nfs", driver_config = (object?)null };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_S3MissingBucket_Returns400()
    {
        object payload = new { driver_type = "s3", driver_config = new { region = "us-east-1" } };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_S3ValidConfig_Returns200WithWarning()
    {
        object payload = new
        {
            driver_type = "s3",
            driver_config = new { bucket = "my-bucket", region = "us-east-1" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
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
        Assert.True(warnings.GetArrayLength() > 0, "Expected at least one warning for s3 driver");

        string? warning = warnings[0].GetString();
        Assert.NotNull(warning);
        Assert.Contains("s3", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not yet implemented", warning, StringComparison.OrdinalIgnoreCase);

        // Restore to local so other tests aren't affected
        await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            new { driver_type = "local", driver_config = (object?)null }
        );
    }

    [Fact]
    public async Task PutBackend_R2ValidConfig_Returns200WithWarning()
    {
        object payload = new
        {
            driver_type = "r2",
            driver_config = new { bucket = "my-r2-bucket", region = "auto" },
        };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
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
        Assert.True(warnings.GetArrayLength() > 0, "Expected at least one warning for r2 driver");

        // Restore to local so other tests aren't affected
        await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            new { driver_type = "local", driver_config = (object?)null }
        );
    }

    [Fact]
    public async Task PutBackend_UnknownFolder_Returns404()
    {
        Ulid unknownId = Ulid.NewUlid();
        object payload = new { driver_type = "local" };

        HttpResponseMessage response = await _authed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{unknownId}/driver",
            payload
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutBackend_Unauthenticated_Returns401Or403()
    {
        object payload = new { driver_type = "local" };

        HttpResponseMessage response = await _unauthed.PutAsJsonAsync(
            $"/api/v1/dashboard/folders/{MovieFolderId}/driver",
            payload
        );

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403, got {(int)response.StatusCode}"
        );
    }
}
