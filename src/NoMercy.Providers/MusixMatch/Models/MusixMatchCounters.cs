using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchCounters
{
    [JsonProperty("track_translation")] public int TrackTranslation;
    [JsonProperty("lyrics_missing")] public int LyricsMissing;
    [JsonProperty("lyrics_ok")] public int LyricsOk;
    [JsonProperty("lyrics_ko")] public int LyricsKo;
    [JsonProperty("lyrics_changed")] public int LyricsChanged;
    [JsonProperty("vote_bonuses")] public int VoteBonuses;
    [JsonProperty("translation_ok")] public int TranslationOk;

    [JsonProperty("track_influencer_bonus_moderator_vote")]
    public int TrackInfluencerBonusModeratorVote;

    [JsonProperty("lyrics_favourite_added")]
    public int LyricsFavouriteAdded;

    [JsonProperty("lyrics_ai_phrases_not_related_no")]
    public int LyricsAiPhrasesNotRelatedNo;

    [JsonProperty("lyrics_report_contain_mistakes")]
    public int LyricsReportContainMistakes;

    [JsonProperty("lyrics_subtitle_added")]
    public int LyricsSubtitleAdded;

    [JsonProperty("lyrics_music_id")] public int LyricsMusicId;

    [JsonProperty("lyrics_ai_phrases_not_related_yes")]
    public int LyricsAiPhrasesNotRelatedYes;

    [JsonProperty("lyrics_report_incomplete_lyrics")]
    public int LyricsReportIncompleteLyrics;

    [JsonProperty("lyrics_ai_phrases_not_related_skip")]
    public int LyricsAiPhrasesNotRelatedSkip;

    [JsonProperty("lyrics_report_completely_wrong")]
    public int LyricsReportCompletelyWrong;

    [JsonProperty("lyrics_implicitly_ok")] public int LyricsImplicitlyOk;
    [JsonProperty("vote_maluses")] public int VoteMaluses;

    [JsonProperty("lyrics_richsync_added")]
    public int LyricsRichsyncAdded;

    [JsonProperty("lyrics_ranking_change")]
    public int LyricsRankingChange;

    [JsonProperty("lyrics_ai_mood_analysis_v3_value")]
    public int LyricsAiMoodAnalysisV3Value;

    [JsonProperty("lyrics_ai_ugc_language")]
    public int LyricsAiUgcLanguage;

    [JsonProperty("track_structure")] public int TrackStructure;

    [JsonProperty("track_complete_metadata")]
    public int TrackCompleteMetadata;
}