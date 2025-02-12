using NoMercy.Encoder.Core;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
// using NoMercy.Providers.TVDB.Client;
// using NoMercy.Providers.TVDB.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace NoMercy.Server;

public class Dev
{
    public static async Task Run()
    {
        FFmpegHardwareConfig ffmpegConfig = new();

        foreach (GpuAccelerator accelerator in ffmpegConfig.Accelerators)
        {
            Logger.Encoder(accelerator);
        }
        
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
    
    public static string GetDominantColor(string path)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(path);
        image.Mutate(x => x
            .Resize(new ResizeOptions()
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
}
