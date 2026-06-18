namespace NoMercy.Networking.Discovery;

public interface INetworkDiscovery
{
    string InternalIp { get; set; }

    /// <summary>
    /// The internal_ip value to send during registration and heartbeat: the discovered LAN IP
    /// when routable, otherwise the 0.0.0.0 sentinel the API accepts and resolves to the external
    /// domain. Never empty — an empty value fails the API's required|ip validation.
    /// </summary>
    string RegistrationInternalIp { get; }

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
