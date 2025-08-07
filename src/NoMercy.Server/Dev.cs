using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using Image = SixLabors.ImageSharp.Image;

namespace NoMercy.Server;

public static class Dev
{
    public static async Task Run()
    {
        // await using MediaContext context = new();
        // Library? lib = await context.Libraries
        //     .Include(lib => lib.FolderLibraries)
        //     .ThenInclude(fl => fl.Folder)
        //     .FirstOrDefaultAsync(l => l.Type == "anime");
        //
        // string? path = lib?.FolderLibraries
        //     .Select(fl => fl.Folder.Path)
        //     .FirstOrDefault();
        //
        // if (string.IsNullOrEmpty(path))
        // {
        //     Logger.Encoder("No anime library found");
        //     return;
        // }
        //
        // PrefixFontFiles(path);

        // await Task.Delay(TimeSpan.FromSeconds(10));
        // MediaContext context = new();
        // Library lib = context.Libraries.First(l => l.Type == "movie");
        // User user = context.Users.First(u => u.Email == "stoney@nomercy.tv");
        //
        // PlaybackPreference prefs = new()
        // {
        //     UserId = user.Id,
        //     LibraryId = lib.Id,
        //     Video = new()
        //     {
        //         Width = 3840,
        //     },
        //     Audio = new()
        //     {
        //         Language = "eng",
        //     },
        //     Subtitle = new()
        //     {
        //         Language = "eng",
        //         Type = "full",
        //         Codec = "vtt"
        //     },
        // };
        //
        // await context.PlaybackPreferences.Upsert(prefs)
        // .On(v => new { v.UserId, v.LibraryId })
        // .WhenMatched((pp, pi) => new()
        // {
        //     UserId = pi.UserId,
        //     LibraryId = pi.LibraryId,
        //     _video = pi._video,
        //     _audio = pi._audio,
        //     _subtitle = pi._subtitle,
        //     UpdatedAt = DateTime.Now
        // })
        // .RunAsync();

        // Logger.Setup("Throwing test exception");
        // try
        // {
        //     SentrySdk.CaptureMessage("Hello Sentry");
        //     
        //     throw new("Test Exception");
        // }
        // catch (Exception ex)
        // {
        //     SentrySdk.CaptureException(ex);
        // }

        // MediaContext context = new();
        // List<Folder> folders = context.Folders
        //     .Include(l => l.FolderLibraries)
        //     .ThenInclude(fl => fl.Library)
        //     .Where(f => f.FolderLibraries
        //         .Any(fl => fl.Library.Type == "movie" || fl.Library.Type == "tv"))
        //     .ToList();
        //
        // foreach (Folder folder in folders)
        // {
        //     await Task.Run(() =>
        //     {
        //         ProcessM3U8Files(folder.Path);
        //     });
        // }

        // OpenSubtitlesClient client = new();
        // OpenSubtitlesClient subtitlesClient = await client.Login();
        // SubtitleSearchResponse? x = await subtitlesClient.SearchSubtitles("Black Panther Wakanda Forever (2022)", "dut");
        // Logger.OpenSubs(x);

        // TvdbArtworkClient artworkClient = new(63150614);
        // TvdbArtWorkResponse? artworkDetails = await artworkClient.Details();
        // Logger.Tvdb(artworkDetails);
        // TvdbArtWorkExtendedResponse? artWorkExtendedResponse = await artworkClient.Extended();
        // Logger.Tvdb(artWorkExtendedResponse);
        // TvdbArtWorkStatusesResponse? arTvdbStatusesResponse = await artworkClient.Statuses();
        // Logger.Tvdb(arTvdbStatusesResponse);
        // TvdbArtWorkTypesResponse? artworkTypesResponse = await artworkClient.Types();
        // Logger.Tvdb(artworkTypesResponse);
        //
        // TvdbAwardsClient awardsClient = new(1);
        // TvdbAwardsResponse? tvdbAwardsResponse = await awardsClient.Awards();
        // Logger.Tvdb(tvdbAwardsResponse);
        // TvdbAwardResponse? tvdbAwardResponse = await awardsClient.Details();
        // Logger.Tvdb(tvdbAwardResponse);
        // TvdbAwardExtendedResponse? tvdbAwardExtendedResponse = await awardsClient.Extended();
        // Logger.Tvdb(tvdbAwardExtendedResponse);
        //
        //
        // TvdbAwardCategoriesClient awardsCategoriesClient = new(2);
        // TvdbAwardCategoryResponse? tvdbAwardCategoryResponse = await awardsCategoriesClient.Categories();
        // Logger.Tvdb(tvdbAwardCategoryResponse);
        // TvdbAwardCategoryExtendedResponse? tvdbAwardCategoryExtendedResponse = await awardsCategoriesClient.CategoriesExtended();
        // Logger.Tvdb(tvdbAwardCategoryExtendedResponse);

        // TvdbPersonClient personClient = new(64425524);
        // TvdbCharacterResponse? character = await personClient.Character();
        // Logger.Tvdb(character);
        // TvdbPeopleTypeResponse? people = await personClient.People();
        // Logger.Tvdb(people);
        // TvdbPeopleTypeResponse? peopleTypes = await personClient.Types();
        // Logger.Tvdb(peopleTypes);


        //
        // TvdbCompanyClient companyClient = new(2151);
        // TvdbCompaniesResponse? tvdbCompaniesResponse = await companyClient.Companies();
        // Logger.Tvdb(tvdbCompaniesResponse?.Data.Take(20));
        // TvdbCompanyResponse? tvdbCompanyResponseDetails = await companyClient.Details();
        // Logger.Tvdb(tvdbCompanyResponseDetails);
        // TvdbCompanyTypesResponse? tvdbCompanyTypesResponse = await companyClient.Types();
        // Logger.Tvdb(tvdbCompanyTypesResponse);
        //
        // TvdbContentRatingClient ratingClient = new();
        // TvdbContentRatingsResponse? ratings = await ratingClient.ContentRatings();
        // Logger.Tvdb(ratings);
        //
        // TvdbCountriesClient countriesClient = new();
        // TvdbCountriesResponse? countries = await countriesClient.Countries();
        // Logger.Tvdb(countries);
        //
        // TvdbEntitiesClient entitiesClient = new();
        // TvdbEntitiesResponse? entities = await entitiesClient.Entities();
        // Logger.Tvdb(entities);
        //
        // TvdbGenderClient genderClient = new();
        // TvdbGendersResponse? tvdbGenders = await genderClient.Genders();
        // Logger.Tvdb(tvdbGenders);
        //
        // TvdbGenresClient client = new(1);
        // TvdbGenresResponse? genres = await client.Genres();
        // Logger.Tvdb(genres);
        // TvdbGenreResponse? genre = await client.Details();
        // Logger.Tvdb(genre);

        // TvdbInspirationClient inspirationClient = new();
        // TvdbInspirationTypesResponse? inspirationTypes = await inspirationClient.InspirationTypes();
        // Logger.Tvdb(inspirationTypes);

        // TvdbLanguagesClient languagesClient = new();
        // TvdbLanguagesResponse? languages = await languagesClient.Languages();
        // Logger.Tvdb(languages);
    }

    private static void ProcessM3U8Files(string rootPath)
    {
        try
        {
            Logger.Encoder($"Processing M3U8 files in {rootPath}");
            string[] m3U8TmpFiles = Directory.GetFiles(rootPath, "*.m3u8.tmp", SearchOption.AllDirectories)
                                    ?? throw new ArgumentNullException(nameof(m3U8TmpFiles), "No M3U8 files found");

            foreach (string tmpFile in m3U8TmpFiles)
            {
                string originalFile = tmpFile[..^4]; // Remove .tmp extension
                if (File.Exists(originalFile))
                {
                    Logger.Encoder($"replacing {originalFile} with {tmpFile}");
                    File.Delete(originalFile);
                    File.Move(tmpFile, originalFile);
                }
                else
                {
                    Logger.Encoder($"renaming {tmpFile} to {originalFile}");
                    File.Move(tmpFile, originalFile);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing M3U8 files: {ex.Message}");
        }
    }

    public static string GetDominantColor(string path)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(path);
        image.Mutate(x => x
            .Resize(new ResizeOptions
            {
                Sampler = KnownResamplers.NearestNeighbor,
                Size = new(100, 0)
            })
            .Quantize(new OctreeQuantizer
            {
                Options =
                {
                    MaxColors = 1,
                    Dither = new OrderedDither(1),
                    DitherScale = 1
                }
            }));

        Rgb24 dominant = image[0, 0];

        return dominant.ToHexString();
    }

    public class Font
    {
        [JsonProperty("mimeType")] public string MimeType { get; set; } = string.Empty;
        [JsonProperty("file")] public string File { get; set; } = string.Empty;
    }

    private static void PrefixFontFiles(string rootPath)
    {
        string[] subFolders = Directory.GetDirectories(rootPath);

        foreach (string folder in subFolders)
        {
            Logger.Encoder($"Cluster: {Path.GetFileName(folder)}");
            string[] fontsJsonFiles = Directory.GetFiles(folder, "fonts.json", SearchOption.AllDirectories);

            foreach (string fontsJson in fontsJsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(fontsJson);
                    Font[]? fonts = json.FromJson<Font[]>();

                    if (fonts == null) continue;

                    bool changed = false;

                    foreach (Font font in fonts)
                    {
                        if (string.IsNullOrEmpty(font.File) || font.File.StartsWith("fonts/")) continue;

                        font.File = "fonts/" + font.File;
                        changed = true;
                    }

                    if (!changed) continue;

                    File.WriteAllText(fontsJson, JsonConvert.SerializeObject(fonts, Formatting.Indented));
                    Logger.Encoder($"Updated: {fontsJson}");
                }
                catch (Exception ex)
                {
                    Logger.Encoder($"Error processing {fontsJson}: {ex.Message}");
                }
            }
        }
    }
}