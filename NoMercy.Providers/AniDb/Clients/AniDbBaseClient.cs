using System.Security;
using AniDB;
using NoMercy.Helpers;

namespace NoMercy.Providers.AniDb.Clients;

public class AniDbBaseClient
{
    private static string Username { get; set; } = "";
    private static string Password { get; set; } = "";
    private static SecureString? ApiKey { get; set; }

    private static readonly Barrier DisconnectBarrier = new(2);

    private static readonly AniDBClientOptions AniDbClientOptions = new()
    {
        ClientName = "nomercy",
        ClientVersion = 1,
        LocalPort = (ushort)(SystemInfo.ServerPort + 1),
    };

    private static readonly AniDBClient AniDbClient = new(AniDbClientOptions);

    static AniDbBaseClient()
    {
        UserPass? userPass = CredentialManager.GetCredential("AniDb");
        if (userPass == null) return;

        Username = userPass.Username;
        Password = userPass.Password;

        if (userPass.ApiKey == null) return;

        ApiKey = CredentialManager.ConvertToSecureString(userPass.ApiKey);
    }

    public static void Dispose()
    {
        if (AniDbClient.IsConnected)
        {
            try
            {
                AniDbClient.Logout(LogoutCallback);
                DisconnectBarrier.SignalAndWait();
            }
            catch (Exception _)
            {
                AniDbClient.Disconnect();
            }
        }
    }

    public static AniDBClient GetClient()
    {
        return AniDbClient;
    }

    public static void SetCredentials(string username, string password, string? apiKey)
    {
        Username = username;
        Password = password;

        if (apiKey == null) return;
        ApiKey = CredentialManager.ConvertToSecureString(apiKey);
    }

    public static Task Init()
    {
        return new Task(() =>
        {
            try
            {
                AniDbClient.Connect();
                AniDbClient.Login(callback: LoginCallback, username: Username, password: Password, api_key: ApiKey);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        });
    }

    private static void LoginCallback(AniDBMessageResponse message)
    {
        Logger.AniDb(message, LogLevel.Debug);
    }

    private static void LogoutCallback(AniDBMessageResponse message)
    {
        AniDbClient.Disconnect();
        DisconnectBarrier.SignalAndWait();
    }
}