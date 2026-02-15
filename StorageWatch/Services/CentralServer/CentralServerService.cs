/// <summary>
/// Central Server Service
/// 
/// This service hosts a lightweight HTTP server using HttpListener that accepts
/// disk space data from agents. It manages the server lifecycle and provides
/// the HTTP API endpoints for data submission.
/// </summary>

using StorageWatch.Config;
using StorageWatch.Data;
using StorageWatch.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StorageWatch.Models;

namespace StorageWatch.Services.CentralServer
{
    /// <summary>
    /// Manages the central server HTTP API and hosts it within the Windows Service.
    /// </summary>
    public class CentralServerService
    {
        private readonly CentralServerConfig _config;
        private readonly RollingFileLogger _logger;
        private readonly CentralServerRepository _repository;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;

        /// <summary>
        /// Initializes a new instance of the CentralServerService class.
        /// </summary>
        /// <param name="config">The central server configuration.</param>
        /// <param name="logger">The logger for recording server operations.</param>
        /// <param name="repository">The data repository for persisting agent data.</param>
        public CentralServerService(
            CentralServerConfig config,
            RollingFileLogger logger,
            CentralServerRepository repository)
        {
            _config = config;
            _logger = logger;
            _repository = repository;
        }

        /// <summary>
        /// Starts the central server HTTP listener.
        /// Must be called during service startup.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for shutdown.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Log($"[CentralServer] Starting central server on port {_config.Port}");

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_config.Port}/");
                _listener.Start();

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _listenerTask = ListenAsync(_cts.Token);

                _logger.Log($"[CentralServer] Central server started successfully on port {_config.Port}");
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer ERROR] Failed to start central server: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Main listener loop that accepts and processes HTTP requests.
        /// </summary>
        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener?.IsListening == true)
                {
                    HttpListenerContext context;
                    try
                    {
                        var getContextTask = _listener.GetContextAsync();
                        var completedTask = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, cancellationToken));
                        
                        if (completedTask == getContextTask)
                        {
                            context = getContextTask.Result;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    _ = ProcessRequestAsync(context);
                }
            }
            catch (ObjectDisposedException)
            {
                // HttpListener disposed, listener is stopping
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer ERROR] Listener error: {ex}");
            }
        }

        /// <summary>
        /// Processes individual HTTP requests.
        /// </summary>
        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                _logger.Log($"[CentralServer API] {request.HttpMethod} {request.RawUrl} from {request.RemoteEndPoint}");

                if (request.HttpMethod == "POST" && request.RawUrl == "/api/logs/disk-space")
                {
                    await HandleDiskSpaceLogAsync(context);
                }
                else if (request.HttpMethod == "GET" && request.RawUrl == "/api/health")
                {
                    await HandleHealthCheckAsync(context);
                }
                else if (request.HttpMethod == "GET" && request.RawUrl == "/api/logs/latest")
                {
                    await HandleGetLatestEntriesAsync(context);
                }
                else
                {
                    _logger.Log($"[CentralServer] Unhandled request: {request.HttpMethod} {request.RawUrl}");
                    response.StatusCode = 404;
                    await WriteJsonResponseAsync(response, new ApiResponse 
                    { 
                        Success = false, 
                        Message = "Endpoint not found" 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer ERROR] Request processing error: {ex}");
                try
                {
                    context.Response.StatusCode = 500;
                }
                catch { }
            }
            finally
            {
                context.Response.Close();
            }
        }

        /// <summary>
        /// Handles POST requests to /api/logs/disk-space.
        /// </summary>
        private async Task HandleDiskSpaceLogAsync(HttpListenerContext context)
        {
            try
            {
                _logger.Log($"[CentralServer API] Received disk space log request from {context.Request.RemoteEndPoint}");

                string bodyText;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    bodyText = await reader.ReadToEndAsync();
                }

                var entry = JsonSerializer.Deserialize<DiskSpaceLogEntry>(bodyText);

                if (entry == null || string.IsNullOrEmpty(entry.AgentMachineName) || string.IsNullOrEmpty(entry.DriveLetter))
                {
                    _logger.Log("[CentralServer API] Invalid request: missing required fields");
                    context.Response.StatusCode = 400;
                    await WriteJsonResponseAsync(context.Response, new ApiResponse
                    {
                        Success = false,
                        Message = "Missing required fields: AgentMachineName, DriveLetter"
                    });
                    return;
                }

                await _repository.InsertDiskSpaceLogAsync(
                    entry.AgentMachineName,
                    entry.DriveLetter,
                    entry.TotalSpaceGb,
                    entry.UsedSpaceGb,
                    entry.FreeSpaceGb,
                    entry.PercentFree,
                    entry.CollectionTimeUtc);

                _logger.Log($"[CentralServer API] Stored disk space log: {entry.AgentMachineName}/{entry.DriveLetter}");

                context.Response.StatusCode = 200;
                await WriteJsonResponseAsync(context.Response, new ApiResponse
                {
                    Success = true,
                    Message = "Disk space log received and stored successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer API ERROR] Failed to process disk space log: {ex}");
                context.Response.StatusCode = 500;
                await WriteJsonResponseAsync(context.Response, new ApiResponse
                {
                    Success = false,
                    Message = "Failed to process request"
                });
            }
        }

        /// <summary>
        /// Handles GET requests to /api/health.
        /// </summary>
        private async Task HandleHealthCheckAsync(HttpListenerContext context)
        {
            _logger.Log("[CentralServer API] Health check requested");
            context.Response.StatusCode = 200;
            await WriteJsonResponseAsync(context.Response, new ApiResponse
            {
                Success = true,
                Message = "Central server is healthy",
                Data = new { ServerId = _config.ServerId, Port = _config.Port }
            });
        }

        /// <summary>
        /// Handles GET requests to /api/logs/latest.
        /// </summary>
        private async Task HandleGetLatestEntriesAsync(HttpListenerContext context)
        {
            try
            {
                _logger.Log("[CentralServer API] Latest entries requested");
                var entries = await _repository.GetLatestEntriesAsync();
                var count = await _repository.GetEntryCountAsync();

                context.Response.StatusCode = 200;
                await WriteJsonResponseAsync(context.Response, new ApiResponse
                {
                    Success = true,
                    Message = "Latest entries retrieved",
                    Data = new { TotalCount = count, Entries = entries }
                });
            }
            catch (Exception ex)
            {
                _logger.Log($"[CentralServer API ERROR] Failed to retrieve latest entries: {ex}");
                context.Response.StatusCode = 500;
                await WriteJsonResponseAsync(context.Response, new ApiResponse
                {
                    Success = false,
                    Message = "Failed to retrieve entries"
                });
            }
        }

        /// <summary>
        /// Writes a JSON response to the HTTP response stream.
        /// </summary>
        private async Task WriteJsonResponseAsync(HttpListenerResponse response, ApiResponse data)
        {
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Stops the central server.
        /// </summary>
        public async Task StopAsync()
        {
            _logger.Log("[CentralServer] Stopping central server");
            _cts?.Cancel();
            
            if (_listenerTask != null)
            {
                try
                {
                    await _listenerTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }

            _listener?.Stop();
            _cts?.Dispose();
        }
    }
}
