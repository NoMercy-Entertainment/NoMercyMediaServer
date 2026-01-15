// ---------------------------------------------------------------------------------------------------------------------
// Imports
// ---------------------------------------------------------------------------------------------------------------------

using NoMercy.Database;
using NoMercy.MediaProcessing.Movies;
using NoMercy.MediaProcessing.People;
using NoMercy.Networking.Dto;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

// ---------------------------------------------------------------------------------------------------------------------
// Code
// ---------------------------------------------------------------------------------------------------------------------
[Serializable]
public class AddMovieExtraDataJob : AbstractMediaExraDataJob<TmdbMovieAppends>
{
    public override string QueueName => "queue";
    public override int Priority => 1;

    public override async Task Handle()
    {
        await using MediaContext context = new();
        JobDispatcher jobDispatcher = new();

        MovieRepository movieRepository = new(context);
        MovieManager movieManager = new(movieRepository, jobDispatcher);

        PersonRepository personRepository = new(context);
        PersonManager personManager = new(personRepository, jobDispatcher);

        await personManager.Store(Storage);

        await movieManager.StoreImages(Storage);
        await movieManager.StoreSimilar(Storage);
        await movieManager.StoreRecommendations(Storage);
        await movieManager.StoreAlternativeTitles(Storage);
        await movieManager.StoreWatchProviders(Storage);
        await movieManager.StoreVideos(Storage);
        await movieManager.StoreCompanies(Storage);
        await movieManager.StoreKeywords(Storage);
        
        Networking.Networking.SendToAll("RefreshLibrary", "videoHub", new RefreshLibraryDto
        {
            QueryKey = ["base","info", Storage.Id.ToString()]
        });
    }
}