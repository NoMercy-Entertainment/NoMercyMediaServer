
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(LanguageId), nameof(LibraryId))]
[Index(nameof(LanguageId))]
[Index(nameof(LibraryId))]
public class LanguageLibrary
{
    [JsonProperty("language_id")] public int LanguageId { get; set; }
    public Language Language { get; set; } = null!;


    [JsonProperty("library_id")] public Ulid LibraryId { get; set; }
    public Library Library { get; set; } = null!;

    public LanguageLibrary()
    {
        //
    }

    public LanguageLibrary(int languageId, Ulid libraryId)
    {
        LanguageId = languageId;
        LibraryId = libraryId;
    }
}
