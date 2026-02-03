using Microsoft.Extensions.DependencyInjection;
using NoMercy.EncoderV2.Execution;
using NoMercy.EncoderV2.FFmpeg;
using NoMercy.EncoderV2.Hardware;
using NoMercy.EncoderV2.Profiles;
using NoMercy.EncoderV2.Progress;
using NoMercy.EncoderV2.Repositories;
using NoMercy.EncoderV2.Shared.Telemetry;
using NoMercy.EncoderV2.Specifications.HLS;
using NoMercy.EncoderV2.Streams;
using NoMercy.EncoderV2.Tasks;
using NoMercy.EncoderV2.Validation;
using NoMercy.NmSystem.Dto;

namespace NoMercy.EncoderV2.Composition;

/// <summary>
/// Dependency injection configuration for EncoderV2
/// Registers all services, repositories, and managers with proper scoping
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Register all EncoderV2 services with the DI container
    /// </summary>
    public static IServiceCollection AddEncoderV2Services(this IServiceCollection services)
    {
        // Repositories (Scoped - one per request)
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IJobRepository, JobRepository>();

        // Managers (Scoped - one per request)
        services.AddScoped<IProfileManager, ProfileManager>();

        // Validators (Scoped)
        services.AddScoped<IProfileValidator, Profiles.ProfileValidator>();
        services.AddScoped<ICodecValidator, CodecValidator>();
        services.AddScoped<IOutputValidator, OutputValidator>();
        services.AddScoped<IPlaylistValidator, PlaylistValidator>();

        // Stream Analysis (Scoped)
        services.AddScoped<IStreamAnalyzer, StreamAnalyzer>();

        // Stream Processors (Scoped)
        services.AddScoped<IAudioStreamProcessor, AudioStreamProcessor>();
        services.AddScoped<IVideoStreamProcessor, VideoStreamProcessor>();
        services.AddScoped<ISubtitleStreamProcessor, SubtitleStreamProcessor>();

        // FFmpeg Services (Scoped)
        services.AddScoped<IFFmpegService, FFmpegService>();

        // Progress Monitoring (Scoped)
        services.AddScoped<IProgressMonitor, ProgressMonitor>();

        // Task Distribution (Scoped)
        services.AddScoped<ITaskSplitter, TaskSplitter>();

        // Hardware Acceleration (Singleton - GPU detection is expensive)
        services.AddSingleton<IHardwareAccelerationService, HardwareAccelerationService>();

        // Job Execution (Scoped)
        services.AddScoped<IEncodingJobExecutor, EncodingJobExecutor>();

        // Executors and registries (Singleton)
        services.AddSingleton<ExecutorRegistry>();

        // HLS Services (Scoped)
        services.AddScoped<IHLSPlaylistGenerator, HLSPlaylistGenerator>();
        services.AddScoped<IHLSValidator, HLSValidator>();
        services.AddScoped<IHLSOutputOrchestrator, HLSOutputOrchestrator>();

        // Codec Selection (Singleton - wraps V1's static codec selector)
        services.AddSingleton<ICodecSelector, CodecSelectorAdapter>();

        // High-level encoding service (Scoped)
        services.AddScoped<Services.IEncodingService, Services.EncodingService>();

        // Telemetry (Singleton - shared across application)
        services.AddSingleton<ITelemetryClient, TelemetryClient>();

        return services;
    }

    /// <summary>
    /// Legacy method for backward compatibility
    /// Use AddEncoderV2Services() for new code
    /// </summary>
    public static IServiceCollection AddFfmpegProcessExecutor(this IServiceCollection services)
    {
        services.AddSingleton<ExecutorRegistry>();
        return services;
    }
}