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

using System.Runtime.InteropServices;

namespace NoMercy.DiscFormat.LibBluray.Native;

/// Calls a static `void m(String, String)` method on a class in an already-running JVM through the
/// raw JNI function tables. JavaVM* and JNIEnv* are pointers to pointer-to-function tables; the
/// fixed slot indices below are the stable JNI ABI (jni.h struct layout), so we read each function
/// pointer at its offset and invoke it. Used only to start the focus-probe daemon in libbluray's VM.
public sealed class JniInvoker
{
    // JNIInvokeInterface_ slots (JavaVM).
    private const int VmAttachCurrentThread = 4;

    // JNINativeInterface_ slots (JNIEnv).
    private const int EnvFindClass = 6;
    private const int EnvGetStaticMethodId = 113;
    private const int EnvNewStringUtf = 167;

    // CallStaticVoidMethodA (143): the jvalue-array form. The varargs form (141) cannot be marshaled
    // reliably through P/Invoke, so we pass a jvalue[] explicitly.
    private const int EnvCallStaticVoidMethodA = 143;
    private const int EnvExceptionClear = 17;

    private readonly nint _javaVm;

    public JniInvoker(nint javaVm)
    {
        _javaVm = javaVm;
    }

    public bool CallStaticVoidStringString(
        string className,
        string methodName,
        string arg0,
        string arg1
    )
    {
        nint env = AttachCurrentThread();
        if (env == nint.Zero)
        {
            return false;
        }

        nint clazz = FindClass(env, className);
        if (clazz == nint.Zero)
        {
            ClearException(env);
            return false;
        }

        nint method = GetStaticMethodId(
            env,
            clazz,
            methodName,
            "(Ljava/lang/String;Ljava/lang/String;)V"
        );
        if (method == nint.Zero)
        {
            ClearException(env);
            return false;
        }

        nint jArg0 = NewStringUtf(env, arg0);
        nint jArg1 = NewStringUtf(env, arg1);
        CallStaticVoid(env, clazz, method, jArg0, jArg1);
        ClearException(env);
        return true;
    }

    private nint AttachCurrentThread()
    {
        nint vmTable = Marshal.ReadIntPtr(_javaVm);
        nint attachPtr = Marshal.ReadIntPtr(vmTable, VmAttachCurrentThread * nint.Size);
        AttachDelegate attach = Marshal.GetDelegateForFunctionPointer<AttachDelegate>(attachPtr);
        int result = attach(_javaVm, out nint env, nint.Zero);
        return result == 0 ? env : nint.Zero;
    }

    private static nint FindClass(nint env, string name)
    {
        nint fn = EnvSlot(env, EnvFindClass);
        FindClassDelegate findClass = Marshal.GetDelegateForFunctionPointer<FindClassDelegate>(fn);
        nint namePtr = Marshal.StringToHGlobalAnsi(name);
        try
        {
            return findClass(env, namePtr);
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
        }
    }

    private static nint GetStaticMethodId(nint env, nint clazz, string name, string signature)
    {
        nint fn = EnvSlot(env, EnvGetStaticMethodId);
        GetStaticMethodDelegate get =
            Marshal.GetDelegateForFunctionPointer<GetStaticMethodDelegate>(fn);
        nint namePtr = Marshal.StringToHGlobalAnsi(name);
        nint sigPtr = Marshal.StringToHGlobalAnsi(signature);
        try
        {
            return get(env, clazz, namePtr, sigPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
            Marshal.FreeHGlobal(sigPtr);
        }
    }

    private static nint NewStringUtf(nint env, string value)
    {
        nint fn = EnvSlot(env, EnvNewStringUtf);
        NewStringDelegate newString = Marshal.GetDelegateForFunctionPointer<NewStringDelegate>(fn);
        nint valuePtr = Marshal.StringToHGlobalAnsi(value);
        try
        {
            return newString(env, valuePtr);
        }
        finally
        {
            Marshal.FreeHGlobal(valuePtr);
        }
    }

    private static void CallStaticVoid(nint env, nint clazz, nint method, nint arg0, nint arg1)
    {
        nint fn = EnvSlot(env, EnvCallStaticVoidMethodA);
        CallStaticVoidADelegate call =
            Marshal.GetDelegateForFunctionPointer<CallStaticVoidADelegate>(fn);

        // jvalue is an 8-byte union per arg; an object ref fills the low 8 bytes on x64.
        nint args = Marshal.AllocHGlobal(nint.Size * 2);
        try
        {
            Marshal.WriteIntPtr(args, 0, arg0);
            Marshal.WriteIntPtr(args, nint.Size, arg1);
            call(env, clazz, method, args);
        }
        finally
        {
            Marshal.FreeHGlobal(args);
        }
    }

    private static void ClearException(nint env)
    {
        nint fn = EnvSlot(env, EnvExceptionClear);
        ExceptionClearDelegate clear =
            Marshal.GetDelegateForFunctionPointer<ExceptionClearDelegate>(fn);
        clear(env);
    }

    private static nint EnvSlot(nint env, int slot)
    {
        nint envTable = Marshal.ReadIntPtr(env);
        return Marshal.ReadIntPtr(envTable, slot * nint.Size);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AttachDelegate(nint vm, out nint env, nint args);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint FindClassDelegate(nint env, nint name);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint GetStaticMethodDelegate(nint env, nint clazz, nint name, nint sig);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint NewStringDelegate(nint env, nint utf);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void CallStaticVoidADelegate(nint env, nint clazz, nint method, nint args);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ExceptionClearDelegate(nint env);
}
