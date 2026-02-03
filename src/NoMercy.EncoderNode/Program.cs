using Asp.Versioning.ApiExplorer;
using AspNetCore.Swagger.Themes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NoMercy.EncoderNode;
using NoMercy.EncoderNode.Configuration;
using NoMercy.EncoderNode.Controllers;
using NoMercy.EncoderNode.Services;
using NoMercy.EncoderNode.Swagger;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

// Parse startup options from command-line arguments
EncoderNodeStartupOptions startupOptions = EncoderNodeStartupOptions.Parse(args);
startupOptions.ApplySettings();

// Set ASP.NET Core environment to match development mode
if (startupOptions.Development)
{
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
}

// Initialize AppFiles for proper storage (now uses _dev suffix if in development mode)
await EncoderNodeAppFiles.InitializeAsync();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        // Use environment variables for configuration
        // Prefix: ENCODER_NODE__
        // Example: ENCODER_NODE__ENCODERNODE__PRIMARYSERVER__URL=https://nomercy:7626
        config.AddEnvironmentVariables();
    })
    .UseSerilog((context, loggerConfig) =>
    {
        loggerConfig
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(EncoderNodeAppFiles.LogPath, "encoder-node-.txt"), rollingInterval: RollingInterval.Day)
            .MinimumLevel.Information();
    })
    .ConfigureServices((context, services) =>
    {
        IConfiguration configuration = context.Configuration;

        // Bind configuration
        services.Configure<EncoderNodeOptions>(configuration.GetSection(EncoderNodeOptions.SectionName));
        
        // Apply configuration settings to static EncoderNodeConfig
        startupOptions.ApplyConfigurationSettings(configuration);

        // Add port conflict validator
        services.AddSingleton<IPortConflictValidator, PortConflictValidator>();

        // ==================== STANDALONE ENCODER NODE ====================
        // The following services are for STANDALONE operation (no server required)
        
        // Add core encoding services (do NOT require server connectivity)
        services.AddSingleton(provider => new CapabilityDetector(
            provider.GetRequiredService<ILogger<CapabilityDetector>>(),
            EncoderNodeAppFiles.FfmpegPath));
        services.AddSingleton<FfmpegCommandBuilder>();
        services.AddSingleton<FfmpegJobExecutor>();
        services.AddSingleton<JobContextManager>();
        services.AddSingleton<BackgroundJobExecutor>();
        services.AddHostedService(provider => provider.GetRequiredService<BackgroundJobExecutor>());
        services.AddSingleton<BinaryPreflightService>();
        services.AddHostedService(provider => provider.GetRequiredService<BinaryPreflightService>());

        // Standalone encoder node service - works without any server
        services.AddSingleton<IStandaloneEncoderNodeService, StandaloneEncoderNodeService>();

        // ==================== OPTIONAL SERVER INTEGRATION ====================
        // The following services are OPTIONAL for server integration
        // They can be disabled or will fail gracefully if server is not available
        
        // HTTP client for API communication
        services.AddHttpClient();

        // Keycloak authentication service
        services.AddSingleton<IKeycloakAuthService, KeycloakAuthService>();

        // Server discovery service
        services.AddSingleton<IServerDiscoveryService, ServerDiscoveryService>();

        // Node registration service (heartbeat + registration)
        services.AddHostedService<NodeRegistrationService>();

        // Optional job reporting (only if server configured)
        services.AddSingleton<IOptionalJobReportingService, OptionalJobReportingService>();

        // Progress emitter for sending encoding progress to the primary server
        services.AddSingleton<IProgressEmitter, ProgressEmitter>();

        // ==================== OPTIONAL AUTHENTICATION ====================
        // Keycloak/JWT authentication is OPTIONAL
        // EncoderNode works fine without it
        
        KeycloakOptions keycloakOptions = configuration.GetSection("EncoderNode:Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();
        
        if (keycloakOptions.Enabled && !string.IsNullOrEmpty(keycloakOptions.ClientId))
        {
            // Authentication is enabled - configure JWT validation
            // Extract auth server URL from full realm URL
            string authBaseUrl = EncoderNodeConfig.AuthBaseUrl.TrimEnd('/');
            string authServerUrl = authBaseUrl.Substring(0, authBaseUrl.LastIndexOf("/realms/"));
            
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authServerUrl;
                    options.Audience = keycloakOptions.ClientId;
                    options.RequireHttpsMetadata = !EncoderNodeConfig.IsDev;
                    options.MetadataAddress = $"{authServerUrl}/realms/{keycloakOptions.Realm}/.well-known/openid-configuration";
                    
                    options.TokenValidationParameters = new()
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authBaseUrl,
                        ValidateAudience = true,
                        ValidAudience = keycloakOptions.ClientId,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                    
                    options.Events = new()
                    {
                        OnTokenValidated = context =>
                        {
                            ILogger<Program> logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogDebug("Token validated for subject: {Subject}", context.Principal?.FindFirst("sub")?.Value);
                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = context =>
                        {
                            ILogger<Program> logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                            logger.LogWarning(context.Exception, "Authentication failed");
                            return Task.CompletedTask;
                        }
                    };
                });
        }
        else
        {
            // No authentication - all endpoints are public
            services.AddAuthentication();
        }

        // Add authorization based on authentication configuration
        if (keycloakOptions.Enabled && !string.IsNullOrEmpty(keycloakOptions.ClientId))
        {
            services.AddAuthorizationBuilder()
                .AddPolicy("api", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                });
        }
        else
        {
            // No authentication required - add minimal authorization without policies
            services.AddAuthorizationBuilder();
        }


        // ==================== WEB API CONFIGURATION ====================
        
        // Add web API
        services.AddControllers()
            .AddApplicationPart(typeof(EncoderNodeController).Assembly);
        services.AddEndpointsApiExplorer();
        
        // Configure API versioning
        services.AddApiVersioning(config =>
            {
                config.ReportApiVersions = true;
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.DefaultApiVersion = new(1, 0);
                config.UnsupportedApiVersionStatusCode = 418;
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
                options.DefaultApiVersion = new(1, 0);
            });

        // Configure Swagger/OpenAPI
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<SwaggerDefaultValues>();
            options.IncludeXmlComments(GetXmlCommentPath());
            options.DocumentFilter<IncludeAllPathsFilter>();
        });
        services.AddSwaggerGenNewtonsoftSupport();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        // Add CORS (permissive for standalone operation)
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        string port = startupOptions.Port.ToString();
        bool useHttps = startupOptions.UseHttps;
        
        string scheme = useHttps ? "https" : "http";
        webBuilder.UseUrls($"{scheme}://0.0.0.0:{port}");
        
        // Configure Kestrel
        webBuilder.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = null; // Allow large file uploads
            
            if (useHttps)
            {
                options.ListenAnyIP(int.Parse(port), listenOptions =>
                {
                    listenOptions.UseHttps();
                });
            }
        });
        
        webBuilder
            .Configure((context, app) =>
            {
                IApiVersionDescriptionProvider provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();

                app.UseSerilogRequestLogging();

                app.UseHttpsRedirection();

                app.UseCors("AllowAll");

                app.UseRouting();
                
                app.UseAuthentication();
                app.UseAuthorization();

                ILogger<Program> logger = app.ApplicationServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("NoMercy EncoderNode starting on {Scheme}://0.0.0.0:{Port}", scheme, port);

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

                // Configure Swagger UI
                app.UseSwagger();
                app.UseSwaggerUI(ModernStyle.Dark, options =>
                {
                    options.RoutePrefix = string.Empty;
                    options.DocumentTitle = "NoMercy MediaServer API";
                    options.OAuthClientId(EncoderNodeConfig.TokenClientId);
                    options.OAuthScopes("openid");
                    options.EnablePersistAuthorization();
                    options.EnableTryItOutByDefault();

                    IReadOnlyList<ApiVersionDescription> descriptions = provider.ApiVersionDescriptions;
                    foreach (ApiVersionDescription description in descriptions)
                    {
                        string groupName = $"v{description.ApiVersion.MajorVersion}";
                        string url = $"/swagger/{groupName}/swagger.json";
                        string name = groupName.ToUpperInvariant();
                        options.SwaggerEndpoint(url, name);
                    }
                });
            });
    })
    .Build();

await host.RunAsync();

/// <summary>
/// Gets the path to the XML documentation file for Swagger
/// </summary>
static string GetXmlCommentPath()
{
    string xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    return File.Exists(xmlPath) ? xmlPath : string.Empty;
}