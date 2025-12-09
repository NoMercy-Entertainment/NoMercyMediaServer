using System.Collections.Concurrent;

namespace NoMercy.NmSystem.Dto;

public sealed class ExecutorRegistry
{
    readonly ConcurrentDictionary<Guid, ExecutorHandle> _handles = new();

    public ExecutorHandle Register(ExecutorHandle handle)
    {
        _handles[handle.Id] = handle;
        return handle;
    }

    public bool TryGet(Guid id, out ExecutorHandle? handle) => _handles.TryGetValue(id, out handle);

    public bool TryRemove(Guid id, out ExecutorHandle? handle) => _handles.TryRemove(id, out handle);

    public IEnumerable<ExecutorHandle> List() => _handles.Values;
}