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

using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NoMercy.Storage.Analyzers;

/// <summary>
/// Shared syntax-matching helpers used by the NoMercy.Storage diagnostic analyzers.
/// </summary>
internal static class AnalyzerSyntaxHelpers
{
    /// <summary>
    /// Returns the simple name being invoked, whether reached through member access
    /// (<c>Path.Combine(...)</c>) or a bare identifier introduced by a static import
    /// (<c>using static System.IO.Path;</c> followed by <c>Combine(...)</c>). Any other
    /// invocation shape (indexers, delegate-returning expressions, etc.) is intentionally
    /// left unresolved and returns null.
    /// </summary>
    internal static SimpleNameSyntax? GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            SimpleNameSyntax simpleName => simpleName,
            _ => null,
        };
    }

    /// <summary>
    /// Returns true when <paramref name="candidate"/> equals <paramref name="target"/> or is
    /// a dot-delimited descendant of it (e.g. <c>NoMercy.Storage.Drivers.Local</c> is a
    /// descendant of <c>NoMercy.Storage.Drivers</c>). A plain prefix comparison would also
    /// match unrelated sibling namespaces that merely share the same leading characters
    /// (e.g. <c>NoMercy.Storage.DriversLegacy</c> is not a descendant of
    /// <c>NoMercy.Storage.Drivers</c>), which would wrongly exempt or include them.
    /// </summary>
    internal static bool IsNamespaceOrDescendant(string candidate, string target)
    {
        if (string.Equals(candidate, target, StringComparison.Ordinal))
        {
            return true;
        }

        return candidate.Length > target.Length
            && candidate[target.Length] == '.'
            && candidate.StartsWith(target, StringComparison.Ordinal);
    }
}
