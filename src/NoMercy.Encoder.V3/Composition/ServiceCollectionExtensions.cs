namespace NoMercy.Encoder.V3.Composition;

using Microsoft.Extensions.DependencyInjection;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Execution;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Infrastructure;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Pipeline.Optimizer;
using NoMercy.Encoder.V3.Pipeline.Stages;
using NoMercy.Encoder.V3.Profiles;
using NoMercy.Encoder.V3.Startup;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNoMercyEncoder(
        this IServiceCollection services,
        EncoderOptions options
    )
    {
        // Configuration
        services.AddSingleton(options);

        // Infrastructure
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IMediaAnalyzer, MediaAnalyzer>();

        // Codecs
        services.AddSingleton<CodecRegistry>();
        services.AddSingleton<ICodecResolver, CodecResolver>();

        // Hardware
        services.AddSingleton<IHardwareDetector, NullHardwareDetector>();
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

        // Startup — register concrete first so IHostedService resolves same instance
        services.AddSingleton<HardwareInitializationService>();
        services.AddHostedService(sp => sp.GetRequiredService<HardwareInitializationService>());

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

        return services;
    }
}
