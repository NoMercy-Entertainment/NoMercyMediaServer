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
using NoMercy.Api.DTOs.Media;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using static System.Int32;

namespace NoMercy.Api.Services.Video;

public class VideoPlayerStateFactory
{
    public static async Task<VideoPlayerState> Create(
        IDbContextFactory<MediaContext> contextFactory,
        User user,
        Device device,
        VideoPlaylistResponseDto item,
        List<VideoPlaylistResponseDto> playlist,
        string type,
        dynamic listId
    )
    {
        await using MediaContext context = await contextFactory.CreateDbContextAsync();

        ArgumentNullException.ThrowIfNull(argument: listId);

        string id = listId.ToString();

        // parse id once and safely
        TryParse(s: id, result: out int parsedId);

        // Cast/remote-control needs the current item's structured chapter/audio/
        // caption/quality lists (parsed chapter times, ordered track lists) which
        // live on Metadata, not the slim wire DTO. Load them once for the current
        // item so the state carries them for the VideoHub command handlers.
        VideoFile? currentVideoFile = await context
            .VideoFiles.AsNoTracking()
            .Include(navigationPropertyPath: videoFile => videoFile.Metadata)
            .FirstOrDefaultAsync(predicate: videoFile => videoFile.Id == item.VideoId);
        Metadata? metadata = currentVideoFile?.Metadata;
        List<IChapter> chapters = metadata?.Chapters ?? [];
        List<IAudio> audioTracks = metadata?.Audio ?? [];
        List<ISubtitle> captions = metadata?.Subtitles ?? [];
        List<IVideo> qualities = metadata?.Video ?? [];

        // Include playback preferences and their Library collections to ensure data available for matching
        User? userPreference = await context
            .Users.Include(navigationPropertyPath: u => u.PlaybackPreferences)
                .ThenInclude(navigationPropertyPath: playbackPreference => playbackPreference.Library)
                    .ThenInclude(navigationPropertyPath: library => library!.LibraryTvs)
            .Include(navigationPropertyPath: u => u.PlaybackPreferences)
                .ThenInclude(navigationPropertyPath: playbackPreference => playbackPreference.Library)
                    .ThenInclude(navigationPropertyPath: library => library!.LibraryMovies)
            .FirstOrDefaultAsync(predicate: u => u.Id == user.Id);

        if (userPreference is null)
        {
            // Fallback to default playback preference when the user could not be loaded
            return new()
            {
                DeviceId = device.DeviceId,
                VolumePercentage = device.VolumePercent ?? Device.DefaultVolumePercent,
                CurrentItem = item,
                CurrentAudio = null,
                CurrentCaption = null,
                CurrentQuality = null,
                Chapters = chapters,
                Audio = audioTracks,
                Captions = captions,
                Qualities = qualities,
                Playlist = playlist,
                PlayState = true,
                Time = (item.Progress?.Time ?? 0) * 1000,
                Duration = item.Duration.ToMilliSeconds(),
                CurrentList = new(uriString: $"/{type}/{listId}/watch", uriKind: UriKind.Relative),
                Actions = new()
                {
                    Disallows = new()
                    {
                        Stopping = false,
                        Seeking = false,
                        Muting = false,
                        Pausing = false,
                        Resuming = true,
                        Previous = playlist.IndexOf(item: item) == 0,
                        Next = playlist.IndexOf(item: item) == playlist.Count - 1,
                    },
                },
            };
        }

        PlaybackPreference? playbackPreference = FindPlaybackPreference(
            userPreference: userPreference,
            id: id,
            parsedId: parsedId,
            type: type
        );

        if (playbackPreference is null)
        {
            playbackPreference = CreateDefaultPlaybackPreference(qualities: qualities, audio: audioTracks, captions: captions);
        }

        return new()
        {
            DeviceId = device.DeviceId,
            VolumePercentage = device.VolumePercent ?? Device.DefaultVolumePercent,
            CurrentItem = item,
            CurrentAudio = playbackPreference.Audio,
            CurrentCaption = playbackPreference.Subtitle,
            CurrentQuality = playbackPreference.Video,
            Chapters = chapters,
            Audio = audioTracks,
            Captions = captions,
            Qualities = qualities,
            Playlist = playlist,
            PlayState = true,
            Time = (item.Progress?.Time ?? 0) * 1000,
            Duration = item.Duration.ToMilliSeconds(),
            CurrentList = new(uriString: $"/{type}/{listId}/watch", uriKind: UriKind.Relative),
            Actions = new()
            {
                Disallows = new()
                {
                    Stopping = false,
                    Seeking = false,
                    Muting = false,
                    Pausing = false,
                    Resuming = true,
                    Previous = playlist.IndexOf(item: item) == 0,
                    Next = playlist.IndexOf(item: item) == playlist.Count - 1,
                },
            },
        };
    }

    private static PlaybackPreference? FindPlaybackPreference(
        User userPreference,
        string id,
        int parsedId,
        string type
    )
    {
        PlaybackPreference? byIds = userPreference.PlaybackPreferences.FirstOrDefault(predicate: p =>
            (
                p.MovieId is not null
                && p.MovieId.ToString() == id
                && MediaTypes.MovieMediaType == type
            )
            || (p.TvId is not null && p.TvId.ToString() == id && MediaTypes.TvMediaType == type)
            || (
                p.CollectionId is not null
                && p.CollectionId.ToString() == id
                && MediaTypes.CollectionMediaType == type
            )
            || (
                p.SpecialId is not null
                && p.SpecialId.ToString() == id
                && MediaTypes.SpecialMediaType == type
            )
        );

        if (byIds is not null)
            return byIds;

        return userPreference.PlaybackPreferences.FirstOrDefault(predicate: p =>
            p.Library != null
            && (
                p.Library.Type == type
                || (
                    type == MediaTypes.TvMediaType
                    && p.Library.LibraryTvs.Any(predicate: t => t.TvId == parsedId)
                )
                || (
                    type == MediaTypes.MovieMediaType
                    && p.Library.LibraryMovies.Any(predicate: m => m.MovieId == parsedId)
                )
            )
        );
    }

    private static PlaybackPreference CreateDefaultPlaybackPreference(
        List<IVideo> qualities,
        List<IAudio> audio,
        List<ISubtitle> captions
    )
    {
        int? width = qualities.Select(selector: q => q.Width).FirstOrDefault();
        string? audioLanguage = audio.Select(selector: a => a.Language).FirstOrDefault();
        string? subtitleLanguage = captions.FirstOrDefault()?.Language;
        string? subtitleType = captions.FirstOrDefault()?.Type;
        string? subtitleCodec = captions.FirstOrDefault()?.Codec;

        return new()
        {
            Video = width.HasValue ? new() { Width = width.Value } : null,
            Audio = audioLanguage is not null ? new() { Language = audioLanguage } : null,
            Subtitle = subtitleLanguage is not null
                ? new()
                {
                    Language = subtitleLanguage,
                    Type = subtitleType,
                    Codec = subtitleCodec,
                }
                : null,
        };
    }
}
