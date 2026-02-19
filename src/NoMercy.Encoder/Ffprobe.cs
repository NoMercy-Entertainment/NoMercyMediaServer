// ReSharper disable MemberCanBePrivate.Global

using System.Diagnostics;
using NoMercy.Encoder.Dto;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Encoder;

public class Ffprobe
{
    private readonly string _filename;
    private FfprobeSourceData SourceData { get; set; } = new();
    public string Error { get; private set; } = string.Empty;

    public string FilePath => _filename;
    public TimeSpan Duration => Format.Duration ?? TimeSpan.Zero;
    public VideoStream? PrimaryVideoStream => VideoStreams.FirstOrDefault();
    public AudioStream? PrimaryAudioStream => AudioStreams.FirstOrDefault();
    public SubtitleStream? PrimarySubtitleStream => SubtitleStreams.FirstOrDefault();
    public ImageStream? PrimaryImageStream => ImageStreams.FirstOrDefault();
    
    /// <summary>
    /// Timeout for ffprobe execution in milliseconds. Default is 30 seconds.
    /// </summary>
    private const int ExecutionTimeoutMs = 30000;
    
    /// <summary>
    /// Maximum number of retry attempts if ffprobe times out or fails.
    /// </summary>
    private const int MaxRetries = 3;
    
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

    public static async Task<Ffprobe> CreateAsync(string file, CancellationToken ct = default)
    {
        return await new Ffprobe(file).GetStreamData();
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
        (string stdOut, string stdErr) = await ExecStdErrOutWithRetry(ct);
        
        // Logger.Encoder(stdOut, LogEventLevel.Debug);
        if (!string.IsNullOrEmpty(stdErr))
            Logger.Encoder(stdErr, LogEventLevel.Debug);
        
        return (stdOut.FromJson<FfprobeSourceData>(), stdErr);
    }

    private async Task<(string, string)> ExecStdErrOutWithRetry(CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                (string stdOut, string stdErr) = await ExecStdErrOut(ct);
                return (stdOut, stdErr);
            }
            catch (OperationCanceledException)
            {
                Logger.Encoder($"ffprobe execution timed out for {_filename} (attempt {attempt}/{MaxRetries})", 
                    LogEventLevel.Warning);
                
                if (attempt < MaxRetries)
                {
                    // Wait briefly before retrying to avoid immediate consecutive failures
                    await Task.Delay(500, ct);
                    continue;
                }
                
                Logger.Encoder($"ffprobe failed after {MaxRetries} attempts for {_filename}", LogEventLevel.Error);
                return (string.Empty, $"ffprobe timed out after {MaxRetries} attempts");
            }
            catch (Exception ex)
            {
                Logger.Encoder($"ffprobe execution failed for {_filename}: {ex.Message} (attempt {attempt}/{MaxRetries})", 
                    LogEventLevel.Warning);
                
                if (attempt < MaxRetries)
                {
                    await Task.Delay(500, ct);
                    continue;
                }
                
                Logger.Encoder($"ffprobe failed after {MaxRetries} attempts for {_filename}: {ex.Message}", 
                    LogEventLevel.Error);
                return (string.Empty, ex.Message);
            }
        }
        
        return (string.Empty, "ffprobe failed after maximum retries");
    }

    private async Task<(string, string)> ExecStdErrOut(CancellationToken ct = default)
    {
        Process? ffprobe = null;

        await FfProbeThrottle.WaitAsync(ct);
        try
        {
            // Create a timeout token that will cancel after ExecutionTimeoutMs
            using CancellationTokenSource timeoutCts = new(ExecutionTimeoutMs);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            ffprobe = new();

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

            string stdOut = await ffprobe.StandardOutput.ReadToEndAsync(linkedCts.Token);
            string error = await ffprobe.StandardError.ReadToEndAsync(linkedCts.Token);

            // Wait for process to complete with timeout
            bool exited = ffprobe.WaitForExit(ExecutionTimeoutMs);
            if (!exited)
            {
                try
                {
                    ffprobe.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // Process already exited between WaitForExit and Kill — safe to ignore
                }
                throw new OperationCanceledException("ffprobe process did not exit within timeout period");
            }

            return (stdOut, error);
        }
        finally
        {
            FfProbeThrottle.Release();
            ffprobe?.Dispose();
        }
    }
}