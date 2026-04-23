using NoMercy.Encoder.LiveTranscode;
using NoMercy.Encoder.Startup;
using NoMercyQueue;

namespace NoMercy.Service.Configuration;

/// <summary>
/// Real <see cref="IEncoderActivityProbe"/> implementation that plugs the
/// Encoder's deferred benchmark into the live queue and streaming state.
/// The encoder is considered busy when there's at least one active live
/// transcode session OR at least one queue worker running an encoder job.
/// </summary>
internal sealed class EncoderActivityProbe(QueueRunner queueRunner, ISessionManager sessionManager)
    : IEncoderActivityProbe
{
    public bool IsBusy
    {
        get
        {
            if (sessionManager.ActiveSessionCount > 0)
                return true;

            foreach (string key in queueRunner.GetActiveWorkerThreads().Keys)
            {
                if (key.StartsWith("encoder", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
