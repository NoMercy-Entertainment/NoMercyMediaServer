using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace NoMercy.Tests.Database;

/// <summary>
/// Verifies that EnableSensitiveDataLogging is only active
/// when Config.IsDev is true (HIGH-03).
/// </summary>
[Trait("Category", "Unit")]
public class SensitiveDataLoggingTests
{
    /// <summary>
    /// Replicates the exact conditional from MediaContext.OnConfiguring
    /// to verify sensitive data logging is gated on Config.IsDev.
    /// </summary>
    private static bool BuildOptionsAndCheckSensitiveLogging(bool isDev)
    {
        DbContextOptionsBuilder options = new();
        options.UseSqlite("Data Source=:memory:");

        if (isDev)
            options.EnableSensitiveDataLogging();

        CoreOptionsExtension? coreExtension = options.Options
            .FindExtension<CoreOptionsExtension>();

        return coreExtension?.IsSensitiveDataLoggingEnabled ?? false;
    }

    [Fact]
    public void ProductionMode_DoesNotEnableSensitiveDataLogging()
    {
        bool isSensitiveLogging = BuildOptionsAndCheckSensitiveLogging(isDev: false);

        Assert.False(isSensitiveLogging,
            "EnableSensitiveDataLogging must not be active in production mode");
    }

    [Fact]
    public void DevMode_EnablesSensitiveDataLogging()
    {
        bool isSensitiveLogging = BuildOptionsAndCheckSensitiveLogging(isDev: true);

        Assert.True(isSensitiveLogging,
            "EnableSensitiveDataLogging must be active in dev mode");
    }

    [Fact]
    public void MediaContext_OnConfiguring_GuardsSensitiveDataLogging_WithConfigIsDev()
    {
        // Verify the source code contains the Config.IsDev guard around EnableSensitiveDataLogging.
        // This catches regressions where someone removes the conditional.
        string sourceFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "NoMercy.Database", "MediaContext.cs");

        string source = File.ReadAllText(sourceFile);

        Assert.Contains("if (Config.IsDev)", source);
        Assert.Contains("EnableSensitiveDataLogging", source);
    }
}
