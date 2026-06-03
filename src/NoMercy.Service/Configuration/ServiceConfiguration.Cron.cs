using NoMercy.MediaProcessing.Jobs.ChangesJobs;
using NoMercy.Queue.MediaServer.Jobs;
using NoMercyQueue.Extensions;

namespace NoMercy.Service.Configuration;

public static partial class ServiceConfiguration
{
    private static void ConfigureCronJobs(IServiceCollection services)
    {
        services.RegisterCronJob<CertificateRenewalCronJob>("certificate-renewal");
        services.RegisterCronJob<ActivityLogRetentionCronJob>("activity-log-retention");
        services.RegisterCronJob<TmdbChangesCronJob>("tmdb-changes-sync");
        services.RegisterCronJob<DeviceDropRuleCronJob>("device-drop-rule-job");
    }
}
