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
using NoMercy.Notifications.Transports;
using Xunit;

namespace NoMercy.Tests.Notifications.Push;

public class NotificationSinkTests
{
    [Fact]
    public void A_Notification_Is_Handed_To_The_Queue_Under_Its_Channel_Slug()
    {
        Mock<IPushDispatchQueue> queue = new();
        NotificationSink sink = new(queue.Object, new NotificationDispatcher([]));

        sink.Notify(
            "encode-finished",
            new PushPayload("Done", "Idiocracy finished encoding", "/movie/1"),
            "token"
        );

        queue.Verify(
            q =>
                q.Enqueue(
                    It.Is<PushDispatchRequest>(request =>
                        request.Channel == "encode-finished"
                        && request.AccessToken == "token"
                        && request.Payload.Body == "Idiocracy finished encoding"
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task NotifyUserAsync_HandsTheNotificationToTheDispatcher()
    {
        Guid userId = Guid.NewGuid();
        Mock<INotificationTransport> transport = new();
        transport.SetupGet(t => t.Name).Returns("SignalR");
        transport
            .Setup(t => t.CanReachAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        UserNotification? delivered = null;
        transport
            .Setup(t => t.DeliverAsync(It.IsAny<UserNotification>(), It.IsAny<CancellationToken>()))
            .Callback<UserNotification, CancellationToken>(
                (notification, _) => delivered = notification
            )
            .Returns(Task.CompletedTask);

        NotificationSink sink = new(
            new Mock<IPushDispatchQueue>().Object,
            new NotificationDispatcher([transport.Object])
        );

        await sink.NotifyUserAsync(
            userId,
            "user-notification",
            new PushPayload("Title", "Body", null),
            CancellationToken.None
        );

        Assert.NotNull(delivered);
        Assert.Equal(userId, delivered!.UserId);
        Assert.Equal("user-notification", delivered.Channel);
    }
}
