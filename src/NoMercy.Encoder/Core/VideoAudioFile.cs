using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Image;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Subtitle;
using NoMercy.Encoder.Format.Video;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Core;

public partial class VideoAudioFile(FfProbeData ffProbeData, string ffmpegPath) : Classes
{
    public string FfmpegPath => ffmpegPath;

    private bool Priority { get; set; }

    public VideoAudioFile AddContainer(BaseContainer container)
    {
        string cropValue = ffProbeData.PrimaryVideoStream is { Width: not 0, Height: not 0 }
            ? CropDetect(ffProbeData.FilePath)
            : string.Empty;

        Container = container;
        Container.Title = Title;
        Container.InputFile = ffProbeData.FilePath;
        Container.BasePath = BasePath;
        Container.FileName = FileName;
        Container.IsImage = IsImage;
        Container.IsAudio = IsAudio;
        Container.IsVideo = IsVideo;
        Container.IsSubtitle = IsSubtitle;
        Container.FfProbeData = ffProbeData;
        Container.ApplyFlags();

        foreach (KeyValuePair<int, dynamic> keyValuePair in Container.Streams)
        {
            keyValuePair.Value.BasePath = Container.BasePath;
            keyValuePair.Value.FileName = Container.FileName;

            if (keyValuePair.Value.IsVideo)
            {
                (keyValuePair.Value as BaseVideo)!.CropValue = cropValue;

                (keyValuePair.Value as BaseVideo)!.VideoStreams = ffProbeData.VideoStreams;
                (keyValuePair.Value as BaseVideo)!.VideoStream = ffProbeData.PrimaryVideoStream;
                (keyValuePair.Value as BaseVideo)!.Index = ffProbeData.PrimaryVideoStream!.Index;
                (keyValuePair.Value as BaseVideo)!.Title = Title;

                Container.VideoStreams.Add((keyValuePair.Value as BaseVideo)!.Build());
            }
            else if (keyValuePair.Value.IsAudio)
            {
                (keyValuePair.Value as BaseAudio)!.AudioStreams = ffProbeData.AudioStreams;
                (keyValuePair.Value as BaseAudio)!.AudioStream = ffProbeData.PrimaryAudioStream!;

                List<BaseAudio> x = (keyValuePair.Value as BaseAudio)!.Build();
                foreach (BaseAudio newStream in x)
                    newStream.Extension = Container.Extension;

                Container.AudioStreams.AddRange(x);
            }
            else if (keyValuePair.Value.IsSubtitle)
            {
                (keyValuePair.Value as BaseSubtitle)!.SubtitleStreams = ffProbeData.SubtitleStreams;
                (keyValuePair.Value as BaseSubtitle)!.SubtitleStream = ffProbeData.PrimarySubtitleStream!;

                List<BaseSubtitle> x = (keyValuePair.Value as BaseSubtitle)!.Build();
                foreach (BaseSubtitle newStream in x)
                    newStream.Extension = BaseSubtitle.GetExtension(newStream);

                Container.SubtitleStreams.AddRange(x);
            }
            else if (keyValuePair.Value.IsImage)
            {
                (keyValuePair.Value as BaseImage)!.CropValue = cropValue;

                (keyValuePair.Value as BaseImage)!.ImageStreams = ffProbeData.ImageStreams;
                (keyValuePair.Value as BaseImage)!.ImageStream = ffProbeData.PrimaryImageStream!;

                Container.ImageStreams.Add((keyValuePair.Value as BaseImage)!.Build());
            }

            (keyValuePair.Value as Classes)!.ApplyFlags();
        }

        return this;
    }

    public VideoAudioFile SetBasePath(string basePath)
    {
        BasePath = basePath;
        return this;
    }

    public VideoAudioFile SetTitle(string title)
    {
        Title = title;
        return this;
    }

    public VideoAudioFile ToFile(string filename)
    {
        FileName = filename;
        return this;
    }

    private string ChooseCrop(ConcurrentDictionary<string, int> crops)
    {
        string maxKey = "";
        int maxValue = 0;

        foreach (KeyValuePair<string, int> crop in crops)
            if (crop.Value > maxValue)
            {
                maxValue = crop.Value;
                maxKey = crop.Key;
            }

        return maxKey;
    }

    private string CropDetect(string path)
    {
        Logger.Encoder($"Detecting crop for {path}");

        const int sections = 10;

        double duration = ffProbeData.Duration.TotalSeconds;
        double max = Math.Floor(duration / 2);
        double step = Math.Floor(max / sections);

        ConcurrentDictionary<string, int> counts = new();
        Regex regex = CropDetectRegex();

        List<string> results = [];

        Parallel.For(0, sections, Config.ParallelOptions, (i, _) =>
        {
            string cropSection =
                $"-threads 1 -nostats -hide_banner -ss {i * step} -i \"{path}\" -vframes 10 -vf cropdetect -t {1} -f null -";

            string result = Shell.ExecStdErrSync(FfmpegPath, cropSection);
            results.Add(result);
        });

        Parallel.ForEach(results, Config.ParallelOptions, (output) =>
        {
            MatchCollection matches = regex.Matches(output);

            foreach (Match match in matches)
            {
                string crop = match.Groups[1].Value;
                if (!counts.TryAdd(crop, 1)) counts[crop]++;
            }
        });

        return ChooseCrop(counts);
    }

    public void Build()
    {
    }

    public VideoAudioFile Prioritize()
    {
        Priority = true;

        return this;
    }

    public string GetFullCommand()
    {
        FFmpegCommandBuilder commandBuilder = new(
            container: Container,
            ffProbeData: ffProbeData,
            accelerators: Accelerators,
            priority: Priority
        );

        string command = commandBuilder.BuildCommand();
        command += " ";

        return command;
    }

    [GeneratedRegex(@"crop=(\d+:\d+:\d+:\d+)", RegexOptions.Multiline)]
    private static partial Regex CropDetectRegex();

    public Task Run(string fullCommand, string basePath, ProgressMeta progressMeta)
    {
        return FfMpeg.Run(fullCommand, basePath, progressMeta);
    }

    public async Task ConvertSubtitles(List<BaseSubtitle> subtitles, int id, string title, string? imgPath)
    {
        foreach (BaseSubtitle? subtitle in subtitles)
        {
            // Ensure the Tesseract language file exists before attempting OCR
            bool languageFileExists = await TesseractLanguageDownloader.EnsureLanguageFileExists(subtitle.Language);
            if (!languageFileExists)
            {
                Logger.Encoder($"Failed to obtain Tesseract language file for {subtitle.Language}. Skipping OCR for this subtitle.", LogEventLevel.Warning);
                continue;
            }

            string input = Path.Combine(BasePath, $"{subtitle.HlsPlaylistFilename}.{subtitle.Extension}");
            string orcFile = Path.Combine(BasePath, "subtitles", "temp.txt");
            string output = Path.Combine(BasePath, $"{subtitle.HlsPlaylistFilename}.vtt");

            string ocrCommand =
                $" -i \"{input}\" -f lavfi -i color=black:s=hd720 -filter_complex \"[0:s:0]ocr=language={subtitle.Language},metadata=print:key=lavfi.ocr.text:file=temp.txt\" -an -f null -";

            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(new EncoderProgressBroadcastEvent
                {
                    ProgressData = new Progress
                    {
                        Id = id,
                        Status = "running",
                        Title = title,
                        Thumbnail = $"/images/original{imgPath}",
                        Message = $"OCR {IsoLanguageMapper.IsoToLanguage[subtitle.Language]}"
                    }
                });

            Logger.Encoder($"Converting {IsoLanguageMapper.IsoToLanguage[subtitle.Language]} subtitle to WebVtt");
            Logger.Encoder(AppFiles.FfmpegPath + ocrCommand, LogEventLevel.Debug);

            Task<string> execTask = Shell.ExecStdErrAsync(AppFiles.FfmpegPath, ocrCommand, new()
            {
                WorkingDirectory = Path.Combine(BasePath, "subtitles"),
                EnvironmentVariables = new()
                {
                    ["TESSDATA_PREFIX"] = AppFiles.TesseractModelsFolder
                }
            });

            Task progressTask = Task.Run(async () =>
            {
                while (!execTask.IsCompleted)
                {
                    if (EventBusProvider.IsConfigured)
                        _ = EventBusProvider.Current.PublishAsync(new EncoderProgressBroadcastEvent
                        {
                            ProgressData = new Progress
                            {
                                Id = id,
                                Status = "running",
                                Title = title,
                                Thumbnail = $"/images/original{imgPath}",
                                Message = $"OCR {IsoLanguageMapper.IsoToLanguage[subtitle.Language]}"
                            }
                        });

                    await Task.Delay(1000);
                }
            });

            await Task.WhenAll(execTask, progressTask);

            Logger.Encoder($"Converting {IsoLanguageMapper.IsoToLanguage[subtitle.Language]} subtitle to WebVtt");

            if (!File.Exists(orcFile)) return;

            Subtitle[] parsedSubtitles = SubtitleParser.ParseSubtitles(orcFile);

            SubtitleParser.SaveToVtt(parsedSubtitles, output);

            File.Delete(orcFile);
        }

        if (EventBusProvider.IsConfigured)
            _ = EventBusProvider.Current.PublishAsync(new EncoderProgressBroadcastEvent
            {
                ProgressData = new Progress
                {
                    Id = id,
                    Status = "completed",
                    Title = title,
                    Thumbnail = $"/images/original{imgPath}",
                    Message = "Completed converting subtitles to WebVtt"
                }
            });
    }
    
    public static async Task GetSubtitleFromWhisperAi(string inputFile, string basePath, string fileName, string language)
    {
        string whisperCommand =
            $@" -i ""{inputFile}"" -vn -af ""whisper=model={AppFiles.WhisperModelPath}:language={language}:queue=3:destination={fileName}:format=srt"" -f null -";

        Logger.Encoder($"Generating {IsoLanguageMapper.IsoToLanguage[language]} subtitle with Whisper AI");
        
        Logger.Encoder(AppFiles.FfmpegPath + " " + whisperCommand, LogEventLevel.Debug);
        
        if (!Directory.Exists(Path.Combine(basePath, "subtitles")))
            Directory.CreateDirectory(Path.Combine(basePath, "subtitles"));
        
        
        Task<string> execTask = Shell.ExecStdErrAsync(AppFiles.FfmpegPath, whisperCommand, new()
        {
            WorkingDirectory = Path.Combine(basePath, "subtitles"),
            EnvironmentVariables = new()
            {
                ["TESSDATA_PREFIX"] = AppFiles.TesseractModelsFolder
            }
        });
        
        await execTask;
    }
}