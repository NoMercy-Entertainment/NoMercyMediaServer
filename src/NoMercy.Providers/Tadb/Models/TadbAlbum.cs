using Newtonsoft.Json;

namespace NoMercy.Providers.Tadb.Models;
public class TadbAlbum
{
    [JsonProperty("idAlbum")] public string IdAlbum { get; set; }
    [JsonProperty("idArtist")] public string IdArtist { get; set; }
    [JsonProperty("idLabel")] public string IdLabel { get; set; }
    [JsonProperty("strAlbum")] public string StrAlbum { get; set; }
    [JsonProperty("strAlbumStripped")] public string StrAlbumStripped { get; set; }
    [JsonProperty("strArtist")] public string StrArtist { get; set; }
    [JsonProperty("strArtistStripped")] public string StrArtistStripped { get; set; }
    [JsonProperty("intYearReleased")] public string IntYearReleased { get; set; }
    [JsonProperty("strStyle")] public string StrStyle { get; set; }
    [JsonProperty("strGenre")] public string StrGenre { get; set; }
    [JsonProperty("strLabel")] public string StrLabel { get; set; }
    [JsonProperty("strReleaseFormat")] public string StrReleaseFormat { get; set; }
    [JsonProperty("intSales")] public string IntSales { get; set; }
    [JsonProperty("strAlbumThumb")] public string StrAlbumThumb { get; set; }
    [JsonProperty("strAlbumThumbHQ")] public string StrAlbumThumbHQ { get; set; }
    [JsonProperty("strAlbumThumbBack")] public string StrAlbumThumbBack { get; set; }
    [JsonProperty("strAlbumCDart")] public string StrAlbumCDart { get; set; }
    [JsonProperty("strAlbumSpine")] public string StrAlbumSpine { get; set; }
    [JsonProperty("strAlbum3DCase")] public string StrAlbum3DCase { get; set; }
    [JsonProperty("strAlbum3DFlat")] public string StrAlbum3DFlat { get; set; }
    [JsonProperty("strAlbum3DFace")] public string StrAlbum3DFace { get; set; }
    [JsonProperty("strAlbum3DThumb")] public string StrAlbum3DThumb { get; set; }
    [JsonProperty("strDescriptionEN")] public string? StrDescriptionEN { get; set; }
    [JsonProperty("strDescriptionDE")] public string? StrDescriptionDE { get; set; }
    [JsonProperty("strDescriptionFR")] public string? StrDescriptionFR { get; set; }
    [JsonProperty("strDescriptionCN")] public string? StrDescriptionCN { get; set; }
    [JsonProperty("strDescriptionIT")] public string? StrDescriptionIT { get; set; }
    [JsonProperty("strDescriptionJP")] public string? StrDescriptionJP { get; set; }
    [JsonProperty("strDescriptionRU")] public string? StrDescriptionRU { get; set; }
    [JsonProperty("strDescriptionES")] public string? StrDescriptionES { get; set; }
    [JsonProperty("strDescriptionPT")] public string? StrDescriptionPT { get; set; }
    [JsonProperty("strDescriptionSE")] public string? StrDescriptionSE { get; set; }
    [JsonProperty("strDescriptionNL")] public string? StrDescriptionNL { get; set; }
    [JsonProperty("strDescriptionHU")] public string? StrDescriptionHU { get; set; }
    [JsonProperty("strDescriptionNO")] public string? StrDescriptionNO { get; set; }
    [JsonProperty("strDescriptionIL")] public string? StrDescriptionIL { get; set; }
    [JsonProperty("strDescriptionPL")] public string? StrDescriptionPL { get; set; }
    [JsonProperty("intLoved")] public string IntLoved { get; set; }
    [JsonProperty("intScore")] public string IntScore { get; set; }
    [JsonProperty("intScoreVotes")] public string IntScoreVotes { get; set; }
    [JsonProperty("strReview")] public string StrReview { get; set; }
    [JsonProperty("strMood")] public string StrMood { get; set; }
    [JsonProperty("strTheme")] public string StrTheme { get; set; }
    [JsonProperty("strSpeed")] public string StrSpeed { get; set; }
    [JsonProperty("strLocation")] public string StrLocation { get; set; }
    [JsonProperty("strMusicBrainzID")] public string StrMusicBrainzId { get; set; }

    [JsonProperty("strMusicBrainzArtistID")]
    public string StrMusicBrainzArtistId { get; set; }

    [JsonProperty("strAllMusicID")] public string StrAllMusicId { get; set; }
    [JsonProperty("strBBCReviewID")] public string StrBbcReviewId { get; set; }
    [JsonProperty("strRateYourMusicID")] public string StrRateYourMusicId { get; set; }
    [JsonProperty("strDiscogsID")] public string StrDiscogsId { get; set; }
    [JsonProperty("strWikidataID")] public string StrWikidataId { get; set; }
    [JsonProperty("strWikipediaID")] public string StrWikipediaId { get; set; }
    [JsonProperty("strGeniusID")] public string StrGeniusId { get; set; }
    [JsonProperty("strLyricWikiID")] public string StrLyricWikiId { get; set; }
    [JsonProperty("strMusicMozID")] public string StrMusicMozId { get; set; }
    [JsonProperty("strItunesID")] public string StrItunesId { get; set; }
    [JsonProperty("strAmazonID")] public string StrAmazonId { get; set; }
    [JsonProperty("strLocked")] public string StrLocked { get; set; }

    [JsonProperty("descriptions")]
    public List<TadbLanguageDescription> Descriptions
    {
        get
        {
            List<TadbLanguageDescription> descriptions = new();
            if (StrDescriptionCN != null)
                descriptions.Add(new() { Iso31661 = "CN", Description = StrDescriptionCN });
            if (StrDescriptionDE != null)
                descriptions.Add(new() { Iso31661 = "DE", Description = StrDescriptionDE });
            if (StrDescriptionEN != null)
                descriptions.Add(new() { Iso31661 = "EN", Description = StrDescriptionEN });
            if (StrDescriptionES != null)
                descriptions.Add(new() { Iso31661 = "ES", Description = StrDescriptionES });
            if (StrDescriptionFR != null)
                descriptions.Add(new() { Iso31661 = "FR", Description = StrDescriptionFR });
            if (StrDescriptionHU != null)
                descriptions.Add(new() { Iso31661 = "HU", Description = StrDescriptionHU });
            if (StrDescriptionIL != null)
                descriptions.Add(new() { Iso31661 = "IL", Description = StrDescriptionIL });
            if (StrDescriptionIT != null)
                descriptions.Add(new() { Iso31661 = "IT", Description = StrDescriptionIT });
            if (StrDescriptionJP != null)
                descriptions.Add(new() { Iso31661 = "JP", Description = StrDescriptionJP });
            if (StrDescriptionNL != null)
                descriptions.Add(new() { Iso31661 = "NL", Description = StrDescriptionNL });
            if (StrDescriptionNO != null)
                descriptions.Add(new() { Iso31661 = "NO", Description = StrDescriptionNO });
            if (StrDescriptionPL != null)
                descriptions.Add(new() { Iso31661 = "PL", Description = StrDescriptionPL });
            if (StrDescriptionPT != null)
                descriptions.Add(new() { Iso31661 = "PT", Description = StrDescriptionPT });
            if (StrDescriptionRU != null)
                descriptions.Add(new() { Iso31661 = "RU", Description = StrDescriptionRU });
            if (StrDescriptionSE != null)
                descriptions.Add(new() { Iso31661 = "SE", Description = StrDescriptionSE });

            return descriptions;
        }
    }
}
