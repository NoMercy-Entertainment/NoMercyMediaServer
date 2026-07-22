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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Pipeline.Stages;

public record ValidateInput(MediaInfo Media, EncodingProfile Profile);

public class ValidateStage(ILogger<ValidateStage> logger)
    : IPipelineStage<ValidateInput, ValidateInput>,
        IValidationStage
{
    public string Name => "Validate";

    public Task<StageResult> ExecuteAsync(
        ValidateInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            message: "[{CorrelationId}] Validating profile '{ProfileName}'", args: [context.CorrelationId, input.Profile.Name]
        );

        ProfileValidationResult result = ProfileValidator.Validate(profile: input.Profile);

        if (!result.IsValid)
        {
            string errors = string.Join(separator: "; ", values: result.Errors);

            return Task.FromResult<StageResult>(
                result: new StageFailure(
                    Error: new(
                        Kind: EncodingErrorKind.ProfileInvalid,
                        Message: $"Profile validation failed: {errors}",
                        FfmpegStderr: null,
                        StageName: Name,
                        Recoverable: false
                    )
                )
            );
        }

        foreach (string warning in result.Warnings)
            logger.LogWarning(
                message: "[{CorrelationId}] Validation warning: {Message}", args: [context.CorrelationId, warning]
            );

        return Task.FromResult<StageResult>(result: new StageSuccess<ValidateInput>(Value: input));
    }
}
