using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Progress;

public interface IProgressObserver
{
    void OnStageStarted(string stageName);

    void OnProgress(EncodingProgress progress);

    void OnStageCompleted(string stageName, TimeSpan duration);

    void OnCompleted();

    void OnError(EncodingError error);

    void OnPlanResolved(
        List<string> videoStreams,
        List<string> audioStreams,
        List<string> subtitleStreams,
        bool hasGpu,
        bool isHdr
    );
}
