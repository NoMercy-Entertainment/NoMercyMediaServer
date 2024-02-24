﻿using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models
{
    [PrimaryKey(nameof(Id))]
    public class User : Timestamps
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [JsonProperty("id")] public Guid Id { get; set; }
        
        [JsonProperty("email")] public string Email { get; set; }
        [JsonProperty("manage")] public bool Manage { get; set; }
        [JsonProperty("owner")] public bool Owner { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("allowed")] public bool Allowed { get; set; }
        [JsonProperty("audio_transcoding")] public bool AudioTranscoding { get; set; }
        [JsonProperty("video_transcoding")] public bool VideoTranscoding { get; set; }
        [JsonProperty("no_transcoding")] public bool NoTranscoding { get; set; }

        [JsonProperty("library_user")] 
        public virtual ICollection<LibraryUser> LibraryUser { get; set; }
        
        [JsonProperty("notification_user")] 
        public virtual ICollection<NotificationUser> NotificationUser { get; set; }

        public User()
        {
        }

        public User(Guid id, string email, bool manage, bool owner, string name, bool allowed, bool audioTranscoding,
            bool videoTranscoding, bool noTranscoding)
        {
            Id = id;
            Email = email;
            Manage = manage;
            Owner = owner;
            Name = name;
            Allowed = allowed;
            AudioTranscoding = audioTranscoding;
            VideoTranscoding = videoTranscoding;
            NoTranscoding = noTranscoding;
        }
    }
}