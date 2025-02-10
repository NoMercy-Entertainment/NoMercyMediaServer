using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;
public class TadbArtist
{
    [JsonProperty("idArtist")] public string? IdArtist { get; set; }
    [JsonProperty("strArtist")] public string? StrArtist { get; set; }
    [JsonProperty("strArtistStripped")] public string? StrArtistStripped { get; set; }
    [JsonProperty("strArtistAlternate")] public string? StrArtistAlternate { get; set; }
    [JsonProperty("strLabel")] public string? StrLabel { get; set; }
    [JsonProperty("idLabel")] public string? IdLabel { get; set; }
    [JsonProperty("intFormedYear")] public string? IntFormedYear { get; set; }
    [JsonProperty("intBornYear")] public string? IntBornYear { get; set; }
    [JsonProperty("intDiedYear")] public string? IntDiedYear { get; set; }
    [JsonProperty("strDisbanded")] public string? StrDisbanded { get; set; }
    [JsonProperty("strStyle")] public string? StrStyle { get; set; }
    [JsonProperty("strGenre")] public string? StrGenre { get; set; }
    [JsonProperty("strMood")] public string? StrMood { get; set; }
    [JsonProperty("strWebsite")] public string? StrWebsite { get; set; }
    [JsonProperty("strFacebook")] public string? StrFacebook { get; set; }
    [JsonProperty("strTwitter")] public string? StrTwitter { get; set; }
    [JsonProperty("strBiographyEN")] public string? StrBiographyEn { get; set; }
    [JsonProperty("strBiographyDE")] public string? StrBiographyDe { get; set; }
    [JsonProperty("strBiographyFR")] public string? StrBiographyFr { get; set; }
    [JsonProperty("strBiographyCN")] public string? StrBiographyCn { get; set; }
    [JsonProperty("strBiographyIT")] public string? StrBiographyIt { get; set; }
    [JsonProperty("strBiographyJP")] public string? StrBiographyJp { get; set; }
    [JsonProperty("strBiographyRU")] public string? StrBiographyRu { get; set; }
    [JsonProperty("strBiographyES")] public string? StrBiographyEs { get; set; }
    [JsonProperty("strBiographyPT")] public string? StrBiographyPt { get; set; }
    [JsonProperty("strBiographySE")] public string? StrBiographySe { get; set; }
    [JsonProperty("strBiographyNL")] public string? StrBiographyNl { get; set; }
    [JsonProperty("strBiographyHU")] public string? StrBiographyHu { get; set; }
    [JsonProperty("strBiographyNO")] public string? StrBiographyNo { get; set; }
    [JsonProperty("strBiographyIL")] public string? StrBiographyIl { get; set; }
    [JsonProperty("strBiographyPL")] public string? StrBiographyPl { get; set; }
    [JsonProperty("strGender")] public string? StrGender { get; set; }
    [JsonProperty("intMembers")] public string? IntMembers { get; set; }
    [JsonProperty("strCountry")] public string? StrCountry { get; set; }
    [JsonProperty("strCountryCode")] public string? StrCountryCode { get; set; }
    [JsonProperty("strArtistThumb")] public string? StrArtistThumb { get; set; }
    [JsonProperty("strArtistLogo")] public string? StrArtistLogo { get; set; }
    [JsonProperty("strArtistCutout")] public string? StrArtistCutout { get; set; }
    [JsonProperty("strArtistClearart")] public string? StrArtistClearart { get; set; }
    [JsonProperty("strArtistWideThumb")] public string? StrArtistWideThumb { get; set; }
    [JsonProperty("strArtistFanart")] public string? StrArtistFanart { get; set; }
    [JsonProperty("strArtistFanart2")] public string? StrArtistFanart2 { get; set; }
    [JsonProperty("strArtistFanart3")] public string? StrArtistFanart3 { get; set; }
    [JsonProperty("strArtistFanart4")] public string? StrArtistFanart4 { get; set; }
    [JsonProperty("strArtistBanner")] public string? StrArtistBanner { get; set; }
    [JsonProperty("strMusicBrainzID")] public string? StrMusicBrainzId { get; set; }
    [JsonProperty("strISNIcode")] public string? StrIsnIcode { get; set; }
    [JsonProperty("strLastFMChart")] public string? StrLastFmChart { get; set; }
    [JsonProperty("intCharted")] public string? IntCharted { get; set; }
    [JsonProperty("strLocked")] public string? StrLocked { get; set; }

    [JsonProperty("descriptions")]
    public List<TadbLanguageDescription> Descriptions
    {
        get
        {
            List<TadbLanguageDescription> descriptions = new();
            if (StrBiographyCn != null)
                descriptions.Add(new() { Iso31661 = "CN", Description = StrBiographyCn });
            if (StrBiographyDe != null)
                descriptions.Add(new() { Iso31661 = "DE", Description = StrBiographyDe });
            if (StrBiographyEn != null)
                descriptions.Add(new() { Iso31661 = "EN", Description = StrBiographyEn });
            if (StrBiographyEs != null)
                descriptions.Add(new() { Iso31661 = "ES", Description = StrBiographyEs });
            if (StrBiographyFr != null)
                descriptions.Add(new() { Iso31661 = "FR", Description = StrBiographyFr });
            if (StrBiographyHu != null)
                descriptions.Add(new() { Iso31661 = "HU", Description = StrBiographyHu });
            if (StrBiographyIl != null)
                descriptions.Add(new() { Iso31661 = "IL", Description = StrBiographyIl });
            if (StrBiographyIt != null)
                descriptions.Add(new() { Iso31661 = "IT", Description = StrBiographyIt });
            if (StrBiographyJp != null)
                descriptions.Add(new() { Iso31661 = "JP", Description = StrBiographyJp });
            if (StrBiographyNl != null)
                descriptions.Add(new() { Iso31661 = "NL", Description = StrBiographyNl });
            if (StrBiographyNo != null)
                descriptions.Add(new() { Iso31661 = "NO", Description = StrBiographyNo });
            if (StrBiographyPl != null)
                descriptions.Add(new() { Iso31661 = "PL", Description = StrBiographyPl });
            if (StrBiographyPt != null)
                descriptions.Add(new() { Iso31661 = "PT", Description = StrBiographyPt });
            if (StrBiographyRu != null)
                descriptions.Add(new() { Iso31661 = "RU", Description = StrBiographyRu });
            if (StrBiographySe != null)
                descriptions.Add(new() { Iso31661 = "SE", Description = StrBiographySe });

            return descriptions;
        }
    }
}
