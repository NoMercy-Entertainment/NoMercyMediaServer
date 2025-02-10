
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(Filename), nameof(HostFolder), IsUnique = true)]
[Index(nameof(Type))]
public class Metadata: Timestamps
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Ulid Id { get; init; } = Ulid.NewUlid();
    
    [JsonProperty("type")] public MediaType Type { get; set; }
    [JsonProperty("duration")] public string Duration { get; set; } = string.Empty;
    [JsonProperty("filename")] public string Filename { get; set; } = string.Empty;
    [JsonProperty("folder")] public string Folder { get; set; } = string.Empty;
    [JsonProperty("host_folder")] public string HostFolder { get; set; } = string.Empty;
    
    [JsonProperty("folder_size")] public long FolderSize { get; set; }
    [JsonProperty("movie_size")] public long MovieSize => Type == MediaType.Movie ? CalculateVideoSize() : 0;
    [JsonProperty("show_size")] public long TvSize => Type == MediaType.Tv ? CalculateVideoSize() : 0;
    [JsonProperty("music_size")] public long MusicSize => Type == MediaType.Music ? Audio?.Sum(a => a.FileSize) ?? 0 : 0;
    [JsonProperty("other_size")] public long OtherSize => FolderSize - (MovieSize + TvSize + MusicSize);
    
    [JsonProperty("audio_track_id")] public Guid? AudioTrackId { get; set; }
    public Track AudioTrack { get; set; } = null!;
    
    [Column("Video")]
    [StringLength(1024)]
    [JsonProperty("video")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _video { get; set; }

    [NotMapped]
    public List<IVideo>? Video
    {
        get => _video != null
            ? JsonConvert.DeserializeObject<List<IVideo>>(_video)
            : null;
        init => _video = JsonConvert.SerializeObject(value);
    }
    
    [Column("Audio")]
    [StringLength(1024)]
    [JsonProperty("audio")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _audio { get; set; }

    [NotMapped]
    public List<IAudio>? Audio
    {
        get => _audio != null
            ? JsonConvert.DeserializeObject<List<IAudio>>(_audio)
            : null;
        init => _audio = JsonConvert.SerializeObject(value);
    }
    
    [Column("Subtitles")]
    [StringLength(1024)]
    [JsonProperty("subtitles")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _subtitles { get; set; }

    [NotMapped]
    public List<ISubtitle>? Subtitles
    {
        get => _subtitles != null
            ? JsonConvert.DeserializeObject<List<ISubtitle>>(_subtitles)
            : null;
        init => _subtitles = JsonConvert.SerializeObject(value);
    }
    
    [Column("Previews")]
    [StringLength(1024)]
    [JsonProperty("previews")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _previews { get; set; }

    [NotMapped]
    public List<IPreview>? Previews
    {
        get => _previews != null
            ? JsonConvert.DeserializeObject<List<IPreview>>(_previews)
            : null;
        init => _previews = JsonConvert.SerializeObject(value);
    }
    
    [Column("Fonts")]
    [StringLength(1024)]
    [JsonProperty("fonts")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _fonts { get; set; }

    [NotMapped]
    public List<IFont>? Fonts
    {
        get => _fonts != null
            ? JsonConvert.DeserializeObject<List<IFont>>(_fonts)
            : null;
        init => _fonts = JsonConvert.SerializeObject(value);
    }
    
    [Column("FontsFile")]
    [StringLength(1024)]
    [JsonProperty("fonts_file")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _fonts_file { get; set; }

    [NotMapped]
    public IFontsFile? FontsFile
    {
        get => _fonts_file != null
            ? JsonConvert.DeserializeObject<IFontsFile>(_fonts_file)
            : null;
        init => _fonts_file = JsonConvert.SerializeObject(value);
    }
    
    [Column("ChaptersFile")]
    [StringLength(1024)]
    [JsonProperty("chapters_file")]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _chapters_file { get; set; }

    [NotMapped]
    public IChaptersFile? Chapters
    {
        get => _chapters_file != null
            ? JsonConvert.DeserializeObject<IChaptersFile>(_chapters_file)
            : null;
        init => _chapters_file = JsonConvert.SerializeObject(value);
    }

    public long CalculateTotalSize()
    {
        long totalSize = 0;

        if (Video != null)
        {
            totalSize += Video.Sum(v => v.FileSize);
        }

        if (Audio != null)
        {
            totalSize += Audio.Sum(a => a.FileSize);
        }

        if (Subtitles != null)
        {
            totalSize += Subtitles.Sum(s => s.FileSize);
        }

        if (Previews != null)
        {
            totalSize += Previews.Sum(p => p.ImageFileSize + p.TimeFileSize);
        }

        if (Fonts != null)
        {
            totalSize += Fonts.Sum(f => f.FileSize);
        }

        if (FontsFile != null)
        {
            totalSize += FontsFile.FileSize;
        }

        if (Chapters != null)
        {
            totalSize += Chapters.FileSize;
        }

        return totalSize;
    }

    private long CalculateVideoSize()
    {
        long totalSize = 0;

        if (Video != null)
        {
            totalSize += Video.Sum(v => v.FileSize);
        }

        if (Audio != null)
        {
            totalSize += Audio.Sum(a => a.FileSize);
        }

        return totalSize;
    }
}

public enum MediaType
{
    Movie,
    Tv,
    Music,
    Other
}

public class IVideo: IHash
{
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("codec")] public string? Codec { get; set; }
    [JsonProperty("bit_rate")] public long BitRate { get; set; }
}

public class IAudio: IHash
{
    [JsonProperty("language")] public string? Language { get; set; }
    [JsonProperty("codec")] public string? Codec { get; set; }
    [JsonProperty("bit_rate")] public long BitRate { get; set; }
    [JsonProperty("channels")] public int Channels { get; set; }
    [JsonProperty("channel_layout")] public string? ChannelLayout { get; set; }
    [JsonProperty("sample_rate")] public int SampleRate { get; set; }
}

public class ISubtitle: IHash
{
    [JsonProperty("language")] public string? Language { get; set; }
    [JsonProperty("codec")] public string? Codec { get; set; }
    [MaxLength(10)]
    [JsonProperty("type")] public string? Type { get; set; }
}

public class IPreview
{
    [JsonProperty("width")] public int? Width { get; set; }
    [JsonProperty("height")] public int? Height { get; set; }
    
    [JsonProperty("image_file_name")] public string? ImageFileName { get; set; }
    [JsonProperty("image_file_hash")] public string? ImageFileHash { get; set; }
    [JsonProperty("image_file_size")] public long ImageFileSize { get; set; }
    
    [JsonProperty("time_file_name")] public string? TimeFileName { get; set; }
    [JsonProperty("time_file_hash")] public string? TimeFileHash { get; set; }
    [JsonProperty("time_file_size")] public long TimeFileSize { get; set; }
}

public class IHash
{
    [JsonProperty("file_name")] public string? FileName { get; set; }
    [JsonProperty("file_hash")] public string? FileHash { get; set; }
    [JsonProperty("file_size")] public long FileSize { get; set; }
}

public class IFont: IHash{}

public class IFontsFile: IHash{}

public class IChaptersFile: IHash{}