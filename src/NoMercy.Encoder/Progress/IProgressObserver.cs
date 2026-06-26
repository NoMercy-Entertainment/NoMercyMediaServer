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

using NoMercy.Encoder.Errors;

namespace NoMercy.Encoder.Progress;

public interface IProgressObserver
{
    void OnStageStarted(string stageName);

    void OnProgress(EncodingProgress progress);

    void OnStageCompleted(string stageName, TimeSpan duration);

    void OnCompleted();

    void OnError(EncodingError error);

    void OnPlanResolved(
        List<string> videoStreams,
        List<string> audioStreams,
        List<string> subtitleStreams,
        bool hasGpu,
        bool isHdr
    );
}
