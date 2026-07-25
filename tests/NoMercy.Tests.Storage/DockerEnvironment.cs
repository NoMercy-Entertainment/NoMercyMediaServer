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

using System.Diagnostics;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Single source of truth for "is Docker usable for tests on this machine".
/// Replaces the per-fixture copies that probed http://localhost:2375 first — a
/// TCP endpoint Docker Desktop disables by default, so that probe is the wrong
/// primary signal. Here the authoritative check is the Docker CLI (which honors
/// DOCKER_HOST / the Desktop named pipe); the TCP probe is only a CI/DinD
/// fallback.
/// </summary>
public static class DockerEnvironment
{
    private static bool? _cached;

    public static async Task<bool> IsAvailableAsync()
    {
        if (_cached is not null)
            return _cached.Value;

        _cached = await DockerInfoSucceedsAsync() || await TcpDaemonRespondsAsync();
        return _cached.Value;
    }

    private static async Task<bool> DockerInfoSucceedsAsync()
    {
        try
        {
            ProcessStartInfo psi = new("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TcpDaemonRespondsAsync()
    {
        try
        {
            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(3) };
            HttpResponseMessage response = await http.GetAsync("http://localhost:2375/info");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
