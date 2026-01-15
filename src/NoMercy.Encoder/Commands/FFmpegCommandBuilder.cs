using System.Text;
using NoMercy.Encoder.Core;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Image;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Subtitle;
using NoMercy.Encoder.Format.Video;
using NoMercy.NmSystem;

namespace NoMercy.Encoder.Commands;

public class FFmpegCommandBuilder
{
    private readonly BaseContainer _container;
    private readonly FfProbeData _FfProbeData;
    private readonly List<GpuAccelerator> _accelerators;
    private readonly bool _priority;

    public FFmpegCommandBuilder(BaseContainer container, FfProbeData ffProbeData, List<GpuAccelerator> accelerators, bool priority = false)
    {
        _container = container;
        _FfProbeData = ffProbeData;
        _accelerators = accelerators;
        _priority = priority;
        
        // Apply container-specific flags (adds -f, HLS options, etc.)
        _container.ApplyFlags();
    }

    public string BuildCommand()
    {
        StringBuilder command = new();

        // Build command sections in order
        AppendGlobalOptions(command);
        AppendInputOptions(command);
        AppendComplexFilters(command);
        AppendVideoOutputs(command);
        AppendAudioOutputs(command);
        AppendSubtitleOutputs(command);
        AppendImageOutputs(command);

        return command.ToString();
    }

    private void AppendGlobalOptions(StringBuilder command)
    {
        command.Append(" -hide_banner ");

        if (_container.IsVideo)
        {
            command.Append(" -probesize 4092M -analyzeduration 9999M");

            int threadCount = Environment.ProcessorCount;
            if (_priority)
                command.Append($" -threads {Math.Floor(threadCount * 2.0)} ");
            else
                command.Append(" -threads 0 ");

            foreach (GpuAccelerator accelerator in _accelerators) 
                command.Append(" " + accelerator.FfmpegArgs + " ");
        }

        command.Append(" -progress - ");
    }

    private void AppendInputOptions(StringBuilder command)
    {
        command.Append($" -y -i \"{_container.InputFile}\" ");

        if (_container.IsVideo && _accelerators.Count > 0)
            command.Append(" -gpu any ");

        command.Append(" -map_metadata -1 ");
    }

    private void AppendComplexFilters(StringBuilder command)
    {
        StringBuilder complexString = BuildComplexFilterString();

        if (complexString.Length > 0)
        {
            command.Append(" -filter_complex \"");
            command.Append(complexString.Replace(";;", ";") + "\"");
        }
    }

    private void ApplyScaleOverrides(BaseVideo stream)
    {
        // Scale Override #1: Set height if missing (with proper double division fix)
        if (stream.Scale.H == 0)
        {
            double aspectRatio = (double)_FfProbeData.PrimaryVideoStream!.Height / _FfProbeData.PrimaryVideoStream.Width;
            stream.Scale = new()
            {
                W = stream.VideoStream?.Width ?? _FfProbeData.PrimaryVideoStream!.Width,
                H = (int)(stream.Scale.W * aspectRatio)  // FIXED: proper double division
            };
        }

        // Scale Override #2: Prevent upscaling
        if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
            stream.Scale = new()
            {
                W = stream.VideoStream.Width,
                H = stream.VideoStream.Height
            };
    }

    private StringBuilder BuildComplexFilterString()
    {
        StringBuilder complexString = new();
        bool isHdr = false;

        // Video filters
        foreach (BaseVideo stream in _container.VideoStreams)
        {
            // Apply scale overrides (simplified approach)
            ApplyScaleOverrides(stream);

            if (ShouldSkipHdrProfile(stream)) continue;

            int index = _container.VideoStreams.IndexOf(stream);
            string videoFilter = BuildVideoFilter(stream, index, ref isHdr);
            complexString.Append(videoFilter);

            if (index != _container.VideoStreams.Count - 1 && complexString.Length > 0) 
                complexString.Append(';');
        }

        // Audio filters
        if (_container.AudioStreams.Count > 0 && complexString.Length > 0) 
            complexString.Append(';');

        foreach (BaseAudio stream in _container.AudioStreams)
        {
            int index = _container.AudioStreams.IndexOf(stream);
            complexString.Append($"[a:{stream.Index}]volume=3[a{index}_hls_0]");

            if (index != _container.AudioStreams.Count - 1 && complexString.Length > 0) 
                complexString.Append(';');
        }

        // Image filters
        if (_container.ImageStreams.Count > 0 && complexString.Length > 0) 
            complexString.Append(';');

        foreach (BaseImage stream in _container.ImageStreams)
        {
            int index = _container.ImageStreams.IndexOf(stream);
            string imageFilter = BuildImageFilter(stream, index, isHdr);
            complexString.Append(imageFilter);

            if (index != _container.ImageStreams.Count - 1 && complexString.Length > 0) 
                complexString.Append(';');
        }

        return complexString;
    }

    private string BuildVideoFilter(BaseVideo stream, int index, ref bool isHdr)
    {
        if (stream is { ConvertToSdr: true, IsHdr: true })
        {
            isHdr = stream.IsHdr;
            return $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv,zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format={stream.PixelFormat}[v{index}_hls_0]";
        }
       
        return $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},format={stream.PixelFormat}[v{index}_hls_0]";
    }

    private string BuildImageFilter(BaseImage stream, int index, bool isHdr)
    {
        if (isHdr)
        {
            return
                $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=smpte2084:m=bt2020nc:p=bt2020:r=tv,zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,fps=1/{stream.FrameRate}[i{index}_hls_0]";
        }

        return $"[v:0]crop={stream.CropValue},scale={stream.ScaleValue},fps=1/{stream.FrameRate}[i{index}_hls_0]";
    }

    private void AppendVideoOutputs(StringBuilder command)
    {
        foreach (BaseVideo stream in _container.VideoStreams)
        {
            // Apply final scale adjustments
            ApplyFinalScaleAdjustments(stream);

            if (ShouldSkipHdrProfile(stream)) continue;

            Dictionary<string, dynamic> commandDictionary = new();
            int index = _container.VideoStreams.IndexOf(stream);

            stream.AddToDictionary(commandDictionary, index);
            AddContainerParameters(commandDictionary);
            AddHlsParameters(commandDictionary, stream.HlsPlaylistFilename, true);

            command.Append(BuildParameterString(commandDictionary));
            stream.CreateFolder();
        }
    }

    /// <summary>
    /// Apply final scale adjustments (the remaining scale override points)
    /// </summary>
    private void ApplyFinalScaleAdjustments(BaseVideo stream)
    {
        // Scale Override #3: Another upscaling check
        if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
        {
            stream.Scale.W = stream.VideoStream.Width;
            stream.Scale.H = stream.VideoStream.Height;
        }

        // Scale Override #4: Downscaling threshold check
        if (stream.Scale.W < stream.VideoStream.Width * 0.95 && stream.Scale.H < stream.VideoStream.Height * 0.95)
        {
            stream.Scale.W = stream.VideoStream.Width;
            stream.Scale.H = stream.VideoStream.Height;
        }
    }

    private void AppendAudioOutputs(StringBuilder command)
    {
        foreach (BaseAudio stream in _container.AudioStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();
            int index = _container.AudioStreams.IndexOf(stream);
            
            stream.AddToDictionary(commandDictionary, index);
            AddContainerParameters(commandDictionary);
            // Don't add container parameters to audio streams - they're not needed
            AddHlsParameters(commandDictionary, stream.HlsPlaylistFilename, true);

            // Build the base stream parameters (map, codec, HLS params, etc.)
            command.Append(BuildParameterString(commandDictionary));

            // Add metadata AFTER the stream parameters
            if (_container.IsAudio)
            {
                AddAudioMetadata(command, stream);
            }
            else
            {
                AddStreamMetadata(command, stream, index);
            }

            stream.CreateFolder();
        }
    }

    private void AppendSubtitleOutputs(StringBuilder command)
    {
        foreach (BaseSubtitle stream in _container.SubtitleStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();
            stream.AddToDictionary(commandDictionary, stream.Index);

            commandDictionary[""] = $"\"./{stream.HlsPlaylistFilename}.{stream.Extension}\"";

            command.Append(BuildParameterString(commandDictionary));
            stream.CreateFolder();
        }
    }

    private void AppendImageOutputs(StringBuilder command)
    {
        foreach (BaseImage stream in _container.ImageStreams)
        {
            Dictionary<string, dynamic> commandDictionary = new();
            int index = _container.ImageStreams.IndexOf(stream);

            stream.AddToDictionary(commandDictionary, index);

            if (_container.ContainerDto.Name == VideoContainers.Hls)
                commandDictionary[""] = $"\"./{stream.Filename}/{stream.Filename}-%04d.jpg\"";

            command.Append(BuildParameterString(commandDictionary));
            stream.CreateFolder();
        }
    }

    #region Helper Methods

    private bool ShouldSkipHdrProfile(BaseVideo stream)
    {
        // Skip profiles that request 10-bit pixel formats (HDR) when the source is not HDR.
        // This prevents generating HDR outputs from SDR sources. Detection is based on
        // pixel-format naming (contains "10") which matches our pixel format constants.
        if (string.IsNullOrEmpty(stream.PixelFormat))
            return false;

        bool profileRequests10Bit = stream.PixelFormat.Contains("10");

        // If the profile requests 10-bit (HDR) but the input stream is not HDR, skip it.
        if (profileRequests10Bit && !stream.IsHdr)
            return true;

        return false;
    }

    private void AddContainerParameters(Dictionary<string, dynamic> commandDictionary)
    {
        foreach (KeyValuePair<string, dynamic> parameter in _container._extraParameters)
            commandDictionary[parameter.Key] = parameter.Value;
    }

    private void AddHlsParameters(Dictionary<string, dynamic> commandDictionary, string playlistFilename, bool isVideo)
    {
        if (_container.ContainerDto.Name == VideoContainers.Hls)
        {
            commandDictionary["-hls_segment_filename"] = $"\"./{playlistFilename}_%05d.ts\"";
            commandDictionary[""] = $"\"./{playlistFilename}.m3u8\"";
        }
        else if (!isVideo)
        {
            commandDictionary[""] = $"\"./{playlistFilename}.{_container.Extension}\"";
        }
    }

    private void AddAudioMetadata(StringBuilder command, BaseAudio stream)
    {
        command.Append(" -map 0:v:0? ");

        if (stream._id3Tags.Count > 0)
        {
            command.Append(" -id3v2_version 3 -write_id3v1 1 ");
            foreach (string extraTag in stream._id3Tags)
                command.Append($" -metadata {extraTag} ");

            command.Append(" -metadata:s:v title=\"Album cover\"");
            command.Append(" -metadata:s:v comment=\"Cover (front)\"");
        }
    }

    private void AddStreamMetadata(StringBuilder command, BaseAudio stream, int index)
    {
        if (!IsoLanguageMapper.IsoToLanguage.TryGetValue(stream.Language, out string? language))
            throw new($"Language {stream.Language} is not supported");

        command.Append($" -metadata:s:a:{index} title=\"{language} {stream.AudioChannels}-{stream.AudioCodec.SimpleValue}\" ");
        command.Append($" -metadata:s:a:{index} language=\"{stream.Language}\" ");
    }

    private string BuildParameterString(Dictionary<string, dynamic> parameters)
    {
        return parameters.Aggregate("", (acc, pair) => $"{acc} {pair.Key} {pair.Value}");
    }

    #endregion
}
