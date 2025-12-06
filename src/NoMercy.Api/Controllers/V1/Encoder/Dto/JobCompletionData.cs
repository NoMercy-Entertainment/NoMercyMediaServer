namespace NoMercy.Api.Controllers.V1.Encoder.Dto;

/// <summary>
/// DTO for job completion data from encoder nodes
/// </summary>
public class JobCompletionData
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}
