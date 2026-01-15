namespace NoMercy.Helpers;

public class UserPass
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string? ApiKey { get; set; }

    public UserPass(string username, string password, string apiKey)
    {
        Username = username;
        Password = password;
        ApiKey = apiKey;
    }
}