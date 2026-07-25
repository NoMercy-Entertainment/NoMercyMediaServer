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
using Newtonsoft.Json;

namespace NoMercy.Database.Models.Users;

[PrimaryKey(nameof(NotificationId), nameof(UserId))]
[Index(nameof(NotificationId))]
[Index(nameof(UserId))]
public class NotificationUser
{
    [JsonProperty("notification_id")]
    public Ulid NotificationId { get; set; }
    public Notification Notification { get; set; } = null!;

    [JsonProperty("user_id")]
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationUser()
    {
        //
    }

    public NotificationUser(Ulid notificationId, Guid userId)
    {
        NotificationId = notificationId;
        UserId = userId;
    }
}
