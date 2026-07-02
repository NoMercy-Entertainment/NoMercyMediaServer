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
[Trait("Category", "Unit")]
public class UserSettingsTests : IDisposable
{
    private readonly int _originalInternalPort = RuntimeServerSettings.Current.InternalServerPort;
    private readonly int _originalExternalPort = RuntimeServerSettings.Current.ExternalServerPort;

    public UserSettingsTests()
    {
        Directory.CreateDirectory(AppFiles.DataPath);

        using AppDbContext context = new();
        context.Database.EnsureCreated();
        context.Configuration.RemoveRange(context.Configuration);
        context.SaveChanges();
    }

    public void Dispose()
    {
        using AppDbContext context = new();
        context.Configuration.RemoveRange(context.Configuration);
        context.SaveChanges();

        RuntimeServerSettings.Current.InternalServerPort = _originalInternalPort;
        RuntimeServerSettings.Current.ExternalServerPort = _originalExternalPort;
    }

    [Fact]
    public void TryGetUserSettings_MalformedPortRow_SkipsRowButKeepsOthers()
    {
        using (AppDbContext context = new())
        {
            context.Configuration.Add(new() { Key = "internalPort", Value = "not-a-number" });
            context.Configuration.Add(new() { Key = "cronRunners", Value = "3" });
            context.SaveChanges();
        }

        bool success = UserSettings.TryGetUserSettings(out Dictionary<string, string> settings);

        success.Should().BeTrue();
        settings.Should().NotContainKey("internalPort");
        settings.Should().ContainKey("cronRunners");
        settings["cronRunners"].Should().Be("3");
    }

    [Fact]
    public void TryGetUserSettings_AllGoodRows_ReturnsEveryRow()
    {
        using (AppDbContext context = new())
        {
            context.Configuration.Add(new() { Key = "cronRunners", Value = "2" });
            context.Configuration.Add(new() { Key = "imageRunners", Value = "5" });
            context.SaveChanges();
        }

        bool success = UserSettings.TryGetUserSettings(out Dictionary<string, string> settings);

        success.Should().BeTrue();
        settings.Should().HaveCount(2);
        settings["cronRunners"].Should().Be("2");
        settings["imageRunners"].Should().Be("5");
    }

    [Fact]
    public void ApplySettings_MalformedPortValue_DoesNotThrow_AndSkipsThatSetting()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;

        Dictionary<string, string> settings = new()
        {
            ["internalPort"] = "garbage",
            ["cronRunners"] = "4",
        };

        Action act = () => UserSettings.ApplySettings(settings, silent: true);

        act.Should().NotThrow();
        RuntimeServerSettings.Current.InternalServerPort.Should().Be(7626);
        RuntimeServerSettings.Current.CronWorkers.Value.Should().Be(4);
    }

    [Fact]
    public void ApplySettings_ValidPortValue_UpdatesRuntimeSetting()
    {
        RuntimeServerSettings.Current.InternalServerPort = 7626;

        Dictionary<string, string> settings = new() { ["internalPort"] = "8080" };

        UserSettings.ApplySettings(settings, silent: true);

        RuntimeServerSettings.Current.InternalServerPort.Should().Be(8080);
    }
}
