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

// EncodeMode is a single concept (single-pass vs two-pass ffmpeg run). Profile
// land declares it; codec land consumes it. There used to be two identical enums
// in two namespaces with an (EncodeMode)(int) cross-cast bridging them — which
// silently broke the moment anyone added a value to one side. This alias keeps
// the historical NoMercy.Encoder.Codecs.EncodeMode name working while collapsing
// both to a single declaration in NoMercy.Encoder.Profiles.
global using EncodeMode = NoMercy.Encoder.Profiles.EncodeMode;
