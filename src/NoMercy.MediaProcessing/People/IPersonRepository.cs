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

using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.MediaProcessing.People;

public interface IPersonRepository
{
    public Task Store(IEnumerable<Person> people);
    public Task StoreTranslationsAsync(IEnumerable<Translation> translations);
    public Task StoreImagesAsync(IEnumerable<Image> images);

    public Task StoreCast(IEnumerable<Cast> cast, Type type);
    public Task StoreCrew(IEnumerable<Crew> crew, Type type);
    public Task StoreCreatorAsync(Creator creator);
    public Task StoreGuestStarsAsync(IEnumerable<GuestStar> guestStars);

    public Task StoreRoles(IEnumerable<Role> roles);
    public Task StoreJobs(IEnumerable<Job> job);

    public Task StoreAggregateCreditsAsync();
    public Task StoreAggregateCastAsync();
    public Task StoreAggregateCrewAsync();
    List<int> GetIds();
}
