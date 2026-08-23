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

using NoMercy.DiscFormat.Abstractions.Disc;

namespace NoMercy.DiscFormat.Dvd.Identity;

/// Derives a DVD's identity from the structural header of every IFO it carries — the VMGI/VTSI
/// management tables (disc category, title and PGC layout) that are intrinsic to the disc, not to
/// where it happens to be mounted. The whole IFO is not hashed: only the first sector of each,
/// which holds the management header, so the id is stable while title-set VOB pointers further in
/// the file (which can shift between masterings of the same content) do not perturb it.
public sealed class DvdIdentityReader : IDiscIdentityReader
{
    private const int IfoHeaderSectorBytes = 2048;

    public bool CanHandle(DiscKind kind)
    {
        return kind == DiscKind.Dvd;
    }

    public DiscIdentity Read(DiscTranspileRequest request)
    {
        IReadOnlyDictionary<string, byte[]> ifoFiles =
            request.IfoFiles
            ?? throw new InvalidOperationException("DVD identity needs IFO files on the request.");

        byte[] skeleton = BuildSkeleton(ifoFiles);
        return DiscIdentity.FromStructuralBytes("dvd", skeleton);
    }

    private static byte[] BuildSkeleton(IReadOnlyDictionary<string, byte[]> ifoFiles)
    {
        IEnumerable<KeyValuePair<string, byte[]>> ordered = ifoFiles
            .Where(entry => entry.Key.EndsWith(".IFO", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase);

        using MemoryStream skeleton = new();
        foreach ((string name, byte[] bytes) in ordered)
        {
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(name.ToUpperInvariant());
            skeleton.Write(nameBytes);
            skeleton.WriteByte(0);

            int headerLength = Math.Min(bytes.Length, IfoHeaderSectorBytes);
            skeleton.Write(bytes, 0, headerLength);
        }

        return skeleton.ToArray();
    }
}
