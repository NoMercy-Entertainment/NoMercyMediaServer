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
using Sharpcaster.Models;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;

namespace NoMercy.Networking.Cast;

public interface IChromeCastService
{
    Task Init();
    string[] GetChromeCasts();
    Task<string?> FindReceiverNameByIpAsync(string ip);
    Task SelectChromecast(string name);
    Task SelectChromecast(ChromecastReceiver? receiver);
    Task Launch(string? name = null);
    Task LaunchAndroidReceiver(
        string? name = null,
        object? customData = null,
        bool useAndroidReceiver = true
    );
    Task CastPlaylist(string value, string? name = null, string? accessToken = null);
    ChromecastStatus? GetChromecastStatus(string? name = null);
    MediaStatus? GetMediaStatus(string? name = null);
    Task Stop(string? name = null);
    Task Disconnect(string? name = null);
    Task DisconnectAllAsync();
}
