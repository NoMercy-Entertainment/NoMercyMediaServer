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
using NoMercy.DiscFormat.Abstractions.Disc;

namespace NoMercy.DiscFormat.LibBluray.Identity;

/// Derives a Blu-ray's identity from the disc's own cryptographic identity — the AACS disc id read
/// via libbluray — combined with the UDF volume id. Two different discs (e.g. two seasons of the
/// same series) carry different AACS disc ids, so a swapped disc under the same mount path produces
/// a different identity. The native dependency stays inside the LibBluray project.
public sealed class BlurayIdentityReader : IDiscIdentityReader
{
    private readonly Func<ILibBluray> _clientFactory;

    public BlurayIdentityReader(Func<ILibBluray> clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public bool CanHandle(DiscKind kind)
    {
        return kind is DiscKind.Bdj or DiscKind.Hdmv;
    }

    public DiscIdentity Read(DiscTranspileRequest request)
    {
        string devicePath =
            request.DevicePath
            ?? throw new InvalidOperationException("Blu-ray identity needs a DevicePath.");

        using ILibBluray client = _clientFactory();
        client.Open(devicePath);

        BlurayDiscInfo info =
            client.DiscInfo
            ?? throw new InvalidOperationException("libbluray returned no disc info.");

        string provider = info.NumBdjTitles >= info.NumHdmvTitles ? "bdj" : "hdmv";
        byte[] skeleton = BuildSkeleton(info);
        return DiscIdentity.FromStructuralBytes(provider, skeleton);
    }

    private static byte[] BuildSkeleton(BlurayDiscInfo info)
    {
        StringBuilder skeleton = new();
        skeleton.Append("aacs:").Append(info.DiscIdHex).Append('\n');
        skeleton.Append("udf:").Append(info.UdfVolumeId ?? string.Empty).Append('\n');
        return Encoding.UTF8.GetBytes(skeleton.ToString());
    }
}
