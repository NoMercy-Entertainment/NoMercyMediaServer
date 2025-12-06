namespace NoMercy.Api.Controllers.V1.Encoder.Dto;

/// <summary>
/// DTO for encoder node registration
/// Received from external encoder nodes on startup
/// </summary>
public class EncoderNodeRegistrationDto
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string NetworkAddress { get; set; } = string.Empty;
    public int NetworkPort { get; set; }
    public bool UseHttps { get; set; }
    public string NodeVersion { get; set; } = string.Empty;
}
