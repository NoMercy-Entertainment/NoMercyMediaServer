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

namespace NoMercy.Providers.TMDB.Models.Shared;

public class TmdbCertificationList
{
    [JsonProperty(propertyName: "AU")]
    public TmdbCertificationItem[] Au { get; set; } = [];

    [JsonProperty(propertyName: "BG")]
    public TmdbCertificationItem[] Bg { get; set; } = [];

    [JsonProperty(propertyName: "BR")]
    public TmdbCertificationItem[] Br { get; set; } = [];

    [JsonProperty(propertyName: "CA-QC")]
    public TmdbCertificationItem[] Caqc { get; set; } = [];

    [JsonProperty(propertyName: "CA")]
    public TmdbCertificationItem[] Ca { get; set; } = [];

    [JsonProperty(propertyName: "DE")]
    public TmdbCertificationItem[] De { get; set; } = [];

    [JsonProperty(propertyName: "ES")]
    public TmdbCertificationItem[] Es { get; set; } = [];

    [JsonProperty(propertyName: "FI")]
    public TmdbCertificationItem[] Fi { get; set; } = [];

    [JsonProperty(propertyName: "FR")]
    public TmdbCertificationItem[] Fr { get; set; } = [];

    [JsonProperty(propertyName: "GB")]
    public TmdbCertificationItem[] Gb { get; set; } = [];

    [JsonProperty(propertyName: "HU")]
    public TmdbCertificationItem[] Hu { get; set; } = [];

    [JsonProperty(propertyName: "IN")]
    public TmdbCertificationItem[] In { get; set; } = [];

    [JsonProperty(propertyName: "KR")]
    public TmdbCertificationItem[] Kr { get; set; } = [];

    [JsonProperty(propertyName: "LT")]
    public TmdbCertificationItem[] Lt { get; set; } = [];

    [JsonProperty(propertyName: "NL")]
    public TmdbCertificationItem[] Nl { get; set; } = [];

    [JsonProperty(propertyName: "NZ")]
    public TmdbCertificationItem[] Nz { get; set; } = [];

    [JsonProperty(propertyName: "PH")]
    public TmdbCertificationItem[] Ph { get; set; } = [];

    [JsonProperty(propertyName: "RU")]
    public TmdbCertificationItem[] Ru { get; set; } = [];

    [JsonProperty(propertyName: "SK")]
    public TmdbCertificationItem[] Sk { get; set; } = [];

    [JsonProperty(propertyName: "US")]
    public TmdbCertificationItem[] Us { get; set; } = [];

    [JsonProperty(propertyName: "DK")]
    public TmdbCertificationItem[] Dk { get; set; } = [];

    [JsonProperty(propertyName: "IT")]
    public TmdbCertificationItem[] It { get; set; } = [];

    [JsonProperty(propertyName: "MY")]
    public TmdbCertificationItem[] My { get; set; } = [];

    [JsonProperty(propertyName: "NO")]
    public TmdbCertificationItem[] No { get; set; } = [];

    [JsonProperty(propertyName: "SE")]
    public TmdbCertificationItem[] Se { get; set; } = [];

    [JsonProperty(propertyName: "TH")]
    public TmdbCertificationItem[] Th { get; set; } = [];

    [JsonProperty(propertyName: "PT")]
    public TmdbCertificationItem[] Pt { get; set; } = [];
}
