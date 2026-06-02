using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercyQueue.Extensions;

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    private static void ConfigureCronJobs(IServiceCollection services)
    {
        services.RegisterCronJob<CertificateRenewalCronJob>("certificate-renewal");
        services.RegisterCronJob<ActivityLogRetentionCronJob>("activity-log-retention");

        services.RegisterCronJob<ShowPaletteCronJob>("show-palette-job");
        services.RegisterCronJob<SeasonPaletteCronJob>("season-palette-job");
        services.RegisterCronJob<EpisodePaletteCronJob>("episode-palette-job");
        services.RegisterCronJob<MoviePaletteCronJob>("movie-palette-job");
        services.RegisterCronJob<CollectionPaletteCronJob>("collection-palette-job");
        services.RegisterCronJob<PersonPaletteCronJob>("person-palette-job");

        services.RegisterCronJob<ImagePaletteCronJob>("image-palette-job");
        services.RegisterCronJob<RecommendationPaletteCronJob>("recommendation-palette-job");
        services.RegisterCronJob<SimilarPaletteCronJob>("similar-palette-job");

        services.RegisterCronJob<ArtistFanartCronJob>("artist-fanart-job");
        services.RegisterCronJob<ArtistPaletteCronJob>("artist-palette-job");
        services.RegisterCronJob<AlbumPaletteCronJob>("album-palette-job");

        // TODO: Remove after all palettes are regenerated with the new Median Cut algorithm
        services.RegisterCronJob<ReprocessAllPalettesCronJob>("reprocess-all-palettes-job");

        services.RegisterCronJob<DeviceDropRuleCronJob>("device-drop-rule-job");
    }
}
