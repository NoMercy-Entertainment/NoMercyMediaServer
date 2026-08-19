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

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Events;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Metadata;
using NoMercy.OpticalMedia.Onboarding;
using NoMercy.OpticalMedia.Rip;
using NoMercy.OpticalMedia.Sources;
using NoMercyQueue.Core.Interfaces;
using Xunit;

namespace NoMercy.Tests.OpticalMedia.Onboarding;

[Trait("Category", "Unit")]
public class DiscOnboardingOrchestratorConfirmTests
{
    [Fact]
    public async Task ConfirmAsync_DispatchesRipJobWithChosenCandidateAsCustomMetadata_AndTransitionsToRipping()
    {
        DiscOnboardingSessionStore store = new();
        store.Set(
            DiscOnboardingSession
                .Create("D:\\")
                .WithCandidates(
                    [new("tmdb", "27205", "Inception", 2010, null, null, 0.6)],
                    DiscOnboardingState.AwaitingConfirm
                )
        );
        Mock<IJobDispatcher> dispatcher = new();
        RipRequest? dispatchedRequest = null;
        dispatcher
            .Setup(d => d.Dispatch(It.IsAny<DiscRipJob>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback<object, string, int>(
                (job, _, _) => dispatchedRequest = ((DiscRipJob)job).Request
            );

        DiscOnboardingOrchestrator orchestrator = new(
            discSourceFactory: null!,
            identificationService: null!,
            store,
            Mock.Of<IEventBus>(),
            dispatcher.Object
        );

        DiscCandidate chosen = new("tmdb", "27205", "Inception", 2010, null, null, 0.6);
        Ulid libraryId = Ulid.NewUlid();
        Ulid folderId = Ulid.NewUlid();

        DiscOnboardingSession result = await orchestrator.ConfirmAsync(
            "D:\\",
            chosen,
            [1],
            libraryId,
            folderId,
            CancellationToken.None
        );

        result.State.Should().Be(DiscOnboardingState.Ripping);
        result.JobId.Should().NotBeNullOrEmpty();
        dispatchedRequest.Should().NotBeNull();
        dispatchedRequest!.Custom.Should().NotBeNull();
        dispatchedRequest.Custom!.Title.Should().Be("Inception");
        dispatchedRequest.LibraryId.Should().Be(libraryId);
        dispatchedRequest.FolderId.Should().Be(folderId);
    }

    [Fact]
    public async Task ConfirmAsync_UnknownDrive_ThrowsInvalidOperationException()
    {
        DiscOnboardingOrchestrator orchestrator = new(
            discSourceFactory: null!,
            identificationService: null!,
            new DiscOnboardingSessionStore(),
            Mock.Of<IEventBus>(),
            Mock.Of<IJobDispatcher>()
        );

        Func<Task> act = () =>
            orchestrator.ConfirmAsync(
                "Z:\\",
                new("tmdb", "1", "X", null, null, null, 1.0),
                [1],
                Ulid.NewUlid(),
                Ulid.NewUlid(),
                CancellationToken.None
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
