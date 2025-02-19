using System.Diagnostics;
using System.Text;

namespace NoMercy.NmSystem.SystemCalls;

public static class Shell
{
    public class ExecOptions
    {
        public string? WorkingDirectory { get; set; }
        public bool CaptureStdErr { get; set; } = true;
        public bool CaptureStdOut { get; set; } = true;
        public bool RedirectInput { get; set; } = false;
        public bool UseShellExecute { get; set; } = false;
        public bool CreateNoWindow { get; set; } = true;
        public bool MergeStdErrToOut { get; set; } = false; // For "2>&1" behavior
    }

    public class ExecResult
    {
        public int ExitCode { get; init; }
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
        public bool Success => ExitCode == 0;
    }

    public static async Task<ExecResult> ExecAsync(string executable, string arguments, ExecOptions? options = null)
    {
        options ??= new();
        using Process process = new();
        process.StartInfo = new()
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = options.WorkingDirectory ?? string.Empty,
            RedirectStandardOutput = options.CaptureStdOut,
            RedirectStandardError = options.CaptureStdErr && !options.MergeStdErrToOut,
            RedirectStandardInput = options.RedirectInput,
            UseShellExecute = options.UseShellExecute,
            CreateNoWindow = options.CreateNoWindow
        };

        if (options.MergeStdErrToOut)
            process.StartInfo.RedirectStandardError = false;
        else
            process.StartInfo.RedirectStandardError = options.CaptureStdErr;

        StringBuilder outputBuilder = new();
        StringBuilder errorBuilder = new();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };

        if (options.CaptureStdErr && !options.MergeStdErrToOut)
        {
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };
        }

        try
        {
            process.Start();

            if (options.CaptureStdOut)
                process.BeginOutputReadLine();
            if (options.CaptureStdErr && !options.MergeStdErrToOut)
                process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            string stdOutput = outputBuilder.ToString().Trim();
            string stdError = errorBuilder.ToString().Trim();

            if (options.MergeStdErrToOut)
                stdOutput += await process.StandardError.ReadToEndAsync();

            return new()
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdOutput,
                StandardError = stdError
            };
        }
        catch (Exception ex)
        {
            return new()
            {
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = $"Error executing command: {ex.Message}"
            };
        }
    }

    public static ExecResult ExecSync(string executable, string arguments, ExecOptions? options = null)
        => ExecAsync(executable, arguments, options).GetAwaiter().GetResult();

    public static async Task<string> ExecStdOutAsync(string executable, string arguments, ExecOptions? options = null)
        => (await ExecAsync(executable, arguments, options)).StandardOutput;

    public static string ExecStdOutSync(string executable, string arguments, ExecOptions? options = null)
        => ExecSync(executable, arguments, options).StandardOutput;

    public static async Task<string> ExecStdErrAsync(string executable, string arguments, ExecOptions? options = null)
    {
        options ??= new() { CaptureStdErr = true, CaptureStdOut = false };
        return (await ExecAsync(executable, arguments, options)).StandardError;
    }

    public static string ExecStdErrSync(string executable, string arguments, ExecOptions? options = null)
    {
        options ??= new() { CaptureStdErr = true, CaptureStdOut = false };
        return ExecSync(executable, arguments, options).StandardError;
    }
}

