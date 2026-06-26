namespace NoMercy.NmSystem.Configuration;

public class ServerConfig
{
    public int InternalServerPort { get; set; } = 7626;
    public int ExternalServerPort { get; set; } = 7626;
    public bool Swagger { get; set; } = true;
    public bool IsDev { get; set; }
    public bool IsTest { get; set; }
    public string ManagementPipeName { get; set; } = "NoMercyManagement";
}
