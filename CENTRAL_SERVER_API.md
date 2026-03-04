# Central Server API Contract

## Agent Publishing Endpoint

### POST /api/agent/report

Agents use this endpoint to publish batches of raw drive rows to the Central Server.

#### Request

**Headers:**
- `Content-Type: application/json`
- `X-API-Key: <api-key>` (optional, if configured)

**Body:**
```json
{
  "machineName": "DESKTOP-ABC123",
  "rows": [
    {
      "machineName": "DESKTOP-ABC123",
      "driveLetter": "C:",
      "totalSpaceGb": 500.0,
      "usedSpaceGb": 300.0,
      "freeSpaceGb": 200.0,
      "percentFree": 40.0,
      "timestamp": "2024-01-15T10:00:00.0000000Z"
    },
    {
      "machineName": "DESKTOP-ABC123",
      "driveLetter": "D:",
      "totalSpaceGb": 1000.0,
      "usedSpaceGb": 500.0,
      "freeSpaceGb": 500.0,
      "percentFree": 50.0,
      "timestamp": "2024-01-15T10:00:00.0000000Z"
    }
  ]
}
```

#### Response

**Success (200 OK):**
```json
{
  "success": true,
  "message": "Batch processed successfully",
  "rowsReceived": 2
}
```

**Error (4xx/5xx):**
```json
{
  "success": false,
  "message": "Error description",
  "details": "Optional error details"
}
```

## Agent Behavior

### Batching
- Maximum 100 rows per batch
- Batches sent sequentially (not in parallel)
- `last_central_run.txt` updated after each successful batch

### Retry Logic
- No automatic retry per batch
- Failed batches remain in local SQLite
- Next publish cycle will attempt to send failed rows again
- Offline mode entered on connection errors

### Offline/Online Mode
- **Entering Offline Mode:**
  - Triggered by `HttpRequestException` (server unreachable)
  - Logs once: "Central server unreachable… entering offline mode"
  - Suppresses further error logs until server becomes reachable
  
- **Resuming Online Mode:**
  - Triggered by successful batch publish after offline mode
  - Logs once: "Central server reachable… resuming publishing"
  - Backlog automatically flushed on next publish cycle

### Silent Operation
- No log entry when there are no new rows to publish
- Reduces log noise during normal operation

## Data Model

### RawDriveRow
```csharp
public class RawDriveRow
{
    public string MachineName { get; set; }      // Agent machine name
    public string DriveLetter { get; set; }      // e.g., "C:"
    public double TotalSpaceGb { get; set; }     // Total drive capacity in GB
    public double UsedSpaceGb { get; set; }      // Used space in GB
    public double FreeSpaceGb { get; set; }      // Free space in GB
    public double PercentFree { get; set; }      // Free space percentage (0-100)
    public DateTime Timestamp { get; set; }      // Collection timestamp (UTC)
}
```

## Server Implementation Notes

The Central Server should:

1. **Validate the API Key** (if configured)
2. **Accept the batch** and store raw rows
3. **Return appropriate HTTP status codes:**
   - `200 OK` - Batch accepted and processed
   - `400 Bad Request` - Invalid request format
   - `401 Unauthorized` - Invalid or missing API key
   - `500 Internal Server Error` - Server-side processing error

4. **Store raw rows as-is** (no aggregation or transformation at ingestion)
5. **Build dashboards/views separately** from the raw data store
6. **Handle duplicate rows gracefully** (idempotency recommended)

## Security Considerations

### API Key
- Optional but recommended for production deployments
- Sent in `X-API-Key` header
- Should be stored encrypted in `AgentConfig.json`
- Can be rotated by updating configuration

### HTTPS
- Recommended for production deployments
- Configure `ServerUrl` with `https://` scheme
- Agents will use TLS for encrypted transport

### Network Isolation
- Central Server should be accessible only to authorized agents
- Consider firewall rules, network segmentation, or VPN

## Example Configuration

### Agent Side (AgentConfig.json)
```json
{
  "StorageWatch": {
    "Mode": "Agent",
    "CentralServer": {
      "ServerUrl": "https://central.example.com:5000",
      "CheckIntervalSeconds": 300,
      "ApiKey": "encrypted-api-key-here"
    }
  }
}
```

### Server Side
The Central Server implementation is in the `StorageWatchServer` project and should expose the `/api/agent/report` endpoint.
