using Newtonsoft.Json;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NoMercy.Database;

public class Timestamps
{
    [DefaultValue("CURRENT_TIMESTAMP")]
    [JsonProperty("created_at")]
    [TypeConverter("TIMESTAMP")]
    [Timestamp]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreatedAt { get; set; }

    [DefaultValue("CURRENT_TIMESTAMP")]
    [JsonProperty("updated_at")]
    [TypeConverter("TIMESTAMP")]
    [Timestamp]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime UpdatedAt { get; set; }
}