using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using StorageWatch.Communication.Models;
using StorageWatch.Services.Logging;

namespace StorageWatch.Communication;

/// <summary>
/// Named Pipe server for local IPC communication between service and UI.
/// Handles incoming requests from the UI application.
/// </summary>
public class ServiceCommunicationServer : BackgroundService
{
    private const string PipeName = "StorageWatchAgentPipe";
    private readonly RollingFileLogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public ServiceCommunicationServer(RollingFileLogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Log("[IPC] Starting Named Pipe server...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(stoppingToken);
                _logger.Log("[IPC] Client connected");

                // Handle the connection in a separate task to allow new connections
                _ = Task.Run(async () => await HandleClientAsync(pipeServer, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.Log($"[IPC ERROR] Named Pipe server error: {ex.Message}");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.Log("[IPC] Named Pipe server stopped");
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(pipeServer, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            var requestJson = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                _logger.Log("[IPC] Received empty request");
                return;
            }

            _logger.Log($"[IPC] Received request: {requestJson}");

            var request = JsonSerializer.Deserialize<ServiceRequest>(requestJson);
            if (request == null)
            {
                _logger.Log("[IPC ERROR] Failed to deserialize request");
                await SendErrorResponse(writer, "Invalid request format");
                return;
            }

            var response = await ProcessRequestAsync(request, cancellationToken);
            var responseJson = JsonSerializer.Serialize(response);

            await writer.WriteLineAsync(responseJson);
            _logger.Log($"[IPC] Sent response for {request.Command}");
        }
        catch (Exception ex)
        {
            _logger.Log($"[IPC ERROR] Error handling client: {ex.Message}");
        }
        finally
        {
            try
            {
                if (pipeServer.IsConnected)
                    pipeServer.Disconnect();
            }
            catch { }
        }
    }

    private async Task<ServiceResponse> ProcessRequestAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return request.Command switch
            {
                "GetStatus" => await HandleGetStatusAsync(cancellationToken),
                "GetLogs" => await HandleGetLogsAsync(request, cancellationToken),
                "GetConfig" => await HandleGetConfigAsync(cancellationToken),
                "ValidateConfig" => await HandleValidateConfigAsync(cancellationToken),
                "TestAlertSenders" => await HandleTestAlertSendersAsync(cancellationToken),
                "GetPluginStatus" => await HandleGetPluginStatusAsync(cancellationToken),
                "GetLocalData" => await HandleGetLocalDataAsync(request, cancellationToken),
                _ => new ServiceResponse
                {
                    Success = false,
                    ErrorMessage = $"Unknown command: {request.Command}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.Log($"[IPC ERROR] Error processing {request.Command}: {ex.Message}");
            return new ServiceResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ServiceResponse> HandleGetStatusAsync(CancellationToken cancellationToken)
    {
        // Get service status from the application lifetime
        var status = new ServiceStatusInfo
        {
            State = "Running",
            Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            LastExecutionTimestamp = DateTime.UtcNow,
            LastError = null
        };

        return new ServiceResponse
        {
            Success = true,
            Data = JsonSerializer.SerializeToElement(status)
        };
    }

    private async Task<ServiceResponse> HandleGetLogsAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var count = 100; // Default to last 100 lines
            if (request.Parameters?.TryGetProperty("count", out var countProp) == true)
            {
                count = countProp.GetInt32();
            }

            var logPath = Path.Combine(@"C:\ProgramData\StorageWatch\Logs", "service.log");
            var lines = await ReadLastLinesAsync(logPath, count, cancellationToken);

            return new ServiceResponse
            {
                Success = true,
                Data = JsonSerializer.SerializeToElement(lines)
            };
        }
        catch (Exception ex)
        {
            return new ServiceResponse
            {
                Success = false,
                ErrorMessage = $"Failed to read logs: {ex.Message}"
            };
        }
    }

    private async Task<ServiceResponse> HandleGetConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "StorageWatchConfig.json");
            var configJson = await File.ReadAllTextAsync(configPath, cancellationToken);

            return new ServiceResponse
            {
                Success = true,
                Data = JsonSerializer.SerializeToElement(new { ConfigPath = configPath, Content = configJson })
            };
        }
        catch (Exception ex)
        {
            return new ServiceResponse
            {
                Success = false,
                ErrorMessage = $"Failed to read config: {ex.Message}"
            };
        }
    }

    private async Task<ServiceResponse> HandleValidateConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "StorageWatchConfig.json");
            var validation = Config.JsonConfigLoader.Validate(configPath);

            var result = new ConfigValidationResult
            {
                IsValid = validation.IsValid,
                Errors = validation.Errors,
                Warnings = validation.Warnings
            };

            return new ServiceResponse
            {
                Success = true,
                Data = JsonSerializer.SerializeToElement(result)
            };
        }
        catch (Exception ex)
        {
            return new ServiceResponse
            {
                Success = false,
                ErrorMessage = $"Config validation failed: {ex.Message}"
            };
        }
    }

    private async Task<ServiceResponse> HandleTestAlertSendersAsync(CancellationToken cancellationToken)
    {
        // This would trigger a test alert through the plugin manager
        // Implementation depends on having access to AlertSenderPluginManager
        return new ServiceResponse
        {
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { Message = "Test alert functionality not yet implemented" })
        };
    }

    private async Task<ServiceResponse> HandleGetPluginStatusAsync(CancellationToken cancellationToken)
    {
        // This would return the status of all loaded alert sender plugins
        return new ServiceResponse
        {
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { Plugins = new List<object>() })
        };
    }

    private async Task<ServiceResponse> HandleGetLocalDataAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        // This would query local SQLite database for trends
        return new ServiceResponse
        {
            Success = true,
            Data = JsonSerializer.SerializeToElement(new { Message = "Local data query not yet implemented" })
        };
    }

    private async Task SendErrorResponse(StreamWriter writer, string error)
    {
        var response = new ServiceResponse
        {
            Success = false,
            ErrorMessage = error
        };
        var json = JsonSerializer.Serialize(response);
        await writer.WriteLineAsync(json);
    }

    private async Task<List<string>> ReadLastLinesAsync(string filePath, int lineCount, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return new List<string>();

        var lines = new List<string>();

        // Read the file in a way that doesn't lock it
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream);

        var allLines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            allLines.Add(line);
        }

        // Return last N lines
        return allLines.TakeLast(lineCount).ToList();
    }
}
