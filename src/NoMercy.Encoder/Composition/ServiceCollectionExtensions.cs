namespace NoMercy.Encoder.Composition;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.BuildingBlocks.Drm;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Encoder.DiscRipping;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.Notifications;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Startup;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Audio;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Encoder.Strategies.Mkv;
using NoMercy.Encoder.Strategies.Mp4;
using NoMercy.Encoder.Subtitles;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNoMercyEncoder(
        this IServiceCollection services,
        Action<EncoderOptions>? configure = null
    )
    {
        // Configuration
        EncoderOptions opts = new();
        configure?.Invoke(opts);
        services.AddSingleton(opts);

        // IHttpClientFactory is needed by the self-registration service and
        // by HttpRemoteWorker callers. Safe to call multiple times — the
        // factory registration is idempotent.
        services.AddHttpClient();

        // Infrastructure
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IMediaAnalyzer, MediaAnalyzer>();

        // Codecs
        services.AddSingleton<CodecRegistry>();
        services.AddSingleton<ICodecResolver, CodecResolver>();

        // Hardware
        services.AddSingleton<IHardwareDetector, PlatformHardwareDetector>();
        services.AddSingleton<FfmpegCapabilities>();
        services.AddSingleton<IFfmpegCapabilities>(sp =>
            sp.GetRequiredService<FfmpegCapabilities>()
        );

        // IHardwareCapabilities factory — reads Capabilities after startup completes
        services.AddSingleton<IHardwareCapabilities>(sp =>
        {
            HardwareInitializationService initService =
                sp.GetRequiredService<HardwareInitializationService>();
            return initService.Capabilities
                ?? new HardwareCapabilities([], Environment.ProcessorCount);
        });

        // IResourceMonitor — cross-platform CPU/memory readings via
        // System.Diagnostics. GPU utilization stays at 0 until a vendor-
        // specific plugin replaces this with one that shells out to
        // nvidia-smi / rocm-smi / intel_gpu_top.
        services.AddSingleton<IResourceMonitor, ProcessResourceMonitor>();

        // IResourceBudget — built from IHardwareCapabilities after detection completes
        services.AddSingleton<IResourceBudget>(sp =>
        {
            IHardwareCapabilities hw = sp.GetRequiredService<IHardwareCapabilities>();
            IResourceMonitor monitor = sp.GetRequiredService<IResourceMonitor>();
            return new ResourceBudget(hw.Gpus, hw.CpuCores, monitor);
        });

        // Startup — register concrete first so IHostedService resolves same instance
        services.AddSingleton<HardwareInitializationService>();
        services.AddHostedService(sp => sp.GetRequiredService<HardwareInitializationService>());
        services.AddHostedService<HardwareBenchmarkHostedService>();

        // HDR
        services.AddTransient<ITonemapSelector, TonemapSelector>();

        // Profiles
        services.AddTransient<IProfileValidator, ProfileValidator>();
        services.AddTransient<IPresetResolver, PresetResolver>();

        // Execution
        services.AddTransient<IFfmpegExecutor, FfmpegExecutor>();
        services.AddTransient<ProgressParser>();
        // ProcessThrottle holds the set of suspended pids; must be Singleton so
        // suspend/resume operations across different callers see the same state.
        services.AddSingleton<ProcessThrottle>();
        services.AddSingleton<IEncoderProcessRegistry, EncoderProcessRegistry>();

        // Checkpoint persistence
        services.AddTransient<ICheckpointStore, JsonCheckpointStore>();

        // Content intelligence (OCR / Whisper / crop detection)
        services.TryAddSingleton(sp =>
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NoMercy-MediaServer");
            return client;
        });
        services.AddTransient<ITesseractModelManager, TesseractModelManager>();
        services.AddTransient<ISubtitleOcrEngine, SubtitleOcrEngine>();
        services.AddTransient<IWhisperTranscriber, WhisperTranscriber>();
        services.AddTransient<ICropDetector, CropDetector>();
        services.AddTransient<IAudioFingerprinter, ChromaprintFingerprinter>();
        services.AddTransient<IIntroDetector, ChromaprintIntroDetector>();

        // Notifications — plugins can replace INotificationDispatcher to swap
        // webhooks for Discord/Slack/email/etc.
        services.AddSingleton<INotificationDispatcher, WebhookNotificationDispatcher>();

        // Building blocks
        services.AddTransient<IFilterGraphBuilder, FilterGraphBuilder>();
        services.AddTransient<IPlaylistGenerator, PlaylistGenerator>();
        services.AddTransient<ISubtitleExtractor, SubtitleExtractor>();
        services.AddTransient<IFontExtractor, FontExtractor>();
        services.AddTransient<IChapterWriter, ChapterWriter>();
        services.AddTransient<IThumbnailGenerator, ThumbnailGenerator>();
        services.AddTransient<IHlsVariantAnalyzer, HlsVariantAnalyzer>();
        services.AddTransient<IAbrLadderGenerator, AbrLadderGenerator>();

        // DRM processors — plugins can register additional processors for
        // custom schemes. IEnumerable<IDrmProcessor> resolution picks the
        // one whose Method matches the profile's DrmConfig.
        services.AddTransient<IDrmProcessor, Aes128HlsDrmProcessor>();

        // Output strategies — resolved via IOutputStrategyFactory from DI.
        // Plugins can register additional IOutputStrategy impls and the factory
        // will prefer them (last registration wins).
        services.AddTransient<IOutputStrategy, HlsOutputStrategy>();
        services.AddTransient<IOutputStrategy, MkvOutputStrategy>();
        services.AddTransient<IOutputStrategy, Mp4OutputStrategy>();
        services.AddTransient<IOutputStrategy, DashOutputStrategy>();
        services.AddTransient<IOutputStrategy, Mp3OutputStrategy>();
        services.AddTransient<IOutputStrategy, FlacOutputStrategy>();
        services.AddTransient<IOutputStrategy, OggOutputStrategy>();
        services.AddTransient<IOutputStrategyFactory, OutputStrategyFactory>();

        // Pipeline stages
        services.AddTransient<AnalyzeStage>();
        services.AddTransient<ValidateStage>();
        services.AddTransient<PlanStage>();
        services.AddTransient<BuildStage>();
        services.AddTransient<ExecuteStage>();
        services.AddTransient<FinalizeStage>();

        // Optimizer
        services.AddTransient<ExecutionGraphBuilder>();
        services.AddTransient<GroupingStrategy>();
        services.AddTransient<ResourceAllocator>();
        services.AddTransient<CostEstimator>();

        // Encoder
        services.AddTransient<IEncoder, Encoder>();

        // Strategies — one per {OutputFormat, EncodeMode} tuple.
        // Plugins can register additional IEncodingStrategy impls and the resolver
        // will pick them up automatically (last registration wins).
        services.AddTransient<IEncodingStrategy, HlsSinglePassStrategy>();
        services.AddTransient<IEncodingStrategy, HlsTwoPassStrategy>();
        services.AddTransient<IEncodingStrategy, MkvStrategy>();
        services.AddTransient<IEncodingStrategy, Mp4SinglePassStrategy>();
        services.AddTransient<IEncodingStrategy, Mp4TwoPassStrategy>();
        services.AddTransient<IEncodingStrategy, DashSinglePassStrategy>();
        services.AddTransient<IEncodingStrategy, DashTwoPassStrategy>();
        services.AddTransient<IEncodingStrategy, Mp3Strategy>();
        services.AddTransient<IEncodingStrategy, FlacStrategy>();
        services.AddTransient<IEncodingStrategy, OggStrategy>();
        services.AddTransient<IStrategyResolver, StrategyResolver>();
        services.AddTransient<IEncodingOrchestrator, EncodingOrchestrator>();

        // Live Transcoding
        services.AddSingleton<IPlaybackDecisionEngine, PlaybackDecisionEngine>();
        services.AddSingleton<LiveSessionLimits>();
        services.AddSingleton<ISessionManager>(sp => new SessionManager(
            sp.GetRequiredService<LiveSessionLimits>()
        ));
        services.AddTransient<ILiveQualitySelector, LiveQualitySelector>();
        services.AddTransient<BufferManager>();
        services.AddSingleton<ISpeedIndexStore, JsonSpeedIndexStore>();
        services.AddSingleton<IHardwareBenchmark, HardwareBenchmark>();

        // Lazy-load cached SpeedIndex from disk (empty when no benchmark has run yet).
        services.AddSingleton<SpeedIndex>(sp =>
            sp.GetRequiredService<IHardwareBenchmark>().GetCachedIndex()
        );
        services.AddTransient<ILiveFfmpegRunner, LiveFfmpegRunner>();
        services.AddSingleton<ILiveEncoder, LiveEncoder>();
        services.AddSingleton<ILiveStreamingService, LiveStreamingService>();
        services.AddTransient<ILivePlaylistBuilder, LivePlaylistBuilder>();

        // Distribution — LocalWorkerDispatcher is the default; remote workers
        // land as a follow-up behind a feature flag. Plugins can register a
        // replacement IWorkerDispatcher to change behavior project-wide.
        services.AddTransient<LocalWorkerDispatcher>();
        services.AddTransient<IWorkerAssigner, WorkerAssigner>();

        // Registry is a singleton so registrations accumulate across the
        // process lifetime. InMemoryRemoteWorkerRegistry replaces the
        // empty default so runtime worker registration actually takes
        // effect — callers can still swap this out for a SignalR-backed
        // registry later.
        services.AddSingleton<InMemoryRemoteWorkerRegistry>();
        services.AddSingleton<IRemoteWorkerRegistry>(sp =>
            sp.GetRequiredService<InMemoryRemoteWorkerRegistry>()
        );

        // Signed transport between coordinator and workers.
        services.AddTransient<ITaskSerializer, TaskSerializer>();

        // Source fetching: when the worker can't see the task's input
        // path locally, it downloads from the coordinator via
        // HttpSourceFetcher. Swapped to NullSourceFetcher when the
        // install uses shared storage and no fetching is needed.
        services.AddTransient<ISourceFetcher, HttpSourceFetcher>();

        // Per-task progress: workers push to the coordinator via
        // HttpTaskProgressSink (only actually pushes when CoordinatorUrl
        // is set). Coordinator-side InMemoryTaskProgressStore holds the
        // latest snapshot per task so the dashboard can read live progress.
        services.AddSingleton<InMemoryTaskProgressStore>();
        services.AddSingleton<ITaskProgressStore>(sp =>
            sp.GetRequiredService<InMemoryTaskProgressStore>()
        );
        services.AddTransient<ITaskProgressSink, HttpTaskProgressSink>();

        // Self-registration background service — no-ops on standalone
        // installs (when CoordinatorUrl is not set). Safe to always
        // register; the service exits cleanly in that case.
        services.AddHostedService<WorkerSelfRegistrationService>();
        // The remote dispatcher is the public face — it transparently falls
        // back to the local dispatcher when no remote workers are registered,
        // which is the default behavior today.
        services.AddTransient<IWorkerDispatcher>(sp => new RemoteWorkerDispatcher(
            sp.GetRequiredService<IRemoteWorkerRegistry>(),
            sp.GetRequiredService<IWorkerAssigner>(),
            sp.GetRequiredService<LocalWorkerDispatcher>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RemoteWorkerDispatcher>>()
        ));

        // Disc ripping — DriveMonitor is Singleton because its polling loop
        // holds state (last-seen drives) across MonitorAsync() enumerations.
        services.AddTransient<IDiscScanner, DiscScanner>();
        services.AddSingleton<IDriveMonitor, DriveMonitor>();
        services.AddTransient<IDiscRipper, DiscRipper>();

        return services;
    }
}
