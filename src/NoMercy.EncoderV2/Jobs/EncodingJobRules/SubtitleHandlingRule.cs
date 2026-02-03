namespace NoMercy.EncoderV2.Jobs.EncodingJobRules;

/// <summary>
/// Handles subtitle-specific processing rules based on codec and format.
/// 
/// Rules:
/// 1. ASS subtitles: Extract embedded fonts from source, embed in MKV output
/// 2. Image-based subtitles (PGS/DVDSUB): Convert to WebVTT via OCR
/// 3. Subtitle language selection: Respect AllowedLanguages from profile
/// 4. Subtitle copying: Pass through without encoding when format matches
/// </summary>
public class SubtitleHandlingRule : IEncodingJobRule
{
    public string RuleName => "Subtitle Handling";

    public bool AppliesToJob(EncodingJobPayload job)
    {
        // Applies to any job with subtitles to process
        return job.Profile?.SubtitleProfile != null;
    }

    public Task<List<PostProcessingAction>> GetPostProcessingActionsAsync(EncodingJobPayload job)
    {
        List<PostProcessingAction> actions = new();

        if (job.Profile?.SubtitleProfile is null)
            return Task.FromResult(actions);

        SubtitleProfileConfig? subProfile = job.Profile.SubtitleProfile;

        // Rule 1: ASS subtitles - extract fonts if embedded in source
        if (subProfile.Codec == "ass")
        {
            actions.Add(new()
            {
                ActionType = "font-extraction",
                DisplayName = "Extracting fonts from subtitles",
                InputPath = job.Input.FilePath,
                OutputPath = Path.Combine(job.Output.DestinationFolder, "fonts"),
                Format = "ass",
                Metadata = new()
                {
                    { "extract_all", true },
                    { "command", "-dump_attachment:t \"\" -i \"{input}\" -y -hide_banner -t 0 -f null null" }
                }
            });
        }

        // Rule 2: Image-based subtitles - convert to WebVTT
        // PGS (hdmv_pgs_subtitle) and DVDSUB (dvd_subtitle) require OCR
        actions.Add(new()
        {
            ActionType = "subtitle-conversion",
            DisplayName = "Converting subtitles to standard format",
            Format = subProfile.Codec,
            Metadata = new()
            {
                // Image-based subtitles that need OCR
                { "image_codecs", new[] { "hdmv_pgs_subtitle", "dvd_subtitle", "xsub" } },
                // Standard text subtitles that need remuxing
                { "text_codecs", new[] { "ass", "ssa", "subrip", "srt", "webvtt" } },
                // Conversion rules by codec
                { "conversions", new Dictionary<string, string>
                {
                    { "hdmv_pgs_subtitle", "webvtt-via-ocr" },
                    { "dvd_subtitle", "webvtt-via-ocr" },
                    { "xsub", "webvtt-via-ocr" },
                    { "ass", "ass" },          // Keep as-is if target is MKV
                    { "srt", "webvtt" },       // SRT to WebVTT for HLS
                    { "subrip", "webvtt" },
                    { "webvtt", "webvtt" }
                } }
            }
        });

        // Rule 3: Language filtering
        if (subProfile.AllowedLanguages.Count > 0)
        {
            actions.Add(new()
            {
                ActionType = "language-filtering",
                DisplayName = "Applying language filters to subtitles",
                Metadata = new()
                {
                    { "allowed_languages", string.Join(",", subProfile.AllowedLanguages) },
                    { "fallback_to_default", true },
                    { "default_language", "eng" }
                }
            });
        }

        return Task.FromResult(actions);
    }
}
