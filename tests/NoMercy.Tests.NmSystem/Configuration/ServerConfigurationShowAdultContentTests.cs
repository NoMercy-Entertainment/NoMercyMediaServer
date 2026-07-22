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
using Microsoft.Extensions.Options;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;

namespace NoMercy.Tests.NmSystem.Configuration;

/// <summary>
/// Regression tests for the adult-content gate split:
/// IServerConfiguration.ShowAdultContent must track RuntimeServerSettings.AllowAdultContent
/// (the dashboard-mutable live value), not a static config-file field.
/// Fails without the fix (ServerConfiguration.ShowAdultContent was a settable POCO field
/// that the dashboard never updated, so the controller gate never reflected the toggle).
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ServerConfigurationShowAdultContentTests : IDisposable
{
    private readonly bool? _originalAllowAdult = RuntimeServerSettings.Current.AllowAdultContent;

    private static ServerConfigurationWrapper BuildWrapper()
    {
        IOptions<ServerConfiguration> opts = Options.Create(options: new ServerConfiguration());
        return new(options: opts);
    }

    public void Dispose()
    {
        RuntimeServerSettings.Current.AllowAdultContent = _originalAllowAdult;
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public void ShowAdultContent_IsFalse_WhenAllowAdultContentIsNull()
    {
        RuntimeServerSettings.Current.AllowAdultContent = null;

        BuildWrapper().ShowAdultContent.Should().BeFalse();
    }

    [Fact]
    public void ShowAdultContent_IsFalse_WhenAllowAdultContentIsFalse()
    {
        RuntimeServerSettings.Current.AllowAdultContent = false;

        BuildWrapper().ShowAdultContent.Should().BeFalse();
    }

    [Fact]
    public void ShowAdultContent_IsTrue_WhenAllowAdultContentIsTrue()
    {
        RuntimeServerSettings.Current.AllowAdultContent = true;

        BuildWrapper().ShowAdultContent.Should().BeTrue();
    }

    [Fact]
    public void ShowAdultContent_FlipsLive_WhenRuntimeSettingChanges()
    {
        ServerConfigurationWrapper wrapper = BuildWrapper();

        RuntimeServerSettings.Current.AllowAdultContent = false;
        bool before = wrapper.ShowAdultContent;

        RuntimeServerSettings.Current.AllowAdultContent = true;
        bool after = wrapper.ShowAdultContent;

        before.Should().BeFalse();
        after.Should().BeTrue();
    }

    [Theory]
    [InlineData(data: [null, false])]
    [InlineData(data: [false, false])]
    [InlineData(data: [true, true])]
    public void ShowAdultContent_MatchesRuntimeSettingForAllThreeStates(
        bool? configured,
        bool expected
    )
    {
        RuntimeServerSettings.Current.AllowAdultContent = configured;

        BuildWrapper().ShowAdultContent.Should().Be(expected: expected);
    }

    [Fact]
    public void ShowAdultContent_IsComputed_NotMutable()
    {
        PropertyInfo property = typeof(ServerConfiguration).GetProperty(
            name: nameof(ServerConfiguration.ShowAdultContent)
        )!;

        property.Should().NotBeNull();
        property
            .CanWrite.Should()
            .BeFalse(
                because: "ShowAdultContent must be a computed guard that tracks RuntimeServerSettings, not a settable field"
            );
    }
}
