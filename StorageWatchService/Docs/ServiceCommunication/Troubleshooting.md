# Service Communication Troubleshooting Guide

## Common Issues and Solutions

### 1. UI Shows "Service Not Responding"

**Symptoms:**
- Service status shows "Running" but no detailed information
- Logs cannot be loaded via IPC
- Error message: "Service not responding: Connection timeout"

**Possible Causes:**
1. Service is not actually running (ServiceController reports stale status)
2. Named Pipe server failed to start
3. Pipe name mismatch
4. Firewall or security software blocking Named Pipes

**Solutions:**

#### Check if service is truly running:
```powershell
Get-Service StorageWatchService | Format-List *
```

#### Check service logs for IPC startup:
```
C:\ProgramData\StorageWatch\Logs\service.log
```

Look for:
```
[IPC] Starting Named Pipe server...
```

If missing, the IPC server failed to start. Check for errors above this line.

#### Verify Named Pipe exists:
```powershell
# List all named pipes
[System.IO.Directory]::GetFiles("\\.\\pipe\\")
```

Look for `StorageWatchServicePipe` in the output.

#### Restart the service:
```powershell
Restart-Service StorageWatchService
```

---

### 2. "Failed to connect after 3 attempts"

**Symptoms:**
- Any IPC operation fails with this message
- UI falls back to direct file/database access

**Possible Causes:**
1. Service is stopped
2. Service is starting up (not ready yet)
3. Named Pipe server crashed

**Solutions:**

#### Start the service:
```powershell
Start-Service StorageWatchService
```

#### Wait for service to fully start:
After starting the service, wait 5-10 seconds before attempting IPC operations.

#### Check for service crashes:
```
Event Viewer → Windows Logs → Application
```

Filter by source: `.NET Runtime`, `StorageWatchService`

---

### 3. Config Validation Always Fails

**Symptoms:**
- ValidateConfigAsync() always returns errors
- Errors mention "Config validation failed: ..."

**Possible Causes:**
1. Configuration file is malformed JSON
2. Configuration file is missing required fields
3. Configuration file has wrong data types

**Solutions:**

#### Validate JSON syntax:
Use an online JSON validator or:

```powershell
Get-Content "C:\Program Files\StorageWatch\StorageWatchConfig.json" | ConvertFrom-Json
```

#### Check required fields:
Ensure the following sections exist:
- `General`
- `Monitoring`
- `Database`
- `Alerting`

#### Check example configuration:
Copy from the sample in the repository:
```
StorageWatch\StorageWatchConfig.json
```

---

### 4. Test Alerts Don't Send

**Symptoms:**
- TestAlertSendersAsync() returns success, but no alerts received
- Plugin status shows "Enabled" but no emails/messages

**Possible Causes:**
1. Alert senders are not configured correctly
2. SMTP server credentials are wrong
3. GroupMe bot token is invalid
4. Network connectivity issues

**Solutions:**

#### Check plugin status:
In the UI Settings view, check the "Plugin Status" section. Look for:
- Health: "Healthy" (good) or "Error" (problem)
- LastError: Any error messages

#### Test SMTP manually:
```powershell
$smtp = New-Object System.Net.Mail.SmtpClient("smtp.example.com", 587)
$smtp.EnableSsl = $true
$smtp.Credentials = New-Object System.Net.NetworkCredential("user", "pass")
$smtp.Send("from@example.com", "to@example.com", "Test", "Test message")
```

#### Test GroupMe bot:
```powershell
Invoke-RestMethod -Uri "https://api.groupme.com/v3/bots/post" `
    -Method POST `
    -Body (@{bot_id="YOUR_BOT_ID"; text="Test"} | ConvertTo-Json) `
    -ContentType "application/json"
```

#### Check service logs:
Look for alert-related errors in:
```
C:\ProgramData\StorageWatch\Logs\service.log
```

---

### 5. Local Data Queries Return Empty Results

**Symptoms:**
- Dashboard shows "No disk data available"
- Trends view is empty
- IPC query succeeds but returns empty array

**Possible Causes:**
1. Service hasn't collected data yet (first run)
2. Database file doesn't exist
3. Database is empty or corrupted

**Solutions:**

#### Wait for first monitoring cycle:
The service collects data every N minutes (configured in `Monitoring.IntervalMinutes`). Wait for one cycle to complete.

#### Check database exists:
```powershell
Test-Path "C:\ProgramData\StorageWatch\StorageWatch.db"
```

#### Query database directly:
```powershell
# Install sqlite3.exe from https://www.sqlite.org/download.html
sqlite3 "C:\ProgramData\StorageWatch\StorageWatch.db" "SELECT * FROM DiskSpaceLog LIMIT 10;"
```

#### Check service logs for database errors:
```
[STARTUP ERROR] Failed to initialize SQLite database: ...
```

---

### 6. Elevation Prompt Appears Every Time

**Symptoms:**
- UAC prompt appears when starting/stopping service
- Annoying for frequent operations

**Possible Causes:**
1. UI is not running as administrator
2. User account does not have service control permissions

**Solutions:**

#### Run UI as administrator:
Right-click `StorageWatchUI.exe` → "Run as administrator"

#### Grant user permissions to control service:
```powershell
sc.exe sdset StorageWatchService "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWRPWPDTLOCRRC;;;SU)"
```

This grants "Interactive Users" (IU) the right to start/stop the service.

**Warning:** Granting service control to non-admin users may be a security risk depending on your environment.

---

### 7. Logs Show Repeated IPC Errors

**Symptoms:**
- Service logs show constant IPC connection errors
- Performance degradation
- High CPU usage

**Possible Causes:**
1. UI is in an infinite retry loop
2. Multiple UI instances are running
3. Another application is trying to connect to the pipe

**Solutions:**

#### Close all UI instances:
```powershell
Get-Process StorageWatchUI | Stop-Process
```

#### Restart the service:
```powershell
Restart-Service StorageWatchService
```

#### Check for conflicting applications:
```powershell
# List all processes with open handles to Named Pipes
Get-Process | Where-Object { $_.Handles -gt 0 }
```

---

## Debugging Tips

### Enable Verbose Logging

Edit `StorageWatchConfig.json`:

```json
{
  "General": {
    "EnableStartupLogging": true,
    "EnableDebugLogging": true
  }
}
```

Then restart the service.

### Monitor Named Pipe Activity

Use **Process Monitor** (procmon.exe) from Sysinternals:

1. Download from: https://docs.microsoft.com/en-us/sysinternals/downloads/procmon
2. Run as Administrator
3. Filter by:
   - Process Name: `StorageWatchService.exe` or `StorageWatchUI.exe`
   - Path: Contains `StorageWatchServicePipe`
4. Look for:
   - `CreateFile` events (connection attempts)
   - `ReadFile` / `WriteFile` events (data transfer)
   - `ACCESS DENIED` results (permission issues)

### Capture IPC Traffic

Use **PipeList** and **PipeSniffer** from Sysinternals:

```powershell
# List all named pipes
pipelist.exe
```

Look for `\Device\NamedPipe\StorageWatchServicePipe`

### Check Windows Event Logs

```powershell
# Application errors
Get-EventLog -LogName Application -Source "StorageWatchService" -Newest 50

# System errors
Get-EventLog -LogName System -Source "Service Control Manager" -Newest 50 | Where-Object { $_.Message -like "*StorageWatch*" }
```

---

## Performance Issues

### High Latency (> 100ms per request)

**Causes:**
- Service is overloaded (monitoring too many disks)
- Database is very large (millions of rows)
- Disk I/O bottleneck

**Solutions:**
1. Increase monitoring interval
2. Enable data retention/cleanup
3. Move database to faster storage (SSD)

### UI Freezes During IPC Operations

**Causes:**
- IPC operations are blocking the UI thread
- Timeout is too long

**Solutions:**
1. Ensure all IPC calls use `async/await` properly
2. Reduce timeout: Edit `ServiceCommunicationClient.cs`, change `TimeoutMilliseconds = 5000` to `3000`
3. Add loading indicators to UI

---

## Security Considerations

### Can Other Users Connect to the Pipe?

By default, Named Pipes inherit the security descriptor of the service. Since the service runs as `LocalSystem`, only administrators and the service itself can connect.

### Can Remote Machines Connect?

No. Named Pipes in this implementation are localhost-only. The pipe name `\\.\pipe\StorageWatchServicePipe` uses the `.` notation, which restricts access to the local machine.

### Can I Encrypt the Communication?

Currently, communication is not encrypted because:
1. It's localhost-only
2. Both endpoints run on the same machine
3. Named Pipes provide process isolation

If encryption is needed for compliance reasons, consider adding TLS over Named Pipes or switching to gRPC with TLS.

---

## Getting Help

If you've tried all the above and still have issues:

1. **Check the GitHub Issues**: https://github.com/tuckerproject/DiskSpaceService/issues
2. **Collect diagnostic information**:
   - Service logs: `C:\ProgramData\StorageWatch\Logs\service.log`
   - Windows Event Logs (Application and System)
   - Configuration file: `C:\Program Files\StorageWatch\StorageWatchConfig.json`
   - procmon trace (if available)
3. **Open a new GitHub Issue** with:
   - StorageWatch version
   - Windows version
   - Description of the problem
   - Steps to reproduce
   - Diagnostic information (redact sensitive data)
