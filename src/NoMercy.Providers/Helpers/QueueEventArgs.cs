namespace NoMercy.Providers.Helpers;

public class QueueEventArgs : EventArgs
{
    public object? Result { get; set; }
    public Exception? Error { get; set; }
}