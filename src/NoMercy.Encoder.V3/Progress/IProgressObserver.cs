namespace NoMercy.Encoder.V3.Progress;

using NoMercy.Encoder.V3.Errors;

public interface IProgressObserver
{
    void OnStageStarted(string stageName);

    void OnProgress(EncodingProgress progress);

    void OnStageCompleted(string stageName, TimeSpan duration);

    void OnError(EncodingError error);
}
