namespace NoMercy.Setup.Boot;

public record StartupTask(
    string Name,
    Func<Task> Action,
    bool CanDefer,
    int Phase,
    string[]? DependsOn = null
);
