﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models
{
    [Index(nameof(Iso6391), IsUnique = true)]
    [PrimaryKey(nameof(Id))]
    public class Language
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("iso_639_1")] public string Iso6391 { get; set; } = string.Empty;

        [JsonProperty("english_name")] public string EnglishName { get; set; } = string.Empty;

        [JsonProperty("name")] public string? Name { get; set; }

        [JsonProperty("language_library")] 
        public virtual ICollection<LanguageLibrary> LanguageLibrary { get; set; }

        public Language()
        {
        }

        public Language(Providers.TMDB.Models.Configuration.Language language)
        {
            Iso6391 = language.Iso6391;
            EnglishName = language.EnglishName;
            Name = language.Name;
        }
    }
}