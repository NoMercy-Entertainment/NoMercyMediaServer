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

namespace NoMercy.NmSystem.Information;

public interface IServerConfiguration
{
    string AuthBaseUrl { get; }
    string TokenClientId { get; }
    string AppBaseUrl { get; }
    string ApiBaseUrl { get; }
    string ApiServerBaseUrl { get; }
    string DnsServer { get; }
    string UserAgent { get; }
    bool Started { get; }
    string? CloudflareTunnelToken { get; }
    int InternalServerPort { get; }
    int ExternalServerPort { get; }
    string ManagementPipeName { get; }
    string ManagementSocketPath { get; }
    bool Swagger { get; }
    bool IsDev { get; }
    bool IsTest { get; }
    bool UpdateAvailable { get; }
    bool RestartNeeded { get; }
    string? LatestVersion { get; }
    
    int LibraryWorkers { get; }
    int ImportWorkers { get; }
    int ExtrasWorkers { get; }
    int EncoderWorkers { get; }
    int EncoderTaskWorkers { get; }
    int GpuEncoderWorkers { get; }
    int CpuEncoderWorkers { get; }
    
    double EncoderCpuHeadroomPercent { get; }
    double EncoderGpuHeadroomPercent { get; }
    long EncoderMinFreeMemoryMb { get; }
    
    int CronWorkers { get; }
    int ImageWorkers { get; }
    int FileWorkers { get; }
    int MusicWorkers { get; }
    int PaletteWorkers { get; }
    
    bool ShowAdultContent { get; }
}
