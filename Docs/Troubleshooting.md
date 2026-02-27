# StorageWatch — Troubleshooting Guide

This guide covers common problems and their solutions for all StorageWatch components.

---

## StorageWatchAgent

### Service Won't Start

**Symptoms:** Service fails to start; Windows Event Log shows errors.

**Steps:**
1. Check the service log at `%PROGRAMDATA%\StorageWatch\Logs\service.log`.
2. Verify `StorageWatchConfig.json` exists and is valid JSON.
3. Ensure the service account has read/write access to `%PROGRAMDATA%\StorageWatch\`.
4. Confirm .NET 10 Runtime is installed:
   ```powershell
   dotnet --list-runtimes
   ```

---

### No Alerts Being Sent

**Symptoms:** Disk space is low, but no alert emails or GroupMe messages arrive.

**Steps:**
1. Confirm `Alerting.EnableNotifications` is `true` in `StorageWatchConfig.json`.
2. Check that at least one sender is enabled (`Smtp.Enabled` or `GroupMe.Enabled`).
3. Verify alert credentials (SMTP host/port/credentials or GroupMe BotId).
4. Check service logs for error messages from the alert sender.
5. Confirm the drive letter is in the `Monitoring.Drives` list (e.g., `"C:"`).
6. Check if the state file (`%PROGRAMDATA%\StorageWatch\alert_state.json`) shows the current state. If it already shows `ALERT`, the system is waiting for a state change.

---

### Duplicate Alerts on Reboot

**Symptoms:** Alerts are sent every time the service restarts.

**Steps:**
1. Verify `alert_state.json` at `%PROGRAMDATA%\StorageWatch\alert_state.json` exists and is not empty.
2. Ensure the service account has write access to the file.
3. Check for mismatched drive letters between the state file and config.

---

### Daily SQL Report Not Running

**Symptoms:** No new rows in the local SQLite database.

**Steps:**
1. Check that `SqlReporting.Enabled` is `true`.
2. Verify the `CollectionTime` format is `HH:mm` (e.g., `"02:00"`).
3. If the service was offline during the scheduled time, enable `RunMissedCollection: true`.
4. Check that the service account can write to the database path.
5. Look for database errors in the service log.

---

### Agent Not Reporting to Central Server

**Symptoms:** Central server shows no data from an agent machine.

**Steps:**
1. Verify `StorageWatch.Mode` is `Agent` in `appsettings.json`.
2. Check that `CentralServer.Enabled` is `true` and `ServerUrl` is correct.
3. Confirm the central server URL is reachable from the agent:
   ```powershell
   Invoke-WebRequest http://central-server:5001/api/machines
   ```
4. Check for firewall rules blocking outbound connections on port 5001 (or your configured port).
5. Review service logs for HTTP errors (connection refused, timeout, etc.).

---

### Drive Shows as NOT_READY

**Symptoms:** Alerts fire for a drive that appears to be working normally.

**Steps:**
1. Verify the drive letter is correct in `Monitoring.Drives` (include the colon: `"D:"`).
2. If the drive is a removable disk that may not always be attached, remove it from the monitored list.
3. Check `%SYSTEMROOT%\System32\drivers\etc\` for any disk redirection issues.

---

## StorageWatchServer

### Server Won't Start

**Symptoms:** StorageWatchServer exits immediately or fails to bind.

**Steps:**
1. Check if the port is already in use:
   ```powershell
   netstat -ano | findstr :5001
   ```
2. Kill the blocking process or change the port:
   ```json
   { "Server": { "ListenUrl": "http://localhost:5002" } }
   ```
3. Verify the `DatabasePath` directory exists and is writable.

---

### Dashboard Shows No Machines

**Symptoms:** The web dashboard loads, but the machine list is empty.

**Steps:**
1. Confirm agents are configured to report to this server (`CentralServer.ServerUrl`).
2. Test the API directly:
   ```powershell
   Invoke-WebRequest http://localhost:5001/api/machines
   ```
3. Check server logs for incoming report errors.
4. Verify agent and server clocks are reasonably synchronized (affects `LastSeenUtc` calculation).

---

### Machine Shows as Offline

**Symptoms:** A machine that is running appears offline in the dashboard.

**Steps:**
1. Check that the agent is sending reports (`StorageWatch.Mode = Agent` + `CentralServer.Enabled = true`).
2. Increase `OnlineTimeoutMinutes` if agents report less frequently than the timeout:
   ```json
   { "Server": { "OnlineTimeoutMinutes": 15 } }
   ```
3. Verify the agent can reach the server URL.

---

### Database Locked Error

**Symptoms:** Server log shows `database is locked` errors.

**Steps:**
1. Stop the server.
2. Delete WAL and shared-memory files:
   ```powershell
   Remove-Item Data\*.db-wal, Data\*.db-shm -ErrorAction SilentlyContinue
   ```
3. Restart the server.

---

### Charts Not Loading

**Symptoms:** Dashboard loads, but historical trend charts are blank.

**Steps:**
1. Open browser developer tools (F12) and check the Console tab for JavaScript errors.
2. Verify Chart.js CDN is accessible from the browser machine.
3. Confirm the selected machine has historical data (`GET /api/machines/{id}/history`).

---

## StorageWatchUI

### UI Won't Launch

**Symptoms:** Application exits immediately or shows an error dialog.

**Steps:**
1. Confirm .NET 10 Desktop Runtime is installed (the WPF version):
   ```powershell
   dotnet --list-runtimes | Select-String "WindowsDesktop"
   ```
2. Check the Windows Event Log (Application) for .NET startup errors.
3. Run from the command line to see error output:
   ```powershell
   .\StorageWatchUI.exe
   ```

---

### Service Status Shows "Unknown" or "Stopped" When Service Is Running

**Symptoms:** Service Status view shows incorrect state.

**Steps:**
1. Ensure the UI is running with administrator privileges (required for service control).
2. Verify the service name is `StorageWatchAgent`:
   ```powershell
   Get-Service StorageWatchAgent
   ```
3. If the named-pipe IPC connection fails, the UI falls back to direct SCM queries.

---

### Local Charts Are Empty

**Symptoms:** Trends view shows no data.

**Steps:**
1. Confirm the service has been running long enough to write at least one daily report.
2. Verify the local database exists at `%PROGRAMDATA%\StorageWatch\Data\StorageWatch.db`.
3. Check that the selected time range covers a period when data was collected.

---

### Central View Shows No Machines

**Symptoms:** Central view is empty or shows "Unable to connect."

**Steps:**
1. Verify a central server URL is configured in `StorageWatchConfig.json` (`CentralServer.ServerUrl`).
2. Confirm the central server is running and accessible.
3. The central view requires Agent mode to be enabled in the service.

---

## NSIS Installer

### Installer Fails with "Service Already Exists"

**Steps:**
1. Remove the existing service manually:
   ```powershell
   sc delete StorageWatchAgent
   ```
2. Run the installer again.

---

### Uninstall Doesn't Remove All Files

**Note:** By design, data files (`%PROGRAMDATA%\StorageWatch\Data\`, `Logs\`, `Config\`) are preserved during uninstallation. You will be prompted to delete them. To remove manually:

```powershell
Remove-Item -Recurse -Force "$env:ProgramData\StorageWatch"
```

---

## General

### Checking Service Logs

Service logs are at `%PROGRAMDATA%\StorageWatch\Logs\service.log` (and rotated files `service.log.1`, `service.log.2`).

```powershell
Get-Content "$env:ProgramData\StorageWatch\Logs\service.log" -Tail 50
```

### Verifying .NET 10 Installation

```powershell
dotnet --version
dotnet --list-runtimes
```

### Restarting the Service

```powershell
Restart-Service StorageWatchAgent
```

### Viewing Windows Event Log Entries

```powershell
Get-EventLog -LogName Application -Source ".NET Runtime" -Newest 10
```

---

## Still Stuck?

- Review the [FAQ](./FAQ.md) for common questions.
- Check the [Architecture Overview](./Architecture.md) to understand how components interact.
- Open an issue on [GitHub](https://github.com/tuckerproject/StorageWatch/issues).
