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

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Hubs;
using NoMercy.Data.Activity;
using NoMercy.Database;
using NoMercy.Networking.Cast;
using NoMercy.Networking.Messaging;
using NoMercy.NmSystem.Auth;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;
using Xunit;

namespace NoMercy.Tests.Api;

// CastHub's device-control methods (GetChromeCasts/SelectChromecast/Launch/
// CastPlaylist/GetChromecastStatus/GetMediaStatus/Stop/Disconnect) are thin,
// unguarded forwards straight to IChromeCastService — no user/moderator check
// happens in the hub itself. IChromeCastService is fully mocked here, so no
// real Chromecast is ever discovered, connected to, or commanded.
[Trait(name: "Category", value: "Unit")]
public class CastHubChromecastPassthroughTests
{
    private static CastHub CreateHub(
        out Mock<IChromeCastService> chromeCast,
        out Mock<IAuthTokenStore> authTokenStore
    )
    {
        chromeCast = new Mock<IChromeCastService>();
        authTokenStore = new Mock<IAuthTokenStore>();

        return new CastHub(
            logger: NullLogger<CastHub>.Instance,
            httpContextAccessor: Mock.Of<IHttpContextAccessor>(),
            contextFactory: Mock.Of<IDbContextFactory<MediaContext>>(),
            connectedClients: new ConnectedClients(),
            clientMessenger: Mock.Of<IClientMessenger>(),
            activityLogger: Mock.Of<IActivityLogger>(),
            authTokenStore: authTokenStore.Object,
            chromeCast: chromeCast.Object
        );
    }

    [Fact]
    public void GetChromeCasts_ReturnsDiscoveredReceiverNames_FromChromeCastService()
    {
        CastHub hub = CreateHub(chromeCast: out Mock<IChromeCastService> chromeCast, authTokenStore: out _);
        chromeCast.Setup(expression: c => c.GetChromeCasts()).Returns(value: ["Living Room", "Bedroom"]);

        string[] result = hub.GetChromeCasts();

        result.Should().Equal(expected: ["Living Room", "Bedroom"]);
    }

    [Fact]
    public async Task SelectChromecast_ForwardsReceiverName_ToChromeCastService()
    {
        CastHub hub = CreateHub(chromeCast: out Mock<IChromeCastService> chromeCast, authTokenStore: out _);

        await hub.SelectChromecast(name: "Living Room");

        chromeCast.Verify(expression: c => c.SelectChromecast("Living Room"), times: Times.Once);
    }

    [Fact]
    public async Task Launch_InvokesChromeCastServiceLaunch_WithNoReceiverNameOverride()
    {
        CastHub hub = CreateHub(chromeCast: out Mock<IChromeCastService> chromeCast, authTokenStore: out _);

        await hub.Launch();

        chromeCast.Verify(expression: c => c.Launch(null), times: Times.Once);
    }

    [Fact]
    public async Task CastPlaylist_ForwardsPlaylistValue_AndCurrentAccessToken()
    {
        CastHub hub = CreateHub(
            chromeCast: out Mock<IChromeCastService> chromeCast,
            authTokenStore: out Mock<IAuthTokenStore> authTokenStore
        );
        authTokenStore.Setup(expression: a => a.AccessToken).Returns(value: "token-123");

        await hub.CastPlaylist(value: "playlist-payload");

        chromeCast.Verify(expression: c => c.CastPlaylist("playlist-payload", null, "token-123"), times: Times.Once);
    }

    [Fact]
    public void GetChromecastStatus_ReturnsWhateverChromeCastServiceReports()
    {
        CastHub hub = CreateHub(chromeCast: out Mock<IChromeCastService> chromeCast, authTokenStore: out _);
        chromeCast.Setup(expression: c => c.GetChromecastStatus(null)).Returns(value: (ChromecastStatus?)null);

        ChromecastStatus? result = hub.GetChromecastStatus();

        result.Should().BeNull();
        chromeCast.Verify(expression: c => c.GetChromecastStatus(null), times: Times.Once);
    }

    [Fact]
    public void GetMediaStatus_ReturnsWhateverChromeCastServiceReports()
    {
        CastHub hub = CreateHub(chromeCast: out Mock<IChromeCastService> chromeCast, authTokenStore: out _);
        chromeCast.Setup(expression: c => c.GetMediaStatus(null)).Returns(value: (MediaStatus?)null);

        MediaStatus? result = hub.GetMediaStatus();

        result.Should().BeNull();
        chromeCast.Verify(expression: c => c.GetMediaStatus(null), times: Times.Once);
    }

    [Fact]
    public async Task Stop_InvokesChromeCastServiceStop_WithNoReceiverNameOverride()
    {
        CastHub hub = CreateHub(chromeCast: out Mock<IChromeCastService> chromeCast, authTokenStore: out _);

        await hub.Stop();

        chromeCast.Verify(expression: c => c.Stop(null), times: Times.Once);
    }

    [Fact]
    public async Task Disconnect_InvokesChromeCastServiceDisconnect_WithNoReceiverNameOverride()
    {
        CastHub hub = CreateHub(chromeCast: out Mock<IChromeCastService> chromeCast, authTokenStore: out _);

        await hub.Disconnect();

        chromeCast.Verify(expression: c => c.Disconnect(null), times: Times.Once);
    }
}
