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

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Text;
using Pastel;

namespace NoMercy.NmSystem.Logging.Rendering;

/// <summary>
/// Renders one log entry into an aligned, optionally-coloured console block:
/// <c>HH:mm:ss [marker] [right-aligned category] | message</c>, with message
/// values token-coloured, continuation/wrapped lines hung under the gutter, and
/// any exception indented under the gutter. All column maths go through
/// <see cref="DisplayWidth"/> so alignment holds for CJK, emoji and
/// already-coloured strings.
/// </summary>
public static class ConsoleLineRenderer
{
    private const int CategoryWidth = 14;
    private const int GutterColumn = 26; // 8 time + 1 + 1 marker + 1 + 14 category + 1

    public static string Render(
        DateTime timestamp,
        LogLevel level,
        LogCategory category,
        string message,
        Exception? exception,
        NoMercyConsoleTheme theme,
        bool color,
        int width = 0
    )
    {
        string dim = ConsoleThemeColors.Dim(theme: theme);
        (string marker, string levelHex) = ConsoleThemeColors.Level(level: level, theme: theme);
        string categoryHex = ConsoleThemeColors.Category(category: category, theme: theme);

        string time = timestamp.ToString(format: "HH:mm:ss", provider: CultureInfo.InvariantCulture);
        string label = DisplayWidth.PadLeft(text: category.DisplayName, width: CategoryWidth);

        StringBuilder head = new();
        head.Append(value: Paint(text: time, hex: dim, color: color)).Append(value: ' ');
        head.Append(value: Paint(text: marker, hex: levelHex, color: color)).Append(value: ' ');
        head.Append(value: Paint(text: label, hex: categoryHex, color: color)).Append(value: ' ');
        head.Append(value: Paint(text: "│", hex: dim, color: color)).Append(value: ' ');

        string gutter = new string(c: ' ', count: GutterColumn) + Paint(text: "│", hex: dim, color: color) + " ";

        int wrapWidth = width > GutterColumn + 4 ? width - GutterColumn - 2 : 0;
        List<string> messageLines = SplitAndWrap(message: message ?? string.Empty, wrapWidth: wrapWidth);

        List<string> output = new();
        for (int i = 0; i < messageLines.Count; i++)
        {
            string rendered = RenderMessage(line: messageLines[index: i], theme: theme, color: color);
            output.Add(item: i == 0 ? head.ToString() + rendered : gutter + rendered);
        }

        if (output.Count == 0)
            output.Add(item: head.ToString());

        if (exception is not null)
        {
            foreach (string raw in exception.ToString().Split(separator: '\n'))
                output.Add(item: gutter + Paint(text: "└ " + raw.TrimEnd(trimChar: '\r'), hex: dim, color: color));
        }

        return string.Join(separator: "\n", values: output);
    }

    private static List<string> SplitAndWrap(string message, int wrapWidth)
    {
        List<string> lines = new();
        foreach (string raw in message.Split(separator: '\n'))
        {
            string line = raw.TrimEnd(trimChar: '\r');
            if (wrapWidth <= 0 || DisplayWidth.Of(text: line) <= wrapWidth)
                lines.Add(item: line);
            else
                lines.AddRange(collection: DisplayWidth.Wrap(text: line, width: wrapWidth));
        }

        return lines;
    }

    private static string RenderMessage(string line, NoMercyConsoleTheme theme, bool color)
    {
        if (!color)
            return line;

        // Pre-coloured content (e.g. the startup banner) already carries ANSI
        // escape sequences. Re-tokenising it would split those sequences and
        // surface their raw characters, so pass such lines through untouched.
        if (line.Contains(value: '\u001b'))
            return line;

        string text = ConsoleThemeColors.Text(theme: theme);
        string number = ConsoleThemeColors.Number(theme: theme);
        string str = ConsoleThemeColors.Str(theme: theme);

        StringBuilder builder = new();
        int index = 0;
        while (index < line.Length)
        {
            char current = line[index: index];
            if (current == '"')
            {
                int end = line.IndexOf(value: '"', startIndex: index + 1);
                if (end < 0)
                    end = line.Length - 1;
                builder.Append(value: line.Substring(startIndex: index, length: end - index + 1).Pastel(hexColor: str));
                index = end + 1;
            }
            else if (char.IsDigit(c: current))
            {
                int end = index;
                while (end < line.Length && (char.IsDigit(c: line[index: end]) || line[index: end] == '.'))
                    end++;
                builder.Append(value: line.Substring(startIndex: index, length: end - index).Pastel(hexColor: number));
                index = end;
            }
            else
            {
                int end = index;
                while (end < line.Length && line[index: end] != '"' && !char.IsDigit(c: line[index: end]))
                    end++;
                builder.Append(value: line.Substring(startIndex: index, length: end - index).Pastel(hexColor: text));
                index = end;
            }
        }

        return builder.ToString();
    }

    private static string Paint(string text, string hex, bool color) =>
        color ? text.Pastel(hexColor: hex) : text;
}
