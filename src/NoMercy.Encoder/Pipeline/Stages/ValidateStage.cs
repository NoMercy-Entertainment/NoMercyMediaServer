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
            "[{CorrelationId}] Validating profile '{ProfileName}'",
            context.CorrelationId,
            input.Profile.Name
        );

        ProfileValidationResult result = ProfileValidator.Validate(input.Profile);

        if (!result.IsValid)
        {
            string errors = string.Join("; ", result.Errors);

            return Task.FromResult<StageResult>(
                new StageFailure(
                    new(
                        EncodingErrorKind.ProfileInvalid,
                        $"Profile validation failed: {errors}",
                        null,
                        Name,
                        false
                    )
                )
            );
        }

        foreach (string warning in result.Warnings)
            logger.LogWarning(
                "[{CorrelationId}] Validation warning: {Message}",
                context.CorrelationId,
                warning
            );

        return Task.FromResult<StageResult>(new StageSuccess<ValidateInput>(input));
    }
}
