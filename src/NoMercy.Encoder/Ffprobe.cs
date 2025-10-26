// ReSharper disable MemberCanBePrivate.Global

using System.Diagnostics;
using NoMercy.Encoder.Dto;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder;

public class Ffprobe
{
    private readonly string _filename;
    private FfprobeSourceData SourceData { get; set; } = new();
    private string Error { get; set; } = string.Empty;
    
    public List<VideoStream> VideoStreams = [];
    public List<ImageStream> ImageStreams = [];
    public List<AudioStream> AudioStreams = [];
    public List<SubtitleStream> SubtitleStreams = [];
    public List<Chapter> Chapters = [];
    public List<Attachment> Attachments = [];
    public FfprobeSourceDataFormat Format { get; set; } = new();
    
    public Ffprobe(string filename)
    {
        _filename = filename;
    }
    
    public Task<Ffprobe> GetStreamData()
    {
        return Task.Run(async () =>
        {
            (FfprobeSourceData? data, string stdErr) = await GetJson();
            if (data == null)
            {
                Logger.Encoder($"Failed to get stream data for {_filename}", LogEventLevel.Error);
                Logger.Encoder(stdErr, LogEventLevel.Error);
                return this;
            }
            
            SourceData = data;
            Error = stdErr;

            VideoStreams.AddRange(data.Streams
                .Where(s => s.CodecType == CodecType.Video)
                .Select(s => new VideoStream(s)));
            AudioStreams.AddRange(data.Streams
                .Where(s => s.CodecType == CodecType.Audio)
                .Select(s => new AudioStream(s)));
            SubtitleStreams.AddRange(data.Streams
                .Where(s => s.CodecType == CodecType.Subtitle)
                .Select(s => new SubtitleStream(s)));
            
            ImageStreams.AddRange(data.Streams
                .Where(s => s.CodecType == CodecType.Image)
                .Select(s => new ImageStream(s)));
            
            Attachments.AddRange(data.Streams
                .Where(s => s.CodecType == CodecType.Attachment)
                .Select(s => new Attachment(s)));

            List<FfprobeSourceDataChapter> chapters = data.Chapters
                .ToList();
            Chapters.AddRange(chapters
                .Select(c => new Chapter(c, chapters.IndexOf(c))));

            Format = data.Format;

            return this;
        });
        
    }
    
    public FfprobeSourceData GetSourceData()
    {
        return SourceData;
    }
    
    public string GetError()
    {
        return Error;
    }
    
    public async Task<(FfprobeSourceData?, string)> GetJson(CancellationToken ct = default)
    {
        (string stdOut, string stdErr) = await ExecStdErrOut(ct);
        
        // Logger.Encoder(stdOut, LogEventLevel.Debug);
        Logger.Encoder(stdErr, LogEventLevel.Debug);
        
        return (stdOut.FromJson<FfprobeSourceData>(), stdErr);
    }

    private async Task<(string, string)> ExecStdErrOut(CancellationToken ct = default)
    {
        Process ffprobe = new();

        ffprobe.StartInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            FileName = AppFiles.FfProbePath,
            Arguments = $"-hide_banner -v quiet -show_format -show_streams -show_chapters -print_format json \"{_filename}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true
        };

        ffprobe.Start();

        string stdOut = await ffprobe.StandardOutput.ReadToEndAsync(ct);
        string error = await ffprobe.StandardError.ReadToEndAsync(ct);

        ffprobe.Close();

        return (stdOut, error);
    }
}