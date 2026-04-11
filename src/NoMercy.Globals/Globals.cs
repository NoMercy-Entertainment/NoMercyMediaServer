namespace NoMercy.Globals;

public static class Globals
{
    private static volatile string? _accessToken;

    public static string? AccessToken
    {
        get => _accessToken;
        set => _accessToken = value;
    }
}
