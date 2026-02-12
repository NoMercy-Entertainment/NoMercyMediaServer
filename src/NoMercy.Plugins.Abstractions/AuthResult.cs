namespace NoMercy.Plugins.Abstractions;

public class AuthResult
{
    public required bool IsAuthenticated { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, string> Claims { get; init; } = new();
}
