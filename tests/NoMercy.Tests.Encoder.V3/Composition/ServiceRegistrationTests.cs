namespace NoMercy.Tests.Encoder.V3.Composition;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Composition;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Infrastructure;
using NoMercy.Encoder.V3.Startup;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddNoMercyEncoder_RegistersAllCoreServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNoMercyEncoder(
            new EncoderOptions(FfmpegPath: "ffmpeg", FfprobePath: "ffprobe")
        );
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IProcessRunner>().Should().NotBeNull();
        provider.GetService<IMediaAnalyzer>().Should().NotBeNull();
        provider.GetService<CodecRegistry>().Should().NotBeNull();
        provider.GetService<ICodecResolver>().Should().NotBeNull();
        provider.GetService<IHardwareDetector>().Should().NotBeNull();
        provider.GetService<IFfmpegCapabilities>().Should().NotBeNull();
        provider.GetService<EncoderOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddNoMercyEncoder_RegistersHostedService()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNoMercyEncoder(
            new EncoderOptions(FfmpegPath: "ffmpeg", FfprobePath: "ffprobe")
        );
        ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();
        hostedServices.Should().Contain(s => s.GetType().Name == "HardwareInitializationService");
    }

    [Fact]
    public void AddNoMercyEncoder_CodecRegistry_IsSingleton()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNoMercyEncoder(
            new EncoderOptions(FfmpegPath: "ffmpeg", FfprobePath: "ffprobe")
        );
        ServiceProvider provider = services.BuildServiceProvider();
        CodecRegistry first = provider.GetRequiredService<CodecRegistry>();
        CodecRegistry second = provider.GetRequiredService<CodecRegistry>();
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddNoMercyEncoder_Options_AreAvailable()
    {
        ServiceCollection services = new();
        services.AddLogging();
        EncoderOptions options = new(
            FfmpegPath: "/usr/bin/ffmpeg",
            FfprobePath: "/usr/bin/ffprobe"
        );
        services.AddNoMercyEncoder(options);
        ServiceProvider provider = services.BuildServiceProvider();
        EncoderOptions resolved = provider.GetRequiredService<EncoderOptions>();
        resolved.FfmpegPath.Should().Be("/usr/bin/ffmpeg");
        resolved.FfprobePath.Should().Be("/usr/bin/ffprobe");
    }
}
