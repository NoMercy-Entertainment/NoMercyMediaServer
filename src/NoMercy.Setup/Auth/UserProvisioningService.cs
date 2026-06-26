#region License
// Copyright NoMercy (c) 2026. All rights reserved.
#endregion

using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Users;
using NoMercy.Helpers.Extensions;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Setup.Auth;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly IDbContextFactory<MediaContext> _mediaContextFactory;

    public UserProvisioningService(IDbContextFactory<MediaContext> mediaContextFactory)
    {
        _mediaContextFactory = mediaContextFactory;
    }

    public async Task ProvisionOwner(User user)
    {
        await using MediaContext mediaContext = await _mediaContextFactory.CreateDbContextAsync();
        await mediaContext
            .Users.Upsert(user)
            .On(x => x.Id)
            .WhenMatched(
                (oldUser, newUser) =>
                    new()
                    {
                        Id = newUser.Id,
                        Name = newUser.Name,
                        Email = newUser.Email,
                        Owner = newUser.Owner,
                        Allowed = newUser.Allowed,
                        AudioTranscoding = newUser.AudioTranscoding,
                        NoTranscoding = newUser.NoTranscoding,
                        VideoTranscoding = newUser.VideoTranscoding,
                        Manage = newUser.Manage,
                    }
            )
            .RunAsync();

        ClaimsPrincipleExtensions.AddUser(user);
    }
}
