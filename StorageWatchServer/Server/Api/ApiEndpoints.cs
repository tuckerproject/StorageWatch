using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
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
    }

    private static async Task<IResult> PostAgentReport(
        AgentReportRequest request,
        ServerRepository repository,
        ILogger<Program> logger,
        HttpContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.MachineName) || request.Drives.Count == 0)
            {
                logger.LogWarning("Agent report validation failed: MachineName={MachineName}, DriveCount={DriveCount}",
                    request.MachineName, request.Drives.Count);
                return Results.BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "MachineName and at least one drive are required."
                });
            }

            var reportTime = request.CollectionTimeUtc == default ? DateTime.UtcNow : request.CollectionTimeUtc;
            var machineId = await repository.UpsertMachineAsync(request.MachineName, reportTime);
            logger.LogInformation("Agent report received from {MachineName} (ID: {MachineId}). Drives: {DriveCount}",
                request.MachineName, machineId, request.Drives.Count);

            foreach (var drive in request.Drives)
            {
                var driveTime = drive.CollectionTimeUtc == default ? reportTime : drive.CollectionTimeUtc;
                var status = new MachineDriveStatus
                {
                    DriveLetter = drive.DriveLetter,
                    TotalSpaceGb = drive.TotalSpaceGb,
                    UsedSpaceGb = drive.UsedSpaceGb,
                    FreeSpaceGb = drive.FreeSpaceGb,
                    PercentFree = drive.PercentFree,
                    LastSeenUtc = driveTime
                };

                await repository.UpsertDriveAsync(machineId, status);
                await repository.InsertDiskHistoryAsync(machineId, drive.DriveLetter, new DiskHistoryPoint
                {
                    CollectionTimeUtc = driveTime,
                    TotalSpaceGb = drive.TotalSpaceGb,
                    UsedSpaceGb = drive.UsedSpaceGb,
                    FreeSpaceGb = drive.FreeSpaceGb,
                    PercentFree = drive.PercentFree
                });
            }

            return Results.Ok(new ApiResponse
            {
                Success = true,
                Message = "Report received."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing agent report from {MachineName}", request.MachineName);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
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
            logger.LogError(ex, "Error retrieving machines");
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
                logger.LogWarning("Machine not found: ID={MachineId}", id);
                return Results.NotFound();
            }

            logger.LogDebug("Retrieved machine: ID={MachineId}, Name={MachineName}", id, machine.MachineName);

            return Results.Ok(new
            {
                machine.Id,
                machine.MachineName,
                machine.LastSeenUtc,
                machine.CreatedUtc,
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving machine: ID={MachineId}", id);
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
                logger.LogWarning("History request missing drive parameter: MachineId={MachineId}", id);
                return Results.BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Drive letter is required."
                });
            }

            var startUtc = ParseRange(range);
            var history = await repository.GetDiskHistoryAsync(id, drive, startUtc);
            logger.LogDebug("Retrieved {PointCount} history points for Machine={MachineId}, Drive={Drive}",
                history.Count, id, drive);

            return Results.Ok(history);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving history: MachineId={MachineId}, Drive={Drive}", id, drive);
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
            logger.LogDebug("Retrieved {AlertCount} alerts", alerts.Count);
            return Results.Ok(alerts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving alerts");
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
            logger.LogDebug("Retrieved {SettingCount} settings", settings.Count);
            return Results.Ok(settings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving settings");
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static DateTime ParseRange(string? range)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return DateTime.UtcNow.AddDays(-7);
        }

        var trimmed = range.Trim().ToLowerInvariant();
        if (trimmed.EndsWith("d") && int.TryParse(trimmed[..^1], out var days))
        {
            return DateTime.UtcNow.AddDays(-days);
        }

        if (trimmed.EndsWith("h") && int.TryParse(trimmed[..^1], out var hours))
        {
            return DateTime.UtcNow.AddHours(-hours);
        }

        return DateTime.UtcNow.AddDays(-7);
    }
}
