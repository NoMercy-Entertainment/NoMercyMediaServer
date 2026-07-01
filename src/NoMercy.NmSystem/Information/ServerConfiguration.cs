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

using Microsoft.Extensions.Options;
using NoMercy.NmSystem.Configuration;

namespace NoMercy.NmSystem.Information;

public class ServerConfiguration : IServerConfiguration
{
    public string AuthBaseUrl { get; set; } = "https://auth.nomercy.tv/realms/NoMercyTV/";
    public string TokenClientId { get; set; } = "nomercy-server";
    public string AppBaseUrl { get; set; } = "https://app.nomercy.tv/";
    public string ApiBaseUrl { get; set; } = "https://api.nomercy.tv/";
    public string ApiServerBaseUrl => $"{ApiBaseUrl}v1/server/";
    public string DnsServer { get; set; } = "1.1.1.1";
    public string UserAgent => $"NoMercy MediaServer ( admin@nomercy.tv )";

    public bool Started { get; set; }
    public string? CloudflareTunnelToken { get; set; }
    public int InternalServerPort { get; set; } = 7626;
    public int ExternalServerPort { get; set; } = 7626;
    public string ManagementPipeName { get; set; } = "NoMercyManagement";
    public string ManagementSocketPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nomercy-management.sock");

    public bool Swagger { get; set; } = true;
    public bool IsDev { get; set; }
    public bool IsTest { get; set; }
    public bool UpdateAvailable { get; set; }
    public bool RestartNeeded { get; set; }
    public string? LatestVersion { get; set; }

    public int LibraryWorkers { get; set; } = 1;
    public int ImportWorkers { get; set; } = 2;
    public int ExtrasWorkers { get; set; } = 4;
    public int EncoderWorkers { get; set; } = 1;
    public int EncoderTaskWorkers { get; set; } = 0;
    public int GpuEncoderWorkers { get; set; } = 1;
    public int CpuEncoderWorkers { get; set; } = 1;

    public double EncoderCpuHeadroomPercent { get; set; } = 90.0;
    public double EncoderGpuHeadroomPercent { get; set; } = 95.0;
    public long EncoderMinFreeMemoryMb { get; set; } = 1024;

    public int CronWorkers { get; set; } = 1;
    public int ImageWorkers { get; set; } = 3;
    public int FileWorkers { get; set; } = 2;
    public int MusicWorkers { get; set; } = 2;
    public int PaletteWorkers { get; set; } = 1;

    // Safe-by-default: delegates to RuntimeServerSettings so the controller gate
    // always reflects the live dashboard setting (AllowAdultContent == true only).
    public bool ShowAdultContent => RuntimeServerSettings.Current.ShowAdultContent;
}

public class ServerConfigurationWrapper(IOptions<ServerConfiguration> options)
    : IServerConfiguration
{
    private readonly ServerConfiguration _config = options.Value;

    public string AuthBaseUrl => _config.AuthBaseUrl;
    public string TokenClientId => _config.TokenClientId;
    public string AppBaseUrl => _config.AppBaseUrl;
    public string ApiBaseUrl => _config.ApiBaseUrl;
    public string ApiServerBaseUrl => _config.ApiServerBaseUrl;
    public string DnsServer => _config.DnsServer;
    public string UserAgent => _config.UserAgent;
    public bool Started => _config.Started;
    public string? CloudflareTunnelToken => _config.CloudflareTunnelToken;
    public int InternalServerPort => _config.InternalServerPort;
    public int ExternalServerPort => _config.ExternalServerPort;
    public string ManagementPipeName => _config.ManagementPipeName;
    public string ManagementSocketPath => _config.ManagementSocketPath;
    public bool Swagger => _config.Swagger;
    public bool IsDev => _config.IsDev;
    public bool IsTest => _config.IsTest;
    public bool UpdateAvailable => _config.UpdateAvailable;
    public bool RestartNeeded => _config.RestartNeeded;
    public string? LatestVersion => _config.LatestVersion;
    public int LibraryWorkers => _config.LibraryWorkers;
    public int ImportWorkers => _config.ImportWorkers;
    public int ExtrasWorkers => _config.ExtrasWorkers;
    public int EncoderWorkers => _config.EncoderWorkers;
    public int EncoderTaskWorkers => _config.EncoderTaskWorkers;
    public int GpuEncoderWorkers => _config.GpuEncoderWorkers;
    public int CpuEncoderWorkers => _config.CpuEncoderWorkers;
    public double EncoderCpuHeadroomPercent => _config.EncoderCpuHeadroomPercent;
    public double EncoderGpuHeadroomPercent => _config.EncoderGpuHeadroomPercent;
    public long EncoderMinFreeMemoryMb => _config.EncoderMinFreeMemoryMb;
    public int CronWorkers => _config.CronWorkers;
    public int ImageWorkers => _config.ImageWorkers;
    public int FileWorkers => _config.FileWorkers;
    public int MusicWorkers => _config.MusicWorkers;
    public int PaletteWorkers => _config.PaletteWorkers;
    public bool ShowAdultContent => _config.ShowAdultContent;
}
