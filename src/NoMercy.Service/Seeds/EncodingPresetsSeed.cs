using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

/// <summary>
/// Seeds the curated built-in shareable preset library. Eleven presets cover
/// the practical resolution × storage matrix users actually pick from
/// (4K Premium / Balanced / Space Saver, 1080p Premium / Balanced / Space
/// Saver, plus a 1080p HDR / 10-bit live-action variant, an Anime 10-bit
/// variant, and three music tiers). Archival "Source Remux" + AV1 Ultra
/// Compact MKV presets land once the Copy codec pipeline is wired in.
///
/// Built-in presets carry a deterministic Ulid in the [100..110] range so
/// repeat seeding upserts instead of duplicating. Old [1..12] built-ins from
/// previous curations are pruned by the stale-cleanup pass below. IsBuiltIn
/// = true prevents accidental deletion via the dashboard API.
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

            // Step 1: prune stale built-ins. Each shipped curation drops some
            // presets and renames others — without this step the dashboard
            // accumulates every historical preset id that ever shipped, with
            // names users never picked. Only IsBuiltIn rows are eligible
            // (user presets are sacred). Materialized EncoderProfile rows
            // for the deleted presets are removed too so the V1 picker
            // doesn't keep resolving deleted built-ins as ghost entries.
            HashSet<Ulid> currentBuiltInIds = builtIns.Select(p => p.Id).ToHashSet();
            List<Ulid> staleBuiltInIds = await context
                .EncodingPresets.AsNoTracking()
                .Where(p => p.IsBuiltIn)
                .Select(p => p.Id)
                .ToListAsync();
            staleBuiltInIds = staleBuiltInIds.Where(id => !currentBuiltInIds.Contains(id)).ToList();

            if (staleBuiltInIds.Count > 0)
            {
                await context
                    .EncoderProfiles.Where(p => staleBuiltInIds.Contains(p.Id))
                    .ExecuteDeleteAsync();
                await context
                    .EncodingPresets.Where(p => staleBuiltInIds.Contains(p.Id))
                    .ExecuteDeleteAsync();

                Logger.Setup(
                    $"Encoding presets: pruned {staleBuiltInIds.Count} stale built-in(s) + their materialized EncoderProfile rows",
                    LogEventLevel.Information
                );
            }

            // Step 2: upsert the curated set + any user-authored presets from
            // the seed file. Existing rows get content updates without losing
            // their CreatedAt timestamp.
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
                // Must pass JsonHelper.Settings explicitly: it carries the
                // StringEnumConverter so that "Format": "Hls" deserializes
                // to OutputFormat.Hls. Default JsonConvert settings would
                // throw on every preset and the Folder picker would silently
                // miss every materialized row.
                profile = JsonConvert.DeserializeObject<EncodingProfile>(
                    preset.ProfileJson,
                    JsonHelper.Settings
                );
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
            // Upsert manually via plain EF — FlexLabs.Upsert was silently
            // returning success without writing rows for this table (no
            // exception, no rows changed, no rollback message). Bypass the
            // library and walk the list ourselves.
            //
            // Fetch ALL existing EncoderProfile ids and intersect in memory
            // rather than asking EF to translate `Where(p => list.Contains(p.Id))`.
            // The translated SQL was matching nothing AND ALSO returning all
            // 25 incoming ids as "existing" via an EF/value-converter quirk
            // we couldn't reproduce — switching to in-memory intersection
            // sidesteps the whole class of bug. EncoderProfiles is small
            // (~50 rows max in practice), so the full scan is cheap.
            HashSet<Ulid> allExistingIds = (
                await context.EncoderProfiles.AsNoTracking().Select(p => p.Id).ToListAsync()
            ).ToHashSet();
            HashSet<Ulid> existingIds = materialized
                .Select(m => m.Id)
                .Where(allExistingIds.Contains)
                .ToHashSet();

            Logger.Setup(
                $"MaterializePresets DIAG: DB has {allExistingIds.Count} existing rows, "
                    + $"{existingIds.Count} of {materialized.Count} incoming match. "
                    + $"Sample DB id: {allExistingIds.FirstOrDefault()}, "
                    + $"sample incoming id: {materialized.FirstOrDefault()?.Id.ToString() ?? "(none)"}",
                LogEventLevel.Information
            );

            int inserted = 0;
            int updated = 0;

            foreach (EncoderProfile incoming in materialized)
            {
                if (existingIds.Contains(incoming.Id))
                {
                    EncoderProfile? tracked = await context.EncoderProfiles.FirstOrDefaultAsync(p =>
                        p.Id == incoming.Id
                    );
                    if (tracked is null)
                        continue;
                    tracked.Name = incoming.Name;
                    tracked.Container = incoming.Container;
                    tracked._videoProfiles = incoming._videoProfiles;
                    tracked._audioProfiles = incoming._audioProfiles;
                    tracked._subtitleProfiles = incoming._subtitleProfiles;
                    tracked._thumbnailProfile = incoming._thumbnailProfile;
                    updated++;
                }
                else
                {
                    context.EncoderProfiles.Add(incoming);
                    inserted++;
                }
            }

            await context.SaveChangesAsync();

            Logger.Setup(
                $"MaterializePresets: {inserted} inserted, {updated} updated EncoderProfile row(s) from EncodingPresets "
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
        // Deterministic Ulids so the seed is idempotent. New IDs live in the
        // [100..110] range to make a clean break from the legacy [1..12]
        // built-ins (those get pruned in Init's stale-cleanup pass). When
        // adding a new built-in: bump byte[^1] past the highest current id,
        // never reuse a retired number — preset ids end up referenced by
        // EncoderProfileFolder rows in user databases and recycling them
        // would silently change which preset a folder is bound to.
        Ulid uhdPremiumId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 100 });
        Ulid uhdBalancedId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 101 });
        Ulid uhdCompactId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 102 });
        Ulid hdPremiumId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 103 });
        Ulid hdBalancedId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104 });
        Ulid hdCompactId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 105 });
        Ulid hdHdrId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 106 });
        Ulid animeId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 107 });
        Ulid musicFlacId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 108 });
        Ulid musicMp3HighId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 109 });
        Ulid musicMp3StdId = new(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 110 });

        return
        [
            // ── 4K tier (HEVC main10, HLS) — modern TVs only ────────────────
            // All 4K presets are HEVC 10-bit because every 4K-capable display
            // from 2017 onwards decodes HEVC Main10, and 8-bit at 4K wastes
            // bitrate without quality gain on HDR-capable panels.
            new()
            {
                Id = uhdPremiumId,
                Name = "4K — Premium Quality",
                Description =
                    "HEVC 10-bit at CRF 18 (slow). Highest practical quality, large files (~25 GB / 2hr).",
                Author = "NoMercy",
                Tags = "4k,2160p,hevc,10bit,premium,hls",
                IsBuiltIn = true,
                ProfileJson = HevcMain10Hls(
                    name: "4K Premium",
                    width: 3840,
                    height: 2160,
                    crf: 18,
                    preset: "slow"
                ),
            },
            new()
            {
                Id = uhdBalancedId,
                Name = "4K — Balanced",
                Description =
                    "HEVC 10-bit at CRF 22 (medium). The recommended 4K default — visually transparent at ~13 GB / 2hr.",
                Author = "NoMercy",
                Tags = "4k,2160p,hevc,10bit,balanced,hls,recommended",
                IsBuiltIn = true,
                ProfileJson = HevcMain10Hls(
                    name: "4K Balanced",
                    width: 3840,
                    height: 2160,
                    crf: 22,
                    preset: "medium"
                ),
            },
            new()
            {
                Id = uhdCompactId,
                Name = "4K — Space Saver",
                Description =
                    "HEVC 10-bit at CRF 26 (medium). Smaller files (~7 GB / 2hr) at noticeable but acceptable quality loss.",
                Author = "NoMercy",
                Tags = "4k,2160p,hevc,10bit,compact,hls",
                IsBuiltIn = true,
                ProfileJson = HevcMain10Hls(
                    name: "4K Space Saver",
                    width: 3840,
                    height: 2160,
                    crf: 26,
                    preset: "medium"
                ),
            },
            // ── 1080p tier (H.264 high, HLS) — universal compatibility ─────
            // H.264 8-bit because the 1080p audience is "every device made
            // since 2010" — smart TVs, old iPads, cheap streamers. HEVC 1080p
            // saves ~30% storage but loses the older quarter of devices.
            new()
            {
                Id = hdPremiumId,
                Name = "1080p — Premium Quality",
                Description =
                    "H.264 high profile at CRF 18 (slow). Premium 1080p — visually transparent at ~9 GB / 2hr.",
                Author = "NoMercy",
                Tags = "1080p,h264,premium,hls",
                IsBuiltIn = true,
                ProfileJson = H264HighHls(
                    name: "1080p Premium",
                    width: 1920,
                    height: 1080,
                    crf: 18,
                    preset: "slow"
                ),
            },
            new()
            {
                Id = hdBalancedId,
                Name = "1080p — Balanced",
                Description =
                    "H.264 high profile at CRF 22 (medium). The global default — works on every device, ~4.5 GB / 2hr.",
                Author = "NoMercy",
                Tags = "1080p,h264,balanced,hls,recommended,default",
                IsBuiltIn = true,
                ProfileJson = H264HighHls(
                    name: "1080p Balanced",
                    width: 1920,
                    height: 1080,
                    crf: 22,
                    preset: "medium"
                ),
            },
            new()
            {
                Id = hdCompactId,
                Name = "1080p — Space Saver",
                Description =
                    "H.264 high profile at CRF 26 (medium). Smaller files (~3 GB / 2hr) for archives or slow connections.",
                Author = "NoMercy",
                Tags = "1080p,h264,compact,hls",
                IsBuiltIn = true,
                ProfileJson = H264HighHls(
                    name: "1080p Space Saver",
                    width: 1920,
                    height: 1080,
                    crf: 26,
                    preset: "medium"
                ),
            },
            // ── Specialty 1080p (HEVC 10-bit, HLS) ──────────────────────────
            new()
            {
                Id = hdHdrId,
                Name = "1080p — HDR / 10-bit",
                Description =
                    "HEVC main10 at CRF 22 (slow), HDR passthrough enabled. For HDR-mastered films or dark / grainy live action where 8-bit shows banding.",
                Author = "NoMercy",
                Tags = "1080p,hevc,10bit,hdr,hls,premium",
                IsBuiltIn = true,
                ProfileJson = HevcMain10Hls(
                    name: "1080p HDR",
                    width: 1920,
                    height: 1080,
                    crf: 22,
                    preset: "slow"
                ),
            },
            new()
            {
                Id = animeId,
                Name = "Anime — 1080p 10-bit",
                Description =
                    "HEVC main10 at CRF 22 (slow) tuned for animation. Flat-color content keeps gradients clean at 10-bit; tune=animation prioritises edge fidelity.",
                Author = "NoMercy",
                Tags = "anime,1080p,hevc,10bit,hls",
                IsBuiltIn = true,
                ProfileJson = HevcMain10Hls(
                    name: "Anime 1080p",
                    width: 1920,
                    height: 1080,
                    crf: 22,
                    preset: "slow",
                    tune: "animation"
                ),
            },
            // ── Music tiers ─────────────────────────────────────────────────
            new()
            {
                Id = musicFlacId,
                Name = "Music — Lossless (FLAC)",
                Description =
                    "FLAC stereo lossless. Bit-perfect preservation for music libraries and audiophile playback.",
                Author = "NoMercy",
                Tags = "music,flac,lossless,audio,archival",
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
            new()
            {
                Id = musicMp3HighId,
                Name = "Music — High Quality (MP3 320k)",
                Description =
                    "MP3 320 kbps stereo. Transparent quality with universal compatibility on every device that decodes audio.",
                Author = "NoMercy",
                Tags = "music,mp3,high,audio",
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
                Id = musicMp3StdId,
                Name = "Music — Standard (MP3 192k)",
                Description =
                    "MP3 192 kbps stereo. Sensible default for large music libraries on mobile devices.",
                Author = "NoMercy",
                Tags = "music,mp3,standard,audio,mobile",
                IsBuiltIn = true,
                ProfileJson = """
                    {
                        "Name": "Music MP3 192k",
                        "Format": "Mp3",
                        "VideoOutputs": [],
                        "AudioOutputs": [
                            {
                                "Codec": "Mp3",
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

    /// <summary>
    /// Builds an HLS profile JSON for a HEVC Main10 (10-bit) video output
    /// plus a stereo AAC 192k audio track. Used by every 4K and HDR / Anime
    /// 1080p preset in the curated library — they all share the same shape
    /// and only differ on width × height × CRF × preset × optional tune.
    /// </summary>
    private static string HevcMain10Hls(
        string name,
        int width,
        int height,
        int crf,
        string preset,
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
                        "Codec": "H265",
                        "Width": {{width}},
                        "Height": {{height}},
                        "BitrateKbps": 0,
                        "Crf": {{crf}},
                        "Preset": "{{preset}}",
                        "Profile": "main10",
                        "Level": null,
                        "ConvertHdrToSdr": false,
                        "KeyframeIntervalSeconds": 2,
                        "TenBit": true,
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
                "SubtitleOutputs": [
                    {
                        "Codec": "WebVtt",
                        "Mode": "Extract",
                        "AllowedLanguages": []
                    }
                ]
            }
            """;
    }

    /// <summary>
    /// Builds an HLS profile JSON for an H.264 high-profile (8-bit) video
    /// output plus a stereo AAC 192k audio track. Used by the universal
    /// 1080p Premium / Balanced / Space Saver tier where broadest device
    /// compatibility matters more than codec efficiency.
    /// </summary>
    private static string H264HighHls(string name, int width, int height, int crf, string preset)
    {
        return $$"""
            {
                "Name": "{{name}}",
                "Format": "Hls",
                "VideoOutputs": [
                    {
                        "Codec": "H264",
                        "Width": {{width}},
                        "Height": {{height}},
                        "BitrateKbps": 0,
                        "Crf": {{crf}},
                        "Preset": "{{preset}}",
                        "Profile": "high",
                        "Level": null,
                        "ConvertHdrToSdr": true,
                        "KeyframeIntervalSeconds": 2,
                        "TenBit": false,
                        "Tune": null
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
            """;
    }
}
