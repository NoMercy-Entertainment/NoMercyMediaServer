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

using System.Reflection;

namespace NoMercy.Tests.Cli.Support;

/// <summary>
/// Invokes private static members of the internal CLI command classes. Several
/// requirements (server-discovery probing in <c>StartCommand</c>, the exit-wait
/// loop in <c>UpdateCommand</c>) live in private helpers with no public seam, but
/// they are pure/deterministic given a controllable dependency or filesystem
/// state, so exercising them directly through reflection is the real code path —
/// not a mock of the unit under test.
/// </summary>
internal static class PrivateReflection
{
    public static T? InvokeStatic<T>(Type type, string methodName, params object?[] args)
    {
        MethodInfo method =
            type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(type.FullName, methodName);

        return (T?)method.Invoke(null, args);
    }

    public static async Task<T?> InvokeStaticAsync<T>(
        Type type,
        string methodName,
        params object?[] args
    )
    {
        MethodInfo method =
            type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(type.FullName, methodName);

        object? result = method.Invoke(null, args);
        return result is Task<T> task ? await task : default;
    }

    public static void ResetStaticField(Type type, string fieldName, object? value)
    {
        FieldInfo field =
            type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(type.FullName, fieldName);

        field.SetValue(null, value);
    }
}
