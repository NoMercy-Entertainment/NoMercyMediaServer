using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.MusicGenres;

public class MusicGenreRepository(MediaContext context) : IMusicGenreRepository
{
    public Task Store(MusicGenre genre)
    {
        return context.MusicGenres.Upsert(genre)
            .On(v => new { v.Id })
            .WhenMatched(v => new()
            {
                Id = v.Id,
                Name = v.Name
            })
            .RunAsync();
    }

    public Task LinkToReleaseGroup(MusicGenreReleaseGroup genreReleaseGroup)
    {
        return context.MusicGenreReleaseGroup.Upsert(genreReleaseGroup)
            .On(e => new { e.GenreId, e.ReleaseGroupId })
            .WhenMatched((s, i) => new()
            {
                GenreId = i.GenreId,
                ReleaseGroupId = i.ReleaseGroupId
            })
            .RunAsync();
    }

    public Task LinkToArtist(IEnumerable<ArtistMusicGenre> genreArtists)
    {
        return context.ArtistMusicGenre.UpsertRange(genreArtists)
            .On(e => new { e.MusicGenreId, e.ArtistId })
            .WhenMatched((s, i) => new()
            {
                MusicGenreId = i.MusicGenreId,
                ArtistId = i.ArtistId
            })
            .RunAsync();
    }

    public Task LinkToRelease(IEnumerable<AlbumMusicGenre> genreReleases)
    {
        return context.AlbumMusicGenre.UpsertRange(genreReleases)
            .On(e => new { e.MusicGenreId, e.AlbumId })
            .WhenMatched((s, i) => new()
            {
                MusicGenreId = i.MusicGenreId,
                AlbumId = i.AlbumId
            })
            .RunAsync();
    }

    public Task LinkToRecording(IEnumerable<MusicGenreTrack> genreRecordings)
    {
        return context.MusicGenreTrack.UpsertRange(genreRecordings)
            .On(e => new { e.GenreId, e.TrackId })
            .WhenMatched((s, i) => new()
            {
                GenreId = i.GenreId,
                TrackId = i.TrackId
            })
            .RunAsync();
    }
}