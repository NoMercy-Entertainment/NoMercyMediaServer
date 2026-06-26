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

using NoMercy.NmSystem.Configuration;
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

    public static string UserAgent =>
        $"NoMercy MediaServer/{Software.Version} ( admin@nomercy.tv )";

    public static int StunPort => InternalServerPort + 1;

    public static int InternalServerPort
    {
        get => RuntimeServerSettings.Current.InternalServerPort;
        set => RuntimeServerSettings.Current.InternalServerPort = value;
    }

    public static int ExternalServerPort
    {
        get => RuntimeServerSettings.Current.ExternalServerPort;
        set => RuntimeServerSettings.Current.ExternalServerPort = value;
    }

    public static string ManagementPipeName
    {
        get => field ?? "NoMercyManagement";
        set;
    }

    public static string ManagementSocketPath =>
        Path.Combine(AppFiles.AppPath, "nomercy-management.sock");

    public static bool Swagger
    {
        get => RuntimeServerSettings.Current.Swagger;
        set => RuntimeServerSettings.Current.Swagger = value;
    }

    public static bool IsDev { get; set; }
    public static bool IsTest { get; set; }

    public static KeyValuePair<string, int> LibraryWorkers
    {
        get => RuntimeServerSettings.Current.LibraryWorkers;
        set => RuntimeServerSettings.Current.LibraryWorkers = value;
    }
    public static KeyValuePair<string, int> ImportWorkers
    {
        get => RuntimeServerSettings.Current.ImportWorkers;
        set => RuntimeServerSettings.Current.ImportWorkers = value;
    }
    public static KeyValuePair<string, int> ExtrasWorkers
    {
        get => RuntimeServerSettings.Current.ExtrasWorkers;
        set => RuntimeServerSettings.Current.ExtrasWorkers = value;
    }
    public static KeyValuePair<string, int> EncoderWorkers
    {
        get => RuntimeServerSettings.Current.EncoderWorkers;
        set => RuntimeServerSettings.Current.EncoderWorkers = value;
    }

    // encoder-task is superseded by encoder-gpu + encoder-cpu (Task 2 resource scheduler).
    // Kept for backward compatibility with any persisted queue-state that still references it.
    public static KeyValuePair<string, int> EncoderTaskWorkers
    {
        get => RuntimeServerSettings.Current.EncoderTaskWorkers;
        set => RuntimeServerSettings.Current.EncoderTaskWorkers = value;
    }

    // Worker count is the upper bound on concurrent encodes. The actual cap is
    // the lower of (a) this number, (b) ResourceBudget's static semaphores
    // (NVENC session count, CPU thread budget), and (c) the live-headroom gate
    // (system CPU + GPU encode utilization + free memory) — see
    // ResourceBudgetOptions. Default to 1 so a fresh install never pegs the
    // host; users with capable hardware can raise it via SetWorkerCount.
    public static KeyValuePair<string, int> GpuEncoderWorkers
    {
        get => RuntimeServerSettings.Current.GpuEncoderWorkers;
        set => RuntimeServerSettings.Current.GpuEncoderWorkers = value;
    }

    public static KeyValuePair<string, int> CpuEncoderWorkers
    {
        get => RuntimeServerSettings.Current.CpuEncoderWorkers;
        set => RuntimeServerSettings.Current.CpuEncoderWorkers = value;
    }

    // Live-headroom thresholds consulted by ResourceBudget.TryAcquire before
    // granting a new encoder lease. Each value left at 0 disables that signal.
    // Defaults leave room for the user's other work — they don't max the box.

    public static KeyValuePair<string, int> CronWorkers
    {
        get => RuntimeServerSettings.Current.CronWorkers;
        set => RuntimeServerSettings.Current.CronWorkers = value;
    }
    public static KeyValuePair<string, int> ImageWorkers
    {
        get => RuntimeServerSettings.Current.ImageWorkers;
        set => RuntimeServerSettings.Current.ImageWorkers = value;
    }
    public static KeyValuePair<string, int> FileWorkers
    {
        get => RuntimeServerSettings.Current.FileWorkers;
        set => RuntimeServerSettings.Current.FileWorkers = value;
    }
    public static KeyValuePair<string, int> MusicWorkers
    {
        get => RuntimeServerSettings.Current.MusicWorkers;
        set => RuntimeServerSettings.Current.MusicWorkers = value;
    }
    public static KeyValuePair<string, int> PaletteWorkers
    {
        get => RuntimeServerSettings.Current.PaletteWorkers;
        set => RuntimeServerSettings.Current.PaletteWorkers = value;
    }

    public static bool? AllowAdultContent
    {
        get => RuntimeServerSettings.Current.AllowAdultContent;
        set => RuntimeServerSettings.Current.AllowAdultContent = value;
    }

    // Safe-by-default: adult content is shown only when explicitly enabled.
    // A null (never configured) or false setting both resolve to hidden.
    public static bool ShowAdultContent => AllowAdultContent == true;
}
