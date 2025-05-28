using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Music.DTO;

public record ArtistResponseDto
{
    [JsonProperty("data")] public ArtistResponseItemDto? Data { get; set; }

    public static readonly Func<MediaContext, Guid, Guid, Task<Artist?>> GetArtist =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId, Guid id) =>
            mediaContext.Artists
                .AsNoTracking()
                .Where(album => album.Id == id)
                .Where(artist => artist.Library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null)

                .Include(artist => artist.Library)

                .Include(artist => artist.ArtistUser
                    .Where(artistUser => artistUser.UserId.Equals(userId))
                )
                .ThenInclude(artistUser => artistUser.User)
                .ThenInclude(user => user.TrackUser)
                .ThenInclude(trackUser => trackUser.Track)

                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ThenInclude(albumArtist => albumArtist.AlbumUser)

                .Include(artist => artist.AlbumArtist)
                .ThenInclude(albumArtist => albumArtist.Album)
                .ThenInclude(artist => artist.Translations)

                .Include(artist => artist.ArtistTrack
                    .OrderBy(artistTrack => artistTrack.Track.TrackNumber)
                    .ThenBy(artistTrack => artistTrack.Track.DiscNumber)
                )
                .ThenInclude(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .ThenInclude(album => album.AlbumArtist)
                .ThenInclude(album => album.Artist)
                .ThenInclude(artist => artist.Images)

                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .ThenInclude(album => album.AlbumUser
                    .Where(albumUser => albumUser.UserId.Equals(userId))
                )

                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.AlbumTrack)
                .ThenInclude(albumTrack => albumTrack.Album)
                .ThenInclude(artist => artist.Translations)

                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.TrackUser
                    .Where(trackUser => trackUser.UserId.Equals(userId))
                )
                .ThenInclude(trackUser => trackUser.User)
                
                .Include(artist => artist.ArtistTrack)
                .ThenInclude(artistTrack => artistTrack.Track)
                .ThenInclude(track => track.MusicPlays
                    .Where(trackUser => trackUser.UserId.Equals(userId))
                )
                .ThenInclude(trackUser => trackUser.User)

                .Include(artist => artist.ArtistReleaseGroup)
                .ThenInclude(artistReleaseGroup => artistReleaseGroup.ReleaseGroup)
                .ThenInclude(releaseGroup => releaseGroup.AlbumReleaseGroup)
                .ThenInclude(albumReleaseGroup => albumReleaseGroup.Album)
                .ThenInclude(artist => artist.Translations)

                .Include(artist => artist.ArtistReleaseGroup)
                .ThenInclude(artistReleaseGroup => artistReleaseGroup.ReleaseGroup)
                .ThenInclude(releaseGroup => releaseGroup.AlbumReleaseGroup)
                .ThenInclude(albumReleaseGroup => albumReleaseGroup.Album)
                .ThenInclude(album => album.AlbumArtist)

                .Include(artist => artist.Images)

                .Include(artist => artist.Translations)

                .Include(artist => artist.ArtistMusicGenre)
                .ThenInclude(artistMusicGenre => artistMusicGenre.MusicGenre)

                .FirstOrDefault());
}
