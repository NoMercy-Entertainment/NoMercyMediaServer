// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Data.Repositories;
using NoMercy.Database;
using NoMercy.Database.Activity;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Api.Services;

/// <summary>
/// Business logic extracted from <see cref="NoMercy.Api.Controllers.V1.Encoder.EncoderProfilesController"/>.
/// The controller owns auth checks and HTTP result mapping; this service owns
/// validation, persistence, and activity logging.
/// </summary>
public class EncoderProfileService(
    IEncodingPresetRepository presetRepository,
    IActivityLogger activityLogger,
    IHttpClientFactory httpClientFactory,
    MediaContext mediaContext,
    ILogger<EncoderProfileService> logger
)
{
    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    public sealed record CreateResult
    {
        public EncodingPreset? Saved { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }

        public bool IsConflict => ErrorCode == "conflict";
        public bool IsValidation => ErrorCode == "validation_error";
        public bool IsSuccess => Saved is not null;
    }

    public async Task<CreateResult> CreateAsync(
        string? name,
        string? profileJson,
        string? description,
        string? author,
        string? tags,
        Ulid? parentPresetId,
        Guid userId
    )
    {
        if (string.IsNullOrWhiteSpace(value: name))
        {
            await TryLogFailureAsync(userId: userId, errorCode: "validation_error", message: "name is required");
            return new()
            {
                ErrorCode = "validation_error",
                ErrorMessage = "name is required",
            };
        }

        if (string.IsNullOrWhiteSpace(value: profileJson))
        {
            await TryLogFailureAsync(userId: userId, errorCode: "validation_error", message: "profile_json is required");
            return new()
            {
                ErrorCode = "validation_error",
                ErrorMessage = "profile_json is required",
            };
        }

        EncodingPreset? existing = await presetRepository.GetByNameAsync(name: name);
        if (existing is not null)
            return new()
            {
                ErrorCode = "conflict",
                ErrorMessage = $"A profile named '{name}' already exists",
            };

        EncodingPreset preset = new()
        {
            Name = name,
            Description = description,
            Author = author,
            Tags = tags,
            ProfileJson = profileJson,
            ParentPresetId = parentPresetId,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(preset: preset);

        await TryLogConfigAsync(
            userId: userId,
            configKey: $"encoder.profile.{saved.Id}",
            oldValue: null,
            newValue: new
            {
                id = saved.Id.ToString(),
                name = saved.Name,
                action = "created",
            }
        );

        return new() { Saved = saved };
    }

    // -------------------------------------------------------------------------
    // Preview
    // -------------------------------------------------------------------------

    public sealed record PreviewParseResult
    {
        public EncodingProfile? Profile { get; init; }

        /// <summary>Non-null means the caller should return an early 200 with this response.</summary>
        public PreviewResponse? EarlyResponse { get; init; }
    }

    /// <summary>
    /// Validates and deserialises the profile JSON for a preview request.
    /// Returns an <see cref="PreviewParseResult"/> with either a parsed profile
    /// or a populated <see cref="PreviewResponse"/> for the early-return cases.
    /// </summary>
    public PreviewParseResult ParseProfileForPreview(
        string id,
        string? profileJson,
        string? sourcePath
    )
    {
        if (string.IsNullOrWhiteSpace(value: profileJson))
        {
            return new()
            {
                EarlyResponse = BuildPreviewErrorResponse(
                    id: id,
                    sourcePath: sourcePath,
                    ruleId: EncoderRuleId.ImportSourceMissing,
                    field: "profile_json",
                    message: "profile_json is required.",
                    suggestion: "Supply the full profile JSON in the profile_json field."
                ),
            };
        }

        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(value: profileJson);
        }
        catch (JsonException ex)
        {
            return new()
            {
                EarlyResponse = BuildPreviewErrorResponse(
                    id: id,
                    sourcePath: sourcePath,
                    ruleId: EncoderRuleId.ImportJsonMalformed,
                    field: "profile_json",
                    message: $"Profile JSON is malformed: {ex.Message}",
                    suggestion: "Fix the JSON syntax error and resubmit."
                ),
            };
        }

        if (profile is null)
        {
            return new()
            {
                EarlyResponse = BuildPreviewErrorResponse(
                    id: id,
                    sourcePath: sourcePath,
                    ruleId: EncoderRuleId.ImportJsonMalformed,
                    field: "profile_json",
                    message: "Profile JSON deserialized to null — check the outer object is present.",
                    suggestion: "Ensure the JSON root is an object, not null or an array."
                ),
            };
        }

        return new() { Profile = profile };
    }

    // -------------------------------------------------------------------------
    // Import
    // -------------------------------------------------------------------------

    public sealed record ImportResult
    {
        public EncodingPreset? Saved { get; init; }
        public EncodingProfile? ImportedProfile { get; init; }

        /// <summary>Non-null means return UnprocessableEntity with this envelope.</summary>
        public ValidationEnvelope? ValidationError { get; init; }

        public bool IsSuccess => Saved is not null;
    }

    public async Task<ImportResult> ImportAsync(
        string? inlineProfileJson,
        string? url,
        bool trustUnsigned,
        IProfileSignatureVerifier signatureVerifier,
        Guid userId,
        CancellationToken ct
    )
    {
        // --- 1. Resolve JSON source ------------------------------------------
        string profileJson;

        if (!string.IsNullOrWhiteSpace(value: inlineProfileJson))
        {
            profileJson = inlineProfileJson;
        }
        else if (!string.IsNullOrWhiteSpace(value: url))
        {
            if (!url.StartsWith(value: "https://", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                return new()
                {
                    ValidationError = ValidationEnvelope.FromRules(rules:
                    [
                        new(
                            Id: EncoderRuleId.ImportHttpNotHttps,
                            Severity: EncoderRuleSeverity.Error,
                            Field: "url",
                            Message: "Profile URLs must use HTTPS — plain HTTP is not permitted.",
                            Fix: "Replace the URL scheme with https:// before importing."
                        ),
                    ]),
                };
            }

            HttpClient client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(seconds: 15);
            client.MaxResponseContentBufferSize = 256 * 1024;

            HttpResponseMessage response;
            try
            {
                using HttpRequestMessage httpReq = new(method: HttpMethod.Get, requestUri: url);
                response = await client.SendAsync(request: httpReq, cancellationToken: ct);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                return new()
                {
                    ValidationError = ValidationEnvelope.FromRules(rules:
                    [
                        new(
                            Id: EncoderRuleId.ImportFetchFailed,
                            Severity: EncoderRuleSeverity.Error,
                            Field: "url",
                            Message: $"Failed to fetch profile from URL: {ex.Message}",
                            Fix: "Verify the URL is reachable and returns valid JSON."
                        ),
                    ]),
                };
            }

            profileJson = await response.Content.ReadAsStringAsync(cancellationToken: ct);
        }
        else
        {
            return new()
            {
                ValidationError = ValidationEnvelope.FromRules(rules:
                [
                    new(
                        Id: EncoderRuleId.ImportSourceMissing,
                        Severity: EncoderRuleSeverity.Error,
                        Field: "profile_json",
                        Message: "Either profile_json or url must be provided.",
                        Fix: "Supply profile_json with the raw JSON or url with an HTTPS URL pointing to the profile."
                    ),
                ]),
            };
        }

        // --- 2. Deserialise --------------------------------------------------
        EncodingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<EncodingProfile>(value: profileJson);
        }
        catch (JsonException ex)
        {
            return new()
            {
                ValidationError = ValidationEnvelope.FromRules(rules:
                [
                    new(
                        Id: EncoderRuleId.ImportJsonMalformed,
                        Severity: EncoderRuleSeverity.Error,
                        Field: "profile_json",
                        Message: $"Profile JSON is malformed: {ex.Message}",
                        Fix: "Fix the JSON syntax error and resubmit."
                    ),
                ]),
            };
        }

        if (profile is null)
        {
            return new()
            {
                ValidationError = ValidationEnvelope.FromRules(rules:
                [
                    new(
                        Id: EncoderRuleId.ImportJsonMalformed,
                        Severity: EncoderRuleSeverity.Error,
                        Field: "profile_json",
                        Message: "Profile JSON deserialized to null — check the outer object is present.",
                        Fix: "Ensure the JSON root is an object, not null or an array."
                    ),
                ]),
            };
        }

        // --- 3. Signature verification ---------------------------------------
        // V2 EncodingProfile no longer carries PublisherKeyFingerprint /
        // Signature inline — those move into the trusted-publisher pipeline as
        // a separate envelope when V2 grows publisher signing support. For
        // now, skip signature verification on V2 profiles.
        bool hasSigning = false;

        if (hasSigning)
        {
            EncoderRule? rejection = signatureVerifier.Verify(
                profileJson: profileJson,
                fingerprint: string.Empty,
                base64Signature: string.Empty,
                keyLookup: fingerprint =>
                    mediaContext
                        .TrustedPublisherKeys.AsNoTracking()
                        .FirstOrDefault(predicate: k => k.Fingerprint == fingerprint)
            );

            if (rejection is not null)
            {
                return new()
                {
                    ValidationError = ValidationEnvelope.FromRules(rules: [rejection]),
                };
            }
        }
        else
        {
            if (!trustUnsigned)
            {
                return new()
                {
                    ValidationError = ValidationEnvelope.FromRules(rules:
                    [
                        new(
                            Id: EncoderRuleId.ImportUnsignedRequiresFlag,
                            Severity: EncoderRuleSeverity.Error,
                            Field: "trust_unsigned",
                            Message: "This profile has no publisher signature. Pass ?trust_unsigned=true to import it anyway.",
                            Fix: "Add ?trust_unsigned=true to the request URL to accept unsigned profiles."
                        ),
                    ]),
                };
            }
        }

        // --- 4. Persist ------------------------------------------------------
        Ulid newId = Ulid.NewUlid();
        EncodingProfile importedProfile = profile with { Id = newId, IsBuiltin = false };
        string savedJson = JsonConvert.SerializeObject(value: importedProfile);

        EncodingPreset preset = new()
        {
            Id = newId,
            Name = importedProfile.Name,
            Description = importedProfile.Description,
            ProfileJson = savedJson,
            IsBuiltIn = false,
        };

        EncodingPreset saved = await presetRepository.CreateAsync(preset: preset);

        await TryLogConfigAsync(
            userId: userId,
            configKey: $"encoder.profile.{saved.Id}",
            oldValue: null,
            newValue: new
            {
                id = saved.Id.ToString(),
                name = saved.Name,
                action = "imported",
                source = url ?? "inline",
            }
        );

        return new() { Saved = saved, ImportedProfile = importedProfile };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PreviewResponse BuildPreviewErrorResponse(
        string id,
        string? sourcePath,
        string ruleId,
        string field,
        string message,
        string suggestion
    )
    {
        return new(
            ProfileId: id,
            SourceVideoFileId: sourcePath ?? string.Empty,
            SourceAnalysis: new(
                Format: string.Empty,
                DurationSeconds: 0,
                FileSizeBytes: 0,
                OverallBitRateKbps: 0,
                VideoStreamCount: 0,
                AudioStreamCount: 0,
                SubtitleStreamCount: 0,
                ChapterCount: 0,
                AttachmentCount: 0,
                HasDolbyVision: false,
                PrimaryVideoCodec: null,
                PrimaryVideoWidth: null,
                PrimaryVideoHeight: null
            ),
            PerStreamPlan: new(VideoStreams: [], AudioStreams: [], SubtitleStreams: []),
            SourceWarnings:
            [
                new(Id: ruleId, Severity: EncoderRuleSeverity.Error, Field: field, Message: message, Fix: suggestion),
            ],
            EstimatedFps: 0,
            EstimatedDurationSeconds: 0,
            EncoderHandle: "auto"
        );
    }

    private async Task TryLogFailureAsync(Guid userId, string errorCode, string message)
    {
        try
        {
            await activityLogger.LogFailureAsync(
                type: "failure.config_save",
                userId: userId,
                deviceId: Ulid.Empty,
                errorCode: errorCode,
                message: message
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(message: "Failed to log failure.config_save: {Message}", args: ex.Message);
        }
    }

    private async Task TryLogConfigAsync(
        Guid userId,
        string configKey,
        object? oldValue,
        object newValue
    )
    {
        try
        {
            await activityLogger.LogConfigurationAsync(
                type: "config.encoder_default_changed",
                userId: userId,
                deviceId: Ulid.Empty,
                configKey: configKey,
                oldValue: oldValue,
                newValue: newValue
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(message: "Failed to log encoder profile config: {Message}", args: ex.Message);
        }
    }
}
