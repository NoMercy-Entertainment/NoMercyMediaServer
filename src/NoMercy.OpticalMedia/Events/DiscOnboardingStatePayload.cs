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

using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Onboarding;

// Lives in NoMercy.OpticalMedia (not NoMercy.Events) because OpticalMedia
// already references Events; the reverse would cycle. Namespace stays
// NoMercy.Events.Onboarding for consumer consistency with DriveMonitor.
namespace NoMercy.Events.Onboarding;

/// <summary>
/// Typed payload broadcast on <c>ripperHub</c> / <c>drivesHub</c> as
/// <c>"DiscOnboardingState"</c> whenever a <see cref="DiscOnboardingSession"/>
/// transitions. Kept separate from <c>DriveStatePayload</c> (which already
/// has shipped clients depending on its exact shape) rather than overloading it.
/// </summary>
public sealed record DiscOnboardingStatePayload(
    Guid SessionId,
    string DrivePath,
    string State,
    DiscCandidate[] Candidates,
    string? JobId,
    string? FailureReason,
    DateTimeOffset UpdatedAt,
    string? ResultType = null,
    string? ResultId = null
)
{
    public static DiscOnboardingStatePayload From(DiscOnboardingSession session) =>
        new(
            SessionId: session.SessionId,
            DrivePath: session.DrivePath,
            State: session.State.ToString(),
            Candidates: session.Candidates,
            JobId: session.JobId,
            FailureReason: session.FailureReason,
            UpdatedAt: session.UpdatedAt,
            ResultType: session.ResultType,
            ResultId: session.ResultId
        );
}
