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

using System.Collections.Concurrent;

namespace NoMercy.OpticalMedia.Onboarding;

/// <summary>
/// Singleton in-memory registry of the active <see cref="DiscOnboardingSession"/>
/// per drive, keyed the same way <see cref="Drives.DriveLockRegistry"/> keys
/// its locks: normalised (trimmed separator, case-insensitive) drive path.
/// One session per drive at a time — a new <see cref="Set"/> for a drive
/// path overwrites whatever session was there before.
/// </summary>
public sealed class DiscOnboardingSessionStore
{
    private readonly ConcurrentDictionary<string, DiscOnboardingSession> _sessions = new(
        StringComparer.OrdinalIgnoreCase
    );

    public bool TryGet(string drivePath, out DiscOnboardingSession? session) =>
        _sessions.TryGetValue(Normalise(drivePath), out session);

    public void Set(DiscOnboardingSession session) =>
        _sessions[Normalise(session.DrivePath)] = session;

    // TODO: unused — needs a call site once session-lifecycle/eject-handling is designed.
    public void Remove(string drivePath) => _sessions.TryRemove(Normalise(drivePath), out _);

    private static string Normalise(string drivePath) => drivePath.TrimEnd('\\', '/');
}
