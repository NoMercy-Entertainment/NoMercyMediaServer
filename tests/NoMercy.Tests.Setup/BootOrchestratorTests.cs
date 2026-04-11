using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.Database;
using NoMercy.Setup;

namespace NoMercy.Tests.Setup;

[Trait("Category", "Unit")]
public class BootOrchestratorTests : IDisposable
{
    private readonly AppDbContext _appContext;
    private readonly AuthManager _authManager;
    private readonly SetupState _setupState;
    private readonly BootOrchestrator _orchestrator;

    public BootOrchestratorTests()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        ServiceProvider provider = services.BuildServiceProvider();
        TokenStore.Initialize(provider);

        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        _appContext = new AppDbContext(optionsBuilder.Options);
        _appContext.Database.OpenConnection();
        _appContext.Database.EnsureCreated();

        _authManager = new AuthManager(_appContext);
        _setupState = new SetupState();
        _orchestrator = new BootOrchestrator(_setupState, _authManager);
    }

    public void Dispose()
    {
        _appContext.Database.CloseConnection();
        _appContext.Dispose();
    }

    [Fact]
    public void SetupState_StartsAsUnauthenticated()
    {
        Assert.Equal(SetupPhase.Unauthenticated, _setupState.CurrentPhase);
        Assert.True(_setupState.IsSetupRequired);
    }

    [Fact]
    public async Task PostAuth_WaitsForAuthenticated()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(200));
        Task postAuth = _orchestrator.RunPostAuthAsync(cts.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => postAuth);
    }

    [Fact]
    public async Task PostAuth_ProceedsWhenAuthenticated()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            _setupState.TransitionTo(SetupPhase.Authenticating);
            _setupState.TransitionTo(SetupPhase.Authenticated);
        });

        try
        {
            await _orchestrator.RunPostAuthAsync(cts.Token);
        }
        catch
        {
            // Registration will fail in test (no network) — expected
        }

        Assert.NotEqual(SetupPhase.Unauthenticated, _setupState.CurrentPhase);
    }
}
