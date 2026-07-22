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
using NoMercy.Api.DTOs.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.Services.Music;

public class MusicPlaylistManager
{
    private readonly IMusicRepository _musicRepository;
    private readonly MediaContext _mediaContext;

    public MusicPlaylistManager(IMusicRepository musicService, MediaContext mediaContext)
    {
        _musicRepository = musicService;
        _mediaContext = mediaContext;
    }

    public async Task<(PlaylistTrackDto item, List<PlaylistTrackDto> playlist)> GetPlaylist(
        Guid userId,
        string type,
        Guid listId,
        Guid trackId,
        string country
    )
    {
        return type.ToLower().Trim() switch
        {
            // For type="track", the track ID is in the listId parameter (second param)
            // Call format: StartPlaybackCommand("track", trackId, null/empty)
            "track" => await GetSingleTrack(userId: userId, trackId: listId, country: country),
            "playlist" => await GetPlaylistTracks(userId: userId, listId: listId, trackId: trackId, country: country),
            "album" => await GetAlbumTracks(userId: userId, listId: listId, trackId: trackId, country: country),
            "artist" => await GetArtistTracks(userId: userId, listId: listId, trackId: trackId, country: country),
            "genre" => await GetGenreTracks(userId: userId, listId: listId, trackId: trackId, country: country),
            _ => throw new ArgumentException(message: $"Invalid playlist type: '{type}'", paramName: nameof(type)),
        };
    }

    public (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) SplitPlaylist(
        List<PlaylistTrackDto> playlist,
        Guid currentTrackId
    )
    {
        int index = playlist.FindIndex(match: p => p.Id == currentTrackId);
        if (index == -1)
            return ([], playlist);

        List<PlaylistTrackDto> before = playlist.GetRange(index: 0, count: index);
        List<PlaylistTrackDto> after = playlist.GetRange(index: index + 1, count: playlist.Count - index - 1);

        return (before, after);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetSingleTrack(
        Guid userId,
        Guid trackId,
        string country
    )
    {
        Track? track = await _musicRepository.GetTrackAsync(id: trackId);

        if (track is null)
            throw new(message: "Track not found");

        // Load TrackUser data for favorite status
        bool isFavorite = await _mediaContext.TrackUser.AnyAsync(predicate: tu =>
            tu.TrackId == trackId && tu.UserId == userId
        );

        if (isFavorite && !track.TrackUser.Any(predicate: tu => tu.UserId == userId))
        {
            track.TrackUser.Add(item: new() { TrackId = trackId, UserId = userId });
        }

        PlaylistTrackDto trackDto = new(track: track, country: country);

        // Return the track with an empty playlist (no other tracks to play)
        return (trackDto, []);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetPlaylistTracks(
        Guid userId,
        Guid listId,
        Guid trackId,
        string country
    )
    {
        List<PlaylistTrack> playlistTracks = await _musicRepository.GetPlaylistTracksAsync(
            userId: userId,
            playlistId: listId
        );

        if (playlistTracks.Count == 0)
            throw new(message: "Playlist track not found");

        List<PlaylistTrackDto> playlist = playlistTracks
            .Select(selector: x => new PlaylistTrackDto(trackTrack: x, country: country))
            .ToList();

        PlaylistTrackDto item =
            playlist.FirstOrDefault(predicate: p => p.Id == trackId) ?? throw new(message: "Playlist track not found");
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(
            playlist: playlist,
            currentTrackId: trackId
        );
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(collection: after);
        sortedPlaylist.AddRange(collection: before);

        return (item, sortedPlaylist);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetAlbumTracks(
        Guid userId,
        Guid listId,
        Guid trackId,
        string country
    )
    {
        List<AlbumTrack> albumTracks = await _musicRepository.GetAlbumTracksAsync(userId: userId, albumId: listId);

        if (albumTracks.Count == 0)
            throw new(message: "Album track not found");

        List<PlaylistTrackDto> playlist = albumTracks
            .Select(selector: x => new PlaylistTrackDto(artistTrack: x, country: country))
            .OrderBy(keySelector: x => x.Disc)
            .ThenBy(keySelector: x => x.Track)
            .ToList();

        PlaylistTrackDto item =
            playlist.FirstOrDefault(predicate: p => p.Id == trackId) ?? throw new(message: "Album track not found");
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(
            playlist: playlist,
            currentTrackId: trackId
        );
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(collection: after);
        sortedPlaylist.AddRange(collection: before);

        return (item, sortedPlaylist);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetArtistTracks(
        Guid userId,
        Guid listId,
        Guid trackId,
        string country
    )
    {
        List<ArtistTrack> artistTracks = await _musicRepository.GetArtistTracksAsync(
            userId: userId,
            artistId: listId
        );

        if (artistTracks.Count == 0)
            throw new(message: "Artist track not found");

        List<PlaylistTrackDto> playlist = artistTracks
            .Select(selector: x => new PlaylistTrackDto(artistTrack: x, country: country))
            .DistinctBy(keySelector: x => x.Id)
            .OrderBy(keySelector: x => x.AlbumName)
            .ThenBy(keySelector: x => x.Disc)
            .ThenBy(keySelector: x => x.Track)
            .ToList();

        PlaylistTrackDto item =
            playlist.FirstOrDefault(predicate: p => p.Id == trackId) ?? throw new(message: "Artist track not found");
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(
            playlist: playlist,
            currentTrackId: trackId
        );
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(collection: after);
        sortedPlaylist.AddRange(collection: before);

        return (item, sortedPlaylist);
    }

    private async Task<(PlaylistTrackDto, List<PlaylistTrackDto>)> GetGenreTracks(
        Guid userId,
        Guid listId,
        Guid trackId,
        string country
    )
    {
        List<MusicGenreTrack> genreTracks = await _musicRepository.GetGenreTracksAsync(
            userId: userId,
            genreId: listId
        );

        if (genreTracks.Count == 0)
            throw new(message: "Genre track not found");

        List<PlaylistTrackDto> playlist = genreTracks
            .Select(selector: x => new PlaylistTrackDto(genreTrack: x, country: country))
            .DistinctBy(keySelector: x => x.Id)
            .OrderBy(keySelector: x => x.Disc)
            .ThenBy(keySelector: x => x.Track)
            .ToList();

        PlaylistTrackDto item =
            playlist.FirstOrDefault(predicate: p => p.Id == trackId) ?? throw new(message: "Genre track not found");
        (List<PlaylistTrackDto> before, List<PlaylistTrackDto> after) = SplitPlaylist(
            playlist: playlist,
            currentTrackId: trackId
        );
        List<PlaylistTrackDto> sortedPlaylist = [];
        sortedPlaylist.AddRange(collection: after);
        sortedPlaylist.AddRange(collection: before);

        return (item, sortedPlaylist);
    }
}
