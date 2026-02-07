using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem.Information;
using NoMercy.Server;

namespace NoMercy.Tests.Api.Infrastructure;

public class NoMercyApiFactory : WebApplicationFactory<Startup>
{
    public NoMercyApiFactory()
    {
        EnsureDirectoriesAndSeedDatabase();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            RemoveHostedServices(services);
            ReplaceAuth(services);
        });
    }

    protected override IWebHostBuilder? CreateWebHostBuilder()
    {
        return Microsoft.AspNetCore.WebHost.CreateDefaultBuilder([])
            .ConfigureLogging(logging => logging.ClearProviders())
            .UseStartup<Startup>()
            .ConfigureServices(services =>
            {
                services.AddSingleton(new StartupOptions());
                services.AddSingleton<ISunsetPolicyManager>(new NoOpSunsetPolicyManager());
                services.AddSingleton<IApiVersionDescriptionProvider, DefaultApiVersionDescriptionProvider>();

                services.AddSingleton(
                    typeof(Microsoft.Extensions.Logging.ILogger<>),
                    typeof(CustomLogger<>));
            });
    }

    private static void EnsureDirectoriesAndSeedDatabase()
    {
        foreach (string path in AppFiles.AllPaths())
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        using MediaContext mediaContext = new();
        mediaContext.Database.EnsureCreated();

        if (!mediaContext.Users.Any())
        {
            User testUser = new()
            {
                Id = TestAuthHandler.DefaultUserId,
                Email = TestAuthHandler.DefaultUserEmail,
                Name = TestAuthHandler.DefaultUserName,
                Owner = true,
                Allowed = true,
                Manage = true
            };
            mediaContext.Users.Add(testUser);
            mediaContext.SaveChanges();
        }

        ClaimsPrincipleExtensions.Users.Clear();
        ClaimsPrincipleExtensions.Users.AddRange(mediaContext.Users.ToList());

        using QueueContext queueContext = new();
        queueContext.Database.EnsureCreated();
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        List<ServiceDescriptor> hostedServices = services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .ToList();

        foreach (ServiceDescriptor descriptor in hostedServices)
            services.Remove(descriptor);
    }

    private static void ReplaceAuth(IServiceCollection services)
    {
        services.RemoveAll<IAuthenticationSchemeProvider>();
        services.RemoveAll<IAuthenticationHandlerProvider>();

        services.AddAuthentication(TestAuthDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthDefaults.AuthenticationScheme, _ => { });

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder(TestAuthDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy("api", new AuthorizationPolicyBuilder(TestAuthDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build());
    }

    private sealed class NoOpSunsetPolicyManager : ISunsetPolicyManager
    {
        public bool TryGetPolicy(string? name, ApiVersion apiVersion, out SunsetPolicy sunsetPolicy)
        {
            sunsetPolicy = default;
            return false;
        }
    }
}
