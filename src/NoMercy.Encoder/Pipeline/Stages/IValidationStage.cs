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

namespace NoMercy.Encoder.Pipeline.Stages;

/// <summary>
/// Named alias for the validation stage contract. Resolving
/// <see cref="IValidationStage"/> from DI returns the same instance as
/// <see cref="ValidateStage"/>. Strategies and plugins that need to
/// swap in a custom validation implementation can register a replacement
/// against this interface without coupling to the concrete class name.
/// </summary>
public interface IValidationStage : IPipelineStage<ValidateInput, ValidateInput> { }
