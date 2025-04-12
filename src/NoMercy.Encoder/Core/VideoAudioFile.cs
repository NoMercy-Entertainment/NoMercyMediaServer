using System.Text;
using System.Text.RegularExpressions;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Image;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Subtitle;
using NoMercy.Encoder.Format.Video;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder.Core;

public partial class VideoAudioFile(MediaAnalysis fMediaAnalysis, string ffmpegPath) : Classes
{
    public string FfmpegPath => ffmpegPath;

    private bool Priority { get; set; }

    public VideoAudioFile AddContainer(BaseContainer container)
    {
        string cropValue = container.IsVideo ? CropDetect(fMediaAnalysis.Path) : string.Empty;

        Container = container;
        Container.Title = Title;
        Container.InputFile = fMediaAnalysis.Path;
        Container.BasePath = BasePath;
        Container.FileName = FileName;
        Container.IsImage = IsImage;
        Container.IsAudio = IsAudio;
        Container.IsVideo = IsVideo;
        Container.IsSubtitle = IsSubtitle;
        Container.MediaAnalysis = fMediaAnalysis;
        Container.ApplyFlags();

        foreach (KeyValuePair<int, dynamic> keyValuePair in Container.Streams)
        {
            keyValuePair.Value.BasePath = Container.BasePath;
            keyValuePair.Value.FileName = Container.FileName;

            if (keyValuePair.Value.IsVideo)
            {
                (keyValuePair.Value as BaseVideo)!.CropValue = cropValue;

                (keyValuePair.Value as BaseVideo)!.VideoStreams = [fMediaAnalysis.PrimaryVideoStream!];
                (keyValuePair.Value as BaseVideo)!.VideoStream = fMediaAnalysis.PrimaryVideoStream!;
                (keyValuePair.Value as BaseVideo)!.Index = fMediaAnalysis.PrimaryVideoStream!.Index;
                (keyValuePair.Value as BaseVideo)!.Title = Title;

                Container.VideoStreams.Add((keyValuePair.Value as BaseVideo)!.Build());
            }
            else if (keyValuePair.Value.IsAudio)
            {
                (keyValuePair.Value as BaseAudio)!.AudioStreams = fMediaAnalysis.AudioStreams;
                (keyValuePair.Value as BaseAudio)!.AudioStream = fMediaAnalysis.PrimaryAudioStream!;

                List<BaseAudio> x = (keyValuePair.Value as BaseAudio)!.Build();
                foreach (BaseAudio newStream in x)
                    newStream.Extension = Container.Extension;
                
                Container.AudioStreams.AddRange(x);
            }
            else if (keyValuePair.Value.IsSubtitle)
            {
                (keyValuePair.Value as BaseSubtitle)!.SubtitleStreams = fMediaAnalysis.SubtitleStreams;
                (keyValuePair.Value as BaseSubtitle)!.SubtitleStream = fMediaAnalysis.PrimarySubtitleStream!;

                List<BaseSubtitle> x = (keyValuePair.Value as BaseSubtitle)!.Build();
                foreach (BaseSubtitle newStream in x)
                    newStream.Extension = BaseSubtitle.GetExtension(newStream);

                Container.SubtitleStreams.AddRange(x);
            }
            else if (keyValuePair.Value.IsImage)
            {
                (keyValuePair.Value as BaseImage)!.CropValue = cropValue;

                (keyValuePair.Value as BaseImage)!.ImageStreams = [fMediaAnalysis.PrimaryVideoStream!];
                (keyValuePair.Value as BaseImage)!.ImageStream = fMediaAnalysis.PrimaryVideoStream!;

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
            string cropSection = $"-threads 1 -nostats -hide_banner -ss {i * step} -i \"{path}\" -vframes 10 -vf cropdetect -t {1} -f null -";
            
            string result = Shell.ExecStdErrSync(FfmpegPath, cropSection);
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

        command.Append(" -hide_banner ");

        if (Container.IsVideo)
        {
            command.Append(" -probesize 4092M -analyzeduration 9999M");
            
            if (Priority)
            {
                command.Append($" -threads {Math.Floor(threadCount * 2.0)} ");
            }
            else
            {
                command.Append($" -threads {Math.Floor(threadCount * 0.5)} ");
            }

            foreach (GpuAccelerator accelerator in Accelerators)
            {
                command.Append($" {accelerator.FfmpegArgs} ");
            }
        }

        command.Append(" -progress - ");

        command.Append($" -y -i \"{Container.InputFile}\" ");
        
        if (Container.IsVideo && Accelerators.Count > 0)
        {
            command.Append(" -gpu any ");
        }

        // If we need to OCR the subs we need to make it possible by adding a background stream
        // Doing it this way is super slow so we do it manually.
        // if(Container.SubtitleStreams.Any(stream => stream.Extension is "sup" or "vob"))
        // {
        //     command.Append(" -f lavfi -i color=black:s=hd720 ");
        // }

        bool isHdr = false;

        StringBuilder complexString = new();
        foreach (BaseVideo stream in Container.VideoStreams)
        {
            int index = Container.VideoStreams.IndexOf(stream);

            // if source is smaller than requested size, don't upscale
            if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
            {
                stream.Scale = new()
                {
                    W = stream.VideoStream.Width,
                    H = stream.VideoStream.Height
                };
            }

            // if source is not HDR then don't make the HDR profile
            if (!stream.IsHdr
                && (stream.PixelFormat == VideoPixelFormats.Yuv444P
                    || stream.PixelFormat == VideoPixelFormats.Yuv444P10Le)
            ) continue;

            if (stream.ConvertToSdr && stream.IsHdr)
            {
                isHdr = stream.IsHdr;
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv,zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format={stream.PixelFormat}[v{index}_hls_0]");
            }
            else
            {
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},format={stream.PixelFormat}[v{index}_hls_0]");

            } 
            if (index != Container.VideoStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
        }

        if (Container.AudioStreams.Count > 0 && complexString.Length > 0) complexString.Append(';');
        
        foreach (BaseAudio stream in Container.AudioStreams)
        {
            int index = Container.AudioStreams.IndexOf(stream);

            complexString.Append($"[a:{stream.Index}]volume=3,loudnorm[a{index}_hls_0]");

            if (index != Container.AudioStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
        }

        // if (Container.SubtitleStreams.Count > 0 && complexString.Length > 0) complexString.Append(';');
        // foreach (BaseSubtitle stream in Container.SubtitleStreams)
        // {
            // If we need to OCR
            // Doing it this way is super slow.
            // if (stream.Extension != "sup" && stream.Extension != "vob") continue;
            // if (!stream.ConvertSubtitle) continue;
            //
            // int index = Container.SubtitleStreams.IndexOf(stream);
            //
            // complexString.Append($"[s:{stream.Index}]ocr=language={stream.Language},metadata=print:key=lavfi.ocr.text:file='subtitles/{stream.HlsPlaylistFilename}.{stream.Extension}'");
            //
            // if (index != Container.SubtitleStreams.Count - 1 && complexString.Length > 0) complexString.Append(';');
        // }

        if (Container.ImageStreams.Count > 0 && complexString.Length > 0) complexString.Append(';');
        
        foreach (BaseImage stream in Container.ImageStreams)
        {
            int index = Container.ImageStreams.IndexOf(stream);

            if (isHdr)
            {
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv,zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,fps=1/{stream.FrameRate}[i{index}_hls_0]");
            }
            else
            {
                complexString.Append(
                    $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},fps=1/{stream.FrameRate}[i{index}_hls_0]");
            }

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
                && (stream.PixelFormat == VideoPixelFormats.Yuv444P
                    || stream.PixelFormat == VideoPixelFormats.Yuv444P10Le)
               ) continue;

            Dictionary<string, dynamic> commandDictionary = new();

            int index = Container.VideoStreams.IndexOf(stream);

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

        foreach (BaseAudio stream in Container.AudioStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();
            int index = Container.AudioStreams.IndexOf(stream);
            stream.AddToDictionary(commandDictionary, index);

            foreach (KeyValuePair<string, dynamic> parameter in Container._extraParameters ??
                                                                new Dictionary<string, dynamic>())
                commandDictionary[parameter.Key] = parameter.Value;

            if (Container.IsAudio)
            {
                command.Append(" -map 0:v:0 ");
                
                if (stream._id3Tags.Count > 0)
                {
                    command.Append(" -id3v2_version 3 -write_id3v1 1 ");
                    foreach (string extraTag in stream._id3Tags)
                        command.Append($" -metadata {extraTag} ");

                    command.Append(" -metadata:s:v title=\"Album cover\"");
                    command.Append(" -metadata:s:v comment=\"Cover (front)\""); // Lowercase f required
                }
            }
            else
            {
                if (!IsoLanguageMapper.IsoToLanguage.TryGetValue(stream.Language, out string? language))
                {
                    throw new($"Language {stream.Language} is not supported");
                }
                
                command.Append($" -metadata:s:a:{index} title=\"{language} {stream.AudioChannels}-{stream.AudioCodec.SimpleValue}\" ");
                command.Append($" -metadata:s:a:{index} language=\"{stream.Language}\" ");
            }

            // commandDictionary["-t"] = 300;
            if (Container.ContainerDto.Name == VideoContainers.Hls)
            {
                commandDictionary["-hls_segment_filename"] = $"\"./{stream.HlsPlaylistFilename}_%05d.ts\"";
                commandDictionary[""] = $"\"./{stream.HlsPlaylistFilename}.m3u8\"";
            }
            else
            {
                commandDictionary[""] = $"\"./{stream.HlsPlaylistFilename}.{stream.Extension}\"";
            }

            command.Append(commandDictionary.Aggregate("", (acc, pair) => $"{acc} {pair.Key} {pair.Value}"));
            
            stream.CreateFolder();
        }

        foreach (BaseSubtitle stream in Container.SubtitleStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();
            stream.AddToDictionary(commandDictionary, stream.Index);

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

            int index = Container.ImageStreams.IndexOf(stream);

            stream.AddToDictionary(commandDictionary, index);

            if (Container.ContainerDto.Name == VideoContainers.Hls)
                commandDictionary[""] = $"\"./{stream.Filename}/{stream.Filename}-%04d.jpg\"";

            command.Append(commandDictionary.Aggregate("", (acc, pair) => $"{acc} {pair.Key} {pair.Value}"));

            stream.CreateFolder();
        }

        command.Append(" ");
        
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
        foreach (BaseSubtitle? subtitle in subtitles.DistinctBy(x => x.HlsPlaylistFilename))
        {
            string input = Path.Combine(BasePath, $"{subtitle.HlsPlaylistFilename}.{subtitle.Extension}");
            string orcFile = Path.Combine(BasePath, "subtitles", "temp.txt");
            string output = Path.Combine(BasePath, $"{subtitle.HlsPlaylistFilename}.vtt");

            string ocrCommand = $" -i \"{input}\" -f lavfi -i color=black:s=hd720 -filter_complex [0:s:0]ocr=language={subtitle.Language},metadata=print:key=lavfi.ocr.text:file=\"temp.txt\" -an -f null -";

            Networking.Networking.SendToAll("encoder-progress", "dashboardHub",  new Progress
            {
                Id = id,
                Status = "running",
                Title = title,
                Thumbnail = $"/images/original{imgPath}",
                Message = $"OCR {IsoLanguageMapper.IsoToLanguage[subtitle.Language]}",
            });

            Logger.Encoder($"Converting {IsoLanguageMapper.IsoToLanguage[subtitle.Language]} subtitle to WebVtt");
            Logger.Encoder(AppFiles.FfmpegPath + ocrCommand, LogEventLevel.Debug);

            Task<string> execTask = Shell.ExecStdErrAsync(AppFiles.FfmpegPath, ocrCommand, new()
            {
                WorkingDirectory = Path.Combine(BasePath, "subtitles")
            });

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
                        Message = $"OCR {IsoLanguageMapper.IsoToLanguage[subtitle.Language]}",
                    });

                    await Task.Delay(1000);
                }
            });

            await Task.WhenAll(execTask, progressTask);
            
            Logger.Encoder($"Converting {IsoLanguageMapper.IsoToLanguage[subtitle.Language]} subtitle to WebVtt");
            
            Subtitle[] parsedSubtitles = SubtitleParser.ParseSubtitles(orcFile);
            
            SubtitleParser.SaveToVtt(parsedSubtitles, output);
            
            File.Delete(orcFile);
        }

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