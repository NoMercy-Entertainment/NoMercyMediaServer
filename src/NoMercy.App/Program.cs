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

using System.Diagnostics;
using System.Reflection;
using InfiniFrame;
using InfiniFrame.Js.MessageHandlers;
using InfiniFrame.WebServer;
using NoMercy.App.EmbeddedStaticAssets;
using NoMercy.NmSystem.Auth;
using NoMercy.Setup.Auth;

namespace NoMercy.App;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string windowTitle = "NoMercy TV";
        string iconPath = GetIconPath();

        string browserDataPath = GetBrowserDataPath();
        ClearBrowserDataOnVersionChange(browserDataPath: browserDataPath);

        // Set environment variable for URL before creating builder
        if (!Debugger.IsAttached)
        {
            Environment.SetEnvironmentVariable(variable: "ASPNETCORE_URLS", value: "http://localhost:7625");
        }

        InfiniFrameWebApplicationBuilder builder = InfiniFrameWebApplication.CreateBuilder(args: args);

        // InfiniFrame's SetBrowserControlInitParameters is platform-shaped: on Windows
        // (WebView2) it is a raw space-separated Chromium flag string, but on Linux
        // (WebKitGTK) it is parsed as a JSON object of WebKitSettings property overrides
        // via simdjson with no exception handling in the pinned InfiniFrame version — a
        // non-JSON value like a Chromium flag string throws an uncaught simdjson_error and
        // terminates the whole process. Remote-debugging-via-flag is Windows/WebView2-only
        // here, so only pass it there; every other platform gets null (native no-op).
        string? browserControlInitParameters = OperatingSystem.IsWindows()
            ? "--remote-debugging-port=9222"
            : null;

        IInfiniFrameWindowBuilder window = builder
            .Window.SetTemporaryFilesPath(path: browserDataPath)
            .Center()
            .SetTitle(title: windowTitle)
            .SetMinSize(width: 1280 + 16, height: 720 + 39)
            .SetSize(width: 1600 + 16, height: 900 + 39)
            .SetResizable(resizable: true)
            .SetIconFile(iconFilePath: iconPath)
            .SetUseOsDefaultSize(useOsDefaultSize: false)
            .SetMediaAutoplayEnabled(enable: true)
            .SetMediaStreamEnabled(enable: true)
            .SetBrowserControlInitParameters(parameters: browserControlInitParameters)
            .RegisterFullScreenWebMessageHandler()
            .RegisterOpenExternalTargetWebMessageHandler()
            .RegisterTitleChangedWebMessageHandler()
            .RegisterWindowManagementWebMessageHandler()
            .RegisterWebMessageReceivedHandler(
                handler: (sender, message) =>
                {
                    if (sender is not IInfiniFrameWindow infiniWindow)
                        return;

                    string response = $"Received message: \"{message}\"";
                    infiniWindow.SendWebMessage(message: response);
                }
            );

        // Parse --route argument
        string route = "";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--route" && i + 1 < args.Length)
            {
                route = args[i + 1];
                break;
            }

            if (args[i].StartsWith(value: "--route="))
            {
                route = args[i]["--route=".Length..];
                break;
            }
        }

        // Set start URL with optional route
        if (Debugger.IsAttached)
            window.SetStartUrl(url: $"https://app-dev.nomercy.tv{route}");
        else if (!string.IsNullOrEmpty(value: route))
            window.SetStartUrl(url: $"http://localhost:7625{route}");

        InfiniFrameWebApplication application = builder.Build();

        application.UseAutoServerClose();

        // Add /sso-callback handler for PKCE browser flow (non-setup mode)
        application.WebApp.Use(
            middleware: async (context, next) =>
            {
                if (
                    context.Request.Path.Value?.Equals(
                        value: "/sso-callback",
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    ) == true
                )
                {
                    string? code = context.Request.Query[key: "code"];
                    string? state = context.Request.Query[key: "state"];
                    string? error = context.Request.Query[key: "error"];
                    string? errorDescription = context.Request.Query[key: "error_description"];

                    int port = context.Request.Host.Port ?? 7625;
                    string redirectUri = $"http://localhost:{port}/sso-callback";

                    if (!string.IsNullOrEmpty(value: error))
                    {
                        // The Keycloak callback carries attacker-controllable error /
                        // error_description params; HTML-encode before reflecting them.
                        string msg = !string.IsNullOrEmpty(value: errorDescription)
                            ? $"Authorization failed: {System.Net.WebUtility.HtmlEncode(value: errorDescription)}"
                            : $"Authorization failed: {System.Net.WebUtility.HtmlEncode(value: error)}";
                        await context.Response.WriteAsync(
                            text: $"<html><body><h2>Authorization Failed</h2><p>{msg}</p></body></html>"
                        );
                        return;
                    }

                    if (string.IsNullOrEmpty(value: code) || string.IsNullOrEmpty(value: state))
                    {
                        await context.Response.WriteAsync(
                            text: "<html><body><h2>Missing code or state</h2></body></html>"
                        );
                        return;
                    }

                    // Use the DI container from the web app if possible, but this is a middleware.
                    // For now, keep it simple as this is a separate process.
                    // Actually, AuthManager might need to be resolved from DI.

                    bool ok = await AuthManager.TryCompletePkceFromCallbackAsync(
                        code: code,
                        state: state,
                        redirectUri: redirectUri,
                        authTokenStore: context.RequestServices.GetService<IAuthTokenStore>()
                    );
                    if (ok)
                    {
                        await context.Response.WriteAsync(
                            text: "<html><body><h2>Authentication Successful</h2><p>You may close this tab.</p><script>try{window.close();}catch(e){}</script></body></html>"
                        );
                    }
                    else
                    {
                        await context.Response.WriteAsync(
                            text: "<html><body><h2>Authentication Failed</h2><p>Could not complete login. Please try again.</p></body></html>"
                        );
                    }
                    return;
                }
                await next();
            }
        );

        // Use custom embedded static assets middleware with optimizations
        // (compression, caching, ETags) - replaces MapStaticAssets for embedded resources
        // Also injects the InfiniFrame.js script tag into HTML files at runtime
        application.WebApp.UseEmbeddedStaticAssets(
            configure: options =>
            {
                // Inject InfiniFrame script before </body> - required for InfiniFrame communication
                options.InjectScripts.Add(item: "/_content/InfiniLore.InfiniFrame.Js/InfiniFrame.js");

                // Force media query re-evaluation after WebView2 viewport settles.
                // WebView2 starts with a small initial viewport before InfiniFrame
                // sizes the window, causing Ionic's mobile detection to misfire.
                options.InjectScripts.Add(
                    item: "<script>window.addEventListener('load',function(){setTimeout(function(){window.dispatchEvent(new Event('resize'))},100)})</script>"
                );
            },
            assembly: typeof(Program).Assembly
        );

        application.Run();
    }

    private static string GetIconPath()
    {
        string iconName;
        if (OperatingSystem.IsWindows())
            iconName = "icon.ico";
        else if (OperatingSystem.IsLinux())
            iconName = "icon.png";
        else if (OperatingSystem.IsMacOS())
            iconName = "icon.icns";
        else
            throw new PlatformNotSupportedException(message: "Unsupported OS platform");

        // Extract embedded icon to temp directory (InfiniFrame requires a file path)
        string tempDir = Path.Combine(path1: Path.GetTempPath(), path2: "NoMercyApp");
        Directory.CreateDirectory(path: tempDir);
        string iconPath = Path.Combine(path1: tempDir, path2: iconName);

        if (!File.Exists(path: iconPath))
        {
            Assembly assembly = typeof(Program).Assembly;
            string resourceName = $"NoMercy.App.Resources.AppIcon.{iconName}";

            using Stream? stream = assembly.GetManifestResourceStream(name: resourceName);
            if (stream == null)
                throw new FileNotFoundException(
                    message: $"Embedded icon resource not found: {resourceName}"
                );

            using FileStream fileStream = File.Create(path: iconPath);
            stream.CopyTo(destination: fileStream);
        }

        return iconPath;
    }

    private static string GetBrowserDataPath()
    {
        // Match the path computed by AppFiles.BrowserPath so the server and app
        // share the same browser-data directory without requiring a project reference
        // to NmSystem (which would bloat the lightweight App executable).
        string appDataPath =
            Environment.OSVersion.Platform == PlatformID.Unix
                ? Path.Combine(
                    path1: Environment.GetEnvironmentVariable(variable: "HOME") ?? "/home/current",
                    path2: ".local/share"
                )
                : Environment.GetFolderPath(folder: Environment.SpecialFolder.LocalApplicationData);

        string baseDir = Path.Combine(path1: appDataPath, path2: "NoMercy", path3: "browser");
        Directory.CreateDirectory(path: baseDir);
        return baseDir;
    }

    // WebView2 subdirectories inside Default/ that hold login/session state — preserved across updates
    private static readonly HashSet<string> PreservedSubDirectories = new(
        comparer: StringComparer.OrdinalIgnoreCase
    )
    {
        "Session Storage",
        "Local Storage",
        "IndexedDB",
    };

    // WebView2 files inside Default/ that hold login/session state — preserved across updates
    private static readonly string[] PreservedFilePrefixes =
    [
        "Cookies",
        "Login Data",
        "Preferences",
        "Web Data",
    ];

    // WebView2 files in the EBWebView/ root that must survive cache clears
    private static readonly string[] PreservedEbWebViewFiles = ["Local State"];

    private static void ClearBrowserDataOnVersionChange(string browserDataPath)
    {
        string versionFile = Path.Combine(path1: browserDataPath, path2: ".app-version");
        string currentVersion =
            typeof(Program)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? typeof(Program).Assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        string? previousVersion = null;
        if (File.Exists(path: versionFile))
            previousVersion = File.ReadAllText(path: versionFile).Trim();

        if (previousVersion != currentVersion)
        {
            ClearBrowserCache(browserDataPath: browserDataPath);
            File.WriteAllText(path: versionFile, contents: currentVersion);
        }
    }

    private static void ClearBrowserCache(string browserDataPath)
    {
        // WebView2 stores profile data under EBWebView/Default/
        // Delete everything except session/login directories and files
        foreach (string dir in Directory.GetDirectories(path: browserDataPath))
        {
            string dirName = Path.GetFileName(path: dir);

            if (string.Equals(a: dirName, b: "EBWebView", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                ClearWebViewProfile(ebWebViewPath: dir);
                continue;
            }

            try
            {
                Directory.Delete(path: dir, recursive: true);
            }
            catch
            { /* locked or inaccessible — skip */
            }
        }

        foreach (string file in Directory.GetFiles(path: browserDataPath))
        {
            if (Path.GetFileName(path: file) == ".app-version")
                continue;
            try
            {
                File.Delete(path: file);
            }
            catch
            { /* skip */
            }
        }
    }

    private static void ClearWebViewProfile(string ebWebViewPath)
    {
        foreach (string profileDir in Directory.GetDirectories(path: ebWebViewPath))
        {
            string profileName = Path.GetFileName(path: profileDir);

            if (string.Equals(a: profileName, b: "Default", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                ClearProfileContents(profilePath: profileDir);
                continue;
            }

            try
            {
                Directory.Delete(path: profileDir, recursive: true);
            }
            catch
            { /* skip */
            }
        }

        foreach (string file in Directory.GetFiles(path: ebWebViewPath))
        {
            string fileName = Path.GetFileName(path: file);
            if (
                PreservedEbWebViewFiles.Any(predicate: p =>
                    fileName.StartsWith(value: p, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
                continue;

            try
            {
                File.Delete(path: file);
            }
            catch
            { /* skip */
            }
        }
    }

    private static void ClearProfileContents(string profilePath)
    {
        foreach (string dir in Directory.GetDirectories(path: profilePath))
        {
            string dirName = Path.GetFileName(path: dir);
            if (PreservedSubDirectories.Contains(item: dirName))
                continue;

            try
            {
                Directory.Delete(path: dir, recursive: true);
            }
            catch
            { /* skip */
            }
        }

        foreach (string file in Directory.GetFiles(path: profilePath))
        {
            string fileName = Path.GetFileName(path: file);
            if (
                PreservedFilePrefixes.Any(predicate: prefix =>
                    fileName.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
            )
                continue;

            try
            {
                File.Delete(path: file);
            }
            catch
            { /* skip */
            }
        }
    }
}
