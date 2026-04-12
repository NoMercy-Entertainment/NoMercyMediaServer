namespace NoMercy.Encoder.V3.Composition;

using Microsoft.Extensions.DependencyInjection;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Infrastructure;
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

        // Startup — register concrete first so IHostedService resolves same instance
        services.AddSingleton<HardwareInitializationService>();
        services.AddHostedService(sp => sp.GetRequiredService<HardwareInitializationService>());

        return services;
    }
}
