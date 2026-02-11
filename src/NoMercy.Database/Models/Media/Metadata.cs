using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Media;

[PrimaryKey(nameof(Id))]
[Index(nameof(Filename), nameof(HostFolder), IsUnique = true)]
[Index(nameof(Type))]
[Index(nameof(AudioTrackId), IsUnique = true)]
public class Metadata : MetadataTracks
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

    [JsonProperty("music_size")]
    public long MusicSize => Type == MediaType.Music ? Audio?.Sum(a => a.FileSize) ?? 0 : 0;

    [JsonProperty("other_size")] public long OtherSize => FolderSize - (MovieSize + TvSize + MusicSize);

    [JsonProperty("audio_track_id")] public Guid? AudioTrackId { get; set; }
    public Track AudioTrack { get; set; } = null!;

    [Column("Previews")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _previews { get; set; }

    [NotMapped]
    [JsonProperty("previews")]
    public List<IPreview>? Previews
    {
        get => _previews != null
            ? JsonConvert.DeserializeObject<List<IPreview>>(_previews)
            : null;
        init => _previews = JsonConvert.SerializeObject(value);
    }

    [Column("Fonts")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _fonts { get; set; }

    [NotMapped]
    [JsonProperty("fonts")]
    public List<IFont>? Fonts
    {
        get => _fonts != null
            ? JsonConvert.DeserializeObject<List<IFont>>(_fonts)
            : null;
        init => _fonts = JsonConvert.SerializeObject(value);
    }

    [Column("FontsFile")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _fonts_file { get; set; }

    [NotMapped]
    [JsonProperty("fonts_file")]
    public IFontsFile? FontsFile
    {
        get => _fonts_file != null
            ? JsonConvert.DeserializeObject<IFontsFile>(_fonts_file)
            : null;
        init => _fonts_file = JsonConvert.SerializeObject(value);
    }
    
    [Column("ChaptersFile")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _chapters_file { get; set; }
    
    [NotMapped]
    [JsonProperty("chapters_file")]
    public IChapterFile? ChapterFile
    {
        get => _chapters_file != null
            ? JsonConvert.DeserializeObject<IChapterFile>(_chapters_file)
            : null;
        init => _chapters_file = JsonConvert.SerializeObject(value);
    }
    
    [Column("Chapters")]
    [StringLength(1024)]
    [JsonIgnore]
    // ReSharper disable once InconsistentNaming
    public string? _chapters { get; set; }

    [NotMapped]
    [JsonProperty("chapters")]
    public List<IChapter>? Chapters
    {
        get => _chapters != null
            ? JsonConvert.DeserializeObject<List<IChapter>>(_chapters)
            : null;
        init => _chapters = JsonConvert.SerializeObject(value);
    }

    public long CalculateTotalSize()
    {
        long totalSize = 0;

        if (Video != null) totalSize += Video.Sum(v => v.FileSize ?? 0);

        if (Audio != null) totalSize += Audio.Sum(a => a.FileSize ?? 0);

        if (Subtitles != null) totalSize += Subtitles.Sum(s => s.FileSize ?? 0);

        if (Previews != null) totalSize += Previews.Sum(p => p.ImageFileSize + p.TimeFileSize);

        if (Fonts != null) totalSize += Fonts.Sum(f => f.FileSize ?? 0);

        if (FontsFile != null) totalSize += FontsFile.FileSize ?? 0;

        if (ChapterFile != null) totalSize += ChapterFile.FileSize ?? 0;

        return totalSize;
    }

    private long CalculateVideoSize()
    {
        long totalSize = 0;

        if (Video != null) totalSize += Video.Sum(v => v.FileSize ?? 0);

        if (Audio != null) totalSize += Audio.Sum(a => a.FileSize ?? 0);

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

public class IVideo : IHash
{
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int? Height { get; set; }
    [JsonProperty("codec")] public string? Codec { get; set; }
    [JsonProperty("bit_rate")] public long? BitRate { get; set; }
}

public class IAudio : IHash
{
    [JsonProperty("language")] public string Language { get; set; } = null!;
    [JsonProperty("codec")] public string? Codec { get; set; }
    [JsonProperty("bit_rate")] public long? BitRate { get; set; }
    [JsonProperty("channels")] public long? Channels { get; set; }
    [JsonProperty("channel_layout")] public string? ChannelLayout { get; set; }
    [JsonProperty("sample_rate")] public long? SampleRate { get; set; }
}

public class ISubtitle : IHash
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
    [JsonProperty("file_name")] public string FileName { get; set; } = null!;
    [JsonProperty("file_hash")] public string? FileHash { get; set; } = null!;
    [JsonProperty("file_size")] public long? FileSize { get; set; }
}

public class IFont : IHash
{
}

public class IFontsFile : IHash
{
}

public class IChapter
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("start_time")] public int StartTime { get; set; }
    [JsonProperty("end_time")] public int EndTime { get; set; }
    [JsonProperty("title")] public string Title { get; set; } = string.Empty;
}

public class IChapterFile : IHash
{
}
