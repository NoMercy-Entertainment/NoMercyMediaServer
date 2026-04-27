namespace NoMercy.Networking.Devices;

public sealed record DeviceListItem
{
    public required Ulid DeviceId { get; init; }
    public required string Fingerprint { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Online { get; init; }
    public string? LanIp { get; init; }
    public DateTime? LastSeenAt { get; init; }
}
