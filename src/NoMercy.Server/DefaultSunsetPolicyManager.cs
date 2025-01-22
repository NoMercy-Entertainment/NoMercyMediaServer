using Asp.Versioning;
using System.Diagnostics.CodeAnalysis;

namespace NoMercy.Server;
internal class DefaultSunsetPolicyManager : ISunsetPolicyManager
{
    public bool TryGetPolicy(string? name, ApiVersion? apiVersion, [MaybeNullWhen(false)] out SunsetPolicy sunsetPolicy)
    {
        sunsetPolicy = new();
        return true;
    }
}