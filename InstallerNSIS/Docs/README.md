# StorageWatch NSIS Installer

## Build Prerequisites

- NSIS (makensis.exe) installed.
- Published binaries staged in `InstallerNSIS/Payload`.

## Payload Folder Structure

Place the files below before running `makensis.exe`:

```
InstallerNSIS/
  Payload/
    Service/
      StorageWatchAgent.exe
      (service dependencies)
    UI/
      StorageWatchUI.exe
      (ui dependencies)
    SQLite/
      (SQLite runtime files)
    Plugins/
      (built-in plugin DLLs)
    Config/
      StorageWatchConfig.json
```

## Build the Installer

From the repository root:

```
"C:\Program Files (x86)\NSIS\makensis.exe" InstallerNSIS\StorageWatchInstaller.nsi
```

This produces `StorageWatchInstaller.exe` in the current working directory.

## Upgrade Behavior

- Existing installation is detected via registry key `HKLM\Software\StorageWatch` and by presence of the service executable.
- The installer stops the service before copying new binaries.
- Config, logs, SQLite data, and plugins are preserved in `%ProgramData%\StorageWatch`.
- The service is restarted after installation.

## Uninstallation

- The uninstaller stops and removes the service.
- Installed binaries and shortcuts are removed.
- You will be prompted to delete config, logs, SQLite data, and plugins.
