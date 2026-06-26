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

using System.Drawing;
using System.Text;
using NoMercy.NmSystem.Extensions;
using Pastel;
using Serilog.Core;
using Serilog.Events;
using Logger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.NmSystem.LogEnrichers;

internal class ConsoleTypeEnricher : ILogEventEnricher
{
    private static string SpacerEnd(string text, int padding)
    {
        StringBuilder spacing = new();
        spacing.Append(text);
        for (int i = 0; i < padding - text.Length; i++)
            spacing.Append(' ');

        return spacing.ToString();
    }

    private static string SpacerBegin(string text, int padding)
    {
        StringBuilder spacing = new();
        for (int i = 0; i < padding - text.Length; i++)
            spacing.Append(' ');
        spacing.Append(text);

        return spacing.ToString();
    }

    private static string Spacer(string text, int padding, bool begin = false)
    {
        return begin ? SpacerBegin(text, padding) : SpacerEnd(text, padding);
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.Properties.TryGetValue("ConsoleType", out LogEventPropertyValue? value);

        string type = value?.ToString().Replace("\"", "") ?? "app";

        Color color = Logger.GetColor(type);

        logEvent.AddOrUpdateProperty(
            propertyFactory.CreateProperty(
                "ConsoleType",
                Spacer(type.ToTitleCase(), 14, true).Pastel(color)
            )
        );
    }
}
