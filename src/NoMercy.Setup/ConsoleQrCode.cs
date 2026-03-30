using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Setup;

/// <summary>
/// Displays a QR code for device auth in the terminal.
/// Delegates to <see cref="SetupTerminalUi"/> for interactive terminals
/// and falls back to a plain log line in non-interactive / service contexts.
/// </summary>
public class ConsoleQrCode
{
    /// <summary>
    /// Show the QR code and device auth UI in the terminal.
    /// Builds the setup page URL from the server's port and localhost.
    /// </summary>
    public static void Display(
        string verificationUriComplete,
        string verificationUri,
        string userCode
    )
    {
        string setupPageUrl = $"http://localhost:{Config.InternalServerPort}/setup";
        Display(verificationUriComplete, verificationUri, userCode, setupPageUrl);
    }

    /// <summary>
    /// Show the QR code and device auth UI in the terminal with a specific setup page URL.
    /// </summary>
    public static void Display(
        string verificationUriComplete,
        string verificationUri,
        string userCode,
        string setupPageUrl
    )
    {
        if (!SetupTerminalUi.IsInteractiveTerminal)
        {
            Logger.Auth($"Scan QR code or visit: {verificationUriComplete}");
            Logger.Auth($"Code: {userCode}");
            Logger.Auth($"Setup page: {setupPageUrl}");
            return;
        }

        SetupTerminalUi ui = new();
        ui.Show(verificationUriComplete, verificationUri, userCode, setupPageUrl);

        // Keep the UI alive until the process ends — the terminal UI object
        // is intentionally not disposed here so the resize watcher keeps running.
        // Callers that own the lifecycle (e.g. SetupServer) pass their own instance.
    }

    /// <summary>
    /// Legacy overload — accepts only the complete verification URI.
    /// Used by Auth.TokenByDeviceGrant() which predates the terminal UI.
    /// </summary>
    public static void Display(string verificationUriComplete)
    {
        // Parse a best-effort display URI from the complete one
        string displayUri;
        try
        {
            Uri uri = new(verificationUriComplete);
            displayUri = $"{uri.Scheme}://{uri.Host}";
            if (!uri.IsDefaultPort)
                displayUri += $":{uri.Port}";
            displayUri += uri.AbsolutePath.TrimEnd('/');
        }
        catch
        {
            displayUri = verificationUriComplete;
        }

        string userCode = "";
        try
        {
            System.Collections.Specialized.NameValueCollection query =
                System.Web.HttpUtility.ParseQueryString(new Uri(verificationUriComplete).Query);
            userCode = query["user_code"] ?? "";
        }
        catch
        {
            userCode = "";
        }

        Display(verificationUriComplete, displayUri, userCode);
    }
}
