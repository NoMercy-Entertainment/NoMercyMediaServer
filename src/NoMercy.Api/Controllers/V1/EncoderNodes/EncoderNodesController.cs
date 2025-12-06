using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Api.Controllers.V1.Encoder.Dto;
using NoMercy.NmSystem.Information;

namespace NoMercy.Api.Controllers.V1.EncoderNodes;

/// <summary>
/// API endpoints for managing distributed encoder nodes
/// Handles registration, heartbeats, and status monitoring
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/encoder-nodes", Order = 25)]
[Authorize]
public class EncoderNodesController : ControllerBase
{
    private readonly MediaContext _context;
    private readonly QueueContext _queueContext;
    private readonly ILogger<EncoderNodesController> _logger;

    public EncoderNodesController(
        MediaContext context,
        QueueContext queueContext,
        ILogger<EncoderNodesController> logger)
    {
        _context = context;
        _queueContext = queueContext;
        _logger = logger;
    }

    /// <summary>
    /// Register a new encoder node with the server
    /// Called by encoder nodes on startup to establish connection
    /// </summary>
    /// <param name="capabilities">Encoder node capabilities from the encoder node</param>
    /// <returns>Registration confirmation with node ID</returns>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterEncoderNode([FromBody] EncoderNodeRegistrationDto capabilities)
    {
        if (capabilities == null)
        {
            _logger.LogWarning("Register endpoint received null capabilities");
            return BadRequest(new { error = "Invalid encoder node registration request" });
        }

        try
        {
            string nodeId = capabilities.NodeId;
            string nodeName = capabilities.NodeName;
            string networkAddress = capabilities.NetworkAddress;
            int networkPort = capabilities.NetworkPort;
            bool useHttps = capabilities.UseHttps;
            string nodeVersion = capabilities.NodeVersion;

            _logger.LogInformation("Encoder node registration: NodeId={NodeId}, NodeName={NodeName}, Address={Address}:{Port}, Version={Version}", 
                nodeId, nodeName, networkAddress, networkPort, nodeVersion);

            // Validate required fields
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                _logger.LogWarning("Registration received but node_id is empty or null");
                return BadRequest(new { error = "node_id is required" });
            }

            if (string.IsNullOrWhiteSpace(nodeName))
            {
                _logger.LogWarning("Registration received but node_name is empty or null");
                return BadRequest(new { error = "node_name is required" });
            }

            if (string.IsNullOrWhiteSpace(networkAddress))
            {
                _logger.LogWarning("Registration received but network_address is empty or null");
                return BadRequest(new { error = "network_address is required" });
            }

            if (networkPort <= 0)
            {
                _logger.LogWarning("Registration received but network_port is invalid: {Port}", networkPort);
                return BadRequest(new { error = "network_port must be a valid port number" });
            }

            // Check if node already exists
            EncoderNode? existingNode = await _context.EncoderNodes
                .FirstOrDefaultAsync(n => n.NodeId == nodeId);

            if (existingNode != null)
            {
                // Update existing node
                existingNode.NodeName = nodeName;
                existingNode.NetworkAddress = networkAddress;
                existingNode.NetworkPort = networkPort;
                existingNode.UseHttps = useHttps;
                existingNode.Version = nodeVersion;
                existingNode.IsActive = true;
                existingNode.LastHeartbeat = DateTime.UtcNow;
                _context.EncoderNodes.Update(existingNode);
                _logger.LogInformation("Updated encoder node {NodeId}: {NodeName} at {Address}:{Port}", 
                    nodeId, nodeName, networkAddress, networkPort);
            }
            else
            {
                // Create new node
                EncoderNode newNode = new()
                {
                    NodeId = nodeId,
                    NodeName = nodeName,
                    NetworkAddress = networkAddress,
                    NetworkPort = networkPort,
                    UseHttps = useHttps,
                    Version = nodeVersion,
                    IsActive = true,
                    LastHeartbeat = DateTime.UtcNow
                };
                _context.EncoderNodes.Add(newNode);
                _logger.LogInformation("Registered new encoder node {NodeId}: {NodeName} at {Address}:{Port}", 
                    nodeId, nodeName, networkAddress, networkPort);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "ok",
                nodeId = nodeId,
                message = "Encoder node registered successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering encoder node");
            return StatusCode(500, new { error = "Failed to register encoder node", details = ex.Message });
        }
    }

    /// <summary>
    /// Send heartbeat from an encoder node
    /// Called periodically to indicate the node is alive and operational
    /// </summary>
    /// <param name="nodeId">The ID of the encoder node</param>
    /// <param name="heartbeat">Current node metrics and status</param>
    /// <returns>Heartbeat acknowledgment</returns>
    [HttpPost("{nodeId}/heartbeat")]
    public async Task<IActionResult> SendHeartbeat(
        [FromRoute] string nodeId,
        [FromBody] EncoderNodeHeartbeatModel heartbeat)
    {
        if (string.IsNullOrEmpty(nodeId) || heartbeat == null)
            return BadRequest(new { error = "Invalid heartbeat request" });

        try
        {
            _logger.LogDebug("Heartbeat received from encoder node {NodeId}", nodeId);

            // Find and update the encoder node
            EncoderNode? node = await _context.EncoderNodes
                .FirstOrDefaultAsync(n => n.NodeId == nodeId);

            if (node != null)
            {
                node.LastHeartbeat = DateTime.UtcNow;
                node.IsActive = true;
                _context.EncoderNodes.Update(node);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                status = "ok",
                message = "Heartbeat received"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat from node {NodeId}", nodeId);
            return StatusCode(500, new { error = "Failed to process heartbeat" });
        }
    }

    /// <summary>
    /// Get status of a specific encoder node
    /// </summary>
    /// <param name="nodeId">The ID of the encoder node</param>
    /// <returns>Encoder node status and information</returns>
    [HttpGet("{nodeId}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetNodeStatus([FromRoute] string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
            return BadRequest(new { error = "Node ID is required" });

        try
        {
            _logger.LogDebug("Status request for encoder node {NodeId}", nodeId);

            // Retrieve node from database
            EncoderNode? node = await _context.EncoderNodes
                .FirstOrDefaultAsync(n => n.NodeId == nodeId);

            if (node == null)
                return NotFound(new { error = "Encoder node not found" });

            return Ok(new
            {
                status = "ok",
                nodeId = node.NodeId,
                nodeName = node.NodeName,
                online = node.IsActive,
                lastHeartbeat = node.LastHeartbeat,
                networkAddress = node.NetworkAddress,
                networkPort = node.NetworkPort
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for node {NodeId}", nodeId);
            return StatusCode(500, new { error = "Failed to retrieve node status" });
        }
    }

    /// <summary>
    /// Queue a test encoding job for encoder nodes to pick up
    /// This is a development/testing endpoint
    /// </summary>
    /// <returns>Queued job ID and status</returns>
    [HttpPost("test-job")]
    public async Task<IActionResult> QueueTestJob()
    {
        try
        {
            // Setup paths
            string sourceFile = Path.Combine(AppFiles.TranscodePath, "Call.of.the.Night.S02E01.mkv");
            string outputFolder = Path.Combine(AppFiles.TranscodePath, "test_output");
            
            // Create output folder if it doesn't exist
            Directory.CreateDirectory(outputFolder);
            
            string jobId = Guid.NewGuid().ToString();
            var testJobPayload = new
            {
                job_id = jobId,
                job_type = "Video",
                media_type = "video",
                created_at = DateTime.UtcNow,
                input = new
                {
                    file_path = sourceFile,
                    network_path = sourceFile,
                    file_hash = "test_hash_123",
                    file_size = 1073741824,
                    duration = "00:30:00"
                },
                output = new
                {
                    destination_folder = outputFolder,
                    file_name = "test_encoded.mp4",
                    thumbnail_folder = Path.Combine(outputFolder, "thumbnails"),
                    chapter_extract_folder = (string)null,
                    font_extract_folder = (string)null,
                    generated_files = new string[] { }
                },
                profile = new
                {
                    name = "Test Profile",
                    container = "hls",
                    video_profile = new
                    {
                        codec = "h264",
                        width = 1920,
                        crf = 23
                    },
                    audio_profile = new
                    {
                        codec = "aac",
                        channels = 2
                    }
                },
                status = new
                {
                    state = "pending",
                    error_message = (string)null,
                    progress_percentage = 0
                }
            };

            // Queue the job
            string jobPayloadJson = JsonConvert.SerializeObject(testJobPayload);
            QueueJob queueJob = new()
            {
                Queue = "encoder:video",
                Payload = jobPayloadJson,
                Priority = 1,
                AvailableAt = DateTime.UtcNow
            };

            _queueContext.QueueJobs.Add(queueJob);
            await _queueContext.SaveChangesAsync();

            _logger.LogInformation("Test encoding job queued: {JobId} - Source: {SourceFile}", jobId, sourceFile);

            return Ok(new
            {
                status = "ok",
                message = "Test job queued successfully",
                job_id = jobId,
                queue = "encoder:video",
                db_id = queueJob.Id,
                source_file = sourceFile,
                output_folder = outputFolder
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing test job");
            return StatusCode(500, new { error = "Failed to queue test job", details = ex.Message });
        }
    }

    #region Hardware Capabilities Endpoints

    /// <summary>
    #endregion
}
