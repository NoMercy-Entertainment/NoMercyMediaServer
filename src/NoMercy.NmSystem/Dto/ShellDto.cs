namespace NoMercy.NmSystem.Dto;

public class ExecOptions
{
    public string? WorkingDirectory { get; set; }
    public bool CaptureStdErr { get; set; } = true;
    public bool CaptureStdOut { get; set; } = true;
    public bool RedirectInput { get; set; } = false;
    public bool UseShellExecute { get; set; } = false;
    public bool CreateNoWindow { get; set; } = true;
    public bool MergeStdErrToOut { get; set; } // For "2>&1" behavior

    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}

public class ExecResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool Success => ExitCode == 0;
}
