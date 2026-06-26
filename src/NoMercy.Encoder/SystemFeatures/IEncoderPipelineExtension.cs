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

using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.SystemFeatures;

public interface IEncoderPipelineExtension
{
    string Name { get; }

    string Version { get; }

    PipelineHook[] GetHooks();
}

public record PipelineHook(
    PipelineStagePosition Position,
    string TargetStage,
    IPipelineStage Stage
);

public enum PipelineStagePosition
{
    Before,
    After,
    Replace,
}
