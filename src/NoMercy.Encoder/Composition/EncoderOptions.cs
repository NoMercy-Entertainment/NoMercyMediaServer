using NoMercy.Encoder.Profiles;

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

    // ── Background-subscriber toggles ──────────────────────────────────────

    /// <summary>
    /// When true (default), <c>AutoEncodeSubscriber</c> fires an encode job
    /// whenever new media files land in a folder mapped by
    /// <see cref="WatchedFolderProfiles"/>.
    /// </summary>
    public bool EnableAutoEncodeSubscriber { get; set; } = true;

    /// <summary>
    /// When true (default), <c>IntroDetectSubscriber</c> runs chromaprint
    /// fingerprinting + intro/outro detection after a library scan completes.
    /// </summary>
    public bool EnableIntroDetectSubscriber { get; set; } = true;

    /// <summary>
    /// When true (default), <c>OcrPostEncodeSubscriber</c> dispatches an OCR
    /// follow-up job when an encode that contains bitmap subtitle streams
    /// (PGS / VOBSUB) targeting HLS or DASH finishes.
    /// </summary>
    public bool EnableOcrPostEncodeSubscriber { get; set; } = true;

    /// <summary>
    /// When true (default), crop detection runs as part of the PlanStage
    /// (see <c>CropDetectSubscriber</c> for details — the live wiring lives
    /// inside the pipeline, not as a separate post-process event).
    /// </summary>
    public bool EnableCropDetectSubscriber { get; set; } = true;

    /// <summary>
    /// Maps absolute folder paths to the <see cref="EncodingProfile"/> that
    /// should be applied when new files are detected in that folder.
    /// Used by <c>AutoEncodeSubscriber</c>. Empty by default — auto-encode
    /// is effectively disabled until at least one mapping is added.
    /// </summary>
    public Dictionary<string, EncodingProfile> WatchedFolderProfiles { get; } = [];

    public byte[] GetDistributedEncodingSigningKey()
    {
        if (string.IsNullOrWhiteSpace(DistributedEncodingSigningKey))
            throw new InvalidOperationException(
                "DistributedEncodingSigningKey not configured — set it via AddNoMercyEncoder() to enable remote workers."
            );
        return System.Text.Encoding.UTF8.GetBytes(DistributedEncodingSigningKey);
    }

    /// <summary>
    /// Coordinator URL this worker should register with on boot. When set,
    /// the self-registration hosted service POSTs to
    /// <c>{CoordinatorUrl}/api/v1/dashboard/workers/register</c> on startup
    /// and sends heartbeats every
    /// <see cref="WorkerHeartbeatInterval"/>. Null = this process is a
    /// coordinator (or a standalone server) and shouldn't self-register.
    /// </summary>
    public string? CoordinatorUrl { get; set; }

    /// <summary>
    /// The base URL remote coordinators can reach THIS worker on. Must be
    /// set when <see cref="CoordinatorUrl"/> is set — the coordinator
    /// records this URL and POSTs encode tasks to it. Loopback only works
    /// for same-host-coordinator setups.
    /// </summary>
    public string? WorkerSelfBaseUrl { get; set; }

    /// <summary>
    /// Identifier this worker registers itself under. Defaults to the
    /// machine name so multi-box setups get stable identities across
    /// restarts without manual config.
    /// </summary>
    public string WorkerId { get; set; } = Environment.MachineName;

    /// <summary>
    /// How often the self-registration service heartbeats the coordinator.
    /// Must be noticeably less than the registry's stale threshold (60s
    /// default) so transient network blips don't evict the worker.
    /// </summary>
    public TimeSpan WorkerHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(20);
}
