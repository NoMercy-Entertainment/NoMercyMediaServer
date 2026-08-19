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

namespace NoMercy.OpticalMedia.Onboarding;

/// <summary>
/// Lifecycle of one disc-onboarding session, one per drive. Drives the
/// dashboard progress screen and the SignalR broadcast in
/// <see cref="DiscOnboardingOrchestrator"/>.
/// </summary>
public enum DiscOnboardingState
{
    Detected,
    Probing,
    Identified,
    AwaitingConfirm,
    AutoConfirmed,
    Ripping,
    Encoding,
    Cataloguing,
    Complete,
    Failed,
}
