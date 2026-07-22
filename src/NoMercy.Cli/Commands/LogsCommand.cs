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

using System.CommandLine;
using System.Drawing;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NoMercy.Cli.Models;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Extensions;
using Pastel;

namespace NoMercy.Cli.Commands;

internal static partial class LogsCommand
{
    private static DateTime _lastEntryTime = DateTime.MinValue;

    [GeneratedRegex(pattern: @"(\x1b|\\u001[bB])\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiEscapeRegex();

    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Option<int> tailOption = new(name: "--tail", aliases: "-n")
        {
            Description = "Number of log entries to show",
            DefaultValueFactory = _ => 100,
        };

        Option<bool> followOption = new(name: "--follow", aliases: "-f")
        {
            Description = "Stream logs in real-time",
            DefaultValueFactory = _ => false,
        };

        Option<string?> levelOption = new(name: "--level")
        {
            Description = "Filter by log level (e.g. Information,Warning,Error)",
        };

        Option<string?> typeOption = new(name: "--type") { Description = "Filter by log type" };

        Command command = new(name: "logs") { Description = "View server logs" };
        command.Options.Add(item: tailOption);
        command.Options.Add(item: followOption);
        command.Options.Add(item: levelOption);
        command.Options.Add(item: typeOption);

        command.SetAction(
            action: async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(option: pipeOption);
                int tail = parseResult.GetValue(option: tailOption);
                bool follow = parseResult.GetValue(option: followOption);
                string? level = parseResult.GetValue(option: levelOption);
                string? type = parseResult.GetValue(option: typeOption);

                using ICliClient client = clientFactory.Create(pipeNameOrSocketPath: pipe);

                // Fetch initial batch
                string query = BuildQuery(tail: tail, level: level, type: type);
                List<LogEntryResponse>? logs = await client.GetAsync<List<LogEntryResponse>>(
                    path: $"{ApiRoutes.Logs}{query}",
                    cancellationToken: ct
                );

                if (logs is null)
                {
                    await Console.Error.WriteLineAsync(value: "Could not connect to server.");
                    return (int)ExitCode.ServerError;
                }

                foreach (LogEntryResponse entry in logs)
                    PrintEntry(entry: entry);

                if (!follow)
                    return (int)ExitCode.Success;

                // Stream via SSE
                using IpcClient ipc = new(pipeNameOrSocketPath: pipe);
                try
                {
                    using HttpResponseMessage response = await ipc.GetStreamAsync(
                        requestUri: $"{ApiRoutes.LogsStream}?backfill=0",
                        cancellationToken: ct
                    );

                    await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken: ct);
                    using StreamReader reader = new(stream: stream);

                    while (!ct.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(cancellationToken: ct);
                        if (line is null)
                            break;
                        if (!line.StartsWith(value: "data: "))
                            continue;

                        string json = line[6..];
                        LogEntryResponse? entry;
                        try
                        {
                            entry = JsonConvert.DeserializeObject<LogEntryResponse>(value: json);
                        }
                        catch (JsonException)
                        {
                            // Truncated SSE line during a server restart — skip
                            // and wait for the next clean event instead of
                            // killing the whole `nomercy logs` session.
                            continue;
                        }
                        if (entry is null)
                            continue;

                        // Apply client-side filters
                        if (
                            !string.IsNullOrWhiteSpace(value: level)
                            && !level
                                .Split(separator: ',')
                                .Any(predicate: l =>
                                    string.Equals(
                                        a: l.Trim(),
                                        b: entry.Level,
                                        comparisonType: StringComparison.OrdinalIgnoreCase
                                    )
                                )
                        )
                            continue;

                        if (
                            !string.IsNullOrWhiteSpace(value: type)
                            && !type.Split(separator: ',')
                                .Any(predicate: t =>
                                    string.Equals(
                                        a: t.Trim(),
                                        b: entry.Type,
                                        comparisonType: StringComparison.OrdinalIgnoreCase
                                    )
                                )
                        )
                            continue;

                        PrintEntry(entry: entry);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected on Ctrl+C
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync(value: $"Stream disconnected: {ex.Message}");
                    return (int)ExitCode.ServerError;
                }

                return (int)ExitCode.Success;
            }
        );

        return command;
    }

    private static void PrintEntry(LogEntryResponse entry)
    {
        if (_lastEntryTime != DateTime.MinValue && entry.Time < _lastEntryTime)
            PrintSessionSeparator();

        _lastEntryTime = entry.Time;

        string message = CleanMessage(message: entry.Message);
        string timestamp = entry
            .Time.ToLocalTime()
            .ToString(format: "d-M-yyyy HH:mm")
            .Pastel(color: Color.DarkGray);
        string typeName = entry.Type.ToTitleCase().PadLeft(totalWidth: 14);

        if (!string.IsNullOrEmpty(value: entry.Color))
            typeName = typeName.Pastel(hexColor: entry.Color);

        Console.WriteLine(value: $"{timestamp} {typeName} | {message}");
    }

    private static void PrintSessionSeparator()
    {
        string separator = new(c: '-', count: 60);
        Console.WriteLine();
        Console.WriteLine(
            value: $"{"", 16}{"Server Restart".PadLeft(totalWidth: 14)} | {separator}".Pastel(color: Color.DarkGray)
        );
        Console.WriteLine();
    }

    private static string CleanMessage(string message)
    {
        // Strip surrounding quotes from double-serialization
        if (message is ['"', _, ..] && message[^1] == '"')
            message = message[1..^1];

        // Strip ANSI escape codes
        message = AnsiEscapeRegex().Replace(input: message, replacement: "");

        // Unescape JSON escapes from double-serialization
        message = message
            .Replace(oldValue: "\\n", newValue: "\n")
            .Replace(oldValue: "\\r", newValue: "\r")
            .Replace(oldValue: "\\t", newValue: "\t")
            .Replace(oldValue: "\\\"", newValue: "\"")
            .Replace(oldValue: @"\\", newValue: "\\");

        return message;
    }

    internal static string BuildQuery(int tail, string? level, string? type)
    {
        List<string> parts = [$"tail={tail}"];
        if (!string.IsNullOrWhiteSpace(value: level))
            parts.Add(item: $"levels={Uri.EscapeDataString(stringToEscape: level)}");
        if (!string.IsNullOrWhiteSpace(value: type))
            parts.Add(item: $"types={Uri.EscapeDataString(stringToEscape: type)}");
        return "?" + string.Join(separator: "&", values: parts);
    }
}
