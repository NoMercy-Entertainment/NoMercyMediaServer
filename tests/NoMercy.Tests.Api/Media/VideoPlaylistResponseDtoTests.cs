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

using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait(name: "Category", value: "Playlist")]
public class VideoPlaylistResponseDtoTests
{
    private static Episode BuildEpisodeWithVideoFile(
        VideoTrack[]? tracks = null,
        string filename = "episode.mkv",
        string? subtitlesJson = null,
        UserData? userData = null,
        Image[]? tvImages = null,
        Certification[]? certifications = null,
        string languagesJson = "[\"en\"]",
        Translation? tvTranslation = null,
        Translation? episodeTranslation = null
    )
    {
        Tv tv = new()
        {
            Id = 1399,
            Title = "Breaking Bad",
            TitleSort = "breaking bad",
            FirstAirDate = new DateTime(year: 2008, month: 1, day: 20),
        };
        foreach (Image image in tvImages ?? [])
            tv.Images.Add(item: image);
        foreach (Certification certification in certifications ?? [])
            tv.CertificationTvs.Add(
                item: new()
                {
                    CertificationId = certification.Id,
                    Certification = certification,
                    TvId = tv.Id,
                    Tv = tv,
                }
            );
        if (tvTranslation is not null)
            tv.Translations.Add(item: tvTranslation);

        Season season = new()
        {
            Id = 3572,
            Title = "Season 1",
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
        };
        Episode episode = new()
        {
            Id = 62085,
            Title = "Pilot",
            Overview = "A struggling teacher is diagnosed with cancer.",
            EpisodeNumber = 1,
            SeasonNumber = 1,
            TvId = tv.Id,
            Tv = tv,
            SeasonId = season.Id,
            Season = season,
        };
        if (episodeTranslation is not null)
            episode.Translations.Add(item: episodeTranslation);

        VideoFile videoFile = new()
        {
            Filename = filename,
            Folder = "/tv",
            HostFolder = "/tv",
            Languages = languagesJson,
            Quality = "1080p",
            Share = "tv",
            EpisodeId = episode.Id,
            Episode = episode,
            Tracks = tracks ?? [],
            Subtitles = subtitlesJson,
        };
        if (userData is not null)
            videoFile.UserData.Add(item: userData);
        episode.VideoFiles.Add(item: videoFile);

        return episode;
    }

    private static Movie BuildMovieWithCertification(
        string countryIso,
        VideoTrack[]? tracks = null,
        string languagesJson = "[\"en\"]",
        Translation? translation = null
    )
    {
        Certification certification = new()
        {
            Id = 1,
            Iso31661 = countryIso,
            Rating = "PG-13",
            Meaning = "Parental guidance",
            Order = 1,
        };

        Movie movie = new()
        {
            Id = 42,
            Title = "Test Movie",
            TitleSort = "test movie",
            Overview = "A movie used for DTO tests.",
        };
        if (translation is not null)
            movie.Translations.Add(item: translation);

        movie.CertificationMovies.Add(
            item: new()
            {
                CertificationId = certification.Id,
                Certification = certification,
                MovieId = movie.Id,
                Movie = movie,
            }
        );

        movie.VideoFiles.Add(
            item: new()
            {
                Filename = "test-movie.mkv",
                Folder = "/test",
                HostFolder = "/test",
                Languages = languagesJson,
                Quality = "1080p",
                Share = "movies",
                MovieId = movie.Id,
                Movie = movie,
                Tracks = tracks ?? [],
            }
        );

        return movie;
    }

    [Fact]
    public void Ctor_PlainMovie_NoIndex_StillSetsContentRating()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US");

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        Assert.NotNull(@object: dto.ContentRating);
        Assert.Equal(expected: "PG-13", actual: dto.ContentRating!.Rating);
    }

    [Fact]
    public void Ctor_CollectionMovie_WithIndex_SetsContentRatingAndCollectionFields()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US");

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "collection", playlistId: 1, country: "US", index: 2);

        Assert.NotNull(@object: dto.ContentRating);
        Assert.Equal(expected: "PG-13", actual: dto.ContentRating!.Rating);
        Assert.Equal(expected: 0, actual: dto.Season);
        Assert.Equal(expected: 2, actual: dto.Episode);
        Assert.Equal(expected: "Collection", actual: dto.SeasonName);
    }

    [Fact]
    public void Ctor_SpriteKindTrack_IsExposedAsThumbnails()
    {
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            tracks: [new() { File = "/sprite.webp", Kind = "sprite" }]
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        Assert.Contains(collection: dto.Tracks, filter: track => track.Kind == "thumbnails");
        Assert.DoesNotContain(collection: dto.Tracks, filter: track => track.Kind == "sprite");
    }

    [Fact]
    public void Ctor_ThumbnailsKindTrack_StaysThumbnails()
    {
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            tracks: [new() { File = "/thumbs_320x178.vtt", Kind = "thumbnails" }]
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        VideoTrack track = Assert.Single(collection: dto.Tracks, predicate: t => t.Kind == "thumbnails");
        Assert.EndsWith(expectedEndString: ".vtt", actualString: track.File);
    }

    [Fact]
    public void Ctor_SpriteAndThumbnailsBothPresent_DedupesToSingleVttThumbnailsTrack()
    {
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            tracks:
            [
                new() { File = "/thumbs_320x178.webp", Kind = "sprite" },
                new() { File = "/thumbs_320x178.vtt", Kind = "thumbnails" },
            ]
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        VideoTrack track = Assert.Single(collection: dto.Tracks, predicate: t => t.Kind == "thumbnails");
        Assert.EndsWith(expectedEndString: ".vtt", actualString: track.File);
    }

    [Fact]
    public void Ctor_TwoSpriteTracksNeitherIsVtt_DedupesToFirstOccurringTrack()
    {
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            tracks:
            [
                new()
                {
                    File = "/first.webp",
                    Kind = "sprite",
                    Label = "first",
                },
                new()
                {
                    File = "/second.webp",
                    Kind = "sprite",
                    Label = "second",
                },
            ]
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        VideoTrack track = Assert.Single(collection: dto.Tracks, predicate: t => t.Kind == "thumbnails");
        Assert.EndsWith(expectedEndString: "/first.webp", actualString: track.File);
    }

    [Fact]
    public void Ctor_TwoThumbnailTracksAlongsideUnrelatedKind_DedupesThumbnailsAndKeepsUnrelated()
    {
        // Exercises the final filter's "track.Kind != thumbnails" short-circuit
        // (true for the unrelated "audio" track, kept unconditionally) alongside
        // the "track == keep" comparison it otherwise has to fall through to for
        // every thumbnails candidate.
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            tracks:
            [
                new() { File = "/first.webp", Kind = "sprite" },
                new() { File = "/second.webp", Kind = "sprite" },
                new() { File = "/audio.m4a", Kind = "audio" },
            ]
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Tracks.Should().ContainSingle(predicate: t => t.Kind == "thumbnails");
        dto.Tracks.Should().ContainSingle(predicate: t => t.Kind == "audio");
    }

    [Fact]
    public void Ctor_SingleThumbnailsTrackAlongsideUnrelatedKind_BothPreserved()
    {
        // NormalizePreviewTracks only dedupes when 2+ tracks resolve to "thumbnails".
        // A lone thumbnails track sharing space with an unrelated kind must return
        // early and leave both untouched.
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            tracks:
            [
                new() { File = "/thumbs.vtt", Kind = "thumbnails" },
                new() { File = "/audio.m4a", Kind = "audio" },
            ]
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Tracks.Should().Contain(predicate: t => t.Kind == "thumbnails");
        dto.Tracks.Should().Contain(predicate: t => t.Kind == "audio");
    }

    [Fact]
    public void Ctor_NonSubtitleTrackFile_IsPrefixedWithVideoFileBaseFolder()
    {
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            tracks: [new() { File = "/thumbs.vtt", Kind = "thumbnails" }]
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        VideoTrack track = dto.Tracks.Single(predicate: t => t.Kind == "thumbnails");
        track.File.Should().Be(expected: "/movies/test/thumbs.vtt");
    }

    [Fact]
    public void Ctor_Movie_Mp4Filename_UsesVideoMp4SourceType()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US");
        movie.VideoFiles.First().Filename = "test-movie.mp4";

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Sources.Should().ContainSingle();
        dto.Sources[0].Type.Should().Be(expected: "video/mp4");
    }

    [Fact]
    public void Ctor_Movie_LanguagesArrayContainsNullEntry_FiltersItOut()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US", languagesJson: "[\"en\", null]");

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Sources[0].Languages.Should().Equal(expected: "en");
    }

    [Fact]
    public void Ctor_Movie_LanguagesJsonIsNullLiteral_YieldsEmptyLanguagesArray()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US", languagesJson: "null");

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Sources[0].Languages.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_Movie_TranslationPresent_OverridesTitleAndOverview()
    {
        Movie movie = BuildMovieWithCertification(
            countryIso: "US",
            translation: new()
            {
                Iso6391 = "nl",
                Title = "Nederlandse titel",
                Overview = "Nederlandse samenvatting.",
            }
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Title.Should().Be(expected: "Nederlandse titel");
        dto.Description.Should().Be(expected: "Nederlandse samenvatting.");
    }

    [Fact]
    public void Ctor_Movie_Certification_MatchesUsExplicitly()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US");

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "NL");

        dto.ContentRating.Should().NotBeNull();
        dto.ContentRating!.Iso31661.Should().Be(expected: "US");
    }

    [Fact]
    public void Ctor_Movie_NoIndex_LeavesCollectionOnlyFieldsNull()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US");

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.SeasonName.Should().BeNull();
        dto.Season.Should().BeNull();
        dto.Episode.Should().BeNull();
    }

    [Fact]
    public void Ctor_Movie_NoVideoFile_LeavesDtoAtDefault()
    {
        Movie movie = new()
        {
            Id = 99,
            Title = "No File",
            TitleSort = "no file",
        };

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Id.Should().Be(expected: 0);
        dto.Title.Should().BeNull();
    }

    [Fact]
    public void Ctor_Movie_CollectionProvided_UsesCollectionIdAsTmdbId()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US");
        Collection collection = new() { Id = 777, Title = "Franchise" };

        VideoPlaylistResponseDto dto = new(
            movie: movie,
            playlistType: "collection",
            playlistId: 1,
            country: "US",
            index: 1,
            collection: collection
        );

        dto.TmdbId.Should().Be(expected: 777);
    }

    [Fact]
    public void Ctor_Movie_Certification_NoUsOrCountryMatch_LeavesContentRatingNull()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "DE");

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "NL");

        dto.ContentRating.Should().BeNull();
    }

    [Fact]
    public void Ctor_Movie_LogoPicksHighestVoteAverageLogoImage()
    {
        Movie movie = BuildMovieWithCertification(countryIso: "US");
        movie.Images.Add(
            item: new()
            {
                Type = "logo",
                FilePath = "/low.png",
                VoteAverage = 1,
            }
        );
        movie.Images.Add(
            item: new()
            {
                Type = "logo",
                FilePath = "/high.png",
                VoteAverage = 9,
            }
        );
        movie.Images.Add(
            item: new()
            {
                Type = "poster",
                FilePath = "/poster.png",
                VoteAverage = 99,
            }
        );

        VideoPlaylistResponseDto dto = new(movie: movie, playlistType: "movie", playlistId: 1, country: "US");

        dto.Logo.Should().Be(expected: "/high.png");
    }

    [Fact]
    public void Ctor_Episode_NoVideoFile_LeavesDtoAtDefault()
    {
        Tv tv = new()
        {
            Id = 1,
            Title = "Show",
            TitleSort = "show",
        };
        Episode episode = new()
        {
            Id = 5,
            Tv = tv,
            TvId = tv.Id,
            SeasonNumber = 1,
            EpisodeNumber = 1,
        };

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Id.Should().Be(expected: 0);
        dto.Title.Should().BeNull();
    }

    [Fact]
    public void Ctor_Episode_NoTv_LeavesDtoAtDefault()
    {
        Episode episode = new()
        {
            Id = 6,
            SeasonNumber = 1,
            EpisodeNumber = 1,
        };
        episode.VideoFiles.Add(
            item: new()
            {
                Filename = "x.mkv",
                Folder = "/tv",
                HostFolder = "/tv",
                Languages = "[\"en\"]",
                Quality = "1080p",
                Share = "tv",
                EpisodeId = episode.Id,
                Episode = episode,
            }
        );

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Id.Should().Be(expected: 0);
    }

    [Fact]
    public void Ctor_Episode_WithIndex_UsesSpecialTitleFormatAndHidesShow()
    {
        Episode episode = BuildEpisodeWithVideoFile();

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "specials", playlistId: 1, country: "US", index: 3);

        dto.Title.Should().Be(expected: "Breaking Bad %S1 %E1 - Pilot");
        dto.Show.Should().BeNull();
        dto.Season.Should().Be(expected: 0);
        dto.Episode.Should().Be(expected: 3);
    }

    [Fact]
    public void Ctor_Episode_WithoutIndex_UsesPlainTitleAndShowsShowName()
    {
        Episode episode = BuildEpisodeWithVideoFile();

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Title.Should().Be(expected: "Pilot");
        dto.Show.Should().Be(expected: "Breaking Bad");
        dto.Season.Should().Be(expected: 1);
        dto.Episode.Should().Be(expected: 1);
        dto.SeasonName.Should().Be(expected: "Season 1");
        dto.EpisodeId.Should().Be(expected: episode.Id);
    }

    [Fact]
    public void Ctor_Episode_LogoPicksHighestVoteAverageLogoImage()
    {
        Episode episode = BuildEpisodeWithVideoFile(
            tvImages:
            [
                new()
                {
                    Type = "logo",
                    FilePath = "/low.png",
                    VoteAverage = 1,
                },
                new()
                {
                    Type = "logo",
                    FilePath = "/high.png",
                    VoteAverage = 9,
                },
                new()
                {
                    Type = "poster",
                    FilePath = "/poster.png",
                    VoteAverage = 99,
                },
            ]
        );

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Logo.Should().Be(expected: "/high.png");
    }

    [Fact]
    public void Ctor_Episode_ProgressPopulatedFromVideoFileUserData()
    {
        UserData userData = new()
        {
            Type = "tv",
            Time = 500,
            LastPlayedDate = "2026-01-15T10:00:00Z",
        };
        Episode episode = BuildEpisodeWithVideoFile(userData: userData);

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Progress.Should().NotBeNull();
        dto.Progress!.Time.Should().Be(expected: 500);
    }

    [Fact]
    public void Ctor_Episode_NoUserData_ProgressIsNull()
    {
        Episode episode = BuildEpisodeWithVideoFile();

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Progress.Should().BeNull();
    }

    [Fact]
    public void Ctor_Episode_Certification_MatchesUsRegardlessOfRequestedCountry()
    {
        Certification us = new()
        {
            Id = 4,
            Iso31661 = "US",
            Rating = "TV-14",
            Meaning = "Parental guidance",
            Order = 1,
        };
        Episode episode = BuildEpisodeWithVideoFile(certifications: [us]);

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "NL");

        dto.ContentRating.Should().NotBeNull();
        dto.ContentRating!.Iso31661.Should().Be(expected: "US");
    }

    [Fact]
    public void Ctor_Episode_TranslationsPresent_OverrideTitleAndOverview()
    {
        Episode episode = BuildEpisodeWithVideoFile(
            tvTranslation: new() { Iso6391 = "nl", Title = "Zwaar Weer" },
            episodeTranslation: new()
            {
                Iso6391 = "nl",
                Title = "Proefaflevering",
                Overview = "Nederlandse samenvatting.",
            }
        );

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Show.Should().Be(expected: "Zwaar Weer");
        dto.Title.Should().Be(expected: "Proefaflevering");
        dto.Description.Should().Be(expected: "Nederlandse samenvatting.");
    }

    [Fact]
    public void Ctor_Episode_Certification_MatchesRequestedCountry_WhenNoUsCertification()
    {
        Certification nl = new()
        {
            Id = 2,
            Iso31661 = "NL",
            Rating = "12",
            Meaning = "Parental guidance",
            Order = 1,
        };
        Episode episode = BuildEpisodeWithVideoFile(certifications: [nl]);

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "NL");

        dto.ContentRating.Should().NotBeNull();
        dto.ContentRating!.Rating.Should().Be(expected: "12");
        dto.ContentRating.Iso31661.Should().Be(expected: "NL");
    }

    [Fact]
    public void Ctor_Episode_Certification_NoUsOrCountryMatch_LeavesContentRatingNull()
    {
        Certification de = new()
        {
            Id = 3,
            Iso31661 = "DE",
            Rating = "16",
            Meaning = "Parental guidance",
            Order = 1,
        };
        Episode episode = BuildEpisodeWithVideoFile(certifications: [de]);

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "NL");

        dto.ContentRating.Should().BeNull();
    }

    [Fact]
    public void Ctor_Episode_ValidSubtitlesJson_AddsSubtitleTextTrack()
    {
        const string subtitlesJson = """[{"language":"nl","type":"full","ext":"vtt"}]""";
        Episode episode = BuildEpisodeWithVideoFile(subtitlesJson: subtitlesJson);

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        VideoTrack track = dto.Tracks.Single(predicate: t => t.Kind == "subtitles");
        track.Language.Should().Be(expected: "nl");
        track.File.Should().EndWith(expected: ".nl.full.vtt");
    }

    [Fact]
    public void Ctor_Episode_MalformedSubtitlesJson_TreatedAsNoSubtitleTracks()
    {
        Episode episode = BuildEpisodeWithVideoFile(subtitlesJson: "{not-valid-json");

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Tracks.Should().NotContain(predicate: t => t.Kind == "subtitles");
    }

    [Fact]
    public void Ctor_Episode_LanguagesArrayContainsNullEntry_FiltersItOut()
    {
        Episode episode = BuildEpisodeWithVideoFile(languagesJson: "[\"en\", null]");

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Sources[0].Languages.Should().Equal(expected: "en");
    }

    [Fact]
    public void Ctor_Episode_LanguagesJsonIsNullLiteral_YieldsEmptyLanguagesArray()
    {
        Episode episode = BuildEpisodeWithVideoFile(languagesJson: "null");

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Sources[0].Languages.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_Episode_TracksOrderedByLanguageAscending()
    {
        VideoTrack[] tracks =
        [
            new()
            {
                File = "/z.vtt",
                Kind = "captions",
                Language = "zzz",
            },
            new()
            {
                File = "/a.vtt",
                Kind = "captions",
                Language = "aaa",
            },
        ];
        Episode episode = BuildEpisodeWithVideoFile(tracks: tracks);

        VideoPlaylistResponseDto dto = new(episode: episode, playlistType: "tv", playlistId: 1, country: "US");

        dto.Tracks.Select(selector: t => t.Language).Should().BeInAscendingOrder();
    }
}
