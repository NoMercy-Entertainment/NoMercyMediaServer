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
using NoMercy.Notifications.Push;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class PushDispatcherTests
{
    // 0xFA 0xFB 0xFC 0xFF 0xBF encode to '+' and '/' under the standard
    // alphabet and to '-' and '_' under base64url, so the two encodings of
    // this array differ and a test that decodes it can tell them apart.
    private static readonly byte[] SealedBytes = [1, 2, 3, 250, 251, 252, 255, 191];

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
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DispatchAsync_Forwards_The_Given_Audience_To_The_Relay()
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
            "token",
            "user-ref-abc"
        );

        relay.Verify(
            r =>
                r.DispatchAsync(
                    "encode-finished",
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    "token",
                    "user-ref-abc",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DispatchAsync_Passes_No_Audience_Through_When_The_Caller_Does_Not_Target_One_Person()
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
                    It.IsAny<IReadOnlyList<PushRelayEntry>>(),
                    "token",
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DispatchAsync_Posts_Standard_Base64_Of_Exactly_The_Sealed_Bytes()
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
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, IReadOnlyList<PushRelayEntry>, string, string?, CancellationToken>(
                (_, entries, _, _, _) => captured = entries
            )
            .Returns(Task.CompletedTask);

        PushDispatcher dispatcher = new(keys.Object, envelope.Object, relay.Object);

        await dispatcher.DispatchAsync("encode-finished", new PushPayload("t", "b", null), "token");

        Assert.NotNull(captured);
        PushRelayEntry entry = Assert.Single(captured!);
        Assert.Equal(1, entry.SubscriptionId);

        // The relay decodes with base64_decode($ciphertext, true) — strict,
        // standard alphabet. Decoding the same way here and comparing bytes is
        // the only assertion that fails if the sender ever moves back to
        // base64url, which no test on the relay side can see.
        Assert.NotEqual(Base64UrlCodec.Encode(SealedBytes), Convert.ToBase64String(SealedBytes));

        byte[] decoded = Convert.FromBase64String(entry.Ciphertext);
        Assert.Equal(SealedBytes, decoded);
        Assert.Equal(Convert.ToBase64String(SealedBytes), entry.Ciphertext);
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
                    It.IsAny<string?>(),
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
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("relay down"));

        PushDispatcher dispatcher = new(keys.Object, envelope.Object, relay.Object);

        await dispatcher.DispatchAsync("encode-finished", new PushPayload("t", "b", null), "token");
    }
}
