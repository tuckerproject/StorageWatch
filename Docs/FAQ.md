# StorageWatch — Frequently Asked Questions

---

## General

**Q: What is StorageWatch?**

StorageWatch is a lightweight, self-hosted Windows storage monitoring platform. It monitors disk space, sends alerts when drives fall below a threshold, logs historical data to a local SQLite database, and optionally aggregates data from multiple machines through a central web dashboard.

---

**Q: Is StorageWatch free?**

Yes. StorageWatch is released under CC0 (Public Domain). You can use, modify, and distribute it freely with no restrictions.

---

**Q: What does StorageWatch require to run?**

- Windows 10 / Windows Server 2016 or later
- .NET 10 Runtime (or .NET 10 Desktop Runtime for the UI)

No SQL Server, IIS, or other external services are required.

---

**Q: How is StorageWatch different from Windows built-in disk monitoring?**

StorageWatch adds:
- Alert notifications via email (SMTP) or GroupMe when disk space drops below a threshold
- Historical trend data stored in a local database
- A multi-machine central dashboard
- A desktop GUI for local monitoring without a browser

---

## Installation

**Q: How do I install StorageWatch?**

Use the NSIS installer (`StorageWatchInstaller.exe`). It installs and registers the Windows Service, creates Start Menu shortcuts, and sets up the default configuration. See [Installer.md](./Installer.md).

---

**Q: Do I need to install both the service and the UI?**

No. Each component is optional:
- The **service** is the core component for monitoring and alerting.
- The **UI** is optional and provides a graphical view of local and central data.
- The **central server** is optional and only needed for multi-machine monitoring.

---

**Q: Can StorageWatch run on Linux or macOS?**

StorageWatch is designed for Windows. The service and installer depend on Windows-specific APIs (Windows Service, Named Pipes, drive letters). The central server (`StorageWatchServer`) can run on Linux/macOS since it is a cross-platform ASP.NET Core application.

---

## Configuration

**Q: Where is the configuration file?**

`%PROGRAMDATA%\StorageWatch\Config\StorageWatchConfig.json`

On most systems this resolves to `C:\ProgramData\StorageWatch\Config\StorageWatchConfig.json`.

---

**Q: Can I monitor more than two drives?**

Yes. Add as many drive letters as you need to the `Monitoring.Drives` list:

```json
"Drives": ["C:", "D:", "E:", "F:"]
```

---

**Q: How do I change the alert threshold?**

Set `Monitoring.ThresholdPercent` in `StorageWatchConfig.json`. For example, to alert when free space drops below 15%:

```json
"Monitoring": {
  "ThresholdPercent": 15
}
```

---

**Q: Can I use both SMTP and GroupMe at the same time?**

Yes. Enable both in the `Alerting` section. Alerts are sent to all enabled senders simultaneously.

---

**Q: The service uses a plain-text password in the config file. Is that secure?**

For most home-lab and small-office use cases, restricting file permissions on `StorageWatchConfig.json` to the service account is sufficient. For stricter environments, use environment variables to pass sensitive values instead of putting them in the file. A future phase will add optional encryption for sensitive fields.

---

## Alerting

**Q: Why did I get two alerts when my drive went below the threshold?**

StorageWatch uses a state machine — it sends one alert when a drive transitions to `ALERT`, and another when it recovers to `NORMAL`. If you're seeing more alerts, check the `alert_state.json` file and the service logs.

---

**Q: Alerts fired after a service restart even though the drive was already low.**

Enable state persistence by ensuring the service account has write access to `%PROGRAMDATA%\StorageWatch\alert_state.json`. If the file doesn't exist or can't be written, state is not persisted and an alert will fire on every restart when a drive is already below the threshold.

---

**Q: Why does the service wait before sending alerts on startup?**

The service waits for network readiness (DNS resolution) before sending the first alert. This prevents failures during early boot before the network stack is fully available.

---

**Q: Can I add Slack, Teams, or webhook notifications?**

Custom alert senders can be added via the plugin architecture. See [PluginArchitecture.md](../StorageWatchAgent/Docs/PluginArchitecture.md) and [QuickStart-AddingPlugins.md](../StorageWatchAgent/Docs/QuickStart-AddingPlugins.md).

---

## Data and Storage

**Q: Where is the local database stored?**

`%PROGRAMDATA%\StorageWatch\Data\StorageWatch.db`

---

**Q: How do I view the database contents?**

Use any SQLite browser, such as [DB Browser for SQLite](https://sqlitebrowser.org/) (free, open source).

---

**Q: How do I back up my data?**

Copy the `.db` file while the service is stopped, or use SQLite's online backup API. The database is a single file and portable.

---

**Q: I was using an older version with SQL Server. How do I migrate?**

See [SQLiteMigration.md](./SQLiteMigration.md) for step-by-step instructions.

---

**Q: How much disk space does the local database use?**

Approximately 1–5 MB per machine per day, depending on the number of monitored drives and collection frequency. Old records are automatically cleaned up based on the data retention settings.

---

## Central Server

**Q: Is the central server required?**

No. StorageWatch works fully in Standalone mode without a central server.

---

**Q: How many agents can the central server handle?**

StorageWatchServer has been tested with 1,000+ agent machines. SQLite is used with appropriate indexes for efficient queries.

---

**Q: Does the central server require authentication?**

No authentication is required in the current version. For production use, deploy behind a reverse proxy (nginx, IIS) with HTTPS and restrict access to trusted networks.

---

**Q: Can I run StorageWatchServer as a Docker container?**

Yes. See the Docker section of the [StorageWatchServer README](../StorageWatchServer/README.md) for a sample Dockerfile.

---

## Development

**Q: How do I build StorageWatch from source?**

```powershell
git clone https://github.com/tuckerproject/StorageWatch.git
cd StorageWatch
dotnet build
dotnet test
```

---

**Q: How do I run the tests?**

```powershell
dotnet test
```

See [StorageWatchAgent.Tests/README.md](../StorageWatchAgent.Tests/README.md) for details on running specific test categories and generating coverage reports.

---

**Q: How do I contribute?**

See [CONTRIBUTING.md](./CONTRIBUTING.md).

---

**Q: What license is StorageWatch under?**

CC0 1.0 Universal (Public Domain). See [LICENSE](../LICENSE).
