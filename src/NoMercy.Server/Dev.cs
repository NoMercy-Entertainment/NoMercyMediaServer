using NoMercy.MediaProcessing.Jobs.MediaJobs;

namespace NoMercy.Server;

public static class Dev
{
    public static async Task Run()
    {
        // await VideoAudioFile.GetSubtitleFromWhisperAi(
        //     "C:\\Users\\patri\\Videos\\2025-09-11_17-58-09.mkv", AppFiles.TranscodePath, "test.srt", "en");

        // Ffprobe ffprobe = new("G:\\Marvels\\Films\\Download\\Doctor.Strange.2016.2160p.BluRay.x265.10bit.SDR.DTS-HD.MA.TrueHD.7.1.Atmos-SWTYBLZ\\Doctor.Strange.2016.2160p.BluRay.x265.10bit.SDR.DTS-HD.MA.TrueHD.7.1.Atmos-SWTYBLZ.mkv");
        // Ffprobe ffprobe = new("M:\\Music\\C\\Chris Brown\\[2011] F.A.M.E_\\01 Deuces [Chris Brown feat. Tyga & Kevin McCall].mp3");
        // Ffprobe ffprobeData = await ffprobe.GetStreamData();
        
        // Logger.App(ffprobeData);
        
        // ProcessReleaseFolderJob processReleaseFolderJob = new()
        // {
        //     Id = Guid.Empty,
        //     FolderId = Ulid.Parse("01HQ5W84R600G31Q038AGNSKGT"),
        //     LibraryId = Ulid.Parse("01HQ5W4JJ9ZAX7721AQJZFQ7E1"),
        //     InputFolder = "M:\\Music\\C\\Chris Brown"
        // };
        
        // await processReleaseFolderJob.Handle();

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
        //         .Any(fl => fl.Library.Title == "Anime"))
        //     .ToList();
        
        // foreach (Folder folder in folders)
        // {
        //     await Task.Run(() =>
        //     {
        //         string[] subFolders = Directory.GetDirectories(folder.Path);
        //         Logger.Encoder($"Cluster: {Path.GetFileName(folder.Path)} - {subFolders.Length} subfolders found");
        //         foreach (string subFolder in subFolders)
        //         {
        //             //     Logger.Encoder($"Processing: {subFolder}");
        //             // ProcessM3U8Files(folder.Path);
        //             // ConvertAacToTs(subFolder);
        //             FixM3U8AacReferences(subFolder);
        //         }
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
        
        await Task.CompletedTask;
    }

    // private static void ProcessM3U8Files(string rootPath)
    // {
    //     try
    //     {
    //         Logger.Encoder($"Processing M3U8 files in {rootPath}");
    //         string[] m3U8TmpFiles = Directory.GetFiles(rootPath, "*.m3u8.tmp", SearchOption.AllDirectories)
    //                                 ?? throw new ArgumentNullException(nameof(m3U8TmpFiles), "No M3U8 files found");

    //         foreach (string tmpFile in m3U8TmpFiles)
    //         {
    //             string originalFile = tmpFile[..^4]; // Remove .tmp extension
    //             if (File.Exists(originalFile))
    //             {
    //                 Logger.Encoder($"replacing {originalFile} with {tmpFile}");
    //                 File.Delete(originalFile);
    //                 File.Move(tmpFile, originalFile);
    //             }
    //             else
    //             {
    //                 Logger.Encoder($"renaming {tmpFile} to {originalFile}");
    //                 File.Move(tmpFile, originalFile);
    //             }
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error processing M3U8 files: {ex.Message}");
    //     }
    // }

    // private static void ConvertAacToTs(string rootPath)
    // {
    //     try
    //     {
    //         Logger.Encoder($"Converting AAC files to TS extensions in {rootPath}");

    //         // Group AAC files by their directory
    //         string[] aacFiles = Directory.GetFiles(rootPath, "*.aac", SearchOption.AllDirectories);
    //         IEnumerable<IGrouping<string, string>> aacFilesByDirectory =
    //             aacFiles.GroupBy(file => Path.GetDirectoryName(file)!);

    //         foreach (IGrouping<string, string> directoryGroup in aacFilesByDirectory)
    //         {
    //             string directory = directoryGroup.Key;
    //             List<(string oldName, string newName)> renamedFiles = [];

    //             // First, rename all AAC files in this directory
    //             foreach (string aacFile in directoryGroup)
    //             {
    //                 string fileNameWithoutExt = Path.GetFileNameWithoutExtension(aacFile);
    //                 string newTsFile = Path.Combine(directory, fileNameWithoutExt + ".ts");

    //                 if (!File.Exists(newTsFile))
    //                 {
    //                     File.Move(aacFile, newTsFile);
    //                     Logger.Encoder($"Renamed: {aacFile} -> {newTsFile}");

    //                     renamedFiles.Add((Path.GetFileName(aacFile), Path.GetFileName(newTsFile)));
    //                 }
    //                 else
    //                 {
    //                     Logger.Encoder($"Target file already exists: {newTsFile}");
    //                 }
    //             }

    //             // Then update the M3U8 file once for all changes in this directory
    //             if (renamedFiles.Count > 0) UpdateM3U8FilesForDirectory(directory, renamedFiles);
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.Encoder($"Error converting AAC to TS: {ex.Message}");
    //     }
    // }
    
    // private static void FixM3U8AacReferences(string rootPath)
    // {
    //     try
    //     {
    //         Logger.Encoder($"Fixing AAC references in M3U8 files in {rootPath}");

    //         // Find all M3U8 files recursively
    //         string[] m3U8Files = Directory.GetFiles(rootPath, "*.m3u8", SearchOption.AllDirectories);

    //         foreach (string m3U8File in m3U8Files)
    //         {
    //             string content = File.ReadAllText(m3U8File);
    //             string originalContent = content;

    //             // Replace all .aac extensions with .ts extensions
    //             content = content.Replace(".aac", ".ts");

    //             // Only write if content actually changed
    //             if (content != originalContent)
    //             {
    //                 File.WriteAllText(m3U8File, content);
                
    //                 // Count how many replacements were made
    //                 int replacements = (originalContent.Length - content.Length) / (".aac".Length - ".ts".Length);
    //                 Logger.Encoder($"Updated M3U8: {m3U8File} - fixed {replacements} AAC references");
    //             }
    //         }

    //         Logger.Encoder("Finished fixing AAC references in M3U8 files");
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.Encoder($"Error fixing M3U8 AAC references: {ex.Message}");
    //     }
    // }

    // private static void UpdateM3U8FilesForDirectory(string directory,
    //     List<(string oldName, string newName)> renamedFiles)
    // {
    //     try
    //     {
    //         // Look for M3U8 files in the current directory
    //         string[] m3U8Files = Directory.GetFiles(directory, "*.m3u8", SearchOption.TopDirectoryOnly);

    //         foreach (string m3U8File in m3U8Files)
    //         {
    //             string content = File.ReadAllText(m3U8File);
    //             string originalContent = content;

    //             // Apply all filename replacements
    //             foreach ((string oldName, string newName) in renamedFiles)
    //             {
    //                 content = content.Replace(oldName, newName);

    //                 // Also handle relative path references like "audio_eng/filename.aac"
    //                 string relativePath = Path.GetFileName(directory) + "/" + oldName;
    //                 string newRelativePath = Path.GetFileName(directory) + "/" + newName;
    //                 content = content.Replace(relativePath, newRelativePath);
    //             }

    //             // Only write if content actually changed
    //             if (content == originalContent) continue;

    //             File.WriteAllText(m3U8File, content);
    //             Logger.Encoder($"Updated M3U8: {m3U8File} - replaced {renamedFiles.Count} file references");
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         Logger.Encoder($"Error updating M3U8 files in directory {directory}: {ex.Message}");
    //     }
    // }

    // public static string GetDominantColor(string path)
    // {
    //     using Image<Rgb24> image = Image.Load<Rgb24>(path);
    //     image.Mutate(x => x
    //         .Resize(new ResizeOptions
    //         {
    //             Sampler = KnownResamplers.NearestNeighbor,
    //             Size = new(100, 0)
    //         })
    //         .Quantize(new OctreeQuantizer
    //         {
    //             Options =
    //             {
    //                 MaxColors = 1,
    //                 Dither = new OrderedDither(1),
    //                 DitherScale = 1
    //             }
    //         }));

    //     Rgb24 dominant = image[0, 0];

    //     return dominant.ToHexString();
    // }

    // private static void PrefixFontFiles(string rootPath)
    // {
    //     string[] subFolders = Directory.GetDirectories(rootPath);

    //     foreach (string folder in subFolders)
    //     {
    //         Logger.Encoder($"Cluster: {Path.GetFileName(folder)}");
    //         string[] fontsJsonFiles = Directory.GetFiles(folder, "fonts.json", SearchOption.AllDirectories);

    //         foreach (string fontsJson in fontsJsonFiles)
    //             try
    //             {
    //                 string json = File.ReadAllText(fontsJson);
    //                 Font[]? fonts = json.FromJson<Font[]>();

    //                 if (fonts == null) continue;

    //                 bool changed = false;

    //                 foreach (Font font in fonts)
    //                 {
    //                     if (string.IsNullOrEmpty(font.File) || font.File.StartsWith("fonts/")) continue;

    //                     font.File = "fonts/" + font.File;
    //                     changed = true;
    //                 }

    //                 if (!changed) continue;

    //                 File.WriteAllText(fontsJson, JsonConvert.SerializeObject(fonts, Formatting.Indented));
    //                 Logger.Encoder($"Updated: {fontsJson}");
    //             }
    //             catch (Exception ex)
    //             {
    //                 Logger.Encoder($"Error processing {fontsJson}: {ex.Message}");
    //             }
    //     }
    // }

    // public class Font
    // {
    //     [JsonProperty("mimeType")] public string MimeType { get; set; } = string.Empty;
    //     [JsonProperty("file")] public string File { get; set; } = string.Empty;
    // }
}