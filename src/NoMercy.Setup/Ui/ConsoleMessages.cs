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

using System.Text;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Server;
using Pastel;
using Serilog.Events;

namespace NoMercy.Setup.Ui;

public abstract class ConsoleMessages
{
    private static string[] Colors => ApiKeyStore.Current.Colors;

    private static string Quote => ApiKeyStore.Current.Quote;

    public static Task ServerRunning()
    {
        if (Console.IsOutputRedirected)
            return Task.CompletedTask;

        ConsoleExtensions.Enable();

        string visitLine = Config.IsDev
            ? $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
                + "      "
                + "  visit:".Pastel(hexColor: "#cccccc")
                + $"  {ExternalServicesConfig.Current.AppBaseUrl}   ".Pastel(hexColor: "#ffffff")
                + $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
            : $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
                + "      "
                + "  visit:".Pastel(hexColor: "#cccccc")
                + $"  {ExternalServicesConfig.Current.AppBaseUrl}       ".Pastel(hexColor: "#ffffff")
                + $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d");

        Logger.WriteBanner(lines:
        [
            ("setup", ("╔" + Repeat(stringToRepeat: "═", repeat: 46) + "╗").Pastel(hexColor: "#00a10d"), LogEventLevel.Information),
            (
                "setup",
                $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
                    + "     "
                    + "Secure Server running: on port:".Pastel(hexColor: "#5ffa71")
                    + $" {RuntimeServerSettings.Current.InternalServerPort}     ".Pastel(hexColor: "#ffffff")
                    + $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d"),
                LogEventLevel.Information
            ),
            ("setup", visitLine, LogEventLevel.Information),
            ("setup", ("╚" + Repeat(stringToRepeat: "═", repeat: 46) + "╝").Pastel(hexColor: "#00a10d"), LogEventLevel.Information),
        ]);

        return Task.CompletedTask;
    }

    private static string _(string? color = null)
    {
        return "║".Pastel(hexColor: color ?? Colors[0]);
    }

    private static string Repeat(string stringToRepeat, int repeat)
    {
        StringBuilder builder = new(capacity: repeat * stringToRepeat.Length);
        for (int i = 0; i < repeat; i++)
            builder.Append(value: stringToRepeat);

        return builder.ToString();
    }

    private static string CreateQuote(string text, int rightPadding)
    {
        // if (text.Length + rightPadding > totalSize - 2) throw new Exception("The text is too long to fit in the quote");

        List<string> spacing = [];

        // Add spaces to the left of the text
        for (int i = 102 - rightPadding; i > text.Length; i--)
            spacing.Add(item: "");

        spacing.Add(item: text);

        // Add spaces to the right of the text
        for (int i = 0; i < rightPadding; i++)
            spacing.Add(item: "");

        return string.Join(separator: " ", values: spacing);
    }

    private static bool IsXmasTime()
    {
        DateTime today = DateTime.Today;
        int currentYear = today.Year;

        long xmasBeginDate = new DateTime(year: currentYear, month: 12, day: 7).Ticks;
        long xmasEndDate = new DateTime(year: currentYear + 1, month: 1, day: 5).Ticks;

        return today.Ticks > xmasBeginDate && xmasEndDate < today.Ticks;
    }

    public static void Logo()
    {
        if (Console.IsOutputRedirected)
            return;

        Console.Clear();

        StringBuilder builder = new();
        string outputString = "║  NoMercy MediaServer  ║";
        int totalWidth = 0;

        bool isXmas = IsXmasTime();

        Dictionary<string, List<string>> letters = isXmas
            ? ConsoleLetters.ColossalXmas
            : ConsoleLetters.Colossal;

        for (int i = 0; i < letters.FirstOrDefault().Value.Count - 1; i++)
        {
            foreach (char letter in outputString)
            {
                string? text = letters[key: letter.ToString()][index: i];

                text = letter switch
                {
                    '║' => text.Pastel(hexColor: Colors[0]),
                    'N' or 'M' or 'S' => text.Pastel(hexColor: Colors[1]),
                    _ => text.Pastel(hexColor: Colors[2]),
                };

                builder.Append(value: text);

                if (i == 5)
                    totalWidth += letters[key: letter.ToString()][index: i].Length;
            }

            if (i == 9)
                continue;
            builder.AppendLine();
        }

        int magicSpacer = totalWidth - 2;

        Console.WriteLine(value: $"{("╔" + Repeat(stringToRepeat: "═", repeat: magicSpacer) + "╗").Pastel(hexColor: Colors[0])}");
        Console.WriteLine(value: $"{_()}{Repeat(stringToRepeat: " ", repeat: magicSpacer)}{_()}");

        Console.WriteLine(value: builder.ToString());

        Console.WriteLine(
            value: $"{_()}{Repeat(stringToRepeat: " ", repeat: 63)}{letters[key: "y"][index: 10].Pastel(hexColor: Colors[2])}"
                   + CreateQuote(text: Quote, rightPadding: 4)
                   + $"{letters[key: "║"][index: 0].Pastel(hexColor: Colors[0])}"
        );
        // Console.WriteLine($"{_()}" + CreateQuote(Quote, totalWidth, 4) + $"{(isXmas() ? ConsoleLetters.ColossalXmas : ConsoleLetters.Colossal)["║"][0].Pastel(Colors[0])}");
        Console.WriteLine(value: $"{("╚" + Repeat(stringToRepeat: "═", repeat: magicSpacer) + "╝").Pastel(hexColor: Colors[0])}");
    }

    public static Task Welcome()
    {
        if (!Console.IsOutputRedirected)
            return Task.CompletedTask;

        Console.WriteLine(value: ("╔" + Repeat(stringToRepeat: "═", repeat: 46) + "╗").Pastel(hexColor: "#00a10d"));
        Console.WriteLine(
            value: $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
                   + @"     "
                   + "Welcome to NoMercy MediaServer".Pastel(hexColor: "#5ffa71")
                   + "     ".Pastel(hexColor: "#ffffff")
                   + $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
        );
        Console.WriteLine(
            value: $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
                   + @"      "
                   + "Version:".Pastel(hexColor: "#cccccc")
                   + "  1.0.0      ".Pastel(hexColor: "#ffffff")
                   + $"{_(color: "#00a10d")}".Pastel(hexColor: "#00a10d")
        );
        Console.WriteLine(value: ("╚" + Repeat(stringToRepeat: "═", repeat: 46) + "╝").Pastel(hexColor: "#00a10d"));

        return Task.CompletedTask;
    }

    private static void SetConsoleSize(int width, int height)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Console.SetWindowSize(
                    width: Math.Min(val1: width, val2: Console.LargestWindowWidth),
                    height: Math.Min(val1: height, val2: Console.LargestWindowHeight)
                );
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                Console.Write(value: $"\x1b[8;{height};{width}t");
        }
        catch (Exception ex)
        {
            Console.WriteLine(value: $"Failed to resize console: {ex.Message}");
        }
    }

    private static void ClearConsole()
    {
        Console.Clear();
        Console.SetCursorPosition(left: 0, top: 0);
    }
}
