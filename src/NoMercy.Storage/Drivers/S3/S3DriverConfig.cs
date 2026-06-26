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

namespace NoMercy.Storage.Drivers.S3;

/// <summary>
/// Parsed representation of the JSON <c>DriverConfig</c> for S3-compatible
/// folder drivers (AWS S3, Cloudflare R2, MinIO, DigitalOcean Spaces, …).
/// </summary>
internal sealed record S3DriverConfig(
    string Bucket,
    string Region,
    string? Prefix,
    string? CredentialsRef,
    string? Endpoint
);
