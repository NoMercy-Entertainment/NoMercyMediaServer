#region License
// Copyright NoMercy (c) 2026. All rights reserved.
#endregion

using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Service.Hosting;

public class ShutdownCoordinator : IShutdownCoordinator, IDisposable
{
    private readonly ILogger<ShutdownCoordinator> _logger;
    private int _shutdownAttempts;
    private readonly object _shutdownLock = new();
    private readonly CancellationTokenSource _applicationShutdownCts = new();

    public ShutdownCoordinator(ILogger<ShutdownCoordinator> logger)
    {
        _logger = logger;
        
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public CancellationToken Token => _applicationShutdownCts.Token;

    public void RequestShutdown()
    {
        lock (_shutdownLock)
        {
            if (!_applicationShutdownCts.IsCancellationRequested)
            {
                _applicationShutdownCts.Cancel();
            }
        }
    }

    public void ForceShutdown()
    {
        _logger.LogInformation("Force shutdown requested!");
        Environment.Exit(1);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        lock (_shutdownLock)
        {
            _shutdownAttempts++;

            if (_shutdownAttempts == 1)
            {
                e.Cancel = true; // Prevent immediate termination
                _logger.LogInformation("Graceful shutdown initiated... (Press Ctrl+C again to force shutdown)");
                RequestShutdown();
            }
            else if (_shutdownAttempts >= 2)
            {
                e.Cancel = false; // Allow immediate termination
                ForceShutdown();
            }
        }
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _applicationShutdownCts.Dispose();
        GC.SuppressFinalize(this);
    }
}
