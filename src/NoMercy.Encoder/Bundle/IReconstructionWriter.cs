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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Output;
using NoMercy.Storage;

namespace NoMercy.Encoder.Bundle;

public interface IReconstructionWriter
{
    /// <summary>
    /// Build a <see cref="Reconstruction"/> record from the encode inputs.
    /// Pure function — does not perform any I/O.
    /// </summary>
    Reconstruction Build(MediaInfo mediaInfo, OutputPlan plan, BundleLayout layout);

    /// <summary>
    /// Serialise <paramref name="mediaInfo"/> + <paramref name="plan"/> into
    /// <paramref name="path"/> inside <paramref name="storage"/>.
    /// Writes unconditionally — even for copy-mode (lossless) encodes.
    /// </summary>
    Task WriteAsync(
        IStorage storage,
        string path,
        MediaInfo mediaInfo,
        OutputPlan plan,
        BundleLayout layout,
        CancellationToken ct
    );

    /// <summary>
    /// Deserialise a previously written <c>reconstruction.json</c>.
    /// Returns <c>null</c> when the path does not exist in storage.
    /// </summary>
    Task<Reconstruction?> ReadAsync(IStorage storage, string path, CancellationToken ct);
}
