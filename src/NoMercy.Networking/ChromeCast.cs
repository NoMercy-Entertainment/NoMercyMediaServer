using System.Text.Json.Serialization;
using NoMercy.Events;
using NoMercy.Events.Cast;
using NoMercy.NmSystem.SystemCalls;
using Sharpcaster;
using Sharpcaster.Models;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;

namespace NoMercy.Networking;

public class ChromeCast
{
    private static readonly ChromecastLocator Locator = new();
    private static IEnumerable<ChromecastReceiver> _chromecastReceivers = new List<ChromecastReceiver>();
    private static ChromecastClient? _client;

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

    public static async Task SelectChromecast(string name)
    {
        ChromecastReceiver? receiver = _chromecastReceivers.FirstOrDefault(x => x.Name == name);

        if (receiver == null)
        {
            Logger.Ping("Chromecast not found");
            return;
        }

        await SelectChromecast(receiver);
    }

    public static async Task SelectChromecast(ChromecastReceiver? receiver)
    {
        if (receiver == null)
        {
            Logger.Ping("Chromecast not found");
            return;
        }

        Logger.Ping("Connecting to chromecast");

        _client = new();

        _client.MediaChannel.StatusChanged += (sender, args) =>
        {
            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(new CastDeviceStatusChangedEvent
                {
                    EventType = "StatusChanged",
                    StatusData = new Dictionary<string, object?> { { "sender", sender }, { "args", args } }
                });
        };

        _client.ReceiverChannel.ReceiverStatusChanged += (sender, args) =>
        {
            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(new CastDeviceStatusChangedEvent
                {
                    EventType = "ReceiverStatusChanged",
                    StatusData = new Dictionary<string, object?> { { "sender", sender }, { "args", args } }
                });
        };

        _client.ReceiverChannel.LaunchStatusChanged += (sender, args) =>
        {
            if (EventBusProvider.IsConfigured)
                _ = EventBusProvider.Current.PublishAsync(new CastDeviceStatusChangedEvent
                {
                    EventType = "LaunchStatusChanged",
                    StatusData = new Dictionary<string, object?> { { "sender", sender }, { "args", args } }
                });
        };

        await _client.ConnectChromecast(receiver);
    }

    public static async Task Disconnect()
    {
        if (_client is null) return;

        await _client.DisconnectAsync();
    }

    public static async Task Stop()
    {
        if (_client is null) return;

        await _client.MediaChannel.StopAsync();
    }

    public static async Task Launch()
    {
        if (_client is null) return;
        Logger.Ping("Launching chromecast");
        //
        // if (!_client.GetChromecastStatus().IsStandBy)
        // {
        _ = await _client.LaunchApplicationAsync("925B4C3C");
        // }
    }

    public class CastCustomData
    {
        [JsonPropertyName("accessToken")] public string? AccessToken { get; set; }
        [JsonPropertyName("basePath")] public string? BasePath { get; set; }
        [JsonPropertyName("playlist")] public string? Playlist { get; set; }
        [JsonPropertyName("deepLink")] public string? DeepLink { get; set; }
    }
    
    public static async Task CastPlaylist(string value)
    {
        if (_client is null) return;
        Logger.Ping("Casting playlist: " + value);

        CastCustomData customData = new()
        {
            AccessToken = Globals.Globals.AccessToken,
            BasePath = Networking.ExternalAddress,
            Playlist = $"{Networking.ExternalAddress}/api/v1/{value}/watch",
            DeepLink = $"tv.nomercy.app://{value}/watch"
        };
        
        string jsonElement = System.Text.Json.JsonSerializer.Serialize(customData);

        Media media = new()
        {
            CustomData = jsonElement
        };

        await _client.MediaChannel.LoadAsync(media).ConfigureAwait(false);
    }

    public static ChromecastStatus? GetChromecastStatus()
    {
        return _client?.ChromecastStatus;
    }

    public static MediaStatus? GetMediaStatus()
    {
        return _client?.MediaChannel.MediaStatus;
    }
}