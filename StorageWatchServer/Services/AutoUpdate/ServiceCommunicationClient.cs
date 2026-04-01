using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatchServer.Services.AutoUpdate
{
    public class ServiceCommunicationClient
    {
        private const string PipeName = "StorageWatchAgentPipe";
        private const int TimeoutMilliseconds = 5000;
        private readonly ILogger<ServiceCommunicationClient> _logger;

        public ServiceCommunicationClient(ILogger<ServiceCommunicationClient> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<ServiceResponse> SendInstallUpdateRequestAsync(CancellationToken cancellationToken = default)
        {
            var request = new ServiceRequest { Command = "InstallUpdate" };
            return SendRequestAsync(request, cancellationToken);
        }

        public Task<ServiceResponse> SendRestartServiceRequestAsync(CancellationToken cancellationToken = default)
        {
            var request = new ServiceRequest { Command = "RestartService" };
            return SendRequestAsync(request, cancellationToken);
        }

        private async Task<ServiceResponse> SendRequestAsync(ServiceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await using var pipeClient = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeoutMilliseconds);

                await pipeClient.ConnectAsync(cts.Token);
                pipeClient.ReadMode = PipeTransmissionMode.Message;

                var requestJson = JsonSerializer.Serialize(request);
                var requestBytes = Encoding.UTF8.GetBytes(requestJson);
                await pipeClient.WriteAsync(requestBytes, 0, requestBytes.Length, cts.Token);
                await pipeClient.FlushAsync(cts.Token);

                var buffer = new byte[4096];
                using var responseStream = new MemoryStream();

                int bytesRead;
                while ((bytesRead = await pipeClient.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                {
                    responseStream.Write(buffer, 0, bytesRead);

                    if (pipeClient.IsMessageComplete)
                        break;
                }

                var responseJson = Encoding.UTF8.GetString(responseStream.ToArray());
                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    return new ServiceResponse
                    {
                        Success = false,
                        ErrorMessage = "Received empty response from agent service"
                    };
                }

                return JsonSerializer.Deserialize<ServiceResponse>(responseJson) ?? new ServiceResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to deserialize response"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AUTOUPDATE] Agent IPC request failed for command {Command}", request.Command);
                return new ServiceResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    public class ServiceRequest
    {
        public string Command { get; set; } = string.Empty;
    }

    public class ServiceResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
