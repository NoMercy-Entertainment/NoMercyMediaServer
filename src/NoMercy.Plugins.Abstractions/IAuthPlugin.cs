namespace NoMercy.Plugins.Abstractions;

public interface IAuthPlugin : IPlugin
{
    Task<AuthResult> AuthenticateAsync(string token, CancellationToken ct = default);
}
