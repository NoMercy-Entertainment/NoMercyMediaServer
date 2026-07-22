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
using NoMercy.Providers.CoverArt.Client;

namespace NoMercy.Tests.Providers.CoverArt.Client;

/// <summary>
/// PROV-H12: Tests verifying that CoverArtCoverArtClient.Download reads the response
/// content exactly once. The bug: ReadAsStreamAsync consumed the stream, then
/// ReadAsByteArrayAsync re-read the (exhausted) response content, and
/// Image.Load was called on the already-consumed stream — producing corrupt images.
/// The fix: Read content once as byte[] and use it for both file writing and image loading.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class CoverArtCoverArtClientTests
{
    private static MethodInfo GetDownloadMethod()
    {
        MethodInfo? method = typeof(CoverArtCoverArtClient).GetMethod(
            name: "Download",
            bindingAttr: BindingFlags.Public | BindingFlags.Static
        );

        Assert.NotNull(@object: method);
        return method;
    }

    private static Type GetStateMachineType(MethodInfo method)
    {
        AsyncStateMachineAttribute? attr = method.GetCustomAttribute<AsyncStateMachineAttribute>();

        Assert.NotNull(@object: attr);
        return attr.StateMachineType;
    }

    [Fact]
    public void Download_IsStaticAsync()
    {
        MethodInfo method = GetDownloadMethod();

        Assert.True(condition: method.IsStatic, userMessage: "Download should be a static method");

        AsyncStateMachineAttribute? attr = method.GetCustomAttribute<AsyncStateMachineAttribute>();
        Assert.NotNull(@object: attr);
    }

    [Fact]
    public void Download_ReturnsTaskOfNullableImage()
    {
        MethodInfo method = GetDownloadMethod();

        Assert.True(condition: method.ReturnType.IsGenericType);
        Assert.Equal(expected: typeof(Task<>), actual: method.ReturnType.GetGenericTypeDefinition());
    }

    [Fact]
    public void Download_AcceptsNullableUriAndOptionalBoolParameters()
    {
        MethodInfo method = GetDownloadMethod();
        ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(expected: 3, actual: parameters.Length);
        Assert.Equal(expected: typeof(Uri), actual: parameters[0].ParameterType);
        Assert.Equal(expected: "url", actual: parameters[0].Name);
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
    public void Download_StateMachine_DoesNotCallReadAsStreamAsync()
    {
        MethodInfo method = GetDownloadMethod();
        Type stateMachineType = GetStateMachineType(method: method);

        MethodInfo moveNext = stateMachineType.GetMethod(
            name: "MoveNext",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        Assert.NotNull(@object: moveNext);

        byte[] ilBytes = moveNext.GetMethodBody()!.GetILAsByteArray()!;
        Module module = stateMachineType.Module;

        bool callsReadAsStream = false;

        for (int i = 0; i < ilBytes.Length; i++)
        {
            if ((ilBytes[i] == 0x28 || ilBytes[i] == 0x6F) && i + 4 < ilBytes.Length)
            {
                int token = BitConverter.ToInt32(value: ilBytes, startIndex: i + 1);
                try
                {
                    MethodBase? calledMethod = module.ResolveMethod(metadataToken: token);
                    if (calledMethod?.Name == "ReadAsStreamAsync")
                    {
                        callsReadAsStream = true;
                        break;
                    }
                }
                catch (Exception)
                {
                    // Token may not resolve — skip
                }
            }
        }

        Assert.False(
            condition: callsReadAsStream,
            userMessage: "PROV-H12 regression: Download should NOT call ReadAsStreamAsync. "
                         + "Content must be read once as byte[] to avoid stream-consumed-then-reused bug."
        );
    }

    [Fact]
    public void Download_StateMachine_CallsReadAsByteArrayAsync()
    {
        MethodInfo method = GetDownloadMethod();
        Type stateMachineType = GetStateMachineType(method: method);

        MethodInfo moveNext = stateMachineType.GetMethod(
            name: "MoveNext",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        Assert.NotNull(@object: moveNext);

        byte[] ilBytes = moveNext.GetMethodBody()!.GetILAsByteArray()!;
        Module module = stateMachineType.Module;

        bool callsReadAsByteArray = false;

        for (int i = 0; i < ilBytes.Length; i++)
        {
            if ((ilBytes[i] == 0x28 || ilBytes[i] == 0x6F) && i + 4 < ilBytes.Length)
            {
                int token = BitConverter.ToInt32(value: ilBytes, startIndex: i + 1);
                try
                {
                    MethodBase? calledMethod = module.ResolveMethod(metadataToken: token);
                    if (calledMethod?.Name == "ReadAsByteArrayAsync")
                    {
                        callsReadAsByteArray = true;
                        break;
                    }
                }
                catch (Exception)
                {
                    // Token may not resolve — skip
                }
            }
        }

        Assert.True(
            condition: callsReadAsByteArray,
            userMessage: "PROV-H12: Download must call ReadAsByteArrayAsync to buffer the content once."
        );
    }

    [Fact]
    public void Download_StateMachine_CallsReadAsByteArrayAsyncExactlyOnce()
    {
        MethodInfo method = GetDownloadMethod();
        Type stateMachineType = GetStateMachineType(method: method);

        MethodInfo moveNext = stateMachineType.GetMethod(
            name: "MoveNext",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        byte[] ilBytes = moveNext.GetMethodBody()!.GetILAsByteArray()!;
        Module module = stateMachineType.Module;

        int readByteArrayCount = 0;

        for (int i = 0; i < ilBytes.Length; i++)
        {
            if ((ilBytes[i] == 0x28 || ilBytes[i] == 0x6F) && i + 4 < ilBytes.Length)
            {
                int token = BitConverter.ToInt32(value: ilBytes, startIndex: i + 1);
                try
                {
                    MethodBase? calledMethod = module.ResolveMethod(metadataToken: token);
                    if (calledMethod?.Name == "ReadAsByteArrayAsync")
                        readByteArrayCount++;
                }
                catch (Exception)
                {
                    // Token may not resolve — skip
                }
            }
        }

        Assert.Equal(expected: 1, actual: readByteArrayCount);
    }

    [Fact]
    public void Download_StateMachine_DoesNotCallContentReadMultipleTimes()
    {
        MethodInfo method = GetDownloadMethod();
        Type stateMachineType = GetStateMachineType(method: method);

        MethodInfo moveNext = stateMachineType.GetMethod(
            name: "MoveNext",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        )!;

        byte[] ilBytes = moveNext.GetMethodBody()!.GetILAsByteArray()!;
        Module module = stateMachineType.Module;

        int contentReadCalls = 0;

        for (int i = 0; i < ilBytes.Length; i++)
        {
            if ((ilBytes[i] == 0x28 || ilBytes[i] == 0x6F) && i + 4 < ilBytes.Length)
            {
                int token = BitConverter.ToInt32(value: ilBytes, startIndex: i + 1);
                try
                {
                    MethodBase? calledMethod = module.ResolveMethod(metadataToken: token);
                    if (
                        calledMethod?.Name
                        is "ReadAsByteArrayAsync"
                            or "ReadAsStreamAsync"
                            or "ReadAsStringAsync"
                    )
                    {
                        contentReadCalls++;
                    }
                }
                catch (Exception)
                {
                    // Token may not resolve — skip
                }
            }
        }

        Assert.Equal(expected: 1, actual: contentReadCalls);
    }

    [Fact]
    public void Download_StateMachine_ImageLoadUsesByteArrayOverload()
    {
        MethodInfo method = GetDownloadMethod();
        Type stateMachineType = GetStateMachineType(method: method);

        MethodInfo moveNext = stateMachineType.GetMethod(
            name: "MoveNext",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
        )!;

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
            userMessage: "PROV-H12: Image.Load should use the byte[] overload, not Stream, "
                         + "to avoid consuming a stream that might be reused."
        );
    }
}
