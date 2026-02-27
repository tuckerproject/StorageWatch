# Contributing to StorageWatch

Thank you for your interest in contributing to StorageWatch. This document explains how to contribute code, documentation, bug reports, and feature requests.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) with the C# extension
- [Git](https://git-scm.com/)
- Windows 10 or later (required for the service and UI; the server runs cross-platform)

### Fork and Clone

1. Fork the repository on GitHub.
2. Clone your fork:
   ```powershell
   git clone https://github.com/YOUR_USERNAME/StorageWatch.git
   cd StorageWatch
   ```
3. Add the upstream remote:
   ```powershell
   git remote add upstream https://github.com/tuckerproject/StorageWatch.git
   ```

### Build

```powershell
dotnet build
```

### Run Tests

```powershell
dotnet test
```

---

## Contributing Code

### Branch Naming

Create a branch from `master` with a short, descriptive name:

| Type | Pattern | Example |
|---|---|---|
| Feature | `feature/short-description` | `feature/slack-alert-sender` |
| Bug fix | `fix/short-description` | `fix/alert-state-persistence` |
| Documentation | `docs/short-description` | `docs/config-reference` |
| Refactor | `refactor/short-description` | `refactor/notification-loop` |

### Commit Messages

Use clear, imperative commit messages:

```
feat: add Slack alert sender plugin
fix: prevent duplicate alerts on reboot
docs: update configuration reference
refactor: simplify NotificationLoop state machine
test: add integration tests for SqlReporter
```

### Code Style

- Follow existing naming conventions (PascalCase for types and members, camelCase for locals).
- Use `async`/`await` for all I/O operations.
- Write XML doc comments on public types and members.
- No new external dependencies without discussion — all dependencies must be MIT, CC0, or similarly permissive.

### Tests

All code changes should include tests. The project uses:

- **xUnit** — test framework
- **Moq** — mocking
- **FluentAssertions** — assertions

New features require unit tests. Changes to data access or service communication require integration tests. See [StorageWatchAgent.Tests/README.md](../StorageWatchAgent.Tests/README.md) for guidance.

### Pull Request Checklist

Before opening a PR, confirm:

- [ ] Code builds without warnings (`dotnet build -warnaserror`)
- [ ] All existing tests pass (`dotnet test`)
- [ ] New tests added for new functionality
- [ ] Documentation updated if behavior changes
- [ ] No new external dependencies introduced without discussion
- [ ] Sensitive values (passwords, keys) are not committed

### Opening a Pull Request

1. Push your branch to your fork.
2. Open a PR against `master` in the upstream repository.
3. Fill in the PR description, linking any related issues.
4. Wait for review. Address any feedback promptly.

---

## Reporting Bugs

Open an issue on [GitHub Issues](https://github.com/tuckerproject/StorageWatch/issues) with:

- **What happened** — describe the problem
- **Expected behavior** — what you expected
- **Steps to reproduce** — exact steps to trigger the bug
- **Environment** — Windows version, .NET version, StorageWatch version
- **Logs** — relevant entries from `%PROGRAMDATA%\StorageWatch\Logs\service.log`

---

## Requesting Features

Open an issue and label it `enhancement`. Describe:

- **Use case** — what problem you are trying to solve
- **Proposed solution** — how you think it could work
- **Alternatives considered** — other approaches you thought about

---

## Writing Alert Sender Plugins

StorageWatch supports custom alert senders via the plugin architecture. To add a new sender (e.g., Slack, Teams, Discord):

1. Create a class that implements `IAlertSender`.
2. Optionally inherit from `AlertSenderBase` to reduce boilerplate.
3. Add the `[AlertSenderPlugin]` attribute.
4. Register the sender in the DI container.

See [PluginArchitecture.md](../StorageWatchAgent/Docs/PluginArchitecture.md) and [QuickStart-AddingPlugins.md](../StorageWatchAgent/Docs/QuickStart-AddingPlugins.md) for complete instructions.

---

## Documentation

Documentation improvements are always welcome. Docs live in:

- `Docs/` — Solution-level docs (architecture, config reference, troubleshooting, FAQ)
- `StorageWatchAgent/Docs/` — Service-specific docs
- `StorageWatchServer/Docs/` — Server-specific docs
- `StorageWatchUI/Docs/` — UI-specific docs
- `InstallerNSIS/Docs/` — Installer-specific docs

Use standard Markdown. Match the style of existing documents. Update the README table of contents when adding a new doc.

---

## License

By contributing, you agree that your contributions will be released under the same [CC0 1.0 Universal](../LICENSE) license as the rest of the project.
