using Microsoft.AspNetCore.Mvc;
using StorageWatchServer.Server.Api;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Reporting;

namespace StorageWatchServer.Controllers;

/// <summary>
/// API controller for raw row ingestion from Agents.
/// Accepts batch ingestion without any transformation or normalization.
/// </summary>
[ApiController]
[Route("api/agent")]
public class RawRowsController : ControllerBase
{
    private readonly RawRowIngestionService _ingestionService;
    private readonly ILogger<RawRowsController> _logger;

    public RawRowsController(RawRowIngestionService ingestionService, ILogger<RawRowsController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/agent/report
    /// Accepts a batch of raw drive rows from an Agent.
    /// </summary>
    [HttpPost("report")]
    public async Task<IActionResult> PostReport([FromBody] AgentReportRequest request)
    {
        // Validate input
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (string.IsNullOrWhiteSpace(request.MachineName))
        {
            return BadRequest(new { error = "machineName is required." });
        }

        if (request.Rows == null)
        {
            return BadRequest(new { error = "rows must be a non-null array." });
        }

        if (request.Rows.Count == 0)
        {
            return BadRequest(new { error = "rows array must not be empty." });
        }

        // Validate each row
        foreach (var row in request.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.DriveLetter))
            {
                return BadRequest(new { error = "All rows must have a driveLetter." });
            }
        }

        try
        {
            // Convert request rows to domain model
            var domainRows = request.Rows.Select(r => new RawDriveRow
            {
                DriveLetter = r.DriveLetter,
                TotalSpaceGb = r.TotalSpaceGb,
                UsedSpaceGb = r.UsedSpaceGb,
                FreeSpaceGb = r.FreeSpaceGb,
                PercentFree = r.PercentFree,
                Timestamp = r.Timestamp
            }).ToList();

            // Insert into database
            await _ingestionService.IngestRawRowsAsync(request.MachineName, domainRows);

            _logger.LogInformation(
                "Batch report accepted from machine '{MachineName}' with {RowCount} rows.",
                request.MachineName,
                request.Rows.Count);

            return Ok(new { message = "Batch report received and processed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch report from machine '{MachineName}'", request.MachineName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred processing the report." });
        }
    }
}
