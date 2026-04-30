namespace NoMercy.Networking.Devices;

public interface IDeviceListChangeNotifier
{
    Task BroadcastChange(Guid ownerUserId);
}
