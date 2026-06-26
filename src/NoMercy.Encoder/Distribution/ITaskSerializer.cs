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

namespace NoMercy.Encoder.Distribution;

/// <summary>
/// Wire format for <see cref="EncodeTask"/> payloads sent between
/// coordinator and remote workers. Mirrors <see cref="Jobs.IJobSerializer"/>
/// — HMAC-SHA256 signed envelope with a short-lived timestamp so replay
/// attacks and tampered FFmpeg arguments are caught at the worker before
/// anything spawns.
/// </summary>
public interface ITaskSerializer
{
    string Serialize(EncodeTask task, byte[] signingKey);

    EncodeTask? Deserialize(string payload, byte[] signingKey);

    string SerializeResult(DispatchResult result, byte[] signingKey);

    DispatchResult? DeserializeResult(string payload, byte[] signingKey);
}
