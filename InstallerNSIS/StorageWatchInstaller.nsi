Unicode true
RequestExecutionLevel admin

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

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

Section -StorageWatchAgent SecService
    SetShellVarContext all
    Call StopServiceIfRunning

    SetOutPath "$INSTDIR\Agent"
    File /r "${PAYLOAD_DIR}\Agent\*"

    Call CreateAgentProgramData
    Call InstallService
SectionEnd

Section -StorageWatchCentralServer SecServer
    SetShellVarContext all
    Call StopServerIfRunning

    SetOutPath "$INSTDIR\Server"
    File /r "${PAYLOAD_DIR}\Server\*"

    Call CreateServerProgramData
    Call InstallServerService
SectionEnd

Section -StorageWatchUI SecUI
    SetShellVarContext all
    SetOutPath "$INSTDIR\UI"
    File /r "${PAYLOAD_DIR}\UI\*"

    CreateDirectory "$SMPROGRAMS\${STARTMENU_FOLDER}"
    CreateShortCut "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Dashboard.lnk" "$INSTDIR\UI\StorageWatchUI.exe"
SectionEnd

Section /o "Desktop Shortcut" SecDesktop
    SetShellVarContext all
    CreateShortCut "$DESKTOP\StorageWatch Dashboard.lnk" "$INSTDIR\UI\StorageWatchUI.exe"
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

    ; Copy plugins
    SetOutPath "$APPDATA\${APP_NAME}\Plugins"
    File /nonfatal /r "${PAYLOAD_DIR}\Plugins\*.dll"
SectionEnd

Section -PostInstall
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    SetShellVarContext all
    WriteRegStr ${REG_ROOT} "${REG_KEY}" "InstallDir" "$INSTDIR"
    WriteRegStr ${REG_ROOT} "${REG_KEY}" "Role" "$SelectedRole"
    
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

Function CheckForUpdates
    DetailPrint "CheckForUpdates placeholder"
FunctionEnd

Function DownloadUpdates
    DetailPrint "DownloadUpdates placeholder"
FunctionEnd

Function ApplyUpdates
    DetailPrint "ApplyUpdates placeholder"
FunctionEnd
