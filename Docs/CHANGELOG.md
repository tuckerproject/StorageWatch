# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- Step 17: Documentation Overhaul — rewrote and consolidated all documentation into a clean, professional suite.

---

## v3.0.0 — Platform Expansion

This release transforms StorageWatch from a single-machine Windows Service into a full multi-machine monitoring platform.

### New Components
- **StorageWatchServer** — Central aggregation server with REST API and web dashboard (Phase 4, Step 14).
- **StorageWatchUI** — WPF desktop application with local and central monitoring views (Phase 3, Step 11).
- **NSIS Installer** — Role-aware installer covering Agent, Server, and Standalone modes (Phase 3, Step 13).

### New Features
- **Standalone / Agent / Server deployment modes** — Single machine or fleet monitoring (Step 15.6).
- **Named-pipe IPC** — Secure local communication between the service and the UI (Phase 3, Step 12).
- **Plugin architecture for alert senders** — SMTP and GroupMe as plugins; extensible for Slack, Teams, Discord (Phase 2, Step 9).
- **Data retention and cleanup** — Automatic deletion of old SQLite records (Phase 2, Step 10).
- **Auto-update mechanism** — UI checks for and applies updates (Phase 4, Step 16).
- **Central web dashboard** — Aggregated fleet view with online/offline detection and historical charts.
- **Agent reporting** — Agents send disk reports to the central server on a configurable interval.

### Infrastructure
- JSON configuration with strongly typed options and reload-on-change (Phase 2, Step 8).
- GitHub Actions CI pipeline: build, test, static analysis, artifact publish (Phase 1, Step 7).
- Comprehensive test suite: unit and integration tests (Phase 1, Step 6).

---

## v2.0.0 — Architecture Overhaul

This release introduced a complete overhaul of the StorageWatch architecture, focusing on reliability, clarity, and real-world operational behavior.

### New Features
- Added **NOT_READY** state detection for drives that are unavailable or unmounted.
- Added **machine-name prefix** to all alert messages for multi-machine environments.
- Added **state file persistence** to prevent alert spam on reboot.
- Added **network-ready check** to avoid failed alert sends during early boot.
- Added **SQL reporter skip logic** for unavailable drives.
- Transitioned from **SQL Server → SQLite** for zero-dependency local storage (Phase 1, Step 3).
- Renamed project from **DiskSpaceService → StorageWatch** (Phase 1, Step 2).
- Upgraded to **.NET 10** (Phase 1, Step 1).

### Improvements
- Replaced threshold-only alerting with a **state machine** (NORMAL, ALERT, NOT_READY).
- Eliminated false positives and startup alert noise.
- Improved logging clarity and consistency across all components.
- Simplified alerting architecture with unified message generation.
- Reduced unnecessary config complexity from v1.0.

### Breaking Changes
- v1.0 alert behavior fully replaced by the state-based system.
- Configuration migrated from XML to JSON.
- Database migrated from SQL Server to SQLite.

---

## v1.0.0 — Initial Release

- Windows Service monitoring one or more drives.
- SMTP and GroupMe alert support.
- Daily SQL Server logging.
- XML configuration file.