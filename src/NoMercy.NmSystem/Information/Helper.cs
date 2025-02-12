using System.Diagnostics;

namespace NoMercy.NmSystem.Information;

public class Helper
{
    internal static string RunCommand(string command)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using Process process = Process.Start(psi);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return string.IsNullOrEmpty(output) ? "Unknown" : output;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running command: {ex.Message}");
        }
        return "Unknown";
    }
    
}