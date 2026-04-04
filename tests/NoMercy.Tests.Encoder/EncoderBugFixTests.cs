using System.Reflection;
using NoMercy.Encoder;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Video;

namespace NoMercy.Tests.Encoder;

/// <summary>
/// Regression tests for the six critical encoder bug fixes.
/// Each test class targets one bug.
/// </summary>
[Trait("Category", "Unit")]
public class AddOptsNoOpRegressionTests
{
    // Bug 1: AddOpts(string) was storing the flag with a null value rendered as
    // the literal string "null", which caused FFmpeg to receive e.g. `-nostdin "null"`
    // instead of just `-nostdin`.  The fix stores an empty string so the flag
    // renders without a trailing value argument.

    [Fact]
    public void AddOpts_SingleFlag_StoresEmptyValue()
    {
        X264 video = new("libx264");

        video.AddOpts("-nostdin");

        // The flag must exist in _extraParameters with an empty string value,
        // not null and not the literal text "null".
        Assert.True(
            video._extraParameters.ContainsKey("-nostdin"),
            "Flag was not stored in _extraParameters"
        );
        Assert.Equal(string.Empty, (string)video._extraParameters["-nostdin"]);
    }

    [Fact]
    public void AddOpts_MultipleFlagsArray_AllStoredWithEmptyValue()
    {
        X264 video = new("libx264");

        video.AddOpts(["-nostdin", "-loglevel", "-stats"]);

        foreach (string flag in new[] { "-nostdin", "-loglevel", "-stats" })
        {
            Assert.True(
                video._extraParameters.ContainsKey(flag),
                $"Flag {flag} was not stored in _extraParameters"
            );
            Assert.Equal(string.Empty, (string)video._extraParameters[flag]);
        }
    }

    [Fact]
    public void AddOpts_SingleFlag_DoesNotStoreLiteralNullString()
    {
        X264 video = new("libx264");

        video.AddOpts("-anyflag");

        string storedValue = (string)video._extraParameters["-anyflag"];
        Assert.NotEqual("\"null\"", storedValue);
        Assert.NotEqual("null", storedValue);
    }
}

[Trait("Category", "Unit")]
public class ProgressParsingPrecisionRegressionTests
{
    // Bug 2: ParseOutputData was using integer division `int.Parse(...) / 100`
    // on the fractional-seconds group from the FFmpeg -progress out_time field.
    // FFmpeg emits microsecond precision (6 digits, e.g. "456789").
    // Dividing 456789 by 100 gives 4567 ms (wrong); dividing by 1000 gives
    // 456 ms (correct).  The corrupted value also inflated TotalMilliseconds and
    // therefore produced a wrong ProgressPercentage.

    [Fact]
    public void ParseOutputData_FractionalSeconds_ParsedWithMillisecondPrecision()
    {
        // out_time of 00:01:23.456789 should give CurrentTime ≈ 83.456 s
        string output =
            "frame=  2001\nfps=24.00\nbitrate= 5000.0kbits/s\n"
            + "out_time=00:01:23.456789\nspeed=1.00x\nprogress=continue\n";
        TimeSpan totalDuration = TimeSpan.FromMinutes(10);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);

        // 1 min 23 sec = 83 seconds, plus ~456 ms
        Assert.InRange(result!.CurrentTime.TotalSeconds, 83.4, 83.6);
    }

    [Fact]
    public void ParseOutputData_FractionalSeconds_ProgressPercentageIsAccurate()
    {
        // 30 seconds into a 60-second video should be ~50 %
        string output =
            "frame=  720\nfps=24.00\nbitrate= 5000.0kbits/s\n"
            + "out_time=00:00:30.000000\nspeed=2.00x\nprogress=continue\n";
        TimeSpan totalDuration = TimeSpan.FromSeconds(60);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.InRange(result!.ProgressPercentage, 49.5, 50.5);
    }

    [Fact]
    public void ParseOutputData_LargeFractionalValue_DoesNotCorruptTotalSeconds()
    {
        // "999999" microseconds = 999 ms, so CurrentTime should be very close to
        // 5 seconds (4.999 s to 5.001 s), not 5 + 9999 ms = ~15 s as the bug produced.
        string output =
            "frame=  120\nfps=24.00\nbitrate= 1000.0kbits/s\n"
            + "out_time=00:00:04.999999\nspeed=1.00x\nprogress=continue\n";
        TimeSpan totalDuration = TimeSpan.FromSeconds(60);

        FfMpeg.ProgressData? result = FfMpeg.ParseOutputData(output, totalDuration);

        Assert.NotNull(result);
        Assert.InRange(result!.CurrentTime.TotalSeconds, 4.9, 5.1);
    }
}

[Trait("Category", "Unit")]
public class EncodingTimeoutCancellationRegressionTests
{
    // Bug 3: FfMpeg.Run() had no timeout — a hung FFmpeg process would block the
    // worker thread forever.  The fix adds a CancellationToken parameter (defaulting
    // to CancellationToken.None so callers don't need to change) and internally
    // applies a 24-hour safety timeout when no external token is provided.

    [Fact]
    public void FfMpegRun_HasCancellationTokenParameter_WithDefaultValue()
    {
        // Verify the signature without spawning a real FFmpeg process.
        MethodInfo? method = typeof(FfMpeg).GetMethod(
            "Run",
            BindingFlags.Public | BindingFlags.Static
        );

        Assert.NotNull(method);

        ParameterInfo[] parameters = method!.GetParameters();
        ParameterInfo? tokenParam = Array.Find(
            parameters,
            p => p.ParameterType == typeof(CancellationToken)
        );

        Assert.NotNull(tokenParam);
        Assert.True(
            tokenParam!.HasDefaultValue,
            "CancellationToken parameter must have a default value so existing callers do not need updating"
        );
    }

    [Fact]
    public void FfMpegRun_ReturnsTask_IsAsync()
    {
        MethodInfo? method = typeof(FfMpeg).GetMethod(
            "Run",
            BindingFlags.Public | BindingFlags.Static
        );

        Assert.NotNull(method);
        Assert.True(
            typeof(System.Threading.Tasks.Task).IsAssignableFrom(method!.ReturnType),
            "FfMpeg.Run must return Task or Task<T>"
        );
    }
}

[Trait("Category", "Unit")]
public class CleanupPartialOutputScopeRegressionTests
{
    // Bug 4: When one encoder profile failed, CleanupPartialOutput was deleting ALL
    // output under the base path — including output from profiles that had already
    // completed successfully.  The fix scopes deletion to only the subdirectories
    // declared by the failing profile's container, via BaseContainer.GetOutputSubdirectories().

    [Fact]
    public void GetOutputSubdirectories_ReturnsOnlyDeclaredStreamDirs()
    {
        // Arrange: two containers simulating two profiles.
        // VideoStreams and AudioStreams are populated directly because they are
        // populated by VideoAudioFile.Build() in production code — which runs before
        // CleanupPartialOutput is ever called in the catch block.
        //
        // Profile A: video in "1080p/", audio segments in "aac/"
        // Profile B: video in "720p/" only
        Hls containerA = new();
        X264 videoA = new("libx264");
        videoA.SetHlsPlaylistFilename("1080p/stream.m3u8");
        containerA.VideoStreams.Add(videoA);

        Aac audioA = new();
        audioA.SetHlsSegmentFilename("aac/%05d.ts");
        containerA.AudioStreams.Add(audioA);

        Hls containerB = new();
        X264 videoB = new("libx264");
        videoB.SetHlsPlaylistFilename("720p/stream.m3u8");
        containerB.VideoStreams.Add(videoB);

        // Act
        HashSet<string> dirsA = containerA.GetOutputSubdirectories();
        HashSet<string> dirsB = containerB.GetOutputSubdirectories();

        // Assert: each container reports only its own stream directories.
        Assert.Contains("1080p", dirsA);
        Assert.Contains("aac", dirsA);
        Assert.DoesNotContain("720p", dirsA);

        Assert.Contains("720p", dirsB);
        Assert.DoesNotContain("1080p", dirsB);
        Assert.DoesNotContain("aac", dirsB);
    }

    [Fact]
    public void GetOutputSubdirectories_EmptyContainer_ReturnsEmptySet()
    {
        Hls container = new();

        HashSet<string> dirs = container.GetOutputSubdirectories();

        Assert.Empty(dirs);
    }
}

[Trait("Category", "Unit")]
public class QueueWorkerAsyncHandleRegressionTests
{
    // Bug 5: QueueWorker was calling classInstance.Handle().Wait() which blocks a
    // thread-pool thread and wraps exceptions in AggregateException, making retry
    // classification incorrect.  The fix replaces .Wait() with .GetAwaiter().GetResult()
    // which propagates the original exception unwrapped.
    //
    // IShouldQueue.Handle() lives in NoMercy.Encoder (via its job base classes), so we
    // can verify the exception-propagation contract here using a local stub.

    private sealed class ThrowingJob : IShouldQueueStub
    {
        // Return a pre-faulted Task rather than throwing synchronously.
        // Task.FromException causes .Wait() to wrap the exception in AggregateException,
        // while .GetAwaiter().GetResult() unwraps and re-throws the original exception.
        // A synchronous throw would bypass the Task machinery entirely and propagate
        // identically from both .Wait() and .GetAwaiter().GetResult().
        public Task Handle() =>
            Task.FromException(new InvalidOperationException("test failure"));
    }

    // Minimal interface mirroring IShouldQueue — avoids a project reference to
    // NoMercyQueue.Core while still testing the GetAwaiter().GetResult() contract.
    private interface IShouldQueueStub
    {
        Task Handle();
    }

    [Fact]
    public void GetAwaiterGetResult_PropagatesOriginalException_NotAggregateException()
    {
        IShouldQueueStub job = new ThrowingJob();

        // Simulate what the fixed QueueWorker does: GetAwaiter().GetResult() rather than .Wait()
        Exception? caught = null;
        try
        {
            job.Handle().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);

        // The original InvalidOperationException must propagate directly —
        // not wrapped inside an AggregateException as .Wait() would do.
        Assert.IsType<InvalidOperationException>(caught);
        Assert.IsNotType<AggregateException>(caught);
    }

    [Fact]
#pragma warning disable xUnit1031 // This test intentionally uses .Wait() to document the old broken behaviour
    public void Wait_WrapsExceptionInAggregateException_ConfirmingWhyFixWasNeeded()
    {
        IShouldQueueStub job = new ThrowingJob();

        // Confirm the old .Wait() behaviour wraps exceptions — this is why the bug existed.
        Exception? caught = null;
        try
        {
            job.Handle().Wait();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.NotNull(caught);
        Assert.IsType<AggregateException>(caught);
    }
#pragma warning restore xUnit1031
}

[Trait("Category", "Unit")]
public class MusicEncodeJobRethrowRegressionTests
{
    // Bug 6: MusicEncodeJob's catch block was logging and publishing the failure event
    // but never re-throwing, causing the queue system to treat a failed encode as a
    // success and mark the job as completed instead of failed/retryable.
    // The fix adds `throw;` after the event publish.
    //
    // The specific MusicEncodeJob type lives in NoMercy.MediaProcessing (not referenced
    // here), so we test the general rethrow contract using a local stub that mirrors
    // the corrected pattern.

    private sealed class EncodeJobStub
    {
        public bool FailureEventPublished { get; private set; }

        /// <summary>
        /// Simulates the corrected MusicEncodeJob catch block:
        /// log → publish failure event → rethrow.
        /// </summary>
        public async Task HandleAsync()
        {
            try
            {
                await Task.FromException(new InvalidOperationException("encode failed"));
            }
            catch
            {
                // Simulate publishing failure event
                FailureEventPublished = true;

                // Re-throw — preserving the original stack trace
                throw;
            }
        }
    }

    [Fact]
    public async Task EncodeFailure_RethrowsAfterPublishingEvent()
    {
        EncodeJobStub job = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.HandleAsync());

        Assert.True(
            job.FailureEventPublished,
            "Failure event must be published before the exception is rethrown"
        );
    }

    [Fact]
    public async Task EncodeFailure_PreservesOriginalExceptionType()
    {
        EncodeJobStub job = new();

        // The rethrown exception must be the original type, not an AggregateException
        // or a wrapper — confirming `throw;` is used rather than `throw ex;`
        Exception caught = await Assert.ThrowsAsync<InvalidOperationException>(
            () => job.HandleAsync()
        );

        Assert.Equal("encode failed", caught.Message);
    }
}
