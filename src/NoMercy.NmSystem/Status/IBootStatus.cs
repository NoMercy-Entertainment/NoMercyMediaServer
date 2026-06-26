using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.Status;

public interface IBootStatus
{
    bool IsStarted { get; }
    void MarkStarted();
}

public class BootStatus : IBootStatus
{
    private volatile bool _started;

    public bool IsStarted => _started;

    public void MarkStarted()
    {
        _started = true;
    }
}
