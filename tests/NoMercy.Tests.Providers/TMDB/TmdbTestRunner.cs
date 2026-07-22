// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Reflection;

namespace NoMercy.Tests.Providers.TMDB;

/// <summary>
/// Test fixture for organizing TMDB test collections
/// Provides test discovery and categorization capabilities
/// </summary>
public class TmdbTestDiscoveryTests
{
    [Fact]
    public void DiscoverAllTmdbTests_WhenCalled_FindsAllTestClasses()
    {
        // Arrange
        Assembly assembly = Assembly.GetExecutingAssembly();
        List<Type> testClasses = assembly
            .GetTypes()
            .Where(predicate: t => t.Namespace?.StartsWith(value: "NoMercy.Tests.Providers.TMDB") == true)
            .Where(predicate: t => t.Name.EndsWith(value: "Tests"))
            .Where(predicate: t => !t.IsAbstract)
            .ToList();

        // Act & Assert
        testClasses.Should().NotBeEmpty(because: "TMDB test classes should be discoverable");
        testClasses
            .Should()
            .Contain(predicate: t => t.Name.Contains("Movie"), because: "Should include movie-related tests");
        testClasses.Should().Contain(predicate: t => t.Name.Contains("Client"), because: "Should include client tests");
        testClasses.Should().Contain(predicate: t => t.Name.Contains("Models"), because: "Should include model tests");
    }

    [Fact]
    public void AllTestClasses_WhenDiscovered_ShouldHaveProperNaming()
    {
        // Arrange
        Assembly assembly = Assembly.GetExecutingAssembly();
        List<Type> testClasses = assembly
            .GetTypes()
            .Where(predicate: t => t.Namespace?.StartsWith(value: "NoMercy.Tests.Providers.TMDB") == true)
            .Where(predicate: t =>
                t.GetMethods()
                    .Any(predicate: m =>
                        m.GetCustomAttribute<FactAttribute>() != null
                        || m.GetCustomAttribute<TheoryAttribute>() != null
                    )
            )
            .Where(predicate: t => t.Name != nameof(TmdbTestDiscoveryTests)) // Exclude this meta-test class
            .ToList();

        // Assert
        testClasses
            .Should()
            .AllSatisfy(expected: testClass =>
            {
                testClass
                    .Name.Should()
                    .EndWith(expected: "Tests", because: "All test classes should end with 'Tests'");
                testClass.IsPublic.Should().BeTrue(because: "All test classes should be public");
            });
    }

    [Fact]
    public void AllTestMethods_WhenDiscovered_ShouldHaveProperNaming()
    {
        // Arrange
        Assembly assembly = Assembly.GetExecutingAssembly();
        List<MethodInfo> testMethods = assembly
            .GetTypes()
            .Where(predicate: t => t.Namespace?.StartsWith(value: "NoMercy.Tests.Providers.TMDB") == true)
            .Where(predicate: t => t.Name != nameof(TmdbTestDiscoveryTests)) // Exclude this meta-test class
            .SelectMany(selector: t => t.GetMethods())
            .Where(predicate: m =>
                m.GetCustomAttribute<FactAttribute>() != null
                || m.GetCustomAttribute<TheoryAttribute>() != null
            )
            .ToList();

        // Assert
        testMethods.Should().NotBeEmpty(because: "Should find test methods");
        testMethods
            .Should()
            .AllSatisfy(expected: method =>
            {
                method
                    .Name.Should()
                    .NotStartWith(unexpected: "Test", because: "Test methods should not start with 'Test' prefix");
                method
                    .Name.Should()
                    .Match(
                        wildcardPattern: "*_*_*",
                        because: "Test methods should follow 'Method_Scenario_ExpectedResult' pattern"
                    );
            });
    }

    public static class TestCategories
    {
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Performance = "Performance";
        public const string ErrorHandling = "ErrorHandling";
    }

    public static class TestCollections
    {
        public const string MovieClient = "MovieClient";
        public const string BaseClient = "BaseClient";
        public const string Models = "Models";
        public const string Mocks = "Mocks";
    }
}
