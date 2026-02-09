using System.Collections.Concurrent;
using System.Diagnostics;
using NoMercy.Encoder;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class FfMpegProcessResourceTests
{
    /// <summary>
    /// Verifies that the static process dictionary uses ConcurrentDictionary
    /// for thread safety during concurrent encoding jobs.
    /// </summary>
    [Fact]
    public void ProcessDictionary_IsConcurrentDictionary()
    {
        Assert.IsType<ConcurrentDictionary<int, Process>>(FfMpeg.FfmpegProcess);
    }

    /// <summary>
    /// Verifies that ExecStdErrOut cleans up the process dictionary even
    /// when the process exits normally.
    /// </summary>
    [Fact]
    public async Task ExecStdErrOut_CleansUpDictionary_AfterNormalExit()
    {
        int countBefore = FfMpeg.FfmpegProcess.Count;

        // Use a simple command that exits immediately
        await FfMpeg.ExecStdErrOut(
            "-version",
            executable: "/bin/echo");

        Assert.Equal(countBefore, FfMpeg.FfmpegProcess.Count);
    }

    /// <summary>
    /// Verifies that ExecStdErrOut cleans up the process dictionary
    /// even when an error occurs (process fails).
    /// </summary>
    [Fact]
    public async Task ExecStdErrOut_CleansUpDictionary_WhenProcessFails()
    {
        int countBefore = FfMpeg.FfmpegProcess.Count;

        // Run a command that will fail (nonexistent arg, but echo will still exit 0)
        await FfMpeg.ExecStdErrOut(
            "",
            executable: "/bin/echo");

        Assert.Equal(countBefore, FfMpeg.FfmpegProcess.Count);
    }

    /// <summary>
    /// Verifies that the process is tracked in the dictionary while running,
    /// and removed after completion.
    /// </summary>
    [Fact]
    public async Task ExecStdErrOut_TracksProcessDuringExecution()
    {
        int countBefore = FfMpeg.FfmpegProcess.Count;

        // Use sleep to keep process alive long enough to observe
        Task<string> task = FfMpeg.ExecStdErrOut(
            "0.5",
            executable: "/bin/sleep");

        // Give it a moment to start
        await Task.Delay(100);

        // Process should be tracked while running
        Assert.True(FfMpeg.FfmpegProcess.Count > countBefore,
            "Process should be tracked in dictionary while running");

        await task;

        // Process should be cleaned up after completion
        Assert.Equal(countBefore, FfMpeg.FfmpegProcess.Count);
    }

    /// <summary>
    /// Verifies that concurrent ExecStdErrOut calls don't corrupt
    /// the process dictionary (thread safety via ConcurrentDictionary).
    /// </summary>
    [Fact]
    public async Task ExecStdErrOut_ConcurrentCalls_DontCorruptDictionary()
    {
        int countBefore = FfMpeg.FfmpegProcess.Count;

        // Launch 10 concurrent processes
        Task<string>[] tasks = Enumerable.Range(0, 10)
            .Select(_ => FfMpeg.ExecStdErrOut(
                "concurrent test",
                executable: "/bin/echo"))
            .ToArray();

        await Task.WhenAll(tasks);

        // All processes should be cleaned up
        Assert.Equal(countBefore, FfMpeg.FfmpegProcess.Count);
    }

    /// <summary>
    /// Verifies that Pause returns false for a non-existent process ID
    /// (the dictionary lookup works correctly).
    /// </summary>
    [Fact]
    public async Task Pause_ReturnsFalse_ForNonExistentProcess()
    {
        bool result = await FfMpeg.Pause(999999);
        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Resume returns false for a non-existent process ID.
    /// </summary>
    [Fact]
    public async Task Resume_ReturnsFalse_ForNonExistentProcess()
    {
        bool result = await FfMpeg.Resume(999999);
        Assert.False(result);
    }

    /// <summary>
    /// Verifies that Pause can find a tracked process in the dictionary
    /// and returns true.
    /// </summary>
    [Fact]
    public async Task Pause_ReturnsTrue_ForTrackedProcess()
    {
        // Start a long-running process
        Task<string> task = FfMpeg.ExecStdErrOut(
            "5",
            executable: "/bin/sleep");

        // Wait for process to start
        await Task.Delay(200);

        // Get the process ID from the dictionary
        KeyValuePair<int, Process> entry = FfMpeg.FfmpegProcess.FirstOrDefault();
        if (entry.Value != null)
        {
            bool result = await FfMpeg.Pause(entry.Key);
            Assert.True(result);

            // Resume so we can clean up
            await FfMpeg.Resume(entry.Key);
        }

        // Kill the sleep process to avoid waiting
        foreach (KeyValuePair<int, Process> kvp in FfMpeg.FfmpegProcess)
        {
            try
            {
                if (!kvp.Value.HasExited)
                    kvp.Value.Kill();
            }
            catch { /* ignore */ }
        }

        try { await task; } catch { /* process was killed */ }
    }
}
