using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Encoder.Dto;

[Serializable]
public class FfprobeSourceData
{
    [JsonProperty("streams")] public FfprobeSourceDataStream[] Streams { get; set; } = [];
    [JsonProperty("chapters")] public FfprobeSourceDataChapter[] Chapters { get; set; } = [];
    [JsonProperty("format")] public FfprobeSourceDataFormat Format { get; set; } = new();
}

public class FfprobeSourceDataChapter
{
    [JsonProperty("id")] public double Id { get; set; }
    [JsonProperty("time_base")] public string? TimeBase { get; set; }
    [JsonProperty("start")] public long Start { get; set; }
    [JsonProperty("start_time")] public double StartTime { get; set; }
    [JsonProperty("end")] public long End { get; set; }
    [JsonProperty("end_time")] public double EndTime { get; set; }
    [JsonProperty("tags")] public FfprobeSourceDataChapterTags Tags { get; set; } = new();
}

public class FfprobeSourceDataChapterTags
{
    [JsonProperty("title")] public string? Title { get; set; }
}

public class FfprobeSourceDataFormat
{
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("nb_streams")] public long NbStreams { get; set; }
    [JsonProperty("nb_programs")] public long NbPrograms { get; set; }
    [JsonProperty("nb_stream_groups")] public long NbStreamGroups { get; set; }
    [JsonProperty("format_name")] public string? FormatName { get; set; }
    [JsonProperty("format_long_name")] public string? FormatLongName { get; set; }
    [JsonProperty("start_time")] public string? StartTime { get; set; }
    [JsonProperty("duration")] public TimeSpan? Duration { get; set; }
    [JsonProperty("size")] public string? Size { get; set; }
    [JsonProperty("bit_rate")] public long BitRate { get; set; }
    [JsonProperty("probe_score")] public long ProbeScore { get; set; }
    [JsonProperty("tags")] public Dictionary<string,string> Tags { get; set; } = new();
}

public class FfprobeSourceDataFormatTags
{
    [JsonProperty("encoder")] public string? Encoder { get; set; }
}

public class FfprobeSourceDataStream
{
    [JsonProperty("index")] public int Index { get; set; }
    [JsonProperty("codec_name")] public string? CodecName { get; set; }
    [JsonProperty("codec_long_name")] public string? CodecLongName { get; set; }
    [JsonProperty("profile")] public string? Profile { get; set; }

    private CodecType _codecType;
    [JsonProperty("codec_type")]
    public CodecType CodecType
    {
        get => _codecType == CodecType.Video && CodecName == "mjpeg"
            ? CodecType.Image
            : _codecType;
        set => _codecType = value;
    }

    [JsonProperty("codec_tag_string")] public string? CodecTagString { get; set; }
    [JsonProperty("codec_tag")] public string? CodecTag { get; set; }
    [JsonProperty("width")] public int Width { get; set; }
    [JsonProperty("height")] public int Height { get; set; }
    [JsonProperty("coded_width")] public int CodedWidth { get; set; }
    [JsonProperty("coded_height")] public int CodedHeight { get; set; }
    [JsonProperty("closed_captions")] public bool? ClosedCaptions { get; set; }
    [JsonProperty("film_grain")] public bool? FilmGrain { get; set; }
    [JsonProperty("has_b_frames")] public bool? HasBFrames { get; set; }
    [JsonProperty("sample_aspect_ratio")] public string? SampleAspectRatio { get; set; }
    [JsonProperty("display_aspect_ratio")] public string? DisplayAspectRatio { get; set; }
    [JsonProperty("pix_fmt")] public string? PixFmt { get; set; }
    [JsonProperty("level")] public long? Level { get; set; }
    [JsonProperty("color_range")] public string? ColorRange { get; set; }
    [JsonProperty("color_space")] public string? ColorSpace { get; set; }
    [JsonProperty("color_transfer")] public string? ColorTransfer { get; set; }
    [JsonProperty("color_primaries")] public string? ColorPrimaries { get; set; }
    [JsonProperty("chroma_location")] public string? ChromaLocation { get; set; }
    [JsonProperty("refs")] public long? Refs { get; set; }
    [JsonProperty("view_ids_available")] public string? ViewIdsAvailable { get; set; }
    [JsonProperty("view_pos_available")] public string? ViewPosAvailable { get; set; }
    [JsonProperty("is_avc")] public bool IsAvc { get; set; }
    [JsonProperty("r_frame_rate")] public string? RFrameRate { get; set; }
    [JsonProperty("avg_frame_rate")] public string? AvgFrameRate { get; set; }
    [JsonProperty("time_base")] public string? TimeBase { get; set; }
    [JsonProperty("start_pts")] public long StartPts { get; set; }
    [JsonProperty("start_time")] public double StartTime { get; set; }
    [JsonProperty("extradata_size")] public int ExtradataSize { get; set; }
    [JsonProperty("disposition")] public Dictionary<string, int> Disposition { get; set; } = new();
    [JsonProperty("tags")] public Dictionary<string,string> Tags { get; set; } = new();
    [JsonProperty("sample_fmt")] public string? SampleFmt { get; set; }
    [JsonProperty("sample_rate")] public long? SampleRate { get; set; }
    [JsonProperty("channels")] public long? Channels { get; set; }
    [JsonProperty("channel_layout")] public string? ChannelLayout { get; set; }
    [JsonProperty("bits_per_sample")] public long? BitsPerSample { get; set; }
    [JsonProperty("duration_ts")] public long? DurationTs { get; set; }
    [JsonProperty("duration")] public double Duration { get; set; }
    [JsonProperty("bit_rate")]  public long? BitRate { get; set; }
}

public class FfprobeSourceDataStreamTags
{
    [JsonProperty("title")] public string? Title { get; set; }
    [JsonProperty("language")] public string? Language { get; set; }
    [JsonProperty("BPS")] public long Bps { get; set; }
    [JsonProperty("DURATION")] public string? Duration { get; set; }
    [JsonProperty("NUMBER_OF_FRAMES")] public long NumberOfFrames { get; set; }
    public long NumberOfBytes { get; set; }
    [JsonProperty("_STATISTICS_WRITING_APP")] public string? StatisticsWritingApp { get; set; }
    [JsonProperty("_STATISTICS_TAGS")] public string? StatisticsTags { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("mimetype")] public string? MimeType { get; set; }
    
    [JsonExtensionData]
    private IDictionary<string, JToken> _additionalData { get; set; } = new Dictionary<string, JToken>();

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        // Look for exact matches first
        if (_additionalData.TryGetValue("NUMBER_OF_BYTES", out JToken? value1))
        {
            NumberOfBytes = value1.ToObject<long>();
        }
        else if (_additionalData.TryGetValue("NumberOfBytes", out JToken? value2))
        {
            NumberOfBytes = value2.ToObject<long>();
        }
        else
        {
            string? numberBytesKey = _additionalData.Keys
                .FirstOrDefault(key => key.StartsWith("NUMBER_OF_BYTES", StringComparison.OrdinalIgnoreCase));
        
            if (!string.IsNullOrEmpty(numberBytesKey) && 
                _additionalData.TryGetValue(numberBytesKey, out JToken? value3))
            {
                NumberOfBytes = value3.ToObject<long>();
            }
        }
    }
}

public class FfprobeSourceMusicBrainz
{
    [JsonProperty("release_id")] public Guid ReleaseId { get; set; }
    [JsonProperty("artist_id")] public Guid ArtistId { get; set; }
    [JsonProperty("release_artist_id")] public Guid ReleaseArtistId { get; set; }
    [JsonProperty("recording_id")] public Guid RecordingId { get; set; }
    [JsonProperty("release_track_id")] public Guid ReleaseTrackId { get; set; }
}