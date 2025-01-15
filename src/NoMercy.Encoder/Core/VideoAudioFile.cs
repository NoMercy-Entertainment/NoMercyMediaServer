using System.Text;
using System.Text.RegularExpressions;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Image;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Subtitle;
using NoMercy.Encoder.Format.Video;
using NoMercy.NmSystem;
using Serilog.Events;
using Logger = NoMercy.NmSystem.Logger;

namespace NoMercy.Encoder.Core;

public partial class VideoAudioFile(MediaAnalysis fMediaAnalysis, string ffmpegPath) : Classes
{
    public string FfmpegPath => ffmpegPath;

    private bool Priority { get; set; } = false;

    public VideoAudioFile AddContainer(BaseContainer container)
    {
        string cropValue = CropDetect(fMediaAnalysis.Path);

        Container = container;
        Container.Title = Title;
        Container.InputFile = fMediaAnalysis.Path;
        Container.BasePath = BasePath;
        Container.FileName = FileName;
        Container.IsImage = IsImage;
        Container.IsAudio = IsAudio;
        Container.IsVideo = IsVideo;
        Container.IsSubtitle = IsSubtitle;
        Container.MediaAnalysis = fMediaAnalysis!;
        Container.ApplyFlags();

        foreach (KeyValuePair<int, dynamic> keyValuePair in Container.Streams)
        {
            keyValuePair.Value.BasePath = Container.BasePath;
            keyValuePair.Value.FileName = Container.FileName;

            if (keyValuePair.Value.IsVideo)
            {
                (keyValuePair.Value as BaseVideo)!.CropValue = cropValue;

                (keyValuePair.Value as BaseVideo)!.VideoStreams = [fMediaAnalysis!.PrimaryVideoStream!];
                (keyValuePair.Value as BaseVideo)!.VideoStream = fMediaAnalysis!.PrimaryVideoStream!;
                (keyValuePair.Value as BaseVideo)!.Index = fMediaAnalysis!.PrimaryVideoStream!.Index;
                (keyValuePair.Value as BaseVideo)!.Title = Title;

                Container.VideoStreams.Add((keyValuePair.Value as BaseVideo)!.Build());
            }
            else if (keyValuePair.Value.IsAudio)
            {
                (keyValuePair.Value as BaseAudio)!.AudioStreams = fMediaAnalysis!.AudioStreams!;
                (keyValuePair.Value as BaseAudio)!.AudioStream = fMediaAnalysis!.PrimaryAudioStream!;

                Container.AudioStreams.AddRange((keyValuePair.Value as BaseAudio)!.Build());
            }
            else if (keyValuePair.Value.IsSubtitle)
            {
                (keyValuePair.Value as BaseSubtitle)!.SubtitleStreams = fMediaAnalysis!.SubtitleStreams!;
                (keyValuePair.Value as BaseSubtitle)!.SubtitleStream = fMediaAnalysis!.PrimarySubtitleStream!;

                List<BaseSubtitle> x = (keyValuePair.Value as BaseSubtitle)!.Build();
                foreach (BaseSubtitle newStream in x)
                    newStream.Extension = BaseSubtitle.GetExtension(newStream);

                Container.SubtitleStreams.AddRange(x);
            }
            else if (keyValuePair.Value.IsImage)
            {
                (keyValuePair.Value as BaseImage)!.CropValue = cropValue;

                (keyValuePair.Value as BaseImage)!.ImageStreams = [fMediaAnalysis!.PrimaryVideoStream!];
                (keyValuePair.Value as BaseImage)!.ImageStream = fMediaAnalysis!.PrimaryVideoStream!;

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

    private string ChooseCrop(Dictionary<string, int> crops)
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

        double duration = fMediaAnalysis.Duration.TotalSeconds;
        double max = Math.Floor(duration / 2);
        double step = Math.Floor(max / sections);

        Dictionary<string, int> counts = new();
        Regex regex = CropDetectRegex();

        List<string> results = [];

        for (int i = 0; i < sections; i++)
        {
            string execString =
                $"-threads 1 -nostats -hide_banner -ss {i * step} -i \"{path}\" -vframes 10 -vf cropdetect -t {1} -f null -";

            string result = FfMpeg.Exec(execString, executable: FfmpegPath).Result;
            results.Add(result);
        }
        
        foreach (string output in results)
        {
            MatchCollection matches = regex.Matches(output);

            foreach (Match match in matches)
            {
                string crop = match.Groups[1].Value;
                if (!counts.TryAdd(crop, 1)) counts[crop]++;
            }
        }

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
        int threadCount = Environment.ProcessorCount;

        StringBuilder command = new();

        command.Append(" -hide_banner -probesize 4092M -analyzeduration 9999M");
        if (Priority)
        {
            command.Append($" -threads {Math.Floor(threadCount * 2.0)} ");
        }
        else
        {
            command.Append($" -threads {Math.Floor(threadCount * 0.8)} ");
        }

        if (HasGpu) command.Append(" -extra_hw_frames 3 -init_hw_device opencl=ocl ");

        command.Append(" -progress - ");

        command.Append($" -y -i \"{Container.InputFile}\" ");

        // command.Append(" -max_muxing_queue_size 9999 ");

        bool isHdr = false;

        StringBuilder complexString = new();
        foreach (BaseVideo stream in Container.VideoStreams)
        {
            int index = Container!.VideoStreams.IndexOf(stream);

            // if source is smaller than requested size, don't upscale
            // if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height) continue;
            if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
            {
                stream.Scale.W = stream.VideoStream.Width;
                stream.Scale.H = stream.VideoStream.Height;
            }

            // if source is not HDR then don't make the HDR profile
            if (!stream.IsHdr
                && (stream.PixelFormat == VideoPixelFormats.Yuv444p
                    || stream.PixelFormat == VideoPixelFormats.Yuv444p10le)
            ) continue;

            if (stream.ConverToSdr && stream.IsHdr)
            {
                isHdr = stream.IsHdr;
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv,zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format={stream.PixelFormat}[v{index}_hls_0]");

                if (index != Container.VideoStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
            }
            else
            {
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},format={stream.PixelFormat}[v{index}_hls_0]");

                if (index != Container.VideoStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
            }
        }

        if (Container.AudioStreams.Count > 0 && complexString.Length > 0) complexString.Append(';');
        foreach (BaseAudio stream in Container.AudioStreams)
        {
            int index = Container!.AudioStreams.IndexOf(stream);

            complexString.Append($"[a:{stream.Index}]volume=3,loudnorm[a{index}_hls_0]");

            if (index != Container.AudioStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
        }

        // if (Container.SubtitleStreams.Count > 0 && complexString.Length > 0) complexString.Append(';');
        // foreach (BaseSubtitle stream in Container.SubtitleStreams)
        // {
        //     int index = Container!.SubtitleStreams.IndexOf(stream);
        //
        //     complexString.Append($"[s:{stream.Index}]overlay[s{index}_hls_0]");
        //
        //     if (index != Container.SubtitleStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
        // }

        if (Container.ImageStreams.Count > 0 && complexString.Length > 0) complexString.Append(';');
        foreach (BaseImage stream in Container.ImageStreams)
        {
            int index = Container!.ImageStreams.IndexOf(stream);

            if (isHdr)
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv,zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,fps=1/{stream.FrameRate}[i{index}_hls_0]");
            else
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},fps=1/{stream.FrameRate}[i{index}_hls_0]");

            if (index != Container.ImageStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
        }

        if (complexString.Length > 0)
        {
            command.Append(" -filter_complex \"");
            command.Append(complexString.Replace(";;", ";") + "\"");
        }

        foreach (BaseVideo stream in Container.VideoStreams)
        {
            // if source is smaller than requested size, don't upscale
            // if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height) continue;
            if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
            {
                stream.Scale.W = stream.VideoStream.Width;
                stream.Scale.H = stream.VideoStream.Height;
            }

            // if source is not HDR then don't make the HDR profile
            if (!stream.IsHdr
                && (stream.PixelFormat == VideoPixelFormats.Yuv444p
                    || stream.PixelFormat == VideoPixelFormats.Yuv444p10le)
               ) continue;

            Dictionary<string, dynamic> commandDictionary = new();

            int index = Container!.VideoStreams.IndexOf(stream);

            stream.AddToDictionary(commandDictionary, index);

            foreach (KeyValuePair<string, dynamic> parameter in Container?._extraParameters ??
                                                                new Dictionary<string, dynamic>())
                commandDictionary[parameter.Key] = parameter.Value;

            // commandDictionary["-t"] = 300;

            if (Container!.ContainerDto.Name == VideoContainers.Hls)
            {
                commandDictionary["-hls_segment_filename"] = $"\"./{stream.HlsPlaylistFilename}_%05d.ts\"";
                commandDictionary[""] = $"\"./{stream.HlsPlaylistFilename}.m3u8\"";
            }

            command.Append(commandDictionary.Aggregate("", (acc, pair) => $"{acc} {pair.Key} {pair.Value}"));

            stream.CreateFolder();
        }

        foreach (BaseAudio stream in Container.AudioStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();
            int index = Container!.AudioStreams.IndexOf(stream);
            stream.AddToDictionary(commandDictionary, index);

            foreach (KeyValuePair<string, dynamic> parameter in Container._extraParameters ??
                                                                new Dictionary<string, dynamic>())
                commandDictionary[parameter.Key] = parameter.Value;

            // commandDictionary["-t"] = 300;
            if (Container.ContainerDto.Name == VideoContainers.Hls)
            {
                commandDictionary["-hls_segment_filename"] = $"\"./{stream.HlsPlaylistFilename}_%05d.ts\"";
                commandDictionary[""] = $"\"./{stream.HlsPlaylistFilename}.m3u8\"";
            }

            command.Append(commandDictionary.Aggregate("", (acc, pair) => $"{acc} {pair.Key} {pair.Value}"));

            stream.CreateFolder();
        }

        foreach (BaseSubtitle stream in Container.SubtitleStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();
            stream.AddToDictionary(commandDictionary, stream.Index);

            // foreach (KeyValuePair<string, dynamic> parameter in Container._extraParameters ?? new Dictionary<string, dynamic>())
            // {
            //     commandDictionary[parameter.Key] = parameter.Value;
            // }

            // commandDictionary["-t"] = 300;
            // if (Container.ContainerDto.Name == VideoContainers.Hls)
            // {
            //     commandDictionary["-hls_segment_filename"] = $"\"./{stream.HlsPlaylistFilename}_%05d.vtt\"";
            //     commandDictionary[""] = $"\"./{stream.HlsPlaylistFilename}.m3u8\"";
            // }

            commandDictionary[""] = $"\"./{stream.HlsPlaylistFilename}.{stream.Extension}\"";

            command.Append(commandDictionary.Aggregate("", (acc, pair) => $"{acc} {pair.Key} {pair.Value}"));
            
            if (stream.ConvertSubtitle)
            {
                ConvertSubtitle = true;
            }

            stream.CreateFolder();
        }

        foreach (BaseImage stream in Container.ImageStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();

            int index = Container!.ImageStreams.IndexOf(stream);

            stream.AddToDictionary(commandDictionary, index);

            // commandDictionary["-t"] = 300;

            if (Container!.ContainerDto.Name == VideoContainers.Hls)
                commandDictionary[""] = $"\"./{stream.Filename}/{stream.Filename}-%04d.jpg\"";

            command.Append(commandDictionary.Aggregate("", (acc, pair) => $"{acc} {pair.Key} {pair.Value}"));

            stream.CreateFolder();
        }

        command.Append(" ");
        // command.Append(" 2>&1 ");
        return command.ToString();
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
            string input = Path.Combine(BasePath, $"{subtitle.HlsPlaylistFilename}.{subtitle.Extension}");
            string arg = $" /convert \"{input}\" WebVtt";

            Networking.Networking.SendToAll("encoder-progress", "dashboardHub",  new Progress
            {
                Id = id,
                Status = "running",
                Title = title,
                Thumbnail = $"/images/original{imgPath}",
                Message = $"Converting {IsoLanguageMapper.IsoToLanguage[subtitle.Language]} subtitle to WebVtt",
            });

            Logger.Encoder($"Converting {IsoLanguageMapper.IsoToLanguage[subtitle.Language]} subtitle to WebVtt");
            Logger.Encoder(AppFiles.SubtitleEdit + arg, LogEventLevel.Debug);

            Task<string> execTask = Shell.Exec(AppFiles.SubtitleEdit, arg);

            Task progressTask = Task.Run(async () =>
            {
                while (!execTask.IsCompleted)
                {
                    Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
                    {
                        Id = id,
                        Status = "running",
                        Title = title,
                        Thumbnail = $"/images/original{imgPath}",
                        Message = $"Converting {IsoLanguageMapper.IsoToLanguage[subtitle.Language]} subtitle to WebVtt",
                    });

                    await Task.Delay(1000);
                }
            });

            await Task.WhenAll(execTask, progressTask);

        };

        Networking.Networking.SendToAll("encoder-progress", "dashboardHub", new Progress
        {
            Id = id,
            Status = "completed",
            Title = title,
            Thumbnail = $"/images/original{imgPath}",
            Message = $"Completed converting subtitles to WebVtt",
        });
    }
}