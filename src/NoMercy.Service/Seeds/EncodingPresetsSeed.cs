using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.Information;
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
            EncodingPreset[] builtIns = BuildBuiltInPresets();
            EncodingPreset[] userPresets = LoadUserPresetsFromFile();
            EncodingPreset[] presets = [.. builtIns, .. userPresets];

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

    /// <summary>
    /// Loads user-authored presets from the config-directory seed file when
    /// it exists. Swallowed on any parse error — a bad seed file must never
    /// block server startup, it just logs a warning and moves on. Presets
    /// loaded here are treated as normal user presets (IsBuiltIn = false).
    /// </summary>
    private static EncodingPreset[] LoadUserPresetsFromFile()
    {
        string path = AppFiles.EncodingPresetsSeedFile;
        if (!File.Exists(path))
            return [];

        try
        {
            string json = File.ReadAllText(path);
            PresetSeedEntry[]? entries = JsonConvert.DeserializeObject<PresetSeedEntry[]>(json);
            if (entries is null || entries.Length == 0)
                return [];

            return entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .Where(e => !string.IsNullOrWhiteSpace(e.ProfileJson))
                .Select(e => new EncodingPreset
                {
                    Id = e.Id ?? Ulid.NewUlid(),
                    Name = e.Name!,
                    Description = e.Description,
                    Author = e.Author,
                    Tags = e.Tags,
                    ProfileJson = e.ProfileJson!,
                    IsBuiltIn = false,
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            Logger.Setup(
                $"Could not load encoding presets from {path}: {ex.Message}",
                LogEventLevel.Warning
            );
            return [];
        }
    }

    /// <summary>
    /// For every <see cref="EncodingPreset"/> row that carries a parseable
    /// <c>ProfileJson</c>, materializes a corresponding <see cref="EncoderProfile"/>
    /// row so V3 presets appear in the Folder picker alongside V1 profiles.
    ///
    /// Rules that match <see cref="BuiltinPresetSeeder"/>:
    ///   - Uses the preset's Id as the EncoderProfile Id (stable, no duplicates).
    ///   - Never touches EncoderProfileFolder rows — folder assignments survive re-seed.
    ///   - Unparseable ProfileJson is skipped with a warning; one bad preset must
    ///     not block the rest.
    /// </summary>
    public static async Task MaterializePresetsAsync(MediaContext context)
    {
        Logger.Setup(
            "Materializing V3 EncodingPresets into EncoderProfiles",
            LogEventLevel.Information
        );

        List<EncodingPreset> presets = await context.EncodingPresets.AsNoTracking().ToListAsync();

        Logger.Setup(
            $"MaterializePresets: scanned {presets.Count} EncodingPreset row(s)",
            LogEventLevel.Information
        );

        List<EncoderProfile> materialized = [];
        int skippedEmpty = 0;
        int skippedDeserializeNull = 0;
        int deserializeFailures = 0;

        foreach (EncodingPreset preset in presets)
        {
            if (string.IsNullOrWhiteSpace(preset.ProfileJson))
            {
                skippedEmpty++;
                continue;
            }

            EncodingProfile? profile;
            try
            {
                profile = JsonConvert.DeserializeObject<EncodingProfile>(preset.ProfileJson);
            }
            catch (Exception ex)
            {
                deserializeFailures++;
                Logger.Setup(
                    $"MaterializePresets: could not deserialize ProfileJson for preset '{preset.Name}' ({preset.Id}): {ex.Message}",
                    LogEventLevel.Warning
                );
                continue;
            }

            if (profile is null)
            {
                skippedDeserializeNull++;
                Logger.Setup(
                    $"MaterializePresets: deserialize returned null for preset '{preset.Name}' ({preset.Id})",
                    LogEventLevel.Warning
                );
                continue;
            }

            (
                Ulid id,
                string name,
                string container,
                string videoJson,
                string audioJson,
                string subtitleJson
            ) = ProfileMapper.ToV1Fields(profile with { Id = preset.Id, Name = preset.Name });

            materialized.Add(
                new()
                {
                    Id = id,
                    Name = name,
                    Container = container,
                    _videoProfiles = videoJson,
                    _audioProfiles = audioJson,
                    _subtitleProfiles = subtitleJson,
                    _thumbnailProfile = string.Empty,
                    EncoderProfileFolder = [],
                }
            );
        }

        if (materialized.Count == 0)
        {
            Logger.Setup(
                $"MaterializePresets: 0 rows materialized "
                    + $"(scanned={presets.Count} empty={skippedEmpty} "
                    + $"deserializeFailures={deserializeFailures} "
                    + $"deserializeNull={skippedDeserializeNull}). "
                    + "Folder picker will only show V1-seeded EncoderProfile rows. "
                    + "Check the warnings above for the failure cause.",
                LogEventLevel.Warning
            );
            return;
        }

        try
        {
            await context
                .EncoderProfiles.UpsertRange(materialized)
                .On(v => new { v.Id })
                .WhenMatched(
                    (existing, incoming) =>
                        new()
                        {
                            Id = incoming.Id,
                            Name = incoming.Name,
                            Container = incoming.Container,
                            _videoProfiles = incoming._videoProfiles,
                            _audioProfiles = incoming._audioProfiles,
                            _subtitleProfiles = incoming._subtitleProfiles,
                            _thumbnailProfile = incoming._thumbnailProfile,
                        }
                )
                .RunAsync();

            Logger.Setup(
                $"MaterializePresets: upserted {materialized.Count} EncoderProfile row(s) from EncodingPresets "
                    + $"(scanned={presets.Count} empty={skippedEmpty} "
                    + $"deserializeFailures={deserializeFailures} "
                    + $"deserializeNull={skippedDeserializeNull})",
                LogEventLevel.Information
            );
        }
        catch (Exception e)
        {
            Logger.Setup($"MaterializePresets: upsert failed: {e.Message}", LogEventLevel.Fatal);
        }
    }

    private sealed record PresetSeedEntry(
        string? Name,
        string? ProfileJson,
        Ulid? Id = null,
        string? Description = null,
        string? Author = null,
        string? Tags = null
    );

    internal static EncodingPreset[] BuildBuiltInPresets()
    {
        // Deterministic Ulids so the seed is idempotent. Same binary layout
        // every run, same rows — upsert maps them to their existing records.
        // New presets: keep the byte[^1] counter incrementing so history
        // remains stable across upserts.
        Ulid generalId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
        Ulid webHighId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 });
        Ulid archivalId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3 });
        Ulid animeId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4 });
        Ulid musicId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5 });
        Ulid chromecast1080pId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6 });
        Ulid appleTv4KId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7 });
        Ulid mobileLowId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8 });
        Ulid uhd4KArchivalId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9 });
        Ulid animeHevcId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10 });
        Ulid musicMp3Id = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 11 });
        Ulid musicFlacId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12 });

        return
        [
            new()
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
            new()
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
            new()
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
            new()
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
            new()
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
            // Chromecast devices support H.264 High Profile up to Level 4.1
            // (1080p) and HE-AAC stereo. Staying on-spec for this target maps
            // to the widest reach of smart TVs and streamers too.
            new()
            {
                Id = chromecast1080pId,
                Name = "Chromecast — 1080p",
                Description =
                    "Chromecast-compatible HLS at 1080p. H.264 High @ L4.1, AAC stereo, widely supported by smart TVs and older devices.",
                Author = "NoMercy",
                Tags = "chromecast,1080p,h264,hls,smarttv",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Chromecast 1080p",
                        "Format": "Hls",
                        "SegmentDurationSeconds": 6,
                        "VideoOutputs": [
                            {
                                "Codec": "H264",
                                "Width": 1920,
                                "Height": 1080,
                                "BitrateKbps": 0,
                                "Crf": 22,
                                "Preset": "medium",
                                "Profile": "high",
                                "Level": "4.1",
                                "ConvertHdrToSdr": true,
                                "KeyframeIntervalSeconds": 2,
                                "TenBit": false
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
                        "SubtitleOutputs": [
                            {
                                "Codec": "WebVtt",
                                "Mode": "Extract",
                                "AllowedLanguages": []
                            }
                        ]
                    }
                    """,
            },
            // Apple TV 4K decodes HEVC Main10 up to Level 5.1 and plays
            // Dolby Digital Plus (EAC3) for surround. HDR passthrough requires
            // 10-bit + HEVC + no tonemap. hvc1 tag is set by the encoder
            // pipeline for videotoolbox.
            new()
            {
                Id = appleTv4KId,
                Name = "Apple TV 4K — HDR",
                Description =
                    "Apple TV 4K profile: HEVC Main10 2160p @ L5.1, EAC3 5.1, HDR passthrough when source is HDR.",
                Author = "NoMercy",
                Tags = "appletv,4k,2160p,hevc,hdr,eac3",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Apple TV 4K HDR",
                        "Format": "Hls",
                        "SegmentDurationSeconds": 6,
                        "VideoOutputs": [
                            {
                                "Codec": "H265",
                                "Width": 3840,
                                "Height": 2160,
                                "BitrateKbps": 0,
                                "Crf": 22,
                                "Preset": "medium",
                                "Profile": "main10",
                                "Level": "5.1",
                                "ConvertHdrToSdr": false,
                                "KeyframeIntervalSeconds": 2,
                                "TenBit": true
                            }
                        ],
                        "AudioOutputs": [
                            {
                                "Codec": "Eac3",
                                "BitrateKbps": 640,
                                "Channels": 6,
                                "SampleRateHz": 48000,
                                "AllowedLanguages": []
                            }
                        ],
                        "SubtitleOutputs": [
                            {
                                "Codec": "WebVtt",
                                "Mode": "Extract",
                                "AllowedLanguages": []
                            }
                        ]
                    }
                    """,
            },
            // Mobile / poor-wifi preset: 480p bitrate-capped for a predictable
            // ceiling. AAC 96k stereo keeps audio reasonable without eating
            // the bandwidth budget. H.264 Main @ L3.1 plays on every phone
            // from the last decade.
            new()
            {
                Id = mobileLowId,
                Name = "Mobile — 480p Low Bandwidth",
                Description =
                    "Data-friendly 480p H.264 Main @ L3.1 with AAC 96 kbps stereo. For mobile networks and low-end devices.",
                Author = "NoMercy",
                Tags = "mobile,480p,low-bandwidth,h264,hls",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Mobile 480p",
                        "Format": "Hls",
                        "SegmentDurationSeconds": 6,
                        "VideoOutputs": [
                            {
                                "Codec": "H264",
                                "Width": 854,
                                "Height": 480,
                                "BitrateKbps": 900,
                                "Crf": 0,
                                "Preset": "fast",
                                "Profile": "main",
                                "Level": "3.1",
                                "ConvertHdrToSdr": true,
                                "KeyframeIntervalSeconds": 2,
                                "TenBit": false
                            }
                        ],
                        "AudioOutputs": [
                            {
                                "Codec": "Aac",
                                "BitrateKbps": 96,
                                "Channels": 2,
                                "SampleRateHz": 48000,
                                "AllowedLanguages": []
                            }
                        ],
                        "SubtitleOutputs": [
                            {
                                "Codec": "WebVtt",
                                "Mode": "Extract",
                                "AllowedLanguages": []
                            }
                        ]
                    }
                    """,
            },
            // 4K archival: HEVC 10-bit at CRF 18 (visually lossless), FLAC
            // audio preserved in MKV. Massive files — intended for long-term
            // storage of source-quality material, not streaming.
            new()
            {
                Id = uhd4KArchivalId,
                Name = "4K Archival — HEVC + FLAC",
                Description =
                    "Long-term archival: HEVC Main10 2160p at CRF 18 (visually lossless), FLAC audio, MKV container. Large files; storage not streaming.",
                Author = "NoMercy",
                Tags = "archival,4k,2160p,hevc,10bit,flac,mkv",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "4K Archival",
                        "Format": "Mkv",
                        "VideoOutputs": [
                            {
                                "Codec": "H265",
                                "Width": 3840,
                                "Height": 2160,
                                "BitrateKbps": 0,
                                "Crf": 18,
                                "Preset": "slow",
                                "Profile": "main10",
                                "Level": "5.1",
                                "ConvertHdrToSdr": false,
                                "KeyframeIntervalSeconds": 4,
                                "TenBit": true
                            }
                        ],
                        "AudioOutputs": [
                            {
                                "Codec": "Flac",
                                "BitrateKbps": 0,
                                "Channels": 6,
                                "SampleRateHz": 48000,
                                "AllowedLanguages": []
                            }
                        ],
                        "SubtitleOutputs": []
                    }
                    """,
            },
            // Anime community standard: HEVC 10-bit preserves color banding
            // much better than H.264 8-bit on flat-shaded content. Opus
            // surround gives small high-quality audio. MKV holds ASS
            // subtitles unchanged (HLS would convert to WebVTT and lose
            // typesetting).
            new()
            {
                Id = animeHevcId,
                Name = "Anime — HEVC 10-bit 1080p",
                Description =
                    "Anime archival preset: HEVC 10-bit handles flat-color content without banding. Opus 5.1 audio, MKV keeps ASS subtitles intact.",
                Author = "NoMercy",
                Tags = "anime,1080p,hevc,10bit,opus,mkv",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Anime HEVC 10bit",
                        "Format": "Mkv",
                        "VideoOutputs": [
                            {
                                "Codec": "H265",
                                "Width": 1920,
                                "Height": 1080,
                                "BitrateKbps": 0,
                                "Crf": 22,
                                "Preset": "slow",
                                "Profile": "main10",
                                "Level": "4.1",
                                "ConvertHdrToSdr": false,
                                "KeyframeIntervalSeconds": 2,
                                "TenBit": true
                            }
                        ],
                        "AudioOutputs": [
                            {
                                "Codec": "Opus",
                                "BitrateKbps": 256,
                                "Channels": 6,
                                "SampleRateHz": 48000,
                                "AllowedLanguages": []
                            }
                        ],
                        "SubtitleOutputs": []
                    }
                    """,
            },
            new()
            {
                Id = musicMp3Id,
                Name = "Music — MP3 320k",
                Description =
                    "MP3 320 kbps stereo output. Legacy-compatible music archival for players that do not handle AAC or FLAC.",
                Author = "NoMercy",
                Tags = "music,mp3,audio,legacy",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Music MP3 320k",
                        "Format": "Mp3",
                        "VideoOutputs": [],
                        "AudioOutputs": [
                            {
                                "Codec": "Mp3",
                                "BitrateKbps": 320,
                                "Channels": 2,
                                "SampleRateHz": 48000,
                                "AllowedLanguages": []
                            }
                        ],
                        "SubtitleOutputs": []
                    }
                    """,
            },
            new()
            {
                Id = musicFlacId,
                Name = "Music — FLAC Lossless",
                Description =
                    "FLAC lossless stereo output. Bit-perfect archival of music libraries with no quality loss.",
                Author = "NoMercy",
                Tags = "music,flac,audio,lossless,archival",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Music FLAC Lossless",
                        "Format": "Flac",
                        "VideoOutputs": [],
                        "AudioOutputs": [
                            {
                                "Codec": "Flac",
                                "BitrateKbps": 0,
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
