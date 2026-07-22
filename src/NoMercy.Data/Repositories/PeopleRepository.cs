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

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.People;

namespace NoMercy.Data.Repositories;

public class PeopleRepository(MediaContext context) : IPeopleRepository
{
    public Task<List<Person>> GetPeopleAsync(
        Guid userId,
        string language,
        int take,
        int page = 0,
        CancellationToken ct = default
    )
    {
        return context
            .People.AsNoTracking()
            .Where(predicate: person =>
                person.Casts.Any(cast =>
                    cast.Tv != null
                    && cast.Tv.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId))
                        != null
                )
                || person.Casts.Any(cast =>
                    cast.Movie != null
                    && cast.Movie.Library.LibraryUsers.FirstOrDefault(u => u.UserId.Equals(userId))
                        != null
                )
            )
            .Include(navigationPropertyPath: person =>
                person.Translations.Where(translation => translation.Iso6391 == language)
            )
            .OrderByDescending(keySelector: person => person.Popularity)
            .ThenBy(keySelector: person => person.Id)
            .Skip(count: page * take)
            .Take(count: take)
            .ToListAsync(cancellationToken: ct);
    }

    public Task<Person?> GetPersonWithCreditsAsync(int id, CancellationToken ct = default)
    {
        return context
            .People.AsNoTracking()
            .Where(predicate: person => person.Id == id)
            .Include(navigationPropertyPath: person => person.Casts)
                .ThenInclude(navigationPropertyPath: cast => cast.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie!.VideoFiles)
            .Include(navigationPropertyPath: person => person.Casts)
                .ThenInclude(navigationPropertyPath: cast => cast.Tv)
                    .ThenInclude(navigationPropertyPath: tv => tv!.Episodes)
                        .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
            .Include(navigationPropertyPath: person => person.Crews)
                .ThenInclude(navigationPropertyPath: crew => crew.Movie)
                    .ThenInclude(navigationPropertyPath: movie => movie!.VideoFiles)
            .Include(navigationPropertyPath: person => person.Crews)
                .ThenInclude(navigationPropertyPath: crew => crew.Tv)
                    .ThenInclude(navigationPropertyPath: tv => tv!.Episodes)
                        .ThenInclude(navigationPropertyPath: episode => episode.VideoFiles)
            .FirstOrDefaultAsync(cancellationToken: ct);
    }
}
