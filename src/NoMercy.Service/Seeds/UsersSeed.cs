using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Users;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Seeds.Dto;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class UsersSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        try
        {
            bool hasUsers = await dbContext.Users.AnyAsync();
            if (hasUsers) return;

            Logger.Setup("Adding Users", LogEventLevel.Verbose);

            Dictionary<string, string> queryParams = new()
            {
                ["id"] = Info.DeviceId.ToString(),
                ["with_self"] = "true"
            };

            GenericHttpClient authClient = new(Config.ApiServerBaseUrl, 10, 0);
            authClient.SetDefaultHeaders(Config.UserAgent, Globals.Globals.AccessToken);
            string response = await authClient.SendAndReadAsync(HttpMethod.Get, "server-users", null, queryParams);

            if (response == null) throw new("Failed to get Server info");

            ServerUserDtoData[] serverUsers = response.FromJson<ServerUserDto>()?.Data ?? [];

            Logger.Setup($"Found {serverUsers.Length} users", LogEventLevel.Verbose);

            User[] users = serverUsers.Select(serverUser => new User
            {
                Id = Guid.Parse(serverUser.UserId),
                Email = serverUser.Email,
                Name = serverUser.Name,
                Allowed = true,
                AudioTranscoding = serverUser.Enabled,
                NoTranscoding = serverUser.Enabled,
                VideoTranscoding = serverUser.Enabled,
                Owner = serverUser.IsOwner
            })
            .ToArray();

            await dbContext.Users
                .UpsertRange(users)
                .On(v => new { v.Id })
                .WhenMatched((us, ui) => new()
                {
                    Id = ui.Id,
                    Email = ui.Email,
                    Name = ui.Name,
                    Allowed = ui.Allowed,
                    Manage = us.Manage,
                    AudioTranscoding = ui.AudioTranscoding,
                    NoTranscoding = ui.NoTranscoding,
                    VideoTranscoding = ui.VideoTranscoding,
                    Owner = ui.Owner
                })
                .RunAsync();

            if (!File.Exists(AppFiles.LibrariesSeedFile)) return;

            Library[] libraries = File.ReadAllTextAsync(AppFiles.LibrariesSeedFile)
                .Result.FromJson<Library[]>() ?? [];

            List<LibraryUser> libraryUsers = [];

            foreach (User user in users.ToList())
            {
                foreach (Library library in libraries.ToList())
                    libraryUsers.Add(new()
                    {
                        LibraryId = library.Id,
                        UserId = user.Id
                    });

                await dbContext.LibraryUser
                    .UpsertRange(libraryUsers)
                    .On(v => new { v.LibraryId, v.UserId })
                    .WhenMatched((lus, lui) => new()
                    {
                        LibraryId = lui.LibraryId,
                        UserId = lui.UserId
                    })
                    .RunAsync();
            }
        }
        catch (Exception e)
        {
            Logger.Setup($"Users seed failed: {e.Message}", LogEventLevel.Warning);
        }
        
    }
}
