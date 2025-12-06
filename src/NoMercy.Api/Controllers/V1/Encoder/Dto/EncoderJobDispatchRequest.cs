namespace NoMercy.Api.Controllers.V1.Encoder.Dto;

/// <summary>
/// DTO for job dispatch requests to encoder nodes
/// </summary>
public class EncoderJobDispatchRequest
{
    public string JobId { get; set; } = string.Empty;
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string ContainerFormat { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
}
