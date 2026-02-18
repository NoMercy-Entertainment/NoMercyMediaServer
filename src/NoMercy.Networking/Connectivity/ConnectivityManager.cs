using Microsoft.Extensions.Hosting;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Networking.Connectivity;

public class ConnectivityManager : IConnectivityManager, IHostedService, IDisposable
{
    private readonly INetworkDiscovery _networkDiscovery;
    private readonly IEnumerable<IConnectivityStrategy> _strategies;
    private readonly CancellationTokenSource _stoppingCts = new();
    private Task? _executingTask;
    private IConnectivityStrategy? _activeStrategy;

    public ConnectivityState CurrentState { get; private set; } = ConnectivityState.Starting;
    public ConnectivityType ActiveStrategy => _activeStrategy?.Type ?? ConnectivityType.LocalOnly;
    public event Action<ConnectivityState>? StateChanged;

    public ConnectivityManager(
        INetworkDiscovery networkDiscovery,
        IEnumerable<IConnectivityStrategy> strategies)
    {
        _networkDiscovery = networkDiscovery;
        _strategies = strategies.OrderBy(s => s.Priority);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _executingTask = ExecuteAsync(_stoppingCts.Token);
        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!Config.Started && !cancellationToken.IsCancellationRequested)
                await Task.Delay(1000, cancellationToken);

            if (cancellationToken.IsCancellationRequested) return;

            if (Globals.Globals.AccessToken is null)
            {
                Logger.Setup("ConnectivityManager waiting for authentication...", LogEventLevel.Debug);
                int maxWait = 30;
                while (Globals.Globals.AccessToken is null && maxWait-- > 0 && !cancellationToken.IsCancellationRequested)
                    await Task.Delay(1000, cancellationToken);

                if (Globals.Globals.AccessToken is null)
                {
                    Logger.Setup("ConnectivityManager skipped — no authentication available", LogEventLevel.Debug);
                    return;
                }
            }

            // Discover external IP + UPnP BEFORE evaluating strategies,
            // so IsPortOpenAsync has the real external IP (not "0.0.0.0")
            await _networkDiscovery.DiscoverExternalIpAsync();

            await EvaluateAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
        catch (Exception ex)
        {
            Logger.Setup($"Error in ConnectivityManager: {ex.Message}", LogEventLevel.Warning);
        }
    }

    public async Task EvaluateAsync(CancellationToken ct)
    {
        SetState(ConnectivityState.Evaluating);

        // Tear down any existing strategy
        if (_activeStrategy is not null)
        {
            await _activeStrategy.TeardownAsync();
            _activeStrategy = null;
        }

        foreach (IConnectivityStrategy strategy in _strategies)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                Logger.Setup($"Trying connectivity strategy: {strategy.Name}");
                bool success = await strategy.TryEstablishAsync(ct);
                if (success)
                {
                    _activeStrategy = strategy;
                    ConnectivityState newState = strategy.Type switch
                    {
                        ConnectivityType.PortForward => ConnectivityState.DirectAccess,
                        ConnectivityType.StunHolePunch => ConnectivityState.HolePunched,
                        ConnectivityType.CloudflareTunnel => ConnectivityState.Tunneled,
                        _ => ConnectivityState.DirectAccess
                    };
                    SetState(newState);
                    Logger.Setup($"Connectivity established via {strategy.Name}");
                    return;
                }

                Logger.Setup($"Strategy {strategy.Name} did not succeed, trying next...", LogEventLevel.Debug);
            }
            catch (Exception ex)
            {
                Logger.Setup($"Strategy {strategy.Name} failed: {ex.Message}", LogEventLevel.Warning);
            }
        }

        SetState(ConnectivityState.LocalOnly);
        Logger.Setup("No remote connectivity strategy succeeded — server is local-only", LogEventLevel.Warning);
    }

    private void SetState(ConnectivityState state)
    {
        if (CurrentState == state) return;
        CurrentState = state;
        StateChanged?.Invoke(state);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null) return;

        try
        {
            await _stoppingCts.CancelAsync();
        }
        finally
        {
            await Task.WhenAny(_executingTask, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
        }

        if (_activeStrategy is not null)
        {
            await _activeStrategy.TeardownAsync();
        }
    }

    public void Dispose()
    {
        _stoppingCts.Cancel();
        _stoppingCts.Dispose();
        GC.SuppressFinalize(this);
    }
}
