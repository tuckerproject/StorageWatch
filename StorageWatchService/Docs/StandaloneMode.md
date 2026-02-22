# StorageWatch — Standalone Mode

StorageWatch supports three operational modes. This document describes the Standalone mode and how it differs from Agent and Server modes.

## Operational Modes

Set the mode in `appsettings.json` under `StorageWatch.Mode`:

| Mode | Description |
|---|---|
| `Standalone` | Local drive monitoring only. No central server communication. |
| `Agent` | Local monitoring + periodic reports to a central StorageWatchServer. |
| `Server` | Central aggregation server. Use StorageWatchServer.exe, not StorageWatchService.exe. |

---

## Standalone Mode

**Best for:** Single-machine environments where no central server is needed.

```json
{
  "StorageWatch": {
    "Mode": "Standalone"
  }
}
```

**Behavior:**
- Monitors local drives
- Stores data in local SQLite database
- Sends local alerts (SMTP, GroupMe, plugins)
- No outbound reporting to any server
- No network access required beyond alert delivery

**Services registered:** monitoring, alerting, SQLite, IPC server.  
**Services NOT registered:** `AgentReportWorker`, HTTP reporting client.

---

## Agent Mode

**Best for:** Multi-machine environments reporting to a central StorageWatchServer.

```json
{
  "StorageWatch": {
    "Mode": "Agent"
  },
  "StorageWatch": {
    "CentralServer": {
      "Enabled": true,
      "ServerUrl": "http://central-server.example.com:5001",
      "ReportIntervalSeconds": 300
    }
  }
}
```

**Behavior:**
- All Standalone behaviors plus:
- Sends disk reports to `ServerUrl` every `ReportIntervalSeconds` seconds
- Handles network failures gracefully — retries on next interval

---

## Server Mode

The Server mode is handled by **StorageWatchServer.exe**, not StorageWatchService.exe. If StorageWatchServer detects the mode is not `Server`, it exits with an error.

---

## Mode Enum

```csharp
public enum StorageWatchMode
{
    Standalone = 0,  // Local-only
    Agent      = 1,  // Reports to central server (default)
    Server     = 2   // Central aggregation server
}
```

The default is `Agent` for backward compatibility with configurations that pre-date the `Mode` property.

---

## Behavior Matrix

| Behavior | Standalone | Agent | Server |
|---|---|---|---|
| Local drive monitoring | ✓ | ✓ | ✗ |
| Local SQLite storage | ✓ | ✓ | ✗ |
| Reports to central server | ✗ | ✓ | ✗ |
| Receives agent reports | ✗ | ✗ | ✓ |
| Hosts web dashboard | ✗ | ✗ | ✓ |
| Requires network | Only for alerts | Yes | Yes |

---

## Configuration Validation

The validator enforces mode-specific rules:

- **Standalone:** `CentralServer.Enabled` must be `false` (or omitted).
- **Agent:** If `CentralServer.Enabled = true`, `ServerUrl` and `ReportIntervalSeconds` are required.
- **Server:** `StorageWatchServer.exe` validates that the config `Mode` equals `Server`.

---

## See Also

- [ConfigReference.md](../../Docs/ConfigReference.md) — Full configuration reference
- [Architecture.md](../../Docs/Architecture.md) — System-wide architecture overview
