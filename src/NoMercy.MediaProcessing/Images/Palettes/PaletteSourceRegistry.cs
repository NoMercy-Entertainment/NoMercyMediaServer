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

namespace NoMercy.MediaProcessing.Images.Palettes;

public class PaletteSourceRegistry
{
    private readonly Dictionary<string, IPaletteSource> _sources;

    public PaletteSourceRegistry(IEnumerable<IPaletteSource> sources)
    {
        _sources = sources.ToDictionary(keySelector: s => s.EntityType, elementSelector: s => s);
    }

    public IPaletteSource? Resolve(string entityType) => _sources.GetValueOrDefault(key: entityType);

    public IReadOnlyCollection<string> EntityTypes => _sources.Keys.ToList();
}
