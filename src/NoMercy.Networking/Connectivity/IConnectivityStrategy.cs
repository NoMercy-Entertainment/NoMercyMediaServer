namespace NoMercy.Networking.Connectivity;

public interface IConnectivityStrategy
{
    string Name { get; }
    int Priority { get; }
    Task<bool> TryEstablishAsync(CancellationToken ct);
    Task TeardownAsync();
    ConnectivityType Type { get; }
}

public enum ConnectivityType
{
    DirectLan,
    PortForward,
    StunHolePunch,
    CloudflareTunnel,
    LocalOnly
}

public enum ConnectivityState
{
    Starting,
    Evaluating,
    DirectAccess,
    HolePunched,
    Tunneled,
    LocalOnly
}
