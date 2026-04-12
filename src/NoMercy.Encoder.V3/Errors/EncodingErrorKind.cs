namespace NoMercy.Encoder.V3.Errors;

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
    Unknown,
}
