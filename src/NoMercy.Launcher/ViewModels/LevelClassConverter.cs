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

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NoMercy.Launcher.ViewModels;

public static class LevelColorConverter
{
    public static FuncValueConverter<string, IBrush> Instance { get; } =
        new(convert: level =>
            level?.ToLowerInvariant() switch
            {
                "fatal" => new(color: Color.Parse(s: "#DC2626")),
                "error" => new(color: Color.Parse(s: "#EF4444")),
                "warning" => new(color: Color.Parse(s: "#EAB308")),
                "debug" => new(color: Color.Parse(s: "#6B7280")),
                "verbose" => new(color: Color.Parse(s: "#4B5563")),
                _ => new SolidColorBrush(color: Color.Parse(s: "#D1D5DB")),
            }
        );
}

public static class LevelWeightConverter
{
    public static FuncValueConverter<string, FontWeight> Instance { get; } =
        new(convert: level =>
            level?.ToLowerInvariant() switch
            {
                "fatal" => FontWeight.Bold,
                "error" => FontWeight.Bold,
                _ => FontWeight.Normal,
            }
        );
}

public static class LogColorConverter
{
    private static readonly SolidColorBrush DefaultBrush = new(color: Color.Parse(s: "#D1D5DB"));

    public static FuncValueConverter<string, IBrush> Instance { get; } =
        new(convert: colorHex =>
        {
            if (string.IsNullOrEmpty(value: colorHex))
                return DefaultBrush;

            try
            {
                return new SolidColorBrush(color: Color.Parse(s: colorHex));
            }
            catch
            {
                return DefaultBrush;
            }
        });
}
