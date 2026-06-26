namespace NoMercy.NmSystem.Auth;

public interface IAuthTokenStore
{
    string? AccessToken { get; }
    void SetAccessToken(string? token);
    event EventHandler<string?> AccessTokenChanged;
}
