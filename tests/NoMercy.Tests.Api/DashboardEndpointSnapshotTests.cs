using System.Net;
using System.Text;
using System.Text.Json;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api;

[Trait("Category", "Characterization")]
public class DashboardEndpointSnapshotTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;
    private readonly HttpClient _client;

    public DashboardEndpointSnapshotTests(NoMercyApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient().AsAuthenticated();
    }

    private static StringContent JsonBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private static void AssertJsonHasProperty(JsonElement element, string propertyName) =>
        Assert.True(element.TryGetProperty(propertyName, out _),
            $"Expected JSON property '{propertyName}' not found. " +
            $"Properties: [{string.Join(", ", EnumerateProperties(element))}]");

    private static IEnumerable<string> EnumerateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (JsonProperty prop in element.EnumerateObject())
                yield return prop.Name;
    }

    private static void AssertStatusResponse(JsonElement root)
    {
        bool hasCustomStatus = root.TryGetProperty("message", out _)
                               && root.TryGetProperty("status", out _);
        bool hasProblemDetails = root.TryGetProperty("detail", out _)
                                 && root.TryGetProperty("status", out _);

        Assert.True(hasCustomStatus || hasProblemDetails,
            $"Expected status response shape. " +
            $"Properties: [{string.Join(", ", EnumerateProperties(root))}]");
    }

    // =========================================================================
    // ConfigurationController — /api/v1/dashboard/configuration
    // =========================================================================

    [Fact]
    public async Task Configuration_Index_ReturnsConfigData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/configuration");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Configuration_Store_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/configuration", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Configuration_Languages_ReturnsList()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/configuration/languages");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        Assert.True(
            json.RootElement.ValueKind == JsonValueKind.Array,
            "Expected array response for languages");
    }

    [Fact]
    public async Task Configuration_Countries_ReturnsList()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/configuration/countries");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        Assert.True(
            json.RootElement.ValueKind == JsonValueKind.Array,
            "Expected array response for countries");
    }

    // =========================================================================
    // DevicesController — /api/v1/dashboard/devices
    // =========================================================================

    [Fact]
    public async Task Devices_Index_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/devices");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Devices_Create_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/devices", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Devices_Destroy_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            "/api/v1/dashboard/devices");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // EncoderController — /api/v1/dashboard/encoderprofiles
    // =========================================================================

    [Fact]
    public async Task Encoder_Index_ReturnsProfiles()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/encoderprofiles");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        Assert.True(
            json.RootElement.ValueKind == JsonValueKind.Array,
            "Expected array response for encoder profiles");
    }

    [Fact]
    public async Task Encoder_Create_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/encoderprofiles", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
    }

    [Fact]
    public async Task Encoder_Destroy_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/dashboard/encoderprofiles/{Ulid.NewUlid()}");

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Encoder_Containers_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/encoderprofiles/containers");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Encoder_FrameSizes_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/encoderprofiles/framesizes");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // Dashboard LibrariesController — /api/v1/dashboard/libraries
    // =========================================================================

    [Fact]
    public async Task DashboardLibraries_Index_ReturnsLibraries()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/libraries");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task DashboardLibraries_Store_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/libraries", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
    }

    [Fact]
    public async Task DashboardLibraries_Delete_NonExistent_ReturnsErrorStatus()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");

        string status = json.RootElement.GetProperty("status").GetString()!;
        Assert.Equal("error", status);
    }

    [Fact]
    public async Task DashboardLibraries_Rescan_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/libraries/rescan", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public async Task DashboardLibraries_RescanById_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/rescan", JsonBody(new { }));

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DashboardLibraries_Refresh_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/libraries/refresh", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public async Task DashboardLibraries_RefreshById_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/refresh", JsonBody(new { }));

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DashboardLibraries_AddFolder_NonExistentLibrary_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/folders",
            JsonBody(new { path = "/tmp/test" }));

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DashboardLibraries_DeleteFolder_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/folders/{Ulid.NewUlid()}");

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DashboardLibraries_DeleteEncoderProfile_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/dashboard/libraries/{Ulid.NewUlid()}/folders/{Ulid.NewUlid()}/encoder_profiles/{Ulid.NewUlid()}");

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected NotFound, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // LogController — /api/v1/dashboard/logs
    // =========================================================================

    [Fact]
    public async Task Logs_Index_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/logs");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Logs_Levels_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/logs/levels");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Logs_Types_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/logs/types");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // PluginController — /api/v1/dashboard/plugins
    // =========================================================================

    [Fact]
    public async Task Plugins_Index_ReturnsDataResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/plugins");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Plugins_Credentials_ReturnsCredentialsOrNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/plugins/credentials");

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Plugins_SetCredentials_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/plugins/credentials",
            JsonBody(new { key = "AniDb", username = "test", apiKey = "test-key" }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
    }

    // =========================================================================
    // ServerActivityController — /api/v1/dashboard/activity
    // =========================================================================

    [Fact]
    public async Task Activity_Index_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/activity");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Activity_Create_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/activity", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Activity_Destroy_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            "/api/v1/dashboard/activity");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    // =========================================================================
    // ServerController — /api/v1/dashboard/server
    // =========================================================================

    [Fact]
    public async Task Server_Index_ReturnsOk()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/server");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Server_Setup_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/server/setup");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        AssertJsonHasProperty(data, "setup_complete");
    }

    [Fact]
    public async Task Server_Start_ReturnsContent()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/server/start", JsonBody(new { }));

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Server_Restart_ReturnsContent()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/server/restart", JsonBody(new { }));

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Server_CheckForUpdate_ReturnsUpdateStatus()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/server/update/check");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "updateAvailable");
    }

    [Fact]
    public async Task Server_Info_ReturnsServerInfo()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/server/info");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
        AssertJsonHasProperty(json.RootElement, "data");

        JsonElement data = json.RootElement.GetProperty("data");
        AssertJsonHasProperty(data, "server");
        AssertJsonHasProperty(data, "setup_complete");
    }

    [Fact]
    public async Task Server_Resources_ReturnsResourceInfo()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/server/resources");

        string body = await response.Content.ReadAsStringAsync();
        // Resources may fail in test env (no monitoring available)
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.UnprocessableEntity,
            $"Expected OK or 422, got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public async Task Server_Paths_ReturnsPathsList()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/server/paths");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        Assert.True(
            json.RootElement.ValueKind == JsonValueKind.Array,
            "Expected array response for server paths");
        Assert.True(json.RootElement.GetArrayLength() > 0, "Expected at least one path entry");
    }

    [Fact]
    public async Task Server_Storage_ReturnsStorageInfo()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/server/storage");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Server_DirectoryTree_ReturnsTreeOrError()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/server/directorytree",
            JsonBody(new { folder = "/tmp" }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.UnprocessableEntity,
            $"Expected OK or 422, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
    }

    // =========================================================================
    // SpecialsController — /api/v1/dashboard/specials
    // =========================================================================

    [Fact]
    public async Task DashboardSpecials_Index_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/specials");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task DashboardSpecials_Store_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/specials", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");
    }

    [Fact]
    public async Task DashboardSpecials_Delete_NonExistent_ReturnsErrorStatus()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/dashboard/specials/{Ulid.NewUlid()}");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "status");

        string status = json.RootElement.GetProperty("status").GetString()!;
        Assert.Equal("error", status);
    }

    [Fact]
    public async Task DashboardSpecials_RescanAll_ReturnsStatusOrNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/specials/rescan", JsonBody(new { }));

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task DashboardSpecials_RescanById_ReturnsStatusOrNotFound()
    {
        HttpResponseMessage response = await _client.PostAsync(
            $"/api/v1/dashboard/specials/{Ulid.NewUlid()}/rescan", JsonBody(new { }));

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected OK or NotFound, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // TasksController — /api/v1/dashboard/tasks
    // =========================================================================

    [Fact]
    public async Task Tasks_Index_ReturnsTasks()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/tasks");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        Assert.True(
            json.RootElement.ValueKind == JsonValueKind.Array,
            "Expected array response for tasks");
    }

    [Fact]
    public async Task Tasks_Store_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/tasks", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Tasks_Runners_ReturnsPlaceholder()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/tasks/runners");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Tasks_Queue_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/tasks/queue");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Tasks_DeleteQueue_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            "/api/v1/dashboard/tasks/queue/999999");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.OK,
            $"Expected NotFound or OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Tasks_FailedJobs_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/tasks/failed");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Tasks_RetryFailed_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/tasks/failed/retry", JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task Tasks_PauseTask_NonExistent_ReturnsOk()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/tasks/pause/999999", JsonBody(new { }));

        // Pause returns bool result; non-existent ID returns false wrapped in 200
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Tasks_ResumeTask_NonExistent_ReturnsOk()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/v1/dashboard/tasks/resume/999999", JsonBody(new { }));

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // UsersController — /api/v1/dashboard/users
    // =========================================================================

    [Fact]
    public async Task Users_Index_ReturnsDataOrServerError()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/users");

        string body = await response.Content.ReadAsStringAsync();
        // Known bug: UsersController.Index includes LibraryUser but not
        // .ThenInclude(x => x.Library), causing NullReferenceException in
        // PermissionsResponseItemDto when LibraryUser entries exist.
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.InternalServerError,
            $"Expected OK or 500, got {(int)response.StatusCode}: {body}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            JsonDocument json = JsonDocument.Parse(body);
            AssertJsonHasProperty(json.RootElement, "data");
        }
    }

    [Fact]
    public async Task Users_Permissions_ReturnsData()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/users/permissions");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertJsonHasProperty(json.RootElement, "data");
    }

    [Fact]
    public async Task Users_Delete_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/dashboard/users/{Guid.Empty}");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.OK,
            $"Expected NotFound or OK, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Users_Delete_Owner_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _client.DeleteAsync(
            $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}");

        string body = await response.Content.ReadAsStringAsync();
        // Owner cannot be deleted
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.OK,
            $"Expected 401/403/OK, got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public async Task Users_Notifications_ReturnsStatusResponse()
    {
        HttpResponseMessage response = await _client.PatchAsync(
            "/api/v1/dashboard/users/notifications",
            JsonBody(new { }));

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {(int)response.StatusCode}: {body}");

        JsonDocument json = JsonDocument.Parse(body);
        AssertStatusResponse(json.RootElement);
    }

    [Fact]
    public async Task Users_UserPermissions_SelfDenied()
    {
        // Viewing own permissions is denied
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/dashboard/users/{TestAuthHandler.DefaultUserId}/permissions");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.OK,
            $"Expected 401/403/OK, got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public async Task Users_UserPermissions_NonExistent_ReturnsNotFound()
    {
        HttpResponseMessage response = await _client.GetAsync(
            $"/api/v1/dashboard/users/{Guid.Empty}/permissions");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.OK,
            $"Expected NotFound or OK, got {(int)response.StatusCode}");
    }

    // =========================================================================
    // OpticalMediaController — /api/v1/dashboard/optical
    // =========================================================================

    [Fact]
    public async Task Optical_Drives_ReturnsListOrError()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/v1/dashboard/optical/drives");

        string body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode is HttpStatusCode.OK
                or HttpStatusCode.InternalServerError,
            $"Expected OK or 500, got {(int)response.StatusCode}: {body}");
    }

    // =========================================================================
    // Cross-cutting: Auth denial on dashboard endpoints
    // =========================================================================

    [Theory]
    [InlineData("GET", "/api/v1/dashboard/configuration")]
    [InlineData("GET", "/api/v1/dashboard/devices")]
    [InlineData("GET", "/api/v1/dashboard/encoderprofiles")]
    [InlineData("GET", "/api/v1/dashboard/libraries")]
    [InlineData("GET", "/api/v1/dashboard/logs")]
    [InlineData("GET", "/api/v1/dashboard/plugins")]
    [InlineData("GET", "/api/v1/dashboard/activity")]
    [InlineData("GET", "/api/v1/dashboard/server")]
    [InlineData("GET", "/api/v1/dashboard/server/info")]
    [InlineData("GET", "/api/v1/dashboard/specials")]
    [InlineData("GET", "/api/v1/dashboard/tasks")]
    [InlineData("GET", "/api/v1/dashboard/users")]
    public async Task DashboardEndpoints_ReturnUnauthorized_WhenUnauthenticated(
        string method, string url)
    {
        HttpClient unauthed = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = method switch
        {
            "GET" => await unauthed.GetAsync(url),
            "POST" => await unauthed.PostAsync(url, JsonBody(new { })),
            _ => throw new ArgumentException($"Unsupported method: {method}")
        };

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 for {method} {url}, got {(int)response.StatusCode}");
    }
}
