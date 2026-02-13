using Newtonsoft.Json;
using NoMercy.Networking;

namespace NoMercy.Tray.Services;

public sealed class ServerConnection : IDisposable
{
    private IpcClient? _client;

    public bool IsConnected { get; private set; }

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
