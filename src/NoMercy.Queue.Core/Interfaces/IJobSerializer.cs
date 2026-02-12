namespace NoMercy.Queue.Core.Interfaces;

public interface IJobSerializer
{
    string Serialize(object job);
    T Deserialize<T>(string data);
}
