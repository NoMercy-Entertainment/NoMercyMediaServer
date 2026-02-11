using Microsoft.EntityFrameworkCore;
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
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Server.Seeds.Data;
using Serilog.Events;

namespace NoMercy.Server.Seeds;

public static class EncoderProfilesSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        Logger.Setup("Adding Encoder Profiles", LogEventLevel.Verbose);

        List<EncoderProfile> encoderProfiles;
        if (File.Exists(AppFiles.EncoderProfilesSeedFile))
            encoderProfiles = File.ReadAllTextAsync(AppFiles.EncoderProfilesSeedFile).Result
                .FromJson<List<EncoderProfile>>()!;
        else
            encoderProfiles = EncoderProfileSeedData.GetEncoderProfiles();

        await File.WriteAllTextAsync(AppFiles.EncoderProfilesSeedFile, encoderProfiles.ToJson());

        try
        {
            await dbContext.EncoderProfiles.UpsertRange(encoderProfiles)
                .On(v => new { v.Id })
                .WhenMatched((vs, vi) => new()
                {
                    Id = vi.Id,
                    Name = vi.Name,
                    Container = vi.Container,
                    Param = vi.Param,
                    _videoProfiles = vi._videoProfiles,
                    _audioProfiles = vi._audioProfiles,
                    _subtitleProfiles = vi._subtitleProfiles
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }

        List<EncoderProfileFolder> encoderProfileFolders = [];
        foreach (EncoderProfile encoderProfile in encoderProfiles)
            encoderProfileFolders.AddRange(encoderProfile.EncoderProfileFolder.ToList()
                .Select(encoderProfileFolder => new EncoderProfileFolder
                {
                    EncoderProfileId = encoderProfile.Id,
                    FolderId = encoderProfileFolder.FolderId
                }));

        try
        {
            await dbContext.EncoderProfileFolder
                .UpsertRange(encoderProfileFolders)
                .On(v => new { v.FolderId, v.EncoderProfileId })
                .WhenMatched((vs, vi) => new()
                {
                    FolderId = vi.FolderId,
                    EncoderProfileId = vi.EncoderProfileId
                })
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
