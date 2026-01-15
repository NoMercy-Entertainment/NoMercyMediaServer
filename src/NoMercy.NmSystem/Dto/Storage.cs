namespace NoMercy.NmSystem.Dto;

public class StorageDevice
{
    public string Name { get; set; } = string.Empty;
    public long TotalSpace { get; set; }
    public long FreeSpace { get; set; }
    public long UsedSpace => TotalSpace - FreeSpace;
}