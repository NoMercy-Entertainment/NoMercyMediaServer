namespace NoMercy.Encoder.Codecs;

/// <summary>
/// Default <see cref="IQualityScalerResolver"/> implementation. Walks the
/// DI-registered <see cref="IQualityScaler"/> collection in registration
/// order and returns the first whose <see cref="IQualityScaler.Supports"/>
/// returns true for the given encoder handle.
///
/// <para><see cref="LinearQualityScaler"/> must be registered last because
/// its <see cref="IQualityScaler.Supports"/> is unconditionally true —
/// it acts as the fallback for any unrecognised encoder.</para>
/// </summary>
public sealed class QualityScalerResolver(IEnumerable<IQualityScaler> scalers)
    : IQualityScalerResolver
{
    private readonly IQualityScaler[] _scalers = scalers.ToArray();

    public IQualityScaler For(string encoderHandle)
    {
        foreach (IQualityScaler scaler in _scalers)
        {
            if (scaler.Supports(encoderHandle))
                return scaler;
        }

        // Unreachable in practice: LinearQualityScaler always matches.
        // Defensive fallback so the return type is never null.
        return new LinearQualityScaler();
    }
}
