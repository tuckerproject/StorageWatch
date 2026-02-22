# StorageWatch UI Update Experience - Quick Reference

## Components Overview

### UpdateViewModel
Central orchestrator for the update experience. Manages:
- Update detection and checking
- Dialog lifecycle
- Progress tracking
- Restart handling

**Key Entry Points:**
```csharp
// Check for updates
viewModel.CheckForUpdatesCommand.Execute(null);

// Begin update process
viewModel.BeginUpdateCommand.Execute(null);

// Cancel in-progress update
viewModel.CancelUpdateCommand.Execute(null);

// Restart application
viewModel.RestartNowCommand.Execute(null);
```

### UI Components

#### UpdateBanner
Location: `Views/UpdateBanner.xaml`
- Shown at top of MainWindow when update available
- Bound to `UpdateViewModel.IsBannerVisible`
- Shows version and provides quick access to update

#### UpdateDialog
Location: `Views/UpdateDialog.xaml`
- Modal dialog confirming update details
- Shows current version, new version, release notes
- Created dynamically from `UpdateViewModel.BeginUpdateAsync()`
- Uses `UpdateDialogViewModel` for data

#### UpdateProgressDialog
Location: `Views/UpdateProgressDialog.xaml`
- Shows installation progress
- Supports indeterminate and percentage modes
- Status text changes during phases
- User can cancel operation

#### RestartPromptDialog
Location: `Views/RestartPromptDialog.xaml`
- Appears after successful installation
- Allows user to restart now or later
- Calls `IUiRestartHandler.RequestRestart()` when clicked

---

## Data Flow

```
┌─────────────────────┐
│  App Startup        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│ Check for Updates   │
│ (CheckForUpdates    │
│  Command)           │
└──────────┬──────────┘
           │
           ▼ (if available)
┌─────────────────────┐
│ Show Banner         │
│ IsBannerVisible=T   │
└──────────┬──────────┘
           │
    (User clicks "Update Now")
           │
           ▼
┌─────────────────────┐
│ Show Update Dialog  │
│ (BeginUpdateCmd)    │
└──────────┬──────────┘
           │
    (User confirms)
           │
           ▼
┌─────────────────────┐
│ Show Progress Dialog│
│ (PerformUpdateAsync)│
└──────────┬──────────┘
           │
    ┌──────┴──────┐
    │ Download    │
    │ Verify      │
    │ Install     │
    └──────┬──────┘
           │
           ▼
┌─────────────────────┐
│ Show Restart Prompt │
│ (RestartRequired=T) │
└──────────┬──────────┘
           │
    (User clicks "Restart Now")
           │
           ▼
┌─────────────────────┐
│ Restart Application │
│ (RequestRestart)    │
└─────────────────────┘
```

---

## Property Binding Reference

### UpdateViewModel Properties
| Property | Type | Use |
|----------|------|-----|
| `IsUpdateAvailable` | bool | Banner visibility trigger |
| `LatestVersion` | string | Display in banner/dialog |
| `ReleaseNotes` | string | Show in update dialog |
| `IsUpdateInProgress` | bool | Disable UI during update |
| `IsRestartRequired` | bool | Show restart prompt |
| `UpdateProgress` | double | Progress bar value |
| `UpdateStatus` | string | Status message display |
| `IsBannerVisible` | bool | Banner visibility (bound to XAML) |

### UpdateDialogViewModel Properties
| Property | Type | Use |
|----------|------|-----|
| `CurrentVersion` | string | Display installed version |
| `NewVersion` | string | Display available version |
| `ReleaseNotes` | string | Display update details |

### UpdateProgressViewModel Properties
| Property | Type | Use |
|----------|------|-----|
| `StatusText` | string | Phase message (Downloading/Verifying/Installing) |
| `Progress` | double | Progress bar percentage (0-100) |
| `IsIndeterminate` | bool | Progress bar mode toggle |

---

## Integration Points

### Dependency Injection
```csharp
// In App.xaml.cs ConfigureServices()
services.AddSingleton<UpdateViewModel>();
services.AddTransient<UpdateDialogViewModel>();
services.AddTransient<UpdateProgressViewModel>();
```

### MainWindow Binding
```xaml
<!-- UpdateViewModel exposed from MainViewModel -->
<views:UpdateBanner 
    DataContext="{Binding UpdateViewModel}"
    Visibility="{Binding IsBannerVisible, 
        Converter={StaticResource BoolToVisibilityConverter}}"/>
```

### MainViewModel Initialization
```csharp
public UpdateViewModel UpdateViewModel => _updateViewModel;
```

---

## Command Reference

### UpdateViewModel Commands
| Command | Trigger | Action |
|---------|---------|--------|
| `CheckForUpdatesCommand` | Manual or startup | Check for available updates |
| `BeginUpdateCommand` | "Update Now" click | Show update dialog |
| `CancelUpdateCommand` | "Cancel" during update | Stop installation |
| `RestartNowCommand` | "Restart Now" click | Restart application |
| `DismissBannerCommand` | Banner X or dismiss | Hide banner |
| `RemindMeLaterCommand` | "Remind Me Later" | Hide banner |

### UpdateDialogViewModel Events
| Event | Fired When | Handler |
|-------|-----------|---------|
| `UpdateRequested` | "Update" clicked | Start update process |
| `CancelRequested` | "Cancel" clicked | Close dialog |

### UpdateProgressViewModel Events
| Event | Fired When | Handler |
|-------|-----------|---------|
| `CancelRequested` | "Cancel" clicked | Stop update |

---

## Testing

### Test Locations
- `StorageWatchUI.Tests/ViewModels/UpdateViewModelTests.cs`
- `StorageWatchUI.Tests/ViewModels/UpdateDialogViewModelTests.cs`
- `StorageWatchUI.Tests/ViewModels/UpdateProgressViewModelTests.cs`
- `StorageWatchUI.Tests/ViewModels/UpdateUxFlowTests.cs`

### Running Tests
```bash
dotnet test StorageWatchUI.Tests/StorageWatchUI.Tests.csproj
```

### Key Test Scenarios
- ✅ Banner appears when update detected
- ✅ Banner can be dismissed
- ✅ Dialog shows correct versions
- ✅ Progress transitions correctly
- ✅ Cancel stops operation
- ✅ Commands are available when needed
- ✅ Restart prompt appears after install

---

## Styling & Appearance

### Color Scheme
- **Primary Action (Update)**: #FF4CAF50 (green)
- **Text Primary**: #FF333333 (dark gray)
- **Text Secondary**: #FF666666 (medium gray)
- **Background**: #FFF0F0F0 (light gray)
- **Border**: #FFCCCCCC (light border)

### Typography
- **Titles**: FontSize=18-20, FontWeight=Bold
- **Body**: FontSize=12-14, Regular weight
- **Status**: FontSize=11-12, Secondary color

---

## Configuration

### Update Check Interval
Configured in `appsettings.json`:
```json
{
  "AutoUpdate": {
    "Enabled": true,
    "ManifestUrl": "https://example.com/updates/manifest.json",
    "CheckIntervalMinutes": 60
  }
}
```

### Disable Updates
Set `AutoUpdate.Enabled` to `false` to disable update checking and UI.

---

## Troubleshooting

### Banner Not Showing
1. Check `AutoUpdate.Enabled` is true in config
2. Verify `ManifestUrl` is correct
3. Check update checker can reach server
4. Verify network connectivity

### Dialog Not Appearing
1. Ensure `BeginUpdateCommand` is bound correctly
2. Check if update information is available
3. Verify `UpdateDialogViewModel` is registered in DI

### Progress Not Updating
1. Ensure download is actually in progress
2. Check `IsIndeterminate` mode setting
3. Verify progress values are being updated
4. Check dialog is modal and visible

### Restart Not Working
1. Verify process has permissions to start new instance
2. Check process path is valid
3. Ensure old process can be terminated
4. Check Windows firewall isn't blocking restart

---

## Future Considerations

1. **Settings Integration** - Allow users to configure update frequency
2. **Update History** - Show installed updates in settings
3. **Changelog Integration** - Link to detailed release notes
4. **Update Size Display** - Show download size to user
5. **Background Installation** - Install while UI remains responsive
6. **Scheduled Updates** - Set specific update windows

---

## Related Documentation

- Step 16 (Auto-Update Logic): `Docs/STEP_16_AUTO_UPDATE.md`
- Phase 3 (UI Implementation): `Docs/PHASE3_ITEM11_SUMMARY.md`
- Master Roadmap: `Docs/CopilotMasterPrompt.md`

---

## Support & Questions

For issues or questions about the UI update experience:
1. Review test cases for expected behavior
2. Check configuration in `appsettings.json`
3. Enable debug logging to trace operations
4. Review implementation in `UpdateViewModel.cs`
