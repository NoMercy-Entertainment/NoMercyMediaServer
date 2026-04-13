namespace NoMercy.Encoder.DiscRipping;

public interface IDiscScanner
{
    Task<DiscInfo> ScanAsync(string drivePath, CancellationToken ct);
}
