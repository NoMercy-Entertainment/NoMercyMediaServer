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

using NoMercy.NmSystem.Configuration;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

/// <summary>
/// A DB-stored artwork path is a bare TMDB path (e.g. "/abc123.jpg"), never a
/// fetchable URL by itself. These pin the exact host and query shape the
/// Android client's own tv.nomercy.shared.images.TmdbImageUrl builds
/// (app.nomercy.tv/tmdb-images{path}?width=...), so a push payload lands the
/// identical, phone-reachable, pre-sized asset a client would have built itself.
/// </summary>
public class PushArtworkUrlTests
{
    [Fact]
    public void Null_Path_Builds_Null_So_A_Missing_Artwork_Never_Blocks_The_Notification()
    {
        Assert.Null(PushArtworkUrl.Build(null, PushArtworkUrl.BackdropWidth));
    }

    [Fact]
    public void Empty_Path_Builds_Null()
    {
        Assert.Null(PushArtworkUrl.Build(string.Empty, PushArtworkUrl.PosterWidth));
    }

    [Fact]
    public void A_Tmdb_Path_Builds_An_Absolute_Https_Url_Through_The_App_Cdn_With_A_Width_Hint()
    {
        string? url = PushArtworkUrl.Build("/abc123.jpg", PushArtworkUrl.BackdropWidth);

        Assert.Equal(
            $"{ExternalServicesConfig.Current.AppBaseUrl.TrimEnd('/')}/tmdb-images/abc123.jpg?width={PushArtworkUrl.BackdropWidth}",
            url
        );
        Assert.StartsWith("https://", url);
    }

    [Fact]
    public void Poster_And_Backdrop_Widths_Are_Distinct_And_Both_Modest()
    {
        Assert.NotEqual(PushArtworkUrl.PosterWidth, PushArtworkUrl.BackdropWidth);
        Assert.True(PushArtworkUrl.PosterWidth <= 500);
        Assert.True(PushArtworkUrl.BackdropWidth <= 780);
    }
}
