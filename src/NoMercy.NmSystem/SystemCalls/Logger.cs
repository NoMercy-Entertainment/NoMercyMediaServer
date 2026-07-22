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

using System.Drawing;
using Newtonsoft.Json;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Lifecycle;
using NoMercy.NmSystem.LogEnrichers;
using NoMercy.NmSystem.Logging;
using NoMercy.NmSystem.Logging.Rendering;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace NoMercy.NmSystem.SystemCalls;

public static class Logger
{
    private static Serilog.Core.Logger ConsoleLog { get; set; }
    private static Serilog.Core.Logger FileLog { get; set; }
    private static LogEventLevel _maxLogLevel = LogEventLevel.Debug;
    private const string ConsoleTemplate =
        "{Time} {ConsoleType} | {@Message:lj}{NewLine}{Exception}";

    public static event Action<LogEntry>? LogEmitted;

    /// <summary>
    /// When set (by the new logging provider's legacy bridge), console output for
    /// legacy <see cref="Logger"/> entries is rendered through the unified pipeline
    /// instead of this class's own Serilog console sink. Null before the bridge
    /// attaches (early startup), where the Serilog console sink is used as fallback.
    /// </summary>
    public static Action<LogEntry>? ConsoleSink;

    public class LogType
    {
        [JsonProperty(propertyName: "name")]
        public string Name { get; }

        [JsonProperty(propertyName: "display_name")]
        public string DisplayName { get; }

        [JsonProperty(propertyName: "color")]
        public Color Color { get; }

        [JsonProperty(propertyName: "colorHex")]
        public string ColorHex { get; }

        [JsonProperty(propertyName: "type")]
        public string Type { get; }

        [JsonProperty(propertyName: "level")]
        public LogEventLevel DefaultLevel { get; }

        public LogType(
            string name,
            string displayName,
            Color color,
            string type,
            LogEventLevel defaultLevel = LogEventLevel.Information
        )
        {
            Name = name;
            DisplayName = displayName;
            Color = color;
            ColorHex = color.ToHexString();
            Type = type;
            DefaultLevel = defaultLevel;
        }
    }

    public static readonly Dictionary<string, LogType> LogTypes = new()
    {
        // System category
        { "_", new(name: "_", displayName: "System", color: Color.DimGray, type: "spacer") },
        { "app", new(name: "app", displayName: "App", color: Color.MediumPurple, type: "System") },
        { "access", new(name: "access", displayName: "Access", color: Color.MediumPurple, type: "System") },
        { "configuration", new(name: "configuration", displayName: "Configuration", color: Color.MediumPurple, type: "System") },
        { "setup", new(name: "setup", displayName: "Setup", color: Color.CornflowerBlue, type: "System") },
        { "system", new(name: "system", displayName: "System", color: Color.CornflowerBlue, type: "System") },
        { "service", new(name: "service", displayName: "Service", color: Color.CornflowerBlue, type: "System") },
        { "debug", new(name: "debug", displayName: "Debug", color: Color.Gray, type: "System", defaultLevel: LogEventLevel.Debug) },
        { "info", new(name: "info", displayName: "Info", color: Color.White, type: "System") },
        { "warning", new(name: "warning", displayName: "Warning", color: Color.Yellow, type: "System", defaultLevel: LogEventLevel.Warning) },
        { "error", new(name: "error", displayName: "Error", color: Color.Red, type: "System", defaultLevel: LogEventLevel.Error) },
        { "auth", new(name: "auth", displayName: "Auth", color: Color.CornflowerBlue, type: "System") },
        { "register", new(name: "register", displayName: "Register", color: Color.CornflowerBlue, type: "System") },
        { "certificate", new(name: "certificate", displayName: "Certificate", color: Color.CornflowerBlue, type: "System") },
        // Workers category
        { "__", new(name: "__", displayName: "Workers", color: Color.DimGray, type: "spacer") },
        { "queue", new(name: "queue", displayName: "Queue", color: Color.Chocolate, type: "Workers", defaultLevel: LogEventLevel.Debug) },
        { "encoder", new(name: "encoder", displayName: "Encoder", color: Color.Chocolate, type: "Workers") },
        { "ripper", new(name: "ripper", displayName: "Ripper", color: Color.Chocolate, type: "Workers") },
        // Networking category
        { "___", new(name: "___", displayName: "Networking", color: Color.DimGray, type: "spacer") },
        { "http", new(name: "http", displayName: "Http", color: Color.Orange, type: "Networking") },
        { "notify", new(name: "notify", displayName: "Notify", color: Color.Orange, type: "Networking") },
        { "ping", new(name: "ping", displayName: "Ping", color: Color.Orange, type: "Networking") },
        { "socket", new(name: "socket", displayName: "Socket", color: Color.Orange, type: "Networking") },
        { "request", new(name: "request", displayName: "Request", color: Color.Orange, type: "Networking", defaultLevel: LogEventLevel.Debug) },
        // Providers category
        { "____", new(name: "____", displayName: "Providers", color: Color.DimGray, type: "spacer") },
        { "youtube", new(name: "youtube", displayName: "YouTube", color: Color.DodgerBlue, type: "Providers") },
        { "acoustid", new(name: "acoustid", displayName: "AcoustID", color: Color.DodgerBlue, type: "Providers") },
        { "anidb", new(name: "anidb", displayName: "AniDB", color: Color.DodgerBlue, type: "Providers") },
        { "audiodb", new(name: "audiodb", displayName: "AudioDB", color: Color.DodgerBlue, type: "Providers") },
        { "coverart", new(name: "coverart", displayName: "CoverArt", color: Color.DodgerBlue, type: "Providers") },
        { "fanart", new(name: "fanart", displayName: "Fanart", color: Color.DodgerBlue, type: "Providers") },
        { "fingerprint", new(name: "fingerprint", displayName: "Fingerprint", color: Color.DodgerBlue, type: "Providers") },
        { "lrclib", new(name: "lrclib", displayName: "Lrclib", color: Color.DodgerBlue, type: "Providers") },
        { "lyrics", new(name: "lyrics", displayName: "Lyrics", color: Color.DodgerBlue, type: "Providers") },
        { "moviedb", new(name: "moviedb", displayName: "TheMovieDB", color: Color.DodgerBlue, type: "Providers") },
        { "musicbrainz", new(name: "musicbrainz", displayName: "MusicBrainz", color: Color.DodgerBlue, type: "Providers") },
        { "musixmatch", new(name: "musixmatch", displayName: "MusixMatch", color: Color.DodgerBlue, type: "Providers") },
        { "openSubs", new(name: "openSubs", displayName: "OpenSubs", color: Color.DodgerBlue, type: "Providers") },
        { "tvdb", new(name: "tvdb", displayName: "TheTVDB", color: Color.DodgerBlue, type: "Providers") },
        // Notifications category
        { "_____", new(name: "_____", displayName: "Notifications", color: Color.DimGray, type: "spacer") },
        { "discord", new(name: "discord", displayName: "Discord", color: Color.Green, type: "Notifications") },
        { "twitch", new(name: "twitch", displayName: "Twitch", color: Color.Green, type: "Notifications") },
        { "spotify", new(name: "spotify", displayName: "Spotify", color: Color.Green, type: "Notifications") },
        { "twitter", new(name: "twitter", displayName: "Twitter", color: Color.Green, type: "Notifications") },
        { "webhook", new(name: "webhook", displayName: "Webhook", color: Color.Green, type: "Notifications") },
        { "whatsapp", new(name: "whatsapp", displayName: "Whatsapp", color: Color.Green, type: "Notifications") },
        { "telegram", new(name: "telegram", displayName: "Telegram", color: Color.Green, type: "Notifications") },
    };

    static Logger()
    {
        ConsoleLog = CreateConsoleConfiguration().CreateLogger();
        FileLog = CreateFileConfiguration().CreateLogger();
    }

    private static LoggerConfiguration DefaultEnrich(this LoggerConfiguration lc)
    {
        return lc.Enrich.FromLogContext().Enrich.With<WithThreadIdEnricher>();
    }

    private static void SinkFile(this LoggerConfiguration lc, string filePath)
    {
        lc.Enrich.With<FileTypeEnricher>()
            .Enrich.With<FileTimestampEnricher>()
            .Enrich.With<FileMessageEnricher>()
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: filePath,
                rollingInterval: RollingInterval.Day,
                // Without shared:true the Serilog file sink takes an exclusive
                // FileShare.Read lock. Two processes pointing at the same log
                // file (Rider-launched server + leftover background process,
                // or a midnight rollover collision) deadlock on each other,
                // and any other tooling that wants to write the same path
                // (e.g. the CLI for crash-time fallback logging) gets locked
                // out. shared mode coordinates appends through a global named
                // mutex.
                shared: true,
                // Serilog batches writes with no flush guarantee on crash.
                // 2 s is short enough that a SIGSEGV / power loss only loses
                // a couple seconds of trailing logs, long enough to keep IO
                // overhead invisible.
                flushToDiskInterval: TimeSpan.FromSeconds(seconds: 2)
            );
    }

    private static SystemConsoleTheme Literate { get; } =
        new(
            styles: new Dictionary<ConsoleThemeStyle, SystemConsoleThemeStyle>
            {
                [key: ConsoleThemeStyle.Text] = new() { Foreground = ConsoleColor.White },
                [key: ConsoleThemeStyle.SecondaryText] = new() { Foreground = ConsoleColor.Gray },
                [key: ConsoleThemeStyle.TertiaryText] = new() { Foreground = ConsoleColor.Cyan },
                [key: ConsoleThemeStyle.Invalid] = new() { Foreground = ConsoleColor.Yellow },
                [key: ConsoleThemeStyle.Null] = new() { Foreground = ConsoleColor.Blue },
                [key: ConsoleThemeStyle.Name] = new() { Foreground = ConsoleColor.Gray },
                [key: ConsoleThemeStyle.String] = new() { Foreground = ConsoleColor.White },
                [key: ConsoleThemeStyle.Number] = new() { Foreground = ConsoleColor.Magenta },
                [key: ConsoleThemeStyle.Boolean] = new() { Foreground = ConsoleColor.DarkYellow },
                [key: ConsoleThemeStyle.Scalar] = new() { Foreground = ConsoleColor.Green },
                [key: ConsoleThemeStyle.LevelVerbose] = new() { Foreground = ConsoleColor.Gray },
                [key: ConsoleThemeStyle.LevelDebug] = new() { Foreground = ConsoleColor.Gray },
                [key: ConsoleThemeStyle.LevelInformation] = new() { Foreground = ConsoleColor.White },
                [key: ConsoleThemeStyle.LevelWarning] = new() { Foreground = ConsoleColor.Yellow },
                [key: ConsoleThemeStyle.LevelError] = new()
                {
                    Foreground = ConsoleColor.White,
                    Background = ConsoleColor.Red,
                },
                [key: ConsoleThemeStyle.LevelFatal] = new()
                {
                    Foreground = ConsoleColor.White,
                    Background = ConsoleColor.Red,
                },
            }
        );

    private static void SinkConsole(this LoggerConfiguration lc)
    {
        lc.Enrich.With<ConsoleTimestampEnricher>()
            .Enrich.With<ConsoleTypeEnricher>()
            .WriteTo.Console(
                applyThemeToRedirectedOutput: true,
                theme: Literate,
                outputTemplate: ConsoleTemplate
            );
    }

    private static LoggerConfiguration CreateConsoleConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .DefaultEnrich()
            .WriteTo.Logger(configureLogger: lc =>
            {
                lc.SinkConsole();
            });
    }

    private static LoggerConfiguration CreateFileConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .DefaultEnrich()
            .WriteTo.Logger(configureLogger: lc => lc.SinkFile(filePath: Path.Join(path1: AppFiles.LogPath, path2: "log.txt")));
    }

    private static bool ShouldLog(LogEventLevel level) => level >= _maxLogLevel;

    public static void SetLogLevel(LogEventLevel level) => _maxLogLevel = level;

    /// <summary>
    /// Holds a mutual-exclusion lock while writing every line of a multi-line banner.
    /// Callers supply (logType, message, level) tuples; all lines are written under
    /// a single lock so no other thread can interleave a log entry between them.
    /// </summary>
    private static readonly object BannerLock = new();

    public static void WriteBanner(
        IEnumerable<(string LogType, string Message, LogEventLevel Level)> lines
    )
    {
        lock (BannerLock)
        {
            foreach ((string logType, string message, LogEventLevel level) in lines)
            {
                Log(logType: logType, message: message, level: level);
            }
        }
    }

    private static readonly object ConsoleFallbackGate = new();

    /// <summary>Renders a log entry to the console through the unified
    /// <see cref="ConsoleLineRenderer"/> using the provider's default theme,
    /// colour and width rules. Used before <see cref="ConsoleSink"/> is wired so
    /// early-boot output is not stuck on the legacy Serilog format.</summary>
    private static void WriteConsoleFallback(LogEntry entry)
    {
        LogCategory category = LogCategories.Resolve(key: entry.Type);
        bool color =
            !Console.IsOutputRedirected && Environment.GetEnvironmentVariable(variable: "NO_COLOR") is null;
        string line = ConsoleLineRenderer.Render(
            timestamp: entry.Time.ToLocalTime(),
            level: ToMelLevel(level: entry.LogLevel),
            category: category,
            message: entry.Message,
            exception: null,
            theme: NoMercyConsoleTheme.Dark,
            color: color,
            width: ConsoleFallbackWidth()
        );

        lock (ConsoleFallbackGate)
        {
            Console.Out.WriteLine(value: line);
        }
    }

    private static int ConsoleFallbackWidth()
    {
        try
        {
            if (!Console.IsOutputRedirected)
            {
                int w = Console.WindowWidth;
                if (w > 0)
                    return w;
            }
        }
        catch
        {
            // No attached console; fall through to a sensible default.
        }

        return int.TryParse(s: Environment.GetEnvironmentVariable(variable: "COLUMNS"), result: out int cols) && cols > 0
            ? cols
            : 120;
    }

    private static Microsoft.Extensions.Logging.LogLevel ToMelLevel(LogEventLevel level) =>
        level switch
        {
            LogEventLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Trace,
            LogEventLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogEventLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            LogEventLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogEventLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogEventLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information,
        };

    private static void Log<T>(string logType, T message, LogEventLevel? level = null)
        where T : class
    {
        if (!LogTypes.TryGetValue(key: logType, value: out LogType? type))
        {
            type = new(name: logType, displayName: logType, color: Color.White, type: "Unknown");
        }

        LogEventLevel logLevel = level ?? type.DefaultLevel;

        if (!ShouldLog(level: logLevel))
            return;

        string colorHex = type.ColorHex;

        LogEntry entry = new()
        {
            Type = logType,
            Color = colorHex,
            Message = message.ToString() ?? string.Empty,
            LogLevel = logLevel,
            Time = DateTime.UtcNow,
            ThreadId = Environment.CurrentManagedThreadId,
        };

        if (ConsoleSink is { } sink)
        {
            sink(obj: entry);
        }
        else
        {
            // No provider bridge yet (early bootstrap, before the host/DI is
            // built): render through the same ConsoleLineRenderer the provider
            // uses so pre-host lines match the unified format instead of the
            // legacy Serilog template.
            WriteConsoleFallback(entry: entry);
        }

        FileLog
            .ForContext(propertyName: "Type", value: logType)
            .ForContext(propertyName: "Color", value: colorHex)
            .ForContext(propertyName: "Message", value: message.ToJson())
            .ForContext(propertyName: "Level", value: logLevel)
            .ForContext(propertyName: "ConsoleType", value: type.Name)
            .Write(level: logLevel, messageTemplate: "{@Message}", propertyValue: message.ToJson());

        LogEmitted?.Invoke(obj: entry);
    }

    // Generic entry point
    public static void Write<T>(string logType, T message, LogEventLevel? level = null)
        where T : class
    {
        Log(logType: logType, message: message, level: level);
    }

    public static void Write(string logType, string message, LogEventLevel? level = null)
    {
        Log(logType: logType, message: message, level: level);
    }

    internal static Color GetColor(string type)
    {
        return LogTypes.TryGetValue(key: type, value: out LogType? color) ? color.Color : Color.Red;
    }

    // Standard logging methods with simplified implementation
    public static void Debug<T>(T message, LogEventLevel? level = null)
        where T : class => Log(logType: "debug", message: message, level: level ?? LogEventLevel.Debug);

    public static void Info<T>(T message, LogEventLevel? level = null)
        where T : class => Log(logType: "info", message: message, level: level ?? LogEventLevel.Information);

    public static void Warning<T>(T message, LogEventLevel? level = null)
        where T : class => Log(logType: "warning", message: message, level: level ?? LogEventLevel.Warning);

    public static void Error<T>(T message, LogEventLevel? level = null)
        where T : class => Log(logType: "error", message: message, level: level ?? LogEventLevel.Error);

    public static void Verbose<T>(T message, LogEventLevel? level = null)
        where T : class => Log(logType: "verbose", message: message, level: level ?? LogEventLevel.Verbose);

    public static void Access<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "access", message: message, level: level);

    public static void App<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "app", message: message, level: level);

    public static void Auth<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "auth", message: message, level: level);

    public static void Register<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "register", message: message, level: level);

    public static void Certificate<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "certificate", message: message, level: level);

    public static void Configuration<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "configuration", message: message, level: level);

    public static void Setup<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "setup", message: message, level: level);

    public static void System<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "system", message: message, level: level);

    public static void Queue<T>(T message, LogEventLevel level = LogEventLevel.Debug)
        where T : class => Log(logType: "queue", message: message, level: level);

    public static void Encoder<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "encoder", message: message, level: level);

    public static void Ripper<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "ripper", message: message, level: level);

    public static void Http<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class =>
        Log(logType: "http", message: message, level: BootLog.IsBootInProgress ? LogEventLevel.Debug : level);

    public static void Ping<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class =>
        Log(logType: "ping", message: message, level: BootLog.IsBootInProgress ? LogEventLevel.Debug : level);

    public static void Request<T>(T message, LogEventLevel level = LogEventLevel.Debug)
        where T : class => Log(logType: "request", message: message, level: level);

    public static void Socket<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class =>
        Log(logType: "socket", message: message, level: BootLog.IsBootInProgress ? LogEventLevel.Debug : level);

    public static void AcoustId<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "acoustid", message: message, level: level);

    public static void AniDb<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "anidb", message: message, level: level);

    public static void AudioDb<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "audiodb", message: message, level: level);

    public static void CoverArt<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "coverart", message: message, level: level);

    public static void FanArt<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "fanart", message: message, level: level);

    public static void Fingerprint<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "fingerprint", message: message, level: level);

    public static void Lrclib<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "lrclib", message: message, level: level);

    public static void Lyrics<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "lyrics", message: message, level: level);

    public static void MovieDb<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "moviedb", message: message, level: level);

    public static void MusicBrainz<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "musicbrainz", message: message, level: level);

    public static void MusixMatch<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "musixmatch", message: message, level: level);

    public static void OpenSubs<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "opensubs", message: message, level: level);

    public static void QBitTorrent<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "qbittorrent", message: message, level: level);

    public static void RuTorrent<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "rutorrent", message: message, level: level);

    public static void SabNzbd<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "sabnzbd", message: message, level: level);

    public static void Tvdb<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "tvdb", message: message, level: level);

    public static void Youtube<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "youtube", message: message, level: level);

    public static void Discord<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "discord", message: message, level: level);

    public static void Notify<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "notify", message: message, level: level);

    public static void Telegram<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "telegram", message: message, level: level);

    public static void Transmission<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "transmission", message: message, level: level);

    public static void Twitter<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "twitter", message: message, level: level);

    public static void Webhook<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "webhook", message: message, level: level);

    public static void Whatsapp<T>(T message, LogEventLevel level = LogEventLevel.Information)
        where T : class => Log(logType: "whatsapp", message: message, level: level);

    public static async Task<List<LogEntry>> GetLogs(
        int limit = 10,
        Func<LogEntry, bool>? filter = null
    )
    {
        string logDirectoryPath = AppFiles.LogPath;
        // LOCAL-ONLY: Logger is a static class in NmSystem; no reference to NoMercy.Providers.
        IStorageDriver driver = new LocalStorageDriver();
        IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [], driver: driver));
        List<LogEntry> logs = await LogReader.GetLogsAsync(
            storage: storage,
            logDirectoryPath: logDirectoryPath,
            filter: filter
        );

        return logs.OrderByDescending(keySelector: entry => entry.Time)
            .Take(count: limit)
            .OrderBy(keySelector: entry => entry.Time)
            .ToList();
    }
}
