using Newtonsoft.Json;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Service.Seeds.Data;

public static class EncoderProfileSeedData
{
    public static List<EncoderProfile> GetEncoderProfiles()
    {
        return
        [
            MakeProfile(
                "01HQ6298ZSZYKJT83WDWTPG4G8",
                "Marvel 4k",
                OutputFormat.Hls,
                [
                    Video(3840, null, 24000, 20, "fast", "main10", "5.1", false, true),
                    Video(1920, null, 10656, 20, "fast", "main10", "4.0", false, true),
                    Video(3840, null, 20000, 20, "fast", "high", "5.1", true, false),
                    Video(1920, null, 8695, 20, "fast", "high", "4.0", true, false),
                ],
                [
                    Audio(AudioCodecType.Aac, 192, 2, 48000),
                    Audio(AudioCodecType.Eac3, 640, 6, 48000),
                ],
                [Sub(SubtitleCodecType.WebVtt), Sub(SubtitleCodecType.Ass)]
            ),
            MakeProfile(
                "01HQ629JAYQDEQAH0GW3ZHGW8Z",
                "1080p high",
                OutputFormat.Hls,
                [Video(1920, null, 10656, 20, "fast", "high", "4.0", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt)]
            ),
            MakeProfile(
                "01HQ629SJ32FTV2Q46NX3H1CK9",
                "1080p regular",
                OutputFormat.Hls,
                [Video(1920, null, 8695, 22, "fast", "high", "4.0", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt), Sub(SubtitleCodecType.Ass)]
            ),
            MakeProfile(
                "01HR360AKTW47XC6ZQ2V9DF024",
                "1080p low",
                OutputFormat.Hls,
                [Video(1920, null, 6956, 24, "fast", "high", "4.0", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt), Sub(SubtitleCodecType.Ass)]
            ),
            MakeProfile(
                "01JRH6Q85QT0D08F9J9577J04K",
                "Music",
                OutputFormat.Mp4,
                [],
                [Audio(AudioCodecType.Mp3, 192, 2, 44100)],
                []
            ),
            MakeProfile(
                "01JRH6Q8A5T0D08F9J9577J04K",
                "HD Streaming (720p)",
                OutputFormat.Hls,
                [Video(1280, null, 4000, 23, "fast", "high", "4.0", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt)]
            ),
            MakeProfile(
                "01JRH6Q8B5T0D08F9J9577J04K",
                "Full HD Streaming (1080p)",
                OutputFormat.Hls,
                [Video(1920, null, 8000, 22, "fast", "high", "4.0", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt), Sub(SubtitleCodecType.Ass)]
            ),
            MakeProfile(
                "01JRH6Q8C5T0D08F9J9577J04K",
                "4K Streaming",
                OutputFormat.Hls,
                [Video(3840, null, 18000, 19, "fast", "high", "5.1", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt), Sub(SubtitleCodecType.Ass)]
            ),
            MakeProfile(
                "01JRH6Q8D5T0D08F9J9577J04K",
                "MP4 Standard (1080p)",
                OutputFormat.Mp4,
                [Video(1920, null, 8000, 21, "fast", "high", "4.0", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt)]
            ),
            MakeProfile(
                "01JRH6Q8E5T0D08F9J9577J04K",
                "MP4 High Quality (4K)",
                OutputFormat.Mp4,
                [Video(3840, null, 18000, 18, "fast", "high", "5.1", true, false)],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                [Sub(SubtitleCodecType.WebVtt)]
            ),
            MakeProfile(
                "01JRH6Q8F5T0D08F9J9577J04K",
                "MP3 High Quality (320kbps)",
                OutputFormat.Mp4,
                [],
                [Audio(AudioCodecType.Mp3, 320, 2, 48000)],
                []
            ),
            MakeProfile(
                "01JRH6Q8G5T0D08F9J9577J04K",
                "MP3 Standard (192kbps)",
                OutputFormat.Mp4,
                [],
                [Audio(AudioCodecType.Mp3, 192, 2, 44100)],
                []
            ),
            MakeProfile(
                "01JRH6Q8H5T0D08F9J9577J04K",
                "FLAC Lossless",
                OutputFormat.Mkv,
                [],
                [Audio(AudioCodecType.Flac, 0, 2, 48000)],
                []
            ),
            MakeProfile(
                "01JRH6Q8I5T0D08F9J9577J04K",
                "AAC Standard",
                OutputFormat.Mp4,
                [],
                [Audio(AudioCodecType.Aac, 192, 2, 48000)],
                []
            ),
            MakeProfile(
                "01JRH6Q8J5T0D08F9J9577J04K",
                "Opus (Streaming Audio)",
                OutputFormat.Mp4,
                [],
                [Audio(AudioCodecType.Opus, 128, 2, 48000)],
                []
            ),
            MakeProfile(
                "01JRH6Q8K5T0D08F9J9577J04K",
                "Archival HEVC (H.265)",
                OutputFormat.Mkv,
                [
                    Video(
                        3840,
                        null,
                        0,
                        20,
                        "slow",
                        "main10",
                        "5.1",
                        false,
                        true,
                        VideoCodecType.H265
                    ),
                ],
                [Audio(AudioCodecType.Flac, 0, 2, 48000)],
                [Sub(SubtitleCodecType.Ass)]
            ),
            MakeProfile(
                "01JRH6Q8L5T0D08F9J9577J04K",
                "Archival AV1",
                OutputFormat.Mkv,
                [
                    Video(
                        3840,
                        null,
                        0,
                        26,
                        "slow",
                        "main10",
                        "5.1",
                        false,
                        true,
                        VideoCodecType.Av1
                    ),
                ],
                [Audio(AudioCodecType.Flac, 0, 2, 48000)],
                [Sub(SubtitleCodecType.Ass)]
            ),
        ];
    }

    private static EncoderProfile MakeProfile(
        string ulidStr,
        string name,
        OutputFormat format,
        VideoOutput[] video,
        AudioOutput[] audio,
        SubtitleOutput[] subtitles
    )
    {
        EncodingProfile v3Profile = new(
            Id: Ulid.Parse(ulidStr),
            Name: name,
            Format: format,
            VideoOutputs: video,
            AudioOutputs: audio,
            SubtitleOutputs: subtitles
        );

        string containerName = format switch
        {
            OutputFormat.Hls => "m3u8",
            OutputFormat.Mkv => "mkv",
            OutputFormat.Mp4 => "mp4",
            OutputFormat.Dash => "dash",
            _ => "mp4",
        };

        return new EncoderProfile
        {
            Id = Ulid.Parse(ulidStr),
            Name = name,
            Container = containerName,
            Param = JsonConvert.SerializeObject(v3Profile),
            EncoderProfileFolder = [],
        };
    }

    private static VideoOutput Video(
        int width,
        int? height,
        int bitrateKbps,
        int crf,
        string preset,
        string profile,
        string level,
        bool convertHdrToSdr,
        bool tenBit,
        VideoCodecType codec = VideoCodecType.H264
    )
    {
        return new VideoOutput(
            Codec: codec,
            Width: width,
            Height: height,
            BitrateKbps: bitrateKbps,
            Crf: crf,
            Preset: preset,
            Profile: profile,
            Level: level,
            ConvertHdrToSdr: convertHdrToSdr,
            KeyframeIntervalSeconds: 2,
            TenBit: tenBit
        );
    }

    private static AudioOutput Audio(
        AudioCodecType codec,
        int bitrateKbps,
        int channels,
        int sampleRateHz
    )
    {
        return new AudioOutput(
            Codec: codec,
            BitrateKbps: bitrateKbps,
            Channels: channels,
            SampleRateHz: sampleRateHz,
            AllowedLanguages: [],
            Loudness: LoudnessMode.None
        );
    }

    private static SubtitleOutput Sub(SubtitleCodecType codec)
    {
        return new SubtitleOutput(Codec: codec, Mode: SubtitleMode.Extract, AllowedLanguages: []);
    }
}
