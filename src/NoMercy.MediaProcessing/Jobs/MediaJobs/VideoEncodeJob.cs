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
using Serilog.Events;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class VideoEncodeJob : AbstractEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 4;
    public string Status { get; set; } = "pending";

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

                // Destination storage: the library folder the encoded output lands in.
                IStorage destinationStorage = StorageFactory.For(
                    folder.Id,
                    folder.DriverId,
                    folder.Path
                );

                // Source storage: when SourceDriverId is set the input file lives on
                // a different driver (e.g. a Vault NFS share). Resolve the root-level
                // storage for that driver so AcquireLocalPathAsync can stage the file.
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
                    videoStreams: SummarizeVideo(encodingProfile),
                    audioStreams: encodingProfile
                        .Audio.Select(a =>
                            $"{a.Codec.ToString().ToLowerInvariant()} {a.Channels}ch"
                        )
                        .ToList(),
                    subtitleStreams: encodingProfile
                        .Subtitles.Select(s => s.Codec.ToString().ToLowerInvariant())
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
                await fileManager.FindFiles(
                    fileMetadata.Id,
                    folder.FolderLibraries.First().Library
                );

                if (EventBusProvider.IsConfigured)
                {
                    stopwatch.Stop();
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingCompletedEvent
                        {
                            JobId = fileMetadata.Id,
                            // Master playlist / muxed file from the orchestrator,
                            // not the output directory — OcrPostEncodeSubscriber
                            // checks the extension to gate HLS/DASH detection
                            // and ffprobes this path directly.
                            OutputPath = result.OutputPath,
                            Duration = stopwatch.Elapsed,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Logger.Encoder(e, LogEventLevel.Error);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = fileMetadata.Id,
                            Status = "failed",
                            Title = fileMetadata.Title,
                            Message = e.Message,
                        }
                    );

                    await EventBusProvider.Current.PublishAsync(
                        new EncodingFailedEvent
                        {
                            JobId = fileMetadata.Id,
                            InputPath = InputFile,
                            ErrorMessage = e.Message,
                            ExceptionType = e.GetType().Name,
                        }
                    );
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Append one EncodingHistory row per successful encode. Failures are
    /// swallowed — a dashboard-history miss should never fail a working encode.
    /// Denormalizes profile name / encoder so the row survives profile deletion.
    /// </summary>
    /// <summary>
    /// One line per ladder rung (or the single reference Video) for the
    /// progress observer. Auto ladders that haven't been expanded yet just
    /// show the reference output — sufficient for the dashboard.
    /// </summary>
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

            // Result.Metrics is nullable — strategies that bypass the metrics
            // collector (e.g. dry-run / preview) leave it null. Skip the row
            // entirely rather than NRE: history without metrics is useless.
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

    /// <summary>
    /// V1 parity: after a successful encode, convert any bitmap subtitle streams
    /// (PGS / VobSub / DVB) into WebVTT via Tesseract OCR. No-op when the encoder
    /// analyzer isn't wired in the DI container or the source has no bitmap subs.
    /// </summary>
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
            .SubtitleStreams.Where(s => !s.IsTextBased)
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
        // Destination storage is already rooted at folder.Path (via StorageFactory.For).
        // Pass only folderName so paths stay relative to the storage root — avoids
        // double-prefix on NFS (export/folder.Path/folder.Path/folderName).
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
