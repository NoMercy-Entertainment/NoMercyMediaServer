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

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NoMercy.Analyzers;

/// <summary>
/// Shared lambda-shape helpers. <see cref="CallbackParameterAnalyzer"/> and
/// <see cref="NamedArgumentsAnalyzer"/> both key off "callback lambda with a
/// single-letter parameter" and must agree on what that means, so the test lives
/// in one place.
/// </summary>
internal static class LambdaArgumentHelpers
{
    /// <summary>
    /// A one-character parameter name. The discard <c>_</c> is excluded: it already
    /// says "this value is deliberately unused", which is the clarity the rule wants.
    /// </summary>
    internal static bool IsSingleLetter(string name) => name.Length == 1 && name != "_";

    /// <summary>
    /// True when <paramref name="expression"/> is a lambda that names at least one of
    /// its parameters with a single letter.
    /// </summary>
    internal static bool HasSingleLetterParameter(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case SimpleLambdaExpressionSyntax simple:
                return IsSingleLetter(simple.Parameter.Identifier.Text);

            case ParenthesizedLambdaExpressionSyntax parenthesized:
                foreach (ParameterSyntax parameter in parenthesized.ParameterList.Parameters)
                {
                    if (IsSingleLetter(parameter.Identifier.Text))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }
}
