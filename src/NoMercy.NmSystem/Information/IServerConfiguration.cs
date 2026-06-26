namespace NoMercy.NmSystem.Information;

public interface IServerConfiguration
{
    string AuthBaseUrl { get; }
    string TokenClientId { get; }
    string AppBaseUrl { get; }
    string ApiBaseUrl { get; }
    string ApiServerBaseUrl { get; }
    string DnsServer { get; }
    string UserAgent { get; }
    bool Started { get; }
    string? CloudflareTunnelToken { get; }
    int InternalServerPort { get; }
    int ExternalServerPort { get; }
    string ManagementPipeName { get; }
    string ManagementSocketPath { get; }
    bool Swagger { get; }
    bool IsDev { get; }
    bool IsTest { get; }
    bool UpdateAvailable { get; }
    bool RestartNeeded { get; }
    string? LatestVersion { get; }
    
    int LibraryWorkers { get; }
    int ImportWorkers { get; }
    int ExtrasWorkers { get; }
    int EncoderWorkers { get; }
    int EncoderTaskWorkers { get; }
    int GpuEncoderWorkers { get; }
    int CpuEncoderWorkers { get; }
    
    double EncoderCpuHeadroomPercent { get; }
    double EncoderGpuHeadroomPercent { get; }
    long EncoderMinFreeMemoryMb { get; }
    
    int CronWorkers { get; }
    int ImageWorkers { get; }
    int FileWorkers { get; }
    int MusicWorkers { get; }
    int PaletteWorkers { get; }
    
    bool ShowAdultContent { get; }
}
