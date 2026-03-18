using Newtonsoft.Json;

namespace NoMercy.MediaSources.OpticalMedia.Dto;

public class FfProbeData
{
    [JsonProperty("programs")] public Program[] Programs { get; set; } = [];
    [JsonProperty("streams")] public Stream[] Streams { get; set; } = [];
    [JsonProperty("chapters")] public IEnumerable<ChapterData> Chapters { get; set; } = [];
    [JsonProperty("format")] public Format Format { get; set; } = new();
}

public class Format
{
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("nb_streams")] public long NbStreams { get; set; }
    [JsonProperty("nb_programs")] public long NbPrograms { get; set; }
    [JsonProperty("nb_stream_groups")] public long NbStreamGroups { get; set; }
    [JsonProperty("format_name")] public string? FormatName { get; set; }
    [JsonProperty("format_long_name")] public string? FormatLongName { get; set; }
    [JsonProperty("start_time")] public string? StartTime { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }
    [JsonProperty("size")] public long Size { get; set; }
    [JsonProperty("bit_rate")] public long BitRate { get; set; }
    [JsonProperty("probe_score")] public long ProbeScore { get; set; }
}

public class Program
{
    [JsonProperty("program_id")] public long ProgramId { get; set; }
    [JsonProperty("program_num")] public long ProgramNum { get; set; }
    [JsonProperty("nb_streams")] public long NbStreams { get; set; }
    [JsonProperty("pmt_pid")] public long PmtPid { get; set; }
    [JsonProperty("pcr_pid")] public long PcrPid { get; set; }
    [JsonProperty("streams")] public Stream[] Streams { get; set; } = [];
}

public class Stream
{
    [JsonProperty("index")] public long Index { get; set; }
    [JsonProperty("codec_name")] public string? CodecName { get; set; }
    [JsonProperty("codec_long_name")] public string? CodecLongName { get; set; }

    [JsonProperty("profile", NullValueHandling = NullValueHandling.Ignore)]
    public string? Profile { get; set; }

    [JsonProperty("codec_type")] public string? CodecType { get; set; }
    [JsonProperty("codec_tag_string")] public string? CodecTagString { get; set; }
    [JsonProperty("codec_tag")] public string? CodecTag { get; set; }

    [JsonProperty("width", NullValueHandling = NullValueHandling.Ignore)]
    public long? Width { get; set; }

    [JsonProperty("height", NullValueHandling = NullValueHandling.Ignore)]
    public long? Height { get; set; }

    [JsonProperty("coded_width", NullValueHandling = NullValueHandling.Ignore)]
    public long? CodedWidth { get; set; }

    [JsonProperty("coded_height", NullValueHandling = NullValueHandling.Ignore)]
    public long? CodedHeight { get; set; }

    [JsonProperty("closed_captions", NullValueHandling = NullValueHandling.Ignore)]
    public long? ClosedCaptions { get; set; }

    [JsonProperty("film_grain", NullValueHandling = NullValueHandling.Ignore)]
    public long? FilmGrain { get; set; }

    [JsonProperty("has_b_frames", NullValueHandling = NullValueHandling.Ignore)]
    public long? HasBFrames { get; set; }

    [JsonProperty("sample_aspect_ratio", NullValueHandling = NullValueHandling.Ignore)]
    public string? SampleAspectRatio { get; set; }

    [JsonProperty("display_aspect_ratio", NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayAspectRatio { get; set; }

    [JsonProperty("pix_fmt", NullValueHandling = NullValueHandling.Ignore)]
    public string? PixFmt { get; set; }

    [JsonProperty("level", NullValueHandling = NullValueHandling.Ignore)]
    public long? Level { get; set; }

    [JsonProperty("color_range", NullValueHandling = NullValueHandling.Ignore)]
    public string? ColorRange { get; set; }

    [JsonProperty("chroma_location", NullValueHandling = NullValueHandling.Ignore)]
    public string? ChromaLocation { get; set; }

    [JsonProperty("field_order", NullValueHandling = NullValueHandling.Ignore)]
    public string? FieldOrder { get; set; }

    [JsonProperty("refs", NullValueHandling = NullValueHandling.Ignore)]
    public long? Refs { get; set; }

    [JsonProperty("ts_id")] public long TsId { get; set; }
    [JsonProperty("ts_packetsize")] public long TsPacketsize { get; set; }
    [JsonProperty("id")] public string? Id { get; set; }
    [JsonProperty("r_frame_rate")] public string? RFrameRate { get; set; }
    [JsonProperty("avg_frame_rate")] public string? AvgFrameRate { get; set; }
    [JsonProperty("time_base")] public string? TimeBase { get; set; }
    [JsonProperty("start_pts")] public long StartPts { get; set; }
    [JsonProperty("start_time")] public string? StartTime { get; set; }
    [JsonProperty("duration_ts")] public long DurationTs { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }

    [JsonProperty("extradata_size", NullValueHandling = NullValueHandling.Ignore)]
    public long? ExtradataSize { get; set; }

    [JsonProperty("disposition")] public Dictionary<string, long> Disposition { get; set; } = new();

    [JsonProperty("side_data_list", NullValueHandling = NullValueHandling.Ignore)]
    public SideDataList[] SideDataList { get; set; } = [];

    [JsonProperty("sample_fmt", NullValueHandling = NullValueHandling.Ignore)]
    public string? SampleFmt { get; set; }

    [JsonProperty("sample_rate", NullValueHandling = NullValueHandling.Ignore)]
    public long? SampleRate { get; set; }

    [JsonProperty("channels", NullValueHandling = NullValueHandling.Ignore)]
    public long? Channels { get; set; }

    [JsonProperty("channel_layout", NullValueHandling = NullValueHandling.Ignore)]
    public string? ChannelLayout { get; set; }

    [JsonProperty("bits_per_sample", NullValueHandling = NullValueHandling.Ignore)]
    public long? BitsPerSample { get; set; }

    [JsonProperty("initial_padding", NullValueHandling = NullValueHandling.Ignore)]
    public long? InitialPadding { get; set; }

    [JsonProperty("bit_rate", NullValueHandling = NullValueHandling.Ignore)]
    public long? BitRate { get; set; }
}

public class SideDataList
{
    [JsonProperty("side_data_type")] public string? SideDataType { get; set; }
    [JsonProperty("max_bitrate")] public long MaxBitrate { get; set; }
    [JsonProperty("min_bitrate")] public long MinBitrate { get; set; }
    [JsonProperty("avg_bitrate")] public long AvgBitrate { get; set; }
    [JsonProperty("buffer_size")] public long BufferSize { get; set; }
    [JsonProperty("vbv_delay")] public long VbvDelay { get; set; }
}

// Define the JSON structure for deserialization
public class FfprobeResult
{
    public FormatInfo Format { get; set; } = new();
    public List<StreamInfo> Streams { get; set; } = new();
}

public class FormatInfo
{
    public string Duration { get; set; } = string.Empty;
    public string BitRate { get; set; } = string.Empty;
    public string FrameRate { get; set; } = string.Empty;
}

public class StreamInfo
{
    public int Id { get; set; }
    public string CodecName { get; set; } = string.Empty;
    public string CodecType { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Channels { get; set; }
    public int? SampleRate { get; set; }
}