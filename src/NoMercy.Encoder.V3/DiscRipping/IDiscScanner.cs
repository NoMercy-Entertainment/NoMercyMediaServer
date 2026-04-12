namespace NoMercy.Encoder.V3.DiscRipping;

public interface IDiscScanner
{
    Task<DiscInfo> ScanAsync(string drivePath, CancellationToken ct);
}
