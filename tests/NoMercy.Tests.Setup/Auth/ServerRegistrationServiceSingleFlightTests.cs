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

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Networking.Certificate;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Status;
using NoMercy.Setup.Auth;
using Xunit;

namespace NoMercy.Tests.Setup.Auth;

/// <summary>
/// Init() used to attempt a non-blocking lock and return early for a "loser"
/// caller while a concurrent registration was already running — that loser
/// advanced its OWN caller (BootOrchestrator / the setup wizard / the
/// degraded-mode retry loop) to Registered, and checked for a certificate,
/// before the winner's attempt (including the certificate renewal at the end)
/// had actually finished. The fix shares the SAME in-flight Task across every
/// concurrent caller instead, so nobody can observe a false "done" ahead of
/// the real completion.
///
/// These tests exercise the gating logic only — RegisterServer/AssignServer
/// make a real outbound HTTP call with no injectable seam, so the shared Task
/// itself is expected to fail in this offline test environment. That failure
/// is irrelevant to what's being verified: that concurrent callers share one
/// Task reference, and that the pre-existing cooldown gate still runs before
/// the single-flight logic.
/// </summary>
public class ServerRegistrationServiceSingleFlightTests
{
    private static ServerRegistrationService MakeService()
    {
        Mock<IAuthTokenStore> authTokenStore = new();
        authTokenStore.Setup(expression: a => a.AccessToken).Returns(value: "test-access-token");

        Mock<IDbContextFactory<AppDbContext>> dbContextFactory = new();
        // Deliberately unconfigured: GetDeviceName() catches any failure here
        // and falls back to Environment.MachineName, so no working EF context
        // is needed for this gating test.

        Mock<IUserProvisioningService> userProvisioning = new();

        Mock<IConnectivityStatus> connectivity = new();
        connectivity.Setup(expression: c => c.StunPublicIp).Returns(value: (string?)null);
        connectivity.Setup(expression: c => c.StunPublicPort).Returns(value: (int?)null);
        connectivity.Setup(expression: c => c.NatStatus).Returns(value: NatStatus.None);

        Mock<ICertificateService> certificateService = new();

        return new(
            authTokenStore: authTokenStore.Object,
            appDbContextFactory: dbContextFactory.Object,
            userProvisioningService: userProvisioning.Object,
            connectivityStatus: connectivity.Object,
            certificateService: certificateService.Object
        );
    }

    private static void SetLastFailureUtc(ServerRegistrationService service, DateTime value)
    {
        FieldInfo field = typeof(ServerRegistrationService).GetField(
            name: "_lastFailureUtc",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        )!;
        field.SetValue(obj: service, value: value);
    }

    [Fact]
    public void Init_ConcurrentCalls_ShareTheSameInFlightTask()
    {
        ServerRegistrationService service = MakeService();

        Task first = service.Init(maxRetries: 1);
        Task second = service.Init(maxRetries: 1);

        first.Should().BeSameAs(expected: second);

        // The real HTTP call will fail in this offline test environment —
        // observe the fault so it never surfaces as an unobserved task
        // exception once this background Task is garbage collected.
        _ = first.ContinueWith(continuationFunction: t => _ = t.Exception, continuationOptions: TaskContinuationOptions.ExecuteSynchronously);
    }

    [Fact]
    public void Init_ThirdConcurrentCall_AlsoJoinsTheSameInFlightTask()
    {
        ServerRegistrationService service = MakeService();

        Task first = service.Init(maxRetries: 1);
        Task second = service.Init(maxRetries: 1);
        Task third = service.Init(maxRetries: 1);

        first.Should().BeSameAs(expected: second);
        second.Should().BeSameAs(expected: third);

        _ = first.ContinueWith(continuationFunction: t => _ = t.Exception, continuationOptions: TaskContinuationOptions.ExecuteSynchronously);
    }

    [Fact]
    public void Init_DuringCooldown_ThrowsBeforeCreatingAnyInFlightTask()
    {
        // Regression guard: the cooldown gate must still run BEFORE the
        // single-flight check — a cooling-down service must not silently
        // join (or start) a registration attempt.
        ServerRegistrationService service = MakeService();
        SetLastFailureUtc(service: service, value: DateTime.UtcNow);

        Action act = () => service.Init(maxRetries: 1);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*cooldown*");
    }
}
