# StorageWatch Alert Sender Plugin Architecture

## Overview

StorageWatch uses a **plugin architecture** for alert delivery mechanisms. This allows the system to support multiple alerting backends (SMTP email, GroupMe, Slack, Teams, Discord, webhooks, SMS, etc.) in a clean, extensible manner.

---

## Architecture Components

### 1. **IAlertSender Interface**

All alert senders implement the `IAlertSender` interface:

```csharp
public interface IAlertSender
{
    string Name { get; }
    Task SendAlertAsync(DiskStatus status, CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
```

**Key Features:**
- `Name`: Unique identifier for the plugin (e.g., "SMTP", "GroupMe", "Slack")
- `SendAlertAsync`: Sends alerts with disk status information
- `HealthCheckAsync`: Optional health verification (connectivity, configuration, etc.)

### 2. **AlertSenderBase Abstract Class**

Base implementation providing common functionality:
- Message formatting from `DiskStatus`
- Error handling and logging
- Enabled/disabled checking

Plugins can inherit from this class to reduce boilerplate.

### 3. **Plugin Metadata**

```csharp
[AlertSenderPlugin("PluginId", Description = "...", Version = "1.0.0")]
public class MyAlertSender : AlertSenderBase
{
    // Implementation
}
```

The `AlertSenderPluginAttribute` marks a class as a plugin and provides metadata for discovery.

### 4. **AlertSenderPluginRegistry**

Discovers and registers plugins:
- Scans assemblies for `IAlertSender` implementations
- Maintains plugin metadata
- Supports manual registration

### 5. **AlertSenderPluginManager**

Manages plugin lifecycle:
- Resolves plugins from DI container
- Filters enabled plugins based on configuration
- Performs health checks
- Provides plugin information

---

## Built-In Plugins

### SMTP Email Plugin
**Plugin ID:** `SMTP`  
**Description:** Sends alerts via SMTP email  
**Configuration:**
```json
{
  "StorageWatch": {
    "Alerting": {
      "EnableNotifications": true,
      "Smtp": {
        "Enabled": true,
        "Host": "smtp.example.com",
        "Port": 587,
        "UseSsl": true,
        "Username": "alerts@example.com",
        "Password": "your-password",
        "FromAddress": "storagewatch@example.com",
        "ToAddress": "admin@example.com"
      }
    }
  }
}
```

### GroupMe Bot Plugin
**Plugin ID:** `GroupMe`  
**Description:** Sends alerts via GroupMe Bot API  
**Configuration:**
```json
{
  "StorageWatch": {
    "Alerting": {
      "EnableNotifications": true,
      "GroupMe": {
        "Enabled": true,
        "BotId": "your-groupme-bot-id"
      }
    }
  }
}
```

---

## Creating a New Plugin

### Step 1: Implement IAlertSender

Option A: Inherit from `AlertSenderBase` (recommended):

```csharp
using StorageWatch.Models;
using StorageWatch.Models.Plugins;
using StorageWatch.Services.Alerting;
using StorageWatch.Services.Logging;

[AlertSenderPlugin("Slack", Description = "Sends alerts to Slack via webhooks", Version = "1.0.0")]
public class SlackAlertSender : AlertSenderBase
{
    private readonly SlackOptions _options;
    private readonly HttpClient _httpClient;

    public SlackAlertSender(SlackOptions options, RollingFileLogger logger)
        : base(logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = new HttpClient();
    }

    public override string Name => "Slack";

    protected override bool IsEnabled() => _options.Enabled;

    protected override async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        var payload = new { text = message };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        await _httpClient.PostAsync(_options.WebhookUrl, content, cancellationToken);
    }

    public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled()) return false;
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl)) return false;
        // Optional: Ping Slack API
        return true;
    }
}
```

Option B: Implement `IAlertSender` directly (more control):

```csharp
[AlertSenderPlugin("Teams", Description = "Sends alerts to Microsoft Teams", Version = "1.0.0")]
public class TeamsAlertSender : IAlertSender
{
    private readonly TeamsOptions _options;
    private readonly HttpClient _httpClient;
    private readonly RollingFileLogger _logger;

    public TeamsAlertSender(TeamsOptions options, RollingFileLogger logger)
    {
        _options = options;
        _httpClient = new HttpClient();
        _logger = logger;
    }

    public string Name => "Teams";

    public async Task SendAlertAsync(DiskStatus status, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        try
        {
            var message = FormatTeamsMessage(status);
            await _httpClient.PostAsync(_options.WebhookUrl, message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Log($"[Teams ERROR] {ex.Message}");
        }
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_options.Enabled && !string.IsNullOrWhiteSpace(_options.WebhookUrl));
    }

    private HttpContent FormatTeamsMessage(DiskStatus status)
    {
        // Format as Teams adaptive card
        // ...
    }
}
```

### Step 2: Create Configuration Options

```csharp
public class SlackOptions
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = string.Empty;
}
```

### Step 3: Add Configuration Section

Update `AlertingOptions`:

```csharp
public class AlertingOptions
{
    // ...existing properties...
    
    public SlackOptions Slack { get; set; } = new();
}
```

### Step 4: Register with Dependency Injection

In `Program.cs`:

```csharp
// Register Slack plugin
services.AddTransient<SlackAlertSender>(sp => 
    new SlackAlertSender(
        options.Alerting.Slack, 
        sp.GetRequiredService<RollingFileLogger>()));

services.AddTransient<IAlertSender, SlackAlertSender>(sp =>
    new SlackAlertSender(
        options.Alerting.Slack,
        sp.GetRequiredService<RollingFileLogger>()));
```

### Step 5: Configure in appsettings.json

```json
{
  "StorageWatch": {
    "Alerting": {
      "EnableNotifications": true,
      "Slack": {
        "Enabled": true,
        "WebhookUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
      }
    }
  }
}
```

---

## Plugin Discovery

Plugins are automatically discovered by scanning assemblies:

```csharp
var registry = new AlertSenderPluginRegistry();
registry.DiscoverPlugins(); // Scans current assembly
```

Or manually register:

```csharp
registry.RegisterPlugin<SlackAlertSender>("Slack", "Slack webhook alerts");
```

---

## Configuration Formats

### Legacy Format (Backward Compatible)

```json
{
  "Alerting": {
    "EnableNotifications": true,
    "Smtp": { "Enabled": true, ... },
    "GroupMe": { "Enabled": true, ... }
  }
}
```

### New Plugin Format (Future)

```json
{
  "Alerting": {
    "EnableNotifications": true,
    "Plugins": {
      "SMTP": { "Enabled": true, "Host": "...", ... },
      "GroupMe": { "Enabled": true, "BotId": "..." },
      "Slack": { "Enabled": true, "WebhookUrl": "..." }
    }
  }
}
```

Both formats are supported for smooth migration.

---

## Plugin Manager Usage

```csharp
// Get plugin manager from DI
var pluginManager = serviceProvider.GetRequiredService<AlertSenderPluginManager>();

// Get enabled senders
var senders = pluginManager.GetEnabledSenders();

// Perform health checks
var healthStatus = await pluginManager.PerformHealthChecksAsync();

// Get plugin metadata
var plugins = pluginManager.GetPluginInfo();
```

---

## Best Practices

1. **Error Handling:** Always catch exceptions in `SendAlertAsync` and log them. Never throw.
2. **Health Checks:** Implement meaningful health checks (connectivity, configuration validation).
3. **Configuration:** Use strongly-typed options classes with validation attributes.
4. **Logging:** Log all important operations (sends, failures, health check results).
5. **Disposal:** Implement `IDisposable` if your plugin uses unmanaged resources (HttpClient, etc.).
6. **Cancellation:** Respect the `CancellationToken` parameter for graceful shutdown.
7. **Thread Safety:** Plugins may be called concurrently; ensure thread safety.
8. **Performance:** Avoid blocking operations; use async/await throughout.

---

## Testing Plugins

### Unit Tests

```csharp
[Fact]
public async Task SlackAlertSender_WithDisabledConfig_DoesNotSendAlert()
{
    var options = new SlackOptions { Enabled = false };
    var sender = new SlackAlertSender(options, logger);
    var status = new DiskStatus { ... };

    await sender.SendAlertAsync(status);

    // Verify no HTTP call was made
}
```

### Integration Tests

```csharp
[Fact]
public async Task SlackAlertSender_SendsFormattedMessage()
{
    var mockHttpHandler = new Mock<HttpMessageHandler>();
    // Setup mock to verify message format
    
    var sender = new SlackAlertSender(options, logger, mockHttpHandler);
    await sender.SendAlertAsync(status);

    // Verify message was sent with correct format
}
```

---

## Future Enhancements

- **External Plugins:** Load plugins from external assemblies at runtime
- **Plugin Configuration UI:** Web-based configuration interface
- **Plugin Marketplace:** Community-contributed plugins
- **Rate Limiting:** Prevent alert spam
- **Alert Routing:** Route alerts to different plugins based on severity
- **Alert Templates:** Customizable message templates per plugin
- **Retry Logic:** Automatic retry for transient failures

---

## FAQ

**Q: Can I have multiple instances of the same plugin?**  
A: Currently, no. Each plugin type is registered once. Future versions may support named instances.

**Q: How do I disable a plugin temporarily?**  
A: Set `"Enabled": false` in the plugin's configuration section.

**Q: Can plugins have dependencies?**  
A: Yes, plugins are resolved via DI and can inject other services.

**Q: How do I debug a plugin?**  
A: Check the service logs at `C:\ProgramData\StorageWatch\Logs\service.log`. Enable `EnableStartupLogging` for verbose output.

**Q: Can I create plugins in separate projects?**  
A: Yes, though external plugin loading is not yet implemented. Add a project reference and register normally.

---

## Support

For questions, issues, or contributions:
- GitHub Issues: https://github.com/tuckerproject/DiskSpaceService/issues
- Documentation: https://github.com/tuckerproject/DiskSpaceService/wiki

---

**Version:** 2.0.0  
**Last Updated:** 2024
