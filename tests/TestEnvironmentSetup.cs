using System.Runtime.CompilerServices;
using NoMercy.NmSystem.Information;

namespace NoMercy.Tests;

/// <summary>
/// Runs once per test assembly before any test executes.
/// Routes all file paths to NoMercy_test and cleans the folder
/// so tests never touch the real dev/production environment.
/// </summary>
public static class TestEnvironmentSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Config.IsTest = true;

        // Ensure the test directory exists — don't delete it here because
        // dotnet test runs multiple assemblies in parallel, and deleting
        // while another assembly is using it causes cascading failures.
        // Use `dotnet test -- RunConfiguration.TestSessionCleanup=true` or
        // a CI script to clean NoMercy_test between full runs if needed.
        string testPath = AppFiles.AppPath;
        if (!Directory.Exists(testPath))
            Directory.CreateDirectory(testPath);
    }
}
