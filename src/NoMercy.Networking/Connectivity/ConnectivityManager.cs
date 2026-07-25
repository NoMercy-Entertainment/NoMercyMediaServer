// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Status;

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

    private readonly IAuthTokenStore _authTokenStore;
    private readonly IBootStatus _bootStatus;

    private readonly ILogger<ConnectivityManager> _logger;

    public ConnectivityManager(
        ILogger<ConnectivityManager> logger,
        IAuthTokenStore authTokenStore,
        INetworkDiscovery networkDiscovery,
        IEnumerable<IConnectivityStrategy> strategies,
        IBootStatus bootStatus
    )
    {
        _logger = logger;
        _authTokenStore = authTokenStore;
        _networkDiscovery = networkDiscovery;
        _strategies = strategies.OrderBy(s => s.Priority);
        _bootStatus = bootStatus;
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
            while (!_bootStatus.IsStarted && !cancellationToken.IsCancellationRequested)
                await Task.Delay(1000, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            if (_authTokenStore.AccessToken is null)
            {
                _logger.LogDebug("ConnectivityManager waiting for authentication...");
                int maxWait = 30;
                while (
                    _authTokenStore.AccessToken is null
                    && maxWait-- > 0
                    && !cancellationToken.IsCancellationRequested
                )
                    await Task.Delay(1000, cancellationToken);

                if (_authTokenStore.AccessToken is null)
                {
                    _logger.LogDebug("ConnectivityManager skipped — no authentication available");
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
            _logger.LogWarning("Error in ConnectivityManager: {Message}", ex.Message);
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
            if (ct.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("Trying connectivity strategy: {Name}", strategy.Name);
                bool success = await strategy.TryEstablishAsync(ct);
                if (success)
                {
                    _activeStrategy = strategy;
                    ConnectivityState newState = strategy.Type switch
                    {
                        ConnectivityType.PortForward => ConnectivityState.DirectAccess,
                        ConnectivityType.StunHolePunch => ConnectivityState.HolePunched,
                        ConnectivityType.CloudflareTunnel => ConnectivityState.Tunneled,
                        _ => ConnectivityState.DirectAccess,
                    };
                    SetState(newState);
                    _logger.LogInformation("Connectivity established via {Name}", strategy.Name);
                    return;
                }

                _logger.LogDebug("Strategy {Name} did not succeed, trying next...", strategy.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Strategy {Name} failed: {Message}", [strategy.Name, ex.Message]);
            }
        }

        SetState(ConnectivityState.LocalOnly);
        _logger.LogWarning("No remote connectivity strategy succeeded — server is local-only");
    }

    private void SetState(ConnectivityState state)
    {
        if (CurrentState == state)
            return;
        CurrentState = state;
        StateChanged?.Invoke(state);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executingTask == null)
            return;

        try
        {
            await _stoppingCts.CancelAsync();
        }
        finally
        {
            await Task.WhenAny(
                _executingTask,
                Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)
            );
        }

        if (_activeStrategy is not null)
        {
            await _activeStrategy.TeardownAsync();
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_stoppingCts.IsCancellationRequested)
                _stoppingCts.Cancel();
        }
        catch (ObjectDisposedException) { }

        try
        {
            _stoppingCts.Dispose();
        }
        catch (ObjectDisposedException) { }

        GC.SuppressFinalize(this);
    }
}
