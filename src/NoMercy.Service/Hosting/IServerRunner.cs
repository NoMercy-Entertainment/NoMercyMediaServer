using Microsoft.AspNetCore.Builder;

namespace NoMercy.Service.Hosting;

public interface IServerRunner
{
    Task<bool> RunWithHttpsRestart(
        WebApplication httpHost,
        StartupOptions options,
        Setup.Boot.BootOrchestrator orchestrator
    );
    Task<bool> RunHost(WebApplication host);
}
