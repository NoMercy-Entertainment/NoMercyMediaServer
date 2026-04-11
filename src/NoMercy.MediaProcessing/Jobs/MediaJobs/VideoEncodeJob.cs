using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Image;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Subtitle;
using NoMercy.Encoder.Format.Video;
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
        await using QueueContext queueContext = new();

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

        foreach (EncoderProfile profile in profiles)
        {
            BaseContainer container = BaseContainer.Create(profile.Container);

            try
            {
                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStartedEvent
                        {
                            JobId = fileMetadata.Id,
                            InputPath = InputFile,
                            OutputPath = fileMetadata.Path,
                            ProfileName = profile.Name,
                        }
                    );
                }

                await PublishStageAsync(
                    fileMetadata,
                    folder,
                    container,
                    "running",
                    "Preparing to encode"
                );

                BuildVideoStreams(profile, ref container);
                BuildAudioStreams(profile, ref container);
                BuildSubtitleStreams(profile, ref container);

                await PublishStageAsync(
                    fileMetadata,
                    folder,
                    container,
                    "running",
                    "Preparing to encode"
                );

                BaseImage sprite = new Sprite().SetScale(320).SetFilename("thumbs_:framesize:");
                container.AddStream(sprite);

                VideoAudioFile ffmpeg = await new FfMpeg().OpenAsync(InputFile);

                ffmpeg.SetBasePath(fileMetadata.Path);
                ffmpeg.SetTitle(fileMetadata.Title);
                ffmpeg.ToFile(fileMetadata.FileName);

                ffmpeg.AddContainer(container);

                // ffmpeg.Prioritize();

                ffmpeg.Build();

                string fullCommand = ffmpeg.GetFullCommand();
                Logger.Encoder(fullCommand);

                ProgressMeta progressMeta = new()
                {
                    Id = fileMetadata.Id,
                    Title = fileMetadata.Title,
                    BaseFolder = fileMetadata.Path,
                    ShareBasePath = folder.Id + "/" + fileMetadata.FolderName,
                    AudioStreams = container
                        .AudioStreams.Select(x =>
                            $"{x.StreamIndex}:{x.Language}_{x.AudioCodec.SimpleValue}"
                        )
                        .Distinct()
                        .ToList(),
                    VideoStreams = container
                        .VideoStreams.Select(x =>
                            $"{x.StreamIndex}:{x.Scale.W}x{x.Scale.H}_{x.VideoCodec.SimpleValue}"
                        )
                        .Distinct()
                        .ToList(),
                    SubtitleStreams = container
                        .SubtitleStreams.Select(x =>
                            $"{x.StreamIndex}:{x.Language}_{x.SubtitleCodec.SimpleValue}"
                        )
                        .Distinct()
                        .ToList(),
                    HasGpu = container.VideoStreams.Any(x => x.VideoCodec.RequiresGpu),
                    IsHdr = container.VideoStreams.Any(x => x.IsHdr),
                };

                await ffmpeg.Run(fullCommand, fileMetadata.Path, progressMeta);

                await PublishStageAsync(
                    fileMetadata,
                    progressMeta,
                    "running",
                    "Building sprite images"
                );

                await sprite.BuildSprite(progressMeta);

                await PublishStageAsync(
                    fileMetadata,
                    progressMeta,
                    "running",
                    "Building Master Playlist"
                );

                await container.BuildMasterPlaylist();

                await PublishStageAsync(
                    fileMetadata,
                    progressMeta,
                    "running",
                    "Extracting chapters"
                );

                await container.ExtractChapters();

                await PublishStageAsync(fileMetadata, progressMeta, "running", "Extracting fonts");

                await container.ExtractFonts();

                if (ffmpeg.Container.SubtitleStreams.Any(x => x.ConvertSubtitle))
                {
                    await PublishStageAsync(
                        fileMetadata,
                        progressMeta,
                        "running",
                        "Converting subtitles"
                    );

                    List<BaseSubtitle> streams = ffmpeg
                        .Container.SubtitleStreams.Where(x => x.ConvertSubtitle)
                        .ToList();
                    await ffmpeg.ConvertSubtitles(
                        streams,
                        Id.ToInt(),
                        fileMetadata.Title,
                        fileMetadata.ImgPath
                    );
                }

                await PublishStageAsync(fileMetadata, progressMeta, "running", "Scanning files");

                fileManager.FilterFiles(container.FileName);

                await fileManager.FindFiles(
                    fileMetadata.Id,
                    folder.FolderLibraries.First().Library
                );

                await PublishStageAsync(fileMetadata, progressMeta, "completed", "Done");

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

                // Only remove the output directories owned by this profile's streams,
                // not the entire base path — other profiles that completed successfully
                // must not have their output destroyed.
                CleanupPartialOutput(fileMetadata.Path, container);

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

    private static async Task PublishStageAsync(
        FileMetadata fileMetadata,
        Folder folder,
        BaseContainer container,
        string status,
        string message
    )
    {
        if (!EventBusProvider.IsConfigured)
            return;
        await EventBusProvider.Current.PublishAsync(
            new EncodingStageChangedEvent
            {
                JobId = fileMetadata.Id,
                Status = status,
                Title = fileMetadata.Title,
                Message = message,
                BaseFolder = fileMetadata.Path,
                ShareBasePath = folder.Id + "/" + fileMetadata.FolderName,
                VideoStreams = container
                    .VideoStreams.Select(x =>
                        $"{x.StreamIndex}:{x.Scale.W}x{x.Scale.H}_{x.VideoCodec.SimpleValue}"
                    )
                    .Distinct()
                    .ToList(),
                AudioStreams = container
                    .AudioStreams.Select(x =>
                        $"{x.StreamIndex}:{x.Language}_{x.AudioCodec.SimpleValue}"
                    )
                    .Distinct()
                    .ToList(),
                SubtitleStreams = container
                    .SubtitleStreams.Select(x =>
                        $"{x.StreamIndex}:{x.Language}_{x.SubtitleCodec.SimpleValue}"
                    )
                    .Distinct()
                    .ToList(),
                HasGpu = container.VideoStreams.Any(x =>
                    x.VideoCodec.Value == VideoCodecs.H264Nvenc.Value
                    || x.VideoCodec.Value == VideoCodecs.H265Nvenc.Value
                ),
                IsHdr = container.VideoStreams.Any(x => x.IsHdr),
            }
        );
    }

    private static async Task PublishStageAsync(
        FileMetadata fileMetadata,
        ProgressMeta progressMeta,
        string status,
        string message
    )
    {
        if (!EventBusProvider.IsConfigured)
            return;
        await EventBusProvider.Current.PublishAsync(
            new EncodingStageChangedEvent
            {
                JobId = fileMetadata.Id,
                Status = status,
                Title = fileMetadata.Title,
                Message = message,
                BaseFolder = progressMeta.BaseFolder,
                ShareBasePath = progressMeta.ShareBasePath,
                VideoStreams = progressMeta.VideoStreams,
                AudioStreams = progressMeta.AudioStreams,
                SubtitleStreams = progressMeta.SubtitleStreams,
                HasGpu = progressMeta.HasGpu,
                IsHdr = progressMeta.IsHdr,
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

    private static void BuildVideoStreams(
        EncoderProfile encoderProfile,
        ref BaseContainer container
    )
    {
        foreach (IVideoProfile profile in encoderProfile.VideoProfiles)
        {
            // Automatically select the best codec based on system capabilities
            string resolvedCodec = CodecSelector.ResolveBestCodec(profile.Codec);

            BaseVideo stream = BaseVideo
                .Create(resolvedCodec)
                .SetScale(profile.Width, profile.Height)
                .SetConstantRateFactor(profile.Crf)
                .SetFrameRate(profile.Framerate)
                .SetKiloBitrate(profile.Bitrate)
                .ConvertHdrToSdr(profile.ConvertHdrToSdr)
                .SetHlsSegmentFilename(profile.SegmentName)
                .SetHlsPlaylistFilename(profile.PlaylistName)
                .SetColorSpace(profile.ColorSpace)
                .SetPreset(profile.Preset)
                .SetProfile(profile.Profile)
                .SetTune(profile.Tune)
                .SetLevel(profile.Level)
                .SetKeyInt(profile.KeyInt)
                .AddOpts(profile.Opts)
                .AddCustomArguments(profile.CustomArguments);

            container.AddStream(stream);
        }
    }

    private static void BuildAudioStreams(
        EncoderProfile encoderProfile,
        ref BaseContainer container
    )
    {
        foreach (IAudioProfile profile in encoderProfile.AudioProfiles)
        {
            BaseAudio stream = BaseAudio
                .Create(profile.Codec)
                .SetAudioChannels(profile.Channels)
                .SetAllowedLanguages(profile.AllowedLanguages)
                .SetSampleRate(profile.SampleRate)
                .SetHlsSegmentFilename(profile.SegmentName)
                .SetHlsPlaylistFilename(profile.PlaylistName)
                .AddOpts(profile.Opts)
                .AddCustomArguments(profile.CustomArguments);

            container.AddStream(stream);
        }
    }

    private static void BuildSubtitleStreams(
        EncoderProfile? encoderProfile,
        ref BaseContainer container
    )
    {
        foreach (ISubtitleProfile profile in encoderProfile?.SubtitleProfiles ?? [])
        {
            BaseSubtitle stream = BaseSubtitle
                .Create(profile.Codec)
                .SetAllowedLanguages(profile.AllowedLanguages)
                .SetHlsSegmentFilename(profile.SegmentName)
                .SetHlsPlaylistFilename(profile.PlaylistName)
                .AddOpts(profile.Opts)
                .AddCustomArguments(profile.CustomArguments);

            container.AddStream(stream);
        }
    }

    /// <summary>
    /// Deletes only the output subdirectories written by the specified container's
    /// streams.  Leaves all other subdirectories inside <paramref name="basePath"/>
    /// intact so that output from previously completed profiles is not destroyed.
    /// If the container reports no subdirectories (e.g. non-HLS formats writing a
    /// single file), the single output file matching the container filename is
    /// removed instead.
    /// </summary>
    internal static void CleanupPartialOutput(string basePath, BaseContainer container)
    {
        try
        {
            HashSet<string> profileDirs = container.GetOutputSubdirectories();

            if (profileDirs.Count > 0)
            {
                foreach (string subdirName in profileDirs)
                {
                    string fullPath = Path.Combine(basePath, subdirName);
                    if (Directory.Exists(fullPath))
                    {
                        Directory.Delete(fullPath, recursive: true);
                        Logger.Encoder($"Cleaned up partial encoding output: {fullPath}");
                    }
                }
            }
            else if (!string.IsNullOrEmpty(container.FileName))
            {
                // Non-HLS single-file output — delete just the output file
                string outputFile = Path.Combine(basePath, container.FileName);
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                    Logger.Encoder($"Cleaned up partial encoding output: {outputFile}");
                }
            }
        }
        catch (Exception cleanupEx)
        {
            Logger.Encoder(
                $"Failed to clean up partial output at {basePath}: {cleanupEx.Message}",
                LogEventLevel.Warning
            );
        }
    }
}
