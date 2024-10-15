using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.People;
public interface IPersonRepository
{
    public Task Store(IEnumerable<Person> people);
    public Task StoreTranslationsAsync(IEnumerable<Translation> translations);
    public Task StoreImagesAsync(IEnumerable<Image> images);

    public Task StoreCast(IEnumerable<Cast> cast, Type type);
    public Task StoreCrew(IEnumerable<Crew> crew, Type type);
    public Task StoreCreatorAsync(Creator creator);
    public Task StoreGuestStarsAsync(IEnumerable<GuestStar> guestStars);

    public Task StoreRoles(IEnumerable<Role> roles);
    public Task StoreJobs(IEnumerable<Job> job);

    public Task StoreAggregateCreditsAsync();
    public Task StoreAggregateCastAsync();
    public Task StoreAggregateCrewAsync();
    List<int> GetIds();
}