// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Security;
using NoMercy.Service;

namespace NoMercy.Tests.Service;

/// <summary>
/// <see cref="StartupOptions.ApplyEnvironmentVariables"/> and
/// <see cref="StartupOptions.ApplySettings"/> mutate a fistful of process-wide
/// statics (<see cref="Config"/>, <see cref="ExternalServicesConfig"/>,
/// <see cref="RuntimeServerSettings"/>,
/// <see cref="StartupOptions.OverrideInternalIp"/>/<see cref="StartupOptions.OverrideExternalIp"/>)
/// exactly once at boot — these tests exercise the real mutation logic (a CLI
/// value always wins over its NOMERCY_* env var; an env var only fills a gap)
/// against real <see cref="Environment.SetEnvironmentVariable"/> calls, and
/// restore every touched static afterward so this test class cannot leak state
/// into any other test running later in the same process.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StartupOptionsTests : IDisposable
{
    private static readonly string[] EnvVarNames =
    [
        "NOMERCY_DEV",
        "NOMERCY_LOG_LEVEL",
        "NOMERCY_INTERNAL_PORT",
        "NOMERCY_EXTERNAL_PORT",
        "NOMERCY_INTERNAL_IP",
        "NOMERCY_EXTERNAL_IP",
        "NOMERCY_PIPE_NAME",
    ];

    private static readonly Lock InitLock = new();
    private static bool _dbInitialized;

    private readonly Dictionary<string, string?> _originalEnvVars = new();
    private readonly bool _originalIsDev = Config.IsDev;
    private readonly string _originalAuthBaseUrl = ExternalServicesConfig.Current.AuthBaseUrl;
    private readonly string _originalAppBaseUrl = ExternalServicesConfig.Current.AppBaseUrl;
    private readonly string _originalApiBaseUrl = ExternalServicesConfig.Current.ApiBaseUrl;
    private readonly string _originalApiServerBaseUrl = ExternalServicesConfig
        .Current
        .ApiServerBaseUrl;
    private readonly string _originalPipeName = Config.ManagementPipeName;
    private readonly int _originalInternalPort = RuntimeServerSettings.Current.InternalServerPort;
    private readonly int _originalExternalPort = RuntimeServerSettings.Current.ExternalServerPort;

    public StartupOptionsTests()
    {
        EnsureAppDatabase();

        foreach (string name in EnvVarNames)
        {
            _originalEnvVars[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    public void Dispose()
    {
        foreach (string name in EnvVarNames)
            Environment.SetEnvironmentVariable(name, _originalEnvVars[name]);

        Config.IsDev = _originalIsDev;
        Config.ManagementPipeName = _originalPipeName;
        ExternalServicesConfig.Current.AuthBaseUrl = _originalAuthBaseUrl;
        ExternalServicesConfig.Current.AppBaseUrl = _originalAppBaseUrl;
        ExternalServicesConfig.Current.ApiBaseUrl = _originalApiBaseUrl;
        ExternalServicesConfig.Current.ApiServerBaseUrl = _originalApiServerBaseUrl;
        RuntimeServerSettings.Current.InternalServerPort = _originalInternalPort;
        RuntimeServerSettings.Current.ExternalServerPort = _originalExternalPort;
        SetOverrideIp("OverrideInternalIp", null);
        SetOverrideIp("OverrideExternalIp", null);
    }

    private static void EnsureAppDatabase()
    {
        lock (InitLock)
        {
            if (_dbInitialized)
                return;

            foreach (string path in AppFiles.AllPaths())
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

            ServiceCollection tokenServices = new();
            tokenServices
                .AddDataProtection()
                .PersistKeysToFileSystem(new(AppFiles.DataProtectionKeysDir))
                .SetApplicationName("NoMercyMediaServer");
            ServiceProvider tokenProvider = tokenServices.BuildServiceProvider();
            TokenStore.Initialize(tokenProvider);

            using AppDbContext appContext = new();
            appContext.Database.EnsureCreated();

            _dbInitialized = true;
        }
    }

    // OverrideInternalIp/OverrideExternalIp have a private setter (only
    // StartupOptions.ApplySettings itself may set them) — reflection is the
    // only way to reset this leaked-by-design static back to null so a value
    // set here cannot bleed into another test.
    private static void SetOverrideIp(string propertyName, string? value)
    {
        PropertyInfo property = typeof(StartupOptions).GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static
        )!;
        property.SetValue(null, value);
    }

    [Theory]
    [InlineData(["1", true])]
    [InlineData(["true", true])]
    [InlineData(["True", true])]
    [InlineData(["TRUE", true])]
    [InlineData(["0", false])]
    [InlineData(["false", false])]
    [InlineData(["yes", false])]
    [InlineData([null, false])]
    public void ApplySettings_DevelopmentFromEnvVar_ParsesTruthyValuesOnly(
        string? envValue,
        bool expected
    )
    {
        Environment.SetEnvironmentVariable("NOMERCY_DEV", envValue);
        StartupOptions options = new() { Development = false };

        options.ApplySettings();

        Assert.Equal(expected, options.Development);
        Assert.Equal(expected, Config.IsDev);
    }

    [Fact]
    public void ApplySettings_DevelopmentAlreadyTrueViaCli_IgnoresEnvVar()
    {
        Environment.SetEnvironmentVariable("NOMERCY_DEV", "false");
        StartupOptions options = new() { Development = true };

        options.ApplySettings();

        Assert.True(options.Development);
    }

    [Fact]
    public void ApplySettings_Development_SetsDevServiceUrls()
    {
        StartupOptions options = new() { Development = true };

        options.ApplySettings();

        Assert.True(Config.IsDev);
        Assert.Equal("https://app-dev.nomercy.tv/", ExternalServicesConfig.Current.AppBaseUrl);
        Assert.Equal("https://api-dev.nomercy.tv/", ExternalServicesConfig.Current.ApiBaseUrl);
        Assert.Equal(
            "https://api-dev.nomercy.tv/v1/server/",
            ExternalServicesConfig.Current.ApiServerBaseUrl
        );
        Assert.Equal(
            "https://auth-dev.nomercy.tv/realms/NoMercyTV/",
            ExternalServicesConfig.Current.AuthBaseUrl
        );
    }

    [Fact]
    public void ApplySettings_NotDevelopment_LeavesServiceUrlsUnchanged()
    {
        string beforeAuthUrl = ExternalServicesConfig.Current.AuthBaseUrl;
        StartupOptions options = new() { Development = false };

        options.ApplySettings();

        // No "reset to production" branch exists — ApplySettings only ever
        // pushes dev URLs in, never pulls them back out.
        Assert.Equal(beforeAuthUrl, ExternalServicesConfig.Current.AuthBaseUrl);
    }

    [Fact]
    public void ApplySettings_PipeNameFromCli_SetsManagementPipeName()
    {
        StartupOptions options = new() { PipeName = "nomercy-test-pipe" };

        options.ApplySettings();

        Assert.Equal("nomercy-test-pipe", Config.ManagementPipeName);
    }

    [Fact]
    public void ApplySettings_PipeNameFromEnvVar_UsedWhenCliOmitted()
    {
        Environment.SetEnvironmentVariable("NOMERCY_PIPE_NAME", "env-pipe");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal("env-pipe", Config.ManagementPipeName);
    }

    [Fact]
    public void ApplySettings_NoPipeNameProvided_LeavesManagementPipeNameUnchanged()
    {
        Config.ManagementPipeName = "unchanged-pipe";
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal("unchanged-pipe", Config.ManagementPipeName);
    }

    [Fact]
    public void ApplySettings_InternalIpFromCli_SetsOverrideInternalIp()
    {
        StartupOptions options = new() { InternalIp = "192.168.1.50" };

        options.ApplySettings();

        Assert.Equal("192.168.1.50", StartupOptions.OverrideInternalIp);
    }

    [Fact]
    public void ApplySettings_ExternalIpFromEnvVar_UsedWhenCliOmitted()
    {
        Environment.SetEnvironmentVariable("NOMERCY_EXTERNAL_IP", "203.0.113.7");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal("203.0.113.7", StartupOptions.OverrideExternalIp);
    }

    [Fact]
    public void ApplySettings_NoIpProvided_OverridesStayNull()
    {
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Null(StartupOptions.OverrideInternalIp);
        Assert.Null(StartupOptions.OverrideExternalIp);
    }

    [Fact]
    public void ApplySettings_InternalPortFromCli_UpdatesRuntimeServerSettings()
    {
        StartupOptions options = new() { InternalPort = 8001 };

        options.ApplySettings();

        Assert.Equal(8001, RuntimeServerSettings.Current.InternalServerPort);
        Assert.Equal(8001, options.InternalPort);
    }

    [Fact]
    public void ApplySettings_ExternalPortFromEnvVar_UsedWhenCliOmitted()
    {
        Environment.SetEnvironmentVariable("NOMERCY_EXTERNAL_PORT", "9002");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal(9002, RuntimeServerSettings.Current.ExternalServerPort);
        Assert.Equal(9002, options.ExternalPort);
    }

    [Fact]
    public void ApplySettings_MalformedInternalPortEnvVar_FallsBackInsteadOfThrowing()
    {
        Environment.SetEnvironmentVariable("NOMERCY_INTERNAL_PORT", "not-a-number");
        StartupOptions options = new();

        Exception? thrown = Record.Exception(() => options.ApplySettings());

        // ApplyEnvironmentVariables leaves InternalPort at 0 (TryParse failed),
        // but ResolvePort then resolves the effective port from CLI -> DB ->
        // fallback and writes the RESOLVED value back onto options.InternalPort
        // — a malformed env var degrades to the 7626 default rather than
        // crashing startup or leaving the port unset.
        Assert.Null(thrown);
        Assert.Equal(7626, options.InternalPort);
        Assert.Equal(7626, RuntimeServerSettings.Current.InternalServerPort);
    }

    [Fact]
    public void ApplySettings_UnknownLogLevel_DoesNotThrow()
    {
        StartupOptions options = new() { LogLevel = "not-a-real-level" };

        Exception? thrown = Record.Exception(() => options.ApplySettings());

        Assert.Null(thrown);
    }

    [Fact]
    public void ApplySettings_ValidLogLevel_DoesNotThrow()
    {
        StartupOptions options = new() { LogLevel = "Debug" };

        Exception? thrown = Record.Exception(() => options.ApplySettings());

        Assert.Null(thrown);
    }

    [Fact]
    public void ApplySettings_LogLevelAlreadyOverriddenByCli_IgnoresEnvVar()
    {
        Environment.SetEnvironmentVariable("NOMERCY_LOG_LEVEL", "Fatal");
        StartupOptions options = new() { LogLevel = "Warning" };

        options.ApplySettings();

        Assert.Equal("Warning", options.LogLevel);
    }

    [Fact]
    public void ApplySettings_LogLevelFromEnvVar_UsedWhenCliLeftAtDefault()
    {
        Environment.SetEnvironmentVariable("NOMERCY_LOG_LEVEL", "  Debug  ");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal("Debug", options.LogLevel);
    }
}
