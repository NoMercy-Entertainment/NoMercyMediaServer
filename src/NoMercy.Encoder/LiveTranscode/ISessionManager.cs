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

namespace NoMercy.Encoder.LiveTranscode;

public interface ISessionManager
{
    IReadOnlyList<ILiveSession> ActiveSessions { get; }
    bool CanStartSession(string? userId = null);
    void RegisterSession(ILiveSession session, string? userId = null);
    void RemoveSession(string sessionId);
    int ActiveSessionCount { get; }

    /// <summary>
    /// Returns the user id that owns <paramref name="sessionId"/>, or null when
    /// the session was registered anonymously or does not exist.
    /// </summary>
    string? GetOwnerUserId(string sessionId);
}
