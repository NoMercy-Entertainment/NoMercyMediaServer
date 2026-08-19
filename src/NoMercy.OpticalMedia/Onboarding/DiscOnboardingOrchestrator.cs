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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Events;
using NoMercy.Events.Onboarding;
using NoMercy.NmSystem.Dto;
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
    IEventBus eventBus,
    NoMercyQueue.Core.Interfaces.IJobDispatcher dispatcher,
    IDriveMonitor driveMonitor,
    IDbContextFactory<MediaContext> contextFactory
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

        try
        {
            DiscInfo info = await source.ProbeAsync(drive, ct);
            session = session.WithState(DiscOnboardingState.Identified);
            await PublishAsync(session, ct);

            DiscIdentification identification = await identificationService.IdentifyAsync(info, ct);

            bool autoConfirms = identification.Candidates.Length == 1 && libraryAutoConfirmEnabled;

            session = session.WithCandidates(
                identification.Candidates,
                autoConfirms
                    ? DiscOnboardingState.AutoConfirmed
                    : DiscOnboardingState.AwaitingConfirm
            );
            await PublishAsync(session, ct);

            return session;
        }
        catch (Exception ex)
        {
            session = session.WithFailure(ex.Message);
            await PublishAsync(session, ct);
            return session;
        }
    }

    /// <summary>
    /// Applies the user's chosen candidate (or the auto-confirmed one),
    /// builds a <see cref="RipRequest"/> with <see cref="CustomMetadata"/>
    /// pre-filled from it, and dispatches the existing <see cref="Rip.DiscRipJob"/>.
    /// Because <see cref="RipRequest.Custom"/> is non-null, DiscRipJob's own
    /// post-rip identify branch is skipped entirely — the match was already
    /// made here, before the rip started.
    /// </summary>
    public async Task<DiscOnboardingSession> ConfirmAsync(
        string drivePath,
        DiscCandidate chosen,
        int[] selectedTitleIndices,
        Ulid libraryId,
        Ulid folderId,
        CancellationToken ct
    )
    {
        if (!store.TryGet(drivePath, out DiscOnboardingSession? session) || session is null)
            throw new InvalidOperationException($"No onboarding session for drive {drivePath}");

        if (
            session.State
            is not (DiscOnboardingState.AwaitingConfirm or DiscOnboardingState.AutoConfirmed)
        )
            throw new InvalidOperationException(
                $"Session for drive {drivePath} is in state {session.State}, not awaiting confirmation"
            );

        DiscDrive? drive = driveMonitor
            .GetDrives()
            .FirstOrDefault(d =>
                d.Path.TrimEnd('\\', '/')
                    .Equals(drivePath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
            );
        if (drive is null)
            throw new InvalidOperationException($"No optical drive found at {drivePath}");

        // Re-probe before dispatch: fail fast on DRM the host can't read
        // (mirrors OpticalMediaController.RipDisc's precheck) and, for CD
        // discs, source the full track list when the caller sent none.
        IDiscSource? source = discSourceFactory.CreateFor(drive.DiscType);
        DiscInfo? probe = null;
        if (source is not null)
        {
            probe = await source.ProbeAsync(drive, ct);
            if (probe.Protection is not null)
                throw new InvalidOperationException(
                    $"Cannot rip — disc is {probe.Protection.Kind}-protected: {probe.Protection.Message}"
                );
        }

        int[] resolvedTitleIndices = selectedTitleIndices;
        if (
            drive.DiscType == OpticalDiscType.Cd
            && resolvedTitleIndices.Length == 0
            && probe?.AudioTracks is { Length: > 0 }
        )
            resolvedTitleIndices = probe.AudioTracks.Select(t => t.Index).ToArray();

        await using MediaContext db = await contextFactory.CreateDbContextAsync(ct);
        Library? targetLibrary = await db
            .Libraries.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == libraryId, ct);
        if (targetLibrary is null)
            throw new InvalidOperationException(
                $"LibraryId {libraryId} does not match any library"
            );

        CustomMetadata custom = new(
            Title: chosen.Title,
            Year: chosen.Year,
            Type: chosen.Type ?? MediaType.Movie,
            PosterUrl: chosen.PosterUrl,
            SeasonNumber: chosen.SeasonNumber,
            EpisodeStartNumber: chosen.EpisodeNumber
        );

        RipRequest request = new(
            DrivePath: drivePath,
            SelectedTitleIndices: resolvedTitleIndices,
            MetadataId: chosen.StableId,
            Custom: custom,
            LibraryId: libraryId,
            FolderId: folderId,
            EncodingProfileId: null,
            AudioTracks: [],
            Subtitles: [],
            DiscType: drive.DiscType
        );

        string sanitisedDrive = drivePath
            .TrimEnd('\\', '/')
            .Replace(":", "")
            .Replace(Path.DirectorySeparatorChar, '_');
        string outputDir = Path.Combine(
            NoMercy.NmSystem.Information.AppFiles.TranscodePath,
            "ripper",
            sanitisedDrive
        );

        Rip.DiscRipJob job = new(
            request,
            outputDir,
            folderId,
            libraryId,
            targetLibraryType: targetLibrary.Type
        );
        dispatcher.Dispatch(job, job.QueueName, job.Priority);

        session = session.WithJob(job.JobId);
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
