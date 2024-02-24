﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models
{
    [PrimaryKey(nameof(Id))]
    public class ActivityLog : Timestamps
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonProperty("id")]
        public int Id { get; private set; }

        [JsonProperty("type")] public string Type { get; set; }
        public required DateTime Time { get; set; }

        [JsonProperty("device_id")] public Ulid DeviceId { get; set; }
        public virtual Device Device { get; set; }

        [JsonProperty("user_id")] public Guid UserId { get; set; }
        public virtual User User { get; set; }
    }
}