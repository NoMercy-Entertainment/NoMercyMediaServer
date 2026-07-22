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
using NoMercy.Service.Seeds;
using Xunit;

namespace NoMercy.Tests.Service;

/// <summary>
/// <see cref="StartupOptions.ApplyEnvironmentVariables"/> and
/// <see cref="StartupOptions.ApplySettings"/> mutate a fistful of process-wide
/// statics (<see cref="Config"/>, <see cref="ExternalServicesConfig"/>,
/// <see cref="RuntimeServerSettings"/>, <see cref="DatabaseSeeder.ShouldSeedMarvel"/>,
/// <see cref="StartupOptions.OverrideInternalIp"/>/<see cref="StartupOptions.OverrideExternalIp"/>)
/// exactly once at boot — these tests exercise the real mutation logic (a CLI
/// value always wins over its NOMERCY_* env var; an env var only fills a gap)
/// against real <see cref="Environment.SetEnvironmentVariable"/> calls, and
/// restore every touched static afterward so this test class cannot leak state
/// into any other test running later in the same process.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class StartupOptionsTests : IDisposable
{
    private static readonly string[] EnvVarNames =
    [
        "NOMERCY_DEV",
        "NOMERCY_LOG_LEVEL",
        "NOMERCY_SEED",
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
    private readonly bool _originalShouldSeedMarvel = DatabaseSeeder.ShouldSeedMarvel;

    public StartupOptionsTests()
    {
        EnsureAppDatabase();

        foreach (string name in EnvVarNames)
        {
            _originalEnvVars[key: name] = Environment.GetEnvironmentVariable(variable: name);
            Environment.SetEnvironmentVariable(variable: name, value: null);
        }
    }

    public void Dispose()
    {
        foreach (string name in EnvVarNames)
            Environment.SetEnvironmentVariable(variable: name, value: _originalEnvVars[key: name]);

        Config.IsDev = _originalIsDev;
        Config.ManagementPipeName = _originalPipeName;
        ExternalServicesConfig.Current.AuthBaseUrl = _originalAuthBaseUrl;
        ExternalServicesConfig.Current.AppBaseUrl = _originalAppBaseUrl;
        ExternalServicesConfig.Current.ApiBaseUrl = _originalApiBaseUrl;
        ExternalServicesConfig.Current.ApiServerBaseUrl = _originalApiServerBaseUrl;
        RuntimeServerSettings.Current.InternalServerPort = _originalInternalPort;
        RuntimeServerSettings.Current.ExternalServerPort = _originalExternalPort;
        DatabaseSeeder.ShouldSeedMarvel = _originalShouldSeedMarvel;
        SetOverrideIp(propertyName: "OverrideInternalIp", value: null);
        SetOverrideIp(propertyName: "OverrideExternalIp", value: null);
    }

    private static void EnsureAppDatabase()
    {
        lock (InitLock)
        {
            if (_dbInitialized)
                return;

            foreach (string path in AppFiles.AllPaths())
                if (!Directory.Exists(path: path))
                    Directory.CreateDirectory(path: path);

            ServiceCollection tokenServices = new();
            tokenServices
                .AddDataProtection()
                .PersistKeysToFileSystem(directory: new(path: AppFiles.DataProtectionKeysDir))
                .SetApplicationName(applicationName: "NoMercyMediaServer");
            ServiceProvider tokenProvider = tokenServices.BuildServiceProvider();
            TokenStore.Initialize(serviceProvider: tokenProvider);

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
            name: propertyName,
            bindingAttr: BindingFlags.Public | BindingFlags.Static
        )!;
        property.SetValue(obj: null, value: value);
    }

    [Theory]
    [InlineData(data: ["1", true])]
    [InlineData(data: ["true", true])]
    [InlineData(data: ["True", true])]
    [InlineData(data: ["TRUE", true])]
    [InlineData(data: ["0", false])]
    [InlineData(data: ["false", false])]
    [InlineData(data: ["yes", false])]
    [InlineData(data: [null, false])]
    public void ApplySettings_DevelopmentFromEnvVar_ParsesTruthyValuesOnly(
        string? envValue,
        bool expected
    )
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_DEV", value: envValue);
        StartupOptions options = new() { Development = false };

        options.ApplySettings();

        Assert.Equal(expected: expected, actual: options.Development);
        Assert.Equal(expected: expected, actual: Config.IsDev);
    }

    [Fact]
    public void ApplySettings_DevelopmentAlreadyTrueViaCli_IgnoresEnvVar()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_DEV", value: "false");
        StartupOptions options = new() { Development = true };

        options.ApplySettings();

        Assert.True(condition: options.Development);
    }

    [Fact]
    public void ApplySettings_Development_SetsDevServiceUrls()
    {
        StartupOptions options = new() { Development = true };

        options.ApplySettings();

        Assert.True(condition: Config.IsDev);
        Assert.Equal(expected: "https://app-dev.nomercy.tv/", actual: ExternalServicesConfig.Current.AppBaseUrl);
        Assert.Equal(expected: "https://api-dev.nomercy.tv/", actual: ExternalServicesConfig.Current.ApiBaseUrl);
        Assert.Equal(
            expected: "https://api-dev.nomercy.tv/v1/server/",
            actual: ExternalServicesConfig.Current.ApiServerBaseUrl
        );
        Assert.Equal(
            expected: "https://auth-dev.nomercy.tv/realms/NoMercyTV/",
            actual: ExternalServicesConfig.Current.AuthBaseUrl
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
        Assert.Equal(expected: beforeAuthUrl, actual: ExternalServicesConfig.Current.AuthBaseUrl);
    }

    [Theory]
    [InlineData(data: ["1", true])]
    [InlineData(data: ["true", true])]
    [InlineData(data: ["0", false])]
    public void ApplySettings_ShouldSeedFromEnvVar_SetsDatabaseSeederFlag(
        string envValue,
        bool expected
    )
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_SEED", value: envValue);
        StartupOptions options = new() { ShouldSeed = false };

        options.ApplySettings();

        Assert.Equal(expected: expected, actual: DatabaseSeeder.ShouldSeedMarvel);
    }

    [Fact]
    public void ApplySettings_ShouldSeedAlreadyTrueViaCli_IgnoresEnvVar()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_SEED", value: "0");
        StartupOptions options = new() { ShouldSeed = true };

        options.ApplySettings();

        Assert.True(condition: DatabaseSeeder.ShouldSeedMarvel);
    }

    [Fact]
    public void ApplySettings_PipeNameFromCli_SetsManagementPipeName()
    {
        StartupOptions options = new() { PipeName = "nomercy-test-pipe" };

        options.ApplySettings();

        Assert.Equal(expected: "nomercy-test-pipe", actual: Config.ManagementPipeName);
    }

    [Fact]
    public void ApplySettings_PipeNameFromEnvVar_UsedWhenCliOmitted()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_PIPE_NAME", value: "env-pipe");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal(expected: "env-pipe", actual: Config.ManagementPipeName);
    }

    [Fact]
    public void ApplySettings_NoPipeNameProvided_LeavesManagementPipeNameUnchanged()
    {
        Config.ManagementPipeName = "unchanged-pipe";
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal(expected: "unchanged-pipe", actual: Config.ManagementPipeName);
    }

    [Fact]
    public void ApplySettings_InternalIpFromCli_SetsOverrideInternalIp()
    {
        StartupOptions options = new() { InternalIp = "192.168.1.50" };

        options.ApplySettings();

        Assert.Equal(expected: "192.168.1.50", actual: StartupOptions.OverrideInternalIp);
    }

    [Fact]
    public void ApplySettings_ExternalIpFromEnvVar_UsedWhenCliOmitted()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_EXTERNAL_IP", value: "203.0.113.7");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal(expected: "203.0.113.7", actual: StartupOptions.OverrideExternalIp);
    }

    [Fact]
    public void ApplySettings_NoIpProvided_OverridesStayNull()
    {
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Null(@object: StartupOptions.OverrideInternalIp);
        Assert.Null(@object: StartupOptions.OverrideExternalIp);
    }

    [Fact]
    public void ApplySettings_InternalPortFromCli_UpdatesRuntimeServerSettings()
    {
        StartupOptions options = new() { InternalPort = 8001 };

        options.ApplySettings();

        Assert.Equal(expected: 8001, actual: RuntimeServerSettings.Current.InternalServerPort);
        Assert.Equal(expected: 8001, actual: options.InternalPort);
    }

    [Fact]
    public void ApplySettings_ExternalPortFromEnvVar_UsedWhenCliOmitted()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_EXTERNAL_PORT", value: "9002");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal(expected: 9002, actual: RuntimeServerSettings.Current.ExternalServerPort);
        Assert.Equal(expected: 9002, actual: options.ExternalPort);
    }

    [Fact]
    public void ApplySettings_MalformedInternalPortEnvVar_FallsBackInsteadOfThrowing()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_INTERNAL_PORT", value: "not-a-number");
        StartupOptions options = new();

        Exception? thrown = Record.Exception(testCode: () => options.ApplySettings());

        // ApplyEnvironmentVariables leaves InternalPort at 0 (TryParse failed),
        // but ResolvePort then resolves the effective port from CLI -> DB ->
        // fallback and writes the RESOLVED value back onto options.InternalPort
        // — a malformed env var degrades to the 7626 default rather than
        // crashing startup or leaving the port unset.
        Assert.Null(@object: thrown);
        Assert.Equal(expected: 7626, actual: options.InternalPort);
        Assert.Equal(expected: 7626, actual: RuntimeServerSettings.Current.InternalServerPort);
    }

    [Fact]
    public void ApplySettings_UnknownLogLevel_DoesNotThrow()
    {
        StartupOptions options = new() { LogLevel = "not-a-real-level" };

        Exception? thrown = Record.Exception(testCode: () => options.ApplySettings());

        Assert.Null(@object: thrown);
    }

    [Fact]
    public void ApplySettings_ValidLogLevel_DoesNotThrow()
    {
        StartupOptions options = new() { LogLevel = "Debug" };

        Exception? thrown = Record.Exception(testCode: () => options.ApplySettings());

        Assert.Null(@object: thrown);
    }

    [Fact]
    public void ApplySettings_LogLevelAlreadyOverriddenByCli_IgnoresEnvVar()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_LOG_LEVEL", value: "Fatal");
        StartupOptions options = new() { LogLevel = "Warning" };

        options.ApplySettings();

        Assert.Equal(expected: "Warning", actual: options.LogLevel);
    }

    [Fact]
    public void ApplySettings_LogLevelFromEnvVar_UsedWhenCliLeftAtDefault()
    {
        Environment.SetEnvironmentVariable(variable: "NOMERCY_LOG_LEVEL", value: "  Debug  ");
        StartupOptions options = new();

        options.ApplySettings();

        Assert.Equal(expected: "Debug", actual: options.LogLevel);
    }
}
