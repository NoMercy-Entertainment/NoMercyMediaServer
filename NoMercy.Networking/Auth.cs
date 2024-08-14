using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;

namespace NoMercy.Networking;

public static class Auth
{
    public static Action<string, string> Logger;
    private static string BaseUrl => Config.AuthBaseUrl;
    private static readonly string TokenUrl = $"{BaseUrl}protocol/openid-connect/token";

    private static string? PublicKey { get; set; }
    private static string? TokenClientId => Config.TokenClientId;
    private static string? TokenClientSecret => Config.TokenClientSecret;

    private static string? RefreshToken { get; set; }
    public static string? AccessToken { get; private set; }
    private static int? ExpiresIn { get; set; }

    private static int? NotBefore { get; set; }

    private static JwtSecurityToken? _jwtSecurityToken;

    private static IWebHost? TempServer { get; set; }

    public static Task Init()
    {
        if (!File.Exists(Config.TokenFile)) File.WriteAllText(Config.TokenFile, "{}");

        AuthKeys();
        
        AccessToken = GetAccessToken();
        RefreshToken = GetRefreshToken();
        ExpiresIn = TokenExpiration();
        NotBefore = TokenNotBefore();

        if (AccessToken == null || RefreshToken == null || ExpiresIn == null)
        {
            TokenByBrowserOrPassword();
            return Task.CompletedTask;
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        _jwtSecurityToken = tokenHandler.ReadJwtToken(AccessToken);

        var expiresInDays = _jwtSecurityToken.ValidTo.AddDays(-5).Subtract(DateTime.UtcNow).Days;

        var expired = NotBefore == null && expiresInDays >= 0;

        if (!expired)
            TokenByRefreshGrand();
        else
            TokenByBrowserOrPassword();

        if (AccessToken == null || RefreshToken == null || ExpiresIn == null)
            throw new Exception("Failed to get tokens");

        return Task.CompletedTask;
    }

    private static void TokenByBrowserOrPassword()
    {
        Logger?.Invoke("Auth","Trying to authenticate by browser or password");
        while (true)
        {
            if (IsDesktopEnvironment())
            {
                TokenByBrowser();
            }
            else
            {
                Console.WriteLine("Enter your email:");
                var email = Console.ReadLine();

                if (string.IsNullOrEmpty(email))
                {
                    Console.WriteLine("Email cannot be empty");
                    continue;
                }

                Console.WriteLine("Enter your password:");
                var password = ReadPassword();
                
                if (string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("Password cannot be empty");
                    continue;
                }
                
                Console.WriteLine("Enter your 2 factor authentication code (if enabled):");
                var otp =  Console.ReadLine();
                
                TokenByPasswordGrant(email, password, otp);
            }

            break;
        }
    }
    
    private static string ReadPassword()
    {
        var password = new StringBuilder();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (key.Key != ConsoleKey.Enter)
            {
                password.Append(key.KeyChar);
                Console.Write('*');
            }
        } while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password.ToString();
    }

    private static void TokenByBrowser()
    {
        var baseUrl = new Uri($"{BaseUrl}protocol/openid-connect/auth");
        var redirectUri = HttpUtility.UrlEncode($"http://localhost:{Config.InternalServerPort}/sso-callback");

        var query = new Dictionary<string, string>
        {
            ["redirect_uri"] = redirectUri,
            ["client_id"] = "nomercy-server",
            ["response_type"] = "code",
            ["scope"] = "openid offline_access email profile"
        }.Select(x => $"{x.Key}={x.Value}");

        var url = new Uri($"{baseUrl}?{string.Join("&", query)}").ToString();

        TempServer = Networking.TempServer();
        TempServer.StartAsync().Wait();

        OpenBrowser(url);

        CheckToken();
    }

    private static void CheckToken()
    {
        Task.Run(async () =>
        {
            await Task.Delay(1000);

            if (AccessToken == null || RefreshToken == null || ExpiresIn == null)
                CheckToken();
            else
                TempServer?.StopAsync().Wait();
        }).Wait();
    }

    private static void SetTokens(string response)
    {
        dynamic data = JsonConvert.DeserializeObject(response)
                       ?? throw new Exception("Failed to deserialize JSON");

        if (data.access_token == null || data.refresh_token == null || data.expires_in == null)
        {
            File.Delete(Config.TokenFile);
            TokenByBrowserOrPassword();

            return;
        }

        var tmp = File.OpenWrite(Config.TokenFile);
        tmp.SetLength(0);
        tmp.Write(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data, Formatting.Indented)));
        tmp.Close();

        Logger?.Invoke("Auth",@"Tokens refreshed");

        AccessToken = data.access_token;
        RefreshToken = data.refresh_token;
        ExpiresIn = data.expires_in;
        NotBefore = data["not-before-policy"];
    }

    private static dynamic TokenData()
    {
        return JsonConvert.DeserializeObject(File.ReadAllText(Config.TokenFile))
               ?? throw new Exception("Failed to deserialize JSON");
    }

    private static string? GetAccessToken()
    {
        var data = TokenData();

        return data.access_token;
    }

    private static string? GetRefreshToken()
    {
        var data = TokenData();
        return data.refresh_token;
    }

    private static int? TokenExpiration()
    {
        var data = TokenData();
        return data.expires_in;
    }

    private static int? TokenNotBefore()
    {
        var data = TokenData();
        return data["not-before-policy"];
    }

    private static void AuthKeys()
    {
        Logger?.Invoke("Auth",@"Getting auth keys");

        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = client.GetStringAsync(BaseUrl).Result;

        dynamic data = JsonConvert.DeserializeObject(response)
                       ?? throw new Exception("Failed to deserialize JSON");

        PublicKey = data.public_key;
    }

    private static void TokenByPasswordGrant(string username, string password, string? otp = "")
    {
        if (TokenClientId == null || TokenClientSecret == null)
            throw new Exception("Auth keys not initialized");

        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        List<KeyValuePair<string, string>> body =
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", TokenClientId),
            new KeyValuePair<string, string>("client_secret", TokenClientSecret),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
        ];
        
        if (!string.IsNullOrEmpty(otp))
            body.Add(new KeyValuePair<string, string>("totp", otp));

        var response = client.PostAsync(TokenUrl, new FormUrlEncodedContent(body))
            .Result.Content.ReadAsStringAsync().Result;

        SetTokens(response);
    }

    private static void TokenByRefreshGrand()
    {
        if (TokenClientId == null || TokenClientSecret == null || RefreshToken == null || _jwtSecurityToken == null)
            throw new Exception("Auth keys not initialized");

        var expiresInDays = _jwtSecurityToken.ValidTo.AddDays(-5).Subtract(DateTime.UtcNow).Days;
        if (expiresInDays >= 0)
        {
            Logger?.Invoke("Auth",$"Token is still valid for {expiresInDays} day{(expiresInDays == 1 ? "" : "s")}");
            return;
        }

        Logger?.Invoke("Auth",@"Refreshing token");

        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        List<KeyValuePair<string, string>> body =
        [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("client_id", TokenClientId),
            new KeyValuePair<string, string>("client_secret", TokenClientSecret),
            new KeyValuePair<string, string>("refresh_token", RefreshToken),
            new KeyValuePair<string, string>("scope", "openid offline_access email profile")
        ];

        var response = client.PostAsync(TokenUrl, new FormUrlEncodedContent(body))
            .Result.Content.ReadAsStringAsync().Result;

        SetTokens(response);
    }

    public static void TokenByAuthorizationCode(string code)
    {
        Logger?.Invoke("Auth",@"Getting token by authorization code");
        if (TokenClientId == null || TokenClientSecret == null)
            throw new Exception("Auth keys not initialized");

        HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new List<KeyValuePair<string, string>>()
        {
            new("grant_type", "authorization_code"),
            new("client_id", TokenClientId),
            new("client_secret", TokenClientSecret),
            new("scope", "openid offline_access email profile"),
            new("redirect_uri", $"http://localhost:{Config.InternalServerPort}/sso-callback"),
            new("code", code)
        };

        var response = client.PostAsync(TokenUrl, new FormUrlEncodedContent(body))
            .Result.Content.ReadAsStringAsync().Result;

        SetTokens(response);
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); 
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url); 
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url); // Not tested
        else
            throw new Exception("Unsupported OS");
    }
    
    private static bool IsDesktopEnvironment()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return true;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;
        
        if (string.IsNullOrEmpty(NmSystem.Info.Gpu?.FirstOrDefault())) return false;
        
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

    }
}