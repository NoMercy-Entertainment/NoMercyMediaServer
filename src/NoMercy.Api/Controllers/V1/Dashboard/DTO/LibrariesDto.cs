
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.V1.Dashboard.DTO;

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
