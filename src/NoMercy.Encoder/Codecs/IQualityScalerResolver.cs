namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Selects the correct <see cref="IQualityScaler"/> for a given FFmpeg
/// encoder handle. Walks the registered scalers in registration order,
/// returning the first whose <see cref="IQualityScaler.Supports"/> returns
/// true. Falls back to <see cref="LinearQualityScaler"/> when nothing else
/// matches.
/// </summary>
public interface IQualityScalerResolver
{
    IQualityScaler For(string encoderHandle);
}
