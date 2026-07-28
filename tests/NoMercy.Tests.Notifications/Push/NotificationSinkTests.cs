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

public class NotificationSinkTests
{
    [Fact]
    public void A_Notification_Is_Handed_To_The_Queue_Under_Its_Channel_Slug()
    {
        Mock<IPushDispatchQueue> queue = new();
        NotificationSink sink = new(queue.Object);

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

    /// <summary>
    /// Both entry points hand off to the same queue. A per-user notification
    /// that dispatched inline would put the relay's HTTP round trip on the
    /// event publisher's thread, which is exactly what the queue exists to
    /// keep it off.
    /// </summary>
    [Fact]
    public void A_User_Notification_Is_Handed_To_The_Same_Queue_Carrying_Its_Target()
    {
        Guid userId = Guid.NewGuid();
        Mock<IPushDispatchQueue> queue = new();
        NotificationSink sink = new(queue.Object);

        sink.NotifyUser(
            userId,
            "musicHub",
            "user-notification",
            new PushPayload("Title", "Body", "/movie/1", "info"),
            "token"
        );

        queue.Verify(
            q =>
                q.Enqueue(
                    It.Is<PushDispatchRequest>(request =>
                        request.Channel == "user-notification"
                        && request.UserId == userId
                        && request.Hub == "musicHub"
                        && request.Payload.Route == "/movie/1"
                        && request.Payload.Category == "info"
                    )
                ),
            Times.Once
        );
    }
}
