using System.Drawing;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using ILogger = Serilog.ILogger;

namespace NoMercy.NmSystem;
public static class Logger
{
    private static Serilog.Core.Logger ConsoleLog { get; set; }
    private static LogEventLevel _maxLogLevel = LogEventLevel.Debug;
    private const string ConsoleTemplate = "{Time} {ConsoleType} | {@Message}{NewLine}{Exception}";

    private static LoggerConfiguration DefaultEnrich(this LoggerConfiguration lc)
    {
        return lc
            .Enrich.FromLogContext()
            .Enrich.With<WithThreadId>();
    }

    private static LoggerConfiguration SinkFile(this LoggerConfiguration lc, string filePath)
    {
        // add log to static list
        return lc
            .Enrich.With<FileTypeEnricher>()
            .Enrich.With<FileTimestampEnricher>()
            .Enrich.With<FileMessageEnricher>()
            .WriteTo.File(
                new CompactJsonFormatter(),
                filePath,
                rollingInterval: RollingInterval.Day
            );
    }

    private static LoggerConfiguration SinkConsole(this LoggerConfiguration lc)
    {
        return lc
            .Enrich.With<ConsoleTimestampEnricher>()
            .Enrich.With<ConsoleTypeEnricher>()
            .WriteTo.Console(
                applyThemeToRedirectedOutput: true,
                outputTemplate: ConsoleTemplate
            );
    }

    static Logger()
    {
        ConsoleLog = CreateConsoleConfiguration()
            .CreateLogger();
    }

    private static LoggerConfiguration CreateConsoleConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .DefaultEnrich()
            .WriteTo.Logger(lc => lc
                .SinkFile(Path.Join(AppFiles.LogPath, "log.txt"))
            )
            .WriteTo.Logger(lc => lc
                .SinkConsole()
            );
    }

    public static void SetLogLevel(LogEventLevel level)
    {
        _maxLogLevel = level;
    }

    private static bool ShouldLog(LogEventLevel level)
    {
        return level >= _maxLogLevel;
    }

    public class LogType
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("display_name")] public string DisplayName { get; set; }
        [JsonProperty("color")] public string Color { get; set; }
        [JsonProperty("type")] public string Type { get; set; }

        public LogType(string name, string displayName, Color color, string type)
        {
            Name = name;
            DisplayName = displayName;
            Color = ToHexString(color);
            Type = type ?? "log";
        }
    }

    public static readonly List<LogType> LogTypes =
    [
        new LogType("_", "System", Color.DimGray, "spacer"),
        new LogType("app", "Access", Color.MediumPurple, "System"),
        new LogType("access", "App", Color.MediumPurple, "System"),
        new LogType("configuration", "Configuration", Color.MediumPurple, "System"),
        new LogType("auth", "Auth", Color.CornflowerBlue, "System"),
        new LogType("register", "Register", Color.CornflowerBlue, "System"),
        new LogType("setup", "Setup", Color.CornflowerBlue, "System"),
        new LogType("system", "System", Color.CornflowerBlue, "System"),
        new LogType("certificate", "Certificate", Color.CornflowerBlue, "System"),

        new LogType("_", "Workers", Color.DimGray, "spacer"),
        new LogType("queue", "Queue", Color.Chocolate, "Workers"),
        new LogType("encoder", "Encoder", Color.Chocolate, "Workers"),
        new LogType("ripper", "Ripper", Color.Chocolate, "Workers"),

        new LogType("_", "Networking", Color.DimGray, "spacer"),
        new LogType("http", "Http", Color.Orange, "Networking"),
        new LogType("notify", "Notify", Color.Orange, "Networking"),
        new LogType("ping", "Ping", Color.Orange, "Networking"),
        new LogType("socket", "Socket", Color.Orange, "Networking"),
        new LogType("request", "Request", Color.Orange, "Networking"),

        new LogType("_", "Providers", Color.DimGray, "spacer"),
        new LogType("acoustid", "AcoustID", Color.DodgerBlue, "Providers"),
        new LogType("anidb", "AniDB", Color.DodgerBlue, "Providers"),
        new LogType("audiodb", "AudioDB", Color.DodgerBlue, "Providers"),
        new LogType("coverart", "CoverArt", Color.DodgerBlue, "Providers"),
        new LogType("fanart", "Fanart", Color.DodgerBlue, "Providers"),
        new LogType("fingerprint", "Fingerprint", Color.DodgerBlue, "Providers"),
        new LogType("moviedb", "TheMovieDB", Color.DodgerBlue, "Providers"),
        new LogType("musicbrainz", "MusicBrainz", Color.DodgerBlue, "Providers"),
        new LogType("musixmatch", "MusixMatch", Color.DodgerBlue, "Providers"),
        new LogType("openSubs", "OpenSubs", Color.DodgerBlue, "Providers"),
        new LogType("tvdb", "TheTVDB", Color.DodgerBlue, "Providers"),
        new LogType("youtube", "YouTube", Color.DodgerBlue, "Providers"),

        new LogType("_", "Notifications", Color.DimGray, "spacer"),
        new LogType("discord", "Discord", Color.Green, "Notifications"),
        new LogType("twitter", "Twiitter", Color.Green, "Notifications"),
        new LogType("whatsapp", "Whatsapp", Color.Green, "Notifications"),
        new LogType("telegram", "Telegram", Color.Green, "Notifications"),
        new LogType("webhook", "Webhook", Color.Green, "Notifications")

        // new LogType("_", "Unused", Color.DimGray, "spacer"),
        // new LogType("qbittorrent", "QBitTorrent", Color.Olive, "Unused"),
        // new LogType("transmission", "Transmission", Color.Olive, "Unused"),
        // new LogType("rutorrent", "RuTorrent", Color.Olive, "Unused"),
        // new LogType("sabnzbd", "SabNzbd", Color.Olive, "Unused"),
        // new LogType("deluge", "Deluge" , Color.Olive, "Unused"),
        // new LogType("emby", "Emby" , Color.Olive, "Unused"),
        // new LogType("jackett", "Jackett" , Color.Olive, "Unused"),
        // new LogType("jellyfin", "Jellyfin" , Color.Olive, "Unused"),
        // new LogType("kodi", "Kodi" , Color.Olive, "Unused"),
        // new LogType("lidarr", "Lidarr" , Color.Olive, "Unused"),
        // new LogType("ombi", "Ombi" , Color.Olive, "Unused"),
        // new LogType("plex", "Plex" , Color.Olive, "Unused"),
        // new LogType("radarr", "Radarr" , Color.Olive, "Unused"),
        // new LogType("sickchill", "SickChill" , Color.Olive, "Unused"),
        // new LogType("sickgear", "SickGear" , Color.Olive, "Unused"),
        // new LogType("sonarr", "sonarr" , Color.Olive, "Unused"),
        // new LogType("usenet", "Usenet" , Color.Olive, "Unused"),
    ];

    private static readonly Dictionary<string, Color>? LogColors = new()
    {
        // { "system", Color.DimGray },
        { "server", Color.MediumPurple },
        { "app", Color.MediumPurple },
        { "access", Color.MediumPurple },
        { "configuration", Color.MediumPurple },
        { "auth", Color.CornflowerBlue },
        { "register", Color.CornflowerBlue },
        { "setup", Color.CornflowerBlue },
        { "system", Color.CornflowerBlue },
        { "certificate", Color.CornflowerBlue },

        { "queue", Color.Chocolate },
        { "encoder", Color.Chocolate },
        { "ripper", Color.Chocolate },

        { "http", Color.Orange },
        { "notify", Color.Orange },
        { "ping", Color.Orange },
        { "socket", Color.Orange },
        { "request", Color.Orange },

        { "fanart", Color.DodgerBlue },
        { "audiodb", Color.DodgerBlue },
        { "fingerprint", Color.DodgerBlue },
        { "moviedb", Color.DodgerBlue },
        { "tvdb", Color.DodgerBlue },
        { "anidb", Color.DodgerBlue },
        { "youtube", Color.DodgerBlue },
        { "openSubs", Color.DodgerBlue },
        { "musicbrainz", Color.DodgerBlue },
        { "acoustid", Color.DodgerBlue },
        { "coverart", Color.DodgerBlue },
        { "musixmatch", Color.DodgerBlue },

        // { "qbittorrent", Color.Olive },
        // { "transmission", Color.Olive },
        // { "rutorrent", Color.Olive },
        // { "sabnzbd", Color.Olive },

        { "discord", Color.Green },
        { "twitter", Color.Green },
        { "whatsapp", Color.Green },
        { "telegram", Color.Green },
        { "webhook", Color.Green },


        { "_", Color.White },
        { "__", Color.White },
        { "___", Color.White },
        { "____", Color.White },
        { "_____", Color.White },
        { "______", Color.White }


        // { "deluge", Color.White },
        // { "emby", Color.White },
        // { "jackett", Color.White },
        // { "jellyfin", Color.White },
        // { "kodi", Color.White },
        // { "lidarr", Color.White },
        // { "ombi", Color.White },
        // { "plex", Color.White },
        // { "radarr", Color.White },
        // { "sickchill", Color.White },
        // { "sickrage", Color.White },
        // { "sickgear", Color.White},
        // { "sonarr", Color.White },
        // { "tautulli", Color.White },
        // { "tivimate", Color.White },
        // { "trakt", Color.White },
        // { "tvmaze", Color.White },
        // { "usenet", Color.White },
        // { "xteve", Color.White },
    };

    public static readonly Dictionary<string, string> Capitalize = new()
    {
        { "_", "System" },
        { "server", "Server" },
        { "access", "Access" },
        { "app", "App" },
        { "configuration", "Configuration" },
        { "auth", "Auth" },
        { "register", "Register" },
        { "setup", "Setup" },
        { "system", "System" },
        { "certificate", "Certificate" },

        { "__", "Workers" },
        { "queue", "Queue" },
        { "encoder", "Encoder" },
        { "ripper", "Ripper" },

        { "___", "Networking" },
        { "http", "Http" },
        { "notify", "Notify" },
        { "ping", "Ping" },
        { "socket", "Socket" },
        { "request", "Request" },

        { "____", "Providers" },
        { "acoustid", "AcoustId" },
        { "anidb", "AniDb" },
        { "audiodb", "AudioDb" },
        { "coverart", "CoverArt" },
        { "fanart", "FanArt" },
        { "fingerprint", "Fingerprint" },
        { "moviedb", "MovieDb" },
        { "musicbrainz", "MusicBrainz" },
        { "musixmatch", "MusixMatch" },
        { "tvdb", "Tvdb" },

        // { "youtube", "Youtube" },
        // { "opensubs", "OpenSubs" },
        // { "qbittorrent", "QBitTorrent" },
        // { "transmission", "Transmission" },
        // { "ruTorrent", "RuTorrent" },
        // { "sabnzbd", "SabNzbd" },

        { "_____", "Notifications" },
        { "discord", "Discord" },
        { "twitter", "Twitter" },
        { "whatsapp", "Whatsapp" },
        { "telegram", "Telegram" },
        { "webhook", "Webhook" }

        // { "deluge", "Deluge" },
        // { "emby", "Emby" },
        // { "jackett", "Jackett" },
        // { "jellyfin", "Jellyfin" },
        // { "kodi", "Kodi" },
        // { "lidarr", "Lidarr" },
        // { "ombi", "Ombi" },
        // { "plex", "Plex" },
        // { "radarr", "Radarr" },
        // { "sickchill", "SickChill" },
        // { "sickgear", "SickGear" },
        // { "sickrage", "SickRage" },
        // { "sonarr", "sonarr" },
        // { "trakt", "Trakt" },
        // { "tvmaze", "TvMaze" },
        // { "usenet", "Usenet" },
        // { "xteve", "Xteve" }
    };

    internal static Color GetColor(string type)
    {
        return LogColors?[type] ?? Color.White;
    }

    public static T Log<T>(this T self, string type = "server") where T : class
    {
        return Log<T>(self, LogEventLevel.Debug, type);
    }

    private static T Log<T>(this T self, LogEventLevel level = LogEventLevel.Debug, string type = "server")
        where T : class
    {
        Log(level, self, type);
        return self;
    }

    public static T Log<T>(this T self) where T : class
    {
        Log(LogEventLevel.Debug, self);
        return self;
    }

    private static void Log<T>(LogEventLevel level, T? message, string type = "server") where T : class?
    {
        if (!ShouldLog(level))
            return;

        Color color = GetColor(type);

        ILogger log = ConsoleLog
            .ForContext("Message", message.ToJson())
            .ForContext("Level", level)
            .ForContext("Color", ToHexString(color))
            .ForContext("ConsoleType", type);

        string messageString = Regex.Replace(Regex.Replace(message.ToJson(), "^\"", ""), "\"$", "");

        switch (level)
        {
            case LogEventLevel.Information:
                log.Information(messageString);
                break;
            case LogEventLevel.Debug:
                log.Debug(messageString);
                break;
            case LogEventLevel.Warning:
                log.Warning(messageString);
                break;
            case LogEventLevel.Error:
                log.Error(messageString);
                break;
            case LogEventLevel.Verbose:
                log.Verbose(messageString);
                break;
            case LogEventLevel.Fatal:
                log.Fatal(messageString);
                break;
            default:
                log.Information(messageString);
                break;
        }

        // Networking.Networking.SendToAll("NewLog", "dashboardHub", new LogEntry
        // {
        //     Color = ToHexString(color),
        //     LogLevel = level,
        //     Message = message.ToJson(),
        //     Time = DateTime.Now,
        //     Type = type,
        //     ThreadId = Thread.CurrentThread.ManagedThreadId
        // });
    }

    private static void Log(LogEventLevel level, string message, string type = "server")
    {
        if (!ShouldLog(level))
            return;

        Color color = GetColor(type);

        ILogger log = ConsoleLog
            .ForContext("Message", message.ToJson())
            .ForContext("Level", level)
            .ForContext("Color", ToHexString(color))
            .ForContext("ConsoleType", type);

        string messageString = Regex.Replace(Regex.Replace(message, "^\"", ""), "\"$", "");

        switch (level)
        {
            case LogEventLevel.Information:
                log.Information(messageString);
                break;
            case LogEventLevel.Debug:
                log.Debug(messageString);
                break;
            case LogEventLevel.Warning:
                log.Warning(messageString);
                break;
            case LogEventLevel.Error:
                log.Error(messageString);
                break;
            case LogEventLevel.Verbose:
                log.Verbose(messageString);
                break;
            default:
                log.Information(messageString);
                break;
        }

        // Networking.Networking.SendToAll("NewLog", "dashboardHub", new LogEntry
        // {
        //     Color = ToHexString(color),
        //     LogLevel = level,
        //     Message = message,
        //     Time = DateTime.Now,
        //     Type = type,
        //     ThreadId = Thread.CurrentThread.ManagedThreadId
        // });
    }

    private static string ToHexString(this Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static void Log(LogEventLevel level, int message, string type = "server")
    {
        if (!ShouldLog(level))
            return;

        Color color = GetColor(type);

        ILogger log = ConsoleLog
            .ForContext("Message", message)
            .ForContext("Level", level)
            .ForContext("Color", ToHexString(color))
            .ForContext("ConsoleType", type);

        string messageString = Regex.Replace(Regex.Replace(message.ToString(), "^\"", ""), "\"$", "");

        switch (level)
        {
            case LogEventLevel.Information:
                log.Information(messageString);
                break;
            case LogEventLevel.Debug:
                log.Debug(messageString);
                break;
            case LogEventLevel.Warning:
                log.Warning(messageString);
                break;
            case LogEventLevel.Error:
                log.Error(messageString);
                break;
            case LogEventLevel.Verbose:
                log.Verbose(messageString);
                break;
            default:
                log.Information(messageString);
                break;
        }

        // Networking.Networking.SendToAll("NewLog", "dashboardHub", new LogEntry
        // {
        //     Color = ToHexString(color),
        //     LogLevel = level,
        //     Message = message.ToString(),
        //     Time = DateTime.Now,
        //     Type = type,
        //     ThreadId = Thread.CurrentThread.ManagedThreadId
        // });
    }

    public static void Debug(string message)
    {
        Log(LogEventLevel.Debug, message, "debug");
    }

    public static void Debug<T>(T? message, LogEventLevel level = LogEventLevel.Debug) where T : class
    {
        Log(level, message);
    }

    public static void Info(string message)
    {
        Log(LogEventLevel.Information, message, "info");
    }

    public static void Info<T>(T? message, LogEventLevel level = LogEventLevel.Debug) where T : class
    {
        Log(level, message);
    }

    public static void Warning(string message)
    {
        Log(LogEventLevel.Warning, message, "warning");
    }

    public static void Warning<T>(T? message, LogEventLevel level = LogEventLevel.Debug) where T : class
    {
        Log(level, message);
    }

    public static void Error(string message)
    {
        Log(LogEventLevel.Error, message, "error");
    }

    public static void Error<T>(T? message, LogEventLevel level = LogEventLevel.Debug) where T : class
    {
        Log(level, message);
    }

    public static void System(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "system");
    }

    public static void System<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "system");
    }

    public static void App(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "app");
    }

    public static void App<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "app");
    }

    public static void Request(string message, LogEventLevel level = LogEventLevel.Debug)
    {
        Log(level, message, "request");
    }

    public static void Request<T>(T? message, LogEventLevel level = LogEventLevel.Debug) where T : class
    {
        Log(level, message, "request");
    }

    public static void AniDb(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "anidb");
    }

    public static void AniDb<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "anidb");
    }

    public static void Access(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "access");
    }

    public static void Access<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "access");
    }

    public static void AcoustId(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "acoustid");
    }

    public static void AcoustId<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "acoustid");
    }

    public static void Configuration(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "configuration");
    }

    public static void Configuration<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "configuration");
    }

    public static void Auth(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "auth");
    }

    public static void Auth<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "auth");
    }

    public static void Register(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "register");
    }

    public static void Register<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "register");
    }

    public static void Certificate(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "certificate");
    }

    public static void Certificate<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "certificate");
    }

    public static void CoverArt(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "coverart");
    }

    public static void CoverArt<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "coverart");
    }

    public static void Setup(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "setup");
    }

    public static void Setup<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "setup");
    }

    public static void Queue(string message, LogEventLevel level = LogEventLevel.Debug)
    {
        Log(level, message, "queue");
    }

    public static void Queue<T>(T? message, LogEventLevel level = LogEventLevel.Debug) where T : class
    {
        Log(level, message, "queue");
    }

    public static void Encoder(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "encoder");
    }

    public static void Encoder<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "encoder");
    }

    public static void Ripper(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "ripper");
    }

    public static void Ripper<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "ripper");
    }

    public static void Http(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "http");
    }

    public static void Http<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "http");
    }

    public static void Notify(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "notify");
    }

    public static void Notify<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "notify");
    }

    public static void Ping(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "ping");
    }

    public static void Ping<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "ping");
    }

    public static void Socket(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "socket");
    }

    public static void Socket<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "socket");
    }

    public static void FanArt(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "fanart");
    }

    public static void FanArt<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "fanart");
    }

    public static void Fingerprint(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "fingerprint");
    }

    public static void Fingerprint<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "fingerprint");
    }

    public static void MovieDb(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "moviedb");
    }

    public static void MovieDb<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "moviedb");
    }

    public static void MusicBrainz(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "musicbrainz");
    }

    public static void MusicBrainz<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "musicbrainz");
    }

    public static void AudioDb(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "audiodb");
    }

    public static void AudioDb<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "audiodb");
    }

    public static void MusixMatch(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "musixmatch");
    }

    public static void MusixMatch<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "musixmatch");
    }

    public static void Tvdb(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "tvdb");
    }

    public static void Tvdb<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "tvdb");
    }

    public static void Youtube(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "youtube");
    }

    public static void Youtube<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "youtube");
    }

    public static void OpenSubs(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "opensubs");
    }

    public static void OpenSubs<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "opensubs");
    }

    public static void QBitTorrent(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "qbittorrent");
    }

    public static void QBitTorrent<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "qbittorrent");
    }

    public static void Transmission(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "transmission");
    }

    public static void Transmission<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "transmission");
    }

    public static void RuTorrent(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "rutorrent");
    }

    public static void RuTorrent<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "rutorrent");
    }

    public static void SabNzbd(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "sabnzbd");
    }

    public static void SabNzbd<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "sabnzbd");
    }

    public static void Discord(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "discord");
    }

    public static void Discord<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "discord");
    }

    public static void Twitter(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "twitter");
    }

    public static void Twitter<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "twitter");
    }

    public static void Whatsapp(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "whatsapp");
    }

    public static void Whatsapp<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "whatsapp");
    }

    public static void Telegram(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "telegram");
    }

    public static void Telegram<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "telegram");
    }

    public static void Webhook(string message, LogEventLevel level = LogEventLevel.Information)
    {
        Log(level, message, "webhook");
    }

    public static void Webhook<T>(T? message, LogEventLevel level = LogEventLevel.Information) where T : class
    {
        Log(level, message, "webhook");
    }

    public static async Task<List<LogEntry>> GetLogs(int limit = 10, Func<LogEntry, bool>? filter = null)
    {
        return await LogReader.GetLastDailyLogsAsync(AppFiles.LogPath, limit, filter);
    }
}