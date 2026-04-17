namespace NoMercy.Encoder.Composition;

public class EncoderOptions
{
    public string? FfmpegPathOverride { get; set; }
    public string? FfprobePathOverride { get; set; }

    /// <summary>
    /// Resolved FFmpeg path: uses the configured override (set via AppFiles.FfmpegPath at startup).
    /// Throws if not configured — callers must register via AddNoMercyEncoder with the path set.
    /// </summary>
    public string FfmpegPath =>
        FfmpegPathOverride
        ?? throw new InvalidOperationException(
            "FfmpegPathOverride not configured. Set it via AddNoMercyEncoder()."
        );

    /// <summary>
    /// Resolved FFprobe path: uses the configured override (set via AppFiles.FfProbePath at startup).
    /// Throws if not configured — callers must register via AddNoMercyEncoder with the path set.
    /// </summary>
    public string FfprobePath =>
        FfprobePathOverride
        ?? throw new InvalidOperationException(
            "FfprobePathOverride not configured. Set it via AddNoMercyEncoder()."
        );
    public int DefaultSegmentDurationSeconds { get; set; } = 4;
    public int MaxBufferAheadSeconds { get; set; } = 30;
    public int MinBufferAheadSeconds { get; set; } = 15;
    public int ProgressThrottleMs { get; set; } = 500;
    public string? SpeedIndexCachePath { get; set; }
    public bool AutoCalibrate { get; set; } = true;

    /// <summary>
    /// Directory that holds Tesseract *.traineddata files. The manager creates
    /// this folder on demand and downloads missing language models into it.
    /// Required for subtitle OCR (PGS / VobSub → WebVTT).
    /// </summary>
    public string? TesseractModelsDirectory { get; set; }

    /// <summary>
    /// Absolute path to the whisper.cpp GGML model (e.g. ggml-large-v3.bin).
    /// Required for speech-to-text subtitle generation.
    /// </summary>
    public string? WhisperModelPath { get; set; }

    /// <summary>
    /// Webhook URLs that receive a JSON POST for each encoder lifecycle event
    /// (started / completed / failed). Empty by default. Each URL is retried up
    /// to 3 times with exponential backoff; failures are logged and swallowed
    /// so a notification error never fails the encode.
    /// </summary>
    public IList<string> NotificationWebhookUrls { get; } = [];

    /// <summary>
    /// Parent directory for live transcode scratch folders. Each live session
    /// gets its own subfolder <c>{this}/{sessionId}</c> that holds the HLS
    /// playlist + .ts segments emitted by FFmpeg. Caller is responsible for
    /// cleanup when the session ends. Defaults to
    /// <see cref="Path.GetTempPath"/><c>/nomercy-live</c>.
    /// </summary>
    public string? LiveTranscodeCachePath { get; set; }

    public string ResolvedLiveTranscodeCachePath =>
        LiveTranscodeCachePath ?? Path.Combine(Path.GetTempPath(), "nomercy-live");

    /// <summary>
    /// Shared secret that signs distributed-encoding payloads between the
    /// coordinator and remote workers (see <see cref="NoMercy.Encoder.Distribution.TaskSerializer"/>).
    /// Must be identical on both sides — any divergence causes every task
    /// and every heartbeat to fail HMAC verification. Leave null on single-
    /// machine installs; distribution features stay disabled.
    /// </summary>
    public string? DistributedEncodingSigningKey { get; set; }

    /// <summary>
    /// True when a distributed-encoding signing key is configured — the API
    /// layer uses this to gate worker registration endpoints.
    /// </summary>
    public bool IsDistributedEncodingEnabled =>
        !string.IsNullOrWhiteSpace(DistributedEncodingSigningKey);

    public byte[] GetDistributedEncodingSigningKey()
    {
        if (string.IsNullOrWhiteSpace(DistributedEncodingSigningKey))
            throw new InvalidOperationException(
                "DistributedEncodingSigningKey not configured — set it via AddNoMercyEncoder() to enable remote workers."
            );
        return System.Text.Encoding.UTF8.GetBytes(DistributedEncodingSigningKey);
    }
}
