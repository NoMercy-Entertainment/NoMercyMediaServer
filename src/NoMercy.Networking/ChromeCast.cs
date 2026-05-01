using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using NoMercy.Events;
using NoMercy.Events.Cast;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using Sharpcaster;
using Sharpcaster.Models;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

    /// <summary>
    /// Looks up a discovered Chromecast receiver by its LAN IP address. Useful
    /// when the caller knows the device IP (from the Devices table) but not the
    /// receiver's mDNS broadcast name (which the user sets independently in
    /// Android TV settings, e.g. "Tv in woonkamer" vs the NoMercy custom name
    /// "Woonkamer TV"). Re-discovers via mDNS once when the cache misses
    /// because Init() runs at server start and stale caches are common — the
    /// TV may have come online after that boot phase.
    /// </summary>
    public static async Task<string?> FindReceiverNameByIpAsync(string ip)
    {
        if (string.IsNullOrEmpty(ip))
            return null;

        string? hit = LookupNameByIp(ip);
        if (hit is not null)
            return hit;

        // Cache miss — refresh and try again. Discovery takes 2-3s; this only
        // runs when the LAN topology changed since Init().
        Logger.Ping($"Chromecast cache miss for {ip} — refreshing mDNS");
        try
        {
            _chromecastReceivers = (await Locator.FindReceiversAsync()).ToList();
            foreach (ChromecastReceiver chromecast in _chromecastReceivers)
                Logger.Ping(
                    $"Discovered chromecast: {chromecast.Name} @ {chromecast.DeviceUri?.Host}"
                );
        }
        catch (Exception ex)
        {
            Logger.Ping($"Chromecast re-discovery failed: {ex.Message}");
        }

        string? cached = LookupNameByIp(ip);
        if (cached is not null)
            return cached;

        // mDNS still empty — Windows firewall / Zeroconf flakiness commonly
        // blocks the multicast scan even when the device is reachable on the
        // same /24. Synthesize a receiver record from the IP and the default
        // Cast control port so SelectChromecast can still try a direct TCP
        // connect. The phone-to-TV cast already proved IP reachability; only
        // the discovery layer is broken.
        ChromecastReceiver synthetic = new()
        {
            Name = ip,
            DeviceUri = new Uri($"https://{ip}"),
            Port = 8009,
            Model = "Chromecast",
            Version = "0",
            Status = string.Empty,
            ExtraInformation = new Dictionary<string, string>(),
        };
        List<ChromecastReceiver> merged = new(_chromecastReceivers) { synthetic };
        _chromecastReceivers = merged;
        Logger.Ping(
            $"Synthesized Chromecast receiver for {ip}:8009 — mDNS unavailable, will attempt direct connect"
        );
        return ip;
    }

    private static string? LookupNameByIp(string ip)
    {
        foreach (ChromecastReceiver receiver in _chromecastReceivers)
        {
            if (
                receiver.DeviceUri is not null
                && string.Equals(receiver.DeviceUri.Host, ip, StringComparison.OrdinalIgnoreCase)
            )
                return receiver.Name;
        }

        return null;
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

    private static int _androidLaunchRequestId = new Random().Next();

    /// <summary>
    /// Launches the NoMercy cast application as an Android TV receiver. Sends
    /// a LAUNCH protocol message with androidReceiverCompatible=true so cast_shell
    /// foregrounds the native APK (tv.nomercy.app) instead of falling back to
    /// the Web Receiver placeholder. Sharpcaster's built-in LaunchApplicationAsync
    /// doesn't expose launchOptions, so we craft and send the JSON ourselves
    /// against the public ChromecastClient.SendAsync surface.
    ///
    /// When <paramref name="customData"/> is supplied (cast-receiver-leanback
    /// flow), the object is serialized into the LAUNCH payload's customData
    /// field. Both the APK (via Cast Connect intent extras) and the Web
    /// Receiver (via cast.framework.system.LaunchRequestEvent.data) receive
    /// it. The APK ignores auth fields (it has its own persistent Keycloak
    /// session); the Web Receiver consumes them to bootstrap volatile auth.
    /// </summary>
    public static async Task LaunchAndroidReceiver(
        string? name = null,
        object? customData = null,
        bool useAndroidReceiver = true
    )
    {
        string? target = name ?? _lastSelectedName;
        if (target == null)
            return;

        if (!ClientPool.TryGetValue(target, out ChromecastClient? client))
            return;

        // Force a round-trip GET_STATUS before LAUNCH. Sharpcaster's
        // ConnectChromecast fires CONNECT and returns; the actual processing
        // on cast_shell side races with our subsequent LAUNCH. If LAUNCH
        // lands before cast_shell finished wiring the session, cast_shell
        // accepts it but skips the CEC OneTouchPlay step (gated on full
        // session handshake). GetChromecastStatusAsync awaits a
        // RECEIVER_STATUS reply, which can only arrive after CONNECT is
        // processed end-to-end.
        try
        {
            await client.ReceiverChannel.GetChromecastStatusAsync();
        }
        catch (Exception ex)
        {
            Logger.Ping(
                $"LaunchAndroidReceiver pre-LAUNCH GET_STATUS failed for {target}: {ex.Message}"
            );
        }

        int requestId = Interlocked.Increment(ref _androidLaunchRequestId);
        string json = BuildLaunchJson(requestId, customData, useAndroidReceiver);

        string flavor = useAndroidReceiver ? "androidReceiverCompatible" : "webReceiverOnly";
        Logger.Ping(
            $"Launching cast-tv ({flavor}{(customData is null ? "" : ", customData")}) on {target}"
        );

        // Watch for cast_shell's reply on this request id. RECEIVER_STATUS
        // arrives back on the receiver channel when cast_shell accepts the
        // LAUNCH — if we don't see ChromecastStatus.Application.AppId match
        // ours within 1.5s, retry once. Single retry is enough; the failure
        // mode is a timing race during the first cold connect, not a hard
        // protocol break.
        bool launchAccepted = false;
        EventHandler<ChromecastStatus>? watcher = null;
        watcher = (sender, status) =>
        {
            if (
                status?.Application is not null
                && string.Equals(
                    status.Application.AppId,
                    "925B4C3C",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                launchAccepted = true;
        };
        client.ReceiverChannel.ReceiverStatusChanged += watcher;

        try
        {
            await SendLaunchAsync(client, requestId, json);

            // Wait for the RECEIVER_STATUS broadcast confirming the app was
            // launched. 5s covers cast_shell's handshake + resource allocation
            // window after mDNS fallback to direct-connect on port 8009.
            int waited = 0;
            while (!launchAccepted && waited < 5000)
            {
                await Task.Delay(100);
                waited += 100;
            }

            if (!launchAccepted)
            {
                Logger.Ping(
                    $"LaunchAndroidReceiver: no LAUNCH ack from {target} within 5s — retrying once"
                );
                int retryRequestId = Interlocked.Increment(ref _androidLaunchRequestId);
                string retryJson = BuildLaunchJson(retryRequestId, customData, useAndroidReceiver);
                await SendLaunchAsync(client, retryRequestId, retryJson);
            }
        }
        catch (Exception ex)
        {
            Logger.Ping($"LaunchAndroidReceiver failed for {target}: {ex.Message}");
        }
        finally
        {
            client.ReceiverChannel.ReceiverStatusChanged -= watcher;
        }
    }

    private static string BuildLaunchJson(
        int requestId,
        object? customData,
        bool useAndroidReceiver
    )
    {
        // Newtonsoft.Json honours [JsonProperty("snake_case")] on
        // LaunchCustomData so the receiver sees access_token / refresh_token /
        // user_id. System.Text.Json ignores those attributes and silently
        // drops the receiver's auth hydration.
        //
        // useAndroidReceiver controls whether cast_shell tries to dispatch to
        // the registered Cast Connect APK first. When true, supportedAppTypes
        // and launchOptions.androidReceiverCompatible nudge cast_shell toward
        // the APK path; if no APK is installed cast_shell falls back to the
        // Web Receiver — and the fallback drops customData on the floor. For
        // Web-Receiver-only TVs (no APK), call with false so cast_shell goes
        // straight to the Web Receiver and customData survives.
        if (customData is null)
        {
            object payload = useAndroidReceiver
                ? new
                {
                    type = "LAUNCH",
                    requestId,
                    appId = "925B4C3C",
                    language = "en-US",
                    supportedAppTypes = new[] { "ANDROID_TV" },
                    launchOptions = new { androidReceiverCompatible = true },
                }
                : new
                {
                    type = "LAUNCH",
                    requestId,
                    appId = "925B4C3C",
                    language = "en-US",
                    supportedAppTypes = new[] { "WEB" },
                };
            return JsonConvert.SerializeObject(payload);
        }

        object payloadWithCustomData = useAndroidReceiver
            ? new
            {
                type = "LAUNCH",
                requestId,
                appId = "925B4C3C",
                language = "en-US",
                supportedAppTypes = new[] { "ANDROID_TV" },
                launchOptions = new { androidReceiverCompatible = true },
                customData,
            }
            : new
            {
                type = "LAUNCH",
                requestId,
                appId = "925B4C3C",
                language = "en-US",
                supportedAppTypes = new[] { "WEB" },
                customData,
            };
        return JsonConvert.SerializeObject(payloadWithCustomData);
    }

    private static async Task SendLaunchAsync(ChromecastClient client, int requestId, string json)
    {
        await client.SendAsync(
            logger: null,
            ns: "urn:x-cast:com.google.cast.receiver",
            messageRequestId: requestId,
            messagePayload: json,
            destinationId: "receiver-0"
        );
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

        string jsonElement = JsonSerializer.Serialize(customData);
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
