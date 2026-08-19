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

using NoMercy.Events;
using NoMercy.Events.Onboarding;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Sources;

namespace NoMercy.OpticalMedia.Onboarding;

/// <summary>
/// Orchestrates the existing probe/identify primitives into one
/// server-owned <see cref="DiscOnboardingSession"/> per drive, and applies
/// the auto-confirm rule: a session auto-confirms only when identification
/// returned exactly one candidate AND the target library has
/// <c>AutoConfirmDiscMatches</c> enabled. Any other outcome — no candidates,
/// competing candidates, or the library flag off — waits at
/// <see cref="DiscOnboardingState.AwaitingConfirm"/> for an explicit confirm.
/// Does not probe, identify, or rip itself — delegates to
/// <see cref="DiscSourceFactory"/>, <see cref="DiscIdentificationService"/>,
/// and (for confirm/rip, added in a later task) the existing
/// <c>DiscRipJob</c> dispatch path.
/// </summary>
public sealed class DiscOnboardingOrchestrator(
    DiscSourceFactory discSourceFactory,
    DiscIdentificationService identificationService,
    DiscOnboardingSessionStore store,
    IEventBus eventBus
)
{
    public async Task<DiscOnboardingSession> StartAsync(
        DiscDrive drive,
        bool libraryAutoConfirmEnabled,
        CancellationToken ct
    )
    {
        DiscOnboardingSession session = DiscOnboardingSession.Create(drive.Path);
        await PublishAsync(session, ct);

        session = session.WithState(DiscOnboardingState.Probing);
        await PublishAsync(session, ct);

        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        if (source is null)
        {
            session = session.WithFailure($"No disc reader registered for {drive.DiscType}");
            await PublishAsync(session, ct);
            return session;
        }

        DiscInfo info = await source.ProbeAsync(drive, ct);
        session = session.WithState(DiscOnboardingState.Identified);

        DiscIdentification identification = await identificationService.IdentifyAsync(info, ct);

        bool autoConfirms = identification.Candidates.Length == 1 && libraryAutoConfirmEnabled;

        session = session.WithCandidates(
            identification.Candidates,
            autoConfirms ? DiscOnboardingState.AutoConfirmed : DiscOnboardingState.AwaitingConfirm
        );
        await PublishAsync(session, ct);

        return session;
    }

    private async Task PublishAsync(DiscOnboardingSession session, CancellationToken ct)
    {
        store.Set(session);
        await eventBus.PublishAsync(
            new DiscOnboardingStateChangedEvent
            {
                StateData = DiscOnboardingStatePayload.From(session),
            },
            ct
        );
    }
}
