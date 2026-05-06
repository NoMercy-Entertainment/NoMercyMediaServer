namespace NoMercy.Encoder.Profiles;

[Flags]
public enum ClientCompatibility
{
    None = 0,
    BrowserMse = 1 << 0,
    NativeAndroid = 1 << 1,
    NativeIos = 1 << 2,
    Cast = 1 << 3,
    LegacyDevices = 1 << 4,
    Universal = BrowserMse | NativeAndroid | NativeIos | Cast,
}
