# NoMercy Video Encoder System - MCP Documentation

## Coding Style Rules
- Use explicit types throughout the codebase
- Follow target-typed new expressions pattern (e.g., `List<TaskDelegate> startupTasks = [];`)
- Maintain proper async/await patterns
- Use clear namespace organization
- Implement dependency injection consistently
- Follow existing constructor parameters and explicit parameter types
- **ALWAYS ask before making drastic file changes**
- **Use PowerShell commands for all terminal operations**
- **Never change directory when working with C# code (only for widget/dashboard work)**
- **Automatically add rules/preferences to this document when user mentions them (without being asked)**
- **Globals project is the top-most project and cannot depend on any other projects - only used for things that are used (or potentially used) by other projects**
- **Convert into 'using' declaration when creating disposable resources for cleaner code**
- **Properly check for required using statements - ensure all necessary imports are included**

## System Overview
The NoMercy Encoder is a complex FFmpeg wrapper system with the following architecture:

```
NoMercy.Encoder/
├── Core/                    # Core functionality and utilities
│   ├── VideoAudioFile.cs   # Main encoder orchestration
│   ├── FfMpeg.cs          # FFmpeg execution wrapper
│   ├── Progress.cs        # Progress tracking
│   └── IsoLanguageMapper.cs # Language handling
├── Format/                 # Format-specific implementations
│   ├── Video/             # Video encoders (H264, H265, VP9, AV1)
│   ├── Audio/             # Audio encoders
│   ├── Subtitle/          # Subtitle handling
│   ├── Image/             # Image processing (sprites, thumbnails)
│   └── Rules/             # Shared rules and constants
└── Container/             # Output container handling
```

## DEEP ANALYSIS: Complexity Issues Found

After tracing through `EncodeVideoJob.Handle()`, I've identified severe architectural problems that make the system overly complex and bug-prone:

### 1. **CRITICAL: Tangled Responsibilities**

**Current Flow Analysis:**
```
EncodeVideoJob.Handle() →
├── BuildVideoStreams() (static helper)
├── BuildAudioStreams() (static helper) 
├── BuildSubtitleStreams() (static helper)
└── VideoAudioFile.AddContainer() →
    ├── CropDetect() (embedded FFmpeg call)
    ├── Complex stream assignment logic
    ├── Scale calculation overrides (3 different places!)
    └── GetFullCommand() (500+ line method!)
```

**Problems:**
- Single methods doing 5+ different things
- Stream building scattered across multiple classes
- Scale calculation happens in 3 different locations
- Command generation is a massive 500+ line method
- Static helper methods mixed with instance methods

### 2. **CRITICAL: Broken Abstraction Layers**

**Current Structure Issues:**
- `Classes.cs` - Base class doing everything (crop, scale, parameters, containers)
- `BaseVideo.cs` - Video-specific logic mixed with generic stream logic  
- `VideoAudioFile.cs` - Handles both audio AND video AND images AND subtitles
- `BaseContainer.cs` - Container logic mixed with codec logic

**Result:** No clear separation of concerns, everything depends on everything else.

### 3. **HIGH: Multiple Scale Calculation Points (Your Bug Source!)**

Found **4 different places** where scaling gets calculated/overridden:

1. **Profile Stage** (`BuildVideoStreams()`):
   ```csharp
   .SetScale(profile.Width, profile.Height)
   ```

2. **Stream Building** (`VideoAudioFile.AddContainer()`):
   ```csharp
   if (stream.Scale.H == 0)
       stream.Scale = new() { W = ..., H = stream.Scale.W * aspect };
   ```

3. **Command Generation** (`GetFullCommand()` line 140):
   ```csharp
   if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
       stream.Scale = new() { W = stream.VideoStream.Width, H = stream.VideoStream.Height };
   ```

4. **Later in Command Generation** (line 280):
   ```csharp
   if (stream.Scale.W < stream.VideoStream.Width * 0.95 && stream.Scale.H < stream.VideoStream.Height * 0.95)
       stream.Scale = new() { W = stream.VideoStream.Width, H = stream.VideoStream.Height };
   ```

**This is why you get unpredictable aspect ratios!**

### 4. **HIGH: Command Generation Nightmare**

The `GetFullCommand()` method is 500+ lines of spaghetti code that:
- Builds complex filter strings
- Handles 4 different stream types
- Does conditional logic for HDR, GPU, containers
- Mixes string building with business logic
- Has nested loops and complex conditionals

### 5. **MEDIUM: Inconsistent Error Handling**

- Some methods throw exceptions
- Some return null/empty
- Some log errors but continue
- No consistent validation strategy

### 6. **LOW: Poor Testability**

- Static methods everywhere
- Tight coupling to FFmpeg paths
- No dependency injection
- Complex object graphs make unit testing impossible

## RECOMMENDED RESTRUCTURING

### Phase 1: Immediate Fixes (1-2 days)

1. **Fix the Scale Calculation Bug**:
   ```csharp
   // Create single ScaleCalculator class
   public class ScaleCalculator
   {
       public ScaleArea Calculate(VideoProfile profile, VideoStream sourceStream, CropArea? crop = null)
       {
           // Single source of truth for all scale calculations
           // Proper aspect ratio handling
           // Validation logic
       }
   }
   ```

2. **Extract Command Builder**:
   ```csharp
   public class FFmpegCommandBuilder
   {
       public string BuildCommand(EncodingContext context)
       {
           // Replace the 500-line method with focused builder
       }
   }
   ```

### Phase 2: Architectural Restructure (1 week)

#### New Structure:
```
NoMercy.Encoder/
├── Abstractions/           # Interfaces and contracts
│   ├── IEncoder.cs
│   ├── IStreamProcessor.cs
│   └── ICommandBuilder.cs
├── Core/                   # Core business logic
│   ├── EncodingContext.cs  # Single source of truth
│   ├── ScaleCalculator.cs  # Centralized scaling
│   └── StreamOrchestrator.cs
├── Processors/             # Stream-specific processors
│   ├── VideoProcessor.cs
│   ├── AudioProcessor.cs
│   ├── SubtitleProcessor.cs
│   └── ImageProcessor.cs
├── Commands/               # Command generation
│   ├── FFmpegCommandBuilder.cs
│   └── FilterBuilder.cs
└── Configuration/          # Profiles and settings
    ├── EncodingProfile.cs
    └── QualityPresets.cs
```

#### Key Classes:

**EncodingContext.cs** - Single source of truth:
```csharp
public class EncodingContext
{
    public MediaAnalysis SourceMedia { get; set; }
    public List<EncodingProfile> Profiles { get; set; }
    public ScaleArea TargetScale { get; set; }
    public CropArea DetectedCrop { get; set; }
    public string OutputPath { get; set; }
    // All encoding state in one place
}
```

**StreamOrchestrator.cs** - Replace current chaos:
```csharp
public class StreamOrchestrator
{
    private readonly IVideoProcessor videoProcessor;
    private readonly IAudioProcessor audioProcessor;
    private readonly ISubtitleProcessor subtitleProcessor;
    
    public EncodingResult Process(EncodingContext context)
    {
        // Clean, testable orchestration
        // Single responsibility
        // Proper error handling
    }
}
```

### Phase 3: Quality Improvements (2-3 days)

1. **Add Comprehensive Testing**:
   - Unit tests for scale calculations
   - Integration tests for command building  
   - Regression tests for aspect ratio issues

2. **Add Validation Layer**:
   - Input validation
   - Output validation
   - Dimension bounds checking

3. **Improve Logging**:
   - Structured logging
   - Debug tracing for scale calculations
   - Performance metrics

## SPECIFIC BUG FIXES NEEDED

### 1. **Scale Calculation Consolidation** 
- Remove all scale override logic from `GetFullCommand()` 
- Create single `ScaleCalculator.Calculate()` method
- Use this method ONCE during stream building

### 2. **Aspect Ratio Fix** ❌ (NOT FIXED - Still Critical)
- **CRITICAL BUG STILL EXISTS** in `Classes.cs` line 57: `internal double AspectRatioValue => Crop.H / Crop.W;`
- This is still doing integer division, causing aspect ratio to always be 0 or 1
- **THIS IS THE ROOT CAUSE** of your 480p wide aspect ratio bug
- Must be fixed to: `internal double AspectRatioValue => (double)Crop.H / (double)Crop.W;`

### 3. **Scale Chaos in VideoAudioFile.GetFullCommand()** ❌ (CRITICAL - Currently Active)
Found the **EXACT LOCATIONS** where your scaling gets broken:

**Scale Override #1** (line 140-148):
```csharp
if (stream.Scale.H == 0)
    stream.Scale = new()
    {
        W = stream.VideoStream?.Width ?? fMediaAnalysis.PrimaryVideoStream!.Width,
        H = stream.Scale.W * (fMediaAnalysis.PrimaryVideoStream!.Height / fMediaAnalysis.PrimaryVideoStream!.Width)  // INTEGER DIVISION AGAIN!
    };
```

**Scale Override #2** (line 150-156):
```csharp
if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
    stream.Scale = new()
    {
        W = stream.VideoStream.Width,
        H = stream.VideoStream.Height
    };
```

**Scale Override #3** (line 269-275):
```csharp
if (stream.Scale.W > stream.VideoStream!.Width || stream.Scale.H > stream.VideoStream.Height)
{
    stream.Scale.W = stream.VideoStream.Width;
    stream.Scale.H = stream.VideoStream.Height;
}
```

**Scale Override #4** (line 278-283):
```csharp
if (stream.Scale.W < stream.VideoStream.Width * 0.95 && stream.Scale.H < stream.VideoStream.Height * 0.95)
{
    stream.Scale.W = stream.VideoStream.Width;
    stream.Scale.H = stream.VideoStream.Height;
}
```

**THESE 4+ SCALE OVERRIDES ARE WHY YOUR 480P GETS WEIRD ASPECT RATIOS!**

### 4. **Command Generation Nightmare** ❌ (Needs Complete Rewrite)
- `GetFullCommand()` is 400+ lines doing everything
- Builds complex filter strings inline 
- Handles video/audio/subtitle/image streams all in one method
- Multiple nested loops with complex conditionals
- No separation of concerns

### 5. **Stream Building Inconsistency** ❌ (Architectural Problem)
- Static helper methods in `EncodeVideoJob` 
- Instance methods in `VideoAudioFile`
- No dependency injection
- Complex object graph mutations

## TESTING STRATEGY

### Critical Test Cases:
1. **480p Encoding** (your bug):
   - Various source aspect ratios (16:9, 4:3, 21:9)
   - Different crop scenarios
   - Profile override scenarios

2. **Scale Override Logic**:
   - Upscaling prevention
   - Downscaling thresholds
   - Aspect ratio preservation

3. **Complex Filter Chains**:
   - HDR to SDR conversion
   - Cropping with scaling
   - Multiple output formats

### Debug Tools:
```csharp
// Add to ScaleCalculator
public class ScaleDebugInfo
{
    public ScaleArea Original { get; set; }
    public ScaleArea AfterProfile { get; set; }
    public ScaleArea AfterCrop { get; set; }
    public ScaleArea Final { get; set; }
    public string[] AppliedRules { get; set; }
}
```

## IMPLEMENTATION ROADMAP

### Week 1: Emergency Fixes
- [x] Fix aspect ratio integer division bug
- [ ] Extract ScaleCalculator class
- [ ] Add scale calculation logging
- [ ] Test with problematic 480p video

### Week 2: Command Builder Extraction
- [ ] Create FFmpegCommandBuilder
- [ ] Extract filter building logic
- [ ] Simplify GetFullCommand()
- [ ] Add command validation

### Week 3: Stream Processing Restructure
- [ ] Create stream processor interfaces
- [ ] Implement VideoProcessor, AudioProcessor, etc.
- [ ] Replace static helper methods
- [ ] Add dependency injection

### Week 4: Testing & Validation
- [ ] Add comprehensive unit tests
- [ ] Create integration test suite
- [ ] Add performance benchmarks
- [ ] Documentation updates

## COLLABORATION NOTES

### Current Pain Points for Development:
1. **Debugging is nightmare** - too many places to check for scale issues
2. **Adding new formats is complex** - requires changes in 5+ files
3. **Testing is impossible** - can't mock or isolate components
4. **Performance is poor** - multiple FFmpeg calls, inefficient string building

### After Restructure Benefits:
1. **Single responsibility** - each class does one thing well
2. **Testable** - can unit test individual components
3. **Extensible** - easy to add new codecs/formats
4. **Debuggable** - clear flow and single points of logic
5. **Maintainable** - changes isolated to specific areas

### Key Questions for Next Steps:
1. Do you want to tackle the scale bug first, or start with architectural changes?
2. Are there other specific encoding issues you've noticed?
3. What's your priority: stability fixes or long-term architecture?
4. Do you have a test video that reproduces the 480p aspect ratio bug?

---
*This analysis shows your encoder system has grown organically and needs strategic refactoring. The aspect ratio bug is just a symptom of deeper architectural issues.*
