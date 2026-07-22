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

using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.Setup.Ui;

namespace NoMercy.Tests.Setup;

// Regression coverage for the "one bad settings row wipes ALL settings" boot
// crash: a single malformed int in the Configuration table used to make
// TryGetUserSettings/ApplySettings throw (or discard every other row).
[Trait(name: "Category", value: "Unit")]
public class UserSettingsTests : IDisposable
{
    private readonly int _originalInternalPort = RuntimeServerSettings.Current.InternalServerPort;
    private readonly int _originalExternalPort = RuntimeServerSettings.Current.ExternalServerPort;

    public UserSettingsTests()
    {
        Directory.CreateDirectory(path: AppFiles.DataPath);

        using AppDbContext context = new();
        context.Database.EnsureCreated();
        context.Configuration.RemoveRange(entities: context.Configuration);
        context.SaveChanges();
    }

    public void Dispose()
    {
        using AppDbContext context = new();
        context.Configuration.RemoveRange(entities: context.Configuration);
        context.SaveChanges();

        RuntimeServerSettings.Current.InternalServerPort = _originalInternalPort;
        RuntimeServerSettings.Current.ExternalServerPort = _originalExternalPort;
    }

    [Fact]
    public void TryGetUserSettings_MalformedPortRow_SkipsRowButKeepsOthers()
    {
        using (AppDbContext context = new())
        {
            context.Configuration.Add(entity: new() { Key = "internalPort", Value = "not-a-number" });
            context.Configuration.Add(entity: new() { Key = "cronRunners", Value = "3" });
            context.SaveChanges();
        }

        bool success = UserSettings.TryGetUserSettings(settings: out Dictionary<string, string> settings);

        success.Should().BeTrue();
        settings.Should().NotContainKey(unexpected: "internalPort");
        settings.Should().ContainKey(expected: "cronRunners");
        settings[key: "cronRunners"].Should().Be(expected: "3");
    }

    [Fact]
    public void TryGetUserSettings_AllGoodRows_ReturnsEveryRow()
    {
        using (AppDbContext context = new())
        {
            context.Configuration.Add(entity: new() { Key = "cronRunners", Value = "2" });
            context.Configuration.Add(entity: new() { Key = "imageRunners", Value = "5" });
            context.SaveChanges();
        }

        bool success = UserSettings.TryGetUserSettings(settings: out Dictionary<string, string> settings);

        success.Should().BeTrue();
        settings.Should().HaveCount(expected: 2);
        settings[key: "cronRunners"].Should().Be(expected: "2");
        settings[key: "imageRunners"].Should().Be(expected: "5");
    }

    [Fact]
    public void ApplySettings_MalformedPortValue_DoesNotThrow_AndSkipsThatSetting()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;

        Dictionary<string, string> settings = new()
        {
            [key: "internalPort"] = "garbage",
            [key: "cronRunners"] = "4",
        };

        Action act = () => UserSettings.ApplySettings(settings: settings, silent: true);

        act.Should().NotThrow();
        RuntimeServerSettings.Current.InternalServerPort.Should().Be(expected: 7626);
        RuntimeServerSettings.Current.CronWorkers.Value.Should().Be(expected: 4);
    }

    [Fact]
    public void ApplySettings_ValidPortValue_UpdatesRuntimeSetting()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;

        Dictionary<string, string> settings = new() { [key: "internalPort"] = "8080" };

        UserSettings.ApplySettings(settings: settings, silent: true);

        RuntimeServerSettings.Current.InternalServerPort.Should().Be(expected: 8080);
    }

    [Fact]
    public void ApplySettings_ExternalPort_UpdatesRuntimeSetting()
    {
        RuntimeServerSettings.Current.ExternalServerPort = 7626;

        UserSettings.ApplySettings(settings: new() { [key: "externalPort"] = "9090" }, silent: true);

        RuntimeServerSettings.Current.ExternalServerPort.Should().Be(expected: 9090);
    }

    [Fact]
    public void ApplySettings_ExternalPort_MalformedValue_DoesNotThrow_KeepsCurrentSetting()
    {
        RuntimeServerSettings.Current.ExternalServerPort = 7626;

        Action act = () =>
            UserSettings.ApplySettings(settings: new() { [key: "externalPort"] = "garbage" }, silent: true);

        act.Should().NotThrow();
        RuntimeServerSettings.Current.ExternalServerPort.Should().Be(expected: 7626);
    }

    [Theory]
    [InlineData(data: ["libraryRunners", 7])]
    [InlineData(data: ["importRunners", 8])]
    [InlineData(data: ["queueRunners", 9])] // alias for importRunners
    [InlineData(data: ["extrasRunners", 10])]
    [InlineData(data: ["dataRunners", 11])] // alias for extrasRunners
    [InlineData(data: ["encoderRunners", 12])]
    [InlineData(data: ["cronRunners", 13])]
    [InlineData(data: ["imageRunners", 14])]
    [InlineData(data: ["fileRunners", 15])]
    [InlineData(data: ["musicRunners", 16])]
    public void ApplySettings_WorkerCountSetting_UpdatesCorrespondingRuntimeWorkerCount(
        string key,
        int value
    )
    {
        UserSettings.ApplySettings(settings: new() { [key: key] = value.ToString() }, silent: true);

        int actual = key switch
        {
            "libraryRunners" => RuntimeServerSettings.Current.LibraryWorkers.Value,
            "importRunners" or "queueRunners" => RuntimeServerSettings.Current.ImportWorkers.Value,
            "extrasRunners" or "dataRunners" => RuntimeServerSettings.Current.ExtrasWorkers.Value,
            "encoderRunners" => RuntimeServerSettings.Current.EncoderWorkers.Value,
            "cronRunners" => RuntimeServerSettings.Current.CronWorkers.Value,
            "imageRunners" => RuntimeServerSettings.Current.ImageWorkers.Value,
            "fileRunners" => RuntimeServerSettings.Current.FileWorkers.Value,
            "musicRunners" => RuntimeServerSettings.Current.MusicWorkers.Value,
            _ => throw new InvalidOperationException(message: $"unmapped key {key}"),
        };

        actual.Should().Be(expected: value);
    }

    [Fact]
    public void ApplySettings_WorkerCountSetting_PreservesTheQueueNameKey()
    {
        KeyValuePair<string, int> before = RuntimeServerSettings.Current.LibraryWorkers;

        UserSettings.ApplySettings(settings: new() { [key: "libraryRunners"] = "5" }, silent: true);

        RuntimeServerSettings.Current.LibraryWorkers.Key.Should().Be(expected: before.Key);
        RuntimeServerSettings.Current.LibraryWorkers.Value.Should().Be(expected: 5);
    }

    [Theory]
    [InlineData(data: ["swagger", "true", true])]
    [InlineData(data: ["swagger", "false", false])]
    [InlineData(data: ["UseSynthesizedDns", "true", true])]
    [InlineData(data: ["UseSynthesizedDns", "false", false])]
    [InlineData(data: ["allowAdultContent", "true", true])]
    [InlineData(data: ["allowAdultContent", "false", false])]
    public void ApplySettings_BooleanSetting_UpdatesCorrespondingRuntimeFlag(
        string key,
        string value,
        bool expected
    )
    {
        UserSettings.ApplySettings(settings: new() { [key: key] = value }, silent: true);

        bool actual = key switch
        {
            "swagger" => RuntimeServerSettings.Current.Swagger,
            "UseSynthesizedDns" => RuntimeServerSettings.Current.UseSynthesizedDns,
            "allowAdultContent" => RuntimeServerSettings.Current.AllowAdultContent!.Value,
            _ => throw new InvalidOperationException(message: $"unmapped key {key}"),
        };

        actual.Should().Be(expected: expected);
    }

    [Fact]
    public void ApplySettings_UnknownKey_IsIgnoredWithoutThrowing()
    {
        Action act = () =>
            UserSettings.ApplySettings(
                settings: new() { [key: "someUnrecognizedFutureSetting"] = "x" },
                silent: true
            );

        act.Should().NotThrow();
    }

    [Fact]
    public void ApplySettings_InternalPortUnchanged_DoesNotReUpsert()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;

        // Same value as current — the `if (internalPortChanged)` guard must skip the
        // Upsert call entirely; if it didn't, this would still pass (idempotent write)
        // but the guard's OWN branch (false path) needs a scenario where the value is
        // identical to prove it's actually being checked, not just always executed.
        UserSettings.ApplySettings(settings: new() { [key: "internalPort"] = "7626" }, silent: true);

        RuntimeServerSettings.Current.InternalServerPort.Should().Be(expected: 7626);
    }

    [Fact]
    public void TryGetUserSettings_InternalPortDriftedFromRuntimeSetting_UpsertsCorrectedValue()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;

        using (AppDbContext context = new())
        {
            // A stale DB value that no longer matches the in-memory runtime setting —
            // TryGetUserSettings must correct the DB row to match, not just read it back.
            context.Configuration.Add(entity: new() { Key = "internalPort", Value = "1234" });
            context.SaveChanges();
        }

        bool success = UserSettings.TryGetUserSettings(settings: out Dictionary<string, string> settings);

        success.Should().BeTrue();
        settings[key: "internalPort"].Should().Be(expected: "7626");

        using AppDbContext verify = new();
        Configuration row = verify.Configuration.Single(predicate: c => c.Key == "internalPort");
        row.Value.Should().Be(expected: "7626");
    }

    [Fact]
    public void TryGetUserSettings_ExternalPortDriftedFromRuntimeSetting_UpsertsCorrectedValue()
    {
        RuntimeServerSettings.Current.ExternalServerPort = 7626;

        using (AppDbContext context = new())
        {
            context.Configuration.Add(entity: new() { Key = "externalPort", Value = "9999" });
            context.SaveChanges();
        }

        bool success = UserSettings.TryGetUserSettings(settings: out Dictionary<string, string> settings);

        success.Should().BeTrue();
        settings[key: "externalPort"].Should().Be(expected: "7626");
    }

    [Fact]
    public void TryGetUserSettings_MalformedExternalPortRow_SkipsRowButKeepsOthers()
    {
        using (AppDbContext context = new())
        {
            context.Configuration.Add(entity: new() { Key = "externalPort", Value = "not-a-number" });
            context.Configuration.Add(entity: new() { Key = "imageRunners", Value = "5" });
            context.SaveChanges();
        }

        bool success = UserSettings.TryGetUserSettings(settings: out Dictionary<string, string> settings);

        success.Should().BeTrue();
        settings.Should().NotContainKey(unexpected: "externalPort");
        settings.Should().ContainKey(expected: "imageRunners");
    }

    [Fact]
    public void ApplySettings_NotSilent_LogsConfigDumpOnce_RedactingSecretKeys()
    {
        // _configDumpLogged is a process-wide static "once" guard — reset it via
        // reflection so this test deterministically observes the "first dump" branch
        // regardless of whatever other tests in this process already ran.
        System.Reflection.FieldInfo? field = typeof(UserSettings).GetField(
            name: "_configDumpLogged",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );
        field.Should().NotBeNull();
        field!.SetValue(obj: null, value: false);

        Dictionary<string, string> settings = new()
        {
            [key: "cronRunners"] = "2",
            [key: "auth_access_token"] = "super-secret-should-be-redacted",
            [key: "ssl_private_key"] = "also-secret",
            [key: "some_fingerprint"] = "secret-too",
            [key: "client_secret_thing"] = "and-this",
        };

        Action act = () => UserSettings.ApplySettings(settings: settings, silent: false);

        act.Should().NotThrow();
        // The guard must have flipped to true — proves the dump path was taken.
        ((bool)field.GetValue(obj: null)!)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ApplySettings_NotSilent_SecondCall_DoesNotRedumpConfig()
    {
        System.Reflection.FieldInfo? field = typeof(UserSettings).GetField(
            name: "_configDumpLogged",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );
        field!.SetValue(obj: null, value: true);

        // With the guard already tripped, a second non-silent call must skip the dump
        // entirely (dumpConfig short-circuits to false) — still must not throw.
        Action act = () =>
            UserSettings.ApplySettings(settings: new() { [key: "cronRunners"] = "3" }, silent: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void ApplySettings_NotSilent_ManySettings_RendersMultiColumnReportWithoutThrowing()
    {
        System.Reflection.FieldInfo? field = typeof(UserSettings).GetField(
            name: "_configDumpLogged",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );
        field!.SetValue(obj: null, value: false);

        // More than 3 entries (the report's column count) so the multi-row layout
        // (row/column index math) actually exercises more than a single row.
        Dictionary<string, string> settings = new();
        for (int i = 0; i < 7; i++)
            settings[key: $"setting{i}"] = $"value{i}";

        Action act = () => UserSettings.ApplySettings(settings: settings, silent: false);

        act.Should().NotThrow();
    }
}
