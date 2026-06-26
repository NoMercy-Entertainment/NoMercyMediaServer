namespace NoMercy.Setup.Auth;

public interface IServerRegistrationService
{
    Task Init(int maxRetries = 5);
    Task GetTunnelAvailability();
}
