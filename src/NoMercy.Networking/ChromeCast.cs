using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using NoMercy.Events;
using NoMercy.Events.Cast;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Sharpcaster;
using Sharpcaster.Models;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;

namespace NoMercy.Networking;

public class ChromeCast
{
    public static INetworkDiscovery? NetworkDiscovery { get; set; }

    private static readonly ChromecastLocator Locator = new();
    private static IEnumerable<ChromecastReceiver> _chromecastReceivers =
        new List<ChromecastReceiver>();

    // Per-receiver client pool keyed by receiver name. Thread-safe.
    private static readonly ConcurrentDictionary<string, ChromecastClient> ClientPool = new(
        StringComparer.OrdinalIgnoreCase
    );

    // Tracks which receiver was most recently selected (for compat callers that
    // call SelectChromecast then Launch/CastPlaylist without passing a name).
    [ThreadStatic]
    private static string? _lastSelectedName;

    public static async Task Init()
    {
        _chromecastReceivers = (await Locator.FindReceiversAsync()).ToList();

        foreach (ChromecastReceiver chromecast in _chromecastReceivers)
            Logger.Ping($"Found chromecast: {chromecast.Name}");
    }

    public static string[] GetChromeCasts()
    {
        return _chromecastReceivers.Select(x => x.Name).ToArray();
    }

    // --- Pool helpers ---

    private static ChromecastClient BuildClient(string receiverName)
    {
        ChromecastClient client = new();

        client.MediaChannel.StatusChanged += (sender, args) =>
        {
            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(
                    new CastDeviceStatusChangedEvent
                    {
                        EventType = "StatusChanged",
                        StatusData = new Dictionary<string, object?>
                        {
                            { "sender", sender },
                            { "args", args },
                            { "receiverName", receiverName },
                        },
                    }
                );
        };

        client.ReceiverChannel.ReceiverStatusChanged += (sender, args) =>
        {
            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(
                    new CastDeviceStatusChangedEvent
                    {
                        EventType = "ReceiverStatusChanged",
                        StatusData = new Dictionary<string, object?>
                        {
                            { "sender", sender },
                            { "args", args },
                            { "receiverName", receiverName },
                        },
                    }
                );
        };

        client.ReceiverChannel.LaunchStatusChanged += (sender, args) =>
        {
            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(
                    new CastDeviceStatusChangedEvent
                    {
                        EventType = "LaunchStatusChanged",
                        StatusData = new Dictionary<string, object?>
                        {
                            { "sender", sender },
                            { "args", args },
                            { "receiverName", receiverName },
                        },
                    }
                );
        };

        return client;
    }

    private static async Task<ChromecastClient?> GetOrCreateClientAsync(string name)
    {
        ChromecastReceiver? receiver = _chromecastReceivers.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
        );

        if (receiver == null)
        {
            Logger.Ping($"Chromecast not found: {name}");
            return null;
        }

        // Lazy-create and connect only when a new entry is added to the pool.
        // If an existing client is already in the pool we reuse it as-is; the
        // caller may reconnect explicitly via SelectChromecast if needed.
        if (ClientPool.TryGetValue(name, out ChromecastClient? existing))
            return existing;

        ChromecastClient newClient = BuildClient(name);
        Logger.Ping($"Connecting to chromecast: {name}");
        await newClient.ConnectChromecast(receiver);

        // Another thread may have won the race; prefer theirs and dispose ours.
        if (ClientPool.TryAdd(name, newClient))
            return newClient;

        try
        {
            await newClient.DisconnectAsync();
        }
        catch
        {
            // best-effort dispose of the losing client
        }

        return ClientPool[name];
    }

    // --- Public API (name-explicit overloads) ---

    /// <summary>
    /// Connects to (or reuses an existing connection to) the named receiver and
    /// makes it the "current" receiver for subsequent parameterless calls on
    /// this thread.
    /// </summary>
    public static async Task SelectChromecast(string name)
    {
        ChromecastClient? client = await GetOrCreateClientAsync(name);
        if (client is not null)
            _lastSelectedName = name;
    }

    public static async Task SelectChromecast(ChromecastReceiver? receiver)
    {
        if (receiver == null)
        {
            Logger.Ping("Chromecast not found");
            return;
        }

        await SelectChromecast(receiver.Name);
    }

    /// <summary>
    /// Launches the NoMercy cast application on the named receiver. If
    /// <paramref name="name"/> is null the last receiver selected on this
    /// thread is used.
    /// </summary>
    public static async Task Launch(string? name = null)
    {
        string? target = name ?? _lastSelectedName;
        if (target == null)
            return;

        if (!ClientPool.TryGetValue(target, out ChromecastClient? client))
            return;

        Logger.Ping($"Launching chromecast: {target}");
        _ = await client.LaunchApplicationAsync("925B4C3C");
    }

    /// <summary>
    /// Casts a playlist to the named receiver. If <paramref name="name"/> is
    /// null the last receiver selected on this thread is used.
    /// </summary>
    public static async Task CastPlaylist(string value, string? name = null)
    {
        string? target = name ?? _lastSelectedName;
        if (target == null)
            return;

        if (!ClientPool.TryGetValue(target, out ChromecastClient? client))
            return;

        Logger.Ping($"Casting playlist to {target}: {value}");

        string externalAddress = (NetworkDiscovery?.ExternalAddress).OrEmpty();
        string? token = Globals.Globals.AccessToken;

        CastCustomData customData = new()
        {
            AccessToken = token,
            BasePath = externalAddress,
            Playlist = $"{externalAddress}/api/v1/{value}/watch",
            DeepLink = $"tv.nomercy.app://{value}/watch",
        };

        string jsonElement = System.Text.Json.JsonSerializer.Serialize(customData);
        Media media = new() { CustomData = jsonElement };

        await client.MediaChannel.LoadAsync(media).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the status for the named receiver, or the last selected one if
    /// <paramref name="name"/> is null.
    /// </summary>
    public static ChromecastStatus? GetChromecastStatus(string? name = null)
    {
        string? target = name ?? _lastSelectedName;
        if (target == null)
            return null;

        return ClientPool.TryGetValue(target, out ChromecastClient? client)
            ? client.ChromecastStatus
            : null;
    }

    /// <summary>
    /// Returns the media status for the named receiver, or the last selected
    /// one if <paramref name="name"/> is null.
    /// </summary>
    public static MediaStatus? GetMediaStatus(string? name = null)
    {
        string? target = name ?? _lastSelectedName;
        if (target == null)
            return null;

        return ClientPool.TryGetValue(target, out ChromecastClient? client)
            ? client.MediaChannel.MediaStatus
            : null;
    }

    /// <summary>
    /// Stops media on the named receiver, or the last selected one if
    /// <paramref name="name"/> is null.
    /// </summary>
    public static async Task Stop(string? name = null)
    {
        string? target = name ?? _lastSelectedName;
        if (target == null)
            return;

        if (!ClientPool.TryGetValue(target, out ChromecastClient? client))
            return;

        await client.MediaChannel.StopAsync();
    }

    /// <summary>
    /// Disconnects and removes the client for the named receiver from the
    /// pool. If <paramref name="name"/> is null the last selected receiver is
    /// used. Pass "*" to disconnect all receivers.
    /// </summary>
    public static async Task Disconnect(string? name = null)
    {
        if (name == "*")
        {
            await DisconnectAllAsync();
            return;
        }

        string? target = name ?? _lastSelectedName;
        if (target == null)
            return;

        if (!ClientPool.TryRemove(target, out ChromecastClient? client))
            return;

        try
        {
            await client.DisconnectAsync();
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>
    /// Disconnects and disposes every client in the pool. Called on
    /// application shutdown.
    /// </summary>
    public static async Task DisconnectAllAsync()
    {
        string[] keys = ClientPool.Keys.ToArray();
        foreach (string key in keys)
        {
            if (!ClientPool.TryRemove(key, out ChromecastClient? client))
                continue;

            try
            {
                await client.DisconnectAsync();
            }
            catch
            {
                // best-effort
            }
        }
    }

    public class CastCustomData
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("basePath")]
        public string? BasePath { get; set; }

        [JsonPropertyName("playlist")]
        public string? Playlist { get; set; }

        [JsonPropertyName("deepLink")]
        public string? DeepLink { get; set; }
    }
}
