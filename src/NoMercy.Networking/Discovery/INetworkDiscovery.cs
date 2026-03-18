namespace NoMercy.Networking.Discovery;

public interface INetworkDiscovery
{
    string InternalIp { get; set; }
    string ExternalIp { get; set; }
    string? InternalIpV6 { get; }
    string? ExternalIpV6 { get; set; }
    string InternalDomain { get; }
    string InternalAddress { get; }
    string ExternalDomain { get; }
    string ExternalAddress { get; }
    string? ExternalAddressV6 { get; }
    bool Ipv6Enabled { get; }
    Task DiscoverExternalIpAsync();
}
