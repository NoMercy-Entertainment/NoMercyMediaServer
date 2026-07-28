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

using Moq;
using NoMercy.NmSystem.Auth;
using NoMercy.Notifications.Push;
using NoMercy.Notifications.Transports;
using Xunit;

namespace NoMercy.Tests.Notifications.Transports;

public class PushNotificationTransportTests
{
    private static readonly byte[] SealedBytes = [9, 8, 7];

    private static Mock<IAuthTokenStore> TokenStore(string accessToken = "token")
    {
        Mock<IAuthTokenStore> tokenStore = new();
        tokenStore.Setup(t => t.AccessToken).Returns(accessToken);
        return tokenStore;
    }

    private static Mock<IWebPushEnvelope> Envelope()
    {
        Mock<IWebPushEnvelope> envelope = new();
        envelope
            .Setup(e => e.Seal(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(SealedBytes);
        return envelope;
    }

    [Fact]
    public async Task CanReachAsync_UserWithNoSubscription_IsNotReachable_AndMakesNoHttpCall()
    {
        Guid otherUser = Guid.NewGuid();

        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(1, "p", "a", "ref-other", otherUser)]);

        Mock<IPushRelayClient> relay = new();
        PushNotificationTransport transport = new(
            keys.Object,
            Envelope().Object,
            relay.Object,
            TokenStore().Object
        );

        bool reachable = await transport.CanReachAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(reachable);
        relay.Verify(
            r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CanReachAsync_UserWithASubscription_IsReachable()
    {
        Guid userId = Guid.NewGuid();

        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(1, "p", "a", "ref-a", userId)]);

        PushNotificationTransport transport = new(
            keys.Object,
            Envelope().Object,
            new Mock<IPushRelayClient>().Object,
            TokenStore().Object
        );

        bool reachable = await transport.CanReachAsync(userId, CancellationToken.None);

        Assert.True(reachable);
    }

    [Fact]
    public async Task DeliverAsync_DispatchesWithTheTargetUsersRefAsAudience_AndOnlyTheirEntries()
    {
        Guid targetUser = Guid.NewGuid();
        Guid otherUser = Guid.NewGuid();

        PushSubscriptionKey[] allKeys =
        [
            new(1, "p1", "a1", "ref-target", targetUser),
            new(2, "p2", "a2", "ref-target", targetUser),
            new(3, "p3", "a3", "ref-other", otherUser),
        ];

        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync("token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(allKeys);

        IReadOnlyList<PushRelayEntry>? capturedEntries = null;
        string? capturedAudience = null;

        Mock<IPushRelayClient> relay = new();
        relay
            .Setup(r =>
                r.DispatchAsync(
                    "encode-finished",
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    "token",
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, IReadOnlyList<PushRelayEntry>, string, string?, CancellationToken>(
                (_, entries, _, audience, _) =>
                {
                    capturedEntries = entries;
                    capturedAudience = audience;
                }
            )
            .Returns(Task.CompletedTask);

        PushNotificationTransport transport = new(
            keys.Object,
            Envelope().Object,
            relay.Object,
            TokenStore().Object
        );

        UserNotification notification = new(
            targetUser,
            "encode-finished",
            new PushPayload("Done", "body", null)
        );

        await transport.DeliverAsync(notification, CancellationToken.None);

        Assert.Equal("ref-target", capturedAudience);
        Assert.NotNull(capturedEntries);
        Assert.Equal(2, capturedEntries!.Count);
        Assert.Equal(
            [1L, 2L],
            capturedEntries.Select(entry => entry.SubscriptionId).OrderBy(id => id).ToArray()
        );
    }

    [Fact]
    public async Task DeliverAsync_UnreachableUser_DispatchesNothing()
    {
        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<IPushRelayClient> relay = new();
        PushNotificationTransport transport = new(
            keys.Object,
            Envelope().Object,
            relay.Object,
            TokenStore().Object
        );

        await transport.DeliverAsync(
            new(Guid.NewGuid(), "encode-finished", new PushPayload("Done", "body", null)),
            CancellationToken.None
        );

        relay.Verify(
            r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    /// <summary>
    /// The fail-safe: a matched user with no UserRef must never fall back to
    /// an unfiltered dispatch, because an absent audience broadcasts to every
    /// subscriber on the channel rather than narrowing to nobody.
    /// </summary>
    [Fact]
    public async Task DeliverAsync_MatchedUserWithNoUserRef_DispatchesNothing_RatherThanBroadcasting()
    {
        Guid targetUser = Guid.NewGuid();

        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(1, "p", "a", null, targetUser)]);

        Mock<IPushRelayClient> relay = new();
        PushNotificationTransport transport = new(
            keys.Object,
            Envelope().Object,
            relay.Object,
            TokenStore().Object
        );

        await transport.DeliverAsync(
            new(targetUser, "encode-finished", new PushPayload("Done", "body", null)),
            CancellationToken.None
        );

        relay.Verify(
            r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
