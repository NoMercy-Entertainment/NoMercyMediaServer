# Encoder V3 Reliability — Filter Graph, Scaling, Integration Tests

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the V3 encoder handle real-world encodes — scaling, multi-output, HDR tonemapping — and prove it with integration tests.

**Architecture:** The 6-stage pipeline (Analyze→Validate→Plan→Build→Execute→Finalize) already has all the components (`FilterGraphBuilder`, `FfmpegCommandBuilder.WithFilterComplex()`, scale/split/tonemap filters). The `BuildStage` just never generates the filter graph when `PlanStage` outputs filter labels (`[v0]`, `[v1]`). Fix is surgical: `BuildStage` inspects the `OutputPlan`'s `MapLabel` fields — if any use filter syntax `[...]`, build the filter graph from the video output dimensions + source `MediaInfo`. Then integration tests prove each scenario works with real FFmpeg.

**Tech Stack:** C# .NET 10, xUnit, FluentAssertions, FFmpeg (system PATH for tests, bundled `nomercy-ffmpeg` in prod)

---

### File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `src/NoMercy.Encoder/Pipeline/Stages/BuildStage.cs` | Modify | Generate `filter_complex` when filter labels are present |
| `src/NoMercy.Encoder/Pipeline/Stages/PlanStage.cs` | Modify | Always use filter labels (remove the passthrough special case — let BuildStage decide) |
| `tests/NoMercy.Tests.Encoder/Integration/RealEncodeTests.cs` | Modify | Add scaling, multi-output, and profile deserialization tests |
| `tests/NoMercy.Tests.Encoder/Pipeline/Stages/BuildStageFilterGraphTests.cs` | Create | Unit tests for filter graph generation |
| `src/NoMercy.Encoder/Composition/EncoderProvider.cs` | — | Already volatile, no change needed |
| `src/NoMercy.MediaProcessing/Jobs/MediaJobs/EventBusProgressObserver.cs` | — | Already correct, tested via integration |

---

### Task 1: Fix PlanStage — always use filter labels for video

The passthrough special case (`0:v:0`) added earlier creates a fork in logic. Simpler: always use `[v0]` filter labels and let BuildStage generate the appropriate filter graph (passthrough = `[0:v:0]copy[v0]`, scale = `[0:v:0]scale=W:-2[v0]`).

**Files:**
- Modify: `src/NoMercy.Encoder/Pipeline/Stages/PlanStage.cs:97-105`

- [ ] **Step 1: Write failing test**

```csharp
// tests/NoMercy.Tests.Encoder/Pipeline/Stages/PlanStageMapLabelTests.cs
[Fact]
public void BuildOutputPlan_SingleVideoSameResolution_UsesFilterLabel()
{
    // Even when input matches output, MapLabel should be [v0] (not 0:v:0)
    // because BuildStage handles the filter graph generation
    // ... arrange with mocked media info where Width=1920, Height=1080
    // ... profile with Width=1920, Height=1080
    // Assert: videoPlan[0].MapLabel == "[v0]"
}
```

- [ ] **Step 2: Run test — expect FAIL** (currently returns `0:v:0` for same-resolution)

- [ ] **Step 3: Remove the `needsFilterGraph` conditional in PlanStage**

Replace lines 97-105 with:
```csharp
string mapLabel = $"[v{i}]";
```

- [ ] **Step 4: Run test — expect PASS**

- [ ] **Step 5: Run all encoder tests** to catch regressions

```bash
dotnet test tests/NoMercy.Tests.Encoder/ --no-restore -v q
```

- [ ] **Step 6: Commit**

```bash
git add src/NoMercy.Encoder/Pipeline/Stages/PlanStage.cs tests/...
git commit -m "refactor(encoder): always use filter labels in PlanStage"
```

---

### Task 2: BuildStage — generate filter_complex from OutputPlan

This is the core fix. When any `VideoOutputPlan.MapLabel` starts with `[`, build a `filter_complex` string using `FilterGraphBuilder`.

Logic:
1. If only 1 video output and no scaling needed → `[0:v:0]copy[v0]`
2. If only 1 video output but needs scaling → `[0:v:0]scale=W:-2[v0]`
3. If N video outputs → `[0:v:0]split=N[split0][split1]...;[split0]scale=W0:-2[v0];[split1]scale=W1:-2[v1]...`
4. If HDR tonemapping needed → insert tonemap before scale in the chain

The input video stream is always `0:v:0`. `context.MediaInfo` provides source dimensions.

**Files:**
- Modify: `src/NoMercy.Encoder/Pipeline/Stages/BuildStage.cs:37-41`

- [ ] **Step 1: Write unit test for single-output scaling**

```csharp
// tests/NoMercy.Tests.Encoder/Pipeline/Stages/BuildStageFilterGraphTests.cs
[Fact]
public void BuildStage_SingleVideoWithScaling_GeneratesFilterComplex()
{
    // Source: 1920x1080, Output: 1280x720, MapLabel: [v0]
    // Expected filter_complex: [0:v:0]scale=1280:-2[v0]
    // Verify the FfmpegCommand arguments contain "-filter_complex" with the scale filter
}
```

- [ ] **Step 2: Write unit test for multi-output split+scale**

```csharp
[Fact]
public void BuildStage_MultipleVideos_GeneratesSplitAndScale()
{
    // Source: 1920x1080, Outputs: 1920x1080 [v0], 1280x720 [v1]
    // Expected: [0:v:0]split=2[split0][split1];[split0]scale=1920:-2[v0];[split1]scale=1280:-2[v1]
}
```

- [ ] **Step 3: Write unit test for passthrough (same resolution, single output)**

```csharp
[Fact]
public void BuildStage_SingleVideoSameResolution_GeneratesCopyFilter()
{
    // Source: 320x180, Output: 320x180, MapLabel: [v0]
    // Expected: [0:v:0]copy[v0]
}
```

- [ ] **Step 4: Run tests — expect all FAIL**

- [ ] **Step 5: Implement filter graph generation in BuildStage**

In `BuildStage.ExecuteAsync()`, after creating the builder and before `strategy.ConfigureOutput()`:

```csharp
// Build filter_complex when video outputs use filter labels
string? filterGraph = BuildFilterGraph(
    input.Plan.OutputPlan, context.MediaInfo
);
if (filterGraph is not null)
{
    builder.WithFilterComplex(filterGraph);
}
```

Add private method:

```csharp
private static string? BuildFilterGraph(OutputPlan plan, MediaInfo? mediaInfo)
{
    VideoOutputPlan[] videoOutputs = plan.VideoOutputs;
    if (videoOutputs.Length == 0)
        return null;

    // Only generate filter graph if labels use filter syntax [vN]
    bool needsFilterGraph = videoOutputs.Any(v => v.MapLabel.StartsWith('['));
    if (!needsFilterGraph)
        return null;

    FilterGraphBuilder fg = new();
    int sourceWidth = mediaInfo?.VideoStreams[0].Width ?? 0;
    int sourceHeight = mediaInfo?.VideoStreams[0].Height ?? 0;

    if (videoOutputs.Length == 1)
    {
        VideoOutputPlan v = videoOutputs[0];
        string outputLabel = v.MapLabel.Trim('[', ']');

        if (v.Width == sourceWidth && v.Height == sourceHeight)
        {
            fg.AddFilter("0:v:0", "copy", outputLabel);
        }
        else
        {
            fg.AddScaleWidth("0:v:0", v.Width, outputLabel);
        }
    }
    else
    {
        // Split input into N branches, scale each
        string[] splitLabels = videoOutputs
            .Select((_, i) => $"split{i}")
            .ToArray();
        fg.AddSplit("0:v:0", splitLabels);

        for (int i = 0; i < videoOutputs.Length; i++)
        {
            VideoOutputPlan v = videoOutputs[i];
            string outputLabel = v.MapLabel.Trim('[', ']');

            if (v.Width == sourceWidth && v.Height == sourceHeight)
            {
                fg.AddFilter($"split{i}", "copy", outputLabel);
            }
            else
            {
                fg.AddScaleWidth($"split{i}", v.Width, outputLabel);
            }
        }
    }

    string graph = fg.Build();
    return graph.Length > 0 ? graph : null;
}
```

- [ ] **Step 6: Run unit tests — expect PASS**

- [ ] **Step 7: Run all encoder tests**

```bash
dotnet test tests/NoMercy.Tests.Encoder/ --no-restore -v q
```

- [ ] **Step 8: CSharpier format**

```bash
dotnet csharpier format src/NoMercy.Encoder/Pipeline/Stages/BuildStage.cs
```

- [ ] **Step 9: Commit**

```bash
git commit -m "feat(encoder): generate filter_complex in BuildStage for scaling and multi-output"
```

---

### Task 3: Integration test — scaling encode (source ≠ output resolution)

Proves the full pipeline works when the input is 320x180 but the profile requests 160x90.

**Files:**
- Modify: `tests/NoMercy.Tests.Encoder/Integration/RealEncodeTests.cs`

- [ ] **Step 1: Add test method**

```csharp
[Fact]
public async Task EncodeAsync_ScalingProfile_ProducesCorrectResolution()
{
    // Input: 320x180 (from test clip)
    // Profile: 160x90 HLS
    // Assert: encoding succeeds, output dir has video_160x90/ with playlist + segments
}
```

- [ ] **Step 2: Run — expect PASS** (if BuildStage filter graph is correct)

- [ ] **Step 3: Commit**

---

### Task 4: Integration test — multi-output encode (2 video variants)

Proves split+scale works with real FFmpeg.

**Files:**
- Modify: `tests/NoMercy.Tests.Encoder/Integration/RealEncodeTests.cs`

- [ ] **Step 1: Add test method**

```csharp
[Fact]
public async Task EncodeAsync_MultiOutputProfile_ProducesMultipleVariants()
{
    // Input: 320x180
    // Profile: 2 video outputs — 320x180 (passthrough) + 160x90 (downscale)
    // Assert: both video_320x180/ and video_160x90/ directories with playlists
}
```

- [ ] **Step 2: Run — expect PASS**

- [ ] **Step 3: Commit**

---

### Task 5: Integration test — seed profile deserialization roundtrip

Proves that the V3 JSON in seed profiles actually deserializes and produces a valid encode request.

**Files:**
- Modify: `tests/NoMercy.Tests.Encoder/Integration/RealEncodeTests.cs`

- [ ] **Step 1: Add test method**

```csharp
[Fact]
public void SeedProfiles_DeserializeToValidEncodingProfiles()
{
    // Get all seed profiles from EncoderProfileSeedData
    // For each profile with non-null Param:
    //   Deserialize Param → EncodingProfile
    //   Assert not null, has Name, has at least one audio or video output
}
```

- [ ] **Step 2: Run — expect PASS**

- [ ] **Step 3: Commit**

---

### Task 6: Unit test — EventBusProgressObserver

**Files:**
- Create: `tests/NoMercy.Tests.Encoder/Integration/EventBusProgressObserverTests.cs`

- [ ] **Step 1: Write tests**

```csharp
[Fact]
public void OnProgress_WhenEventBusConfigured_PublishesEvent()
{
    // Configure EventBusProvider with a mock event bus
    // Create observer, call OnProgress
    // Verify EncodingProgressEvent was published
}

[Fact]
public void OnProgress_WhenEventBusNotConfigured_DoesNotThrow()
{
    // Don't configure EventBusProvider
    // Create observer, call OnProgress
    // Assert no exception
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Tests should pass against existing implementation** (no code changes needed, just verification)

- [ ] **Step 4: Commit**

---

### Task 7: Unit test — EncoderProvider

**Files:**
- Create: `tests/NoMercy.Tests.Encoder/Composition/EncoderProviderTests.cs`

- [ ] **Step 1: Write tests**

```csharp
[Fact]
public void Resolve_WhenNotConfigured_ThrowsInvalidOperation()

[Fact]
public void Resolve_WhenConfigured_ReturnsEncoderFromFactory()

[Fact]
public void IsConfigured_BeforeConfigure_ReturnsFalse()

[Fact]
public void Configure_WithNull_ThrowsArgumentNull()
```

- [ ] **Step 2: Implement and run**

- [ ] **Step 3: Commit**

---

### Task 8: Full verification

- [ ] **Step 1: Run ALL encoder tests**

```bash
dotnet test tests/NoMercy.Tests.Encoder/ --no-restore -v q
```
Expected: all pass (target: 590+)

- [ ] **Step 2: Run full solution build**

```bash
dotnet build --no-restore -v q
```
Expected: 0 errors

- [ ] **Step 3: Verify zero encoder-v3 TODOs**

```bash
grep -rn "TODO(encoder-v3)" src/ --include="*.cs"
```
Expected: 0 results

- [ ] **Step 4: Final commit and push**

```bash
git push
```
