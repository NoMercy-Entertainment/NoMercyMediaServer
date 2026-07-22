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
using System.Runtime.CompilerServices;
using NoMercy.Providers.NoMercy.Client;

namespace NoMercy.Tests.Providers.NoMercy.Client;

/// <summary>
/// PROV-H16: Tests verifying that NoMercyImageClient.Download reads the response
/// content exactly once. The bug: ReadAsByteArrayAsync consumed the content for
/// file writing, then ReadAsStreamAsync re-read the already-consumed response
/// content — producing corrupt/empty images.
/// The fix: Read content once as byte[] and use it for both file writing and image loading.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class NoMercyImageClientTests
{
    private static MethodInfo GetDownloadMethod()
    {
        MethodInfo? method = typeof(NoMercyImageClient).GetMethod(
            name: "Download",
            bindingAttr: BindingFlags.Public | BindingFlags.Static
        );

        Assert.NotNull(@object: method);
        return method;
    }

    /// <summary>
    /// The local async function Task() inside Download is compiled into a
    /// compiler-generated state machine nested type. Find it by looking for
    /// nested types with AsyncStateMachineAttribute on their MoveNext.
    /// </summary>
    private static (Type StateMachineType, MethodInfo MoveNext) GetLocalFunctionStateMachine()
    {
        // The compiler generates a display class (e.g., <>c__DisplayClass0_0)
        // containing the local function, which itself has a state machine.
        // We search all nested types (including nested-of-nested) for one
        // that has MoveNext and IAsyncStateMachine.
        Type[] allNested = typeof(NoMercyImageClient).GetNestedTypes(
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Public
        );

        foreach (Type nested in allNested)
        {
            // Check nested types within the display class
            Type[] deepNested = nested.GetNestedTypes(bindingAttr: BindingFlags.NonPublic | BindingFlags.Public);

            foreach (Type deep in deepNested)
            {
                if (typeof(IAsyncStateMachine).IsAssignableFrom(c: deep))
                {
                    MethodInfo? moveNext = deep.GetMethod(
                        name: "MoveNext",
                        bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (moveNext != null)
                        return (deep, moveNext);
                }
            }

            // Also check the nested type itself
            if (typeof(IAsyncStateMachine).IsAssignableFrom(c: nested))
            {
                MethodInfo? moveNext = nested.GetMethod(
                    name: "MoveNext",
                    bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (moveNext != null)
                    return (nested, moveNext);
            }
        }

        throw new InvalidOperationException(
            message: "Could not find async state machine for the local Task() function in NoMercyImageClient.Download"
        );
    }

    private static List<string> GetCalledMethodNames(MethodInfo moveNext)
    {
        byte[] ilBytes = moveNext.GetMethodBody()!.GetILAsByteArray()!;
        Module module = moveNext.DeclaringType!.Module;
        List<string> names = [];

        for (int i = 0; i < ilBytes.Length; i++)
        {
            if ((ilBytes[i] == 0x28 || ilBytes[i] == 0x6F) && i + 4 < ilBytes.Length)
            {
                int token = BitConverter.ToInt32(value: ilBytes, startIndex: i + 1);
                try
                {
                    MethodBase? calledMethod = module.ResolveMethod(metadataToken: token);
                    if (calledMethod != null)
                        names.Add(item: calledMethod.Name);
                }
                catch (Exception)
                {
                    // Token may not resolve — skip
                }
            }
        }

        return names;
    }

    [Fact]
    public void Download_IsStaticAndReturnsTask()
    {
        MethodInfo method = GetDownloadMethod();

        Assert.True(condition: method.IsStatic, userMessage: "Download should be a static method");
        Assert.True(condition: method.ReturnType.IsGenericType);
        Assert.Equal(expected: typeof(Task<>), actual: method.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void Download_AcceptsStringPathAndOptionalBoolParameters()
    {
        MethodInfo method = GetDownloadMethod();
        ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(expected: 3, actual: parameters.Length);
        Assert.Equal(expected: typeof(string), actual: parameters[0].ParameterType);
        Assert.Equal(expected: "path", actual: parameters[0].Name);
        Assert.Equal(expected: typeof(bool?), actual: parameters[1].ParameterType);
        Assert.Equal(expected: "download", actual: parameters[1].Name);
        Assert.True(condition: parameters[1].HasDefaultValue);
        Assert.Equal(expected: true, actual: parameters[1].DefaultValue);
        Assert.Equal(expected: "maxDecodeSize", actual: parameters[2].Name);
        Assert.Equal(expected: "Size", actual: Nullable.GetUnderlyingType(nullableType: parameters[2].ParameterType)?.Name);
        Assert.True(condition: parameters[2].HasDefaultValue);
        Assert.Null(@object: parameters[2].DefaultValue);
    }

    [Fact]
    public void Download_LocalFunction_DoesNotCallReadAsStreamAsync()
    {
        (Type _, MethodInfo moveNext) = GetLocalFunctionStateMachine();
        List<string> calledMethods = GetCalledMethodNames(moveNext: moveNext);

        Assert.DoesNotContain(expected: "ReadAsStreamAsync", collection: calledMethods);
    }

    [Fact]
    public void Download_LocalFunction_CallsReadAsByteArrayAsync()
    {
        (Type _, MethodInfo moveNext) = GetLocalFunctionStateMachine();
        List<string> calledMethods = GetCalledMethodNames(moveNext: moveNext);

        Assert.Contains(expected: "ReadAsByteArrayAsync", collection: calledMethods);
    }

    [Fact]
    public void Download_LocalFunction_CallsReadAsByteArrayAsyncExactlyOnce()
    {
        (Type _, MethodInfo moveNext) = GetLocalFunctionStateMachine();
        List<string> calledMethods = GetCalledMethodNames(moveNext: moveNext);

        int count = calledMethods.Count(predicate: n => n == "ReadAsByteArrayAsync");
        Assert.Equal(expected: 1, actual: count);
    }

    [Fact]
    public void Download_LocalFunction_DoesNotCallContentReadMultipleTimes()
    {
        (Type _, MethodInfo moveNext) = GetLocalFunctionStateMachine();
        List<string> calledMethods = GetCalledMethodNames(moveNext: moveNext);

        int contentReadCalls = calledMethods.Count(predicate: n =>
            n is "ReadAsByteArrayAsync" or "ReadAsStreamAsync" or "ReadAsStringAsync"
        );

        Assert.Equal(expected: 1, actual: contentReadCalls);
    }

    [Fact]
    public void Download_LocalFunction_ImageLoadUsesByteArrayOverload()
    {
        (Type stateMachineType, MethodInfo moveNext) = GetLocalFunctionStateMachine();

        byte[] ilBytes = moveNext.GetMethodBody()!.GetILAsByteArray()!;
        Module module = stateMachineType.Module;

        bool hasImageLoadWithByteArray = false;

        for (int i = 0; i < ilBytes.Length; i++)
        {
            if ((ilBytes[i] == 0x28 || ilBytes[i] == 0x6F) && i + 4 < ilBytes.Length)
            {
                int token = BitConverter.ToInt32(value: ilBytes, startIndex: i + 1);
                try
                {
                    MethodBase? calledMethod = module.ResolveMethod(metadataToken: token);
                    if (calledMethod?.Name == "Load" && calledMethod.GetParameters().Length > 0)
                    {
                        ParameterInfo firstParam = calledMethod.GetParameters()[0];
                        if (
                            firstParam.ParameterType == typeof(byte[])
                            || firstParam.ParameterType == typeof(ReadOnlySpan<byte>)
                        )
                        {
                            hasImageLoadWithByteArray = true;
                        }
                    }
                }
                catch (Exception)
                {
                    // Token may not resolve — skip
                }
            }
        }

        Assert.True(
            condition: hasImageLoadWithByteArray,
            userMessage: "PROV-H16: Image.Load should use the byte[] overload, not Stream, "
                         + "to avoid consuming a stream that might be reused."
        );
    }

    [Fact]
    public void Download_LocalFunction_HasAsyncStateMachine()
    {
        // Verify we can actually find the state machine — this is a prerequisite
        // for all other IL-based tests. If this fails, the compiler changed how
        // it generates local async functions.
        (Type stateMachineType, MethodInfo moveNext) = GetLocalFunctionStateMachine();

        Assert.NotNull(@object: stateMachineType);
        Assert.NotNull(@object: moveNext);
        Assert.True(condition: typeof(IAsyncStateMachine).IsAssignableFrom(c: stateMachineType));
    }
}
