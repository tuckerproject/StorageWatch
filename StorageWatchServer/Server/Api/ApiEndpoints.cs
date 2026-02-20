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
        group.MapPost("/agent/report", async (AgentReportRequest request, ServerRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(request.MachineName) || request.Drives.Count == 0)
            {
                return Results.BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "MachineName and at least one drive are required."
                });
            }

            var reportTime = request.CollectionTimeUtc == default ? DateTime.UtcNow : request.CollectionTimeUtc;
            var machineId = await repository.UpsertMachineAsync(request.MachineName, reportTime);

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
        });

        group.MapGet("/machines", async (ServerRepository repository, MachineStatusService statusService) =>
        {
            var machines = await repository.GetMachinesAsync();
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
        });

        group.MapGet("/machines/{id:int}", async (int id, ServerRepository repository, MachineStatusService statusService) =>
        {
            var machine = await repository.GetMachineAsync(id);
            if (machine == null)
            {
                return Results.NotFound();
            }

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
        });

        group.MapGet("/machines/{id:int}/history", async (int id, string drive, string range, ServerRepository repository) =>
        {
            if (string.IsNullOrWhiteSpace(drive))
            {
                return Results.BadRequest(new ApiResponse
                {
                    Success = false,
                    Message = "Drive letter is required."
                });
            }

            var startUtc = ParseRange(range);
            var history = await repository.GetDiskHistoryAsync(id, drive, startUtc);
            return Results.Ok(history);
        });

        group.MapGet("/alerts", async (ServerRepository repository) =>
        {
            var alerts = await repository.GetAlertsAsync();
            return Results.Ok(alerts);
        });

        group.MapGet("/settings", async (ServerRepository repository) =>
        {
            var settings = await repository.GetSettingsAsync();
            return Results.Ok(settings);
        });
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
