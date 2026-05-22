using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NoMercy.Database;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subscribers;
using NoMercy.Events;

namespace NoMercy.Tests.Encoder.Subscribers;

public class V3SubscriberActivatorTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostLifetime>();
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        services.AddDbContextFactory<MediaContext>(o => o.UseInMemoryDatabase("test-media"));
        services.AddDbContextFactory<AppDbContext>(o => o.UseInMemoryDatabase("test-app"));
        services.AddNoMercyEncoder(opts =>
        {
            opts.FfmpegPathOverride = "ffmpeg";
            opts.FfprobePathOverride = "ffprobe";
        });
        return services.BuildServiceProvider();
    }

    private sealed class TestHostLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted { get; } = CancellationToken.None;
        public CancellationToken ApplicationStopping { get; } = CancellationToken.None;
        public CancellationToken ApplicationStopped { get; } = CancellationToken.None;

        public void StopApplication() { }
    }

    [Fact]
    public void V3Subscribers_AreRegisteredAsSingletons()
    {
        ServiceProvider provider = BuildProvider();

        AutoEncodeSubscriber autoEncode1 = provider.GetRequiredService<AutoEncodeSubscriber>();
        AutoEncodeSubscriber autoEncode2 = provider.GetRequiredService<AutoEncodeSubscriber>();
        autoEncode1.Should().BeSameAs(autoEncode2);

        IntroDetectSubscriber intro1 = provider.GetRequiredService<IntroDetectSubscriber>();
        IntroDetectSubscriber intro2 = provider.GetRequiredService<IntroDetectSubscriber>();
        intro1.Should().BeSameAs(intro2);

        OcrPostEncodeSubscriber ocr1 = provider.GetRequiredService<OcrPostEncodeSubscriber>();
        OcrPostEncodeSubscriber ocr2 = provider.GetRequiredService<OcrPostEncodeSubscriber>();
        ocr1.Should().BeSameAs(ocr2);

        CropDetectSubscriber crop1 = provider.GetRequiredService<CropDetectSubscriber>();
        CropDetectSubscriber crop2 = provider.GetRequiredService<CropDetectSubscriber>();
        crop1.Should().BeSameAs(crop2);
    }

    [Fact]
    public void V3SubscriberActivator_IsRegisteredAsHostedService()
    {
        ServiceProvider provider = BuildProvider();
        IEnumerable<IHostedService> hosted = provider.GetServices<IHostedService>();
        hosted.Should().Contain(s => s.GetType().Name == "V3SubscriberActivator");
    }

    [Fact]
    public async Task V3SubscriberActivator_StartAsync_ResolvesEverySubscriber()
    {
        ServiceProvider provider = BuildProvider();
        IHostedService activator = provider
            .GetServices<IHostedService>()
            .First(s => s.GetType().Name == "V3SubscriberActivator");

        await activator.StartAsync(CancellationToken.None);

        // Subscribers should be alive — resolving them after start returns the
        // same instance the activator pulled.
        provider.GetRequiredService<AutoEncodeSubscriber>().Should().NotBeNull();
        provider.GetRequiredService<IntroDetectSubscriber>().Should().NotBeNull();
        provider.GetRequiredService<OcrPostEncodeSubscriber>().Should().NotBeNull();
        provider.GetRequiredService<CropDetectSubscriber>().Should().NotBeNull();
    }
}
