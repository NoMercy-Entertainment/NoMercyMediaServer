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

namespace NoMercy.Providers.Tadb.Models;

public class TadbArtist
{
    [JsonProperty(propertyName: "idArtist")]
    public string? IdArtist { get; set; }

    [JsonProperty(propertyName: "strArtist")]
    public string? StrArtist { get; set; }

    [JsonProperty(propertyName: "strArtistStripped")]
    public string? StrArtistStripped { get; set; }

    [JsonProperty(propertyName: "strArtistAlternate")]
    public string? StrArtistAlternate { get; set; }

    [JsonProperty(propertyName: "strLabel")]
    public string? StrLabel { get; set; }

    [JsonProperty(propertyName: "idLabel")]
    public string? IdLabel { get; set; }

    [JsonProperty(propertyName: "intFormedYear")]
    public string? IntFormedYear { get; set; }

    [JsonProperty(propertyName: "intBornYear")]
    public string? IntBornYear { get; set; }

    [JsonProperty(propertyName: "intDiedYear")]
    public string? IntDiedYear { get; set; }

    [JsonProperty(propertyName: "strDisbanded")]
    public string? StrDisbanded { get; set; }

    [JsonProperty(propertyName: "strStyle")]
    public string? StrStyle { get; set; }

    [JsonProperty(propertyName: "strGenre")]
    public string? StrGenre { get; set; }

    [JsonProperty(propertyName: "strMood")]
    public string? StrMood { get; set; }

    [JsonProperty(propertyName: "strWebsite")]
    public string? StrWebsite { get; set; }

    [JsonProperty(propertyName: "strFacebook")]
    public string? StrFacebook { get; set; }

    [JsonProperty(propertyName: "strTwitter")]
    public string? StrTwitter { get; set; }

    [JsonProperty(propertyName: "strBiographyEN")]
    public string? StrBiographyEn { get; set; }

    [JsonProperty(propertyName: "strBiographyDE")]
    public string? StrBiographyDe { get; set; }

    [JsonProperty(propertyName: "strBiographyFR")]
    public string? StrBiographyFr { get; set; }

    [JsonProperty(propertyName: "strBiographyCN")]
    public string? StrBiographyCn { get; set; }

    [JsonProperty(propertyName: "strBiographyIT")]
    public string? StrBiographyIt { get; set; }

    [JsonProperty(propertyName: "strBiographyJP")]
    public string? StrBiographyJp { get; set; }

    [JsonProperty(propertyName: "strBiographyRU")]
    public string? StrBiographyRu { get; set; }

    [JsonProperty(propertyName: "strBiographyES")]
    public string? StrBiographyEs { get; set; }

    [JsonProperty(propertyName: "strBiographyPT")]
    public string? StrBiographyPt { get; set; }

    [JsonProperty(propertyName: "strBiographySE")]
    public string? StrBiographySe { get; set; }

    [JsonProperty(propertyName: "strBiographyNL")]
    public string? StrBiographyNl { get; set; }

    [JsonProperty(propertyName: "strBiographyHU")]
    public string? StrBiographyHu { get; set; }

    [JsonProperty(propertyName: "strBiographyNO")]
    public string? StrBiographyNo { get; set; }

    [JsonProperty(propertyName: "strBiographyIL")]
    public string? StrBiographyIl { get; set; }

    [JsonProperty(propertyName: "strBiographyPL")]
    public string? StrBiographyPl { get; set; }

    [JsonProperty(propertyName: "strGender")]
    public string? StrGender { get; set; }

    [JsonProperty(propertyName: "intMembers")]
    public string? IntMembers { get; set; }

    [JsonProperty(propertyName: "strCountry")]
    public string? StrCountry { get; set; }

    [JsonProperty(propertyName: "strCountryCode")]
    public string? StrCountryCode { get; set; }

    [JsonProperty(propertyName: "strArtistThumb")]
    public string? StrArtistThumb { get; set; }

    [JsonProperty(propertyName: "strArtistLogo")]
    public string? StrArtistLogo { get; set; }

    [JsonProperty(propertyName: "strArtistCutout")]
    public string? StrArtistCutout { get; set; }

    [JsonProperty(propertyName: "strArtistClearart")]
    public string? StrArtistClearart { get; set; }

    [JsonProperty(propertyName: "strArtistWideThumb")]
    public string? StrArtistWideThumb { get; set; }

    [JsonProperty(propertyName: "strArtistFanart")]
    public string? StrArtistFanart { get; set; }

    [JsonProperty(propertyName: "strArtistFanart2")]
    public string? StrArtistFanart2 { get; set; }

    [JsonProperty(propertyName: "strArtistFanart3")]
    public string? StrArtistFanart3 { get; set; }

    [JsonProperty(propertyName: "strArtistFanart4")]
    public string? StrArtistFanart4 { get; set; }

    [JsonProperty(propertyName: "strArtistBanner")]
    public string? StrArtistBanner { get; set; }

    [JsonProperty(propertyName: "strMusicBrainzID")]
    public string? StrMusicBrainzId { get; set; }

    [JsonProperty(propertyName: "strISNIcode")]
    public string? StrIsnIcode { get; set; }

    [JsonProperty(propertyName: "strLastFMChart")]
    public string? StrLastFmChart { get; set; }

    [JsonProperty(propertyName: "intCharted")]
    public string? IntCharted { get; set; }

    [JsonProperty(propertyName: "strLocked")]
    public string? StrLocked { get; set; }

    [JsonProperty(propertyName: "descriptions")]
    public List<TadbLanguageDescription> Descriptions
    {
        get
        {
            List<TadbLanguageDescription> descriptions = [];
            if (StrBiographyCn != null)
                descriptions.Add(item: new() { Iso31661 = "CN", Description = StrBiographyCn });
            if (StrBiographyDe != null)
                descriptions.Add(item: new() { Iso31661 = "DE", Description = StrBiographyDe });
            if (StrBiographyEn != null)
                descriptions.Add(item: new() { Iso31661 = "EN", Description = StrBiographyEn });
            if (StrBiographyEs != null)
                descriptions.Add(item: new() { Iso31661 = "ES", Description = StrBiographyEs });
            if (StrBiographyFr != null)
                descriptions.Add(item: new() { Iso31661 = "FR", Description = StrBiographyFr });
            if (StrBiographyHu != null)
                descriptions.Add(item: new() { Iso31661 = "HU", Description = StrBiographyHu });
            if (StrBiographyIl != null)
                descriptions.Add(item: new() { Iso31661 = "IL", Description = StrBiographyIl });
            if (StrBiographyIt != null)
                descriptions.Add(item: new() { Iso31661 = "IT", Description = StrBiographyIt });
            if (StrBiographyJp != null)
                descriptions.Add(item: new() { Iso31661 = "JP", Description = StrBiographyJp });
            if (StrBiographyNl != null)
                descriptions.Add(item: new() { Iso31661 = "NL", Description = StrBiographyNl });
            if (StrBiographyNo != null)
                descriptions.Add(item: new() { Iso31661 = "NO", Description = StrBiographyNo });
            if (StrBiographyPl != null)
                descriptions.Add(item: new() { Iso31661 = "PL", Description = StrBiographyPl });
            if (StrBiographyPt != null)
                descriptions.Add(item: new() { Iso31661 = "PT", Description = StrBiographyPt });
            if (StrBiographyRu != null)
                descriptions.Add(item: new() { Iso31661 = "RU", Description = StrBiographyRu });
            if (StrBiographySe != null)
                descriptions.Add(item: new() { Iso31661 = "SE", Description = StrBiographySe });

            return descriptions;
        }
    }
}
