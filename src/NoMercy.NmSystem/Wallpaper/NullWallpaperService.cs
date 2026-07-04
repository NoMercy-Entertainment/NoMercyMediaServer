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

namespace NoMercy.NmSystem.Wallpaper;

public class NullWallpaperService : IWallpaperService
{
    public bool IsSupported => false;

    public void Set(string imagePath, WallpaperStyle style, string hexColor) { }

    public void SetSilent(string imagePath, WallpaperStyle style, string hexColor) { }

    public void Restore() { }
}
