# StorageWatch UI Architecture

## Overview

StorageWatchUI is a WPF desktop application that provides a modern graphical interface for monitoring disk space on local machines and viewing centralized monitoring data from multiple machines.

## Architecture Pattern: MVVM

The application follows the **Model-View-ViewModel (MVVM)** architecture pattern:

- **Models**: Plain data classes representing domain entities (DiskInfo, MachineStatus, LogEntry, etc.)
- **Views**: XAML files defining the UI layout and appearance
- **ViewModels**: Bridge between Views and Services, containing presentation logic and state management

## Project Structure

```
StorageWatchUI/
├── Models/                     # Domain models
│   ├── DiskInfo.cs            # Current disk information
│   ├── MachineStatus.cs       # Central server machine status
│   ├── LogEntry.cs            # Service log entries
│   └── TrendDataPoint.cs      # Historical trend data
│
├── ViewModels/                 # ViewModels (MVVM pattern)
│   ├── ViewModelBase.cs       # Base class with INotifyPropertyChanged
│   ├── MainViewModel.cs       # Main window navigation
│   ├── DashboardViewModel.cs  # Dashboard view logic
│   ├── TrendsViewModel.cs     # Trends view logic
│   ├── CentralViewModel.cs    # Central view logic
│   ├── SettingsViewModel.cs   # Settings view logic
│   └── ServiceStatusViewModel.cs # Service status logic
│
├── Views/                      # XAML Views
│   ├── DashboardView.xaml     # Local disk status
│   ├── TrendsView.xaml        # Historical charts
│   ├── CentralView.xaml       # Central server machines
│   ├── SettingsView.xaml      # Configuration display
│   └── ServiceStatusView.xaml # Service management
│
├── Services/                   # Business logic and data access
│   ├── IDataProvider.cs       # Data provider interface
│   ├── LocalDataProvider.cs   # SQLite data access
│   ├── CentralDataProvider.cs # REST API client
│   ├── ServiceManager.cs      # Windows Service control
│   └── ConfigurationService.cs # Config file management
│
├── Converters/                 # Value converters for data binding
│   ├── StatusToColorConverter.cs
│   └── BoolToVisibilityConverter.cs
│
└── Styles/                     # Theme resources
    ├── DarkTheme.xaml         # Dark theme (default)
    └── LightTheme.xaml        # Light theme
```

## Data Flow

### Local Monitoring (Dashboard & Trends)

```
┌─────────────────┐
│ DashboardView   │
│ (XAML)          │
└────────┬────────┘
         │
         │ Data Binding
         ▼
┌─────────────────┐
│ DashboardVM     │◄──────────┐
└────────┬────────┘           │
         │                    │
         │ Call               │ Return Data
         ▼                    │
┌─────────────────┐           │
│ LocalDataProvider│──────────┘
└────────┬────────┘
         │
         │ SQL Query
         ▼
┌─────────────────┐
│ StorageWatch.db │
│ (SQLite)        │
└─────────────────┘
```

### Central Monitoring (Central View)

```
┌─────────────────┐
│ CentralView     │
│ (XAML)          │
└────────┬────────┘
         │
         │ Data Binding
         ▼
┌─────────────────┐
│ CentralVM       │◄──────────┐
└────────┬────────┘           │
         │                    │
         │ Call               │ Return Data
         ▼                    │
┌─────────────────┐           │
│CentralDataProvider│─────────┘
└────────┬────────┘
         │
         │ HTTP GET
         ▼
┌─────────────────┐
│ Central Server  │
│ REST API        │
└─────────────────┘
```

## Key Components

### 1. LocalDataProvider

**Purpose**: Accesses local SQLite database to retrieve disk monitoring data.

**Methods**:
- `GetCurrentDiskStatusAsync()`: Latest disk status for each drive
- `GetTrendDataAsync(driveName, daysBack)`: Historical data points
- `GetMonitoredDrivesAsync()`: List of all monitored drives

**Database Location**:
1. `%ProgramData%\StorageWatch\StorageWatch.db` (primary)
2. Current directory fallback
3. Configured path in `appsettings.json`

### 2. CentralDataProvider

**Purpose**: Communicates with the central server via REST API.

**Methods**:
- `CheckHealthAsync()`: Verify server connectivity
- `GetAllMachineStatusAsync()`: Retrieve status of all reporting machines

**Configuration**: Reads `StorageWatch:CentralServer` section from config.

### 3. ServiceManager

**Purpose**: Controls the StorageWatch Windows Service.

**Methods**:
- `IsServiceInstalled()`: Check if service exists
- `GetServiceStatus()`: Get current service state
- `StartServiceAsync()`: Start the service
- `StopServiceAsync()`: Stop the service
- `RestartServiceAsync()`: Restart the service

**Requirements**: Application must run with administrator privileges (see `app.manifest`).

### 4. ViewModels

All ViewModels inherit from `ViewModelBase` and implement:
- `INotifyPropertyChanged` for data binding
- `RefreshCommand` for manual data refresh
- Auto-refresh timers (where appropriate)
- Async data loading with loading indicators

## Dependency Injection

The application uses **Microsoft.Extensions.DependencyInjection** for IoC:

**App.xaml.cs** configures:
- Services (singletons for data providers and managers)
- ViewModels (MainViewModel as singleton, others as transient)
- Views (MainWindow as singleton)

## Threading Model

- UI runs on the main (UI) thread
- Data loading operations are async and run on background threads
- `ObservableCollection` updates must occur on the UI thread
- Auto-refresh timers dispatch to the UI thread

## Charts

The Trends view uses **LiveCharts2** (MIT licensed) for rendering historical data:
- Line charts for percent free space over time
- SkiaSharp rendering backend
- Responsive to theme changes

## Themes

Two themes are provided:
1. **DarkTheme** (default): Dark background, good for low-light environments
2. **LightTheme**: Light background, traditional Windows look

Themes are defined in `Styles/` and loaded in `App.xaml`.

## Testing Strategy

### Unit Tests
- ViewModel logic (using Moq for service mocking)
- Service methods (isolated with test databases)
- Converter logic

### Integration Tests
- LocalDataProvider with real SQLite database
- Full data flow from database to ViewModel

### Test Project
- xUnit test framework
- Moq for mocking dependencies
- FluentAssertions for readable assertions

## Configuration

The UI reads configuration from multiple sources (in order of precedence):
1. `%ProgramData%\StorageWatch\StorageWatchConfig.json` (shared with service)
2. `appsettings.json` (UI-specific settings)
3. Current directory fallback

## Future Enhancements

- Plugin support for custom data visualizations
- Export data to CSV/Excel
- Alert testing from UI
- Real-time notifications
- Remote service management (not just local)
- Custom dashboard layouts

## Performance Considerations

- Auto-refresh intervals are configurable
- Database queries use indexes for efficiency
- Large result sets are limited (e.g., last 20 log entries)
- Charts render only visible data points
- Async operations prevent UI blocking

## Security

- Configuration files may contain sensitive data (API keys, passwords)
- Future versions will support config encryption
- Service control requires administrator privileges
- Central server communication should use HTTPS in production
