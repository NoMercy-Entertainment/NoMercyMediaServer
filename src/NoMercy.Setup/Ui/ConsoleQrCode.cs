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

using System.Collections.Specialized;
using System.Web;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Setup.Ui;

/// <summary>
/// Displays a QR code for device auth in the terminal.
/// Delegates to <see cref="SetupTerminalUi"/> for interactive terminals
/// and falls back to a plain log line in non-interactive / service contexts.
/// </summary>
public class ConsoleQrCode
{
    /// <summary>
    /// Show the QR code and device auth UI in the terminal.
    /// Builds the setup page URL from the server's port and localhost.
    /// </summary>
    public static void Display(
        string verificationUriComplete,
        string verificationUri,
        string userCode
    )
    {
        string setupPageUrl =
            $"http://localhost:{RuntimeServerSettings.Current.InternalServerPort}/setup";
        Display(verificationUriComplete: verificationUriComplete, verificationUri: verificationUri, userCode: userCode, setupPageUrl: setupPageUrl);
    }

    /// <summary>
    /// Show the QR code and device auth UI in the terminal with a specific setup page URL.
    /// </summary>
    public static void Display(
        string verificationUriComplete,
        string verificationUri,
        string userCode,
        string setupPageUrl
    )
    {
        if (!SetupTerminalUi.IsInteractiveTerminal)
        {
            Logger.Auth(message: $"Scan QR code or visit: {verificationUriComplete}");
            Logger.Auth(message: $"Code: {userCode}");
            Logger.Auth(message: $"Setup page: {setupPageUrl}");
            return;
        }

        SetupTerminalUi ui = new();
        ui.Show(verificationUriComplete: verificationUriComplete, verificationUri: verificationUri, userCode: userCode, setupPageUrl: setupPageUrl);

        // Keep the UI alive until the process ends — the terminal UI object
        // is intentionally not disposed here so the resize watcher keeps running.
        // Callers that own the lifecycle pass their own instance.
    }

    /// <summary>
    /// Legacy overload — accepts only the complete verification URI.
    /// Used by Auth.TokenByDeviceGrant() which predates the terminal UI.
    /// </summary>
    public static void Display(string verificationUriComplete)
    {
        // Parse a best-effort display URI from the complete one
        string displayUri;
        try
        {
            Uri uri = new(uriString: verificationUriComplete);
            displayUri = $"{uri.Scheme}://{uri.Host}";
            if (!uri.IsDefaultPort)
                displayUri += $":{uri.Port}";
            displayUri += uri.AbsolutePath.TrimEnd(trimChar: '/');
        }
        catch
        {
            displayUri = verificationUriComplete;
        }

        string userCode = "";
        try
        {
            NameValueCollection query = HttpUtility.ParseQueryString(
                query: new Uri(uriString: verificationUriComplete).Query
            );
            userCode = query[name: "user_code"] ?? "";
        }
        catch
        {
            userCode = "";
        }

        Display(verificationUriComplete: verificationUriComplete, verificationUri: displayUri, userCode: userCode);
    }
}
