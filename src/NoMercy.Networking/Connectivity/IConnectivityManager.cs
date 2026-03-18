namespace NoMercy.Networking.Connectivity;

public interface IConnectivityManager
{
    ConnectivityState CurrentState { get; }
    ConnectivityType ActiveStrategy { get; }
    Task EvaluateAsync(CancellationToken ct);
    event Action<ConnectivityState>? StateChanged;
}
