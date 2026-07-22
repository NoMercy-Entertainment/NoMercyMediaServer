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
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;

namespace NoMercy.Api.DTOs.Music;

public record AlbumsResponseItemDto
{
    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "backdrop")]
    public string? Backdrop { get; set; }

    [JsonProperty(propertyName: "cover")]
    public string? Cover { get; set; }

    [JsonProperty(propertyName: "disambiguation")]
    public string? Disambiguation { get; set; }

    [JsonProperty(propertyName: "description")]
    public string? Description { get; set; }

    [JsonProperty(propertyName: "folder")]
    public string? Folder { get; set; }

    [JsonProperty(propertyName: "id")]
    public Guid Id { get; set; }

    [JsonProperty(propertyName: "name")]
    public string Name { get; set; }

    [JsonProperty(propertyName: "track_id")]
    public string? TrackId { get; set; }

    [JsonProperty(propertyName: "type")]
    public string Type { get; set; }

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; }

    [JsonProperty(propertyName: "tracks")]
    public int Tracks { get; set; }

    public AlbumsResponseItemDto(Album album, string? country = "US")
    {
        string? description = album
            .Translations.FirstOrDefault(predicate: translation => translation.Iso31661 == country)
            ?.Description;
        Image? img = album.Images.FirstOrDefault(predicate: image => image.Type == "background");

        Description = !string.IsNullOrEmpty(value: description) ? description : album.Description;

        Backdrop = !string.IsNullOrEmpty(value: img?.FilePath)
            ? new Uri(uriString: $"/images/music{img.FilePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Cover = !string.IsNullOrEmpty(value: album.Cover)
            ? new Uri(uriString: $"/images/music{album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        ColorPalette = album.ColorPalette;
        if (ColorPalette is not null)
            ColorPalette.Backdrop = img?.ColorPalette?.Image;
        Disambiguation = album.Disambiguation;
        Folder = album.Folder;
        Id = album.Id;
        Name = album.Name;
        Type = "album";
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);

        Tracks = album
            .AlbumTrack.Select(selector: albumTrack => albumTrack.Track)
            .Count(predicate: albumTrack => albumTrack.Duration != null);
    }

    public AlbumsResponseItemDto(AlbumCardDto album)
    {
        Description = !string.IsNullOrEmpty(value: album.TranslatedDescription)
            ? album.TranslatedDescription
            : album.Description;

        Backdrop = !string.IsNullOrEmpty(value: album.BackgroundImagePath)
            ? new Uri(uriString: $"/images/music{album.BackgroundImagePath}", uriKind: UriKind.Relative).ToString()
            : null;
        Cover = !string.IsNullOrEmpty(value: album.Cover)
            ? new Uri(uriString: $"/images/music{album.Cover}", uriKind: UriKind.Relative).ToString()
            : null;
        ColorPalette = ColorPalette.FromJsonOrNull(json: album.ColorPalette);
        if (ColorPalette is not null)
        {
            ColorPalette? bgPalette = ColorPalette.FromJsonOrNull(
                json: album.BackgroundImageColorPalette
            );
            ColorPalette.Backdrop = bgPalette?.Image;
        }
        Disambiguation = album.Disambiguation;
        Folder = album.Folder;
        Id = album.Id;
        Name = album.Name;
        Type = "album";
        Link = new(uriString: $"/music/albums/{Id}", uriKind: UriKind.Relative);
        Tracks = album.TrackCount;
    }
}
