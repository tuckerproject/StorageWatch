# StorageWatch Central Web Dashboard

## Overview
The StorageWatchServer project hosts the central web dashboard and REST API. It uses ASP.NET Core on .NET 10 with Kestrel, Razor Pages for the UI, and SQLite for the server database. The dashboard runs independently of the Windows service used by agents.

## Architecture
- **Host**: `StorageWatchServer` (ASP.NET Core + Kestrel)
- **UI**: Razor Pages under the `Dashboard` folder
- **API**: Minimal API endpoints under `Server/Api`
- **Data**: Lightweight SQLite access in `Server/Data`
- **Models**: DTOs and view models in `Server/Models`
- **Services**: Online/offline detection in `Server/Services`

## Database Schema
The central database stores aggregated data from agents:
- `Machines`: Registered machines with `LastSeenUtc`
- `MachineDrives`: Latest drive status per machine
- `DiskHistory`: Historical disk usage samples
- `Alerts`: Active and historical alerts
- `Settings`: Read-only server settings for the dashboard

## API Endpoints
- `POST /api/agent/report`: Agent reporting payload (machine + drives)
- `GET /api/machines`: Machine list with drive summaries
- `GET /api/machines/{id}`: Machine details
- `GET /api/machines/{id}/history?drive=C&range=7d`: Disk history
- `GET /api/alerts`: Alert list
- `GET /api/settings`: Read-only settings

## UI Pages
- `/` Dashboard home with machine list and status
- `/machines/{id}` Machine details with history charts
- `/alerts` Alerts view (active + historical)
- `/settings` Read-only settings view

## Configuration
`appsettings.json` provides the Kestrel listen URL, SQLite database path, and online timeout:

```json
{
  "Server": {
    "ListenUrl": "http://localhost:5001",
    "DatabasePath": "Data/StorageWatchServer.db",
    "OnlineTimeoutMinutes": 10
  }
}
```

## Notes
- Authentication is intentionally omitted for Phase 4.5.
- Settings are read-only in Phase 4.
- The dashboard uses Chart.js (MIT) via CDN for trend charts.
