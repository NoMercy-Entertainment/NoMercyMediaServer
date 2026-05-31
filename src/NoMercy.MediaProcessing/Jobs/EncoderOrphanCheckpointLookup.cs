using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Jobs;
using NoMercy.Queue.MediaServer;

namespace NoMercy.MediaProcessing.Jobs;

/// <summary>
/// Looks up crash checkpoints for encoder queue jobs during orphan recovery.
/// Extracts the output directory from the serialized job payload, then
/// delegates to <see cref="ICheckpointStore"/> to check for a crash checkpoint.
/// </summary>
public class EncoderOrphanCheckpointLookup(
    ICheckpointStore checkpointStore,
    ILogger<EncoderOrphanCheckpointLookup> logger
) : IOrphanCheckpointLookup
{
    public async Task<bool> HasCheckpointAsync(string jobPayload, CancellationToken ct = default)
    {
        string? outputDirectory = ExtractOutputDirectory(jobPayload);
        if (string.IsNullOrEmpty(outputDirectory))
            return false;

        try
        {
            JobCheckpoint? checkpoint = await checkpointStore.LoadAsync(outputDirectory, ct);
            return checkpoint?.FailedAt is not null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to load checkpoint for OutputDirectory={OutputDirectory}",
                outputDirectory
            );
            return false;
        }
    }

    private static string? ExtractOutputDirectory(string payload)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;

            if (
                root.TryGetProperty("OutputDirectory", out JsonElement dirElement)
                && dirElement.ValueKind == JsonValueKind.String
            )
            {
                return dirElement.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
