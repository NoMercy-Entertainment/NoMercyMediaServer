using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Status;

public interface IConnectivityStatus
{
    NatStatus NatStatus { get; set; }
    bool PortForwarded { get; set; }
    string? StunPublicIp { get; set; }
    int? StunPublicPort { get; set; }
}

public class ConnectivityStatus : IConnectivityStatus
{
    public NatStatus NatStatus { get; set; } = NatStatus.None;
    public bool PortForwarded { get; set; }
    public string? StunPublicIp { get; set; }
    public int? StunPublicPort { get; set; }
}
