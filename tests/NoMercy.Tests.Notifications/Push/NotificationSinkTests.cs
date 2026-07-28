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
    public async Task A_Notification_Reaches_Both_The_Hub_And_Push()
    {
        Mock<IPushDispatcher> push = new();
        NotificationSink sink = new(push.Object);

        await sink.NotifyAsync(
            "encode-finished",
            new PushPayload("Done", "Idiocracy finished encoding", "/movie/1"),
            "token"
        );

        push.Verify(
            dispatcher =>
                dispatcher.DispatchAsync(
                    "encode-finished",
                    It.IsAny<PushPayload>(),
                    "token",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
