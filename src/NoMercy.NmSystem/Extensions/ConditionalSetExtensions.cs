namespace NoMercy.NmSystem.Extensions;

public static class ConditionalSetExtensions
{
    public static T? GetIf<T>(this T? source, bool condition) where T : class
    {
        return condition && source != null ? source : null;
    }
    
    public static T? GetIfNotNull<T>(this T? source) where T : class
    {
        return source;
    }
}