namespace NoMercy.Encoder.Errors;

public enum EncodingErrorKind
{
    InputNotFound,
    InputCorrupt,
    InputUnsupported,
    CodecUnavailable,
    HardwareUnavailable,
    HardwareFailure,
    ProfileInvalid,
    DiskFull,
    Timeout,
    Cancelled,
    ProcessCrashed,
    NetworkPathUnavailable,
    NetworkPathTimeout,
    NetworkPathPermission,
    ResourceExhausted,
    InputDrmProtected,
    Unknown,
}
