using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Dashboard;

public record LibrariesDto
{
    [JsonProperty("data")] public IEnumerable<LibrariesResponseItemDto> Data { get; set; } = [];

    public static readonly Func<MediaContext, Guid, IAsyncEnumerable<Library?>> GetLibraries =
        EF.CompileAsyncQuery((MediaContext mediaContext, Guid userId) =>
            mediaContext.Libraries.AsNoTracking()
                .Where(library => library.LibraryUsers
                    .FirstOrDefault(u => u.UserId.Equals(userId)) != null
                )
                .Include(library => library.FolderLibraries)
                .ThenInclude(folderLibrary => folderLibrary.Folder)
                .ThenInclude(folder => folder.EncoderProfileFolder)
                .ThenInclude(library => library.EncoderProfile)
                .Include(library => library.LanguageLibraries)
                .ThenInclude(languageLibrary => languageLibrary.Language));
}