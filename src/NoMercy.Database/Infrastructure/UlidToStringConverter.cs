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

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NoMercy.Database;

public class UlidToStringConverter : ValueConverter<Ulid, string>
{
    private static readonly ConverterMappingHints DefaultHints = new(size: 26);

    public UlidToStringConverter()
        : this(mappingHints: null) { }

    private UlidToStringConverter(ConverterMappingHints? mappingHints = null)
        : base(convertToProviderExpression: x => x.ToString(), convertFromProviderExpression: x => Ulid.Parse(x), mappingHints: DefaultHints.With(hints: mappingHints)) { }
}
