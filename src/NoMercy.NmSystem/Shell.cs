using System.Diagnostics;

namespace NoMercy.NmSystem;

public static class Shell
{
    public static async Task<string> Exec(string command, string args)
    {
        Process process = new()
        {
            StartInfo = new()
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string result = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return result;
    }
}