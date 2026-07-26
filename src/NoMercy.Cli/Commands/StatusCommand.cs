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
using NoMercy.Cli.Models;

namespace NoMercy.Cli.Commands;

internal static class StatusCommand
{
    public static Command Create(Option<string?> pipeOption, ICliClientFactory clientFactory)
    {
        Command command = new("status") { Description = "Show server status" };

        command.SetAction(
            async (parseResult, ct) =>
            {
                string? pipe = parseResult.GetValue(pipeOption);
                using ICliClient client = clientFactory.Create(pipe);
                StatusResponse? status = await client.GetAsync<StatusResponse>(
                    ApiRoutes.Status,
                    ct
                );

                if (status is null)
                {
                    await Console.Error.WriteLineAsync("Could not connect to server.");
                    return (int)ExitCode.ServerError;
                }

                TimeSpan uptime = TimeSpan.FromSeconds(status.UptimeSeconds);

                Console.WriteLine($"Status:       {status.Status}");
                Console.WriteLine($"Server:       {status.ServerName}");
                Console.WriteLine($"Version:      {status.Version}");
                Console.WriteLine($"Platform:     {status.Platform} ({status.Architecture})");
                Console.WriteLine($"OS:           {status.Os}");
                Console.WriteLine($"Uptime:       {FormatUptime(uptime)}");
                Console.WriteLine($"Started:      {status.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
                if (status.IsDev)
                    Console.WriteLine("Mode:         Development");

                if (!string.IsNullOrEmpty(status.InternalAddress))
                    Console.WriteLine($"Local:        {status.InternalAddress}");

                // "Can people outside my network reach this?" is the question the server was
                // answering internally every boot and telling nobody. It belongs here, in the
                // first place anyone looks.
                if (status.Connectivity is { } connectivity)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Remote access: {DescribeState(connectivity)}");

                    if (!string.IsNullOrEmpty(status.ExternalAddress))
                        Console.WriteLine($"Remote URL:    {status.ExternalAddress}");

                    if (
                        !string.Equals(
                            connectivity.Mode,
                            "Auto",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        Console.WriteLine($"Pinned to:     {connectivity.Mode}");

                    if (
                        string.Equals(
                            connectivity.TunnelAvailability,
                            "CheckFailed",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        Console.WriteLine(
                            "Tunnel:        could not be checked — the API was unreachable"
                        );
                }

                return (int)ExitCode.Success;
            }
        );

        return command;
    }

    /// <summary>
    /// Plain-language rendering of the connectivity state. The enum names are accurate but
    /// mean nothing to someone trying to work out why a friend cannot connect.
    /// </summary>
    internal static string DescribeState(ConnectivityResponse connectivity)
    {
        return connectivity.State switch
        {
            "Tunneled" => "yes, through a Cloudflare tunnel",
            "DirectAccess" => connectivity.PortForwarded
                ? "yes, directly via port forwarding"
                : "yes, directly (unverified port forward)",
            "HolePunched" => "yes, via STUN hole punching",
            "LocalOnly" => "no — reachable on this network only",
            "Evaluating" => "still working it out",
            "Starting" => "not evaluated yet",
            _ => connectivity.State ?? "unknown",
        };
    }

    internal static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
    }
}
