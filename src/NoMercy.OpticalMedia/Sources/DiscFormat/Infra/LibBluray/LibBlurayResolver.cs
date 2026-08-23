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
using System.Runtime.InteropServices;

namespace NoMercy.DiscFormat.LibBluray;

/// <summary>
/// Registers a DLL import resolver so that "libbluray" and its AACS/BD+ sidecars
/// are located next to the entry-point assembly. Also adds the native/ directory to
/// the Windows DLL search path so libbluray's own dlopen calls for libaacs / libbdplus
/// can find those sidecars and their own dependencies (libgcrypt, libgpg-error).
/// </summary>
public static partial class LibBlurayResolver
{
    private static bool _registered;
    private static readonly object Lock = new();

    [LibraryImport(
        "kernel32",
        EntryPoint = "AddDllDirectory",
        StringMarshalling = StringMarshalling.Utf16
    )]
    [return: MarshalAs(UnmanagedType.SysInt)]
    private static partial nint AddDllDirectory(string newDirectory);

    [LibraryImport("kernel32", EntryPoint = "SetDefaultDllDirectories")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDefaultDllDirectories(uint directoryFlags);

    // libbluray's getenv (UCRT) reads the ucrtbase env table, not the Win32 block
    // that Environment.SetEnvironmentVariable writes — sidecar overrides must be set
    // in both or libbluray never sees LIBAACS_PATH and reports "No usable AACS libraries".
    [LibraryImport(
        "ucrtbase",
        EntryPoint = "_putenv_s",
        StringMarshalling = StringMarshalling.Utf8
    )]
    private static partial int PutEnvS(string name, string value);

    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
    private const uint LoadLibrarySearchUserDirs = 0x00000400;

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Lock)
        {
            if (_registered)
            {
                return;
            }

            string nativeDir = Path.Combine(AppContext.BaseDirectory, "native");

            if (Directory.Exists(nativeDir))
            {
                if (OperatingSystem.IsWindows())
                {
                    // Expand the DLL search path so libbluray's internal LoadLibrary
                    // calls for libaacs/libbdplus and their own dependencies can also
                    // resolve from the native/ directory.
                    SetDefaultDllDirectories(
                        LoadLibrarySearchDefaultDirs | LoadLibrarySearchUserDirs
                    );
                    AddDllDirectory(nativeDir);

                    // Also prepend native/ to PATH so libaacs's own dependency chain
                    // (libgcrypt, libgpg-error) resolves when libbluray dlopen's it.
                    PrependToPath(nativeDir);
                }

                // Prefer MakeMKV's libmmbd when present: it implements the libaacs +
                // libbdplus API but uses MakeMKV's AACS engine, which completes the
                // drive authentication that the bundled libaacs cannot on drives that
                // refuse the AACS certificate read. libbluray loads whatever LIBAACS_PATH
                // / LIBBDPLUS_PATH point at, so one library serves both roles.
                ConfigureDecryptionProvider(nativeDir);
            }

            NativeLibrary.SetDllImportResolver(typeof(LibBlurayResolver).Assembly, Resolve);

            _registered = true;
        }
    }

    private static void SetSidecarEnvVar(string key, string fullDllPath)
    {
        if (
            !File.Exists(fullDllPath)
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key))
        )
        {
            return;
        }

        // libbluray's dl_dlopen appends ".dll" to whatever path it loads, so strip
        // the extension here so the final LoadLibraryExW call gets the correct path.
        string withoutExtension = Path.Combine(
            Path.GetDirectoryName(fullDllPath)!,
            Path.GetFileNameWithoutExtension(fullDllPath)
        );

        // Set in both the Win32 block and the UCRT table — libbluray's getenv reads
        // the latter, which Environment.SetEnvironmentVariable does not write.
        Environment.SetEnvironmentVariable(key, withoutExtension);
        if (OperatingSystem.IsWindows())
        {
            PutEnvS(key, withoutExtension);
        }
    }

    /// <summary>Full path of the AACS/BD+ provider chosen at registration (libmmbd or bundled).</summary>
    public static string? DecryptionProviderPath { get; private set; }

    /// <summary>
    /// Points LIBAACS_PATH / LIBBDPLUS_PATH at MakeMKV's libmmbd when it is installed,
    /// otherwise at the bundled libaacs / libbdplus. Either way the dependency chain is
    /// pre-loaded so libbluray's dlopen resolves it under the restricted DLL search path.
    /// </summary>
    private static void ConfigureDecryptionProvider(string nativeDir)
    {
        string? libmmbd = ResolveLibmmbd();
        if (libmmbd is not null)
        {
            string libmmbdDir = Path.GetDirectoryName(libmmbd)!;
            if (OperatingSystem.IsWindows())
            {
                AddDllDirectory(libmmbdDir);
                PrependToPath(libmmbdDir);
            }

            // One library answers both the AACS and BD+ entry points.
            SetSidecarEnvVar("LIBAACS_PATH", libmmbd);
            SetSidecarEnvVar("LIBBDPLUS_PATH", libmmbd);
            NativeLibrary.TryLoad(libmmbd, out _);
            DecryptionProviderPath = libmmbd;
            return;
        }

        SetSidecarEnvVar("LIBAACS_PATH", Path.Combine(nativeDir, "libaacs.dll"));
        SetSidecarEnvVar("LIBBDPLUS_PATH", Path.Combine(nativeDir, "libbdplus.dll"));
        PreloadSidecars(nativeDir);
        DecryptionProviderPath = Path.Combine(nativeDir, "libaacs.dll");
    }

    /// <summary>
    /// Locates MakeMKV's libmmbd: an explicit NMDF_LIBMMBD_PATH override first, then the
    /// standard MakeMKV install directories (64-bit build preferred for a 64-bit process).
    /// </summary>
    private static string? ResolveLibmmbd()
    {
        string? overridePath = Environment.GetEnvironmentVariable("NMDF_LIBMMBD_PATH");
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        string[] installDirs =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "MakeMKV"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "MakeMKV"
            ),
        ];

        string[] names = Environment.Is64BitProcess
            ? ["libmmbd64.dll", "libmmbd.dll"]
            : ["libmmbd.dll", "libmmbd64.dll"];

        foreach (string dir in installDirs)
        {
            foreach (string name in names)
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static void PreloadSidecars(string nativeDir)
    {
        // Dependency order: libgpg-error <- libgcrypt <- libaacs; libbdplus is independent.
        string[] sidecarsInLoadOrder =
        [
            "libgpg-error6-0.dll",
            "libgcrypt-20.dll",
            "libaacs.dll",
            "libbdplus.dll",
        ];

        foreach (string name in sidecarsInLoadOrder)
        {
            string path = Path.Combine(nativeDir, name);
            if (File.Exists(path))
            {
                NativeLibrary.TryLoad(path, out _);
            }
        }
    }

    private static void PrependToPath(string directory)
    {
        string current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (current.Contains(directory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string updated = directory + ";" + current;
        Environment.SetEnvironmentVariable("PATH", updated);
        PutEnvS("PATH", updated);
    }

    private static nint Resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath
    )
    {
        string nativeDir = Path.Combine(AppContext.BaseDirectory, "native");

        string[] candidates =
        [
            Path.Combine(nativeDir, libraryName + ".dll"),
            Path.Combine(AppContext.BaseDirectory, libraryName + ".dll"),
        ];

        foreach (string candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            if (NativeLibrary.TryLoad(candidate, out nint handle))
            {
                return handle;
            }
        }

        // Fall back to default OS search
        NativeLibrary.TryLoad(libraryName, assembly, searchPath, out nint fallback);
        return fallback;
    }
}
