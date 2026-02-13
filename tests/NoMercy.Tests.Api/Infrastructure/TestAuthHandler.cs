using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NoMercy.Tests.Api.Infrastructure;

public static class TestAuthDefaults
{
    public const string AuthenticationScheme = "TestScheme";
    public const string TestAuthHeader = "X-Test-Auth";
    public const string Deny = "deny";
}

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static Guid DefaultUserId { get; } = Guid.Parse("37d03e60-7b0a-4246-a85b-a5618966a383");
    public static string DefaultUserName { get; } = "Test User";
    public static string DefaultUserEmail { get; } = "test@nomercy.tv";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(TestAuthDefaults.TestAuthHeader, out Microsoft.Extensions.Primitives.StringValues value)
            && value.ToString() == TestAuthDefaults.Deny)
        {
            return Task.FromResult(AuthenticateResult.Fail("Authentication denied by test"));
        }

        Guid userId = DefaultUserId;
        string userName = DefaultUserName;
        string userEmail = DefaultUserEmail;

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Email, userEmail),
            new(ClaimTypes.Role, "user"),
            new("scope", "openid"),
            new("scope", "profile")
        ];

        ClaimsIdentity identity = new(claims, TestAuthDefaults.AuthenticationScheme);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, TestAuthDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
