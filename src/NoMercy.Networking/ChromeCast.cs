using Microsoft.Extensions.Logging;
using NoMercy.NmSystem;
using Sharpcaster;
using Sharpcaster.Interfaces;
using Sharpcaster.Models;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;
using Media = Sharpcaster.Models.Media.Media;

namespace NoMercy.Networking;

public class ChromeCast
{
    private static readonly IChromecastLocator Locator = new MdnsChromecastLocator();
    private static readonly CancellationTokenSource Source = new(TimeSpan.FromMilliseconds(1500));
    private static IEnumerable<ChromecastReceiver> _chromecastReceivers = new List<ChromecastReceiver>();
    private static ChromecastClient? _client;

    public static async Task Init()
    {
        _chromecastReceivers = (await Locator.FindReceiversAsync(Source.Token)).ToList();

        foreach (ChromecastReceiver chromecast in _chromecastReceivers)
        {
            Logger.Ping(chromecast.Name);
        }
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
            Networking.SendToAll("StatusChanged", "castHub", new Dictionary<string, object?>{
                {"sender", sender},
                {"args", args},
            });
        };
        
        _client.ReceiverChannel.ReceiverStatusChanged += (sender, args) =>
        {
            Networking.SendToAll("ReceiverStatusChanged", "castHub", new Dictionary<string, object?>{
                {"sender", sender},
                {"args", args},
            });
        };
        
        _client.ReceiverChannel.LaunchStatusChanged += (sender, args) =>
        {
            Networking.SendToAll("LaunchStatusChanged", "castHub", new Dictionary<string, object?>{
                {"sender", sender},
                {"args", args},
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

    public static async Task CastPlaylist(string value)
    {
        if (_client is null) return;
        Logger.Ping("Casting playlist: " + value);
        
        Media media = new()
        {
            CustomData = new Dictionary<string, string>
            {
                {"accessToken", Auth.AccessToken!},
                {"basePath", Networking.ExternalAddress},
                {"playlist", $"{Networking.ExternalAddress}/api/v1/{value}/watch"}
            }
        };
        
        _ = await _client.MediaChannel.LoadAsync(media);
    }
    
    public static ChromecastStatus? GetChromecastStatus()
    {
        return _client?.GetChromecastStatus();
    }
    
    public static MediaStatus? GetMediaStatus()
    {
        return _client?.MediaChannel.MediaStatus;
    }
    
}