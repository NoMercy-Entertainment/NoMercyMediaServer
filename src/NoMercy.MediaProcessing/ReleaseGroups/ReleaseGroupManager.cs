using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Providers.MusicBrainz.Models;
using Serilog.Events;

namespace NoMercy.MediaProcessing.ReleaseGroups;

public class ReleaseGroupManager(IReleaseGroupRepository releaseGroupRepository) : BaseManager, IReleaseGroupManager
{
    public async Task Store(MusicBrainzReleaseGroup releaseGroup, Ulid id, CoverArtImageManagerManager.CoverPalette? coverPalette)
    {
        Logger.MusicBrainz($"Storing Release Group: {releaseGroup.Title}", LogEventLevel.Verbose);

        ReleaseGroup insert = new()
        {
            Id = releaseGroup.Id,
            Title = releaseGroup.Title,
            Description = string.IsNullOrEmpty(releaseGroup.Disambiguation)
                ? null
                : releaseGroup.Disambiguation,
            Year = releaseGroup.FirstReleaseDate.ParseYear(),
            LibraryId = id,
            Disambiguation = string.IsNullOrEmpty(releaseGroup.Disambiguation)
                ? null
                : releaseGroup.Disambiguation,

            Cover = coverPalette?.Url is not null
                ? $"/{coverPalette.Url.FileName()}"
                : null,
        };

        await releaseGroupRepository.Store(insert);
        
        Logger.MusicBrainz($"Release Group {releaseGroup.Title} stored", LogEventLevel.Verbose);
    }
}