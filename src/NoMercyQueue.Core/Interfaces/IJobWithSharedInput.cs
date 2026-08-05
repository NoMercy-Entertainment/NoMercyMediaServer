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

namespace NoMercyQueue.Core.Interfaces;

/// <summary>
/// A job whose real input is too big to live in its payload, and is instead
/// stored once under a key that many jobs share.
/// <para>
/// The key is recorded on the queue row rather than left to be dug back out of
/// the payload, so the sweep that reclaims unreferenced input is an indexed
/// anti-join instead of a scan over every payload in the queue.
/// </para>
/// </summary>
public interface IJobWithSharedInput
{
    string? SharedInputKey { get; }
}
