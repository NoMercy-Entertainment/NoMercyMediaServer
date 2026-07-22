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

using System.Text;
using Newtonsoft.Json;
using NoMercy.Launcher.Models;
using NoMercy.Networking.Discovery;

namespace NoMercy.Launcher.Services;

public sealed class ServerConnection : IDisposable
{
    private IpcClient? _client;
    private readonly string? _pipeNameOrSocketPath;

    /// <param name="pipeNameOrSocketPath">
    /// Overrides the management pipe/socket the connection targets. Null means
    /// the machine-global default — tests pass a unique name so a dev server
    /// running on the same machine can never answer for them.
    /// </param>
    public ServerConnection(string? pipeNameOrSocketPath = null)
    {
        _pipeNameOrSocketPath = pipeNameOrSocketPath;
    }

    public bool IsConnected { get; internal set; }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Disconnect();
            _client = new(pipeNameOrSocketPath: _pipeNameOrSocketPath);

            using HttpResponseMessage response = await _client.GetAsync(
                requestUri: "/manage/status",
                cancellationToken: cancellationToken
            );

            IsConnected = response.IsSuccessStatusCode;
            return IsConnected;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_client is null)
            return null;

        try
        {
            using HttpResponseMessage response = await _client.GetAsync(requestUri: path, cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            string json = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

            return JsonConvert.DeserializeObject<T>(value: json);
        }
        catch
        {
            IsConnected = false;
            return null;
        }
    }

    public async Task<bool> PostAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return false;

        try
        {
            using HttpResponseMessage response = await _client.PostAsync(
                requestUri: path,
                content: null,
                cancellationToken: cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public async Task<(bool Success, string? Body)> PostWithBodyAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        if (_client is null)
            return (false, null);

        try
        {
            using HttpResponseMessage response = await _client.PostAsync(
                requestUri: path,
                content: null,
                cancellationToken: cancellationToken
            );

            string body = await response.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

            return (response.IsSuccessStatusCode, body);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            return (false, ex.Message);
        }
    }

    public async Task<bool> PostAsync<T>(
        string path,
        T body,
        CancellationToken cancellationToken = default
    )
    {
        if (_client is null)
            return false;

        try
        {
            string json = JsonConvert.SerializeObject(value: body);
            using StringContent content = new(content: json, encoding: Encoding.UTF8, mediaType: "application/json");
            using HttpResponseMessage response = await _client.PostAsync(
                requestUri: path,
                content: content,
                cancellationToken: cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public async Task<bool> PutAsync<T>(
        string path,
        T body,
        CancellationToken cancellationToken = default
    )
    {
        if (_client is null)
            return false;

        try
        {
            string json = JsonConvert.SerializeObject(value: body);
            using StringContent content = new(content: json, encoding: Encoding.UTF8, mediaType: "application/json");
            using HttpResponseMessage response = await _client.PutAsync(
                requestUri: path,
                content: content,
                cancellationToken: cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public async Task StreamLogsAsync(
        Action<LogEntryResponse> onEntry,
        CancellationToken cancellationToken,
        Action? onConnected = null,
        Action? onDisconnected = null
    )
    {
        int retryDelay = 1000;
        const int MaxRetryDelay = 30000;

        while (!cancellationToken.IsCancellationRequested)
        {
            IpcClient? streamClient = null;
            try
            {
                // Ensure server is reachable via the shared client
                if (!IsConnected)
                    await ConnectAsync(cancellationToken: cancellationToken);

                if (!IsConnected)
                {
                    await Task.Delay(millisecondsDelay: retryDelay, cancellationToken: cancellationToken);
                    retryDelay = Math.Min(val1: retryDelay * 2, val2: MaxRetryDelay);
                    continue;
                }

                // Use a dedicated IPC client for the long-lived stream
                // so it doesn't interfere with (or get disposed by) the
                // shared _client used for status polling / other requests.
                streamClient = new();
                using HttpResponseMessage response = await streamClient.GetStreamAsync(
                    requestUri: "/manage/logs/stream",
                    cancellationToken: cancellationToken
                );
                await using Stream stream = await response.Content.ReadAsStreamAsync(
                    cancellationToken: cancellationToken
                );
                using StreamReader reader = new(stream: stream);

                retryDelay = 1000;
                onConnected?.Invoke();

                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(cancellationToken: cancellationToken);

                    if (line is null)
                    {
                        // Server closed the stream (e.g. restart)
                        IsConnected = false;
                        onDisconnected?.Invoke();
                        break;
                    }

                    if (!line.StartsWith(value: "data: "))
                        continue;

                    string json = line[6..];
                    LogEntryResponse? entry = JsonConvert.DeserializeObject<LogEntryResponse>(value: json);
                    if (entry is not null)
                        onEntry(obj: entry);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                IsConnected = false;
                onDisconnected?.Invoke();
            }
            finally
            {
                streamClient?.Dispose();
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            await Task.Delay(millisecondsDelay: retryDelay, cancellationToken: cancellationToken);
            retryDelay = Math.Min(val1: retryDelay * 2, val2: MaxRetryDelay);
        }
    }

    private void Disconnect()
    {
        _client?.Dispose();
        _client = null;
        IsConnected = false;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
