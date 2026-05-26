using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using StorageWatch.Shared.Update.Models;

namespace StorageWatchServer.Services;

public interface IAgentUnifiedUpdateRelayClient
{
    Task<UnifiedUpdateStatusInfo?> GetUnifiedStatusAsync(CancellationToken cancellationToken);
    Task<UnifiedUpdateInstallResult?> StartUnifiedInstallAsync(UnifiedInstallUpdateRequest request, CancellationToken cancellationToken);
    Task<UnifiedUpdateProgressInfo?> GetUnifiedInstallProgressAsync(CancellationToken cancellationToken);
    Task<UnifiedUpdateInstallResult?> GetLastUnifiedInstallResultAsync(CancellationToken cancellationToken);
}

public class AgentUnifiedUpdateRelayClient : IAgentUnifiedUpdateRelayClient
{
    private const string PipeName = "StorageWatchAgentPipe";
    private const int TimeoutMilliseconds = 5000;
    private readonly ILogger<AgentUnifiedUpdateRelayClient> _logger;

    public AgentUnifiedUpdateRelayClient(ILogger<AgentUnifiedUpdateRelayClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UnifiedUpdateStatusInfo?> GetUnifiedStatusAsync(CancellationToken cancellationToken)
    {
        return await SendAndDeserializeAsync<UnifiedUpdateStatusInfo>(UnifiedUpdateCommands.GetUnifiedUpdateStatus, parameters: null, cancellationToken);
    }

    public async Task<UnifiedUpdateInstallResult?> StartUnifiedInstallAsync(UnifiedInstallUpdateRequest request, CancellationToken cancellationToken)
    {
        return await SendAndDeserializeAsync<UnifiedUpdateInstallResult>(
            UnifiedUpdateCommands.StartUnifiedInstall,
            JsonSerializer.SerializeToElement(request),
            cancellationToken);
    }

    public async Task<UnifiedUpdateProgressInfo?> GetUnifiedInstallProgressAsync(CancellationToken cancellationToken)
    {
        return await SendAndDeserializeAsync<UnifiedUpdateProgressInfo>(UnifiedUpdateCommands.GetUnifiedInstallProgress, parameters: null, cancellationToken);
    }

    public async Task<UnifiedUpdateInstallResult?> GetLastUnifiedInstallResultAsync(CancellationToken cancellationToken)
    {
        return await SendAndDeserializeAsync<UnifiedUpdateInstallResult>(UnifiedUpdateCommands.GetLastUnifiedInstallResult, parameters: null, cancellationToken);
    }

    private async Task<T?> SendAndDeserializeAsync<T>(string command, JsonElement? parameters, CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendRequestAsync(command, parameters, cancellationToken);
            if (response == null || !response.Success || response.Data == null)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(response.Data.Value.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[WEB] Agent relay request failed for command {Command}", command);
            return default;
        }
    }

    private async Task<ServiceResponse?> SendRequestAsync(string command, JsonElement? parameters, CancellationToken cancellationToken)
    {
        await using var pipeClient = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeoutMilliseconds);

        await pipeClient.ConnectAsync(timeout.Token);
        pipeClient.ReadMode = PipeTransmissionMode.Message;

        var request = new ServiceRequest
        {
            Command = command,
            Parameters = parameters
        };

        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        await pipeClient.WriteAsync(requestBytes, timeout.Token);
        await pipeClient.FlushAsync(timeout.Token);

        var buffer = new byte[4096];
        using var responseStream = new MemoryStream();

        int bytesRead;
        while ((bytesRead = await pipeClient.ReadAsync(buffer, timeout.Token)) > 0)
        {
            responseStream.Write(buffer, 0, bytesRead);
            if (pipeClient.IsMessageComplete)
            {
                break;
            }
        }

        if (responseStream.Length == 0)
            return null;

        var responseJson = Encoding.UTF8.GetString(responseStream.ToArray());
        return JsonSerializer.Deserialize<ServiceResponse>(responseJson);
    }

    private class ServiceRequest
    {
        public string Command { get; set; } = string.Empty;
        public JsonElement? Parameters { get; set; }
    }

    private class ServiceResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public JsonElement? Data { get; set; }
    }
}
