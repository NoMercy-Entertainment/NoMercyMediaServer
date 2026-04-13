namespace NoMercy.Encoder.Commands;

public record FfmpegCommand(string Executable, string[] Arguments, string? WorkingDirectory);
