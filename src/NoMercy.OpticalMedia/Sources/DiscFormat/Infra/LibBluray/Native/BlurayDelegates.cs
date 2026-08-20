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

/// Matches bd_overlay_proc_f in bluray.h — called with a pointer to BD_OVERLAY.
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void BdOverlayProcDelegate(nint handle, nint overlayEvent);

/// Matches bd_argb_overlay_proc_f in bluray.h — called with a pointer to BD_ARGB_OVERLAY.
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void BdArgbOverlayProcDelegate(nint handle, nint argbOverlayEvent);
