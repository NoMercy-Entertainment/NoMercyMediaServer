namespace NoMercy.NmSystem.Status;

public interface IUpdateStatus
{
    bool UpdateAvailable { get; set; }
    bool RestartNeeded { get; set; }
    string? LatestVersion { get; set; }
}

public class UpdateStatus : IUpdateStatus
{
    public bool UpdateAvailable { get; set; }
    public bool RestartNeeded { get; set; }
    public string? LatestVersion { get; set; }
}
