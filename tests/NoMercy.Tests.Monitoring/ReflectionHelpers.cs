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

namespace NoMercy.Tests.Monitoring;

/// <summary>
/// Invokes private members of the OS-specific resource providers directly.
/// <see cref="NoMercy.Monitoring.LinuxResourceProvider"/> and
/// <see cref="NoMercy.Monitoring.WindowsResourceProvider"/> keep their parsing
/// helpers <c>private</c> on purpose (no production caller outside the class
/// needs them) so reflection — not a widened access modifier and not a mock of
/// the OS — is the seam that lets this assembly demand their behaviour
/// directly, matching the pattern already used in
/// <c>NoMercy.Tests.Encoder.LiveTranscode.LiveRuntimeSessionTests</c>.
/// </summary>
internal static class ReflectionHelpers
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    public static object? InvokeStatic(Type type, string methodName, params object?[] args)
    {
        MethodInfo method =
            type.GetMethod(methodName, PrivateStatic)
            ?? throw new MissingMethodException(type.FullName, methodName);
        return method.Invoke(null, args);
    }

    public static object? InvokeInstance(object instance, string methodName, params object?[] args)
    {
        MethodInfo method =
            instance.GetType().GetMethod(methodName, PrivateInstance)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return method.Invoke(instance, args);
    }

    public static void SetField(object instance, string fieldName, object? value)
    {
        FieldInfo field =
            instance.GetType().GetField(fieldName, PrivateInstance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }

    public static T GetField<T>(object instance, string fieldName)
    {
        FieldInfo field =
            instance.GetType().GetField(fieldName, PrivateInstance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)field.GetValue(instance)!;
    }

    public static T GetField<T>(Type type, string fieldName)
    {
        FieldInfo field =
            type.GetField(fieldName, PrivateStatic)
            ?? throw new MissingFieldException(type.FullName, fieldName);
        return (T)field.GetValue(null)!;
    }

    /// <summary>
    /// Builds an instance of a private nested record/class via its declared constructor
    /// (positional records included), by parameter count — every nested type reflected
    /// here declares exactly one constructor.
    /// </summary>
    public static object CreateNested(Type outerType, string nestedTypeName, params object?[] args)
    {
        Type nestedType =
            outerType.GetNestedType(nestedTypeName, BindingFlags.NonPublic)
            ?? throw new MissingMemberException(outerType.FullName, nestedTypeName);

        ConstructorInfo constructor =
            nestedType
                .GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                )
                .First(c => c.GetParameters().Length == args.Length)
            ?? throw new MissingMethodException(nestedType.FullName, ".ctor");

        return constructor.Invoke(args);
    }
}
