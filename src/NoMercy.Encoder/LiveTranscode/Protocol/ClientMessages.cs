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

namespace NoMercy.Encoder.LiveTranscode.Protocol;

public record RequestSeekMessage(double PositionSeconds);

public record RequestQualityMessage(string? QualityId);

public record ReportPositionMessage(double CurrentTimeSeconds);

public record RequestPauseMessage;

public record RequestResumeMessage;

public record HeartbeatMessage;

public record EndSessionMessage;
