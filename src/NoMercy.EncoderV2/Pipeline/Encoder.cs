using NoMercy.EncoderV2.Abstractions;

namespace NoMercy.EncoderV2.Pipeline;

/// <summary>
/// Simplified encoder facade implementing IEncoder
/// </summary>
public sealed class Encoder : IEncoder
{
    private readonly IEncodingPipeline _pipeline;
    private readonly IMediaAnalyzer _analyzer;
    private readonly IProfileRegistry _profileRegistry;

    public Encoder(
        IEncodingPipeline pipeline,
        IMediaAnalyzer analyzer,
        IProfileRegistry profileRegistry)
    {
        _pipeline = pipeline;
        _analyzer = analyzer;
        _profileRegistry = profileRegistry;
    }

    public async Task<EncodingJobResult> EncodeAsync(
        string inputPath,
        string outputPath,
        string profileId,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IEncodingProfile? profile = await _profileRegistry.GetProfileAsync(profileId, cancellationToken);

        if (profile == null)
        {
            return new EncodingJobResult
            {
                JobId = Guid.NewGuid().ToString(),
                Success = false,
                Errors = [$"Profile '{profileId}' not found"]
            };
        }

        return await EncodeAsync(inputPath, outputPath, profile, progress, cancellationToken);
    }

    public async Task<EncodingJobResult> EncodeAsync(
        string inputPath,
        string outputPath,
        IEncodingProfile profile,
        IProgress<EncodingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (!File.Exists(inputPath))
        {
            return new EncodingJobResult
            {
                JobId = Guid.NewGuid().ToString(),
                Success = false,
                Errors = [$"Input file not found: {inputPath}"]
            };
        }

        // Validate profile
        ValidationResult validation = profile.Validate();
        if (!validation.IsValid)
        {
            return new EncodingJobResult
            {
                JobId = Guid.NewGuid().ToString(),
                Success = false,
                Errors = [.. validation.Errors]
            };
        }

        // Create job
        EncodingJob job = new()
        {
            Id = Guid.NewGuid().ToString(),
            InputPath = inputPath,
            OutputPath = outputPath,
            Profile = profile,
            Priority = 5
        };

        // Submit and wait
        string jobId = await _pipeline.SubmitJobAsync(job, cancellationToken);

        Progress<EncodingJobStatus>? statusProgress = progress != null
            ? new Progress<EncodingJobStatus>(status =>
            {
                progress.Report(new EncodingProgress
                {
                    Percentage = status.Progress,
                    Elapsed = status.Elapsed,
                    Estimated = status.EstimatedRemaining
                });
            })
            : null;

        return await _pipeline.WaitForCompletionAsync(jobId, statusProgress, cancellationToken);
    }

    public Task<MediaInfo> AnalyzeAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        return _analyzer.AnalyzeAsync(inputPath, cancellationToken);
    }
}
