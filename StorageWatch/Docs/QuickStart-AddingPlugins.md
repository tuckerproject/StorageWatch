# Quick Start: Adding a New Alert Sender Plugin

This guide walks you through adding a new alert sender plugin to StorageWatch in 5 simple steps.

---

## Example: Adding a Discord Alert Sender

### Step 1: Create the Options Class

Create `Config/Options/DiscordOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace StorageWatch.Config.Options
{
    public class DiscordOptions
    {
        public const string SectionKey = "Discord";
        
        public bool Enabled { get; set; } = false;
        
        [Url]
        public string WebhookUrl { get; set; } = string.Empty;
        
        public string Username { get; set; } = "StorageWatch Bot";
    }
}
```

### Step 2: Implement the Alert Sender

Create `Services/Alerting/DiscordAlertSender.cs`:

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;
using StorageWatch.Config.Options;
using StorageWatch.Models.Plugins;
using StorageWatch.Services.Logging;

namespace StorageWatch.Services.Alerting
{
    [AlertSenderPlugin("Discord", 
        Description = "Sends alerts to Discord via webhooks", 
        Version = "1.0.0")]
    public class DiscordAlertSender : AlertSenderBase, IDisposable
    {
        private readonly DiscordOptions _options;
        private readonly HttpClient _httpClient;

        public DiscordAlertSender(DiscordOptions options, RollingFileLogger logger)
            : base(logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = new HttpClient();
        }

        public override string Name => "Discord";

        protected override bool IsEnabled() => _options.Enabled;

        protected override async Task SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            var payload = new
            {
                username = _options.Username,
                content = message
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_options.WebhookUrl, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Log($"[Discord ERROR] Status: {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        }

        public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled()) return false;
            
            if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
            {
                Logger.Log("[Discord] Health check failed: Missing webhook URL");
                return false;
            }
            
            return true;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
```

### Step 3: Add to AlertingOptions

Update `Config/Options/StorageWatchOptions.cs`:

```csharp
public class AlertingOptions
{
    // ...existing properties...
    
    [Required]
    public DiscordOptions Discord { get; set; } = new();
}
```

### Step 4: Register with DI

Update `Program.cs`:

```csharp
// In ConfigureServices section, after other plugin registrations:

// Register Discord plugin
services.AddTransient<DiscordAlertSender>(sp => 
    new DiscordAlertSender(
        options.Alerting.Discord, 
        sp.GetRequiredService<Services.Logging.RollingFileLogger>()));

services.AddTransient<IAlertSender, DiscordAlertSender>(sp =>
    new DiscordAlertSender(
        options.Alerting.Discord,
        sp.GetRequiredService<Services.Logging.RollingFileLogger>()));
```

### Step 5: Add Configuration

Update `StorageWatchConfig.json`:

```json
{
  "StorageWatch": {
    "Alerting": {
      "EnableNotifications": true,
      "Discord": {
        "Enabled": true,
        "WebhookUrl": "https://discord.com/api/webhooks/YOUR_WEBHOOK_ID/YOUR_WEBHOOK_TOKEN",
        "Username": "Storage Alert Bot"
      }
    }
  }
}
```

---

## Testing Your Plugin

### Create Unit Test

Create `StorageWatch.Tests/UnitTests/DiscordAlertSenderTests.cs`:

```csharp
using FluentAssertions;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;

namespace StorageWatch.Tests.UnitTests
{
    public class DiscordAlertSenderTests
    {
        private readonly RollingFileLogger _logger;

        public DiscordAlertSenderTests()
        {
            _logger = new RollingFileLogger(Path.GetTempFileName());
        }

        [Fact]
        public void DiscordAlertSender_HasCorrectName()
        {
            var options = new DiscordOptions { Enabled = true, WebhookUrl = "https://test.url" };
            var sender = new DiscordAlertSender(options, _logger);

            sender.Name.Should().Be("Discord");
        }

        [Fact]
        public async Task DiscordAlertSender_WithDisabled_DoesNotSend()
        {
            var options = new DiscordOptions { Enabled = false };
            var sender = new DiscordAlertSender(options, _logger);
            var status = new DiskStatus { DriveName = "C:", TotalSpaceGb = 100, FreeSpaceGb = 5 };

            Func<Task> act = async () => await sender.SendAlertAsync(status);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task DiscordAlertSender_HealthCheck_ReturnsFalse_WhenMisconfigured()
        {
            var options = new DiscordOptions { Enabled = true, WebhookUrl = "" };
            var sender = new DiscordAlertSender(options, _logger);

            var isHealthy = await sender.HealthCheckAsync();

            isHealthy.Should().BeFalse();
        }
    }
}
```

---

## Verify Plugin Discovery

Run your application with `EnableStartupLogging` set to `true` and check the logs:

```
[STARTUP] Config loaded from JSON
[STARTUP] SQLite database initialized
[PLUGIN MANAGER] Found 3 registered alert sender plugin(s).
[PLUGIN MANAGER] Enabling plugin: SMTP
[PLUGIN MANAGER] Plugin disabled: GroupMe
[PLUGIN MANAGER] Enabling plugin: Discord
[STARTUP] Loaded 2 alert sender plugin(s)
[STARTUP]   - SMTP
[STARTUP]   - Discord
```

---

## Common Plugin Patterns

### REST API Webhook

```csharp
protected override async Task SendMessageAsync(string message, CancellationToken cancellationToken)
{
    var payload = new { text = message };
    var json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    
    await _httpClient.PostAsync(_options.WebhookUrl, content, cancellationToken);
}
```

### Custom Message Formatting

```csharp
protected override string FormatMessage(DiskStatus status)
{
    return $"🚨 **ALERT** 🚨\n" +
           $"Server: {Environment.MachineName}\n" +
           $"Drive: {status.DriveName}\n" +
           $"Free Space: {status.FreeSpaceGb:F2} GB ({status.PercentFree:F2}%)\n" +
           $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
}
```

### Authentication Headers

```csharp
protected override async Task SendMessageAsync(string message, CancellationToken cancellationToken)
{
    var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
    {
        Content = new StringContent(message, Encoding.UTF8, "application/json")
    };
    
    request.Headers.Add("Authorization", $"Bearer {_options.ApiToken}");
    
    await _httpClient.SendAsync(request, cancellationToken);
}
```

---

## Troubleshooting

**Plugin not appearing in logs:**
- Verify the `[AlertSenderPlugin]` attribute is present
- Check that the class implements `IAlertSender`
- Ensure the plugin is registered in `Program.cs`

**Plugin not sending alerts:**
- Check `Enabled` is set to `true` in configuration
- Verify `EnableNotifications` is `true` globally
- Check service logs for error messages

**Health check failing:**
- Validate all required configuration fields are populated
- Test connectivity to external services manually
- Check firewall/network settings

---

## Next Steps

- Add validation for your options class
- Create integration tests
- Add custom message templates
- Implement retry logic for transient failures
- Add rate limiting if needed

---

For complete documentation, see [PluginArchitecture.md](PluginArchitecture.md).
