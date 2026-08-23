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
    DateTimeOffset UpdatedAt,
    Ulid? LibraryId = null,
    int? ConfirmedTmdbId = null,
    string? ConfirmedMediaType = null,
    string? ResultType = null,
    string? ResultId = null
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

    /// <summary>
    /// Records the job dispatched by Confirm plus the confirmed target
    /// (library + TMDB id/type) so a later <see cref="MediaFilesScannedEvent"/>
    /// (published once the ripped file is actually imported into the library)
    /// can be matched back to this session — see
    /// <c>DiscOnboardingCompletionEventHandler</c>.
    /// </summary>
    public DiscOnboardingSession WithJob(
        string jobId,
        Ulid? libraryId = null,
        int? confirmedTmdbId = null,
        string? confirmedMediaType = null
    ) =>
        this with
        {
            JobId = jobId,
            State = DiscOnboardingState.Ripping,
            LibraryId = libraryId ?? LibraryId,
            ConfirmedTmdbId = confirmedTmdbId ?? ConfirmedTmdbId,
            ConfirmedMediaType = confirmedMediaType ?? ConfirmedMediaType,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    public DiscOnboardingSession WithFailure(string reason) =>
        this with
        {
            State = DiscOnboardingState.Failed,
            FailureReason = reason,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// Terminal success transition: the ripped file has been imported and a
    /// real library item exists for it. Only ever reached going forward from
    /// <see cref="DiscOnboardingState.Ripping"/> — an unmatched rip simply
    /// stays in <see cref="DiscOnboardingState.Ripping"/> forever (no timeout
    /// mechanism; see the disc-onboarding completion handler for why).
    /// </summary>
    public DiscOnboardingSession WithCompletion(string resultType, string resultId) =>
        this with
        {
            State = DiscOnboardingState.Complete,
            ResultType = resultType,
            ResultId = resultId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
}
