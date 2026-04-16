namespace NoMercy.Encoder.Jobs;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Infrastructure;

/// <summary>
/// Persists job checkpoints as JSON files next to the encode output.
/// File location: {OutputDirectory}/.checkpoint.json — one per output. Resume
/// reads this; on success the caller deletes it.
/// </summary>
public class JsonCheckpointStore(IFileSystem fileSystem, ILogger<JsonCheckpointStore> logger)
    : ICheckpointStore
{
    private const string FileName = ".checkpoint.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public async Task SaveAsync(JobCheckpoint checkpoint, CancellationToken ct = default)
    {
        Directory.CreateDirectory(checkpoint.OutputDirectory);
        string path = Path.Combine(checkpoint.OutputDirectory, FileName);

        JobCheckpoint toWrite = checkpoint with { LastUpdated = DateTime.UtcNow };
        string json = JsonSerializer.Serialize(toWrite, SerializerOptions);
        await File.WriteAllTextAsync(path, json, ct);

        logger.LogDebug("Checkpoint saved: {JobId} → {Path}", checkpoint.JobId, path);
    }

    public async Task<JobCheckpoint?> LoadAsync(
        string outputDirectory,
        CancellationToken ct = default
    )
    {
        string path = Path.Combine(outputDirectory, FileName);
        if (!fileSystem.FileExists(path))
            return null;

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<JobCheckpoint>(
                stream,
                SerializerOptions,
                ct
            );
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Checkpoint at {Path} is corrupt; treating as missing", path);
            return null;
        }
    }

    public Task DeleteAsync(string outputDirectory, CancellationToken ct = default)
    {
        string path = Path.Combine(outputDirectory, FileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            logger.LogDebug("Checkpoint deleted at {Path}", path);
        }

        return Task.CompletedTask;
    }
}
