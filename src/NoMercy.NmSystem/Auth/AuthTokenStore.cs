namespace NoMercy.NmSystem.Auth;

public class AuthTokenStore : IAuthTokenStore
{
    private volatile string? _accessToken;

    public string? AccessToken => _accessToken;

    public event EventHandler<string?>? AccessTokenChanged;

    public void SetAccessToken(string? token)
    {
        _accessToken = token;
        AccessTokenChanged?.Invoke(this, token);
    }
}
