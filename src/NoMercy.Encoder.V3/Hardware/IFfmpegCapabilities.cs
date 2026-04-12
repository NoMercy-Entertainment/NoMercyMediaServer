namespace NoMercy.Encoder.V3.Hardware;

public interface IFfmpegCapabilities
{
    IReadOnlySet<string> AvailableEncoders { get; }
    IReadOnlySet<string> AvailableDecoders { get; }
    IReadOnlySet<string> AvailableFilters { get; }
    IReadOnlySet<string> AvailableProtocols { get; }
    bool HasEncoder(string name);
    bool HasFilter(string name);
    bool HasProtocol(string name);
    Task ProbeAsync(CancellationToken ct = default);
}
