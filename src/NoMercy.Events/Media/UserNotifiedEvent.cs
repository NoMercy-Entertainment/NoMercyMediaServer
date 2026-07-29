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

namespace NoMercy.Events.Media;

public sealed class UserNotifiedEvent : EventBase
{
    public override string Source => "Media";

    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Type { get; init; }
    public string Hub { get; init; } = "videoHub";

    // Null (default) keeps the existing broadcast-to-everyone behavior. When set,
    // the notification is dispatched to that one user over whichever transport
    // can currently reach them instead of to every connected client.
    public Guid? UserId { get; init; }

    // In-app navigation target a client opens when the notification is tapped.
    public string? Route { get; init; }

    // Backdrop/still and poster paths for the movie, episode or album this
    // notification is about, when the publisher has one. Null for admin
    // free-text broadcasts/sends and any notice with no natural artwork.
    public string? BackdropPath { get; init; }
    public string? PosterPath { get; init; }
}
