using Microsoft.Extensions.Configuration;
using StorageWatchUI.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace StorageWatchUI.Services;

/// <summary>
/// Provides data access to the central server via REST API.
/// </summary>
public class CentralDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string? _serverUrl;
    private readonly bool _isEnabled;

    public CentralDataProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        // Check if central server is configured
        var centralSection = configuration.GetSection("StorageWatch:CentralServer");
        _isEnabled = centralSection.GetValue<bool>("Enabled");
        _serverUrl = centralSection.GetValue<string>("ServerUrl");

        if (!string.IsNullOrEmpty(_serverUrl))
        {
            _httpClient.BaseAddress = new Uri(_serverUrl);
        }
    }

    public bool IsEnabled => _isEnabled && !string.IsNullOrEmpty(_serverUrl);

    /// <summary>
    /// Checks if the central server is reachable.
    /// </summary>
    public async Task<bool> CheckHealthAsync()
    {
        if (!IsEnabled)
            return false;

        try
        {
            var response = await _httpClient.GetAsync("/api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the latest entries from all machines reporting to the central server.
    /// </summary>
    public async Task<List<MachineStatus>> GetAllMachineStatusAsync()
    {
        var machines = new List<MachineStatus>();

        if (!IsEnabled)
            return machines;

        try
        {
            var response = await _httpClient.GetAsync("/api/logs/latest");
            if (!response.IsSuccessStatusCode)
                return machines;

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse?.Success == true && apiResponse.Data != null)
            {
                var data = apiResponse.Data as JsonElement?;
                if (data.HasValue && data.Value.TryGetProperty("Entries", out var entriesElement))
                {
                    var entriesText = entriesElement.GetString();
                    if (!string.IsNullOrEmpty(entriesText))
                    {
                        machines = ParseEntries(entriesText);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching machine status: {ex}");
        }

        return machines;
    }

    private List<MachineStatus> ParseEntries(string entriesText)
    {
        var machineDict = new Dictionary<string, MachineStatus>();

        var lines = entriesText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length < 4)
                continue;

            var machineName = parts[0];
            var driveLetter = parts[1];
            var percentFreeText = parts[2].TrimEnd('%');
            var collectionTimeText = parts[3];

            if (!double.TryParse(percentFreeText, out var percentFree))
                continue;

            if (!DateTime.TryParse(collectionTimeText, out var collectionTime))
                continue;

            if (!machineDict.ContainsKey(machineName))
            {
                machineDict[machineName] = new MachineStatus
                {
                    MachineName = machineName,
                    LastReportTime = collectionTime
                };
            }

            var machine = machineDict[machineName];
            machine.Disks.Add(new DiskInfo
            {
                DriveName = driveLetter,
                Status = DiskInfo.CalculateStatus(percentFree),
                LastUpdated = collectionTime
            });

            // Update last report time if this entry is newer
            if (collectionTime > machine.LastReportTime)
            {
                machine.LastReportTime = collectionTime;
            }
        }

        return machineDict.Values.ToList();
    }

    private class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
    }
}
