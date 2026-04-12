namespace NoMercy.Encoder.V3.Commands;

public record FfmpegCommand(string Executable, string[] Arguments, string? WorkingDirectory);
