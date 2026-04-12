namespace NoMercy.Encoder.V3.Progress;

public interface IProgressObserver
{
    void OnProgress(EncodingProgress progress);

    void OnCompleted(string correlationId);

    void OnError(string correlationId, string message);
}
