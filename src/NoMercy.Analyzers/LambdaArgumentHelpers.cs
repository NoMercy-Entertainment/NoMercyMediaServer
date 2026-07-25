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

namespace NoMercy.Analyzers;

/// <summary>
/// Shared lambda-shape helpers for <see cref="CallbackParameterAnalyzer"/>.
/// </summary>
internal static class LambdaArgumentHelpers
{
    /// <summary>
    /// A one-character parameter name. The discard <c>_</c> is excluded: it already
    /// says "this value is deliberately unused", which is the clarity the rule wants.
    /// </summary>
    internal static bool IsSingleLetter(string name) => name.Length == 1 && name != "_";
}
