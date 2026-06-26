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

using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Providers.TMDB.Models.Collections;

namespace NoMercy.MediaProcessing.Collections;

public interface ICollectionRepository
{
    public Task Store(Collection collection);
    public Task LinkToLibrary(Library library, Collection collection);
    public Task LinkToMovies(TmdbCollectionAppends collection);
    public Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles);
    public Task StoreTranslations(IEnumerable<Translation> translations);
    public Task StoreImages(IEnumerable<Image> images);
}
