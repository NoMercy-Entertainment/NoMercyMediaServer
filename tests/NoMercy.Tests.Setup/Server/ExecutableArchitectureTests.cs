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
using NoMercy.Setup.Server;

namespace NoMercy.Tests.Setup.Server;

[Trait("Category", "Unit")]
public class ExecutableArchitectureTests
{
    private static byte[] PeExecutable(ushort machine)
    {
        byte[] bytes = new byte[80];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        BitConverter.GetBytes(0x40u).CopyTo(bytes, 0x3C);
        bytes[0x40] = (byte)'P';
        bytes[0x41] = (byte)'E';
        BitConverter.GetBytes(machine).CopyTo(bytes, 0x44);
        return bytes;
    }

    private static byte[] ElfExecutable(ushort machine)
    {
        byte[] bytes = new byte[64];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';
        BitConverter.GetBytes(machine).CopyTo(bytes, 0x12);
        return bytes;
    }

    private static byte[] MachOExecutable(uint cpuType)
    {
        byte[] bytes = new byte[64];
        BitConverter.GetBytes(0xFEEDFACFu).CopyTo(bytes, 0);
        BitConverter.GetBytes(cpuType).CopyTo(bytes, 4);
        return bytes;
    }

    [Theory]
    [InlineData((ushort)0x8664, Architecture.X64)]
    [InlineData((ushort)0xAA64, Architecture.Arm64)]
    [InlineData((ushort)0x014C, Architecture.X86)]
    public void Read_PeHeader_ReturnsMachineArchitecture(ushort machine, Architecture expected)
    {
        using MemoryStream stream = new(PeExecutable(machine));

        ExecutableArchitecture.Read(stream).Should().Be(expected);
    }

    [Theory]
    [InlineData((ushort)0x003E, Architecture.X64)]
    [InlineData((ushort)0x00B7, Architecture.Arm64)]
    public void Read_ElfHeader_ReturnsMachineArchitecture(ushort machine, Architecture expected)
    {
        using MemoryStream stream = new(ElfExecutable(machine));

        ExecutableArchitecture.Read(stream).Should().Be(expected);
    }

    [Theory]
    [InlineData(0x01000007u, Architecture.X64)]
    [InlineData(0x0100000Cu, Architecture.Arm64)]
    public void Read_MachOHeader_ReturnsCpuArchitecture(uint cpuType, Architecture expected)
    {
        using MemoryStream stream = new(MachOExecutable(cpuType));

        ExecutableArchitecture.Read(stream).Should().Be(expected);
    }

    [Fact]
    public void Read_UnknownFormat_ReturnsNull()
    {
        using MemoryStream stream = new("#!/bin/sh\necho not a native binary\n"u8.ToArray());

        ExecutableArchitecture.Read(stream).Should().BeNull();
    }

    [Fact]
    public void Read_TruncatedFile_ReturnsNull()
    {
        using MemoryStream stream = new([(byte)'M', (byte)'Z']);

        ExecutableArchitecture.Read(stream).Should().BeNull();
    }

    [Fact]
    public void Read_PeWithUnknownMachine_ReturnsNull()
    {
        using MemoryStream stream = new(PeExecutable(0x1234));

        ExecutableArchitecture.Read(stream).Should().BeNull();
    }

    [Fact]
    public void MatchesProcess_WrongArchitecture_ReturnsFalse()
    {
        // The bug this guards against: an ARM64 windows ffmpeg on an x64 machine.
        ushort wrongMachine =
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? (ushort)0x8664
                : (ushort)0xAA64;
        using MemoryStream stream = new(PeExecutable(wrongMachine));

        ExecutableArchitecture.MatchesProcess(stream).Should().BeFalse();
    }

    [Fact]
    public void MatchesProcess_UnknownFormat_ReturnsTrue()
    {
        // Never force a re-download on a guess.
        using MemoryStream stream = new(new byte[32]);

        ExecutableArchitecture.MatchesProcess(stream).Should().BeTrue();
    }
}
