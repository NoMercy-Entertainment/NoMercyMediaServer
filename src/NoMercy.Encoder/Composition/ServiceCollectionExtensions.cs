namespace NoMercy.Encoder.Composition;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.BuildingBlocks;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.ContentAnalysis;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.PostProcess;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Startup;
using NoMercy.Encoder.Strategies;
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

        // IResourceBudget — built from IHardwareCapabilities after detection completes
        services.AddSingleton<IResourceBudget>(sp =>
        {
            IHardwareCapabilities hw = sp.GetRequiredService<IHardwareCapabilities>();
            return new ResourceBudget(hw.Gpus, hw.CpuCores);
        });

        // Startup — register concrete first so IHostedService resolves same instance
        services.AddSingleton<HardwareInitializationService>();
        services.AddHostedService(sp => sp.GetRequiredService<HardwareInitializationService>());

        // HDR
        services.AddTransient<ITonemapSelector, TonemapSelector>();

        // Profiles
        services.AddTransient<IProfileValidator, ProfileValidator>();

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

        // Building blocks
        services.AddTransient<IFilterGraphBuilder, FilterGraphBuilder>();
        services.AddTransient<IPlaylistGenerator, PlaylistGenerator>();
        services.AddTransient<ISubtitleExtractor, SubtitleExtractor>();
        services.AddTransient<IFontExtractor, FontExtractor>();
        services.AddTransient<IChapterWriter, ChapterWriter>();
        services.AddTransient<IThumbnailGenerator, ThumbnailGenerator>();
        services.AddTransient<IHlsVariantAnalyzer, HlsVariantAnalyzer>();
        services.AddTransient<IAbrLadderGenerator, AbrLadderGenerator>();

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
        services.AddTransient<IEncodingStrategy, DashSinglePassStrategy>();
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
        services.AddSingleton<SpeedIndex>(
            new SpeedIndex(new Dictionary<SpeedKey, SpeedMeasurement>())
        );
        services.AddSingleton<ILiveEncoder, LiveEncoder>();

        return services;
    }
}
