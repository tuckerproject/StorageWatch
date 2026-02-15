# StorageWatch Installer - Pre-Build Checklist

Before building the installer for the first time, complete these required steps:

## ⚠️ Required Actions

### 1. Replace Placeholder Icon ⭐ **REQUIRED**

**Current State:** `StorageWatchInstaller/icon.ico` is a text placeholder file

**Action Required:**
- Create or obtain a proper `.ico` file for StorageWatch
- Icon should contain multiple sizes: 16x16, 32x32, 48x48, 256x256
- Replace `StorageWatchInstaller/icon.ico` with the actual icon file

**Tools for Creating Icons:**
- Online: https://convertio.co/png-ico/
- Windows: Use Paint or Photoshop
- Free tools: IcoFX, GIMP with ICO plugin

**Why Important:** The installer will fail to build if icon.ico is not a valid icon file.

---

### 2. Generate Unique GUIDs ⭐ **REQUIRED for Production**

**Current State:** `Components.wxs` uses placeholder GUIDs

**Action Required:**
Replace placeholder GUIDs with unique GUIDs generated for your installation:

```powershell
# PowerShell: Generate new GUIDs
1..20 | ForEach-Object { [guid]::NewGuid().ToString().ToUpper() }
```

**Files to Update:**
- `StorageWatchInstaller/Variables.wxi` - UpgradeCode (NEVER change after first release!)
- `StorageWatchInstaller/Components.wxs` - All Component Guid attributes

**Example:**
```xml
<!-- OLD (Placeholder) -->
<Component Id="ServiceExecutable" Guid="11111111-1111-1111-1111-111111111111">

<!-- NEW (Unique GUID) -->
<Component Id="ServiceExecutable" Guid="A1B2C3D4-E5F6-7890-ABCD-EF1234567890">
```

**⚠️ Critical:** Once you release version 1.0.0, NEVER change these GUIDs. They are used by Windows Installer to track components.

---

### 3. Update Project References in .wixproj ⚡ **RECOMMENDED**

**Current State:** `StorageWatchInstaller.wixproj` has placeholder project GUIDs

**Action Required:**
Get the actual Project GUIDs from your .csproj files:

```powershell
# Get Service project GUID
Select-String -Path "StorageWatch/StorageWatchService.csproj" -Pattern "<ProjectGuid>"

# Get UI project GUID
Select-String -Path "StorageWatchUI/StorageWatchUI.csproj" -Pattern "<ProjectGuid>"
```

If your projects don't have GUIDs (SDK-style projects), you can leave the placeholder or remove the ProjectReference and use file references instead.

**Alternative:** Use file references instead of project references:
```xml
<!-- Instead of ProjectReference, use direct file paths -->
<HarvestDirectory Include="$(OutputPath)" />
```

---

### 4. Install WiX Toolset ⭐ **REQUIRED**

**Action Required:**
```powershell
# Install WiX globally
dotnet tool install --global wix

# Verify installation
wix --version
```

**Expected Output:** `5.0.x` or later

**If Installation Fails:**
- Download from: https://wixtoolset.org/
- Or use: `dotnet tool install --global wix --version 5.0.2`

---

### 5. Verify .NET 10 SDK Installed ⭐ **REQUIRED**

**Action Required:**
```powershell
# Check .NET version
dotnet --version
```

**Expected Output:** `10.0.x` or later

**If Not Installed:**
- Download from: https://dotnet.microsoft.com/download/dotnet/10.0

---

## ✅ Optional but Recommended

### 6. Test Icon Appearance

After replacing `icon.ico`, test how it looks:

```powershell
# Build a quick test MSI (Debug mode)
.\build-installer.ps1 -Configuration Debug

# Install and check Add/Remove Programs
# The icon should appear next to "StorageWatch" in the programs list
```

### 7. Customize Product Metadata

Edit `StorageWatchInstaller/Variables.wxi`:

```xml
<?define ProductName = "StorageWatch" ?>
<?define ProductVersion = "1.0.0.0" ?>
<?define Manufacturer = "StorageWatch Project" ?>
<?define UpgradeCode = "YOUR-UNIQUE-GUID-HERE" ?>
```

### 8. Review License Text

Edit `StorageWatchInstaller/License.rtf` to ensure the CC0 license text is accurate for your distribution.

### 9. Add Digital Signature (Production)

For production releases, sign the MSI:

```powershell
# Sign with authenticode certificate
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com StorageWatchInstaller.msi
```

This increases user trust and avoids Windows SmartScreen warnings.

---

## 🚀 First Build

Once you've completed the required steps above:

1. **Run the build script:**
   ```powershell
   .\build-installer.ps1
   ```

2. **If build fails, check:**
   - Icon.ico is a valid icon file (not text)
   - WiX Toolset is installed
   - All project dependencies built successfully
   - No GUID conflicts in Components.wxs

3. **If build succeeds:**
   - Test install on a clean VM
   - Verify service starts
   - Verify UI launches
   - Test uninstall

---

## 📝 Quick Validation Checklist

Before distributing the installer:

- [ ] Icon.ico replaced with actual icon
- [ ] All GUIDs are unique (not placeholders)
- [ ] UpgradeCode documented and never changed
- [ ] WiX Toolset installed
- [ ] .NET 10 SDK installed
- [ ] Build script runs successfully
- [ ] Test install on clean Windows 10/11 VM
- [ ] Service installs and starts automatically
- [ ] UI launches without errors
- [ ] Configuration file deployed to ProgramData
- [ ] Start Menu shortcuts work
- [ ] Desktop shortcut (optional) works
- [ ] Upgrade from previous version tested
- [ ] Uninstall tested (standard and complete removal)
- [ ] MSI signed with digital certificate (production)

---

## 🛠️ Troubleshooting Common Issues

### "Cannot find WiX"
**Solution:** Run `dotnet tool install --global wix`

### "Icon file not found or invalid"
**Solution:** Replace `icon.ico` with a valid `.ico` file

### "GUID conflict in Components"
**Solution:** Generate unique GUIDs for all components

### "Project reference failed"
**Solution:** Build StorageWatchService and StorageWatchUI projects first

### "File not found: e_sqlite3.dll"
**Solution:** Ensure NuGet packages are restored (`dotnet restore`)

---

## 📚 Next Steps

After your first successful build:

1. Read [docs/Installer/Testing.md](docs/Installer/Testing.md) for testing procedures
2. Review [docs/Installer/UpgradeBehavior.md](docs/Installer/UpgradeBehavior.md) for upgrade strategy
3. Review [docs/Installer/UninstallBehavior.md](docs/Installer/UninstallBehavior.md) for uninstall behavior
4. Plan your release versioning strategy
5. Document your GUID decisions

---

## 📞 Need Help?

- **Documentation**: `docs/Installer/`
- **Issues**: https://github.com/tuckerproject/DiskSpaceService/issues
- **WiX Docs**: https://wixtoolset.org/docs/
