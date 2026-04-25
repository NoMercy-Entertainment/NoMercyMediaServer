using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoMercy.Encoder.Subscribers;

/// <summary>
/// Forces resolution of the V3 event-bus subscribers at host startup so their
/// constructors run and they subscribe to the event bus. The subscribers are
/// registered as singletons and self-subscribe in their constructors; without
/// this activator they would never be resolved and therefore never receive
/// events.
///
/// Coexists with the legacy <c>NoMercy.MediaProcessing.EventHandlers.AutoEncodeSubscriber</c>
/// — the V3 <see cref="AutoEncodeSubscriber"/> is gated on
/// <c>EncoderOptions.WatchedFolderProfiles</c> being non-empty (default empty),
/// so it is dormant on a default install. Operators opt in by populating that
/// config; the legacy DB-driven path keeps working for users who haven't
/// migrated. Each subscriber also exposes a per-class enable flag on
/// <c>EncoderOptions</c> for hard opt-out.
/// </summary>
internal sealed class V3SubscriberActivator(
    IServiceProvider services,
    ILogger<V3SubscriberActivator> logger
) : IHostedService, IDisposable
{
    private AutoEncodeSubscriber? _autoEncode;
    private IntroDetectSubscriber? _introDetect;
    private OcrPostEncodeSubscriber? _ocrPostEncode;
    private CropDetectSubscriber? _cropDetect;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _autoEncode = services.GetRequiredService<AutoEncodeSubscriber>();
        _introDetect = services.GetRequiredService<IntroDetectSubscriber>();
        _ocrPostEncode = services.GetRequiredService<OcrPostEncodeSubscriber>();
        _cropDetect = services.GetRequiredService<CropDetectSubscriber>();

        logger.LogInformation(
            "V3 encoder subscribers activated (auto_encode, intro_detect, ocr_post_encode, crop_detect)"
        );
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _autoEncode?.Dispose();
        _introDetect?.Dispose();
        _ocrPostEncode?.Dispose();
        _autoEncode = null;
        _introDetect = null;
        _ocrPostEncode = null;
        _cropDetect = null;
    }
}
