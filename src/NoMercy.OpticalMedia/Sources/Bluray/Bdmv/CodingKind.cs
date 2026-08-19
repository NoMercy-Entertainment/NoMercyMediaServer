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

namespace NoMercy.DiscFormat.Disc.Bdmv;

/// Which attribute layout a BDAV stream coding type uses on the wire — the three branches of
/// libbluray clpi_parse.c:_parse_stream_attr / mpls_parse.c:_parse_stream_attributes.
public enum CodingKind
{
    Video,
    Audio,
    Graphics,
    Text,
}
