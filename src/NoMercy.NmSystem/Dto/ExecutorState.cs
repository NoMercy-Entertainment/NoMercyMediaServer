namespace NoMercy.NmSystem.Dto;

public enum ExecutorState
{
    Created,
    Running,
    Paused,
    Resuming,
    Completed,
    Cancelled,
    Failed
}