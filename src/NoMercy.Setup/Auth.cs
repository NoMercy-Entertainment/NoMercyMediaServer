using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;
using HttpClient = NoMercy.NmSystem.Extensions.HttpClient;

namespace NoMercy.Setup;

public static class Auth
{
    private static string BaseUrl => Config.AuthBaseUrl;
    private static readonly string TokenUrl = $"{BaseUrl}protocol/openid-connect/token";
    private static string? PublicKey { get; set; }
    private static string? RefreshToken { get; set; }
    private static int? ExpiresIn { get; set; }
    private static int? NotBefore { get; set; }

    private static JwtSecurityToken? _jwtSecurityToken;

    private static IWebHost? TempServerInstance { get; set; }

    public static Task Init()
    {
        if (!File.Exists(AppFiles.TokenFile))
        {
            File.WriteAllText(AppFiles.TokenFile, "{}");
        }

        AuthKeys();

        Globals.Globals.AccessToken = GetAccessToken();
        RefreshToken = GetRefreshToken();
        ExpiresIn = TokenExpiration();
        NotBefore = TokenNotBefore();

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
        {
            TokenByBrowserOrPassword();
            return Task.CompletedTask;
        }

        JwtSecurityTokenHandler tokenHandler = new();
        _jwtSecurityToken = tokenHandler.ReadJwtToken(Globals.Globals.AccessToken);

        int expiresInDays = _jwtSecurityToken.ValidTo.AddDays(-5).Subtract(DateTime.UtcNow).Days;

        bool expired = NotBefore == null && expiresInDays >= 0;
        
        if (!expired)
            TokenByRefreshGrand();
        else
            TokenByBrowserOrPassword();

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
            throw new("Failed to get tokens");

        return Task.CompletedTask;
    }

    private static void TokenByBrowserOrPassword()
    {
        Logger.Auth("Trying to authenticate by browser, QR code or password", LogEventLevel.Verbose);

        if (IsDesktopEnvironment() && Environment.OSVersion.Platform != PlatformID.Unix)
        {
            TokenByBrowser();
            return;
        }

        Console.WriteLine("Select login method:");
        Console.WriteLine("1. QR code / device login (recommended)");
        Console.WriteLine("2. Password login");
        Console.WriteLine("Auto-selecting QR code login in 15 seconds...");

        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
        Task<ConsoleKeyInfo> inputTask = Task.Run(() =>
        {
            while (!Console.KeyAvailable && !timeoutTask.IsCompleted)
            {
                Thread.Sleep(100);
            }
            return Console.KeyAvailable ? Console.ReadKey(true) : default;
        });

        Task completedTask = Task.WhenAny(timeoutTask, inputTask).Result;

        if (completedTask == timeoutTask || inputTask.Result == default)
        {
            TokenByDeviceGrant();
            return;
        }

        ConsoleKeyInfo key = inputTask.Result;
        Console.WriteLine();

        switch (key.KeyChar)
        {
            case '2':
                TokenByPassword();
                break;
            case '1':
            default:
                TokenByDeviceGrant();
                break;
        }
    }
    
    private static void TokenByDeviceGrant()
    {
        if (Config.TokenClientId == null)
            throw new("Auth keys not initialized");

        Logger.Auth("Authenticating via device grant", LogEventLevel.Verbose);

        using 
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));

        List<KeyValuePair<string, string>> deviceCodeBody =
        [
            new("client_id", Config.TokenClientId),
            new("scope", "openid offline_access email profile")
        ];

        string deviceCodeResponse = client.PostAsync($"{BaseUrl}protocol/openid-connect/auth/device",
                new FormUrlEncodedContent(deviceCodeBody))
            .Result.Content.ReadAsStringAsync().Result;
        
        DeviceAuthResponse deviceData = deviceCodeResponse.FromJson<DeviceAuthResponse>()
                                      ?? throw new("Failed to get device code");
        
        Logger.Auth($"Scan QR code or visit: {deviceData.VerificationUriComplete}");
        
        ConsoleQrCode.Display(deviceData.VerificationUriComplete);

        List<KeyValuePair<string, string>> tokenBody =
        [
            new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            new("client_id", Config.TokenClientId),
            new("device_code", deviceData.DeviceCode)
        ];

        DateTime expiresAt = DateTime.Now.AddSeconds(deviceData.ExpiresIn);
        bool authenticated = false;

        while (DateTime.Now < expiresAt && !authenticated)
        {
            Thread.Sleep(deviceData.Interval * 1000);
            try
            {
                HttpResponseMessage response = client.PostAsync(TokenUrl, new FormUrlEncodedContent(tokenBody))
                    .Result;

                if (response.IsSuccessStatusCode)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    SetTokens(content);
                    authenticated = true;
                    Console.Clear();
                }
                else
                {
                    dynamic? error = JsonConvert.DeserializeObject<dynamic>(response.Content.ReadAsStringAsync().Result);
                    if (error?.error.ToString() != "authorization_pending")
                    {
                        Logger.Auth($"Error: {error?.error_description}", LogEventLevel.Error);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Auth($"Error: {ex.Message}", LogEventLevel.Error);
                return;
            }
        }

        if (!authenticated)
        {
            throw new("Device authorization timed out");
        }
    }

    private static string ReadPassword()
    {
        StringBuilder password = new();
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(true);

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
        Uri baseUrl = new($"{BaseUrl}protocol/openid-connect/auth");
        string redirectUri = HttpUtility.UrlEncode($"http://localhost:{Config.InternalServerPort}/sso-callback");
        string scope = HttpUtility.UrlEncode("openid offline_access email profile");

        IEnumerable<string> query = new Dictionary<string, string>
        {
            ["redirect_uri"] = redirectUri,
            ["client_id"] = Config.TokenClientId,
            ["response_type"] = "code",
            ["scope"] = scope
        }.Select(x => $"{x.Key}={x.Value}");

        string url = new Uri($"{baseUrl}?{string.Join("&", query)}").ToString();

        TempServerInstance = TempServer.Start();
        TempServerInstance.StartAsync().Wait();

        OpenBrowser(url);

        CheckToken();
    }

    private static void CheckToken()
    {
        Task.Run(async () =>
        {
            await Task.Delay(1000);

            if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
                CheckToken();
            else
                TempServerInstance?.StopAsync().Wait();
        }).Wait();
    }

    private static void SetTokens(string response)
    {
        AuthResponse data = response.FromJson<AuthResponse>()
                        ?? throw new("Failed to deserialize JSON");

        FileStream tmp = File.OpenWrite(AppFiles.TokenFile);
        tmp.SetLength(0);
        tmp.Write(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data, Formatting.Indented)));
        tmp.Close();

        Logger.Auth("Tokens refreshed");

        Globals.Globals.AccessToken = data.AccessToken;
        RefreshToken = data.RefreshToken;
        ExpiresIn = data.ExpiresIn;
        NotBefore = data.NotBeforePolicy;
    }

    private static AuthResponse TokenData()
    {
        string fileContents = File.ReadAllText(AppFiles.TokenFile);
        return fileContents.FromJson<AuthResponse>()
               ?? throw new("Failed to deserialize JSON");
    }

    private static string? GetAccessToken()
    {
        AuthResponse data = TokenData();

        return data.AccessToken;
    }

    private static string? GetRefreshToken()
    {
        AuthResponse data = TokenData();
        return data.RefreshToken;
    }

    private static int? TokenExpiration()
    {
        AuthResponse data = TokenData();
        return data.ExpiresIn;
    }

    private static int? TokenNotBefore()
    {
        AuthResponse data = TokenData();
        return data.NotBeforePolicy;
    }

    private static void AuthKeys()
    {
        Logger.Auth("Getting auth keys", LogEventLevel.Verbose);
        
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));

        string response = client.GetStringAsync(BaseUrl).Result;

        AuthKeysResponse data = JsonConvert.DeserializeObject<AuthKeysResponse>(response)
                                ?? throw new("Failed to deserialize JSON");

        PublicKey = data.PublicKey;
    }

    private static void TokenByPassword()
    {
        while (true)
        {
            Console.WriteLine("Enter your email:");
            string? email = Console.ReadLine();

            if (string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Email cannot be empty");
                continue;
            }

            Console.WriteLine("Enter your password:");
            string password = ReadPassword();

            if (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Password cannot be empty");
                continue;
            }

            Console.WriteLine(
                "Enter your 2 factor authentication code (if enabled, hit enter if you don't have it setup):");
            string? otp = Console.ReadLine();

            TokenByPasswordGrant(email, password, otp);
            break;
        }
    }

    private static void TokenByPasswordGrant(string username, string password, string? otp = "")
    {
        if (Config.TokenClientId == null || Config.TokenClientSecret == null)
            throw new("Auth keys not initialized");
        
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));

        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", "password"),
            new("client_id", Config.TokenClientId),
            new("client_secret", Config.TokenClientSecret),
            new("username", username),
            new("password", password)
        ];

        if (!string.IsNullOrEmpty(otp))
            body.Add(new("totp", otp));

        string response = client.PostAsync(TokenUrl, new FormUrlEncodedContent(body))
            .Result.Content.ReadAsStringAsync().Result;

        SetTokens(response);
    }

    private static void TokenByRefreshGrand()
    {
        if (Config.TokenClientId == null || Config.TokenClientSecret == null || RefreshToken == null || _jwtSecurityToken == null)
            throw new("Auth keys not initialized");

        Logger.Auth("Refreshing token");

        
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));

        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", "refresh_token"),
            new("client_id", Config.TokenClientId),
            new("client_secret", Config.TokenClientSecret),
            new("refresh_token", RefreshToken),
            new("scope", "openid offline_access email profile")
        ];

        string response = client.PostAsync(TokenUrl, new FormUrlEncodedContent(body))
            .Result.Content.ReadAsStringAsync().Result;

        SetTokens(response);
    }

    public static void TokenByAuthorizationCode(string code)
    {
        Logger.Auth("Getting token by authorization code", LogEventLevel.Verbose);
        if (Config.TokenClientId == null || Config.TokenClientSecret == null)
            throw new("Auth keys not initialized");

        
        System.Net.Http.HttpClient client = HttpClient.WithDns();
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));

        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", "authorization_code"),
            new("client_id", Config.TokenClientId),
            new("client_secret", Config.TokenClientSecret),
            new("scope", "openid offline_access email profile"),
            new("redirect_uri", $"http://localhost:{Config.InternalServerPort}/sso-callback"),
            new("code", code)
        ];

        string response = client.PostAsync(TokenUrl, new FormUrlEncodedContent(body))
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
            throw new("Unsupported OS");
    }

    private static bool IsDesktopEnvironment()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return true;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return false;

        if (string.IsNullOrEmpty(Info.GpuVendors.FirstOrDefault())) return false;

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));
    }
    
}