namespace NoMercy.Setup.Server;

public interface IApiKeyLoader
{
    Task LoadKeys(CancellationToken ct = default);
}
