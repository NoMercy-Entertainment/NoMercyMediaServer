using System.Text.RegularExpressions;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace NoMercy.Tests.Providers.Setup;

[Trait("Category", "Characterization")]
public class AuthExpirationTests
{
    private static readonly string SourcePath = FindSourceFile();

    private static string FindSourceFile()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(dir, "src", "NoMercy.Setup", "Auth.cs");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir)!;
        }
        throw new FileNotFoundException("Could not find src/NoMercy.Setup/Auth.cs");
    }

    private static string GetSourceCode() => File.ReadAllText(SourcePath);

    private static string ExtractInitMethod(string source)
    {
        int start = source.IndexOf("public static async Task Init()", StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("Could not find Init() method");

        int braceCount = 0;
        bool foundFirst = false;
        int end = start;
        for (int i = start; i < source.Length; i++)
        {
            if (source[i] == '{') { braceCount++; foundFirst = true; }
            if (source[i] == '}') braceCount--;
            if (foundFirst && braceCount == 0) { end = i + 1; break; }
        }
        return source[start..end];
    }

    [Fact]
    public void ExpirationCheck_DoesNotReferenceNotBefore()
    {
        // The fixed expression should be `expiresInDays < 0`, not involving NotBefore
        string source = GetSourceCode();
        string initMethod = ExtractInitMethod(source);

        // Find the line that assigns `bool expired = ...`
        Regex expiredAssignment = new(@"bool\s+expired\s*=\s*([^;]+);");
        RegexMatch match = expiredAssignment.Match(initMethod);
        Assert.True(match.Success, "Could not find 'bool expired = ...' assignment in Init()");

        string expression = match.Groups[1].Value;
        Assert.DoesNotContain("NotBefore", expression);
    }

    [Fact]
    public void ExpirationCheck_UsesLessThanZero()
    {
        // The fixed expression should check expiresInDays < 0
        string source = GetSourceCode();
        string initMethod = ExtractInitMethod(source);

        Regex expiredAssignment = new(@"bool\s+expired\s*=\s*([^;]+);");
        RegexMatch match = expiredAssignment.Match(initMethod);
        Assert.True(match.Success, "Could not find 'bool expired = ...' assignment in Init()");

        string expression = match.Groups[1].Value.Trim();
        Assert.Contains("expiresInDays", expression);
        Assert.Contains("< 0", expression);
    }

    [Fact]
    public void ExpirationCheck_IsNotInvertedGreaterThanOrEqual()
    {
        // Regression: must NOT use >= 0 (which was the buggy expression)
        string source = GetSourceCode();
        string initMethod = ExtractInitMethod(source);

        Regex expiredAssignment = new(@"bool\s+expired\s*=\s*([^;]+);");
        RegexMatch match = expiredAssignment.Match(initMethod);
        Assert.True(match.Success, "Could not find 'bool expired = ...' assignment in Init()");

        string expression = match.Groups[1].Value;
        Assert.DoesNotContain(">= 0", expression);
    }

    [Fact]
    public void ExpirationCheck_NoLogicalAndWithNotBefore()
    {
        // Regression: must NOT have the original `NotBefore == null && expiresInDays >= 0` pattern
        string source = GetSourceCode();
        string initMethod = ExtractInitMethod(source);

        Regex buggyPattern = new(@"NotBefore\s*==\s*null\s*&&\s*expiresInDays\s*>=\s*0");
        Assert.False(buggyPattern.IsMatch(initMethod),
            "Init() still contains the buggy expression: NotBefore == null && expiresInDays >= 0");
    }

    [Fact]
    public void ExpirationLogic_ValidTokenNotExpired()
    {
        // Simulate: token with 10 days remaining (after -5 day offset = 5 days positive)
        int expiresInDays = 5;
        bool expired = expiresInDays < 0;
        Assert.False(expired, "A token with 5+ days remaining should NOT be marked expired");
    }

    [Fact]
    public void ExpirationLogic_ExpiredTokenIsExpired()
    {
        // Simulate: token expired 2 days ago (after -5 day offset = negative)
        int expiresInDays = -2;
        bool expired = expiresInDays < 0;
        Assert.True(expired, "A token that expired 2 days ago should be marked expired");
    }

    [Fact]
    public void ExpirationLogic_ZeroDaysIsNotExpired()
    {
        // Simulate: token at exactly the 5-day boundary
        int expiresInDays = 0;
        bool expired = expiresInDays < 0;
        Assert.False(expired, "A token at exactly the boundary (0 days) should NOT be marked expired");
    }

    [Fact]
    public void ExpirationLogic_NegativeOneDayIsExpired()
    {
        // Simulate: token 1 day past the refresh window
        int expiresInDays = -1;
        bool expired = expiresInDays < 0;
        Assert.True(expired, "A token 1 day past the refresh window should be marked expired");
    }

    [Fact]
    public void ExpirationCheck_ExpressionIsSimple()
    {
        // The fixed expression should be just `expiresInDays < 0` — nothing more
        string source = GetSourceCode();
        string initMethod = ExtractInitMethod(source);

        Regex expiredAssignment = new(@"bool\s+expired\s*=\s*([^;]+);");
        RegexMatch match = expiredAssignment.Match(initMethod);
        Assert.True(match.Success, "Could not find 'bool expired = ...' assignment in Init()");

        string expression = match.Groups[1].Value.Trim();
        Assert.Equal("expiresInDays < 0", expression);
    }

    [Fact]
    public void IfNotExpired_TriesRefreshFirst()
    {
        // Verify the control flow: !expired → TokenByRefreshGrand (with fallback)
        string source = GetSourceCode();
        string initMethod = ExtractInitMethod(source);

        // After `bool expired = ...`, the code should have `if (!expired)` followed by TokenByRefreshGrand
        Regex controlFlow = new(@"if\s*\(\s*!expired\s*\).*?TokenByRefreshGrand", RegexOptions.Singleline);
        Assert.True(controlFlow.IsMatch(initMethod),
            "Init() should try TokenByRefreshGrand when token is not expired (!expired)");
    }

    [Fact]
    public void ElseBranch_GoesToBrowserOrPassword()
    {
        // Verify the control flow: else → TokenByBrowserOrPassword
        string source = GetSourceCode();
        string initMethod = ExtractInitMethod(source);

        // The else branch should call TokenByBrowserOrPassword directly
        Regex elseBranch = new(@"else\s+await\s+TokenByBrowserOrPassword", RegexOptions.Singleline);
        Assert.True(elseBranch.IsMatch(initMethod),
            "Init() should call TokenByBrowserOrPassword in the else branch (expired token)");
    }
}
