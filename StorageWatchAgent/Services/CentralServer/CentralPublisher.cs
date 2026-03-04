/// <summary>
/// Central Publisher
/// 
/// This service publishes new raw drive rows from the local SQLite database to the Central Server.
/// It runs on a fixed interval, tracks the last successful publish via last_central_run.txt,
/// and handles offline/online state transitions gracefully.
/// </summary>

using StorageWatch.Config.Options;
using StorageWatch.Services.Logging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace StorageWatch.Services.CentralServer
{
    /// <summary>
    /// Publishes new drive rows to the Central Server in batches.
    /// </summary>
    public class CentralPublisher : BackgroundService
    {
        private readonly IOptionsMonitor<StorageWatchOptions> _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RollingFileLogger _logger;
        private readonly string _lastCentralRunPath;
        private bool _isServerReachable = true;
        private const int BatchSize = 100;

        /// <summary>
        /// Initializes a new instance of the CentralPublisher class.
        /// </summary>
        public CentralPublisher(
            IOptionsMonitor<StorageWatchOptions> options,
            IHttpClientFactory httpClientFactory,
            RollingFileLogger logger)
        {
            _options = options;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var agentDirectory = Path.Combine(programData, "StorageWatch", "Agent");
            Directory.CreateDirectory(agentDirectory);
            _lastCentralRunPath = Path.Combine(agentDirectory, "last_central_run.txt");
        }

        /// <summary>
        /// Main execution loop that publishes new rows to the Central Server.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var centralOptions = _options.CurrentValue.CentralServer;
                var interval = TimeSpan.FromSeconds(centralOptions.CheckIntervalSeconds);

                try
                {
                    // Only publish if we have a valid server URL
                    if (!string.IsNullOrWhiteSpace(centralOptions.ServerUrl))
                    {
                        await PublishNewRowsAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"[CENTRAL PUBLISHER ERROR] Unexpected error: {ex}");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }

        /// <summary>
        /// Publishes all new rows since last_central_run to the Central Server.
        /// </summary>
        private async Task PublishNewRowsAsync(CancellationToken stoppingToken)
        {
            var centralOptions = _options.CurrentValue.CentralServer;
            var dbOptions = _options.CurrentValue.Database;

            // Load the last publish timestamp
            DateTime? lastCentralRun = LoadLastCentralRun();

            // Query SQLite for new rows
            var newRows = await QueryNewRowsAsync(dbOptions.ConnectionString, lastCentralRun, stoppingToken);

            // If there are no new rows, exit silently
            if (newRows.Count == 0)
            {
                return;
            }

            // Batch and publish rows
            var batches = BatchRows(newRows, BatchSize);
            DateTime? latestTimestamp = null;

            foreach (var batch in batches)
            {
                bool success = await PublishBatchAsync(batch, centralOptions, stoppingToken);

                if (!success)
                {
                    // Server is unreachable; stop processing batches and enter offline mode
                    if (_isServerReachable)
                    {
                        _logger.Log("[CENTRAL PUBLISHER] Central server unreachable… entering offline mode.");
                        _isServerReachable = false;
                    }
                    break;
                }

                // Update the latest timestamp after each successful batch
                latestTimestamp = batch.Max(r => r.Timestamp);
            }

            // If we successfully published at least one batch, update last_central_run.txt
            if (latestTimestamp.HasValue)
            {
                if (!_isServerReachable)
                {
                    _logger.Log("[CENTRAL PUBLISHER] Central server reachable… resuming publishing.");
                    _isServerReachable = true;
                }

                SaveLastCentralRun(latestTimestamp.Value);
            }
        }

        /// <summary>
        /// Queries the local SQLite database for rows newer than the specified timestamp.
        /// </summary>
        private async Task<List<RawDriveRow>> QueryNewRowsAsync(
            string connectionString,
            DateTime? lastCentralRun,
            CancellationToken stoppingToken)
        {
            var rows = new List<RawDriveRow>();

            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync(stoppingToken);

                string sql;
                if (lastCentralRun.HasValue)
                {
                    sql = @"
                        SELECT MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc
                        FROM DiskSpaceLog
                        WHERE CollectionTimeUtc > @lastRun
                        ORDER BY CollectionTimeUtc ASC
                    ";
                }
                else
                {
                    // If we've never published before, get all rows
                    sql = @"
                        SELECT MachineName, DriveLetter, TotalSpaceGB, UsedSpaceGB, FreeSpaceGB, PercentFree, CollectionTimeUtc
                        FROM DiskSpaceLog
                        ORDER BY CollectionTimeUtc ASC
                    ";
                }

                using var command = new SqliteCommand(sql, connection);
                if (lastCentralRun.HasValue)
                {
                    command.Parameters.AddWithValue("@lastRun", lastCentralRun.Value);
                }

                using var reader = await command.ExecuteReaderAsync(stoppingToken);
                while (await reader.ReadAsync(stoppingToken))
                {
                    rows.Add(new RawDriveRow
                    {
                        MachineName = reader.GetString(0),
                        DriveLetter = reader.GetString(1),
                        TotalSpaceGb = reader.GetDouble(2),
                        UsedSpaceGb = reader.GetDouble(3),
                        FreeSpaceGb = reader.GetDouble(4),
                        PercentFree = reader.GetDouble(5),
                        Timestamp = reader.GetDateTime(6)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"[CENTRAL PUBLISHER ERROR] Failed to query new rows: {ex}");
            }

            return rows;
        }

        /// <summary>
        /// Splits rows into batches of the specified size.
        /// </summary>
        private List<List<RawDriveRow>> BatchRows(List<RawDriveRow> rows, int batchSize)
        {
            var batches = new List<List<RawDriveRow>>();

            for (int i = 0; i < rows.Count; i += batchSize)
            {
                batches.Add(rows.Skip(i).Take(batchSize).ToList());
            }

            return batches;
        }

        /// <summary>
        /// Publishes a single batch to the Central Server.
        /// </summary>
        private async Task<bool> PublishBatchAsync(
            List<RawDriveRow> batch,
            CentralServerOptions options,
            CancellationToken stoppingToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                var endpoint = $"{options.ServerUrl.TrimEnd('/')}/api/agent/report";

                var requestBody = new
                {
                    machineName = Environment.MachineName,
                    rows = batch
                };

                var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Add API key header if configured
                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", options.ApiKey);
                }

                var response = await httpClient.PostAsync(endpoint, content, stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.Log($"[CENTRAL PUBLISHER] Published batch of {batch.Count} rows to central server.");
                    return true;
                }
                else
                {
                    _logger.Log($"[CENTRAL PUBLISHER ERROR] Server returned status {response.StatusCode}.");
                    return false;
                }
            }
            catch (HttpRequestException)
            {
                // Server is unreachable; suppress further errors until it becomes reachable again
                return false;
            }
            catch (Exception ex)
            {
                _logger.Log($"[CENTRAL PUBLISHER ERROR] Failed to publish batch: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Loads the timestamp of the last successful publish from disk.
        /// </summary>
        private DateTime? LoadLastCentralRun()
        {
            try
            {
                if (!File.Exists(_lastCentralRunPath))
                    return null;

                string text = File.ReadAllText(_lastCentralRunPath).Trim();

                if (DateTime.TryParse(text, out var dt))
                    return dt;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Saves the timestamp of the last successful publish to disk.
        /// </summary>
        private void SaveLastCentralRun(DateTime dt)
        {
            try
            {
                File.WriteAllText(_lastCentralRunPath, dt.ToString("o"));
            }
            catch (Exception ex)
            {
                _logger.Log($"[CENTRAL PUBLISHER ERROR] Failed to save last_central_run.txt: {ex}");
            }
        }
    }

    /// <summary>
    /// Represents a raw drive row from the SQLite database.
    /// </summary>
    public class RawDriveRow
    {
        public string MachineName { get; set; } = string.Empty;
        public string DriveLetter { get; set; } = string.Empty;
        public double TotalSpaceGb { get; set; }
        public double UsedSpaceGb { get; set; }
        public double FreeSpaceGb { get; set; }
        public double PercentFree { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
