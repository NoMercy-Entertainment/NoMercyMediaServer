using Microsoft.Extensions.DependencyInjection;
using NoMercy.EncoderV2.Abstractions;
using NoMercy.EncoderV2.Factories;
using NoMercy.EncoderV2.PostProcessing;
using NoMercy.EncoderV2.Profiles;
using NoMercy.EncoderV2.Services;

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

        // Post-processing services
        services.AddScoped<IChapterProcessor, ChapterProcessor>();
        services.AddScoped<ISpriteGenerator, SpriteGenerator>();

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
}
