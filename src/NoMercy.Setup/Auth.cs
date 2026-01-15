using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Setup.Dto;
using Serilog.Events;
using Config = NoMercy.NmSystem.Information.Config;

namespace NoMercy.Setup;

public static class Auth
{
    private static string? PublicKey { get; set; }
    private static string? RefreshToken { get; set; }
    private static int? ExpiresIn { get; set; }
    private static int? NotBefore { get; set; }

    private static JwtSecurityToken? _jwtSecurityToken;

    private static IWebHost? TempServerInstance { get; set; }

    public static async Task Init()
    {
        if (!File.Exists(AppFiles.TokenFile)) await File.WriteAllTextAsync(AppFiles.TokenFile, "{}");

        await AuthKeys();

        Globals.Globals.AccessToken = GetAccessToken();
        RefreshToken = GetRefreshToken();
        ExpiresIn = TokenExpiration();
        NotBefore = TokenNotBefore();

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
        {
            try
            {
                await TokenByRefreshGrand();
            }
            catch (Exception e)
            {
                //
            }
            return;
        }

        JwtSecurityTokenHandler tokenHandler = new();
        _jwtSecurityToken = tokenHandler.ReadJwtToken(Globals.Globals.AccessToken);

        int expiresInDays = _jwtSecurityToken.ValidTo.AddDays(-5).Subtract(DateTime.UtcNow).Days;

        bool expired = NotBefore == null && expiresInDays >= 0;

        if (!expired)
            try
            {
                await TokenByRefreshGrand();
            }
            catch (Exception e)
            {
                await TokenByBrowserOrPassword();
            }
        else
            await TokenByBrowserOrPassword();

        if (Globals.Globals.AccessToken == null || RefreshToken == null || ExpiresIn == null)
            throw new("Failed to get tokens");
    }

    private static async Task TokenByBrowserOrPassword()
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
            while (!Console.KeyAvailable && !timeoutTask.IsCompleted) Thread.Sleep(100);
            return Console.KeyAvailable ? Console.ReadKey(true) : default;
        });

        Task completedTask = Task.WhenAny(timeoutTask, inputTask).Result;

        if (completedTask == timeoutTask || inputTask.Result == default)
        {
            await TokenByDeviceGrant();
            return;
        }

        ConsoleKeyInfo key = inputTask.Result;
        Console.WriteLine();

        switch (key.KeyChar)
        {
            case '2':
                await TokenByPassword();
                break;
            default:
                await TokenByDeviceGrant();
                break;
        }
    }

    private static async Task TokenByDeviceGrant()
    {
        if (Config.TokenClientId == null)
            throw new("Auth keys not initialized");

        Logger.Auth("Authenticating via device grant", LogEventLevel.Verbose);

        List<KeyValuePair<string, string>> deviceCodeBody =
        [
            new("client_id", Config.TokenClientId),
            new("scope", "openid offline_access email profile")
        ];

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string deviceCodeResponse = await authClient.SendAndReadAsync(HttpMethod.Post,
            "protocol/openid-connect/auth/device", new FormUrlEncodedContent(deviceCodeBody));

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
                HttpResponseMessage response = await authClient.SendAsync(HttpMethod.Post,
                    "protocol/openid-connect/token", new FormUrlEncodedContent(tokenBody));

                if (response.IsSuccessStatusCode)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    AuthResponse data = content.FromJson<AuthResponse>()
                                        ?? throw new("Failed to deserialize JSON");
                    SetTokens(data);
                    authenticated = true;
                    Console.Clear();
                }
                else
                {
                    dynamic? error =
                        JsonConvert.DeserializeObject<dynamic>(response.Content.ReadAsStringAsync().Result);
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

        if (!authenticated) throw new("Device authorization timed out");
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
        if (string.IsNullOrEmpty(Config.AuthBaseUrl) || string.IsNullOrEmpty(Config.TokenClientId))
            throw new ArgumentException("Auth base URL or client ID is not initialized");

        string baseUrl = Config.AuthBaseUrl.TrimEnd('/') + "/protocol/openid-connect/auth";
        string redirectUri = $"http://localhost:{Config.InternalServerPort}/sso-callback";
        string scope = "openid offline_access email profile";

        Dictionary<string, string> queryParams = new()
        {
            ["client_id"] = Config.TokenClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope
        };

        string queryString = string.Join("&", queryParams.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        string url = $"{baseUrl}?{queryString}";
        Logger.Setup($"Opening browser for authentication: {url}", LogEventLevel.Verbose);

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

    private static void SetTokens(AuthResponse data)
    {
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

    private static async Task AuthKeys()
    {
        Logger.Auth("Getting auth keys", LogEventLevel.Verbose);

        GenericHttpClient authClient = new();
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(HttpMethod.Get, Config.AuthBaseUrl);

        AuthKeysResponse data = JsonConvert.DeserializeObject<AuthKeysResponse>(response)
                                ?? throw new("Failed to deserialize JSON");

        PublicKey = data.PublicKey;
    }

    private static async Task TokenByPassword()
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

            await TokenByPasswordGrant(email, password, otp);
            break;
        }
    }

    private static async Task TokenByPasswordGrant(string username, string password, string? otp = "")
    {
        if (Config.TokenClientId == null || Config.TokenClientSecret == null)
            throw new("Auth keys not initialized");

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

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "protocol/openid-connect/token",
            new FormUrlEncodedContent(body));
        
        AuthResponse data = response.FromJson<AuthResponse>()
                            ?? throw new("Failed to deserialize JSON");

        SetTokens(data);
    }

    private static async Task TokenByRefreshGrand()
    {
        if (Config.TokenClientId == null || Config.TokenClientSecret == null || RefreshToken == null ||
            _jwtSecurityToken == null)
            throw new("Auth keys not initialized");

        Logger.Auth("Refreshing token");

        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", "refresh_token"),
            new("client_id", Config.TokenClientId),
            new("client_secret", Config.TokenClientSecret),
            new("refresh_token", RefreshToken),
            new("scope", "openid offline_access email profile")
        ];

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "protocol/openid-connect/token",
            new FormUrlEncodedContent(body));
        
        AuthResponse data = response.FromJson<AuthResponse>()
                            ?? throw new("Failed to deserialize JSON");
        
        SetTokens(data);
    }

    public static async Task TokenByAuthorizationCode(string code)
    {
        Logger.Auth("Getting token by authorization code", LogEventLevel.Verbose);
        if (Config.TokenClientId == null || Config.TokenClientSecret == null)
            throw new("Auth keys not initialized");

        List<KeyValuePair<string, string>> body =
        [
            new("grant_type", "authorization_code"),
            new("client_id", Config.TokenClientId),
            new("client_secret", Config.TokenClientSecret),
            new("scope", "openid offline_access email profile"),
            new("redirect_uri", $"http://localhost:{Config.InternalServerPort}/sso-callback"),
            new("code", code)
        ];

        GenericHttpClient authClient = new(Config.AuthBaseUrl);
        authClient.SetDefaultHeaders(Config.UserAgent);
        string response = await authClient.SendAndReadAsync(HttpMethod.Post, "protocol/openid-connect/token",
            new FormUrlEncodedContent(body));
        
        AuthResponse data = response.FromJson<AuthResponse>()
                            ?? throw new("Failed to deserialize JSON");

        SetTokens(data);
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