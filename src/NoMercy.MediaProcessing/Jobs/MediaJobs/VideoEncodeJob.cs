using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using Serilog.Events;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Coordinator job for video encoding. Runs the pipeline through PlanStage,
/// decomposes the output plan into per-rung child tasks, and enqueues each
/// as a separate <see cref="EncodeTaskJob"/> in the <c>encoder-task</c> queue.
///
/// <para>When the resolved strategy returns a single
/// <see cref="EncodeTaskKind.Whole"/> task (MP4, MKV, non-splittable formats)
/// the coordinator falls back to the original inline-encode path so existing
/// behaviour is fully preserved.</para>
///
/// <para>For decomposable strategies (HLS, DASH, two-pass) the coordinator
/// returns immediately after enqueueing children. Post-encode work (history,
/// OCR, library refresh) runs inside an <see cref="EncodeTaskCompletedEvent"/>
/// handler on a thread-pool thread when all children complete.</para>
/// </summary>
public class VideoEncodeJob : AbstractEncoderJob, IJobIdReceiver
{
    public override string QueueName => "encoder";
    public override int Priority => 4;
    public string Status { get; set; } = "pending";

    private int _selfJobId;

    public void ReceiveJobId(int jobId) => _selfJobId = jobId;

    public override async Task Handle()
    {
        await using MediaContext context = new();

        await using LibraryRepository libraryRepository = new(context, StorageDriver);
        FileRepository fileRepository = new(context, StorageDriver);
        FileManager fileManager = new(fileRepository, StorageFactory, StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
            return;

        List<EncodingPreset> presets = folder
            .EncodingPresetFolders.Where(link => link.Preset is not null)
            .Select(link => link.Preset!)
            .ToList();

        if (presets.Count == 0)
            return;

        FileMetadata fileMetadata = await GetFileMetaData(folder, context);
        if (!fileMetadata.Success)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();

        foreach (EncodingPreset preset in presets)
        {
            try
            {
                EncodingProfile encodingProfile;
                try
                {
                    encodingProfile = PresetResolver.Resolve(
                        preset.Id,
                        new DbPresetLookup(context)
                    );
                }
                catch (Exception ex)
                {
                    Logger.Encoder(
                        $"Skipping preset '{preset.Name}' ({preset.Id}): resolve failed — {ex.Message}",
                        LogEventLevel.Warning
                    );
                    continue;
                }

                if (encodingProfile.Video is null && encodingProfile.Audio.Length == 0)
                {
                    Logger.Encoder(
                        $"Skipping preset {preset.Name}: no video or audio outputs configured"
                    );
                    continue;
                }

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStartedEvent
                        {
                            JobId = fileMetadata.Id,
                            InputPath = InputFile,
                            OutputPath = fileMetadata.Path,
                            ProfileName = preset.Name,
                        }
                    );
                }

                IEncodingOrchestrator orchestrator =
                    EncoderProvider.ResolveService<IEncodingOrchestrator>()
                    ?? throw new InvalidOperationException(
                        "IEncodingOrchestrator is not registered. Did AddNoMercyEncoder() run?"
                    );

                IStorage destinationStorage = StorageFactory.For(
                    folder.Id,
                    folder.DriverId,
                    folder.Path
                );

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

                string groupTag = Ulid.NewUlid().ToString();
                DecomposedTask[] tasks = await orchestrator.DecomposeAsync(request, groupTag);

                bool isWhole = tasks.Length == 1 && tasks[0].Kind == EncodeTaskKind.Whole;

                if (isWhole)
                {
                    await RunInlineAsync(
                        orchestrator,
                        request,
                        encodingProfile,
                        preset,
                        fileMetadata,
                        stopwatch,
                        sourceStorage,
                        context,
                        fileManager,
                        folder
                    );
                    continue;
                }

                await DispatchChildrenAsync(
                    tasks,
                    preset.Id,
                    encodingProfile,
                    fileMetadata,
                    stopwatch,
                    sourceStorage,
                    context,
                    fileManager,
                    folder
                );
            }
            catch (Exception ex)
            {
                Logger.Encoder(ex, LogEventLevel.Error);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = fileMetadata.Id,
                            Status = "failed",
                            Title = fileMetadata.Title,
                            Message = ex.Message,
                        }
                    );

                    await EventBusProvider.Current.PublishAsync(
                        new EncodingFailedEvent
                        {
                            JobId = fileMetadata.Id,
                            InputPath = InputFile,
                            ErrorMessage = ex.Message,
                            ExceptionType = ex.GetType().Name,
                        }
                    );
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Original single-process encode path. Used when the strategy returns a
    /// single Whole task (MP4, MKV, audio-only formats that cannot split).
    /// </summary>
    private async Task RunInlineAsync(
        IEncodingOrchestrator orchestrator,
        EncodingRequest request,
        EncodingProfile encodingProfile,
        EncodingPreset preset,
        FileMetadata fileMetadata,
        Stopwatch stopwatch,
        IStorage sourceStorage,
        MediaContext context,
        FileManager fileManager,
        Folder folder
    )
    {
        IEncoderProcessRegistry? processRegistry =
            EncoderProvider.ResolveService<IEncoderProcessRegistry>();

        EventBusProgressObserver progressObserver = new(
            jobId: fileMetadata.Id,
            title: fileMetadata.Title,
            baseFolder: fileMetadata.Path,
            sharePath: fileMetadata.Path,
            videoStreams: SummarizeVideo(encodingProfile),
            audioStreams: encodingProfile
                .Audio.Select(audio =>
                    $"{audio.Codec.ToString().ToLowerInvariant()} {audio.Channels}ch"
                )
                .ToList(),
            subtitleStreams: encodingProfile
                .Subtitles.Select(subtitle => subtitle.Codec.ToString().ToLowerInvariant())
                .ToList(),
            hasGpu: false,
            isHdr: false,
            registry: processRegistry
        );

        EncodingResult result = await orchestrator.EncodeAsync(request, progressObserver);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Encoding failed for {InputFile}: {result.Error?.Message ?? "unknown error"}"
            );
        }

        Logger.Encoder(
            $"Encoded {InputFile} → {result.OutputPath} in {result.Duration.TotalSeconds:F1}s ({result.Metrics?.EncoderUsed ?? "unknown"})"
        );

        await PublishStageAsync(fileMetadata, "Recording encoding history");
        await RecordEncodingHistoryAsync(context, preset, result, InputFile, StorageDriver);

        await PublishStageAsync(fileMetadata, "Checking source subtitles");
        await RunBitmapSubtitleOcrAsync(fileMetadata, InputFile, sourceStorage);

        await PublishStageAsync(fileMetadata, "Refreshing library");
        fileManager.FilterFiles(fileMetadata.FileName);
        await fileManager.FindFiles(fileMetadata.Id, folder.FolderLibraries.First().Library);

        if (EventBusProvider.IsConfigured)
        {
            stopwatch.Stop();
            await EventBusProvider.Current.PublishAsync(
                new EncodingCompletedEvent
                {
                    JobId = fileMetadata.Id,
                    OutputPath = result.OutputPath,
                    Duration = stopwatch.Elapsed,
                }
            );
        }
    }

    /// <summary>
    /// Decomposable encode path. Stamps each task with the coordinator's own
    /// job ID as <c>ParentJobId</c>, subscribes to
    /// <see cref="EncodeTaskCompletedEvent"/>, enqueues N child
    /// <see cref="EncodeTaskJob"/> entries, then returns immediately so the
    /// coordinator's worker thread is freed. Post-encode finalization runs
    /// inside the event handler when all children complete.
    /// </summary>
    private async Task DispatchChildrenAsync(
        DecomposedTask[] tasks,
        Ulid presetId,
        EncodingProfile encodingProfile,
        FileMetadata fileMetadata,
        Stopwatch stopwatch,
        IStorage sourceStorage,
        MediaContext context,
        FileManager fileManager,
        Folder folder
    )
    {
        int parentJobId = _selfJobId;
        string groupTag = tasks[0].GroupTag;
        int expectedCount = tasks.Length;
        int completedCount = 0;
        int failedCount = 0;
        IDisposable? subscription = null;

        Logger.Encoder(
            $"[VideoEncodeJob] Decomposed into {expectedCount} child tasks (groupTag={groupTag})"
        );

        if (EventBusProvider.IsConfigured)
        {
            subscription = EventBusProvider.Current.Subscribe<EncodeTaskCompletedEvent>(
                async (encodeTaskCompletedEvent, ct) =>
                {
                    if (encodeTaskCompletedEvent.GroupTag != groupTag)
                        return;

                    int completed = Interlocked.Increment(ref completedCount);
                    if (!encodeTaskCompletedEvent.Success)
                        Interlocked.Increment(ref failedCount);

                    Logger.Encoder(
                        $"[VideoEncodeJob] Child task '{encodeTaskCompletedEvent.TaskId}' {(encodeTaskCompletedEvent.Success ? "succeeded" : "failed")} ({completed}/{expectedCount})"
                    );

                    if (completed < expectedCount)
                        return;

                    subscription?.Dispose();

                    await RunPostEncodeAsync(
                        fileMetadata,
                        stopwatch,
                        sourceStorage,
                        fileManager,
                        folder,
                        failedCount: Volatile.Read(ref failedCount)
                    );
                }
            );
        }

        // Stamp each task with the coordinator's queue-job ID, then enqueue.
        NoMercyQueue.JobDispatcher dispatcher =
            QueueRunner.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "QueueRunner.Current is null — queue not initialized"
            );

        foreach (DecomposedTask task in tasks)
        {
            DecomposedTask stamped = task with { ParentJobId = parentJobId };

            EncodeTaskJob childJob = new()
            {
                LibraryId = LibraryId,
                FolderId = FolderId,
                Id = Id,
                InputFile = InputFile,
                SourceDriverId = SourceDriverId,
                PresetId = presetId,
                Task = stamped,
            };

            dispatcher.DispatchChild(
                childJob,
                onQueue: childJob.QueueName,
                priority: childJob.Priority,
                parentJobId: parentJobId,
                groupTag: groupTag
            );
        }

        Logger.Encoder(
            $"[VideoEncodeJob] Enqueued {tasks.Length} child tasks; coordinator returning"
        );

        await Task.CompletedTask;
    }

    private async Task RunPostEncodeAsync(
        FileMetadata fileMetadata,
        Stopwatch stopwatch,
        IStorage sourceStorage,
        FileManager fileManager,
        Folder folder,
        int failedCount
    )
    {
        try
        {
            if (failedCount > 0)
            {
                Logger.Encoder(
                    $"[VideoEncodeJob] {failedCount} child task(s) failed for groupTag — skipping post-encode steps",
                    LogEventLevel.Warning
                );

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = fileMetadata.Id,
                            Status = "failed",
                            Title = fileMetadata.Title,
                            Message = $"{failedCount} rung(s) failed",
                        }
                    );
                }

                return;
            }

            await using MediaContext finCtx = new();

            await PublishStageAsync(fileMetadata, "Checking source subtitles");
            await RunBitmapSubtitleOcrAsync(fileMetadata, InputFile, sourceStorage);

            await PublishStageAsync(fileMetadata, "Refreshing library");
            Library library = folder.FolderLibraries.First().Library;
            fileManager.FilterFiles(fileMetadata.FileName);
            await fileManager.FindFiles(fileMetadata.Id, library);

            stopwatch.Stop();
            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(
                    new EncodingCompletedEvent
                    {
                        JobId = fileMetadata.Id,
                        OutputPath = fileMetadata.Path,
                        Duration = stopwatch.Elapsed,
                    }
                );
            }
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"[VideoEncodeJob] Post-encode finalization failed: {ex.Message}",
                LogEventLevel.Error
            );
        }
    }

    private static List<string> SummarizeVideo(EncodingProfile profile)
    {
        List<string> summary = [];

        if (profile.Ladder?.Rungs is { Length: > 0 } rungs)
        {
            foreach (LadderRung rung in rungs)
                summary.Add(
                    $"{rung.Width}x{rung.Height} {rung.Codec.ToString().ToLowerInvariant()}"
                );
            return summary;
        }

        if (profile.Video is { } video)
            summary.Add(
                $"{video.Width}x{video.Height ?? 0} {video.Codec.ToString().ToLowerInvariant()}"
            );

        return summary;
    }

    private static async Task RecordEncodingHistoryAsync(
        MediaContext context,
        EncodingPreset preset,
        EncodingResult result,
        string inputPath,
        IStorageDriver storageDriver
    )
    {
        try
        {
            long inputSize = 0;
            try
            {
                if (storageDriver.FileExists(inputPath))
                    inputSize = storageDriver.GetFileSize(inputPath);
            }
            catch
            {
                // keep inputSize = 0 when the file is inaccessible
            }

            if (result.Metrics is null)
                return;

            double ratio =
                inputSize > 0 && result.Metrics.OutputSizeBytes > 0
                    ? (double)result.Metrics.OutputSizeBytes / inputSize
                    : 0;

            context.EncodingHistory.Add(
                new()
                {
                    InputPath = inputPath,
                    OutputPath = result.OutputPath,
                    ProfileId = preset.Id,
                    ProfileName = preset.Name,
                    EncoderUsed = result.Metrics.EncoderUsed,
                    GpuUsed = result.Metrics.GpuUsed,
                    DurationSeconds = result.Duration.TotalSeconds,
                    InputSizeBytes = inputSize,
                    OutputSizeBytes = result.Metrics.OutputSizeBytes,
                    CompressionRatio = ratio,
                    AverageSpeed = result.Metrics.AverageSpeed,
                    AverageFps = result.Metrics.AverageFps,
                }
            );
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"Could not write encoding history: {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }

    private async Task RunBitmapSubtitleOcrAsync(
        FileMetadata fileMetadata,
        string inputPath,
        IStorage sourceStorage
    )
    {
        IMediaAnalyzer? analyzer = EncoderProvider.ResolveService<IMediaAnalyzer>();
        ISubtitleOcrEngine? ocrEngine = EncoderProvider.ResolveService<ISubtitleOcrEngine>();

        if (analyzer is null || ocrEngine is null)
            return;

        MediaInfo mediaInfo;
        try
        {
            mediaInfo = await analyzer.AnalyzeAsync(
                inputPath,
                sourceStorage,
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"Could not analyze {inputPath} for OCR: {ex.Message}",
                LogEventLevel.Warning
            );
            return;
        }

        List<SubtitleStreamInfo> bitmap = mediaInfo
            .SubtitleStreams.Where(subtitle => !subtitle.IsTextBased)
            .ToList();

        if (bitmap.Count == 0)
            return;

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new EncodingStageChangedEvent
                {
                    JobId = fileMetadata.Id,
                    Status = "running",
                    Title = fileMetadata.Title,
                    Message = "Converting subtitles",
                }
            );
        }

        foreach (SubtitleStreamInfo stream in bitmap)
        {
            string language = stream.Language ?? "eng";
            try
            {
                SubtitleTrack track = await ocrEngine.OcrAsync(
                    inputPath,
                    stream.Index,
                    language,
                    SubtitleCodecType.WebVtt,
                    CancellationToken.None
                );
                Logger.Encoder($"OCR {language} → {track.FilePath} ({track.CueCount} cues)");
            }
            catch (Exception ex)
            {
                Logger.Encoder(
                    $"OCR failed for {inputPath} stream {stream.Index} ({language}): {ex.Message}",
                    LogEventLevel.Warning
                );
            }
        }
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
        string? imgPath = movie?.Backdrop ?? episode?.Still;

        return new()
        {
            Success = true,
            FolderName = folderName,
            Title = title,
            FileName = fileName,
            Path = basePath,
            Id = baseId,
            ImgPath = imgPath,
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
        public string? ImgPath { get; set; }
    }

    private static async Task PublishStageAsync(FileMetadata fileMetadata, string message)
    {
        if (!EventBusProvider.IsConfigured)
            return;
        await EventBusProvider.Current.PublishAsync(
            new EncodingStageChangedEvent
            {
                JobId = fileMetadata.Id,
                Status = "running",
                Title = fileMetadata.Title,
                Message = message,
            }
        );
    }
}
