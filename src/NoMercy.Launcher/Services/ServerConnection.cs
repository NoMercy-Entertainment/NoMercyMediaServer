using System.Text;
using Newtonsoft.Json;
using NoMercy.Networking;
using NoMercy.Launcher.Models;

namespace NoMercy.Launcher.Services;

public sealed class ServerConnection : IDisposable
{
    private IpcClient? _client;

    public bool IsConnected { get; internal set; }

    public async Task<bool> ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            Disconnect();
            _client = new();

            using HttpResponseMessage response =
                await _client.GetAsync("/manage/status", cancellationToken);

            IsConnected = response.IsSuccessStatusCode;
            return IsConnected;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }

    public async Task<T?> GetAsync<T>(
        string path,
        CancellationToken cancellationToken = default) where T : class
    {
        if (_client is null) return null;

        try
        {
            using HttpResponseMessage response =
                await _client.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode) return null;

            string json = await response.Content
                .ReadAsStringAsync(cancellationToken);

            return JsonConvert.DeserializeObject<T>(json);
        }
        catch
        {
            IsConnected = false;
            return null;
        }
    }

    public async Task<bool> PostAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_client is null) return false;

        try
        {
            using HttpResponseMessage response = await _client.PostAsync(
                path, null, cancellationToken);

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
        CancellationToken cancellationToken = default)
    {
        if (_client is null) return (false, null);

        try
        {
            using HttpResponseMessage response = await _client.PostAsync(
                path, null, cancellationToken);

            string body = await response.Content.ReadAsStringAsync(cancellationToken);

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
        CancellationToken cancellationToken = default)
    {
        if (_client is null) return false;

        try
        {
            string json = JsonConvert.SerializeObject(body);
            using StringContent content = new(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client.PostAsync(
                path, content, cancellationToken);

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
        CancellationToken cancellationToken = default)
    {
        if (_client is null) return false;

        try
        {
            string json = JsonConvert.SerializeObject(body);
            using StringContent content = new(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client.PutAsync(
                path, content, cancellationToken);

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
        Action? onDisconnected = null)
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
                    await ConnectAsync(cancellationToken);

                if (!IsConnected)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = Math.Min(retryDelay * 2, MaxRetryDelay);
                    continue;
                }

                // Use a dedicated IPC client for the long-lived stream
                // so it doesn't interfere with (or get disposed by) the
                // shared _client used for status polling / other requests.
                streamClient = new IpcClient();
                using HttpResponseMessage response = await streamClient.GetStreamAsync(
                    "/manage/logs/stream", cancellationToken);
                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using StreamReader reader = new(stream);

                retryDelay = 1000;
                onConnected?.Invoke();

                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(cancellationToken);

                    if (line is null)
                    {
                        // Server closed the stream (e.g. restart)
                        IsConnected = false;
                        onDisconnected?.Invoke();
                        break;
                    }

                    if (!line.StartsWith("data: ")) continue;

                    string json = line[6..];
                    LogEntryResponse? entry = JsonConvert.DeserializeObject<LogEntryResponse>(json);
                    if (entry is not null)
                        onEntry(entry);
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

            if (cancellationToken.IsCancellationRequested) break;

            await Task.Delay(retryDelay, cancellationToken);
            retryDelay = Math.Min(retryDelay * 2, MaxRetryDelay);
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
