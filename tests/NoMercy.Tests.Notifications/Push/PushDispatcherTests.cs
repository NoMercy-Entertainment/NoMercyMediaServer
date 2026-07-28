// -----------------------------------------------------------------------------
//  Copyright (c) NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy and is proprietary and confidential.
//  Unauthorized copying, distribution, or use is prohibited. See LICENSE.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Moq;
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class PushDispatcherTests
{
    private static readonly byte[] SealedBytes = [1, 2, 3, 250, 251, 252];

    private static PushSubscriptionKey[] TwoDevices() =>
        [new(1, "BJ1V", "c2Vj"), new(2, "BJ2W", "dGhy")];

    [Fact]
    public async Task DispatchAsync_Encrypts_Once_Per_Subscription()
    {
        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TwoDevices());

        Mock<IWebPushEnvelope> envelope = new();
        envelope
            .Setup(e => e.Seal(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(SealedBytes);

        Mock<IPushRelayClient> relay = new();

        PushDispatcher dispatcher = new(keys.Object, envelope.Object, relay.Object);

        await dispatcher.DispatchAsync(
            "encode-finished",
            new PushPayload("Done", "Idiocracy finished encoding", "/movie/1"),
            "token"
        );

        envelope.Verify(e => e.Seal(It.IsAny<byte[]>(), "BJ1V", "c2Vj"), Times.Once);
        envelope.Verify(e => e.Seal(It.IsAny<byte[]>(), "BJ2W", "dGhy"), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Sends_One_Batch_With_Every_Entry()
    {
        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TwoDevices());

        Mock<IWebPushEnvelope> envelope = new();
        envelope
            .Setup(e => e.Seal(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(SealedBytes);

        Mock<IPushRelayClient> relay = new();
        PushDispatcher dispatcher = new(keys.Object, envelope.Object, relay.Object);

        await dispatcher.DispatchAsync(
            "encode-finished",
            new PushPayload("Done", "body", null),
            "token"
        );

        relay.Verify(
            r =>
                r.DispatchAsync(
                    "encode-finished",
                    It.Is<IReadOnlyList<PushRelayEntry>>(entries => entries.Count == 2),
                    "token",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DispatchAsync_Posts_Base64Url_Of_Exactly_The_Sealed_Bytes()
    {
        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new(1, "BJ1V", "c2Vj")]);

        Mock<IWebPushEnvelope> envelope = new();
        envelope.Setup(e => e.Seal(It.IsAny<byte[]>(), "BJ1V", "c2Vj")).Returns(SealedBytes);

        IReadOnlyList<PushRelayEntry>? captured = null;
        Mock<IPushRelayClient> relay = new();
        relay
            .Setup(r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, IReadOnlyList<PushRelayEntry>, string, CancellationToken>(
                (_, entries, _, _) => captured = entries
            )
            .Returns(Task.CompletedTask);

        PushDispatcher dispatcher = new(keys.Object, envelope.Object, relay.Object);

        await dispatcher.DispatchAsync("encode-finished", new PushPayload("t", "b", null), "token");

        Assert.NotNull(captured);
        PushRelayEntry entry = Assert.Single(captured!);
        Assert.Equal(1, entry.SubscriptionId);

        byte[] decoded = Base64UrlCodec.Decode(entry.Ciphertext);
        Assert.Equal(SealedBytes, decoded);

        // The sealed body carries bytes that are not valid standard-base64
        // alphabet-safe without padding (0xFA/0xFB/0xFC produce '+'/'/' in
        // standard base64) — asserting inequality with Convert.ToBase64String
        // pins that the wire format is base64url, not standard base64.
        Assert.NotEqual(Convert.ToBase64String(SealedBytes), entry.Ciphertext);
    }

    [Fact]
    public async Task DispatchAsync_Sends_Nothing_When_There_Are_No_Devices()
    {
        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<IPushRelayClient> relay = new();
        PushDispatcher dispatcher = new(
            keys.Object,
            new Mock<IWebPushEnvelope>().Object,
            relay.Object
        );

        await dispatcher.DispatchAsync("encode-finished", new PushPayload("t", "b", null), "token");

        relay.Verify(
            r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task DispatchAsync_Swallows_A_Relay_Failure()
    {
        Mock<IPushKeyClient> keys = new();
        keys.Setup(client => client.GetKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TwoDevices());

        Mock<IWebPushEnvelope> envelope = new();
        envelope
            .Setup(e => e.Seal(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns([1]);

        Mock<IPushRelayClient> relay = new();
        relay
            .Setup(r =>
                r.DispatchAsync(
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("relay down"));

        PushDispatcher dispatcher = new(keys.Object, envelope.Object, relay.Object);

        await dispatcher.DispatchAsync("encode-finished", new PushPayload("t", "b", null), "token");
    }
}
