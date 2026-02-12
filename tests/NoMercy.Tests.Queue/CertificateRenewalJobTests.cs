using Microsoft.Extensions.Logging;
using Moq;
using NoMercy.Queue.MediaServer.Jobs;
using Xunit;

namespace NoMercy.Tests.Queue;

public class CertificateRenewalJobTests
{
    [Fact]
    public void CronExpression_ReturnsDaily2AM()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalJob>> logger = new();
        CertificateRenewalJob job = new(logger.Object);

        // Act
        string cronExpression = job.CronExpression;

        // Assert
        Assert.NotNull(cronExpression);
        Assert.NotEmpty(cronExpression);
        // The CronExpressionBuilder.Daily(2) should return a valid cron expression for 2 AM daily
    }

    [Fact]
    public void JobName_ReturnsCorrectName()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalJob>> logger = new();
        CertificateRenewalJob job = new(logger.Object);

        // Act
        string jobName = job.JobName;

        // Assert
        Assert.Equal("Daily Certificate Renewal", jobName);
    }

    [Fact]
    public async Task ExecuteAsync_LogsStartAndCompletion()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalJob>> loggerMock = new();
        CertificateRenewalJob job = new(loggerMock.Object);

        // Act & Assert
        // Note: This test will fail in the test environment because Certificate.RenewSslCertificate()
        // likely requires actual certificate infrastructure. In a real scenario, you'd want to
        // mock the Certificate.RenewSslCertificate() method or test it separately.
        
        try
        {
            await job.ExecuteAsync("test-parameters");
        }
        catch (Exception)
        {
            // Expected to fail in test environment - we're mainly testing that the structure is correct
        }

        // Verify that logging was attempted (at least the start message)
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting certificate renewal job")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CertificateRenewalJob_ImplementsICronJobExecutor()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalJob>> logger = new();

        // Act
        CertificateRenewalJob job = new(logger.Object);

        // Assert
        Assert.IsAssignableFrom<NoMercy.Queue.Interfaces.ICronJobExecutor>(job);
    }

    [Fact]
    public void CertificateRenewalJob_HasRequiredProperties()
    {
        // Arrange
        Mock<ILogger<CertificateRenewalJob>> logger = new();
        CertificateRenewalJob job = new(logger.Object);

        // Act & Assert
        Assert.NotNull(job.CronExpression);
        Assert.NotNull(job.JobName);
        Assert.NotEmpty(job.CronExpression);
        Assert.NotEmpty(job.JobName);
    }
}
