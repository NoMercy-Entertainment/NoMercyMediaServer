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

namespace NoMercy.Encoder.BuildingBlocks;

public interface IFilterGraphBuilder
{
    IFilterGraphBuilder AddFilter(string inputLabel, string filter, string outputLabel);
    IFilterGraphBuilder AddSplit(string inputLabel, string[] outputLabels);
    IFilterGraphBuilder AddScale(string inputLabel, int width, int height, string outputLabel);
    IFilterGraphBuilder AddScaleWidth(string inputLabel, int width, string outputLabel);
    IFilterGraphBuilder AddGpuScale(
        string inputLabel,
        string scaleFilter,
        int width,
        int height,
        string outputLabel
    );
    IFilterGraphBuilder AddTonemap(string inputLabel, string algorithm, string outputLabel);
    IFilterGraphBuilder AddLibplaceboTonemap(
        string inputLabel,
        string algorithm,
        string outputLabel
    );
    IFilterGraphBuilder AddDeinterlace(
        string inputLabel,
        string outputLabel,
        string method = "yadif"
    );
    IFilterGraphBuilder AddCrop(
        string inputLabel,
        int width,
        int height,
        int x,
        int y,
        string outputLabel
    );
    string Build();
    bool HasFilters { get; }
}
