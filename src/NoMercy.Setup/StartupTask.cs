namespace NoMercy.Setup;

public record StartupTask(
    string Name,
    Func<Task> Action,
    bool CanDefer,
    int Phase,
    string[]? DependsOn = null
);
