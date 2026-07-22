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
            type.GetMethod(name: methodName, bindingAttr: PrivateStatic)
            ?? throw new MissingMethodException(className: type.FullName, methodName: methodName);
        return method.Invoke(obj: null, parameters: args);
    }

    public static object? InvokeInstance(object instance, string methodName, params object?[] args)
    {
        MethodInfo method =
            instance.GetType().GetMethod(name: methodName, bindingAttr: PrivateInstance)
            ?? throw new MissingMethodException(className: instance.GetType().FullName, methodName: methodName);
        return method.Invoke(obj: instance, parameters: args);
    }

    public static void SetField(object instance, string fieldName, object? value)
    {
        FieldInfo field =
            instance.GetType().GetField(name: fieldName, bindingAttr: PrivateInstance)
            ?? throw new MissingFieldException(className: instance.GetType().FullName, fieldName: fieldName);
        field.SetValue(obj: instance, value: value);
    }

    public static T GetField<T>(object instance, string fieldName)
    {
        FieldInfo field =
            instance.GetType().GetField(name: fieldName, bindingAttr: PrivateInstance)
            ?? throw new MissingFieldException(className: instance.GetType().FullName, fieldName: fieldName);
        return (T)field.GetValue(obj: instance)!;
    }

    public static T GetField<T>(Type type, string fieldName)
    {
        FieldInfo field =
            type.GetField(name: fieldName, bindingAttr: PrivateStatic)
            ?? throw new MissingFieldException(className: type.FullName, fieldName: fieldName);
        return (T)field.GetValue(obj: null)!;
    }

    /// <summary>
    /// Builds an instance of a private nested record/class via its declared constructor
    /// (positional records included), by parameter count — every nested type reflected
    /// here declares exactly one constructor.
    /// </summary>
    public static object CreateNested(Type outerType, string nestedTypeName, params object?[] args)
    {
        Type nestedType =
            outerType.GetNestedType(name: nestedTypeName, bindingAttr: BindingFlags.NonPublic)
            ?? throw new MissingMemberException(className: outerType.FullName, memberName: nestedTypeName);

        ConstructorInfo constructor =
            nestedType
                .GetConstructors(
                    bindingAttr: BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                )
                .First(predicate: c => c.GetParameters().Length == args.Length)
            ?? throw new MissingMethodException(className: nestedType.FullName, methodName: ".ctor");

        return constructor.Invoke(parameters: args);
    }
}
