namespace NoMercy.Encoder.Progress;

/// <summary>
/// No-op implementation of <see cref="IAnalysisProgressObserver"/>.
/// Used as the DI default so analysis jobs compile and run without a
/// real SignalR hub — tests and headless workers use this automatically.
/// </summary>
public sealed class NullAnalysisProgressObserver : IAnalysisProgressObserver
{
    public static readonly NullAnalysisProgressObserver Instance = new();

    public void Report(
        string jobId,
        string type,
        double percent,
        string stage,
        double? etaSeconds = null
    ) { }
}
