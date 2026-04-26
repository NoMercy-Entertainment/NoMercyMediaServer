using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Service.Seeds.Data;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

public static class EncoderProfilesSeed
{
    public static async Task Init(this MediaContext dbContext)
    {
        Logger.Setup("Adding Encoder Profiles", LogEventLevel.Verbose);

        List<EncoderProfile> encoderProfiles;
        if (File.Exists(AppFiles.EncoderProfilesSeedFile))
            encoderProfiles = File.ReadAllTextAsync(AppFiles.EncoderProfilesSeedFile)
                .Result.FromJson<List<EncoderProfile>>()!;
        else
            encoderProfiles = EncoderProfileSeedData.GetEncoderProfiles();

        // Strip any folder bindings before re-serializing — the seed file is
        // a catalog of profiles, never a record of user-driven assignments.
        // The dashboard owns EncoderProfileFolder rows; if the file keeps
        // them, removed bindings come back on every restart.
        foreach (EncoderProfile profile in encoderProfiles)
            profile.EncoderProfileFolder = [];

        await File.WriteAllTextAsync(AppFiles.EncoderProfilesSeedFile, encoderProfiles.ToJson());

        try
        {
            await dbContext
                .EncoderProfiles.UpsertRange(encoderProfiles)
                .On(v => new { v.Id })
                .WhenMatched(
                    (vs, vi) =>
                        new()
                        {
                            Id = vi.Id,
                            Name = vi.Name,
                            Container = vi.Container,
                            Param = vi.Param,
                            _videoProfiles = vi._videoProfiles,
                            _audioProfiles = vi._audioProfiles,
                            _subtitleProfiles = vi._subtitleProfiles,
                            _thumbnailProfile = vi._thumbnailProfile,
                        }
                )
                .RunAsync();
        }
        catch (Exception e)
        {
            Logger.Setup(e.Message, LogEventLevel.Fatal);
        }
    }
}
