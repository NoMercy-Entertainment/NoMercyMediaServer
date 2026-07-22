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

namespace NoMercy.NmSystem.Information;

// Hardware/OS facts probed lazily: GPU/CPU enumeration and device-id lookup are
// expensive and were previously run eagerly at type load (any member access paid
// for all of them, and one failure poisoned the whole type). Each computed value
// is now a thread-safe, once-only Lazy so callers only pay for what they touch.
public static class Info
{
    public static string DeviceName { get; set; } = Environment.MachineName;

    // Eager on purpose: captures the moment the type is first touched (~app start).
    public static readonly DateTime StartTime = DateTime.UtcNow;

    private static readonly Lazy<Guid> LazyDeviceId = new(valueFactory: Software.GetDeviceId);
    private static Guid? _resolvedDeviceId;

    // Prefers the persisted/resolved server identity set by
    // DeviceIdentityResolver during startup Phase 1 (before anything else
    // observes DeviceId); falls back to the raw hardware fingerprint only if
    // resolution never ran. Never reassign this to a fresh hardware value once
    // set — that would break every already-registered server's DNS subdomain
    // and certificate CN.
    public static Guid DeviceId => _resolvedDeviceId ?? LazyDeviceId.Value;

    public static void SetResolvedDeviceId(Guid id)
    {
        _resolvedDeviceId = id;
    }

    private static readonly Lazy<string> LazyOs = new(valueFactory: () => RuntimeInformation.OSDescription);
    public static string Os => LazyOs.Value;

    private static readonly Lazy<string> LazyPlatform = new(valueFactory: Software.GetPlatform);
    public static string Platform => LazyPlatform.Value;

    private static readonly Lazy<string> LazyArchitecture = new(valueFactory: () =>
        RuntimeInformation.ProcessArchitecture.ToString()
    );
    public static string Architecture => LazyArchitecture.Value;

    private static readonly Lazy<List<string>> LazyCpuVendors = new(valueFactory: Cpu.Vendors);
    public static List<string> CpuVendors => LazyCpuVendors.Value;

    private static readonly Lazy<List<string>> LazyCpuNames = new(valueFactory: Cpu.Names);
    public static List<string> CpuNames => LazyCpuNames.Value;

    private static readonly Lazy<List<string>> LazyGpuVendors = new(valueFactory: Gpu.Vendors);
    public static List<string> GpuVendors => LazyGpuVendors.Value;

    private static readonly Lazy<List<string>> LazyGpuNames = new(valueFactory: Gpu.Names);
    public static List<string> GpuNames => LazyGpuNames.Value;

    private static readonly Lazy<string?> LazyOsVersion = new(valueFactory: Software.GetSystemVersion);
    public static string? OsVersion => LazyOsVersion.Value;

    private static readonly Lazy<DateTime> LazyBootTime = new(valueFactory: Software.GetBootTime);
    public static DateTime BootTime => LazyBootTime.Value;

    private static readonly Lazy<string> LazyExecSuffix = new(valueFactory: () =>
        Platform == "windows" ? ".exe" : ""
    );
    public static string ExecSuffix => LazyExecSuffix.Value;

    private static readonly Lazy<string> LazyIconSuffix = new(valueFactory: () =>
        Platform == "windows" ? ".ico"
        : Platform == "macos" ? ".icns"
        : ".png"
    );
    public static string IconSuffix => LazyIconSuffix.Value;
}
