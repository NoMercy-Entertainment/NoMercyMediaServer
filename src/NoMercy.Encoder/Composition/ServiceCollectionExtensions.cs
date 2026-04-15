namespace NoMercy.Encoder.Composition;

using Microsoft.Extensions.DependencyInjection;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Hdr;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Startup;

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
        services.AddTransient<ProcessThrottle>();

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
