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
using NoMercy.DiscFormat.LibBluray.Native;

namespace NoMercy.DiscFormat.LibBluray;

/// <summary>
/// Safe IDisposable wrapper around a libbluray BLURAY* session.
/// One instance = one disc open. Dispose closes the native handle.
/// </summary>
public sealed class LibBlurayClient : ILibBluray
{
    private nint _handle;
    private bool _disposed;

    public BlurayDiscInfo? DiscInfo { get; private set; }
    public uint TitleCount { get; private set; }

    static LibBlurayClient()
    {
        LibBlurayResolver.EnsureRegistered();
    }

    public void Open(string devicePath, string? keyfilePath = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_handle != nint.Zero)
        {
            LibBlurayNative.BdClose(_handle);
            _handle = nint.Zero;
        }

        nint bd = LibBlurayNative.BdOpen(devicePath, keyfilePath);
        if (bd == nint.Zero)
        {
            throw new InvalidOperationException(
                $"bd_open returned NULL for device '{devicePath}'."
            );
        }

        _handle = bd;

        DiscInfo = ReadDiscInfo(bd);
        TitleCount = LibBlurayNative.BdGetTitles(bd, 0, 0);
    }

    private static BlurayDiscInfo ReadDiscInfo(nint bd)
    {
        nint infoPtr = LibBlurayNative.BdGetDiscInfo(bd);
        if (infoPtr == nint.Zero)
        {
            throw new InvalidOperationException("bd_get_disc_info returned NULL.");
        }

        BlurayDiscInfoNative native = Marshal.PtrToStructure<BlurayDiscInfoNative>(infoPtr);

        string? discName =
            native.DiscNamePtr != nint.Zero ? Marshal.PtrToStringUTF8(native.DiscNamePtr) : null;

        string? udfVolumeId =
            native.UdfVolumeIdPtr != nint.Zero
                ? Marshal.PtrToStringUTF8(native.UdfVolumeIdPtr)
                : null;

        string discIdHex = ReadDiscIdHex(native);

        return new BlurayDiscInfo
        {
            BlurayDetected = native.BlurayDetected != 0,
            AacsDetected = native.AacsDetected != 0,
            AacsHandled = native.AacsHandled != 0,
            BdplusDetected = native.BdplusDetected != 0,
            FirstPlaySupported = native.FirstPlaySupported != 0,
            TopMenuSupported = native.TopMenuSupported != 0,
            NumTitles = native.NumTitles,
            NumHdmvTitles = native.NumHdmvTitles,
            NumBdjTitles = native.NumBdjTitles,
            NumUnsupportedTitles = native.NumUnsupportedTitles,
            AacsErrorCode = native.AacsErrorCode,
            AacsMkbv = native.AacsMkbv,
            DiscName = discName,
            UdfVolumeId = udfVolumeId,
            DiscIdHex = discIdHex,
        };
    }

    private static unsafe string ReadDiscIdHex(BlurayDiscInfoNative native)
    {
        byte[] discId = new byte[20];
        for (int index = 0; index < discId.Length; index++)
        {
            discId[index] = native.DiscId[index];
        }

        return Convert.ToHexStringLower(discId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_handle != nint.Zero)
        {
            LibBlurayNative.BdClose(_handle);
            _handle = nint.Zero;
        }

        _disposed = true;
    }
}
