using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

/// <summary>
/// Seeds the built-in shareable preset library. Matches Handbrake's approach
/// — users see "General", "Web", "Matroska", etc. out of the box, and can
/// duplicate any built-in into an editable preset.
///
/// Built-in presets carry a deterministic Ulid so repeat seeding upserts
/// instead of duplicating. IsBuiltIn = true prevents accidental deletion
/// via the dashboard API.
/// </summary>
public static class EncodingPresetsSeed
{
    public static async Task Init(MediaContext context)
    {
        Logger.Setup("Adding Encoding Presets", LogEventLevel.Verbose);

        try
        {
            EncodingPreset[] presets = BuildBuiltInPresets();

            await context
                .EncodingPresets.UpsertRange(presets)
                .On(p => p.Id)
                .WhenMatched(
                    (existing, incoming) =>
                        new()
                        {
                            Id = incoming.Id,
                            Name = incoming.Name,
                            Description = incoming.Description,
                            Author = incoming.Author,
                            Tags = incoming.Tags,
                            ProfileJson = incoming.ProfileJson,
                            ParentPresetId = incoming.ParentPresetId,
                            IsBuiltIn = incoming.IsBuiltIn,
                            UpdatedAt = incoming.UpdatedAt,
                            CreatedAt = existing.CreatedAt,
                        }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }

    private static EncodingPreset[] BuildBuiltInPresets()
    {
        // Deterministic Ulids so the seed is idempotent. Same binary layout
        // every run, same rows — upsert maps them to their existing records.
        Ulid generalId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
        Ulid webHighId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 });
        Ulid archivalId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3 });
        Ulid animeId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4 });
        Ulid musicId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5 });

        return
        [
            new EncodingPreset
            {
                Id = generalId,
                Name = "General — 1080p Fast",
                Description = "Balanced streaming preset: H.264 1080p, medium preset, CRF 23.",
                Author = "NoMercy",
                Tags = "general,1080p,hls",
                IsBuiltIn = true,
                ProfileJson = ProfileJsonTemplate(
                    name: "General 1080p",
                    width: 1920,
                    height: 1080,
                    crf: 23,
                    preset: "medium"
                ),
            },
            new EncodingPreset
            {
                Id = webHighId,
                Name = "Web — 720p",
                Description = "Lower-bitrate HLS for slow connections. 720p H.264 fast preset.",
                Author = "NoMercy",
                Tags = "web,720p,hls,low-bandwidth",
                IsBuiltIn = true,
                ProfileJson = ProfileJsonTemplate(
                    name: "Web 720p",
                    width: 1280,
                    height: 720,
                    crf: 24,
                    preset: "fast"
                ),
            },
            new EncodingPreset
            {
                Id = archivalId,
                Name = "Archival — H.265 1080p",
                Description =
                    "HEVC archival preset. Smaller files at high visual quality. Slower encode.",
                Author = "NoMercy",
                Tags = "archival,1080p,h265,hevc",
                IsBuiltIn = true,
                ProfileJson = ProfileJsonTemplate(
                    name: "Archival H265",
                    width: 1920,
                    height: 1080,
                    crf: 20,
                    preset: "slow",
                    codec: "H265"
                ),
            },
            new EncodingPreset
            {
                Id = animeId,
                Name = "Anime — 1080p",
                Description =
                    "Flat-color content preset. x264 tuned for anime with slightly higher CRF.",
                Author = "NoMercy",
                Tags = "anime,1080p,h264",
                IsBuiltIn = true,
                ProfileJson = ProfileJsonTemplate(
                    name: "Anime 1080p",
                    width: 1920,
                    height: 1080,
                    crf: 22,
                    preset: "slow",
                    tune: "animation"
                ),
            },
            new EncodingPreset
            {
                Id = musicId,
                Name = "Music — AAC 192k",
                Description =
                    "AAC 192 kbps stereo output in an M4A container. For music library encoding.",
                Author = "NoMercy",
                Tags = "music,aac,m4a,audio",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Music AAC 192k",
                        "Format": "Mp4",
                        "VideoOutputs": [],
                        "AudioOutputs": [
                            {
                                "Codec": "Aac",
                                "BitrateKbps": 192,
                                "Channels": 2,
                                "SampleRateHz": 48000,
                                "AllowedLanguages": []
                            }
                        ],
                        "SubtitleOutputs": []
                    }
                    """,
            },
        ];
    }

    private static string ProfileJsonTemplate(
        string name,
        int width,
        int height,
        int crf,
        string preset,
        string codec = "H264",
        string? tune = null
    )
    {
        string tuneJson = tune is null ? "null" : $"\"{tune}\"";
        return $$"""
            {
                "Name": "{{name}}",
                "Format": "Hls",
                "VideoOutputs": [
                    {
                        "Codec": "{{codec}}",
                        "Width": {{width}},
                        "Height": {{height}},
                        "BitrateKbps": 0,
                        "Crf": {{crf}},
                        "Preset": "{{preset}}",
                        "Profile": "high",
                        "Level": null,
                        "ConvertHdrToSdr": false,
                        "KeyframeIntervalSeconds": 2,
                        "TenBit": false,
                        "Tune": {{tuneJson}}
                    }
                ],
                "AudioOutputs": [
                    {
                        "Codec": "Aac",
                        "BitrateKbps": 192,
                        "Channels": 2,
                        "SampleRateHz": 48000,
                        "AllowedLanguages": []
                    }
                ],
                "SubtitleOutputs": []
            }
            """;
    }
}
