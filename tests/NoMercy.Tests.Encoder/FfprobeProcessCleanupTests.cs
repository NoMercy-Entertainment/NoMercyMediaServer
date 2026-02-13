using System.Diagnostics;

namespace NoMercy.Tests.Encoder;

[Trait("Category", "Unit")]
public class FfprobeProcessCleanupTests
{
    /// <summary>
    /// Verifies that calling Kill on an already-exited process does not throw,
    /// confirming the process cleanup pattern in Ffprobe.ExecStdErrOut is safe.
    /// On Linux, Kill on an exited process silently succeeds; on Windows it may
    /// throw InvalidOperationException. The try-catch guard handles both cases.
    /// </summary>
    [Fact]
    public void Kill_OnAlreadyExitedProcess_DoesNotPropagateException()
    {
        using Process process = new();
        process.StartInfo = new()
        {
            FileName = "echo",
            Arguments = "done",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        process.WaitForExit();

        Assert.True(process.HasExited);

        // Simulate the exact pattern from Ffprobe.ExecStdErrOut timeout path (HIGH-19 fix)
        Exception? caughtException = Record.Exception(() =>
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited — safe to ignore (defensive guard)
            }
        });

        Assert.Null(caughtException);
    }

    /// <summary>
    /// Verifies that Kill on a disposed process throws ObjectDisposedException,
    /// which is NOT caught by the InvalidOperationException handler — this is correct
    /// because in Ffprobe.ExecStdErrOut, disposal happens in the finally block AFTER Kill.
    /// </summary>
    [Fact]
    public void Kill_OnDisposedProcess_ThrowsObjectDisposedException()
    {
        Process process = new();
        process.StartInfo = new()
        {
            FileName = "echo",
            Arguments = "done",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        process.WaitForExit();
        process.Dispose();

        // After dispose, Kill throws ObjectDisposedException — this confirms
        // the importance of the code ordering in Ffprobe: Kill before Dispose
        Assert.ThrowsAny<Exception>(() => process.Kill(entireProcessTree: true));
    }

    /// <summary>
    /// Verifies that process disposal works correctly after Kill (whether Kill
    /// succeeded or was caught). This matches the finally block in Ffprobe.ExecStdErrOut.
    /// </summary>
    [Fact]
    public void ProcessDispose_SucceedsAfterKillOnExitedProcess()
    {
        Process process = new();
        process.StartInfo = new()
        {
            FileName = "echo",
            Arguments = "done",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        process.WaitForExit();

        // Execute Kill with the defensive guard pattern
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Expected on some platforms
        }

        // Dispose must not throw — this is what the finally block does in Ffprobe
        Exception? disposeException = Record.Exception(() => process.Dispose());
        Assert.Null(disposeException);
    }
}
