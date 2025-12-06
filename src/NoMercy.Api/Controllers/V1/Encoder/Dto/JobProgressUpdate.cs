namespace NoMercy.Api.Controllers.V1.Encoder.Dto;

/// <summary>
/// DTO for job progress updates from encoder nodes
/// </summary>
public class JobProgressUpdate
{
    public string JobId { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
