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

using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Hardware;

public class DriverFingerprintTests
{
    private static GpuDriverInfo Nvidia(string driver = "31.0.15.4601") =>
        new(Vendor: "Nvidia", Model: "RTX 4090", DriverVersion: driver, Index: 0);

    private static GpuDriverInfo Intel(string driver = "31.0.101.2134") =>
        new(Vendor: "Intel", Model: "Arc A770", DriverVersion: driver, Index: 1);

    [Fact]
    public void ComputeHash_SameInputs_ProducesStableHash()
    {
        DriverFingerprint fp = new(Gpus: [Nvidia(), Intel()]);

        string first = fp.ComputeHash();
        string second = fp.ComputeHash();

        first.Should().Be(expected: second);
    }

    [Fact]
    public void ComputeHash_OrderInsensitive_SameHashRegardlessOfGpuOrder()
    {
        DriverFingerprint fp1 = new(Gpus: [Nvidia(), Intel()]);
        DriverFingerprint fp2 = new(Gpus: [Intel(), Nvidia()]);

        fp1.ComputeHash().Should().Be(expected: fp2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_ChangesWhenDriverVersionChanges()
    {
        DriverFingerprint fp1 = new(Gpus: [Nvidia()]);
        DriverFingerprint fp2 = new(Gpus: [Nvidia(driver: "31.0.15.5000")]);

        fp1.ComputeHash().Should().NotBe(unexpected: fp2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_ChangesWhenGpuAdded()
    {
        DriverFingerprint fp1 = new(Gpus: [Nvidia()]);
        DriverFingerprint fp2 = new(Gpus: [Nvidia(), Intel()]);

        fp1.ComputeHash().Should().NotBe(unexpected: fp2.ComputeHash());
    }

    [Fact]
    public void ComputeHash_HandlesEmptyDriverVersion_ProducesStableHash()
    {
        DriverFingerprint fp1 = new(Gpus: [new(Vendor: "Nvidia", Model: "RTX 4090", DriverVersion: string.Empty, Index: 0)]);
        DriverFingerprint fp2 = new(Gpus: [new(Vendor: "Nvidia", Model: "RTX 4090", DriverVersion: string.Empty, Index: 0)]);

        string hash = fp1.ComputeHash();

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Be(expected: fp2.ComputeHash());
    }
}
