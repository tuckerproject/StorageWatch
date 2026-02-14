# StorageWatch — Copilot Master Prompt

This document defines the full modernization and expansion plan for the project currently named **DiskSpaceService**. GitHub Copilot should use this file as authoritative context when assisting with refactoring, architecture, testing, UI development, installer creation, and feature implementation.

All work should follow **.NET 10**, modern .NET best practices, and the roadmap below.

---

# PROJECT OVERVIEW

StorageWatch is a Windows‑based storage monitoring platform consisting of:

- **StorageWatchService.exe**  
  - Runs as a Windows Service  
  - Monitors local disks  
  - Stores data in local SQLite  
  - Optionally reports to a central server  

- **StorageWatchServer.exe**  
  - Central aggregation server (optional)  
  - Receives data from agents  
  - Hosts web dashboard  
  - Stores multi‑machine data  

- **StorageWatchUI.exe**  
  - Local GUI on every machine  
  - Shows local trends (from local SQLite)  
  - Shows global trends (from central server, if configured)  

The system must support:

- **Standalone mode** (local‑only)  
- **Centralized mode** (one server, many agents)  
- **Offline resilience**  
- **Local GUI always functional**  
- **Optional central dashboard**  

All code must remain compatible with the project’s **CC0 license**.  
All dependencies must be MIT, Public Domain, or similarly permissive.

---

# PHASE 1 — FOUNDATION & MODERNIZATION

### 1. Upgrade Entire Project to .NET 10
- Update all `.csproj` files to target `net10.0`  
- Update Microsoft.Extensions packages to latest compatible versions  
- Update publish profiles  
- Update documentation to reflect .NET 10 LTS  

### 2. Rename DiskSpaceService → StorageWatch
- Rename solution, project folders, namespaces, executables  
- Update README, docs, installer text, and comments  
- Ensure consistent naming across all components  

### 3. Transition from SQL Server → SQLite
- Remove SQL Server dependency  
- Add SQLite (public domain)  
- Create new SQLite schema for disk logs  
- Update SQLReporter to write to local `.db` file  
- Add migration script for existing SQL Server users  
- Update documentation  

### 4. Introduce Agent/Server Role Configuration
- Add configuration option for:
  - **Agent mode** (default)  
  - **Central server mode**  
- Agent mode:
  - Writes to local SQLite  
  - Optionally reports to central server  
- Server mode:
  - Hosts API + dashboard  
  - Stores multi‑machine data  

### 5. Installer Role Selection
Installer must ask:
- “Install as **Central Server** or **Agent**?”  
- If Agent:
  - Ask whether to connect to a central server  
- If Server:
  - Configure server URL, ports, and database  

### 6. Testing Infrastructure
- Add unit tests for:
  - Drive scanning  
  - Alerting logic  
  - Config parsing  
  - SQLite writes  
- Add integration tests for:
  - SQLite schema  
  - Alert senders  
  - Network readiness  
- Add code coverage reporting  

### 7. Continuous Integration
- GitHub Actions workflow:
  - Build on .NET 10  
  - Run tests  
  - Static analysis  
  - Publish artifacts  

---

# PHASE 2 — ARCHITECTURE ENHANCEMENTS

### 8. Configuration System Redesign
- Move from XML → JSON  
- Strongly typed options  
- Validation rules  
- Reload‑on‑change  
- Optional encryption for sensitive fields  

### 9. Plugin Architecture for Alert Senders
- Define `IAlertSender` interface  
- Convert SMTP + GroupMe into plugins  
- Add support for future senders:
  - Slack  
  - Teams  
  - Discord  
  - Webhooks  
  - SMS  

### 10. Data Retention & Cleanup
- Automatic deletion of old SQLite rows  
- Optional archiving  
- Optional CSV export  

---

# PHASE 3 — USER EXPERIENCE

### 11. StorageWatch UI (Local GUI)
- Build WPF or WinUI desktop application  
- Local view:
  - Reads local SQLite  
  - Shows local trends  
- Central view:
  - Reads from central server API  
  - Shows multi‑machine trends  
- Additional features:
  - Service status  
  - Log viewer  
  - Settings editor  
  - Alert testing tools  

### 12. Service ↔ UI Communication
- Local SQLite for local trends  
- REST API (Kestrel) for central server communication  
- Optional named pipes for advanced local interactions  

### 13. Installer Package
- Single installer containing:
  - StorageWatchService.exe  
  - StorageWatchServer.exe  
  - StorageWatchUI.exe  
  - SQLite runtime  
  - Config templates  
- Installer responsibilities:
  - Install service  
  - Register service  
  - Create Start Menu shortcuts  
  - Start service  

---

# PHASE 4 — ADVANCED FEATURES

### 14. Central Web Dashboard
- Hosted by StorageWatchServer  
- Shows:
  - All machines  
  - Online/offline status  
  - Historical trends  
  - Alerts  
  - Settings  

### 15. Remote Monitoring Agents
- Agents report to central server  
- Server aggregates data  
- Dashboard displays unified view  

### 16. Auto‑Update Mechanism
- UI checks for updates  
- Downloads new version  
- Gracefully updates service  

---

# PHASE 5 — DOCUMENTATION & COMMUNITY

### 17. Documentation Overhaul
- Architecture overview  
- Config reference  
- Troubleshooting guide  
- FAQ  
- Screenshots (UI)  
- Contribution guide  

### 18. Community & Ecosystem
- Issue templates  
- Feature request templates  
- Plugin examples  
- Sample dashboards (Power BI, Grafana)  

---

# COPILOT USAGE INSTRUCTIONS

When asked to work on a specific phase or milestone:

- Use this roadmap as the authoritative source  
- Provide complete code files or diffs  
- Suggest folder structures  
- Generate tests  
- Update documentation  
- Provide migration scripts  
- Maintain naming consistency with **StorageWatch**  
- Follow **.NET 10** best practices  
- Keep all dependencies CC0/MIT/Public Domain compatible  

Always work **one phase or one milestone at a time** unless explicitly instructed otherwise.

---

# END OF MASTER PROMPT