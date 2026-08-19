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

namespace NoMercy.OpticalMedia.Onboarding;

/// <summary>
/// Server-owned state for one drive's onboarding journey: detect → probe →
/// identify → confirm (manual or auto) → rip → encode → catalogue. Immutable;
/// every transition returns a new instance via <see cref="WithState"/> /
/// <see cref="WithCandidates"/>. Held by <see cref="DiscOnboardingSessionStore"/>,
/// one per normalised drive path.
/// </summary>
public sealed record DiscOnboardingSession(
    Guid SessionId,
    string DrivePath,
    DiscOnboardingState State,
    DiscCandidate[] Candidates,
    string? JobId,
    string? FailureReason,
    DateTimeOffset UpdatedAt
)
{
    public static DiscOnboardingSession Create(string drivePath) =>
        new(
            SessionId: Guid.NewGuid(),
            DrivePath: drivePath,
            State: DiscOnboardingState.Detected,
            Candidates: [],
            JobId: null,
            FailureReason: null,
            UpdatedAt: DateTimeOffset.UtcNow
        );

    public DiscOnboardingSession WithState(DiscOnboardingState state) =>
        this with
        {
            State = state,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    public DiscOnboardingSession WithCandidates(
        DiscCandidate[] candidates,
        DiscOnboardingState state
    ) => this with { Candidates = candidates, State = state, UpdatedAt = DateTimeOffset.UtcNow };

    public DiscOnboardingSession WithJob(string jobId) =>
        this with
        {
            JobId = jobId,
            State = DiscOnboardingState.Ripping,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    public DiscOnboardingSession WithFailure(string reason) =>
        this with
        {
            State = DiscOnboardingState.Failed,
            FailureReason = reason,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
}
