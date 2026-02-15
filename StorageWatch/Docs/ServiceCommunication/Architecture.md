# Service Communication Architecture

## Overview

The StorageWatch service and UI communicate via **Named Pipes**, a Windows IPC mechanism that provides secure, high-performance, localhost-only communication.

## Architecture Diagram

```
┌─────────────────────────────────────┐
│   StorageWatchUI (Desktop App)      │
│                                     │
│  ┌────────────────────────────────┐ │
│  │ ServiceCommunicationClient     │ │
│  │  - GetStatusAsync()            │ │
│  │  - GetLogsAsync()              │ │
│  │  - ValidateConfigAsync()       │ │
│  │  - TestAlertSendersAsync()     │ │
│  └────────────────────────────────┘ │
└─────────────────┬───────────────────┘
                  │
                  │ Named Pipe: "StorageWatchServicePipe"
                  │ (localhost only, secure)
                  │
┌─────────────────▼───────────────────┐
│  StorageWatchService (Windows Svc)  │
│                                     │
│  ┌────────────────────────────────┐ │
│  │ ServiceCommunicationServer     │ │
│  │  - Hosted Background Service   │ │
│  │  - Handles Commands            │ │
│  │  - Returns Responses           │ │
│  └────────────────────────────────┘ │
│                                     │
│  ┌────────────────────────────────┐ │
│  │ Service Components             │ │
│  │  - Worker (monitoring)         │ │
│  │  - RollingFileLogger           │ │
│  │  - SQLite Database             │ │
│  │  - AlertSenderPluginManager    │ │
│  └────────────────────────────────┘ │
└─────────────────────────────────────┘
```

## Communication Protocol

### Request Format

All requests are JSON messages sent over the Named Pipe:

```json
{
  "Command": "GetStatus",
  "Parameters": {
    "optionalParam": "value"
  }
}
```

### Response Format

All responses follow this structure:

```json
{
  "Success": true,
  "ErrorMessage": null,
  "Data": {
    // Command-specific data
  }
}
```

## Supported Commands

### 1. GetStatus

**Request:**
```json
{
  "Command": "GetStatus"
}
```

**Response:**
```json
{
  "Success": true,
  "Data": {
    "State": "Running",
    "Uptime": "2.05:30:15",
    "LastExecutionTimestamp": "2024-01-15T10:30:00Z",
    "LastError": null
  }
}
```

### 2. GetLogs

**Request:**
```json
{
  "Command": "GetLogs",
  "Parameters": {
    "count": 100
  }
}
```

**Response:**
```json
{
  "Success": true,
  "Data": [
    "2024-01-15 10:30:00  [INFO] Service started",
    "2024-01-15 10:30:15  [INFO] Monitoring cycle completed"
  ]
}
```

### 3. GetConfig

**Request:**
```json
{
  "Command": "GetConfig"
}
```

**Response:**
```json
{
  "Success": true,
  "Data": {
    "ConfigPath": "C:\\ProgramData\\StorageWatch\\StorageWatchConfig.json",
    "Content": "{ ... }"
  }
}
```

### 4. ValidateConfig

**Request:**
```json
{
  "Command": "ValidateConfig"
}
```

**Response:**
```json
{
  "Success": true,
  "Data": {
    "IsValid": true,
    "Errors": [],
    "Warnings": [
      "SMTP server not configured"
    ]
  }
}
```

### 5. TestAlertSenders

**Request:**
```json
{
  "Command": "TestAlertSenders"
}
```

**Response:**
```json
{
  "Success": true,
  "Data": {
    "Message": "Test alerts sent to 2 sender(s)"
  }
}
```

### 6. GetPluginStatus

**Request:**
```json
{
  "Command": "GetPluginStatus"
}
```

**Response:**
```json
{
  "Success": true,
  "Data": {
    "Plugins": [
      {
        "Name": "SMTP Alert Sender",
        "Type": "SMTP",
        "Enabled": true,
        "Health": "Healthy",
        "LastError": null
      }
    ]
  }
}
```

### 7. GetLocalData

**Request:**
```json
{
  "Command": "GetLocalData",
  "Parameters": {
    "QueryType": "TrendData",
    "DriveName": "C:",
    "DaysBack": 7,
    "Limit": 1000
  }
}
```

**Response:**
```json
{
  "Success": true,
  "Data": [
    {
      "Timestamp": "2024-01-15T10:00:00Z",
      "DriveName": "C:",
      "PercentFree": 45.2,
      "FreeSpaceGb": 120.5,
      "TotalSpaceGb": 250.0
    }
  ]
}
```

## Security Considerations

### Localhost Only

Named Pipes in this implementation are configured for **localhost-only** communication. The pipe name `StorageWatchServicePipe` can only be accessed from the local machine.

### No Authentication Required

Since communication is localhost-only and both the service and UI run under the same user context (or the UI runs elevated), no additional authentication is required.

### Data Integrity

All data is transmitted over a Named Pipe, which provides:
- Process isolation
- Message boundaries
- Automatic cleanup on disconnect

## Error Handling

### Client-Side Retry Logic

The `ServiceCommunicationClient` implements automatic retry with exponential backoff:

```csharp
MaxRetries = 3
RetryDelay = 500ms * attempt
```

### Timeout Handling

All IPC operations have a 5-second timeout:

```csharp
TimeoutMilliseconds = 5000
```

### Graceful Degradation

If IPC communication fails, the UI falls back to:
- Direct file system reads (for logs)
- Direct SQLite reads (for data)
- Service status via ServiceController API

## Performance Characteristics

- **Latency:** < 10ms for typical requests
- **Throughput:** Supports multiple concurrent connections
- **Resource Usage:** Minimal (pipe-based, no polling)

## Implementation Details

### Service Side

The `ServiceCommunicationServer` is registered as a hosted service:

```csharp
services.AddHostedService<ServiceCommunicationServer>();
```

It runs continuously, accepting connections and processing requests asynchronously.

### UI Side

The `ServiceCommunicationClient` is instantiated per-ViewModel:

```csharp
private readonly ServiceCommunicationClient _communicationClient 
    = new ServiceCommunicationClient();
```

Each request creates a new Named Pipe connection, sends the request, receives the response, and disconnects.

## Testing

See:
- `StorageWatch.Tests\Communication\ServiceCommunicationServerTests.cs`
- `StorageWatchUI.Tests\Communication\ServiceCommunicationClientTests.cs`

## Future Enhancements

1. **Live log tailing** via persistent connections
2. **Service command execution** (e.g., reload config, trigger manual scan)
3. **Bi-directional notifications** (service pushes alerts to UI)
4. **Secure authentication** for multi-user scenarios
