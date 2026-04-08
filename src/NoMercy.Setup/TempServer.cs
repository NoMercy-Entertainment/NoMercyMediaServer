using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NoMercy.NmSystem.Information;

namespace NoMercy.Setup;

public class TempServer
{
    public static WebApplication Start()
    {
        throw new InvalidOperationException(
            "TempServer is deprecated — the /sso-callback route is now handled by SetupModeMiddleware + SetupServer within the main Kestrel pipeline. "
            + "This method previously attempted to bind to the same port as the main server, causing conflicts. "
            + "Authentication now flows through the web UI exclusively."
        );
    }
}
