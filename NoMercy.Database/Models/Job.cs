﻿#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
[Index(nameof(CreditId), IsUnique = true)]
public class Job
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("task")] public string? Task { get; set; }
    [JsonProperty("episode_count")] public int? EpisodeCount { get; set; }
    [JsonProperty("order")] public int? Order { get; set; }

    [JsonProperty("credit_id")] public string? CreditId { get; set; }
    public Crew? Crew { get; set; }

    public Job()
    {
    }
    
}