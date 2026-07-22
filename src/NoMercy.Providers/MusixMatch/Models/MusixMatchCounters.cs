// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Newtonsoft.Json;

namespace NoMercy.Providers.MusixMatch.Models;

public class MusixMatchCounters
{
    [JsonProperty(propertyName: "track_translation")]
    public int TrackTranslation;

    [JsonProperty(propertyName: "lyrics_missing")]
    public int LyricsMissing;

    [JsonProperty(propertyName: "lyrics_ok")]
    public int LyricsOk;

    [JsonProperty(propertyName: "lyrics_ko")]
    public int LyricsKo;

    [JsonProperty(propertyName: "lyrics_changed")]
    public int LyricsChanged;

    [JsonProperty(propertyName: "vote_bonuses")]
    public int VoteBonuses;

    [JsonProperty(propertyName: "translation_ok")]
    public int TranslationOk;

    [JsonProperty(propertyName: "track_influencer_bonus_moderator_vote")]
    public int TrackInfluencerBonusModeratorVote;

    [JsonProperty(propertyName: "lyrics_favourite_added")]
    public int LyricsFavouriteAdded;

    [JsonProperty(propertyName: "lyrics_ai_phrases_not_related_no")]
    public int LyricsAiPhrasesNotRelatedNo;

    [JsonProperty(propertyName: "lyrics_report_contain_mistakes")]
    public int LyricsReportContainMistakes;

    [JsonProperty(propertyName: "lyrics_subtitle_added")]
    public int LyricsSubtitleAdded;

    [JsonProperty(propertyName: "lyrics_music_id")]
    public int LyricsMusicId;

    [JsonProperty(propertyName: "lyrics_ai_phrases_not_related_yes")]
    public int LyricsAiPhrasesNotRelatedYes;

    [JsonProperty(propertyName: "lyrics_report_incomplete_lyrics")]
    public int LyricsReportIncompleteLyrics;

    [JsonProperty(propertyName: "lyrics_ai_phrases_not_related_skip")]
    public int LyricsAiPhrasesNotRelatedSkip;

    [JsonProperty(propertyName: "lyrics_report_completely_wrong")]
    public int LyricsReportCompletelyWrong;

    [JsonProperty(propertyName: "lyrics_implicitly_ok")]
    public int LyricsImplicitlyOk;

    [JsonProperty(propertyName: "vote_maluses")]
    public int VoteMaluses;

    [JsonProperty(propertyName: "lyrics_richsync_added")]
    public int LyricsRichsyncAdded;

    [JsonProperty(propertyName: "lyrics_ranking_change")]
    public int LyricsRankingChange;

    [JsonProperty(propertyName: "lyrics_ai_mood_analysis_v3_value")]
    public int LyricsAiMoodAnalysisV3Value;

    [JsonProperty(propertyName: "lyrics_ai_ugc_language")]
    public int LyricsAiUgcLanguage;

    [JsonProperty(propertyName: "track_structure")]
    public int TrackStructure;

    [JsonProperty(propertyName: "track_complete_metadata")]
    public int TrackCompleteMetadata;
}
