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

using Microsoft.Extensions.Logging;
using NoMercy.Networking.Cast;
using NoMercy.Setup.Cast;

namespace NoMercy.Api.Services.Music;

/// <summary>
/// Owns MusicHub.ChangeDeviceCommand's server-side Cast panel-wake LAUNCH
/// (HDMI-CEC One Touch Play via sharpcaster). This is the single gate: firing
/// the LAUNCH while the target is already live on MusicHub lets cast_shell
/// miss the running APK on the Cast Connect path and fall back to the Web
/// Receiver, replacing the app mid-playback and getting the session
/// liveness-ended. <see cref="ShouldFireCastWake"/> must stay the only
/// condition under which <see cref="LaunchIfColdAsync"/> touches
/// <see cref="IChromeCastService"/> — do not add an unconditional call path.
/// </summary>
public class CastPanelWakeLauncher(
    IChromeCastService chromeCast,
    ILogger<CastPanelWakeLauncher> logger
)
{
    public static bool ShouldFireCastWake(bool targetIsLive) => !targetIsLive;

    /// <summary>
    /// resolveLaunchData is deferred so a live target never pays for the
    /// token-exchange call it would just discard.
    /// </summary>
    public async Task LaunchIfColdAsync(
        bool targetIsLive,
        string targetIp,
        bool useAndroidReceiver,
        Func<Task<LaunchCustomData?>> resolveLaunchData
    )
    {
        if (!ShouldFireCastWake(targetIsLive))
            return;

        try
        {
            string? receiverName = await chromeCast.FindReceiverNameByIpAsync(targetIp);
            if (string.IsNullOrEmpty(receiverName))
            {
                logger.LogWarning(
                    "No Chromecast receiver discovered at {TargetIp} — panel won't wake via CEC",
                    targetIp
                );
                return;
            }

            LaunchCustomData? launchData = await resolveLaunchData();
            if (launchData is null)
            {
                logger.LogWarning(
                    "Cast token mint failed for {TargetIp} — falling back to LAUNCH without customData",
                    targetIp
                );
            }

            // SelectChromecast connects/reuses the pool entry for this specific
            // receiver. useAndroidReceiver is true only when the APK is reachable
            // on this TV (registered with the bus registry); otherwise cast_shell
            // would try the Cast Connect path, fail to find the APK, and fall back
            // to Web Receiver — that fallback path drops customData and the
            // receiver hangs on its splash. Going straight to Web Receiver
            // preserves customData.
            await chromeCast.SelectChromecast(receiverName);
            await chromeCast.LaunchAndroidReceiver(
                receiverName,
                launchData,
                useAndroidReceiver
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Server-side Cast launch failed for {TargetIp}: {Message}", [targetIp, ex.Message]
            );
        }
    }
}
