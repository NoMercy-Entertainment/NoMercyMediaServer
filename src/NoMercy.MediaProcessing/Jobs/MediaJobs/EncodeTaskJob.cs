using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Events;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Executes a single decomposed encode task produced by
/// <see cref="VideoEncodeJob"/> coordinator. One child job per video rung,
/// audio group, subtitle track, or thumbnail strip. Lives in the
/// <c>encoder-task</c> queue so that rung-level failures are isolated and
/// individual rungs can be retried without restarting the whole encode.
/// </summary>
[Serializable]
public class EncodeTaskJob : AbstractEncoderJob
{
    public override string QueueName => "encoder-task";
    public override int Priority => 4;

    /// <summary>Preset that defines the full encoding profile for this job.</summary>
    public Ulid PresetId { get; set; }

    /// <summary>Task descriptor from the coordinator's decompose call.</summary>
    public DecomposedTask Task { get; set; } = null!;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using LibraryRepository libraryRepository = new(context, StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
            return;

        EncodingProfile encodingProfile;
        try
        {
            encodingProfile = PresetResolver.Resolve(PresetId, new DbPresetLookup(context));
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"[EncodeTaskJob] Skipping task '{Task.Label}' for preset {PresetId}: resolve failed — {ex.Message}",
                LogEventLevel.Warning
            );
            await PublishCompletedAsync(success: false, error: ex.Message, artifacts: []);
            return;
        }

        FileMetadata fileMetadata = await GetFileMetaData(folder, context);
        if (!fileMetadata.Success)
        {
            await PublishCompletedAsync(
                success: false,
                error: "Could not resolve media metadata",
                artifacts: []
            );
            return;
        }

        IEncodingOrchestrator orchestrator =
            EncoderProvider.ResolveService<IEncodingOrchestrator>()
            ?? throw new InvalidOperationException(
                "IEncodingOrchestrator is not registered. Did AddNoMercyEncoder() run?"
            );

        IStorage destinationStorage = StorageFactory.For(folder.Id, folder.DriverId, folder.Path);

        IStorage sourceStorage = SourceDriverId.HasValue
            ? StorageFactory.For(SourceDriverId.Value, SourceDriverId.Value, string.Empty)
            : destinationStorage;

        EncodingRequest request = new(
            InputPath: InputFile,
            OutputDirectory: fileMetadata.Path,
            Profile: encodingProfile,
            MediaTitle: fileMetadata.FileName,
            SourceStorage: sourceStorage,
            DestinationStorage: destinationStorage
        );

        IEncoderProcessRegistry? processRegistry =
            EncoderProvider.ResolveService<IEncoderProcessRegistry>();

        EventBusProgressObserver progressObserver = new(
            jobId: fileMetadata.Id,
            title: fileMetadata.Title,
            baseFolder: fileMetadata.Path,
            sharePath: fileMetadata.Path,
            registry: processRegistry
        );

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            EncodingResult result = await orchestrator.EncodeAsync(request, Task, progressObserver);
            stopwatch.Stop();

            if (!result.Success)
            {
                string errorMsg =
                    result.Error?.Message ?? result.EnrichedError?.Message ?? "encode failed";
                Logger.Encoder(
                    $"[EncodeTaskJob] Task '{Task.Label}' failed: {errorMsg}",
                    LogEventLevel.Warning
                );
                await PublishCompletedAsync(success: false, error: errorMsg, artifacts: []);
                return;
            }

            Logger.Encoder(
                $"[EncodeTaskJob] Task '{Task.Label}' completed in {stopwatch.Elapsed.TotalSeconds:F1}s"
            );

            List<string> artifactPaths = result
                .Artifacts.Select(artifact => artifact.Path)
                .ToList();
            await PublishCompletedAsync(success: true, error: null, artifacts: artifactPaths);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Encoder(
                $"[EncodeTaskJob] Task '{Task.Label}' threw: {ex.Message}",
                LogEventLevel.Error
            );
            await PublishCompletedAsync(success: false, error: ex.Message, artifacts: []);
            throw;
        }
    }

    private async Task PublishCompletedAsync(
        bool success,
        string? error,
        IReadOnlyList<string> artifacts
    )
    {
        if (!EventBusProvider.IsConfigured)
            return;

        await EventBusProvider.Current.PublishAsync(
            new EncodeTaskCompletedEvent
            {
                TaskId = Task.TaskId,
                ParentJobId = Task.ParentJobId,
                GroupTag = Task.GroupTag,
                Success = success,
                Error = error,
                Kind = Task.Kind,
                OutputArtifacts = artifacts,
            }
        );
    }

    private async Task<FileMetadata> GetFileMetaData(Folder folder, MediaContext context)
    {
        Movie? movie = folder.FolderLibraries.Any(x => x.Library.Type == Config.MovieMediaType)
            ? await context.Movies.FirstOrDefaultAsync(x => x.Id == Id.ToInt())
            : null;

        Episode? episode = folder.FolderLibraries.Any(x =>
            x.Library.Type == Config.TvMediaType || x.Library.Type == Config.AnimeMediaType
        )
            ? await context.Episodes.Include(x => x.Tv).FirstOrDefaultAsync(x => x.Id == Id.ToInt())
            : null;

        if (movie is null && episode is null)
            return new() { Success = false };

        string folderName =
            movie?.CreateFolderName().Replace("/", "")
            ?? episode!.Tv.CreateFolderName().Replace("/", "") + episode.CreateFolderName();

        string title = movie?.CreateTitle() ?? episode!.CreateTitle();
        string fileName = movie?.CreateFileName() ?? episode!.CreateFileName();
        string basePath = folderName;
        int baseId = movie?.Id ?? episode!.Id;

        return new()
        {
            Success = true,
            FolderName = folderName,
            Title = title,
            FileName = fileName,
            Path = basePath,
            Id = baseId,
        };
    }

    private record FileMetadata
    {
        public bool Success { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Id { get; set; }
    }
}
