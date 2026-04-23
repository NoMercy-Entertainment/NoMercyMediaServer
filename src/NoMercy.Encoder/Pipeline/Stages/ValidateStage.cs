namespace NoMercy.Encoder.Pipeline.Stages;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;

public record ValidateInput(MediaInfo Media, EncodingProfile Profile);

public class ValidateStage(IProfileValidator validator, ILogger<ValidateStage> logger)
    : IPipelineStage<ValidateInput, ValidateInput>
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

        ValidationResult result = validator.Validate(input.Profile);

        if (!result.IsValid)
        {
            string errors = string.Join(
                "; ",
                result
                    .Errors.Where(e => e.Severity == ValidationSeverity.Error)
                    .Select(e => e.Message)
            );

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

        foreach (
            ValidationError warning in result.Errors.Where(e =>
                e.Severity == ValidationSeverity.Warning
            )
        )
            logger.LogWarning(
                "[{CorrelationId}] Validation warning: {Message}",
                context.CorrelationId,
                warning.Message
            );

        return Task.FromResult<StageResult>(new StageSuccess<ValidateInput>(input));
    }
}
