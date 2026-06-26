namespace NoMercy.Service.Hosting;

public interface IShutdownCoordinator
{
    CancellationToken Token { get; }
    void RequestShutdown();
    void ForceShutdown();
}
