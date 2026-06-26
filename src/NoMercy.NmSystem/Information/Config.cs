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

using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Information;

public static class Config
{
    private const string DefaultAuthBaseUrl = "https://auth.nomercy.tv/realms/NoMercyTV/";
    private const string DefaultAppBaseUrl = "https://app.nomercy.tv/";
    private const string DefaultApiBaseUrl = "https://api.nomercy.tv/";

    public static string AuthBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_AUTH_URL") ?? DefaultAuthBaseUrl;

    public static readonly string TokenClientId = "nomercy-server";

    public static string AppBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_APP_URL") ?? DefaultAppBaseUrl;

    public static string ApiBaseUrl { get; set; } =
        Environment.GetEnvironmentVariable("NOMERCY_API_URL") ?? DefaultApiBaseUrl;

    public static string ApiServerBaseUrl { get; set; } = $"{ApiBaseUrl}v1/server/";

    public static readonly string DnsServer = "1.1.1.1";

    public static string UserAgent =>
        $"NoMercy MediaServer/{Software.Version} ( admin@nomercy.tv )";

    public static string? CloudflareTunnelToken { get; set; }

    public static NatStatus NatStatus { get; set; } = NatStatus.None;
    public static bool PortForwarded { get; set; }

    public static string? StunPublicIp { get; set; }
    public static int? StunPublicPort { get; set; }
    public static int StunPort => InternalServerPort + 1;

    private static int? _internalServerPort;

    public static int InternalServerPort
    {
        get => _internalServerPort ?? 7626;
        set => _internalServerPort = value;
    }

    private static int? _externalServerPort;

    public static int ExternalServerPort
    {
        get => _externalServerPort ?? 7626;
        set => _externalServerPort = value;
    }

    public static string ManagementPipeName
    {
        get => field ?? "NoMercyManagement";
        set;
    }

    public static string ManagementSocketPath =>
        Path.Combine(AppFiles.AppPath, "nomercy-management.sock");

    public static bool Swagger { get; set; } = true;

    public static bool IsDev { get; set; }
    public static bool IsTest { get; set; }
    public static bool UpdateAvailable { get; set; }
    public static bool RestartNeeded { get; set; }
    public static string? LatestVersion { get; set; }

    public static KeyValuePair<string, int> LibraryWorkers { get; set; } = new("library", 1);
    public static KeyValuePair<string, int> ImportWorkers { get; set; } = new("import", 2);
    public static KeyValuePair<string, int> ExtrasWorkers { get; set; } = new("extras", 4);
    public static KeyValuePair<string, int> EncoderWorkers { get; set; } = new("encoder", 1);

    // encoder-task is superseded by encoder-gpu + encoder-cpu (Task 2 resource scheduler).
    // Kept for backward compatibility with any persisted queue-state that still references it.
    public static KeyValuePair<string, int> EncoderTaskWorkers { get; set; } =
        new("encoder-task", 0);

    // Worker count is the upper bound on concurrent encodes. The actual cap is
    // the lower of (a) this number, (b) ResourceBudget's static semaphores
    // (NVENC session count, CPU thread budget), and (c) the live-headroom gate
    // (system CPU + GPU encode utilization + free memory) — see
    // ResourceBudgetOptions. Default to 1 so a fresh install never pegs the
    // host; users with capable hardware can raise it via SetWorkerCount.
    public static KeyValuePair<string, int> GpuEncoderWorkers { get; set; } = new("encoder-gpu", 1);

    public static KeyValuePair<string, int> CpuEncoderWorkers { get; set; } = new("encoder-cpu", 1);

    // Live-headroom thresholds consulted by ResourceBudget.TryAcquire before
    // granting a new encoder lease. Each value left at 0 disables that signal.
    // Defaults leave room for the user's other work — they don't max the box.
    public static double EncoderCpuHeadroomPercent { get; set; } = 90.0;
    public static double EncoderGpuHeadroomPercent { get; set; } = 95.0;
    public static long EncoderMinFreeMemoryMb { get; set; } = 1024;

    public static KeyValuePair<string, int> CronWorkers { get; set; } = new("cron", 1);
    public static KeyValuePair<string, int> ImageWorkers { get; set; } = new("image", 3);
    public static KeyValuePair<string, int> FileWorkers { get; set; } = new("file", 2);
    public static KeyValuePair<string, int> MusicWorkers { get; set; } = new("music", 2);
    public static KeyValuePair<string, int> PaletteWorkers { get; set; } = new("palette", 1);

    public static readonly ParallelOptions ParallelOptions = new()
    {
        MaxDegreeOfParallelism = (int)Math.Floor(Environment.ProcessorCount / 2.0),
    };

    public static bool? AllowAdultContent { get; set; }

    // Safe-by-default: adult content is shown only when explicitly enabled.
    // A null (never configured) or false setting both resolve to hidden.
    public static bool ShowAdultContent => AllowAdultContent == true;
}
