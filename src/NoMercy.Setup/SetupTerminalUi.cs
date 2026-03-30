using NoMercy.NmSystem.SystemCalls;
using QRCoder;
using Serilog.Events;

namespace NoMercy.Setup;

/// <summary>
/// Manages a persistent, full-screen terminal display during setup mode.
/// Draws QR code, device code, and setup URLs. Redraws on terminal resize.
/// Falls back gracefully when running as a service or without a TTY.
/// </summary>
public sealed class SetupTerminalUi : IDisposable
{
    // Minimum terminal width needed to render the QR code block
    private const int MinWidthForQr = 40;

    private readonly CancellationTokenSource _cts = new();
    private Task? _resizeWatchTask;
    private bool _isActive;

    private string? _verificationUriComplete;
    private string? _verificationUri;
    private string? _userCode;
    private string? _setupPageUrl;
    private string _statusLine = "Waiting for you to sign in...";

    private int _lastKnownWidth;
    private int _lastKnownHeight;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the terminal is interactive and large enough to draw
    /// the UI. Gates all Console.WindowWidth / WindowHeight calls.
    /// </summary>
    public static bool IsInteractiveTerminal
    {
        get
        {
            if (!Environment.UserInteractive)
                return false;
            if (Console.IsOutputRedirected)
                return false;

            try
            {
                int width = Console.WindowWidth;
                return width > 0;
            }
            catch (IOException)
            {
                // Windows services throw IOException — treat as non-interactive
                return false;
            }
        }
    }

    /// <summary>
    /// Start showing the setup UI. Call once when auth is needed.
    /// In non-interactive mode, logs URLs once and returns immediately.
    /// </summary>
    public void Show(
        string verificationUriComplete,
        string verificationUri,
        string userCode,
        string setupPageUrl
    )
    {
        _verificationUriComplete = verificationUriComplete;
        _verificationUri = verificationUri;
        _userCode = userCode;
        _setupPageUrl = setupPageUrl;

        if (!IsInteractiveTerminal)
        {
            // Non-interactive: Docker, systemd, Windows service
            // Just log the essential info once — no terminal UI
            Logger.Setup("=== NoMercy Setup Required ===");
            Logger.Setup($"Open in your browser: {setupPageUrl}");
            Logger.Setup($"Or visit:             {verificationUriComplete}");
            Logger.Setup($"Device code:          {userCode}");
            Logger.Setup("==============================");
            return;
        }

        _isActive = true;
        _lastKnownWidth = GetTerminalWidth();
        _lastKnownHeight = GetTerminalHeight();

        Draw();
        StartResizeWatcher();
    }

    /// <summary>
    /// Update the status line shown at the bottom of the terminal UI.
    /// </summary>
    public void SetStatus(string message)
    {
        _statusLine = message;
        if (_isActive && IsInteractiveTerminal)
            Draw();
    }

    /// <summary>
    /// Transition to a progress message (after auth completes).
    /// </summary>
    public void ShowProgress(string phase, string detail)
    {
        if (!IsInteractiveTerminal)
            return;

        _isActive = false;
        StopResizeWatcher();

        try
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
        }
        catch (IOException)
        {
            return;
        }

        string phaseLabel = phase switch
        {
            "Authenticating" => "Signed in successfully!",
            "Authenticated" => "Signed in successfully!",
            "Registering" => "Connecting your server to NoMercy...",
            "Registered" => "Setting up your server address...",
            "CertificateAcquired" => "Securing your connection...",
            "Complete" => "All done!",
            _ => phase,
        };

        Console.WriteLine();
        Console.WriteLine($"  {phaseLabel}");
        if (!string.IsNullOrEmpty(detail))
            Console.WriteLine($"  {detail}");
        Console.WriteLine();
    }

    /// <summary>
    /// Show a completion message and server URL after setup finishes.
    /// </summary>
    public void ShowComplete(string serverUrl)
    {
        if (!IsInteractiveTerminal)
            return;

        StopResizeWatcher();
        _isActive = false;

        try
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
        }
        catch (IOException)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("  Setup complete!");
        Console.WriteLine($"  Your server is running at {serverUrl}");
        Console.WriteLine();
    }

    public void Dispose()
    {
        StopResizeWatcher();
        _cts.Dispose();
    }

    // ── Drawing ─────────────────────────────────────────────────────────────

    private void Draw()
    {
        if (!IsInteractiveTerminal)
            return;

        int width = GetTerminalWidth();
        bool canDrawQr = width >= MinWidthForQr;

        try
        {
            Console.Clear();
            Console.SetCursorPosition(0, 0);
        }
        catch (IOException)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("  NoMercy MediaServer — Setup");
        Console.WriteLine();

        if (canDrawQr && !string.IsNullOrEmpty(_verificationUriComplete))
        {
            string[] qrLines = GenerateAsciiQr(_verificationUriComplete, width);

            if (qrLines.Length > 0)
            {
                // Centre each QR line
                int qrWidth = qrLines[0].Length;
                int leftPad = Math.Max(0, (width - qrWidth) / 2);
                string pad = new(' ', leftPad);

                foreach (string line in qrLines)
                    Console.WriteLine(pad + line);

                Console.WriteLine();
            }
        }

        if (!string.IsNullOrEmpty(_userCode))
        {
            Console.WriteLine($"  Code:  {_userCode}");
        }

        if (!string.IsNullOrEmpty(_verificationUri))
        {
            Console.WriteLine($"  Visit: {_verificationUri}");
        }

        if (!string.IsNullOrEmpty(_setupPageUrl))
        {
            Console.WriteLine();
            Console.WriteLine($"  Or open the setup page in your browser:");
            Console.WriteLine($"  {_setupPageUrl}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {_statusLine}");
        Console.WriteLine();
    }

    // ── QR generation ───────────────────────────────────────────────────────

    private static string[] GenerateAsciiQr(string text, int terminalWidth)
    {
        try
        {
            using QRCodeGenerator generator = new();
            using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.L);

            // Each module in AsciiQRCode is 2 chars wide (block chars).
            // Try module size 1 first; if it doesn't fit, skip QR entirely.
            AsciiQRCode qrCode = new(data);
            string raw = qrCode.GetGraphic(1);
            string[] lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length == 0)
                return [];

            int qrWidth = lines[0].Length;
            if (qrWidth + 4 > terminalWidth)
            {
                // QR won't fit — caller will fall back to text-only
                return [];
            }

            return lines;
        }
        catch (Exception ex)
        {
            Logger.Setup($"QR code generation failed: {ex.Message}", LogEventLevel.Debug);
            return [];
        }
    }

    // ── Resize watcher ──────────────────────────────────────────────────────

    private void StartResizeWatcher()
    {
        _resizeWatchTask = Task.Run(
            async () =>
            {
                while (!_cts.Token.IsCancellationRequested && _isActive)
                {
                    try
                    {
                        await Task.Delay(250, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (!IsInteractiveTerminal)
                        continue;

                    int newWidth = GetTerminalWidth();
                    int newHeight = GetTerminalHeight();

                    if (newWidth != _lastKnownWidth || newHeight != _lastKnownHeight)
                    {
                        _lastKnownWidth = newWidth;
                        _lastKnownHeight = newHeight;
                        Draw();
                    }
                }
            },
            _cts.Token
        );
    }

    private void StopResizeWatcher()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }

    // ── Safe console size reads ──────────────────────────────────────────────

    private static int GetTerminalWidth()
    {
        if (!Environment.UserInteractive)
            return 0;
        try
        {
            return Console.WindowWidth;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static int GetTerminalHeight()
    {
        if (!Environment.UserInteractive)
            return 0;
        try
        {
            return Console.WindowHeight;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
