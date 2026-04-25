using NoMercy.Encoder.Progress;
using NoMercy.Networking.Messaging;

namespace NoMercy.Api.EventHandlers;

/// <summary>
/// Forwards analysis-job progress to all dashboard subscribers via
/// <c>IClientMessenger</c> on <c>dashboardHub</c> / <c>AnalysisProgress</c>.
///
/// Payload shape:
/// <code>
/// {
///   "job_id":      string,
///   "type":        "crop" | "fingerprint" | "whisper" | "ocr" | "benchmark",
///   "percent":     double  // [0–100]
///   "stage":       string,
///   "eta_seconds": double? // null when unknown
/// }
/// </code>
///
/// Dashboard clients subscribe to one hub (<c>dashboardHub</c>) and render
/// any analysis job type uniformly. Existing encode-progress events on the
/// same hub are not affected.
/// </summary>
public sealed class SignalRAnalysisProgressObserver(IClientMessenger clientMessenger)
    : IAnalysisProgressObserver
{
    public void Report(
        string jobId,
        string type,
        double percent,
        string stage,
        double? etaSeconds = null
    )
    {
        // Fire-and-forget — Report is called from async analysis loops
        // that cannot await. Failures are non-fatal: the job continues
        // even if SignalR delivery drops.
        _ = clientMessenger.SendToAll(
            "AnalysisProgress",
            "dashboardHub",
            new
            {
                job_id = jobId,
                type,
                percent,
                stage,
                eta_seconds = etaSeconds,
            }
        );
    }
}
