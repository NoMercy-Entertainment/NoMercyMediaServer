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
/// Seeds the curated built-in shareable preset library. Fourteen presets
/// cover the practical resolution × storage matrix users actually pick from:
///   - 4K Premium / Balanced / Space Saver         (HEVC main10, HLS)
///   - 1080p Premium / Balanced / Space Saver      (H.264 high, HLS)
///   - 1080p HDR / 10-bit                          (HEVC main10, HLS)
///   - Anime 1080p 10-bit                          (HEVC main10 + tune, HLS)
///   - Archive Lossless Original                   (Copy V/A/S, MKV)
///   - 4K / 1080p Ultra Compact (AV1)              (AV1 + Copy A/S, MKV)
///   - Music Lossless / High / Standard            (FLAC / MP3 320 / 192)
///
/// Deterministic Ulids in the [100..113] range. Old [1..12] built-ins from
/// previous curations are pruned by the stale-cleanup pass in Init.
/// IsBuiltIn = true prevents accidental deletion via the dashboard API.
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
                // Drain bindings before EncoderProfile rows — non-cascading FK.
                int unboundCount = await context
                    .EncoderProfileFolder.Where(b => staleBuiltInIds.Contains(b.EncoderProfileId))
                    .ExecuteDeleteAsync();
                await context
                    .EncoderProfiles.Where(p => staleBuiltInIds.Contains(p.Id))
                    .ExecuteDeleteAsync();
                await context
                    .EncodingPresets.Where(p => staleBuiltInIds.Contains(p.Id))
                    .ExecuteDeleteAsync();

                Logger.Setup(
                    $"Encoding presets: pruned {staleBuiltInIds.Count} stale built-in(s), "
                        + $"unbound {unboundCount} folder assignment(s), removed materialized EncoderProfile rows",
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

            // V1 ThumbnailProfile column carries the same {Width, IntervalSeconds}
            // shape as the V3 ThumbnailOutput record, so round-trip is a flat
            // serialize. Empty when the V3 preset omits Thumbnails — the
            // encoder pipeline then skips the spritevtt mux entirely instead
            // of writing an empty thumbs file.
            string thumbnailJson = profile.Thumbnails is not null
                ? JsonConvert.SerializeObject(
                    new
                    {
                        Width = profile.Thumbnails.Width,
                        IntervalSeconds = profile.Thumbnails.IntervalSeconds,
                    }
                )
                : string.Empty;

            materialized.Add(
                new()
                {
                    Id = id,
                    Name = name,
                    Container = container,
                    _videoProfiles = videoJson,
                    _audioProfiles = audioJson,
                    _subtitleProfiles = subtitleJson,
                    _thumbnailProfile = thumbnailJson,
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
        // Stable Ulids inlined per preset declaration so the id and the
        // preset's content live next to each other. Re-seeding upserts
        // existing rows by id; legacy built-ins from previous curations
        // are pruned in Init's stale-cleanup pass. Never recycle a
        // retired id — preset ids are referenced by EncoderProfileFolder
        // rows in user databases and reuse would silently rebind a
        // folder to a different preset.
        return
        [
            // ── 4K tier (HEVC main10, HLS) — modern TVs only ────────────────
            // All 4K presets are HEVC 10-bit because every 4K-capable display
            // from 2017 onwards decodes HEVC Main10, and 8-bit at 4K wastes
            // bitrate without quality gain on HDR-capable panels.
            BuiltIn(
                Ulid.Parse("01KQ5R5GD8492DRDSHBTVZE8ZJ"),
                "4K — Premium Quality",
                "HEVC 10-bit at CRF 16 (slow). Archive-grade — indistinguishable from BD source. Large files.",
                "4k,2160p,hevc,10bit,premium,hls",
                HevcMain10Hls("4K Premium", width: 3840, height: 2160, crf: 16, preset: "slow")
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GD9EQY63ZZCR3H99C0W"),
                "4K — Balanced",
                "HEVC 10-bit at CRF 20 (slow). The recommended 4K default — visually transparent vs source. Quality preserved.",
                "4k,2160p,hevc,10bit,balanced,hls,recommended",
                HevcMain10Hls("4K Balanced", width: 3840, height: 2160, crf: 20, preset: "slow")
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GD9166D87SWC9KC2HQW"),
                "4K — Space Saver",
                "HEVC 10-bit at CRF 26 (medium). Smaller files (~7 GB / 2hr) at noticeable but acceptable quality loss.",
                "4k,2160p,hevc,10bit,compact,hls",
                HevcMain10Hls(
                    "4K Space Saver",
                    width: 3840,
                    height: 2160,
                    crf: 26,
                    preset: "medium"
                )
            ),
            // ── 1080p tier (H.264 high, HLS) — universal compatibility ─────
            // H.264 8-bit because the 1080p audience is "every device made
            // since 2010" — smart TVs, old iPads, cheap streamers. HEVC 1080p
            // saves ~30% storage but loses the older quarter of devices.
            BuiltIn(
                Ulid.Parse("01KQ5R5GD9W0YYVR9295J3662N"),
                "1080p — Premium Quality",
                "H.264 high profile at CRF 16 (slow). Archive-grade 1080p — indistinguishable from BD source.",
                "1080p,h264,premium,hls",
                H264HighHls("1080p Premium", width: 1920, height: 1080, crf: 16, preset: "slow")
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GD9W3ZK7E2TJTASSW7G"),
                "1080p — Balanced",
                "H.264 high profile at CRF 18 (slow). The global default — visually transparent vs source, works on every device.",
                "1080p,h264,balanced,hls,recommended,default",
                H264HighHls("1080p Balanced", width: 1920, height: 1080, crf: 18, preset: "slow")
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GDAEMZRGF2Q2YS3D69V"),
                "1080p — Space Saver",
                "H.264 high profile at CRF 26 (medium). Smaller files (~3 GB / 2hr) for archives or slow connections.",
                "1080p,h264,compact,hls",
                H264HighHls(
                    "1080p Space Saver",
                    width: 1920,
                    height: 1080,
                    crf: 26,
                    preset: "medium"
                )
            ),
            // ── Specialty 1080p (HEVC 10-bit, HLS) ──────────────────────────
            BuiltIn(
                Ulid.Parse("01KQ5R5GDAEC3FH2EVTPMT4QDY"),
                "1080p — HDR / 10-bit",
                "HEVC main10 at CRF 18 (slow), HDR passthrough enabled. Lower CRF preserves shadow detail without banding on HDR-mastered films and dark / grainy live action.",
                "1080p,hevc,10bit,hdr,hls,premium",
                HevcMain10Hls("1080p HDR", width: 1920, height: 1080, crf: 18, preset: "slow")
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GDABK8644SSY7686R95"),
                "Anime — 1080p 10-bit",
                "HEVC main10 at CRF 18 (slow) tuned for animation. Matches typical anime BD-rip CRF (16–18) so re-encodes stay roughly source-size; tune=animation preserves edge fidelity on flat-shaded content.",
                "anime,1080p,hevc,10bit,hls",
                HevcMain10Hls(
                    "Anime 1080p",
                    width: 1920,
                    height: 1080,
                    crf: 18,
                    preset: "slow",
                    tune: "animation"
                )
            ),
            // ── Archive + AV1 tier (MKV, browser-incompatible by design) ────
            // MKV is the forcing function: web browsers can't decode the
            // container so playback always exercises the live transcoder.
            // Every native player (Android TV, VLC, Kodi, Apple TV with HEVC
            // support) direct-plays. Audio + subtitles are stream-copied so
            // lossless source tracks (TrueHD, DTS-HD MA, FLAC) and image
            // subtitle formats (PGS) are preserved bit-perfect.
            BuiltIn(
                Ulid.Parse("01KQ5R5GDAHM28MKB8BSQV7KX6"),
                "Archive — Lossless Original",
                "Source video, audio, and subtitles remuxed into MKV with zero re-encoding. File size matches the source. The forcing function for the live transcoder — web playback always transcodes on demand.",
                "archive,mkv,copy,lossless,passthrough",
                ArchiveSourceRemux()
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GDAE491G35K8K04WJMS"),
                "4K — Ultra Compact (AV1)",
                "AV1 main10 at CRF 32 (libsvtav1 preset 4). Half the bitrate of HEVC at the same quality (~6 GB / 2hr). Audio + subs stream-copied to preserve lossless source tracks. Modern devices only — older hardware triggers live transcode.",
                "4k,2160p,av1,10bit,mkv,ultra-compact,modern",
                Av1UltraCompactMkv(width: 3840, height: 2160, tenBit: true)
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GDBW9EXPFMN9V4YEXGT"),
                "1080p — Ultra Compact (AV1)",
                "AV1 main at CRF 32 (libsvtav1 preset 4). Smallest 1080p files in the library (~1.5 GB / 2hr) with high visual quality. Audio + subs stream-copied. Modern devices only.",
                "1080p,av1,mkv,ultra-compact,modern",
                Av1UltraCompactMkv(width: 1920, height: 1080, tenBit: false)
            ),
            // ── Music tiers ─────────────────────────────────────────────────
            BuiltIn(
                Ulid.Parse("01KQ5R5GDAGS3XTRGF5P4Y3WD9"),
                "Music — Lossless (FLAC)",
                "FLAC stereo lossless. Bit-perfect preservation for music libraries and audiophile playback.",
                "music,flac,lossless,audio,archival",
                MusicProfile(OutputFormat.Flac, AudioCodecType.Flac, bitrateKbps: 0)
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GDA78TYFPSWA0Q9V9E8"),
                "Music — High Quality (MP3 320k)",
                "MP3 320 kbps stereo. Transparent quality with universal compatibility on every device that decodes audio.",
                "music,mp3,high,audio",
                MusicProfile(OutputFormat.Mp3, AudioCodecType.Mp3, bitrateKbps: 320)
            ),
            BuiltIn(
                Ulid.Parse("01KQ5R5GDAECGXPVMAQW1Y50XG"),
                "Music — Standard (MP3 192k)",
                "MP3 192 kbps stereo. Sensible default for large music libraries on mobile devices.",
                "music,mp3,standard,audio,mobile",
                MusicProfile(OutputFormat.Mp3, AudioCodecType.Mp3, bitrateKbps: 192)
            ),
        ];
    }

    /// <summary>
    /// Wraps a typed <see cref="EncodingProfile"/> into an <see cref="EncodingPreset"/>
    /// row, serialising the profile once via <see cref="JsonHelper.Settings"/>. Lets
    /// the BuildBuiltInPresets array stay declarative — each entry reads as
    /// "this id, this name, this typed profile" without an inline JSON wall of
    /// strings that the compiler can't validate.
    /// </summary>
    private static EncodingPreset BuiltIn(
        Ulid id,
        string name,
        string description,
        string tags,
        EncodingProfile profile
    ) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            Author = "NoMercy",
            Tags = tags,
            IsBuiltIn = true,
            ProfileJson = JsonConvert.SerializeObject(profile, JsonHelper.Settings),
        };

    /// <summary>
    /// HEVC main10 (10-bit) HLS profile with stereo AAC 192k, an Extract
    /// WebVTT subtitle track for every source language, and a sprite-vtt
    /// thumbnail track at 320 px every 10 seconds. Used by every 4K and
    /// 1080p HDR / Anime preset.
    /// </summary>
    private static EncodingProfile HevcMain10Hls(
        string name,
        int width,
        int height,
        int crf,
        string preset,
        string? tune = null
    ) =>
        new(
            Id: Ulid.Empty,
            Name: name,
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H265,
                    Width: width,
                    Height: height,
                    BitrateKbps: 0,
                    Crf: crf,
                    Preset: preset,
                    Profile: "main10",
                    Level: null,
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: true,
                    Tune: tune
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs:
            [
                new(
                    Codec: SubtitleCodecType.Copy,
                    Mode: SubtitleMode.Extract,
                    AllowedLanguages: []
                ),
            ],
            Thumbnails: new(Width: 320, IntervalSeconds: 10)
        );

    /// <summary>
    /// H.264 high-profile (8-bit) HLS profile with stereo AAC 192k, an
    /// Extract WebVTT subtitle track, and the standard sprite-vtt thumbnail
    /// track. Used by the universal 1080p Premium / Balanced / Space Saver
    /// tier where broadest device compatibility matters more than codec
    /// efficiency. ConvertHdrToSdr=true so an HDR source falls back to a
    /// tonemapped 8-bit output instead of crushing colours.
    /// </summary>
    private static EncodingProfile H264HighHls(
        string name,
        int width,
        int height,
        int crf,
        string preset
    ) =>
        new(
            Id: Ulid.Empty,
            Name: name,
            Format: OutputFormat.Hls,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.H264,
                    Width: width,
                    Height: height,
                    BitrateKbps: 0,
                    Crf: crf,
                    Preset: preset,
                    Profile: "high",
                    Level: null,
                    ConvertHdrToSdr: true,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false,
                    Tune: null
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs:
            [
                new(
                    Codec: SubtitleCodecType.Copy,
                    Mode: SubtitleMode.Extract,
                    AllowedLanguages: []
                ),
            ],
            Thumbnails: new(Width: 320, IntervalSeconds: 10)
        );

    /// <summary>
    /// Source bytes remuxed into MKV without re-encoding. Video, audio, and
    /// subtitle streams all set to Copy passthrough so HEVC 10-bit, TrueHD,
    /// DTS-HD MA, PGS image subs all survive the round-trip exactly. Width
    /// and bit-depth fields are sentinel zero / false because nothing in the
    /// pipeline reads them for Copy outputs.
    /// </summary>
    private static EncodingProfile ArchiveSourceRemux() =>
        new(
            Id: Ulid.Empty,
            Name: "Archive Source Remux",
            Format: OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.Copy,
                    Width: 0,
                    Height: null,
                    BitrateKbps: 0,
                    Crf: 0,
                    Preset: null,
                    Profile: null,
                    Level: null,
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 2,
                    TenBit: false
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Copy,
                    BitrateKbps: 0,
                    Channels: 0,
                    SampleRateHz: 0,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs:
            [
                new(
                    Codec: SubtitleCodecType.Copy,
                    Mode: SubtitleMode.Extract,
                    AllowedLanguages: []
                ),
            ]
        );

    /// <summary>
    /// AV1 main / main10 in MKV with stream-copied audio and subtitles.
    /// CRF 32 + libsvtav1 preset 4 hits the ~half-HEVC-bitrate sweet spot.
    /// 4-second keyframe interval matches AV1's typical GOP for streaming
    /// fall-throughs. Audio Copy preserves lossless source tracks; subtitle
    /// Copy preserves PGS / ASS typesetting.
    /// </summary>
    private static EncodingProfile Av1UltraCompactMkv(int width, int height, bool tenBit) =>
        new(
            Id: Ulid.Empty,
            Name: tenBit ? "AV1 4K Ultra Compact" : "AV1 1080p Ultra Compact",
            Format: OutputFormat.Mkv,
            VideoOutputs:
            [
                new(
                    Codec: VideoCodecType.Av1,
                    Width: width,
                    Height: height,
                    BitrateKbps: 0,
                    Crf: 32,
                    Preset: "4",
                    Profile: tenBit ? "main10" : "main",
                    Level: null,
                    ConvertHdrToSdr: false,
                    KeyframeIntervalSeconds: 4,
                    TenBit: tenBit
                ),
            ],
            AudioOutputs:
            [
                new(
                    Codec: AudioCodecType.Copy,
                    BitrateKbps: 0,
                    Channels: 0,
                    SampleRateHz: 0,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs:
            [
                new(
                    Codec: SubtitleCodecType.Copy,
                    Mode: SubtitleMode.Extract,
                    AllowedLanguages: []
                ),
            ]
        );

    /// <summary>
    /// Audio-only profile for music libraries. No video / subtitle outputs.
    /// Bitrate 0 is the conventional "lossless / encoder-default" sentinel
    /// for FLAC; explicit kbps for MP3 tiers.
    /// </summary>
    private static EncodingProfile MusicProfile(
        OutputFormat format,
        AudioCodecType codec,
        int bitrateKbps
    ) =>
        new(
            Id: Ulid.Empty,
            Name: $"Music {codec} {(bitrateKbps == 0 ? "Lossless" : bitrateKbps + "k")}",
            Format: format,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    Codec: codec,
                    BitrateKbps: bitrateKbps,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: []
                ),
            ],
            SubtitleOutputs: []
        );
}
