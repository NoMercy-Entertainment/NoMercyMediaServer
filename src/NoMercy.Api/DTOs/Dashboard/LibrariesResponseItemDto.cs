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
using NoMercy.Data.DTOs;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Api.DTOs.Dashboard;

public record LibrariesResponseItemDto
{
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; set; }

    [JsonProperty(propertyName: "autoRefreshInterval")]
    public long AutoRefreshInterval { get; set; }

    [JsonProperty(propertyName: "chapterImages")]
    public long ChapterImages { get; set; }

    [JsonProperty(propertyName: "image")]
    public string? Image { get; set; }

    [JsonProperty(propertyName: "perfectSubtitleMatch")]
    public bool PerfectSubtitleMatch { get; set; }

    [JsonProperty(propertyName: "realtime")]
    public bool Realtime { get; set; }

    [JsonProperty(propertyName: "autoEncodeOnScan")]
    public bool AutoEncodeOnScan { get; set; }

    [JsonProperty(propertyName: "encodePresetId")]
    public Ulid? EncodePresetId { get; set; }

    [JsonProperty(propertyName: "specialSeasonName")]
    public string? SpecialSeasonName { get; set; }

    [JsonProperty(propertyName: "title")]
    public string? Title { get; set; }

    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }

    [JsonProperty(propertyName: "order")]
    public int? Order { get; set; }

    [JsonProperty(propertyName: "color_palette")]
    public ColorPalette? ColorPalette { get; set; }

    [JsonProperty(propertyName: "pagination")]
    public string Pagination { get; set; } = "auto";

    [JsonProperty(propertyName: "link")]
    public Uri Link { get; set; } = null!;

    [JsonProperty(propertyName: "created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty(propertyName: "updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonProperty(propertyName: "folder_library")]
    public FolderLibraryDto[] FolderLibrary { get; set; }

    [JsonProperty(propertyName: "subtitles")]
    public string[] Subtitles { get; set; }

    public LibrariesResponseItemDto(Library library)
    {
        bool shouldPaginate = library.LibraryMovies.Count + library.LibraryTvs.Count > 500;
        Id = library.Id;
        AutoRefreshInterval = library.AutoRefreshInterval;
        Image = library.Image;
        PerfectSubtitleMatch = library.PerfectSubtitleMatch;
        Realtime = library.Realtime;
        AutoEncodeOnScan = library.AutoEncodeOnScan;
        EncodePresetId = library.EncodePresetId;
        SpecialSeasonName = library.SpecialSeasonName;
        Title = library.Title;
        Type = library.Type;
        Order = library.Order;
        CreatedAt = library.CreatedAt;
        Pagination = shouldPaginate ? "letter" : "auto";
        Link = shouldPaginate
            ? new(uriString: $"/libraries/{Id}/letter/A", uriKind: UriKind.Relative)
            : new(uriString: $"/libraries/{Id}", uriKind: UriKind.Relative);
        Subtitles = library
            .LanguageLibraries.Select(selector: languageLibrary => languageLibrary.Language.Iso6391)
            .ToArray();

        FolderLibrary = library
            .FolderLibraries.Select(selector: folderLibrary => new FolderLibraryDto
            {
                FolderId = folderLibrary.FolderId,
                LibraryId = folderLibrary.LibraryId,
                Folder = new()
                {
                    Id = folderLibrary.Folder.Id,
                    Path = folderLibrary.Folder.Path,
                    DriverId = folderLibrary.Folder.DriverId,
                    DriverName = folderLibrary.Folder.Driver?.Name ?? string.Empty,
                    EncoderProfiles = folderLibrary
                        .Folder.EncodingPresetFolders.Where(predicate: link => link.Preset is not null)
                        .Select(selector: link => new Data.DTOs.Encoder.FolderPresetDto
                        {
                            Id = link.Preset!.Id,
                            Name = link.Preset!.Name,
                            // See FolderDto — no Container column on the preset row.
                            Container = string.Empty,
                        })
                        .ToArray(),
                },
            })
            .ToArray();
    }
}
