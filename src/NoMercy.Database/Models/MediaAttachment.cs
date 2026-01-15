using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[PrimaryKey(nameof(Id))]
public class MediaAttachment
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonProperty("id")]
    public required Ulid Id { get; set; } = Ulid.NewUlid();
}