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

using NoMercy.MediaProcessing.Images.Palettes.Sources;

namespace NoMercy.MediaProcessing.Images.Palettes;

/// <summary>
/// Provides the default <see cref="PaletteSourceRegistry"/> used by queue jobs
/// that cannot receive DI-injected registries. All sources are stateless and safe
/// to instantiate without a container.
/// </summary>
public static class DefaultPaletteSourceRegistry
{
    private static readonly PaletteSourceRegistry _instance = new([
        new MoviePaletteSource(),
        new TvPaletteSource(),
        new SeasonPaletteSource(),
        new EpisodePaletteSource(),
        new CollectionPaletteSource(),
        new PersonPaletteSource(),
        new RecommendationPaletteSource(),
        new SimilarPaletteSource(),
        new ImagePaletteSource(),
        new ArtistPaletteSource(),
        new AlbumPaletteSource(),
        new TrackPaletteSource(),
        new PlaylistPaletteSource(),
        new ReleaseGroupPaletteSource(),
    ]);

    public static PaletteSourceRegistry Instance => _instance;
}
