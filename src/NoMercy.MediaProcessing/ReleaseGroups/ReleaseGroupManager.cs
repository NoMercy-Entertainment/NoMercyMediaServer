using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Images;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
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
                ? $"/{coverPalette.Url.FileName()}" : null,
            _colorPalette = coverPalette?.Palette ?? string.Empty,
        };
        
        await releaseGroupRepository.Store(insert);
        
        Logger.MusicBrainz($"Release Group {releaseGroup.Title} stored", LogEventLevel.Verbose);
    }
    
}