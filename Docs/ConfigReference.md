# StorageWatch — Configuration Reference

This document describes every configuration option for all StorageWatch components.

---

## StorageWatchAgent

The service uses two JSON files:

| File | Purpose |
|---|---|
| `appsettings.json` | Operational mode and logging |
| `StorageWatchConfig.json` | Monitoring, alerting, and reporting |

Both files are located at `%PROGRAMDATA%\StorageWatch\Config\` on installed systems.

---

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "StorageWatch": {
    "Mode": "Standalone"
  }
}
```

#### `StorageWatch.Mode`

Sets the operational mode for the service.

| Value | Description |
|---|---|
| `Standalone` | Local monitoring only. No central server communication. |
| `Agent` | Local monitoring + reports to a central StorageWatchServer. |
| `Server` | Runs as central server. Not used for StorageWatchAgent — use StorageWatchServer instead. |

**Default:** `Standalone`

---

### StorageWatchConfig.json

Complete example with all options:

```json
{
  "StorageWatch": {
    "General": {
      "EnableStartupLogging": true
    },
    "Monitoring": {
      "ThresholdPercent": 10,
      "Drives": ["C:", "D:"]
    },
    "Alerting": {
      "EnableNotifications": true,
      "Smtp": {
        "Enabled": false,
        "Host": "smtp.gmail.com",
        "Port": 587,
        "UseSsl": true,
        "Username": "your-email@gmail.com",
        "Password": "your-app-password",
        "FromAddress": "your-email@gmail.com",
        "ToAddress": "alert-recipient@example.com"
      },
      "GroupMe": {
        "Enabled": false,
        "BotId": "your-groupme-bot-id"
      }
    },
    "CentralServer": {
      "Enabled": false,
      "ServerUrl": "http://central-server.example.com:5000",
      "ReportIntervalSeconds": 300
    },
    "SqlReporting": {
      "Enabled": true,
      "RunMissedCollection": true,
      "RunOnlyOncePerDay": true,
      "CollectionTime": "02:00"
    }
  }
}
```

---

#### General

| Property | Type | Default | Description |
|---|---|---|---|
| `EnableStartupLogging` | bool | `true` | Log detailed startup information |

---

#### Monitoring

| Property | Type | Default | Description |
|---|---|---|---|
| `ThresholdPercent` | int | `10` | Alert threshold — percentage of free space below which an alert is sent |
| `Drives` | string[] | `["C:"]` | List of drive letters to monitor (e.g., `"C:"`, `"D:"`) |

---

#### Alerting

| Property | Type | Default | Description |
|---|---|---|---|
| `EnableNotifications` | bool | `true` | Master switch — disable to suppress all alerts |

##### SMTP

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `false` | Enable SMTP email alerts |
| `Host` | string | — | SMTP server hostname |
| `Port` | int | `587` | SMTP server port |
| `UseSsl` | bool | `true` | Use TLS/SSL |
| `Username` | string | — | SMTP authentication username |
| `Password` | string | — | SMTP authentication password |
| `FromAddress` | string | — | Sender email address |
| `ToAddress` | string | — | Recipient email address |

##### GroupMe

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `false` | Enable GroupMe bot alerts |
| `BotId` | string | — | GroupMe bot ID |

---

#### CentralServer (Agent Mode Only)

These settings only apply when `StorageWatch.Mode` is `Agent`.

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `false` | Enable reporting to central server |
| `ServerUrl` | string | — | Base URL of the StorageWatchServer (e.g., `http://server:5001`) |
| `ReportIntervalSeconds` | int | `300` | How often to send reports to the server (seconds) |
| `ApiKey` | string | — | Optional API key (for future authentication support) |
| `AgentId` | string | — | Optional custom agent identifier |

---

#### SqlReporting

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | `true` | Enable daily SQLite disk logging |
| `RunMissedCollection` | bool | `true` | Run immediately after boot if the scheduled time was missed |
| `RunOnlyOncePerDay` | bool | `true` | Prevent multiple runs in the same day |
| `CollectionTime` | string | `"02:00"` | Daily collection time in 24-hour format (HH:mm) |

---

### Environment Variables (Service)

Any `StorageWatchConfig.json` property can be overridden using environment variables with double-underscore as the path separator:

```
StorageWatch__Monitoring__ThresholdPercent=15
StorageWatch__Alerting__Smtp__Enabled=true
StorageWatch__CentralServer__ServerUrl=http://central:5001
```

---

## StorageWatchServer

### appsettings.json

```json
{
  "Server": {
    "ListenUrl": "http://localhost:5001",
    "DatabasePath": "Data/StorageWatchServer.db",
    "AgentReportDatabasePath": "Data/StorageWatchAgentReports.db",
    "OnlineTimeoutMinutes": 10
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

#### Server

| Property | Type | Default | Description |
|---|---|---|---|
| `ListenUrl` | string | `http://localhost:5001` | URL and port the server binds to |
| `DatabasePath` | string | `Data/StorageWatchServer.db` | Path to the central SQLite database |
| `AgentReportDatabasePath` | string | `Data/StorageWatchAgentReports.db` | Path to the agent reports database |
| `OnlineTimeoutMinutes` | int | `10` | Minutes since last report before a machine is marked offline |

### Environment Variables (Server)

```
Server__ListenUrl=http://0.0.0.0:8080
Server__DatabasePath=/data/agents.db
Server__OnlineTimeoutMinutes=5
Logging__LogLevel__Default=Debug
```

---

## StorageWatchUI

### appsettings.json

```json
{
  "StorageWatchUI": {
    "RefreshIntervalSeconds": 30,
    "ChartDataPoints": 50,
    "Theme": "Dark"
  }
}
```

#### StorageWatchUI

| Property | Type | Default | Description |
|---|---|---|---|
| `RefreshIntervalSeconds` | int | `30` | How often local disk data refreshes (seconds) |
| `ChartDataPoints` | int | `50` | Maximum data points displayed in trend charts |
| `Theme` | string | `"Dark"` | UI theme — `"Dark"` or `"Light"` |

---

## Data Directories

All runtime data is stored under `%PROGRAMDATA%\StorageWatch\`:

| Path | Contents |
|---|---|
| `Config\StorageWatchConfig.json` | Service configuration |
| `Data\StorageWatch.db` | Local SQLite database |
| `Logs\service.log` | Rolling service log files |
| `alert_state.json` | Persisted alert state |

---

## Security Notes

- Sensitive values (SMTP passwords, GroupMe bot IDs) are stored in plain text in the configuration file.
- Restrict file permissions on `StorageWatchConfig.json` to the service account only.
- The configuration file is excluded from version control (`.gitignore`).
- For production environments, consider using environment variables or Windows DPAPI encryption for sensitive values.
