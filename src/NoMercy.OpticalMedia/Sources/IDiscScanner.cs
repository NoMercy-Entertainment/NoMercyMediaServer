namespace NoMercy.OpticalMedia.Sources;

public interface IDiscScanner
{
    Task<DiscInfo> ScanAsync(string drivePath, CancellationToken ct);
}
