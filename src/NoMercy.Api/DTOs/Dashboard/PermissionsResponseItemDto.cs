using Newtonsoft.Json;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Dashboard;

public class PermissionsResponseItemDto : User
{
    [JsonProperty("library_user")] public new LibraryUserDto[] LibraryUser { get; set; }
    [JsonProperty("libraries")] public Ulid[] Libraries { get; set; }

    public PermissionsResponseItemDto(User user)
    {
        Id = user.Id;
        Email = user.Email;
        Manage = user.Manage;
        Owner = user.Owner;
        Name = user.Name;
        Allowed = user.Allowed;
        AudioTranscoding = user.AudioTranscoding;
        VideoTranscoding = user.VideoTranscoding;
        NoTranscoding = user.NoTranscoding;
        CreatedAt = user.CreatedAt;

        LibraryUser = user.LibraryUser
            .Select(libraryUser => new LibraryUserDto
            {
                LibraryId = libraryUser.LibraryId,
                UserId = libraryUser.UserId
            })
            .ToArray();

        Libraries = user.LibraryUser
            .Select(libraryUser => libraryUser.Library.Id)
            .ToArray();
    }
}