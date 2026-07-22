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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database.Infrastructure;

namespace NoMercy.Database.Models.Media;

[PrimaryKey(propertyName: nameof(Id))]
[Index(propertyName: nameof(Filename), additionalPropertyNames: nameof(HostFolder), IsUnique = true)]
[Index(propertyName: nameof(Type))]
[Index(propertyName: nameof(AudioTrackId), IsUnique = true)]
public class Metadata : MetadataTracks
{
    [DatabaseGenerated(databaseGeneratedOption: DatabaseGeneratedOption.None)]
    [JsonProperty(propertyName: "id")]
    public Ulid Id { get; init; } = Ulid.NewUlid();

    [JsonProperty(propertyName: "type")]
    public MediaType Type { get; set; }

    [JsonProperty(propertyName: "duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonProperty(propertyName: "filename")]
    public string Filename
    {
        get;
        set => field = PathNormalizer.Normalize(value: value);
    } = string.Empty;

    [JsonProperty(propertyName: "folder")]
    public string Folder
    {
        get;
        set => field = PathNormalizer.Normalize(value: value);
    } = string.Empty;

    [JsonProperty(propertyName: "host_folder")]
    public string HostFolder
    {
        get;
        set => field = PathNormalizer.Normalize(value: value);
    } = string.Empty;

    [JsonProperty(propertyName: "folder_size")]
    public long FolderSize { get; set; }

    [JsonProperty(propertyName: "movie_size")]
    public long MovieSize => Type == MediaType.Movie ? CalculateVideoSize() : 0;

    [JsonProperty(propertyName: "show_size")]
    public long TvSize => Type == MediaType.Tv ? CalculateVideoSize() : 0;

    [JsonProperty(propertyName: "music_size")]
    public long MusicSize => Type == MediaType.Music ? Audio?.Sum(selector: a => a.FileSize) ?? 0 : 0;

    [JsonProperty(propertyName: "other_size")]
    public long OtherSize => FolderSize - (MovieSize + TvSize + MusicSize);

    [JsonProperty(propertyName: "audio_track_id")]
    public Guid? AudioTrackId { get; set; }
    public Track AudioTrack { get; set; } = null!;

    [Column(name: "Previews")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _previews { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "previews")]
    public List<IPreview>? Previews
    {
        get => _previews != null ? JsonConvert.DeserializeObject<List<IPreview>>(value: _previews) : null;
        init => _previews = JsonConvert.SerializeObject(value: value);
    }

    [Column(name: "Fonts")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _fonts { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "fonts")]
    public List<IFont>? Fonts
    {
        get => _fonts != null ? JsonConvert.DeserializeObject<List<IFont>>(value: _fonts) : null;
        init => _fonts = JsonConvert.SerializeObject(value: value);
    }

    [Column(name: "FontsFile")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _fonts_file { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "fonts_file")]
    public IFontsFile? FontsFile
    {
        get => _fonts_file != null ? JsonConvert.DeserializeObject<IFontsFile>(value: _fonts_file) : null;
        init => _fonts_file = JsonConvert.SerializeObject(value: value);
    }

    [Column(name: "ChaptersFile")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _chapters_file { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "chapters_file")]
    public IChapterFile? ChapterFile
    {
        get =>
            _chapters_file != null
                ? JsonConvert.DeserializeObject<IChapterFile>(value: _chapters_file)
                : null;
        init => _chapters_file = JsonConvert.SerializeObject(value: value);
    }

    [Column(name: "Chapters")]
    [StringLength(maximumLength: 1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _chapters { get; set; }

    [NotMapped]
    [JsonProperty(propertyName: "chapters")]
    public List<IChapter>? Chapters
    {
        get => _chapters != null ? JsonConvert.DeserializeObject<List<IChapter>>(value: _chapters) : null;
        init => _chapters = JsonConvert.SerializeObject(value: value);
    }

    public long CalculateTotalSize()
    {
        long totalSize = 0;

        if (Video != null)
            totalSize += Video.Sum(selector: v => v.FileSize ?? 0);

        if (Audio != null)
            totalSize += Audio.Sum(selector: a => a.FileSize ?? 0);

        if (Subtitles != null)
            totalSize += Subtitles.Sum(selector: s => s.FileSize ?? 0);

        if (Previews != null)
            totalSize += Previews.Sum(selector: p => p.ImageFileSize + p.TimeFileSize);

        if (Fonts != null)
            totalSize += Fonts.Sum(selector: f => f.FileSize ?? 0);

        if (FontsFile != null)
            totalSize += FontsFile.FileSize ?? 0;

        if (ChapterFile != null)
            totalSize += ChapterFile.FileSize ?? 0;

        return totalSize;
    }

    private long CalculateVideoSize()
    {
        long totalSize = 0;

        if (Video != null)
            totalSize += Video.Sum(selector: v => v.FileSize ?? 0);

        if (Audio != null)
            totalSize += Audio.Sum(selector: a => a.FileSize ?? 0);

        return totalSize;
    }
}

public enum MediaType
{
    Movie,
    Tv,
    Music,
    Other,
}

public class IVideo : IHash
{
    [JsonProperty(propertyName: "width")]
    public int Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public int? Height { get; set; }

    [JsonProperty(propertyName: "codec")]
    public string? Codec { get; set; }

    [JsonProperty(propertyName: "bit_rate")]
    public long? BitRate { get; set; }
}

public class IAudio : IHash
{
    [JsonProperty(propertyName: "language")]
    public string Language { get; set; } = null!;

    [JsonProperty(propertyName: "codec")]
    public string? Codec { get; set; }

    [JsonProperty(propertyName: "bit_rate")]
    public long? BitRate { get; set; }

    [JsonProperty(propertyName: "channels")]
    public long? Channels { get; set; }

    [JsonProperty(propertyName: "channel_layout")]
    public string? ChannelLayout { get; set; }

    [JsonProperty(propertyName: "sample_rate")]
    public long? SampleRate { get; set; }
}

public class ISubtitle : IHash
{
    [JsonProperty(propertyName: "language")]
    public string? Language { get; set; }

    [JsonProperty(propertyName: "codec")]
    public string? Codec { get; set; }

    [MaxLength(length: 10)]
    [JsonProperty(propertyName: "type")]
    public string? Type { get; set; }
}

public class IPreview
{
    [JsonProperty(propertyName: "width")]
    public int? Width { get; set; }

    [JsonProperty(propertyName: "height")]
    public int? Height { get; set; }

    [JsonProperty(propertyName: "image_file_name")]
    public string? ImageFileName { get; set; }

    [JsonProperty(propertyName: "image_file_hash")]
    public string? ImageFileHash { get; set; }

    [JsonProperty(propertyName: "image_file_size")]
    public long ImageFileSize { get; set; }

    [JsonProperty(propertyName: "time_file_name")]
    public string? TimeFileName { get; set; }

    [JsonProperty(propertyName: "time_file_hash")]
    public string? TimeFileHash { get; set; }

    [JsonProperty(propertyName: "time_file_size")]
    public long TimeFileSize { get; set; }
}

public class IHash
{
    [JsonProperty(propertyName: "file_name")]
    public string FileName { get; set; } = null!;

    [JsonProperty(propertyName: "file_hash")]
    public string? FileHash { get; set; }

    [JsonProperty(propertyName: "file_size")]
    public long? FileSize { get; set; }
}

public class IFont : IHash { }

public class IFontsFile : IHash { }

public class IChapter
{
    [JsonProperty(propertyName: "id")]
    public int Id { get; set; }

    [JsonProperty(propertyName: "start_time")]
    public int StartTime { get; set; }

    [JsonProperty(propertyName: "end_time")]
    public int EndTime { get; set; }

    [JsonProperty(propertyName: "title")]
    public string Title { get; set; } = string.Empty;
}

public class IChapterFile : IHash { }
