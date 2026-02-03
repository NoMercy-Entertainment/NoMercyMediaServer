namespace NoMercy.EncoderNode.Configuration;

/// <summary>
/// Configuration options for the encoder node
/// </summary>
public class EncoderNodeOptions
{
    public const string SectionName = "EncoderNode";

    /// <summary>
    /// Node identifier
    /// </summary>
    public string NodeId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Human-readable node name
    /// </summary>
    public string NodeName { get; set; } = Environment.MachineName;

    /// <summary>
    /// Primary server configuration
    /// </summary>
    public PrimaryServerOptions PrimaryServer { get; set; } = new();

    /// <summary>
    /// Encoder configuration
    /// </summary>
    public EncoderOptions Encoder { get; set; } = new();

    /// <summary>
    /// Registration configuration
    /// </summary>
    public RegistrationOptions Registration { get; set; } = new();

    /// <summary>
    /// Keycloak authentication configuration
    /// </summary>
    public KeycloakOptions Keycloak { get; set; } = new();
}

public class PrimaryServerOptions
{
    /// <summary>
    /// Primary server URL (e.g., https://nomercy-server:5001)
    /// </summary>
    public string Url { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

public class KeycloakOptions
{
    /// <summary>
    /// Keycloak server URL (e.g., https://auth.nomercy.tv)
    /// </summary>
    public string Url { get; set; } = "https://auth.nomercy.tv";

    /// <summary>
    /// Realm name (e.g., NoMercyTV)
    /// </summary>
    public string Realm { get; set; } = "NoMercyTV";

    /// <summary>
    /// Client ID for encoder node authentication
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for encoder node authentication
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Whether Keycloak authentication is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

public class EncoderOptions
{
    /// <summary>
    /// Maximum number of concurrent encoding jobs
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 1;

    /// <summary>
    /// Path to FFmpeg executable
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>
    /// Path to FFprobe executable
    /// </summary>
    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>
    /// Temporary folder for encoding scratch space
    /// </summary>
    public string TempFolder { get; set; } = "./temp";

    /// <summary>
    /// Output folder for completed encodings
    /// </summary>
    public string OutputFolder { get; set; } = "./output";

    /// <summary>
    /// Whether to enable GPU acceleration if available
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;

    /// <summary>
    /// Whether to enable encoding jobs on this node
    /// Useful for disabling on weak hardware (NAS, Raspberry Pi)
    /// </summary>
    public bool EnableEncoderJobs { get; set; } = true;

    /// <summary>
    /// Allowed source folders for encoding (security)
    /// </summary>
    public List<string> AllowedSourceFolders { get; set; } = [];

    /// <summary>
    /// Allowed output folders for encoding (security)
    /// </summary>
    public List<string> AllowedOutputFolders { get; set; } = [];

    /// <summary>
    /// Progress report interval in milliseconds (default: 1000ms)
    /// </summary>
    public int ProgressReportIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Whether to log progress to console
    /// </summary>
    public bool LogProgress { get; set; } = true;
}

public class RegistrationOptions
{
    /// <summary>
    /// Heartbeat interval in seconds
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Initial registration retry interval in seconds
    /// </summary>
    public int RegistrationRetryIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of registration retry attempts
    /// </summary>
    public int RegistrationMaxRetries { get; set; } = 5;

    /// <summary>
    /// Whether to require successful registration to start serving jobs
    /// </summary>
    public bool RequireSuccessfulRegistration { get; set; } = false;
}
