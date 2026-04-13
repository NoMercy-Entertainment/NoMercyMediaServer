// TODO(encoder-v3): CleanupPartialOutput was a V1 VideoEncodeJob method that depended on
// BaseContainer from NoMercy.Encoder.Format. It was removed in the V3 rewrite.
// These tests need to be rewritten once the V3 encoder's output cleanup contract is defined.

namespace NoMercy.Tests.MediaProcessing.Jobs;

public class VideoEncodeJobCleanupTests
{
    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_RemovesOnlyReportedSubdirectory()
    {
        Assert.True(true);
    }

    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_RemovesAllReportedSubdirectories()
    {
        Assert.True(true);
    }

    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_ReportedSubdirDoesNotExist_DoesNotThrow()
    {
        Assert.True(true);
    }

    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_NestedContentInSubdir_RemovedRecursively()
    {
        Assert.True(true);
    }

    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_SingleFileContainer_RemovesOutputFile()
    {
        Assert.True(true);
    }

    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_SingleFileContainer_FileMissing_DoesNotThrow()
    {
        Assert.True(true);
    }

    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_EmptyContainer_LeavesDirectoryUntouched()
    {
        Assert.True(true);
    }

    [Fact(
        Skip = "encoder-v3: CleanupPartialOutput removed — tests need rewrite for V3 output contract"
    )]
    public void CleanupPartialOutput_NonExistentBasePath_DoesNotThrow()
    {
        Assert.True(true);
    }
}
