using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchUI.Communication;

/// <summary>
/// Client for communicating with the StorageWatch service via Named Pipes.
/// </summary>
public class ServiceCommunicationClient
{
    private const string PipeName = "StorageWatchServicePipe";
    private const int TimeoutMilliseconds = 5000;
    private const int MaxRetries = 3;

    /// <summary>
    /// Sends a request to the service and returns the response.
    /// </summary>
    public async Task<ServiceResponse> SendRequestAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await SendRequestInternalAsync(request, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                lastException = ex;
                if (attempt < MaxRetries)
                {
                    await Task.Delay(500 * attempt, cancellationToken); // Exponential backoff
                }
            }
            catch (Exception ex)
            {
                // Don't retry on non-timeout errors
                return new ServiceResponse
                {
                    Success = false,
                    ErrorMessage = $"Communication error: {ex.Message}"
                };
            }
        }

        return new ServiceResponse
        {
            Success = false,
            ErrorMessage = $"Failed to connect after {MaxRetries} attempts. Is the service running?"
        };
    }

    private async Task<ServiceResponse> SendRequestInternalAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        await using var pipeClient = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeoutMilliseconds);

        await pipeClient.ConnectAsync(cts.Token);

        using var writer = new StreamWriter(pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipeClient, Encoding.UTF8, leaveOpen: true);

        var requestJson = JsonSerializer.Serialize(request);
        await writer.WriteLineAsync(requestJson.AsMemory(), cts.Token);

        var responseJson = await reader.ReadLineAsync(cts.Token);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new ServiceResponse
            {
                Success = false,
                ErrorMessage = "Received empty response from service"
            };
        }

        var response = JsonSerializer.Deserialize<ServiceResponse>(responseJson);
        return response ?? new ServiceResponse
        {
            Success = false,
            ErrorMessage = "Failed to deserialize response"
        };
    }

    /// <summary>
    /// Gets the current service status.
    /// </summary>
    public async Task<ServiceStatusInfo?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest { Command = "GetStatus" };
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
            return null;

        return JsonSerializer.Deserialize<ServiceStatusInfo>(response.Data.Value.GetRawText());
    }

    /// <summary>
    /// Gets the last N log entries from the service.
    /// </summary>
    public async Task<List<string>?> GetLogsAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        var parameters = JsonSerializer.SerializeToElement(new { count });
        var request = new ServiceRequest 
        { 
            Command = "GetLogs",
            Parameters = parameters
        };
        
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
            return null;

        return JsonSerializer.Deserialize<List<string>>(response.Data.Value.GetRawText());
    }

    /// <summary>
    /// Gets the current configuration from the service.
    /// </summary>
    public async Task<(string ConfigPath, string Content)?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest { Command = "GetConfig" };
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
            return null;

        var data = JsonSerializer.Deserialize<JsonElement>(response.Data.Value.GetRawText());
        var configPath = data.GetProperty("ConfigPath").GetString() ?? string.Empty;
        var content = data.GetProperty("Content").GetString() ?? string.Empty;

        return (configPath, content);
    }

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    public async Task<ConfigValidationResult?> ValidateConfigAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest { Command = "ValidateConfig" };
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
            return null;

        return JsonSerializer.Deserialize<ConfigValidationResult>(response.Data.Value.GetRawText());
    }

    /// <summary>
    /// Tests alert senders (sends a test alert).
    /// </summary>
    public async Task<ServiceResponse> TestAlertSendersAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest { Command = "TestAlertSenders" };
        return await SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets the status of all loaded plugins.
    /// </summary>
    public async Task<List<PluginStatusInfo>?> GetPluginStatusAsync(CancellationToken cancellationToken = default)
    {
        var request = new ServiceRequest { Command = "GetPluginStatus" };
        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
            return null;

        var data = JsonSerializer.Deserialize<JsonElement>(response.Data.Value.GetRawText());
        var plugins = data.GetProperty("Plugins");
        return JsonSerializer.Deserialize<List<PluginStatusInfo>>(plugins.GetRawText());
    }

    /// <summary>
    /// Queries local data (trends, usage, etc.).
    /// </summary>
    public async Task<JsonElement?> GetLocalDataAsync(LocalDataQuery query, CancellationToken cancellationToken = default)
    {
        var parameters = JsonSerializer.SerializeToElement(query);
        var request = new ServiceRequest 
        { 
            Command = "GetLocalData",
            Parameters = parameters
        };

        var response = await SendRequestAsync(request, cancellationToken);

        if (!response.Success || response.Data == null)
            return null;

        return response.Data;
    }
}

// Models (these should match the service-side models)

public class ServiceRequest
{
    public string Command { get; set; } = string.Empty;
    public JsonElement? Parameters { get; set; }
}

public class ServiceResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public JsonElement? Data { get; set; }
}

public class ServiceStatusInfo
{
    public string State { get; set; } = "Unknown";
    public TimeSpan Uptime { get; set; }
    public DateTime LastExecutionTimestamp { get; set; }
    public string? LastError { get; set; }
}

public class ConfigValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class PluginStatusInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Health { get; set; } = "Unknown";
    public string? LastError { get; set; }
}

public class LocalDataQuery
{
    public string QueryType { get; set; } = string.Empty;
    public string? DriveName { get; set; }
    public int DaysBack { get; set; } = 7;
    public int Limit { get; set; } = 1000;
}
