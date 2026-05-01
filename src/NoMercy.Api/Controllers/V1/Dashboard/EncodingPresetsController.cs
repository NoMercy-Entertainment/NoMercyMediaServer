using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Helpers.Extensions;
using NoMercy.Storage;

namespace NoMercy.Api.Controllers.V1.Dashboard;

[ApiController]
[Tags("Dashboard Encoding Presets")]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/dashboard/encoding/presets")]
public class EncodingPresetsController(
    EncodingPresetRepository presetRepository,
    IPresetResolver presetResolver,
    IProfileValidator profileValidator,
    IMediaAnalyzer mediaAnalyzer,
    IDbContextFactory<MediaContext> contextFactory,
    IHttpClientFactory httpClientFactory,
    IStorageDriver storageDriver
) : BaseController
{
    [Obsolete("Use GET /api/v1/encoder/profiles")]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int pageSize = 100,
        [FromQuery] int pageIndex = 0,
        [FromQuery] string? tag = null
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        pageSize = Math.Clamp(pageSize, 1, 500);
        if (pageIndex < 0)
            pageIndex = 0;

        List<EncodingPreset> presets = await presetRepository.ListAsync(pageSize, pageIndex, tag);
        int total = await presetRepository.GetTotalCountAsync();

        return Ok(
            new
            {
                data = presets,
                meta = new
                {
                    total,
                    pageSize,
                    pageIndex,
                    totalPages = (int)Math.Ceiling((double)total / pageSize),
                },
            }
        );
    }

    [Obsolete("Use GET /api/v1/encoder/profiles/{id}")]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? preset = await presetRepository.GetByIdAsync(presetId);
        if (preset is null)
            return NotFoundResponse("Preset not found");

        return Ok(preset);
    }

    [Obsolete("Use POST /api/v1/encoder/profiles")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePresetRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to create presets");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequestResponse("name is required");
        if (string.IsNullOrWhiteSpace(request.ProfileJson))
            return BadRequestResponse("profile_json is required");

        EncodingPreset? existing = await presetRepository.GetByNameAsync(request.Name);
        if (existing is not null)
            return ConflictResponse($"A preset named '{request.Name}' already exists");

        EncodingPreset preset = new()
        {
            Name = request.Name,
            Description = request.Description,
            Author = request.Author,
            Tags = request.Tags,
            ProfileJson = request.ProfileJson,
            ParentPresetId = request.ParentPresetId,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(preset);
        return Ok(saved);
    }

    [Obsolete("Use PUT /api/v1/encoder/profiles/{id}")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdatePresetRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to update presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        // Guard: load the row first so we can return a structured 422 for
        // built-in presets instead of the generic ConflictResponse the repo
        // would otherwise produce via InvalidOperationException.
        EncodingPreset? existing = await presetRepository.GetByIdAsync(presetId);
        if (existing is null)
            return NotFoundResponse("Preset not found");

        if (existing.IsBuiltIn)
        {
            ValidationEnvelope envelope = ValidationEnvelope.FromRules([
                new EncoderRule(
                    EncoderRuleId.ProfileBuiltinReadonly,
                    EncoderRuleSeverity.Error,
                    "id",
                    "Built-in presets are read-only — clone the preset to edit it.",
                    $"POST /api/v1/dashboard/encoding/presets/{id}/clone to make an editable copy."
                ),
            ]);
            return UnprocessableEntity(envelope);
        }

        try
        {
            EncodingPreset? updated = await presetRepository.UpdateAsync(
                presetId,
                preset =>
                {
                    if (request.Name is not null)
                        preset.Name = request.Name;
                    if (request.Description is not null)
                        preset.Description = request.Description;
                    if (request.Author is not null)
                        preset.Author = request.Author;
                    if (request.Tags is not null)
                        preset.Tags = request.Tags;
                    if (request.ProfileJson is not null)
                        preset.ProfileJson = request.ProfileJson;
                    if (request.ParentPresetId.HasValue)
                        preset.ParentPresetId = request.ParentPresetId.Value;
                }
            );

            if (updated is null)
                return NotFoundResponse("Preset not found");

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
    }

    [Obsolete("Use GET /api/v1/encoder/profiles/tags")]
    [HttpGet("tags")]
    public async Task<IActionResult> ListAllTags()
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        IReadOnlyList<string> tags = await presetRepository.GetAllTagsAsync();
        return Ok(new { data = tags });
    }

    [Obsolete("Use POST /api/v1/encoder/profiles/{id}/clone")]
    [HttpPost("{id}/clone")]
    public async Task<IActionResult> Clone(string id, [FromBody] ClonePresetRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to clone presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? source = await presetRepository.GetByIdAsync(presetId);
        if (source is null)
            return NotFoundResponse("Preset not found");

        string name = string.IsNullOrWhiteSpace(request.Name)
            ? await FindUnusedCloneNameAsync(source.Name)
            : request.Name;

        if (await presetRepository.GetByNameAsync(name) is not null)
            return ConflictResponse($"A preset named '{name}' already exists");

        EncodingPreset clone = new()
        {
            Name = name,
            Description = source.Description,
            Author = request.Author ?? source.Author,
            Tags = source.Tags,
            ProfileJson = source.ProfileJson,
            ParentPresetId = source.ParentPresetId,
            // Cloning a built-in produces an editable user preset. That's
            // the whole point — lets users tweak base presets without
            // touching the seeded rows.
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(clone);
        return Ok(saved);
    }

    private async Task<string> FindUnusedCloneNameAsync(string sourceName)
    {
        string candidate = $"{sourceName} (copy)";
        int suffix = 1;
        while (await presetRepository.GetByNameAsync(candidate) is not null)
        {
            suffix++;
            candidate = $"{sourceName} (copy {suffix})";
        }
        return candidate;
    }

    [Obsolete("Use GET /api/v1/encoder/profiles/{id}/resolve")]
    [HttpGet("{id}/resolve")]
    public async Task<IActionResult> Resolve(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to view presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? leaf = await presetRepository.GetByIdAsync(presetId);
        if (leaf is null)
            return NotFoundResponse("Preset not found");

        string? parentName = await ResolveParentNameAsync(leaf.ParentPresetId);
        PresetResolveRequest request = new(leaf.Name, leaf.ProfileJson, parentName);

        try
        {
            EncodingProfile resolved = presetResolver.Resolve(
                request,
                new RepositoryPresetLookup(presetRepository)
            );
            return Ok(resolved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResponse($"Preset could not be resolved: {ex.Message}");
        }
    }

    [Obsolete("Use GET /api/v1/encoder/profiles/{id}/export")]
    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to export presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        EncodingPreset? preset = await presetRepository.GetByIdAsync(presetId);
        if (preset is null)
            return NotFoundResponse("Preset not found");

        PresetExport export = new(
            Name: preset.Name,
            Description: preset.Description,
            Author: preset.Author,
            Tags: preset.Tags,
            ProfileJson: preset.ProfileJson
        );

        return Ok(export);
    }

    [Obsolete("Use POST /api/v1/encoder/profiles/import")]
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] PresetExport import)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to import presets");

        if (string.IsNullOrWhiteSpace(import.Name))
            return BadRequestResponse("name is required");
        if (string.IsNullOrWhiteSpace(import.ProfileJson))
            return BadRequestResponse("profile_json is required");

        // Collision rename: append "(imported N)" until we find an unused name.
        // Users can rename afterwards — we'd rather keep both copies than
        // silently overwrite an existing preset.
        string finalName = import.Name;
        int suffix = 1;
        while (await presetRepository.GetByNameAsync(finalName) is not null)
        {
            finalName = $"{import.Name} (imported {suffix++})";
        }

        EncodingPreset preset = new()
        {
            Name = finalName,
            Description = import.Description,
            Author = import.Author,
            Tags = import.Tags,
            ProfileJson = import.ProfileJson,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(preset);
        return Ok(saved);
    }

    [Obsolete("Use POST /api/v1/encoder/profiles/import")]
    [HttpPost("import-url")]
    public async Task<IActionResult> ImportFromUrl(
        [FromBody] ImportFromUrlRequest request,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to import presets");

        if (string.IsNullOrWhiteSpace(request.Url))
            return BadRequestResponse("url is required");

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? parsed))
            return BadRequestResponse("url is not a valid absolute URL");

        // Only allow HTTPS for community presets — HTTP means a MITM attacker
        // can inject arbitrary encoding profiles onto the server. HTTPS shifts
        // trust to certificate validation, which is what we want.
        if (parsed.Scheme != Uri.UriSchemeHttps)
            return BadRequestResponse("Only https:// URLs are supported for preset imports");

        PresetExport? export;
        try
        {
            HttpClient client = httpClientFactory.CreateClient("preset-import");
            client.Timeout = TimeSpan.FromSeconds(15);
            string body = await client.GetStringAsync(parsed, ct);
            export = JsonConvert.DeserializeObject<PresetExport>(body);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return BadRequestResponse($"Could not fetch or parse preset from URL: {ex.Message}");
        }

        if (export is null || string.IsNullOrWhiteSpace(export.Name))
            return BadRequestResponse("URL response did not contain a valid preset payload");

        return await Import(export);
    }

    private async Task<string?> ResolveParentNameAsync(Ulid? parentId)
    {
        if (parentId is null)
            return null;

        EncodingPreset? parent = await presetRepository.GetByIdAsync(parentId.Value);
        return parent?.Name;
    }

    /// <summary>
    /// Validates a preset's profile JSON at save time so the UI can surface
    /// errors and warnings before the user ships a broken preset into an
    /// encode. Uses the canonical <see cref="IProfileValidator"/> — same
    /// rules the encode pipeline enforces at run time — so what validates
    /// here will not be rejected during encoding.
    ///
    /// Response shape:
    ///   {
    ///     valid: bool,          // true when no ERROR-severity issues
    ///     errors: [{ field, message }],
    ///     warnings: [{ field, message }]
    ///   }
    ///
    /// Warnings never block. They flag the "this will encode but might not
    /// be what you want" cases (preset mismatch, inverted ABR ladder,
    /// segment-keyframe misalignment, etc).
    /// </summary>
    /// <summary>
    /// Validates an encoding profile.
    /// </summary>
    /// <remarks>
    /// Deprecated: Use POST /api/v1/encoder/profiles/validate for the richer
    /// <see cref="NoMercy.Encoder.Errors.ValidationEnvelope"/> shape with stable rule IDs and fix hints.
    /// </remarks>
    [Obsolete("Use POST /api/v1/encoder/profiles/validate")]
    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ValidatePresetRequest request)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to validate presets");

        if (string.IsNullOrWhiteSpace(request.ProfileJson))
            return BadRequestResponse("profile_json is required");

        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(request.ProfileJson);
        }
        catch (JsonException ex)
        {
            return Ok(
                new
                {
                    valid = false,
                    errors = new[]
                    {
                        new
                        {
                            field = "ProfileJson",
                            message = $"Profile JSON is malformed: {ex.Message}",
                        },
                    },
                    warnings = Array.Empty<object>(),
                }
            );
        }

        if (profile is null)
        {
            return Ok(
                new
                {
                    valid = false,
                    errors = new[]
                    {
                        new
                        {
                            field = "ProfileJson",
                            message = "Profile JSON deserialized to null — check the outer object is present",
                        },
                    },
                    warnings = Array.Empty<object>(),
                }
            );
        }

        // Forward to the new envelope-producing path, then project back to the
        // legacy (IsValid, ValidationError[]) shape so existing dashboard
        // clients keep working without modification.
        ValidationEnvelope envelope = profileValidator.ValidateAsEnvelope(profile);

        object[] errors = envelope
            .Errors.Select(e => (object)new { field = e.Field, message = e.Message })
            .ToArray();
        object[] warnings = envelope
            .Warnings.Select(e => (object)new { field = e.Field, message = e.Message })
            .ToArray();

        return Ok(
            new
            {
                valid = envelope.Valid,
                errors,
                warnings,
            }
        );
    }

    /// <summary>
    /// Previews what the encoder will do with a specific source file under a
    /// profile. Returns the per-stream plan: copy / transcode / extract /
    /// drop, plus a human-readable rationale for each decision. Lets the UI
    /// tell users things like "your DTS 5.1 track will be transcoded to AAC
    /// stereo because HLS doesn't carry DTS" before they kick off a job.
    ///
    /// Accepts any ffmpeg-parseable input — source codec/container
    /// combinations that aren't in our output set still work, we just
    /// transcode them automatically.
    /// </summary>
    [Obsolete("Use POST /api/v1/encoder/profiles/{id}/preview")]
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewRequest request,
        CancellationToken ct
    )
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to preview encodes");

        if (string.IsNullOrWhiteSpace(request.ProfileJson))
            return BadRequestResponse("profile_json is required");
        if (string.IsNullOrWhiteSpace(request.VideoFileId))
            return BadRequestResponse("video_file_id is required");
        if (!Ulid.TryParse(request.VideoFileId, out Ulid fileId))
            return BadRequestResponse("video_file_id is not a valid ULID");

        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(request.ProfileJson);
        }
        catch (JsonException ex)
        {
            return BadRequestResponse($"profile_json is malformed: {ex.Message}");
        }
        if (profile is null)
            return BadRequestResponse("profile_json deserialized to null");

        await using MediaContext context = await contextFactory.CreateDbContextAsync(ct);
        VideoFile? file = await context.VideoFiles.FirstOrDefaultAsync(v => v.Id == fileId, ct);
        if (file is null)
            return NotFoundResponse("Video file not found");

        string path = Path.Combine(file.HostFolder, file.Filename);
        if (!storageDriver.FileExists(path))
            return NotFoundResponse($"Source file missing on disk: {path}");

        NoMercy.Encoder.Analysis.MediaInfo media;
        try
        {
            media = await mediaAnalyzer.AnalyzeAsync(path, ct);
        }
        catch (Exception ex)
        {
            return InternalServerErrorResponse($"ffprobe failed: {ex.Message}");
        }

        StreamActionResolver resolver = new();
        List<object> videoPlan = [];
        List<object> audioPlan = [];
        List<object> subtitlePlan = [];

        // Per-variant video decisions. When the profile has multiple variants
        // (ABR ladder), each one gets its own plan against the first source
        // video stream.
        foreach (VideoStreamInfo source in media.VideoStreams)
        {
            for (int i = 0; i < profile.VideoOutputs.Length; i++)
            {
                VideoOutput output = profile.VideoOutputs[i];
                StreamAction action = resolver.ResolveVideo(source, output);
                videoPlan.Add(
                    new
                    {
                        source_index = source.Index,
                        source_codec = source.Codec,
                        source_resolution = $"{source.Width}x{source.Height}",
                        source_bitrate_kbps = source.BitRateKbps,
                        variant_index = i,
                        target_codec = output.Codec.ToString(),
                        target_resolution = $"{output.Width}x{output.Height ?? source.Height}",
                        target_bitrate_kbps = output.BitrateKbps,
                        action = action.ToString(),
                        rationale = DescribeVideoAction(source, output, action),
                    }
                );
            }
        }

        foreach (AudioStreamInfo source in media.AudioStreams)
        {
            for (int i = 0; i < profile.AudioOutputs.Length; i++)
            {
                AudioOutput output = profile.AudioOutputs[i];
                StreamAction action = resolver.ResolveAudio(source, output, profile.Format);
                audioPlan.Add(
                    new
                    {
                        source_index = source.Index,
                        source_codec = source.Codec,
                        source_language = source.Language,
                        source_channels = source.Channels,
                        source_bitrate_kbps = source.BitRateKbps,
                        output_index = i,
                        target_codec = output.Codec.ToString(),
                        target_channels = output.Channels,
                        target_bitrate_kbps = output.BitrateKbps,
                        action = action.ToString(),
                        rationale = DescribeAudioAction(source, output, profile.Format, action),
                    }
                );
            }
        }

        foreach (SubtitleStreamInfo source in media.SubtitleStreams)
        {
            for (int i = 0; i < profile.SubtitleOutputs.Length; i++)
            {
                SubtitleOutput output = profile.SubtitleOutputs[i];
                StreamAction action = resolver.ResolveSubtitle(source, output, profile.Format);
                subtitlePlan.Add(
                    new
                    {
                        source_index = source.Index,
                        source_codec = source.Codec,
                        source_language = source.Language,
                        source_is_text = source.IsTextBased,
                        output_index = i,
                        target_codec = output.Codec.ToString(),
                        mode = output.Mode.ToString(),
                        action = action.ToString(),
                        rationale = DescribeSubtitleAction(source, output, profile.Format, action),
                    }
                );
            }
        }

        // Also run profile validation so the preview carries warnings that
        // apply regardless of source (level mismatch, inverted ladder, etc.).
        ValidationResult validation = profileValidator.Validate(profile);

        List<object> sourceWarnings = CollectSourceHealthWarnings(media, profile);

        return Ok(
            new
            {
                source = new
                {
                    path,
                    duration_seconds = media.Duration.TotalSeconds,
                    overall_bitrate_kbps = media.OverallBitRateKbps,
                    file_size_bytes = media.FileSizeBytes,
                    video_streams = media.VideoStreams.Count,
                    audio_streams = media.AudioStreams.Count,
                    subtitle_streams = media.SubtitleStreams.Count,
                    chapters = media.Chapters.Count,
                    is_hdr = media.IsHdr,
                    has_dolby_vision = media.DolbyVision is not null,
                    has_hdr10_plus = media.HasHdr10Plus,
                    is_variable_frame_rate = media.IsVariableFrameRate,
                },
                profile_format = profile.Format.ToString(),
                video_plan = videoPlan,
                audio_plan = audioPlan,
                subtitle_plan = subtitlePlan,
                source_warnings = sourceWarnings,
                warnings = validation
                    .Errors.Where(e => e.Severity == ValidationSeverity.Warning)
                    .Select(e => new { field = e.Field, message = e.Message })
                    .ToArray(),
                errors = validation
                    .Errors.Where(e => e.Severity == ValidationSeverity.Error)
                    .Select(e => new { field = e.Field, message = e.Message })
                    .ToArray(),
            }
        );
    }

    /// <summary>
    /// Source-level issues the encode pipeline will have to navigate. These
    /// are not profile mistakes — the PROFILE can be perfect and still
    /// produce degraded output because the SOURCE has problems. Surfacing
    /// them in the preview tells the user what to expect before they kick
    /// off the job: "your source is VFR, expect A/V sync issues", "this is
    /// a Dolby Vision source but your profile targets SDR, RPU will be
    /// stripped", etc.
    /// </summary>
    private static List<object> CollectSourceHealthWarnings(
        NoMercy.Encoder.Analysis.MediaInfo media,
        EncodingProfile profile
    )
    {
        List<object> warnings = [];

        // Variable frame rate is the most common source-side trap. Most
        // phones and screen recordings are VFR, encoders assume CFR by
        // default, and the output drifts audio/video sync over long clips.
        if (media.IsVariableFrameRate)
        {
            warnings.Add(
                new
                {
                    field = "source.frame_rate",
                    severity = "warning",
                    message = "Source has variable frame rate. Encoder will convert to constant FR; "
                        + "check the output for audio/video sync drift on long content.",
                }
            );
        }

        // Missing / zero duration — ffprobe couldn't determine length, the
        // segmented encoders can't estimate progress, the resume checkpoint
        // can't validate completion. Usually indicates a broken source.
        if (media.Duration == TimeSpan.Zero)
        {
            warnings.Add(
                new
                {
                    field = "source.duration",
                    severity = "warning",
                    message = "Source duration could not be determined. Progress estimates will be "
                        + "unavailable and segmented encode resume may not work correctly.",
                }
            );
        }

        // Source has no video streams but profile targets video — nothing to
        // encode. Surface as a warning, not error: audio-only profiles still
        // work fine and the plan stage will handle it gracefully.
        if (media.VideoStreams.Count == 0 && profile.VideoOutputs.Length > 0)
        {
            warnings.Add(
                new
                {
                    field = "source.video_streams",
                    severity = "warning",
                    message = "Profile targets video outputs but the source file has no video "
                        + "streams. Video outputs will be skipped.",
                }
            );
        }

        // Source has no audio but profile targets audio — HLS/DASH playback
        // breaks on iOS without audio. Mention it.
        if (media.AudioStreams.Count == 0 && profile.AudioOutputs.Length > 0)
        {
            warnings.Add(
                new
                {
                    field = "source.audio_streams",
                    severity = "warning",
                    message = "Profile targets audio outputs but the source has no audio streams. "
                        + "Audio outputs will be skipped — consider switching to a video-only "
                        + "profile for this source.",
                }
            );
        }

        // Dolby Vision source — RPU survives only under specific conditions
        // (HEVC 10-bit output in HLS/MP4/MKV, no tonemap). Warn when the
        // profile can't preserve it.
        if (media.DolbyVision is not null)
        {
            bool profileCanPreserveDv = profile.VideoOutputs.Any(v =>
                v.Codec == VideoCodecType.H265
                && v.TenBit
                && !v.ConvertHdrToSdr
                && profile.Format is OutputFormat.Hls or OutputFormat.Mp4 or OutputFormat.Mkv
            );

            warnings.Add(
                new
                {
                    field = "source.dolby_vision",
                    severity = profileCanPreserveDv ? "info" : "warning",
                    message = profileCanPreserveDv
                        ? $"Source has Dolby Vision (profile {media.DolbyVision.Profile}) — "
                            + "will be preserved through the encode."
                        : $"Source has Dolby Vision (profile {media.DolbyVision.Profile}) but "
                            + "the profile can't preserve it (needs HEVC 10-bit in HLS/MP4/MKV "
                            + "without tonemap). The RPU metadata will be stripped.",
                }
            );
        }

        // HDR without Dolby Vision — still needs 10-bit output + HDR-capable
        // container to preserve. Flag if the profile is SDR-only.
        if (media.IsHdr && media.DolbyVision is null)
        {
            bool profilePreservesHdr = profile.VideoOutputs.Any(v =>
                v.TenBit && !v.ConvertHdrToSdr
            );
            if (!profilePreservesHdr)
            {
                warnings.Add(
                    new
                    {
                        field = "source.hdr",
                        severity = "info",
                        message = "Source is HDR10/HLG. Profile targets SDR output — tonemapping "
                            + "will convert to SDR (set ConvertHdrToSdr=true to control the "
                            + "algorithm, otherwise you may get incorrect colors).",
                    }
                );
            }
        }

        // Upscale guard: profile target resolution > source resolution.
        // ResolveDimensions clamps so the encoder NEVER upscales, but the
        // user's output will be smaller than they expected. Warn.
        if (media.VideoStreams.Count > 0)
        {
            VideoStreamInfo src = media.VideoStreams[0];
            foreach (VideoOutput v in profile.VideoOutputs)
            {
                int targetH = v.Height ?? v.Width * 9 / 16;
                if (v.Width > src.Width || targetH > src.Height)
                {
                    warnings.Add(
                        new
                        {
                            field = "profile.resolution",
                            severity = "info",
                            message = $"Source is {src.Width}x{src.Height} but profile targets "
                                + $"{v.Width}x{targetH}. Output will be clamped to source "
                                + "resolution — upscaling is intentionally disabled.",
                        }
                    );
                    break; // One warning covers the general issue.
                }
            }
        }

        return warnings;
    }

    private static string DescribeVideoAction(
        VideoStreamInfo source,
        VideoOutput target,
        StreamAction action
    )
    {
        return action switch
        {
            StreamAction.Copy =>
                $"Source {source.Codec} {source.Width}x{source.Height} @ {source.BitRateKbps}kbps "
                    + $"already matches target — remuxing without re-encoding.",
            StreamAction.Transcode => BuildVideoTranscodeRationale(source, target),
            _ => action.ToString(),
        };
    }

    private static string BuildVideoTranscodeRationale(VideoStreamInfo source, VideoOutput target)
    {
        List<string> reasons = [];
        string sourceCodec = source.Codec.ToLowerInvariant();
        if (!MatchesTargetCodec(sourceCodec, target.Codec))
            reasons.Add($"source is {source.Codec}, target is {target.Codec}");
        if (source.Width != target.Width)
            reasons.Add($"resolution {source.Width}px → {target.Width}px");
        if (target.Height is int th && source.Height != th)
            reasons.Add($"height {source.Height}px → {th}px");
        if (source.BitRateKbps < target.BitrateKbps && target.BitrateKbps > 0)
            reasons.Add(
                $"source bitrate {source.BitRateKbps}kbps below target {target.BitrateKbps}kbps"
            );

        if (reasons.Count == 0)
            return "Transcoding (no exact match found for passthrough).";
        return "Transcoding: " + string.Join(", ", reasons) + ".";
    }

    private static bool MatchesTargetCodec(string sourceCodecName, VideoCodecType target) =>
        (target, sourceCodecName) switch
        {
            (VideoCodecType.H264, "h264" or "avc") => true,
            (VideoCodecType.H265, "hevc" or "h265") => true,
            (VideoCodecType.Av1, "av1") => true,
            (VideoCodecType.Vp9, "vp9") => true,
            _ => false,
        };

    private static string DescribeAudioAction(
        AudioStreamInfo source,
        AudioOutput target,
        OutputFormat format,
        StreamAction action
    )
    {
        return action switch
        {
            StreamAction.Copy =>
                $"Source {source.Codec} {source.Channels}ch @ {source.BitRateKbps}kbps is a passthrough match — remuxing.",
            StreamAction.Transcode => BuildAudioTranscodeRationale(source, target, format),
            _ => action.ToString(),
        };
    }

    private static string BuildAudioTranscodeRationale(
        AudioStreamInfo source,
        AudioOutput target,
        OutputFormat format
    )
    {
        string sourceCodec = source.Codec.ToLowerInvariant();
        // Container-forbidden codecs — highlight when the user's source is
        // something the output container simply can't carry.
        if (format == OutputFormat.Hls && sourceCodec is "dts" or "dca" or "truehd")
            return $"{source.Codec} is not supported in HLS — transcoding to {target.Codec}.";
        if (format == OutputFormat.Mp4 && sourceCodec is "truehd")
            return $"TrueHD is not standard in MP4 — transcoding to {target.Codec}.";

        if (sourceCodec is "flac" or "truehd" && !IsLossless(target.Codec))
            return $"Lossless source ({source.Codec}) → lossy target ({target.Codec}) always transcodes.";

        if (source.Channels > target.Channels)
            return $"Downmixing {source.Channels}ch → {target.Channels}ch with {target.Codec}.";

        return $"Transcoding {source.Codec} → {target.Codec} "
            + $"({source.BitRateKbps}kbps → {target.BitrateKbps}kbps).";
    }

    private static bool IsLossless(AudioCodecType codec) =>
        codec is AudioCodecType.Flac or AudioCodecType.TrueHd;

    private static string DescribeSubtitleAction(
        SubtitleStreamInfo source,
        SubtitleOutput target,
        OutputFormat format,
        StreamAction action
    )
    {
        return action switch
        {
            StreamAction.Copy =>
                $"Source {source.Codec} subtitle copied into MKV container unchanged.",
            StreamAction.Extract =>
                $"Source {source.Codec} subtitle extracted to a WebVTT/SRT sidecar next to the playlist.",
            StreamAction.Transcode when target.Mode == SubtitleMode.BurnIn =>
                "Subtitle burned into the video frame (BurnIn mode).",
            StreamAction.Transcode when !source.IsTextBased =>
                $"Bitmap subtitle ({source.Codec}) cannot be embedded in {format} — burning into video.",
            StreamAction.Transcode => $"Transcoding {source.Codec} subtitle to {target.Codec}.",
            _ => action.ToString(),
        };
    }

    [Obsolete("Use DELETE /api/v1/encoder/profiles/{id}")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!User.IsModerator())
            return UnauthorizedResponse("You do not have permission to delete presets");

        if (!Ulid.TryParse(id, out Ulid presetId))
            return BadRequestResponse("Invalid preset id");

        try
        {
            bool removed = await presetRepository.DeleteAsync(presetId);
            if (!removed)
                return NotFoundResponse("Preset not found");

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ConflictResponse(ex.Message);
        }
    }
}

public record CreatePresetRequest(
    string Name,
    string ProfileJson,
    string? Description = null,
    string? Author = null,
    string? Tags = null,
    Ulid? ParentPresetId = null
);

/// <summary>
/// Adapter that lets <see cref="IPresetResolver"/> walk the parent chain by
/// hitting the database once per ancestor. Synchronous lookup — the resolver
/// is pure and doesn't await, so the adapter blocks on async repository
/// calls. Fine for the rare resolve path; optimize if it ever gets called
/// in a tight loop.
/// </summary>
internal sealed class RepositoryPresetLookup(EncodingPresetRepository repository) : IPresetLookup
{
    public PresetResolveRequest? FindByName(string name)
    {
        EncodingPreset? preset = repository.GetByNameAsync(name).GetAwaiter().GetResult();
        if (preset is null)
            return null;

        string? parentName = preset.ParentPresetId is Ulid parentId
            ? repository.GetByIdAsync(parentId).GetAwaiter().GetResult()?.Name
            : null;

        return new(preset.Name, preset.ProfileJson, parentName);
    }
}

public record PresetExport(
    string Name,
    string ProfileJson,
    string? Description = null,
    string? Author = null,
    string? Tags = null
);

public record ImportFromUrlRequest(string Url);

public record ClonePresetRequest(string? Name = null, string? Author = null);

public record ValidatePresetRequest(string ProfileJson);

public record PreviewRequest(
    [property: JsonProperty("profile_json")] string ProfileJson,
    [property: JsonProperty("video_file_id")] string VideoFileId
);

public record UpdatePresetRequest(
    string? Name = null,
    string? Description = null,
    string? Author = null,
    string? Tags = null,
    string? ProfileJson = null,
    Ulid? ParentPresetId = null
);
