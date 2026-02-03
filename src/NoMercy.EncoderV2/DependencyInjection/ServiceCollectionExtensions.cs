using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Factories;
using NoMercy.EncoderV2.PostProcessing;
using NoMercy.EncoderV2.Processing;
using NoMercy.EncoderV2.Profiles;
using NoMercy.EncoderV2.Repositories;
using NoMercy.EncoderV2.Services;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Streams;
using NoMercy.EncoderV2.Tasks;
using NoMercy.EncoderV2.Validation;
using NoMercy.EncoderV2.Workers;

namespace NoMercy.EncoderV2.DependencyInjection;

/// <summary>
/// Extension methods for registering EncoderV2 services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all EncoderV2 services to the service collection
    /// </summary>
    public static IServiceCollection AddEncoderV2(this IServiceCollection services)
    {
        return services.AddEncoderV2(new EncoderV2Options());
    }

    /// <summary>
    /// Adds all EncoderV2 services to the service collection with custom options
    /// </summary>
    public static IServiceCollection AddEncoderV2(this IServiceCollection services, EncoderV2Options options)
    {
        // Register options
        services.AddSingleton(options);

        // Core services
        services.AddSingleton<IFFmpegExecutor>(sp =>
        {
            EncoderV2Options opts = sp.GetRequiredService<EncoderV2Options>();
            if (!string.IsNullOrEmpty(opts.FfmpegPath) && !string.IsNullOrEmpty(opts.FfprobePath))
            {
                return new FFmpegExecutor(opts.FfmpegPath, opts.FfprobePath);
            }
            return new FFmpegExecutor();
        });

        services.AddSingleton<IMediaAnalyzer, MediaAnalyzer>();
        services.AddSingleton<IHardwareAccelerationDetector, HardwareAccelerationDetector>();

        // Factories
        services.AddSingleton<ICodecFactory, CodecFactory>();
        services.AddSingleton<IContainerFactory, ContainerFactory>();

        // Validation services
        services.AddScoped<IOutputValidator, OutputValidator>();

        // HLS services
        services.AddScoped<IHLSPlaylistGenerator, HLSPlaylistGenerator>();

        // Post-processing services
        services.AddScoped<IFontExtractor, FontExtractor>();
        services.AddScoped<IChapterProcessor, ChapterProcessor>();
        services.AddScoped<ISpriteGenerator, SpriteGenerator>();
        services.AddScoped<IPostProcessor, PostProcessor>();

        // HDR processing
        services.AddScoped<IHdrProcessor, HdrProcessor>();

        // Task distribution
        services.AddScoped<ITaskSplitter, TaskSplitter>();
        services.AddScoped<INodeSelector, NodeSelector>();
        services.AddScoped<IJobDispatcher, JobDispatcher>();

        // Node health monitoring (singleton for background service)
        services.AddSingleton<HealthMonitorOptions>(sp =>
        {
            EncoderV2Options opts = sp.GetRequiredService<EncoderV2Options>();
            return new HealthMonitorOptions
            {
                CheckIntervalSeconds = opts.HealthCheckIntervalSeconds,
                HeartbeatTimeoutSeconds = opts.HeartbeatTimeoutSeconds,
                AutoReassignTasks = opts.AutoReassignTasksOnNodeFailure,
                VerboseLogging = opts.VerboseHealthLogging
            };
        });
        services.AddSingleton<NodeHealthMonitor>();
        services.AddSingleton<INodeHealthMonitor>(sp => sp.GetRequiredService<NodeHealthMonitor>());
        services.AddHostedService(sp => sp.GetRequiredService<NodeHealthMonitor>());

        // Stream analysis
        services.AddScoped<IStreamAnalyzer, StreamAnalyzer>();

        // Repositories
        services.AddScoped<IProfileRepository, ProfileRepository>();

        // Profiles
        services.AddSingleton<IProfileRegistry>(sp =>
        {
            ProfileRegistry registry = new();

            // Register additional profile providers from options
            EncoderV2Options opts = sp.GetRequiredService<EncoderV2Options>();
            foreach (IProfileProvider provider in opts.AdditionalProfileProviders)
            {
                registry.RegisterProvider(provider);
            }

            return registry;
        });

        return services;
    }

    /// <summary>
    /// Adds the EncoderV2 task worker background service.
    /// Call this separately if you want the worker to process tasks automatically.
    /// </summary>
    public static IServiceCollection AddEncoderV2TaskWorker(this IServiceCollection services)
    {
        return services.AddEncoderV2TaskWorker(new EncoderV2WorkerOptions());
    }

    /// <summary>
    /// Adds the EncoderV2 task worker with custom options
    /// </summary>
    public static IServiceCollection AddEncoderV2TaskWorker(this IServiceCollection services, EncoderV2WorkerOptions options)
    {
        services.AddSingleton(options);
        services.AddHostedService<EncoderV2TaskWorker>();
        return services;
    }

    /// <summary>
    /// Adds the EncoderV2 task worker with configuration action
    /// </summary>
    public static IServiceCollection AddEncoderV2TaskWorker(this IServiceCollection services, Action<EncoderV2WorkerOptions> configure)
    {
        EncoderV2WorkerOptions options = new();
        configure(options);
        return services.AddEncoderV2TaskWorker(options);
    }

    /// <summary>
    /// Adds EncoderV2 services with a custom configuration action
    /// </summary>
    public static IServiceCollection AddEncoderV2(this IServiceCollection services, Action<EncoderV2Options> configure)
    {
        EncoderV2Options options = new();
        configure(options);
        return services.AddEncoderV2(options);
    }
}

/// <summary>
/// Configuration options for EncoderV2
/// </summary>
public sealed class EncoderV2Options
{
    /// <summary>
    /// Path to FFmpeg executable (null to use default)
    /// </summary>
    public string? FfmpegPath { get; set; }

    /// <summary>
    /// Path to FFprobe executable (null to use default)
    /// </summary>
    public string? FfprobePath { get; set; }

    /// <summary>
    /// Maximum number of concurrent encoding jobs
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 1;

    /// <summary>
    /// Whether to automatically detect and use hardware acceleration
    /// </summary>
    public bool AutoDetectHardwareAcceleration { get; set; } = true;

    /// <summary>
    /// Additional profile providers to register
    /// </summary>
    public List<IProfileProvider> AdditionalProfileProviders { get; } = [];

    /// <summary>
    /// Default output directory for encoded files
    /// </summary>
    public string? DefaultOutputDirectory { get; set; }

    /// <summary>
    /// Interval between node health checks in seconds (default: 30)
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum age of last heartbeat before a node is considered unhealthy (default: 60 seconds)
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to automatically reassign tasks when a node becomes unhealthy (default: true)
    /// </summary>
    public bool AutoReassignTasksOnNodeFailure { get; set; } = true;

    /// <summary>
    /// Whether to log detailed health check results (default: false)
    /// </summary>
    public bool VerboseHealthLogging { get; set; } = false;
}
