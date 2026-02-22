# StorageWatch

A lightweight, self-hosted storage monitoring platform for Windows. StorageWatch monitors disk space on one or more machines, logs metrics to a local SQLite database, sends real-time alerts, and optionally aggregates data from multiple machines through a central web dashboard.

Built on **.NET 10** and designed for reliability, clarity, and minimal configuration.

---

## 🧭 Overview

StorageWatch consists of three components that work independently or together:

| Component | Description |
|---|---|
| **StorageWatchService** | Windows Service — monitors local disks, stores data in SQLite, sends alerts |
| **StorageWatchServer** | Central server — aggregates data from agents, hosts web dashboard |
| **StorageWatchUI** | Desktop GUI — shows local and central trends, manages service |

### Deployment Modes

- **Standalone** — Single machine, local monitoring only. No central server required.
- **Agent** — Reports disk data to a central StorageWatchServer in addition to local monitoring.
- **Server** — Runs as the central aggregation server, hosting the web dashboard.

---

## 🚀 Features

- **State-driven alerting** — Alerts fire only on state change (NORMAL → ALERT → NOT_READY), eliminating duplicate notifications.
- **SQLite storage** — Zero-dependency local database. No SQL Server required.
- **Plugin-based alert senders** — SMTP and GroupMe built-in; add Slack, Teams, Discord, or webhooks via plugins.
- **Multi-machine dashboard** — Central server aggregates data from unlimited agents.
- **Desktop GUI** — Local and central trend charts, service control, log viewer, and settings.
- **Rolling log files** — Automatic log rotation (1 MB, 3 files).
- **Offline resilience** — Local GUI and alerting always functional, even when the central server is unreachable.
- **NSIS installer** — Role-aware installer covers service registration, shortcuts, and config setup.

---

## 📦 Quick Start

### Install via NSIS Installer (Recommended)

1. Download `StorageWatchInstaller.exe` from the [Releases](https://github.com/tuckerproject/StorageWatch/releases) page.
2. Run the installer and choose your role: **Agent** or **Central Server**.
3. Follow the prompts to complete installation.
4. The service starts automatically after installation.

See [Installer.md](./Installer.md) for full installer documentation.

### Build from Source

```powershell
git clone https://github.com/tuckerproject/StorageWatch.git
cd StorageWatch
dotnet build
```

See [BuildInstaller.md](./BuildInstaller.md) for publishing and packaging.

---

## ⚙ Configuration

StorageWatch uses JSON configuration files. The primary service configuration is at:

```
%PROGRAMDATA%\StorageWatch\Config\StorageWatchConfig.json
```

### Minimal Configuration (Standalone)

```json
{
  "StorageWatch": {
    "Monitoring": {
      "ThresholdPercent": 10,
      "Drives": ["C:", "D:"]
    },
    "Alerting": {
      "EnableNotifications": true,
      "Smtp": {
        "Enabled": false
      },
      "GroupMe": {
        "Enabled": false
      }
    },
    "SqlReporting": {
      "Enabled": true,
      "CollectionTime": "02:00"
    }
  }
}
```

For the full configuration reference, see [ConfigReference.md](./ConfigReference.md).

---

## 📚 Documentation

| Document | Description |
|---|---|
| [Architecture.md](./Architecture.md) | System architecture, component overview, data flow |
| [ConfigReference.md](./ConfigReference.md) | All configuration options for every component |
| [Troubleshooting.md](./Troubleshooting.md) | Common issues and solutions |
| [FAQ.md](./FAQ.md) | Frequently asked questions |
| [CONTRIBUTING.md](./CONTRIBUTING.md) | How to contribute |
| [Installer.md](./Installer.md) | Installer documentation |
| [BuildInstaller.md](./BuildInstaller.md) | How to build the installer |
| [CHANGELOG.md](./CHANGELOG.md) | Version history |
| [SQLiteMigration.md](./SQLiteMigration.md) | Migrating from SQL Server to SQLite |

### Component Documentation

| Component | Documentation |
|---|---|
| StorageWatchService | [Plugin Architecture](../StorageWatchService/Docs/PluginArchitecture.md) · [IPC Communication](../StorageWatchService/Docs/ServiceCommunication/README.md) · [Standalone Mode](../StorageWatchService/Docs/StandaloneMode.md) |
| StorageWatchServer | [API Reference](../StorageWatchServer/Docs/CentralWebDashboard.md) · [Quick Reference](../StorageWatchServer/Docs/QuickReference.md) |
| StorageWatchUI | [User Guide](../StorageWatchUI/Docs/UI/UserGuide.md) · [Architecture](../StorageWatchUI/Docs/UI/Architecture.md) |
| Installer | [Build Guide](../InstallerNSIS/Docs/README.md) · [Quick Start](../InstallerNSIS/Docs/QUICK-START.md) |

---

## 🤝 Contributing

Contributions are welcome. See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines.

## 📜 License

This project is dedicated to the public domain under [CC0 1.0 Universal](../LICENSE).
