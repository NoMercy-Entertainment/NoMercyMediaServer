using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

namespace NoMercy.Encoder.Strategies;

/// <summary>
/// Shared single-pass wrapper: delegates encoding to <see cref="IEncoder"/>
/// and provides crash-path partial-output sweep so the destination directory
/// is not left in a half-written state after a non-cancel encode failure.
///
/// On cancellation the checkpoint is NOT written (user-cancelled encodes are
/// not resumable). On a crash failure any segments already published to the
/// destination are swept, keeping the dir clean for a future retry.
/// The crash checkpoint written by ExecuteStage is left intact so orphan
/// recovery can re-queue with resume-from-keyframe.
/// </summary>
public abstract class SinglePassStrategyBase(IEncoder encoder, ILogger logger, IStorage storage)
    : IEncodingStrategy
{
    public abstract OutputFormat Format { get; }
    public EncodeMode EncodeMode => EncodeMode.SinglePass;

    public virtual DecomposedTask[] Decompose(OutputPlan plan, string groupTag) =>
        [IEncodingStrategy.WholeTask(groupTag)];

    public async Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        IStorage effectiveStorage = request.DestinationStorage ?? request.SourceStorage ?? storage;

        try
        {
            EncodingResult result = await encoder.EncodeAsync(request, progress, ct);

            if (!result.Success)
            {
                DeletePartialOutput(
                    request.OutputDirectory,
                    effectiveStorage,
                    preserveCheckpoint: true
                );
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            DeletePartialOutput(
                request.OutputDirectory,
                effectiveStorage,
                preserveCheckpoint: false
            );
            throw;
        }
    }

    private void DeletePartialOutput(string outputDirectory, IStorage stor, bool preserveCheckpoint)
    {
        try
        {
            if (!stor.Exists(outputDirectory))
                return;

            foreach (
                StorageEntry entry in stor.List(outputDirectory, "*", recursive: true)
                    .Where(entry => !entry.IsDirectory)
                    .Where(entry =>
                        !preserveCheckpoint
                        || !Path.GetFileName(entry.Path)
                            .Equals(".checkpoint.json", StringComparison.OrdinalIgnoreCase)
                    )
            )
            {
                try
                {
                    stor.Delete(entry.Path);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to delete partial output file {File} after crash",
                        entry.Path
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to enumerate partial output for deletion after crash in {OutputDirectory}",
                outputDirectory
            );
        }
    }
}
