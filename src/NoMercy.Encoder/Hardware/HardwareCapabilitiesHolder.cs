namespace NoMercy.Encoder.Hardware;

/// <summary>
/// Singleton holder that decouples <see cref="IHardwareCapabilities"/>
/// resolution from <see cref="Startup.HardwareInitializationService"/>.
///
/// Without this indirection the IHardwareCapabilities factory would have to
/// resolve HIS, which transitively pulls in HardwareBenchmark — and
/// HardwareBenchmark itself depends on IHardwareCapabilities, forming a
/// singleton resolution cycle that causes the DI container to recurse until
/// the test host overflows the stack.
///
/// HIS sets <see cref="Current"/> after detection completes; the
/// IHardwareCapabilities factory reads it and falls back to a CPU-only
/// default when called before detection has run.
/// </summary>
public sealed class HardwareCapabilitiesHolder
{
    public IHardwareCapabilities? Current { get; set; }
}
