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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Music;

public class AlbumDto
{
    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public JToken? ColorPalette { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    // [JsonProperty("tracks")] public IEnumerable<Track> Tracks { get; set; }
    [JsonProperty(propertyName: "year")]
    public int? Year { get; set; }

    [JsonProperty(propertyName: "album_artist")]
    public Guid? AlbumArtist { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    public AlbumDto(AlbumArtist albumArtist, string country)
    {
        string? description = albumArtist
            .Album.Translations.FirstOrDefault(predicate: translation => translation.Iso31661 == country)
            ?.Description;

        Image? img = albumArtist.Artist.Images.FirstOrDefault(predicate: image => image.Type == "background");

        Id = albumArtist.Album.Id;
        Name = albumArtist.Album.Name;
        Cover = albumArtist.Album.Cover is not null
            ? new Uri(uriString: $"/images/music{albumArtist.Album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = albumArtist.Album.Disambiguation;
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Description = !string.IsNullOrEmpty(value: description)
            ? description
            : albumArtist.Album.Description;
        Type = "album";
        ColorPalette = albumArtist.Album._colorPalette.ToRaw();
        // Tracks = albumArtist.Albums.AlbumTrack.Select(a => a.Track);
        Year = albumArtist.Album.Year;

        AlbumArtist = albumArtist.ArtistId;
    }

    public AlbumDto(AlbumTrack albumTrack, string country)
    {
        string? description = albumTrack
            .Album.Translations.FirstOrDefault(predicate: translation => translation.Iso31661 == country)
            ?.Description;

        Image? img = albumTrack
            .Album.AlbumArtist.FirstOrDefault()
            ?.Album.Images.FirstOrDefault(predicate: image => image.Type == "background");
        Id = albumTrack.Album.Id;
        Name = albumTrack.Album.Name;
        Cover = albumTrack.Album.Cover is not null
            ? new Uri(uriString: $"/images/music{albumTrack.Album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = albumTrack.Album.Disambiguation;
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Description = !string.IsNullOrEmpty(value: description)
            ? description
            : albumTrack.Album.Description;
        Type = "album";
        ColorPalette = albumTrack.Album._colorPalette.ToRaw();
        Year = albumTrack.Album.Year;

        AlbumArtist = albumTrack.Album.AlbumArtist.MaxBy(keySelector: at => at.ArtistId)?.ArtistId;
    }

    public AlbumDto(Album album, string country)
    {
        string? description = album
            .Translations.FirstOrDefault(predicate: translation => translation.Iso31661 == country)
            ?.Description;
        Image? img = album
            .AlbumArtist.FirstOrDefault()
            ?.Artist.Images.FirstOrDefault(predicate: image => image.Type == "background");

        Id = album.Id;
        Name = album.Name;
        Cover = album.Cover is not null
            ? new Uri(uriString: $"/images/music{album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        Backdrop = img?.FilePath is not null
            ? new Uri(uriString: $"/images/music{img.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Disambiguation = album.Disambiguation;
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Description = !string.IsNullOrEmpty(value: description) ? description : album.Description;
        Type = "album";
        ColorPalette = album._colorPalette.ToRaw();
        Disambiguation = album.Disambiguation;
        // Tracks = album.AlbumTrack.Select(a => a.Track);
        Year = album.Year;

        List<IGrouping<Guid, AlbumArtist>> artists = album
            .AlbumArtist.GroupBy(keySelector: albumArtist => albumArtist.ArtistId)
            .OrderBy(keySelector: artist => artist.Count())
            .ToList();

        int trackCount = album.Tracks;

        int? artistTrackCount = album
            .AlbumTrack.Select(selector: albumTrack => albumTrack.Track)
            .SelectMany(selector: track => track.ArtistTrack)
            .Count();

        bool isAlbumArtist = artistTrackCount >= trackCount * 0.45;

        AlbumArtist = isAlbumArtist ? artists.FirstOrDefault()?.Key : null;
    }

    /// <summary>
    /// A copy for the per-broadcast queue projection with the fields no client
    /// renders from a nested queue-entry album nulled out: the album
    /// <c>color_palette</c> (a full swatch graph) and <c>description</c> (an
    /// unbounded bio). Mirrors the queue-entry lyric strip in
    /// <see cref="MusicPlayerState.CloneForBroadcast"/> — together they are the
    /// bulk of the wire weight a long queue re-broadcasts every ~5s. Returns a
    /// throwaway shallow copy; the stored DTO is never mutated.
    /// </summary>
    public AlbumDto ForBroadcastQueueEntry()
    {
        AlbumDto copy = (AlbumDto)MemberwiseClone();
        copy.ColorPalette = null;
        copy.Description = null;
        return copy;
    }
}
