namespace NoMercy.Plugins.Abstractions;

public static class PluginLifecycle
{
    private static readonly Dictionary<PluginStatus, HashSet<PluginStatus>> AllowedTransitions = new()
    {
        [PluginStatus.Active] = [PluginStatus.Disabled, PluginStatus.Malfunctioned, PluginStatus.Deleted],
        [PluginStatus.Disabled] = [PluginStatus.Active, PluginStatus.Deleted],
        [PluginStatus.Malfunctioned] = [PluginStatus.Active, PluginStatus.Disabled, PluginStatus.Deleted],
        [PluginStatus.Deleted] = []
    };

    public static bool CanTransition(PluginStatus from, PluginStatus to)
    {
        if (AllowedTransitions.TryGetValue(from, out HashSet<PluginStatus>? allowed))
        {
            return allowed.Contains(to);
        }

        return false;
    }

    public static void Transition(PluginInfo info, PluginStatus newStatus)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (!CanTransition(info.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition plugin '{info.Name}' from {info.Status} to {newStatus}.");
        }

        info.Status = newStatus;
    }
}
