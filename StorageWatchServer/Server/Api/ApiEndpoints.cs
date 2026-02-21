using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Reporting;
using StorageWatchServer.Server.Reporting.Data;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Server.Api;

public static class ApiEndpoints
{
    public static void MapAgentEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/agent/report", PostAgentReport)
            .WithName("PostAgentReport");

        group.MapGet("/machines", GetMachines)
            .WithName("GetMachines");

        group.MapGet("/machines/{id:int}", GetMachineById)
            .WithName("GetMachineById");

        group.MapGet("/machines/{id:int}/history", GetMachineHistory)
            .WithName("GetMachineHistory");

        group.MapGet("/alerts", GetAlerts)
            .WithName("GetAlerts");

        group.MapGet("/settings", GetSettings)
            .WithName("GetSettings");

        group.MapGet("/dashboard/reports/recent", GetRecentReports)
            .WithName("GetRecentReports");
    }

    private static async Task<IResult> PostAgentReport(
        AgentReportRequest request,
        IAgentReportRepository repository,
        ILogger<Program> logger,
        HttpContext context)
    {
        try
        {
            if (!TryValidateAgentReport(request, out var validationMessage))
            {
                logger.LogWarning("Agent report validation failed: {Message}", validationMessage);
                return Results.BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = validationMessage
                });
            }

            var receivedUtc = DateTime.UtcNow;
            var report = AgentReportMapper.Map(request, receivedUtc);

            await repository.SaveReportAsync(report);

            logger.LogInformation("Agent report received from {AgentId}. Drives: {DriveCount}, Alerts: {AlertCount}",
                report.AgentId, report.Drives.Count, report.Alerts.Count);

            return Results.Ok(new ApiResponse
            {
                Success = true,
                Message = "Report received."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing agent report from {AgentId}", request.AgentId);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static bool TryValidateAgentReport(AgentReportRequest request, out string message)
    {
        if (string.IsNullOrWhiteSpace(request.AgentId))
        {
            message = "AgentId is required.";
            return false;
        }

        if (request.Drives.Count == 0)
        {
            message = "At least one drive report is required.";
            return false;
        }

        if (request.Drives.Any(drive => string.IsNullOrWhiteSpace(drive.DriveLetter)))
        {
            message = "DriveLetter is required for all drive reports.";
            return false;
        }

        if (request.Drives.Any(drive => drive.TotalSpaceGb < 0 || drive.FreeSpaceGb < 0))
        {
            message = "Drive space values must be non-negative.";
            return false;
        }

        if (request.Drives.Any(drive => drive.UsedPercent < 0 || drive.UsedPercent > 100))
        {
            message = "UsedPercent must be between 0 and 100.";
            return false;
        }

        if (request.Alerts.Any(alert => string.IsNullOrWhiteSpace(alert.DriveLetter)))
        {
            message = "DriveLetter is required for all alerts.";
            return false;
        }

        if (request.Alerts.Any(alert => string.IsNullOrWhiteSpace(alert.Level)))
        {
            message = "Level is required for all alerts.";
            return false;
        }

        if (request.Alerts.Any(alert => string.IsNullOrWhiteSpace(alert.Message)))
        {
            message = "Message is required for all alerts.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static async Task<IResult> GetMachines(
        ServerRepository repository,
        MachineStatusService statusService,
        ILogger<Program> logger)
    {
        try
        {
            var machines = await repository.GetMachinesAsync();
            logger.LogDebug("Retrieved {MachineCount} machines from database", machines.Count);

            var response = machines.Select(machine => new
            {
                machine.Id,
                machine.MachineName,
                machine.LastSeenUtc,
                IsOnline = statusService.IsOnline(machine.LastSeenUtc),
                Drives = machine.Drives.Select(drive => new
                {
                    drive.DriveLetter,
                    drive.TotalSpaceGb,
                    drive.UsedSpaceGb,
                    drive.FreeSpaceGb,
                    drive.PercentFree,
                    drive.LastSeenUtc
                })
            });

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve machines");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetMachineById(
        int id,
        ServerRepository repository,
        MachineStatusService statusService,
        ILogger<Program> logger)
    {
        try
        {
            var machine = await repository.GetMachineAsync(id);
            if (machine == null)
            {
                return Results.NotFound();
            }

            var response = new
            {
                machine.Id,
                machine.MachineName,
                machine.LastSeenUtc,
                IsOnline = statusService.IsOnline(machine.LastSeenUtc),
                Drives = machine.Drives.Select(drive => new
                {
                    drive.DriveLetter,
                    drive.TotalSpaceGb,
                    drive.UsedSpaceGb,
                    drive.FreeSpaceGb,
                    drive.PercentFree,
                    drive.LastSeenUtc
                })
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve machine {MachineId}", id);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetMachineHistory(
        int id,
        string drive,
        string range,
        ServerRepository repository,
        ILogger<Program> logger)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(drive))
            {
                return Results.BadRequest("Drive letter is required");
            }

            var startUtc = GetHistoryStartUtc(range);
            var history = await repository.GetDiskHistoryAsync(id, drive, startUtc);

            return Results.Ok(history);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve machine history for {MachineId}", id);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetAlerts(
        ServerRepository repository,
        ILogger<Program> logger)
    {
        try
        {
            var alerts = await repository.GetAlertsAsync();
            return Results.Ok(alerts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve alerts");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetSettings(
        ServerRepository repository,
        ILogger<Program> logger)
    {
        try
        {
            var settings = await repository.GetSettingsAsync();
            return Results.Ok(settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve settings");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetRecentReports(
        int? count,
        IAgentReportRepository repository,
        ILogger<Program> logger)
    {
        var reportCount = count.GetValueOrDefault(50);
        if (reportCount <= 0)
        {
            return Results.BadRequest("Count must be greater than zero.");
        }

        try
        {
            var reports = await repository.GetRecentReportsAsync(reportCount);
            var response = reports.Select(report => new DashboardReportResponse
            {
                AgentId = report.AgentId,
                TimestampUtc = report.TimestampUtc,
                Drives = report.Drives.Select(drive => new DashboardDriveSummary
                {
                    DriveLetter = drive.DriveLetter,
                    UsedPercent = drive.UsedPercent
                }).ToList(),
                Alerts = report.Alerts.Select(alert => new DashboardAlertSummary
                {
                    DriveLetter = alert.DriveLetter,
                    Level = alert.Level,
                    Message = alert.Message
                }).ToList()
            });

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve recent reports");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static DateTime GetHistoryStartUtc(string range)
    {
        return range switch
        {
            "24h" => DateTime.UtcNow.AddHours(-24),
            "7d" => DateTime.UtcNow.AddDays(-7),
            "30d" => DateTime.UtcNow.AddDays(-30),
            _ => DateTime.UtcNow.AddDays(-7)
        };
    }
}
