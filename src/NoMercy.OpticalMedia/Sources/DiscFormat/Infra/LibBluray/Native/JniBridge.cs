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

/// Reaches into the JVM libbluray already created for BD-J, so the capture session can start the
/// NmdfFocusProbe daemon without libbluray itself being patched. It does not create a VM — it finds
/// the one in flight via JNI_GetCreatedJavaVMs (exported by jvm.dll, already loaded), attaches the
/// calling thread, and invokes one static void method. All native, quarantined in this project.
public static partial class JniBridge
{
    private const string JvmLib = "jvm";

    [LibraryImport(JvmLib, EntryPoint = "JNI_GetCreatedJavaVMs")]
    private static partial int JniGetCreatedJavaVms(out nint vm, int bufferLength, out int found);

    /// Starts the focus-probe daemon in the running BD-J JVM with the given trigger/output file
    /// paths. Returns false when no JVM is in flight (BD-J not started) or the bridge cannot attach.
    public static bool StartFocusProbeDaemon(string triggerPath, string outputPath)
    {
        if (JniGetCreatedJavaVms(out nint javaVm, 1, out int found) != 0 || found <= 0)
        {
            return false;
        }

        JniInvoker invoker = new(javaVm);
        return invoker.CallStaticVoidStringString(
            "org/videolan/NmdfFocusProbe",
            "startDaemon",
            triggerPath,
            outputPath
        );
    }
}
