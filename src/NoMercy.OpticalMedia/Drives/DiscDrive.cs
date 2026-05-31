using NoMercy.NmSystem.Dto;

namespace NoMercy.OpticalMedia.Drives;

public record DiscDrive(string Path, string? Label, bool HasDisc, OpticalDiscType DiscType);
