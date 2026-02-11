using NoMercy.NmSystem.Information;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// MED-17: Tests verifying that Config properties load from environment variables
/// and that the hardcoded client secret has been removed.
/// </summary>
[Trait("Category", "Unit")]
public class ConfigEnvironmentVariableTests
{
    [Fact]
    public void TokenClientSecret_DefaultsToEmpty_WhenEnvVarNotSet()
    {
        // MED-17: The hardcoded secret "1lHWBazSTHfBpuIzjAI6xnNjmwUnryai" must not appear
        // as a compiled default. When NOMERCY_CLIENT_SECRET is not set, the value should be empty.
        string? envValue = Environment.GetEnvironmentVariable("NOMERCY_CLIENT_SECRET");
        if (envValue is not null)
        {
            // If env var is set in CI or developer environment, verify it's used
            Assert.Equal(envValue, Config.TokenClientSecret);
        }
        else
        {
            Assert.Equal(string.Empty, Config.TokenClientSecret);
        }
    }

    [Fact]
    public void TokenClientSecret_NoHardcodedSecret()
    {
        // MED-17: Verify the known hardcoded secret is not baked into the default value.
        // If someone sets it via env var that's fine, but it must not be the compiled default.
        string? envValue = Environment.GetEnvironmentVariable("NOMERCY_CLIENT_SECRET");
        if (envValue is null)
        {
            Assert.NotEqual("1lHWBazSTHfBpuIzjAI6xnNjmwUnryai", Config.TokenClientSecret);
        }
    }

    [Fact]
    public void AuthBaseUrl_HasDefault()
    {
        // MED-17: AuthBaseUrl should have a sensible default when env var is not set
        Assert.False(string.IsNullOrEmpty(Config.AuthBaseUrl));
    }

    [Fact]
    public void ApiBaseUrl_HasDefault()
    {
        Assert.False(string.IsNullOrEmpty(Config.ApiBaseUrl));
    }

    [Fact]
    public void AppBaseUrl_HasDefault()
    {
        Assert.False(string.IsNullOrEmpty(Config.AppBaseUrl));
    }

    [Fact]
    public void ApiServerBaseUrl_DerivedFromApiBaseUrl()
    {
        // MED-17: ApiServerBaseUrl should be derived from ApiBaseUrl
        Assert.Contains("v1/server/", Config.ApiServerBaseUrl);
    }

    [Fact]
    public void AuthBaseDevUrl_Removed()
    {
        // MED-17: The unused AuthBaseDevUrl property should no longer exist.
        // Dev URL is set directly in StartupOptions when --dev flag is used.
        System.Reflection.PropertyInfo? devUrlProp = typeof(Config).GetProperty("AuthBaseDevUrl");
        Assert.Null(devUrlProp);
    }

    [Fact]
    public void TokenClientSecret_IsSettableProperty()
    {
        // MED-17: TokenClientSecret must be a settable property (not a field)
        // so StartupOptions can override it at runtime.
        System.Reflection.PropertyInfo? prop = typeof(Config).GetProperty("TokenClientSecret");
        Assert.NotNull(prop);
        Assert.True(prop.CanWrite);
    }
}
