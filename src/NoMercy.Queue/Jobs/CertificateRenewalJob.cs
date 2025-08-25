using Microsoft.Extensions.Logging;
using NoMercy.Networking;
using NoMercy.Queue.Interfaces;

namespace NoMercy.Queue.Jobs;

public class CertificateRenewalJob : ICronJobExecutor
{
    private readonly ILogger<CertificateRenewalJob> _logger;

    public string CronExpression => new CronExpressionBuilder().Daily(2);
    public string JobName => "Daily Certificate Renewal";

    public CertificateRenewalJob(ILogger<CertificateRenewalJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(string parameters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting certificate renewal job");
            
        await Certificate.RenewSslCertificate();
            
        _logger.LogInformation("Certificate renewal job completed");
    }
}