using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.BuildingBlocks.Drm;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.ContentAnalysis.Fingerprinting;
using NoMercy.Encoder.Devices;
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
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
using NoMercy.Encoder.Subscribers;
using NoMercy.Encoder.Subtitles;
using NoMercy.Resources;
using NoMercy.Storage;

namespace NoMercy.Encoder.Composition;

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
        // IStorage is the cross-project filesystem abstraction. Registered
        // via TryAdd inside AddNoMercyStorage so a host that already
        // configured allowed roots (or swapped in a non-local backend)
        // wins. Empty allowed-roots list keeps the path guard permissive
        // until consumers populate it during the encoder Phase 0.2 sweep.
        services.AddNoMercyStorage();

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IMediaAnalyzer, MediaAnalyzer>();

        // Codecs
        services.AddSingleton<CodecRegistry>();
        services.AddSingleton<ICodecResolver, CodecResolver>();
        services.AddSingleton<IHardwarePreferenceResolver, HardwarePreferenceResolver>();
        services.AddSingleton<IBitDepthPolicyResolver, BitDepthPolicyResolver>();

        // Quality scalers — specific encoders first, LinearQualityScaler last
        // (it is the unconditional fallback: Supports always returns true).
        services.AddSingleton<IQualityScaler, NvencQualityScaler>();
        services.AddSingleton<IQualityScaler, QsvQualityScaler>();
        services.AddSingleton<IQualityScaler, VideoToolboxQualityScaler>();
        services.AddSingleton<IQualityScaler, AmfAv1QualityScaler>();
        services.AddSingleton<IQualityScaler, SvtAv1QualityScaler>();
        services.AddSingleton<IQualityScaler, LinearQualityScaler>();
        services.AddSingleton<IQualityScalerResolver, QualityScalerResolver>();

        // Hardware
        services.AddSingleton<IHardwareDetector, PlatformHardwareDetector>();
        // Driver fingerprint lives in AppDbContext.Configuration so a server
        // admin can move the install without dragging a sidecar JSON file.
        // The DB store auto-imports any legacy driver_fingerprint.json on
        // first load and deletes the file on success.
        services.AddSingleton<IDriverFingerprintStore, DbDriverFingerprintStore>();
        services.AddSingleton<IDriverChangeDetector, DriverChangeDetector>();
        services.AddSingleton<FfmpegCapabilities>();
        services.AddSingleton<IFfmpegCapabilities>(sp =>
            sp.GetRequiredService<FfmpegCapabilities>()
        );

        // IHardwareCapabilities — forwards every read through the holder so
        // singletons constructed before HIS finishes detection (most notably
        // HardwareBenchmark) pick up the GPU list once detection completes.
        //
        // Earlier this was a factory that captured Holder.Current at first
        // resolution. That always returned null at startup and cached an
        // empty CPU-only HardwareCapabilities forever — so the benchmark
        // scheduler saw zero GPUs even after HIS detected an Nvidia / AMD /
        // Intel card, and ran software calibrations only. The holder-backed
        // forwarder fixes the staleness without disturbing the no-cycle
        // dependency graph below.
        //
        // The dependency graph does NOT pass through HardwareInitializationService.
        // HIS itself transitively depends on IHardwareCapabilities (via
        // BenchmarkJobTracker → HardwareBenchmark), so resolving capabilities
        // through HIS forms a cycle that crashes the test host with a stack
        // overflow. The holder is a tiny mutable singleton that HIS writes to
        // once detection completes; before that, callers receive a CPU-only
        // default that lets pipeline construction succeed without GPU info.
        services.AddSingleton<HardwareCapabilitiesHolder>();
        services.AddSingleton<IHardwareCapabilities, HolderBackedHardwareCapabilities>();

        // IResourceMonitor — cross-platform CPU/memory readings via
        // System.Diagnostics. GPU utilization stays at 0 until a vendor-
        // specific plugin replaces this with one that shells out to
        // nvidia-smi / rocm-smi / intel_gpu_top.
        services.AddSingleton<IResourceMonitor, ProcessResourceMonitor>();

        // NVENC session cap — enforced at dispatch time in EncodingOrchestrator
        // before ffmpeg is spawned. Consumer NVIDIA GPUs cap at 3 concurrent sessions;
        // pro cards report int.MaxValue (no cap). NvencSessionCap reads the limit from
        // IHardwareCapabilities.Gpus[*].MaxEncoderSessions.
        services.AddSingleton<INvencSessionCap, NvencSessionCap>();

        // IResourceBudget — holds IHardwareCapabilities live so the GPU list
        // is re-read after async detection completes. The previous factory
        // captured hw.Gpus eagerly (an empty list at startup since
        // HardwareInitializationService runs as a hosted service AFTER the DI
        // graph is built), which silently produced a semaphore map with zero
        // entries. Every encoder-gpu task then crashed on first job pick
        // because GetGpuSemaphore threw "device 'h264_nvenc' is not
        // registered" — see UnobservedTaskException entries in encoder logs
        // before this fix.
        services.AddSingleton<IResourceBudget>(sp =>
        {
            IHardwareCapabilities hw = sp.GetRequiredService<IHardwareCapabilities>();
            IResourceMonitor monitor = sp.GetRequiredService<IResourceMonitor>();
            ResourceBudgetOptions options =
                sp.GetService<ResourceBudgetOptions>() ?? ResourceBudgetOptions.Disabled;
            ILogger<ResourceBudget> logger = sp.GetRequiredService<ILogger<ResourceBudget>>();
            return new ResourceBudget(hw, monitor, options, logger);
        });

        // Startup — register concrete first so IHostedService resolves same instance
        services.AddSingleton<HardwareInitializationService>();
        services.AddHostedService(sp => sp.GetRequiredService<HardwareInitializationService>());

        // Hardware benchmark runs lazily — defers past ApplicationStarted, then
        // waits for the encoder to be idle (no active streams/jobs) before
        // spawning calibration ffmpeg processes. Startup is never blocked and
        // user-visible work never shares CPU/GPU with the benchmark.
        services.TryAddSingleton<IEncoderActivityProbe, NullEncoderActivityProbe>();
        services.AddHostedService<HardwareBenchmarkBackgroundService>();

        // Periodic recalibration — checks every hour whether the speed index
        // is stale (> 30 days) or a driver change occurred; defers to idle
        // before running. Separate from HardwareBenchmarkBackgroundService
        // which only handles the first-boot case.
        services.AddHostedService<HardwareBenchmarkRecalibrationService>();

        // HDR
        services.AddTransient<ITonemapSelector, TonemapSelector>();

        // Naming resolvers
        services.AddSingleton<IMediaKeyResolver, MediaKeyResolver>();
        services.AddSingleton<IOutputNamingResolver, OutputNamingResolver>();

        // Metadata injector and merger
        services.AddSingleton<IMetadataInjector, MetadataInjector>();
        services.AddSingleton<IMetadataMerger, MetadataMerger>();

        // Bundle writers
        services.AddSingleton<IBundleManifestWriter, BundleManifestWriter>();
        services.AddSingleton<IReconstructionWriter, ReconstructionWriter>();
        services.AddSingleton<IBundleGarbageCollector, BundleGarbageCollector>();

        // Profiles
        services.AddTransient<IProfileValidator, V2BackedProfileValidator>();
        services.AddTransient<INamePresetResolver, ByNamePresetResolver>();
        services.AddSingleton<IProfileSignatureVerifier, ProfileSignatureVerifier>();

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
        services.AddSingleton<ISubtitleRouter, SubtitleRouter>();
        services.AddTransient<IWhisperTranscriber, WhisperTranscriber>();
        services.AddTransient<ICropDetector, CropDetector>();
        services.AddTransient<IAudioFingerprinter, ChromaprintFingerprinter>();
        services.AddTransient<IIntroDetector, ChromaprintIntroDetector>();

        // Notifications — plugins can replace INotificationDispatcher to swap
        // webhooks for Discord/Slack/email/etc.
        services.AddSingleton<INotificationDispatcher, WebhookNotificationDispatcher>();

        // Subtitle acquisition — adapter + service. IOpenSubtitlesProvider has a
        // no-op default registered here so encoder-only test contexts always
        // resolve. The Service host registers the real XML-RPC provider after
        // this call, replacing the no-op via standard DI last-wins.
        services.AddSingleton<IOpenSubtitlesProvider, NoOpOpenSubtitlesProvider>();
        services.AddTransient<IOpenSubtitlesAdapter, OpenSubtitlesAdapter>();
        services.AddTransient<ISubtitleAcquisitionService, SubtitleAcquisitionService>();

        // Subtitle burn-in filter builders
        services.AddTransient<AssBurnInFilterBuilder>();
        services.AddTransient<PgsBurnInFilterBuilder>();

        // Building blocks
        services.AddTransient<IFilterGraphBuilder, FilterGraphBuilder>();
        services.AddTransient<IPlaylistGenerator, PlaylistGenerator>();
        services.AddTransient<ISubtitleExtractor, SubtitleExtractor>();
        services.AddTransient<IFontExtractor, FontExtractor>();
        services.AddTransient<IChapterWriter, ChapterWriter>();
        services.AddTransient<IThumbnailGenerator, ThumbnailGenerator>();
        services.AddTransient<IHlsVariantAnalyzer, HlsVariantAnalyzer>();
        services.AddTransient<IAbrLadderGenerator, AbrLadderGenerator>();
        services.AddSingleton<ILadderGenerator, LadderGenerator>();

        // DRM processors — plugins can register additional processors for
        // custom schemes. IEnumerable<IDrmProcessor> resolution picks the
        // one whose Method matches the profile's DrmConfig.
        services.AddTransient<IDrmProcessor, Aes128HlsDrmProcessor>();
        // CENC processor handles DASH raw-key packaging via shaka-packager.
        // PrepareAsync is a no-op for CENC (post-encode); callers that need
        // CENC packaging resolve CencDrmProcessor directly and call PackageAsync.
        services.AddTransient<CencDrmProcessor>();

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
        services.AddTransient<IOutputStrategy, AudioHlsOutputStrategy>();
        services.AddTransient<IOutputStrategyFactory, OutputStrategyFactory>();

        // Pipeline stages — each concrete stage is also reachable via its
        // named role interface so strategies can resolve by role without
        // coupling to the concrete class.
        services.AddTransient<AnalyzeStage>();
        services.AddTransient<IAnalysisStage>(sp => sp.GetRequiredService<AnalyzeStage>());
        services.AddTransient<ValidateStage>();
        services.AddTransient<IValidationStage>(sp => sp.GetRequiredService<ValidateStage>());
        services.AddTransient<PlanStage>();
        services.AddTransient<IPlanStage>(sp => sp.GetRequiredService<PlanStage>());
        services.AddTransient<BuildStage>();
        services.AddTransient<IBuildStage>(sp => sp.GetRequiredService<BuildStage>());
        services.AddTransient<ExecuteStage>();
        services.AddTransient<IExecutionStage>(sp => sp.GetRequiredService<ExecuteStage>());
        services.AddTransient<FinalizeStage>();
        services.AddTransient<IFinalizeStage>(sp => sp.GetRequiredService<FinalizeStage>());

        // Optimizer
        services.AddTransient<ExecutionGraphBuilder>();
        services.AddTransient<GroupingStrategy>();
        services.AddTransient<ResourceAllocator>();
        services.AddTransient<CostEstimator>();

        // Encoder
        services.AddTransient<IEncoder, Pipeline.Encoder>();

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
        services.AddTransient<IEncodingStrategy, AudioHlsStrategy>();
        services.AddTransient<IStrategyResolver, StrategyResolver>();
        services.AddTransient<IEncodingOrchestrator, EncodingOrchestrator>();

        // Device capability negotiation
        services.AddSingleton<IDeviceCapabilityRegistry, DeviceCapabilityRegistry>();
        services.AddSingleton<IDeviceAwareVariantSelector, DeviceAwareVariantSelector>();

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
        services.AddSingleton<IBenchmarkJobTracker, BenchmarkJobTracker>();

        // Lazy-load cached SpeedIndex from disk (empty when no benchmark has run yet).
        services.AddSingleton<SpeedIndex>(sp =>
            sp.GetRequiredService<IHardwareBenchmark>().GetCachedIndex()
        );
        services.AddTransient<ILiveFfmpegRunner, LiveFfmpegRunner>();
        services.AddSingleton<ILiveEncoder, LiveEncoder>();
        services.AddSingleton<ILiveStreamingService, LiveStreamingService>();
        services.AddTransient<ILivePlaylistBuilder, LivePlaylistBuilder>();

        // At startup: delete any lts-* orphan dirs left by a previous crash.
        services.AddHostedService<LiveTranscodeOrphanSweeper>();

        // Every 30 s: evict sessions with no playlist/segment hit in the last N minutes.
        services.AddHostedService<LiveSessionIdleReaper>();

        // Every 5 s: suspend over-buffered encoders, resume drained ones, drop quality when buffer is low.
        services.AddHostedService<BufferAdaptiveService>();

        // Distribution — LocalWorkerDispatcher is the default; remote workers
        // land as a follow-up behind a feature flag. Plugins can register a
        // replacement IWorkerDispatcher to change behavior project-wide.
        services.AddTransient<LocalWorkerDispatcher>();
        services.AddTransient<IWorkerAssigner, WorkerAssigner>();

        // Registry is a singleton so registrations accumulate across the
        // process lifetime. InMemoryRemoteWorkerRegistry is always registered
        // as the runtime backing store. In coordinator mode (signing key set
        // + no CoordinatorUrl) we wrap it with JsonRemoteWorkerRegistry so
        // worker identities survive a coordinator restart. Worker mode and
        // standalone installs use the in-memory registry directly.
        services.AddSingleton<InMemoryRemoteWorkerRegistry>();
        services.AddSingleton<IRemoteWorkerRegistry>(sp =>
        {
            EncoderOptions encoderOpts = sp.GetRequiredService<EncoderOptions>();

            if (encoderOpts.IsCoordinatorMode)
            {
                string persistPath = encoderOpts.WorkerRegistryPath;
                return new JsonRemoteWorkerRegistry(
                    inner: sp.GetRequiredService<InMemoryRemoteWorkerRegistry>(),
                    filePath: persistPath,
                    httpClientFactory: sp.GetRequiredService<IHttpClientFactory>(),
                    serializer: sp.GetRequiredService<ITaskSerializer>(),
                    signingKey: encoderOpts.GetDistributedEncodingSigningKey(),
                    logger: sp.GetRequiredService<ILogger<JsonRemoteWorkerRegistry>>(),
                    storage: sp.GetRequiredService<IStorage>()
                );
            }

            return sp.GetRequiredService<InMemoryRemoteWorkerRegistry>();
        });

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

        // Capability probe — deferred 5 s after ApplicationStarted.
        // Checks fpcalc, Whisper model, Tesseract eng.traineddata, and
        // validates required FFmpeg filters/muxers. Reuses FfmpegCapabilities
        // for filters/protocols to avoid duplicate shell calls.
        services.AddSingleton<IFfmpegCapabilityProbe, FfmpegCapabilityProbe>();
        services.AddHostedService<FfmpegCapabilityProbeBackgroundService>();

        // BuiltinPresetSeeder retired — Service-side EncodingPresetsSeed
        // is the single source of truth for the curated preset library.

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
            sp.GetRequiredService<ILogger<RemoteWorkerDispatcher>>()
        ));

        // Disc ripping registrations moved to NoMercy.OpticalMedia/Composition/
        // ServiceCollectionExtensions.AddNoMercyOpticalMedia(); call that from
        // the host alongside AddNoMercyEncoder().

        // V3 event-bus subscribers — registered as singletons so the activator
        // can resolve them once at start-up (their constructors subscribe to
        // the event bus). Coexists with the legacy MediaProcessing subscribers
        // — V3 AutoEncode is dormant until EncoderOptions.WatchedFolderProfiles
        // is populated, so default installs see no behaviour change. Each
        // subscriber has its own opt-out flag on EncoderOptions for hard
        // disable.
        services.AddSingleton<AutoEncodeSubscriber>();
        services.AddSingleton<IntroDetectSubscriber>();
        services.AddSingleton<OcrPostEncodeSubscriber>();
        services.AddSingleton<CropDetectSubscriber>();
        services.AddHostedService<V3SubscriberActivator>();

        return services;
    }
}
