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

namespace NoMercy.Setup.Server;

/// <summary>
/// Reads the CPU architecture a native executable was built for, from its
/// PE / ELF / Mach-O header. Exists because a release once shipped with an
/// arch-blind asset picker, leaving ARM64 binaries on x64 machines — a binary
/// that passes the version check but can never start. Returns null when the
/// format is not recognized, so callers never force a re-download on a guess.
/// </summary>
internal static class ExecutableArchitecture
{
    private const ushort PeMachineX86 = 0x014C;
    private const ushort PeMachineX64 = 0x8664;
    private const ushort PeMachineArm64 = 0xAA64;

    private const ushort ElfMachineX86 = 0x0003;
    private const ushort ElfMachineX64 = 0x003E;
    private const ushort ElfMachineArm64 = 0x00B7;

    private const uint MachOMagic64 = 0xFEEDFACF;
    private const uint MachOCpuTypeX64 = 0x01000007;
    private const uint MachOCpuTypeArm64 = 0x0100000C;

    public static Architecture? Read(Stream stream)
    {
        byte[] header = new byte[64];
        int bytesRead = ReadUpTo(stream, header);
        if (bytesRead < 8)
            return null;

        if (header[0] == (byte)'M' && header[1] == (byte)'Z')
            return ReadPe(stream, header, bytesRead);

        if (
            header[0] == 0x7F
            && header[1] == (byte)'E'
            && header[2] == (byte)'L'
            && header[3] == (byte)'F'
        )
            return ReadElf(header, bytesRead);

        if (BitConverter.ToUInt32(header, 0) == MachOMagic64)
            return ReadMachO(header);

        return null;
    }

    public static bool MatchesProcess(Stream stream)
    {
        Architecture? binaryArchitecture = Read(stream);
        return binaryArchitecture is null
            || binaryArchitecture == RuntimeInformation.ProcessArchitecture;
    }

    private static Architecture? ReadPe(Stream stream, byte[] header, int bytesRead)
    {
        if (bytesRead < 64 || !stream.CanSeek)
            return null;

        uint peOffset = BitConverter.ToUInt32(header, 0x3C);
        stream.Seek(peOffset, SeekOrigin.Begin);

        byte[] peHeader = new byte[6];
        if (ReadUpTo(stream, peHeader) < 6)
            return null;

        if (
            peHeader[0] != (byte)'P'
            || peHeader[1] != (byte)'E'
            || peHeader[2] != 0
            || peHeader[3] != 0
        )
            return null;

        ushort machine = BitConverter.ToUInt16(peHeader, 4);
        return machine switch
        {
            PeMachineX64 => Architecture.X64,
            PeMachineArm64 => Architecture.Arm64,
            PeMachineX86 => Architecture.X86,
            _ => null,
        };
    }

    private static Architecture? ReadElf(byte[] header, int bytesRead)
    {
        if (bytesRead < 0x14)
            return null;

        ushort machine = BitConverter.ToUInt16(header, 0x12);
        return machine switch
        {
            ElfMachineX64 => Architecture.X64,
            ElfMachineArm64 => Architecture.Arm64,
            ElfMachineX86 => Architecture.X86,
            _ => null,
        };
    }

    private static Architecture? ReadMachO(byte[] header)
    {
        uint cpuType = BitConverter.ToUInt32(header, 4);
        return cpuType switch
        {
            MachOCpuTypeX64 => Architecture.X64,
            MachOCpuTypeArm64 => Architecture.Arm64,
            _ => null,
        };
    }

    private static int ReadUpTo(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                break;
            total += read;
        }

        return total;
    }
}
