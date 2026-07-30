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

namespace NoMercy.Plugins.Abstractions;

/// <summary>What a form field collects.</summary>
public static class PluginFormFieldType
{
    public const string Text = "text";
    public const string Password = "password";
    public const string Number = "number";
    public const string Toggle = "toggle";
    public const string Select = "select";

    /// <summary>A single on/off box in a set, as opposed to a standalone toggle.</summary>
    public const string Checkbox = "checkbox";

    /// <summary>A file upload, posted to the plugin's own REST endpoint.</summary>
    public const string File = "file";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            Text,
            Password,
            Number,
            Toggle,
            Select,
            Checkbox,
            File,
        };
}
