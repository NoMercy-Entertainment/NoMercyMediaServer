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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.MediaProcessing.Images.Palettes;
using NoMercy.MediaProcessing.Images.Palettes.Sources;

namespace NoMercy.Tests.MediaProcessing.Palettes;

[Trait("Category", "Unit")]
public class PaletteSourceTests : IDisposable
{
    private readonly MediaContext _db;

    public PaletteSourceTests()
    {
        DbContextOptions<MediaContext> options = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    // ── TMDB sources ────────────────────────────────────────────────────────

    [Fact]
    public async Task Movie_returns_NoImage_when_both_image_fields_are_null()
    {
        Movie movie = new()
        {
            Id = 1,
            Title = "Test",
            Poster = null,
            Backdrop = null,
        };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        MoviePaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(_db, "1", CancellationToken.None);

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Movie_returns_NoImage_when_entity_not_found()
    {
        MoviePaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(_db, "999", CancellationToken.None);

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Tv_returns_NoImage_when_both_image_fields_are_null()
    {
        Database.Models.TvShows.Tv tv = new()
        {
            Id = 1,
            Title = "Test",
            Poster = null,
            Backdrop = null,
        };
        _db.Tvs.Add(tv);
        await _db.SaveChangesAsync();

        TvPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(_db, "1", CancellationToken.None);

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Season_returns_NoImage_when_poster_is_null()
    {
        Database.Models.TvShows.Season season = new() { Id = 1, Poster = null };
        _db.Seasons.Add(season);
        await _db.SaveChangesAsync();

        SeasonPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(_db, "1", CancellationToken.None);

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Image_returns_NoImage_for_non_tmdb_site()
    {
        Database.Models.Media.Image image = new()
        {
            FilePath = "/some/file.jpg",
            Type = "poster",
            Site = "https://assets.fanart.tv/",
            AspectRatio = 1.0,
        };
        _db.Images.Add(image);
        await _db.SaveChangesAsync();

        ImagePaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            image.Id.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Image_returns_NoImage_for_svg_file()
    {
        Database.Models.Media.Image image = new()
        {
            FilePath = "/some/file.svg",
            Type = "poster",
            Site = "https://image.tmdb.org/t/p/",
            AspectRatio = 1.0,
        };
        _db.Images.Add(image);
        await _db.SaveChangesAsync();

        ImagePaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            image.Id.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    // ── Local-file (music) sources ──────────────────────────────────────────

    [Fact]
    public async Task Artist_returns_NoImage_when_cover_is_null()
    {
        Artist artist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            HostFolder = "/music",
            Cover = null,
        };
        _db.Artists.Add(artist);
        await _db.SaveChangesAsync();

        ArtistPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            artist.Id.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Artist_returns_NoImage_when_local_file_does_not_exist()
    {
        Artist artist = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            HostFolder = "/music",
            Cover = "/nonexistent.jpg",
        };
        _db.Artists.Add(artist);
        await _db.SaveChangesAsync();

        ArtistPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            artist.Id.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Album_returns_NoImage_when_cover_is_null()
    {
        Album album = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Cover = null,
        };
        _db.Albums.Add(album);
        await _db.SaveChangesAsync();

        AlbumPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            album.Id.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }

    [Fact]
    public async Task Track_derives_album_palette_when_cover_matches()
    {
        const string albumPalette = "{\"cover\":{\"dominant\":\"#102030\"}}";
        Guid albumId = Guid.NewGuid();
        Guid trackId = Guid.NewGuid();

        _db.Albums.Add(
            new()
            {
                Id = albumId,
                Name = "Album",
                Cover = "/cover.jpg",
                _colorPalette = albumPalette,
            }
        );
        _db.Tracks.Add(
            new()
            {
                Id = trackId,
                Name = "Track",
                Cover = "/cover.jpg",
            }
        );
        _db.AlbumTrack.Add(new(albumId, trackId));
        await _db.SaveChangesAsync();

        TrackPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            trackId.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be(albumPalette);
        result.Permanent.Should().BeFalse();
    }

    [Fact]
    public async Task Track_derives_album_palette_when_track_has_no_cover()
    {
        const string albumPalette = "{\"cover\":{\"dominant\":\"#abcdef\"}}";
        Guid albumId = Guid.NewGuid();
        Guid trackId = Guid.NewGuid();

        _db.Albums.Add(
            new()
            {
                Id = albumId,
                Name = "Album",
                Cover = "/cover.jpg",
                _colorPalette = albumPalette,
            }
        );
        _db.Tracks.Add(
            new()
            {
                Id = trackId,
                Name = "Track",
                Cover = null,
            }
        );
        _db.AlbumTrack.Add(new(albumId, trackId));
        await _db.SaveChangesAsync();

        TrackPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            trackId.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be(albumPalette);
        result.Permanent.Should().BeFalse();
    }

    [Fact]
    public async Task Track_returns_NoImage_when_no_album_and_no_cover()
    {
        Guid trackId = Guid.NewGuid();
        _db.Tracks.Add(
            new()
            {
                Id = trackId,
                Name = "Track",
                Cover = null,
            }
        );
        await _db.SaveChangesAsync();

        TrackPaletteSource source = new();
        PaletteResult result = await source.GenerateAsync(
            _db,
            trackId.ToString(),
            CancellationToken.None
        );

        result.Json.Should().Be("{}");
        result.Permanent.Should().BeTrue();
    }
}
