Unicode true
RequestExecutionLevel admin

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!include "WordFunc.nsh"

!define APP_NAME "StorageWatch"
!define COMPANY_NAME "StorageWatch"
!define SERVICE_NAME "StorageWatchAgent"
!define SERVICE_DISPLAY_NAME "StorageWatch Agent"
!define SERVER_SERVICE_NAME "StorageWatchServer"
!define SERVER_SERVICE_DISPLAY_NAME "StorageWatch Central Server"
!define STARTMENU_FOLDER "StorageWatch"
!define REG_ROOT "HKLM"
!define REG_KEY "Software\StorageWatch"

!ifndef PAYLOAD_DIR
!define PAYLOAD_DIR "Payload"
!endif

InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey ${REG_ROOT} "${REG_KEY}" "InstallDir"

Name "${APP_NAME}"
OutFile "StorageWatchInstaller.exe"

!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
Page custom RoleSelectionPage RoleSelectionLeave
Page custom ServerConfigPage ServerConfigLeave
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Var DesktopShortcutSelected
Var SelectedRole
Var ServerPort
Var ServerDataDir
Var hwndRoleAgent
Var hwndRoleServer
Var ServerDataDirEscaped
Var InstallerVersion

Section -StorageWatchAgent SecService
    SetShellVarContext all
    Call StopServiceIfRunning

    Push "$INSTDIR\Agent\StorageWatchAgent.exe"
    Call SetOverwriteModeForComponent

    SetOutPath "$INSTDIR\Agent"
    DetailPrint "[INSTALL] Agent copy mode: overwrite only when installer is newer; otherwise copy missing files only."
    File /r /x "appsettings.json" /x "AutoUpdate.json" /x "AutoUpdateSettings.json" /x "updater\*" "${PAYLOAD_DIR}\Agent\*"
    SetOverwrite on

    IfFileExists "$INSTDIR\Agent\appsettings.json" AgentAppSettingsExists
    DetailPrint "[INSTALL] Agent appsettings.json not found. Deploying default appsettings.json."
    SetOutPath "$INSTDIR\Agent"
    File "${PAYLOAD_DIR}\Agent\appsettings.json"
    Goto AgentAppSettingsDone
AgentAppSettingsExists:
    DetailPrint "[INSTALL] Agent appsettings.json exists. Preserving existing file."
AgentAppSettingsDone:

    IfFileExists "$INSTDIR\Agent\AutoUpdate.json" AgentAutoUpdateExists
    DetailPrint "[INSTALL] Agent AutoUpdate.json not found. Deploying default AutoUpdate.json if present."
    SetOutPath "$INSTDIR\Agent"
    File /nonfatal "${PAYLOAD_DIR}\Agent\AutoUpdate.json"
    Goto AgentAutoUpdateDone
AgentAutoUpdateExists:
    DetailPrint "[INSTALL] Agent AutoUpdate.json exists. Preserving existing file."
AgentAutoUpdateDone:

    IfFileExists "$INSTDIR\Agent\AutoUpdateSettings.json" AgentAutoUpdateSettingsExists
    DetailPrint "[INSTALL] Agent AutoUpdateSettings.json not found. Deploying default AutoUpdateSettings.json if present."
    SetOutPath "$INSTDIR\Agent"
    File /nonfatal "${PAYLOAD_DIR}\Agent\AutoUpdateSettings.json"
    Goto AgentAutoUpdateSettingsDone
AgentAutoUpdateSettingsExists:
    DetailPrint "[INSTALL] AutoUpdate settings preserved (Agent AutoUpdateSettings.json)."
AgentAutoUpdateSettingsDone:

    Call CreateAgentProgramData
    Call InstallService
SectionEnd

Section -StorageWatchCentralServer SecServer
    SetShellVarContext all
    Call StopServerIfRunning

    Push "$INSTDIR\Server\StorageWatchServer.exe"
    Call SetOverwriteModeForComponent

    SetOutPath "$INSTDIR\Server"
    DetailPrint "[INSTALL] Server copy mode: overwrite only when installer is newer; otherwise copy missing files only."
    File /r /x "appsettings.json" /x "AutoUpdate.json" /x "AutoUpdateSettings.json" /x "updater\*" "${PAYLOAD_DIR}\Server\*"
    SetOverwrite on

    IfFileExists "$INSTDIR\Server\appsettings.json" ServerAppSettingsExists
    DetailPrint "[INSTALL] Server appsettings.json not found. Deploying default appsettings.json."
    SetOutPath "$INSTDIR\Server"
    File "${PAYLOAD_DIR}\Server\appsettings.json"
    Goto ServerAppSettingsDone
ServerAppSettingsExists:
    DetailPrint "[INSTALL] Server appsettings.json exists. Preserving existing file."
ServerAppSettingsDone:

    IfFileExists "$INSTDIR\Server\AutoUpdate.json" ServerAutoUpdateExists
    DetailPrint "[INSTALL] Server AutoUpdate.json not found. Deploying default AutoUpdate.json if present."
    SetOutPath "$INSTDIR\Server"
    File /nonfatal "${PAYLOAD_DIR}\Server\AutoUpdate.json"
    Goto ServerAutoUpdateDone
ServerAutoUpdateExists:
    DetailPrint "[INSTALL] Server AutoUpdate.json exists. Preserving existing file."
ServerAutoUpdateDone:

    IfFileExists "$INSTDIR\Server\AutoUpdateSettings.json" ServerAutoUpdateSettingsExists
    DetailPrint "[INSTALL] Server AutoUpdateSettings.json not found. Deploying default AutoUpdateSettings.json if present."
    SetOutPath "$INSTDIR\Server"
    File /nonfatal "${PAYLOAD_DIR}\Server\AutoUpdateSettings.json"
    Goto ServerAutoUpdateSettingsDone
ServerAutoUpdateSettingsExists:
    DetailPrint "[INSTALL] AutoUpdate settings preserved (Server AutoUpdateSettings.json)."
ServerAutoUpdateSettingsDone:

    Call CreateServerProgramData
    Call InstallServerService
SectionEnd

Section -StorageWatchUI SecUI
    SetShellVarContext all

    Push "$INSTDIR\UI\StorageWatchUI.exe"
    Call SetOverwriteModeForComponent

    SetOutPath "$INSTDIR\UI"
    DetailPrint "[INSTALL] UI copy mode: overwrite only when installer is newer; otherwise copy missing files only."
    File /r /x "appsettings.json" /x "AutoUpdate.json" /x "AutoUpdateSettings.json" /x "updater\*" "${PAYLOAD_DIR}\UI\*"
    SetOverwrite on

    IfFileExists "$INSTDIR\UI\appsettings.json" UiAppSettingsExists
    DetailPrint "[INSTALL] UI appsettings.json not found. Deploying default appsettings.json."
    SetOutPath "$INSTDIR\UI"
    File "${PAYLOAD_DIR}\UI\appsettings.json"
    Goto UiAppSettingsDone
UiAppSettingsExists:
    DetailPrint "[INSTALL] UI appsettings.json exists. Preserving existing file."
UiAppSettingsDone:

    IfFileExists "$INSTDIR\UI\AutoUpdate.json" UiAutoUpdateExists
    DetailPrint "[INSTALL] UI AutoUpdate.json not found. Deploying default AutoUpdate.json if present."
    SetOutPath "$INSTDIR\UI"
    File /nonfatal "${PAYLOAD_DIR}\UI\AutoUpdate.json"
    Goto UiAutoUpdateDone
UiAutoUpdateExists:
    DetailPrint "[INSTALL] UI AutoUpdate.json exists. Preserving existing file."
UiAutoUpdateDone:

    IfFileExists "$INSTDIR\UI\AutoUpdateSettings.json" UiAutoUpdateSettingsExists
    DetailPrint "[INSTALL] UI AutoUpdateSettings.json not found. Deploying default AutoUpdateSettings.json if present."
    SetOutPath "$INSTDIR\UI"
    File /nonfatal "${PAYLOAD_DIR}\UI\AutoUpdateSettings.json"
    Goto UiAutoUpdateSettingsDone
UiAutoUpdateSettingsExists:
    DetailPrint "[INSTALL] AutoUpdate settings preserved (UI AutoUpdateSettings.json)."
UiAutoUpdateSettingsDone:

    CreateDirectory "$SMPROGRAMS\${STARTMENU_FOLDER}"
    CreateShortCut "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Dashboard.lnk" "$INSTDIR\UI\StorageWatchUI.exe"
SectionEnd

Section -StorageWatchUpdater SecUpdater
    SetShellVarContext all
    CreateDirectory "$INSTDIR\Updater"
    Call InstallSharedUpdater
    Call ApplyUpdaterPermissions
    Call CleanupLegacyUpdaterCopies
SectionEnd

Section -ProgramData SecProgramData
    SetShellVarContext all

    ; Create root ProgramData folder structure for the new Agent/Server split
    CreateDirectory "$APPDATA\${APP_NAME}"
    CreateDirectory "$APPDATA\${APP_NAME}\Agent"
    CreateDirectory "$APPDATA\${APP_NAME}\Server"
    CreateDirectory "$APPDATA\${APP_NAME}\Plugins"
    CreateDirectory "$APPDATA\${APP_NAME}\Logs"

    Call ApplyFolderPermissions

    ; Stage plugins from installer payload and install with version-aware overwrite rules
    SetOutPath "$PLUGINSDIR\Plugins"
    File /nonfatal /r "${PAYLOAD_DIR}\Plugins\*.dll"
    Call InstallPluginDllsRespectingInstallerVersion
SectionEnd

Section -PostInstall
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    SetShellVarContext all
    WriteRegStr ${REG_ROOT} "${REG_KEY}" "InstallDir" "$INSTDIR"
    WriteRegStr ${REG_ROOT} "${REG_KEY}" "Role" "$SelectedRole"
    Call WriteVersionMetadata
    
    ${If} $SelectedRole == "Agent"
        Call StartService
    ${Else}
        Call StartServerService
    ${EndIf}
SectionEnd

Section "Uninstall"
    SetShellVarContext all
    Call un.StopAndRemoveService
    Call un.StopAndRemoveServerService

    Delete "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Dashboard.lnk"
    Delete "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Central Dashboard.lnk"
    Delete "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Server Logs.lnk"
    RMDir "$SMPROGRAMS\${STARTMENU_FOLDER}"
    Delete "$DESKTOP\StorageWatch Dashboard.lnk"

    RMDir /r "$INSTDIR\Agent"
    RMDir /r "$INSTDIR\UI"
    RMDir /r "$INSTDIR\Server"
    Call un.SafeRemoveSharedUpdater
    RMDir "$INSTDIR"

    ; Note: Do NOT delete ProgramData directories (Agent, Server, Logs, Plugins)
    ; to preserve user configuration and data during upgrades
    Call un.PromptDeleteLogs
    Call un.PromptDeletePlugins

    DeleteRegKey ${REG_ROOT} "${REG_KEY}"
SectionEnd

Function .onInit
    SetShellVarContext all
    IfFileExists "$INSTDIR\Agent\StorageWatchAgent.exe" 0 checkServer
    Call StopServiceIfRunning
    checkServer:
    IfFileExists "$INSTDIR\Server\StorageWatchServer.exe" 0 done
    Call StopServerIfRunning
    done:
FunctionEnd

Function RoleSelectionPage
    !insertmacro MUI_HEADER_TEXT "Select Installation Role" "Choose whether to install StorageWatch as an Agent or Central Server"
    
    nsDialogs::Create 1018
    Pop $0
    
    ${NSD_CreateGroupBox} 10u 10u 280u 60u "Installation Role"
    Pop $1
    
    ${NSD_CreateRadioButton} 20u 25u 150u 12u "Agent (Local Monitoring)"
    Pop $hwndRoleAgent
    ${NSD_OnClick} $hwndRoleAgent RoleChanged
    SendMessage $hwndRoleAgent ${BM_SETCHECK} 1 0
    
    ${NSD_CreateRadioButton} 20u 45u 150u 12u "Central Server (Aggregation & Dashboard)"
    Pop $hwndRoleServer
    ${NSD_OnClick} $hwndRoleServer RoleChanged
    
    ${NSD_CreateLabel} 10u 85u 280u 80u "Agent Mode:$\r$\nMonitors local disks and stores data locally.$\r$\nOptionally reports to a central server.$\r$\n$\r$\nCentral Server Mode:$\r$\nAggregates data from multiple agents.$\r$\nHosts web dashboard accessible via browser.$\r$\nRequires network connectivity for agents to report."
    Pop $2
    
    nsDialogs::Show
FunctionEnd

Function RoleSelectionLeave
    ${NSD_GetState} $hwndRoleAgent $0
    ${If} $0 == 1
        StrCpy $SelectedRole "Agent"
        SectionSetFlags ${SecServer} 0
    ${Else}
        StrCpy $SelectedRole "Server"
        SectionSetFlags ${SecServer} ${SF_SELECTED}
    ${EndIf}
FunctionEnd

Function RoleChanged
FunctionEnd

Function ServerConfigPage
    ${If} $SelectedRole != "Server"
        Abort
    ${EndIf}
    
    !insertmacro MUI_HEADER_TEXT "Configure Central Server" "Specify server port and data directory"
    
    nsDialogs::Create 1018
    Pop $0
    
    ${NSD_CreateGroupBox} 10u 10u 280u 100u "Server Configuration"
    Pop $1
    
    ${NSD_CreateLabel} 20u 25u 80u 12u "Port:"
    Pop $2
    
    ${NSD_CreateNumber} 110u 23u 60u 12u "5001"
    Pop $3
    StrCpy $ServerPort "5001"
    ${NSD_OnChange} $3 ServerPortChanged
    
    ${NSD_CreateLabel} 20u 50u 80u 12u "Data Directory:"
    Pop $4
    
    ${NSD_CreateText} 110u 48u 100u 12u "$APPDATA\${APP_NAME}\Data"
    Pop $5
    StrCpy $ServerDataDir "$APPDATA\${APP_NAME}\Data"
    ${NSD_OnChange} $5 ServerDataDirChanged
    
    ${NSD_CreateLabel} 20u 70u 260u 35u "The server will listen on http://localhost:<port>$\r$\nand store the SQLite database in the specified directory.$\r$\nEnsure the data directory has sufficient disk space."
    Pop $6
    
    nsDialogs::Show
FunctionEnd

Function ServerPortChanged
    Pop $ServerPort
FunctionEnd

Function ServerDataDirChanged
    Pop $ServerDataDir
FunctionEnd

Function ServerConfigLeave
FunctionEnd

Function InstallService
    ExecWait '"$SYSDIR\sc.exe" create "${SERVICE_NAME}" binPath= "$INSTDIR\Agent\StorageWatchAgent.exe" start= auto DisplayName= "${SERVICE_DISPLAY_NAME}"'
FunctionEnd

Function StartService
    ExecWait '"$SYSDIR\sc.exe" start "${SERVICE_NAME}"'
FunctionEnd

Function StopServiceIfRunning
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVICE_NAME}"'
FunctionEnd

Function un.StopAndRemoveService
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVICE_NAME}"'
    ExecWait '"$SYSDIR\sc.exe" delete "${SERVICE_NAME}"'
FunctionEnd

Function InstallServerService
    ; Remove any existing broken service
    ExecWait '"$SYSDIR\sc.exe" delete "${SERVER_SERVICE_NAME}"'

    ; Correct service creation with contentRoot
    ExecWait '"$SYSDIR\sc.exe" create "${SERVER_SERVICE_NAME}" binPath= "\"$INSTDIR\Server\StorageWatchServer.exe\" --contentRoot \"$INSTDIR\Server\"" start= auto DisplayName= "${SERVER_SERVICE_DISPLAY_NAME}" obj= LocalSystem'
    
    ; Start menu shortcuts
    CreateDirectory "$SMPROGRAMS\${STARTMENU_FOLDER}"
    CreateShortCut "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Central Dashboard.lnk" "http://localhost:$ServerPort"
    CreateShortCut "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Logs.lnk" "$APPDATA\${APP_NAME}\Logs"
FunctionEnd

Function StartServerService
    ExecWait '"$SYSDIR\sc.exe" start "${SERVER_SERVICE_NAME}"'
FunctionEnd

Function StopServerIfRunning
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVER_SERVICE_NAME}"'
FunctionEnd

Function un.StopAndRemoveServerService
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVER_SERVICE_NAME}"'
    ExecWait '"$SYSDIR\sc.exe" delete "${SERVER_SERVICE_NAME}"'
FunctionEnd

Function EscapeBackslashes
    Exch $0 ; input
    Push $1
    Push $2
    StrCpy $1 ""
loop:
    StrCpy $2 $0 1
    StrCmp $2 "" done
    StrCmp $2 "\" addslash noslash
addslash:
    StrCpy $1 "$1\\"
    Goto next
noslash:
    StrCpy $1 "$1$2"
next:
    StrCpy $0 $0 "" 1
    Goto loop
done:
    Pop $2
    Pop $0
    Exch $1 ; output
FunctionEnd

Function GenerateServerConfig
    ; Escape backslashes for JSON
    Push $ServerDataDir
    Call EscapeBackslashes
    Pop $ServerDataDirEscaped

    FileOpen $0 "$INSTDIR\Server\appsettings.json" w
    FileWrite $0 "{$\r$\n"
    FileWrite $0 '  "Server": {$\r$\n'
    FileWrite $0 '    "ListenUrl": "http://localhost:$ServerPort",$\r$\n'
    FileWrite $0 '    "DatabasePath": "$ServerDataDirEscaped\\StorageWatchServer.db",$\r$\n'
    FileWrite $0 '    "OnlineTimeoutMinutes": 10$\r$\n'
    FileWrite $0 "  }$\r$\n"
    FileWrite $0 "}"
    FileClose $0
FunctionEnd

Function ApplyFolderPermissions
    ExecWait '"$SYSDIR\icacls.exe" "$APPDATA\${APP_NAME}" /grant "SYSTEM:(OI)(CI)F" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$APPDATA\${APP_NAME}\Agent" /grant "Users:(OI)(CI)M" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$APPDATA\${APP_NAME}\Server" /grant "Users:(OI)(CI)M" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$APPDATA\${APP_NAME}\Logs" /grant "Users:(OI)(CI)M" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$APPDATA\${APP_NAME}\Plugins" /grant "Users:(OI)(CI)RX" /T'
FunctionEnd

Function InstallSharedUpdater
    ; Install exactly one shared updater at $INSTDIR\Updater\StorageWatch.Updater.exe
    IfFileExists "$INSTDIR\Updater\StorageWatch.Updater.exe" compareVersions copyMissing

copyMissing:
    DetailPrint "[INSTALL] Updater EXE installed (missing target)."
    SetOutPath "$INSTDIR\Updater"
    SetOverwrite on
    File /nonfatal "${PAYLOAD_DIR}\UI\updater\StorageWatch.Updater.exe"
    File /nonfatal "${PAYLOAD_DIR}\Agent\updater\StorageWatch.Updater.exe"
    File /nonfatal "${PAYLOAD_DIR}\Server\updater\StorageWatch.Updater.exe"
    Return

compareVersions:
    ClearErrors
    ${GetFileVersion} "$EXEPATH" $0
    ${If} ${Errors}
        DetailPrint "[INSTALL] Could not determine installer version. Preserving existing shared updater."
        Return
    ${EndIf}

    ClearErrors
    ${GetFileVersion} "$INSTDIR\Updater\StorageWatch.Updater.exe" $1
    ${If} ${Errors}
        DetailPrint "[INSTALL] Existing shared updater version unknown. Updater EXE installed."
        SetOutPath "$INSTDIR\Updater"
        SetOverwrite on
        File /nonfatal "${PAYLOAD_DIR}\UI\updater\StorageWatch.Updater.exe"
        File /nonfatal "${PAYLOAD_DIR}\Agent\updater\StorageWatch.Updater.exe"
        File /nonfatal "${PAYLOAD_DIR}\Server\updater\StorageWatch.Updater.exe"
        Return
    ${EndIf}

    ${VersionCompare} $0 $1 $2
    ${If} $2 == 1
        DetailPrint "[INSTALL] Updater EXE installed (installer version is newer)."
        SetOutPath "$INSTDIR\Updater"
        SetOverwrite on
        File /nonfatal "${PAYLOAD_DIR}\UI\updater\StorageWatch.Updater.exe"
        File /nonfatal "${PAYLOAD_DIR}\Agent\updater\StorageWatch.Updater.exe"
        File /nonfatal "${PAYLOAD_DIR}\Server\updater\StorageWatch.Updater.exe"
    ${ElseIf} $2 == 0
        DetailPrint "[INSTALL] Updater EXE skipped due to newer installed version."
    ${Else}
        DetailPrint "[INSTALL] Updater EXE skipped (installed version is equal)."
    ${EndIf}
FunctionEnd

Function ApplyUpdaterPermissions
    ; Users: read/execute. Administrators: modify (includes write). System: full control.
    ExecWait '"$SYSDIR\icacls.exe" "$INSTDIR\Updater" /grant "Users:(OI)(CI)RX"'
    ExecWait '"$SYSDIR\icacls.exe" "$INSTDIR\Updater" /grant "Administrators:(OI)(CI)M"'
    ExecWait '"$SYSDIR\icacls.exe" "$INSTDIR\Updater" /grant "SYSTEM:(OI)(CI)F"'
FunctionEnd

Function CleanupLegacyUpdaterCopies
    ; Ensure only one updater copy remains on disk.
    Delete "$INSTDIR\Agent\updater\StorageWatch.Updater.exe"
    Delete "$INSTDIR\Server\updater\StorageWatch.Updater.exe"
    Delete "$INSTDIR\UI\updater\StorageWatch.Updater.exe"
    RMDir "$INSTDIR\Agent\updater"
    RMDir "$INSTDIR\Server\updater"
    RMDir "$INSTDIR\UI\updater"
FunctionEnd

Function CreateAgentProgramData
    ; Create Agent subdirectories (already created in SecProgramData, but ensure they exist)
    CreateDirectory "$APPDATA\${APP_NAME}\Agent"
    CreateDirectory "$APPDATA\${APP_NAME}\Logs"

    ; Copy default AgentConfig.json ONLY if it does NOT already exist
    IfFileExists "$APPDATA\${APP_NAME}\Agent\AgentConfig.json" SkipAgentConfig
    
    SetOutPath "$APPDATA\${APP_NAME}\Agent"
    File "${PAYLOAD_DIR}\Agent\Defaults\AgentConfig.default.json"
    
    ; Rename the default file to AgentConfig.json
    Rename "$APPDATA\${APP_NAME}\Agent\AgentConfig.default.json" "$APPDATA\${APP_NAME}\Agent\AgentConfig.json"

    SkipAgentConfig:
FunctionEnd

Function CreateServerProgramData
    ; Create Server subdirectories (already created in SecProgramData, but ensure they exist)
    CreateDirectory "$APPDATA\${APP_NAME}\Server"
    CreateDirectory "$APPDATA\${APP_NAME}\Logs"

    ; Copy default ServerConfig.json ONLY if it does NOT already exist
    IfFileExists "$APPDATA\${APP_NAME}\Server\ServerConfig.json" SkipServerConfig
    
    SetOutPath "$APPDATA\${APP_NAME}\Server"
    File "${PAYLOAD_DIR}\Server\Defaults\ServerConfig.default.json"
    
    ; Rename the default file to ServerConfig.json
    Rename "$APPDATA\${APP_NAME}\Server\ServerConfig.default.json" "$APPDATA\${APP_NAME}\Server\ServerConfig.json"

    SkipServerConfig:
FunctionEnd

Function un.PromptDeleteLogs
    MessageBox MB_YESNO "Delete logs?" IDYES deleteLogs IDNO doneLogs
    deleteLogs:
        RMDir /r "$APPDATA\${APP_NAME}\Logs"
    doneLogs:
FunctionEnd

Function un.PromptDeletePlugins
    MessageBox MB_YESNO "Delete plugins?" IDYES deletePlugins IDNO donePlugins
    deletePlugins:
        RMDir /r "$APPDATA\${APP_NAME}\Plugins"
    donePlugins:
FunctionEnd

Function WriteVersionMetadata
    ClearErrors
    ${GetFileVersion} "$EXEPATH" $0
    ${If} ${Errors}
        StrCpy $0 "unknown"
        DetailPrint "[INSTALL] Could not determine installer version from '$EXEPATH'. Writing 'unknown'."
    ${EndIf}

    FileOpen $1 "$INSTDIR\version.txt" w
    FileWrite $1 "StorageWatchVersion=$0$\r$\n"
    FileClose $1

    DetailPrint "[INSTALL] Wrote version metadata to '$INSTDIR\version.txt': $0"
FunctionEnd

Function SetOverwriteModeForComponent
    Exch $0

    Push $1
    Push $2

    ; Missing target executable: allow overwrite (fresh install path)
    IfFileExists "$0" 0 allowOverwrite

    Call EnsureInstallerVersion

    StrCmp $InstallerVersion "unknown" copyMissingOnly

    ClearErrors
    ${GetFileVersion} "$0" $1
    ${If} ${Errors}
        ; Existing file has unknown version, preserve it and only copy missing files.
        Goto copyMissingOnly
    ${EndIf}

    ${VersionCompare} $InstallerVersion $1 $2
    ${If} $2 == 1
        Goto allowOverwrite
    ${Else}
        Goto copyMissingOnly
    ${EndIf}

allowOverwrite:
    SetOverwrite on
    Goto done

copyMissingOnly:
    SetOverwrite off

done:
    Pop $2
    Pop $1
    Pop $0
FunctionEnd

Function InstallPluginDllsRespectingInstallerVersion
    Push $0
    Push $1
    Push $2
    Push $3
    Push $4

    Call EnsureInstallerVersion

    FindFirst $0 $1 "$PLUGINSDIR\Plugins\*.dll"
    StrCmp $1 "" done

loop:
    StrCpy $2 "$PLUGINSDIR\Plugins\$1"
    StrCpy $3 "$APPDATA\${APP_NAME}\Plugins\$1"

    IfFileExists "$3" compare existingMissing

existingMissing:
    DetailPrint "[INSTALL] Plugin '$1' missing. Installing."
    CopyFiles /SILENT "$2" "$3"
    Goto next

compare:
    StrCmp $InstallerVersion "unknown" keepExisting

    ClearErrors
    ${GetFileVersion} "$3" $4
    ${If} ${Errors}
        DetailPrint "[INSTALL] Plugin '$1' existing version unknown. Preserving existing file."
        Goto next
    ${EndIf}

    ${VersionCompare} $InstallerVersion $4 $4
    ${If} $4 == 1
        DetailPrint "[INSTALL] Plugin '$1' overwritten because installer is newer."
        CopyFiles /SILENT "$2" "$3"
    ${Else}
keepExisting:
        DetailPrint "[INSTALL] Plugin '$1' preserved (installer is not newer)."
    ${EndIf}

next:
    FindNext $0 $1
    StrCmp $1 "" done
    Goto loop

done:
    FindClose $0

    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Pop $0
FunctionEnd

Function EnsureInstallerVersion
    StrCmp $InstallerVersion "" 0 done

    ClearErrors
    ${GetFileVersion} "$EXEPATH" $InstallerVersion
    ${If} ${Errors}
        StrCpy $InstallerVersion "unknown"
    ${EndIf}

done:
FunctionEnd

Function un.SafeRemoveSharedUpdater
    Push $0
    Push $1
    Push $2

    StrCmp "$INSTDIR" "" keepUpdater

    IfFileExists "$INSTDIR\Updater\StorageWatch.Updater.exe" 0 done

    ; Keep updater if any component still appears installed.
    IfFileExists "$INSTDIR\Agent\StorageWatchAgent.exe" keepUpdater checkServer
checkServer:
    IfFileExists "$INSTDIR\Server\StorageWatchServer.exe" keepUpdater checkUi
checkUi:
    IfFileExists "$INSTDIR\UI\StorageWatchUI.exe" keepUpdater compareVersions

compareVersions:
    ClearErrors
    ${GetFileVersion} "$EXEPATH" $0
    ${If} ${Errors}
        DetailPrint "[UNINSTALL] Could not determine uninstaller version. Preserving shared updater for safety."
        Goto keepUpdater
    ${EndIf}

    ClearErrors
    ${GetFileVersion} "$INSTDIR\Updater\StorageWatch.Updater.exe" $1
    ${If} ${Errors}
        DetailPrint "[UNINSTALL] Could not determine updater version. Removing shared updater."
        Goto removeUpdater
    ${EndIf}

    ${VersionCompare} $1 $0 $2
    ${If} $2 == 1
        DetailPrint "[UNINSTALL] Shared updater is newer than uninstaller version. Preserving updater."
        Goto keepUpdater
    ${EndIf}

removeUpdater:
    Delete "$INSTDIR\Updater\StorageWatch.Updater.exe"
    RMDir "$INSTDIR\Updater"
    Goto done

keepUpdater:
    DetailPrint "[UNINSTALL] Updater EXE preserved during uninstall."

done:
    Pop $2
    Pop $1
    Pop $0
FunctionEnd
