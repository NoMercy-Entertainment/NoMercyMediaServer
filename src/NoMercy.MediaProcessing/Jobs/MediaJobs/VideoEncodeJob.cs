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
using Serilog.Events;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

public class VideoEncodeJob : AbstractEncoderJob
{
    public override string QueueName => "encoder";
    public override int Priority => 4;
    public string Status { get; set; } = "pending";

    public override async Task Handle()
    {
        await using MediaContext context = new();

        await using LibraryRepository libraryRepository = new(context);
        FileRepository fileRepository = new(context);
        FileManager fileManager = new(fileRepository);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
            return;

        List<EncoderProfile> profiles = folder
            .EncoderProfileFolder.Select(e => e.EncoderProfile)
            .ToList();

        if (profiles.Count == 0)
            return;

        FileMetadata fileMetadata = await GetFileMetaData(folder, context);
        if (!fileMetadata.Success)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();

        foreach (EncoderProfile dbProfile in profiles)
        {
            try
            {
                if (dbProfile.VideoProfiles.Length == 0 && dbProfile.AudioProfiles.Length == 0)
                {
                    Logger.Encoder(
                        $"Skipping profile {dbProfile.Name}: no video or audio profiles configured"
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
                            ProfileName = dbProfile.Name,
                        }
                    );
                }

                EncodingProfile encodingProfile = ProfileMapper.FromV1(
                    dbProfile.Id,
                    dbProfile.Name,
                    dbProfile.Container ?? "m3u8",
                    dbProfile
                        .VideoProfiles.Select(v => new V1VideoProfile(
                            v.Codec,
                            v.Bitrate,
                            v.Width,
                            v.Height,
                            v.Preset,
                            v.Profile,
                            v.Tune,
                            v.Level,
                            v.SegmentName,
                            v.PlaylistName,
                            v.ColorSpace,
                            v.Crf,
                            v.KeyInt,
                            v.ConvertHdrToSdr,
                            v.CustomArguments
                        ))
                        .ToArray(),
                    dbProfile
                        .AudioProfiles.Select(a => new V1AudioProfile(
                            a.Codec,
                            a.Channels,
                            a.SampleRate,
                            a.SegmentName,
                            a.PlaylistName,
                            a.AllowedLanguages,
                            a.CustomArguments
                        ))
                        .ToArray(),
                    dbProfile
                        .SubtitleProfiles.Select(s => new V1SubtitleProfile(
                            s.Codec,
                            s.PlaylistName,
                            s.AllowedLanguages,
                            s.CustomArguments
                        ))
                        .ToArray(),
                    dbProfile.Thumbnails is not null
                        ? new V1ThumbnailProfile(
                            dbProfile.Thumbnails.Width,
                            dbProfile.Thumbnails.IntervalSeconds
                        )
                        : null
                );

                IEncoder encoder = EncoderProvider.Resolve();

                EncodingRequest request = new(
                    InputPath: InputFile,
                    OutputDirectory: fileMetadata.Path,
                    Profile: encodingProfile,
                    MediaTitle: fileMetadata.FileName
                );

                IEncoderProcessRegistry? processRegistry =
                    EncoderProvider.ResolveService<IEncoderProcessRegistry>();

                EventBusProgressObserver progressObserver = new(
                    jobId: fileMetadata.Id,
                    title: fileMetadata.Title,
                    baseFolder: fileMetadata.Path,
                    sharePath: fileMetadata.Path,
                    videoStreams: dbProfile
                        .VideoProfiles.Select(v => $"{v.Width}x{v.Height} {v.Codec}")
                        .ToList(),
                    audioStreams: dbProfile
                        .AudioProfiles.Select(a => $"{a.Codec} {a.Channels}ch")
                        .ToList(),
                    subtitleStreams: dbProfile.SubtitleProfiles.Select(s => s.Codec).ToList(),
                    hasGpu: false,
                    isHdr: false,
                    registry: processRegistry
                );

                EncodingResult result = await encoder.EncodeAsync(request, progressObserver);

                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        $"Encoding failed for {InputFile}: {result.Error?.Message ?? "unknown error"}"
                    );
                }

                Logger.Encoder(
                    $"Encoded {InputFile} → {result.OutputPath} in {result.Duration.TotalSeconds:F1}s ({result.Metrics.EncoderUsed})"
                );

                await RunBitmapSubtitleOcrAsync(fileMetadata, InputFile);

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
                            OutputPath = fileMetadata.Path,
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
    /// V1 parity: after a successful encode, convert any bitmap subtitle streams
    /// (PGS / VobSub / DVB) into WebVTT via Tesseract OCR. No-op when the encoder
    /// analyzer isn't wired in the DI container or the source has no bitmap subs.
    /// </summary>
    private async Task RunBitmapSubtitleOcrAsync(FileMetadata fileMetadata, string inputPath)
    {
        IMediaAnalyzer? analyzer = EncoderProvider.ResolveService<IMediaAnalyzer>();
        ISubtitleOcrEngine? ocrEngine = EncoderProvider.ResolveService<ISubtitleOcrEngine>();

        if (analyzer is null || ocrEngine is null)
            return;

        MediaInfo mediaInfo;
        try
        {
            mediaInfo = await analyzer.AnalyzeAsync(inputPath, CancellationToken.None);
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
        string basePath = Path.Combine(folder.Path, folderName);
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
}
