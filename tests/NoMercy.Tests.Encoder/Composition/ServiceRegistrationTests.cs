namespace NoMercy.Tests.Encoder.Composition;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Optimizer;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Startup;

public class ServiceRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddNoMercyEncoder(
            new EncoderOptions(FfmpegPath: "ffmpeg", FfprobePath: "ffprobe")
        );
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddNoMercyEncoder_RegistersAllCoreServices()
    {
        ServiceProvider provider = BuildProvider();

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
        ServiceProvider provider = BuildProvider();
        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();
        hostedServices.Should().Contain(s => s.GetType().Name == "HardwareInitializationService");
    }

    [Fact]
    public void AddNoMercyEncoder_CodecRegistry_IsSingleton()
    {
        ServiceProvider provider = BuildProvider();
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

    [Fact]
    public void AddNoMercyEncoder_IEncoder_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        IEncoder encoder = provider.GetRequiredService<IEncoder>();
        encoder.Should().NotBeNull();
        encoder.Should().BeOfType<Encoder>();
    }

    [Fact]
    public void AddNoMercyEncoder_IProfileValidator_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        IProfileValidator validator = provider.GetRequiredService<IProfileValidator>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<ProfileValidator>();
    }

    [Fact]
    public void AddNoMercyEncoder_IFfmpegExecutor_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        IFfmpegExecutor executor = provider.GetRequiredService<IFfmpegExecutor>();
        executor.Should().NotBeNull();
        executor.Should().BeOfType<FfmpegExecutor>();
    }

    [Fact]
    public void AddNoMercyEncoder_IFileSystem_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        IFileSystem fileSystem = provider.GetRequiredService<IFileSystem>();
        fileSystem.Should().NotBeNull();
        fileSystem.Should().BeOfType<FileSystemAdapter>();
    }

    [Fact]
    public void AddNoMercyEncoder_IHardwareCapabilities_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        IHardwareCapabilities capabilities = provider.GetRequiredService<IHardwareCapabilities>();
        capabilities.Should().NotBeNull();
    }

    [Fact]
    public void AddNoMercyEncoder_PipelineStages_AllResolve()
    {
        ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<AnalyzeStage>().Should().NotBeNull();
        provider.GetRequiredService<ValidateStage>().Should().NotBeNull();
        provider.GetRequiredService<PlanStage>().Should().NotBeNull();
        provider.GetRequiredService<BuildStage>().Should().NotBeNull();
        provider.GetRequiredService<ExecuteStage>().Should().NotBeNull();
        provider.GetRequiredService<FinalizeStage>().Should().NotBeNull();
    }

    [Fact]
    public void AddNoMercyEncoder_OptimizerServices_AllResolve()
    {
        ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<ExecutionGraphBuilder>().Should().NotBeNull();
        provider.GetRequiredService<GroupingStrategy>().Should().NotBeNull();
        provider.GetRequiredService<ResourceAllocator>().Should().NotBeNull();
        provider.GetRequiredService<CostEstimator>().Should().NotBeNull();
    }

    [Fact]
    public void AddNoMercyEncoder_ExecutionHelpers_AllResolve()
    {
        ServiceProvider provider = BuildProvider();

        provider.GetRequiredService<ProgressParser>().Should().NotBeNull();
        provider.GetRequiredService<ProcessThrottle>().Should().NotBeNull();
    }

    [Fact]
    public void AddNoMercyEncoder_HardwareInitializationService_IsSingleton()
    {
        ServiceProvider provider = BuildProvider();
        HardwareInitializationService first =
            provider.GetRequiredService<HardwareInitializationService>();
        HardwareInitializationService second =
            provider.GetRequiredService<HardwareInitializationService>();
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddNoMercyEncoder_ILiveEncoder_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        ILiveEncoder encoder = provider.GetRequiredService<ILiveEncoder>();
        encoder.Should().NotBeNull();
        encoder.Should().BeOfType<LiveEncoder>();
    }

    [Fact]
    public void AddNoMercyEncoder_IPlaybackDecisionEngine_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        IPlaybackDecisionEngine engine = provider.GetRequiredService<IPlaybackDecisionEngine>();
        engine.Should().NotBeNull();
        engine.Should().BeOfType<PlaybackDecisionEngine>();
    }

    [Fact]
    public void AddNoMercyEncoder_ISessionManager_Resolves()
    {
        ServiceProvider provider = BuildProvider();
        ISessionManager manager = provider.GetRequiredService<ISessionManager>();
        manager.Should().NotBeNull();
        manager.Should().BeOfType<SessionManager>();
    }
}
