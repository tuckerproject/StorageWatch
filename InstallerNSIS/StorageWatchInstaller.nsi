Unicode true
RequestExecutionLevel admin

!include "MUI2.nsh"
!include "x64.nsh"

!define APP_NAME "StorageWatch"
!define COMPANY_NAME "StorageWatch"
!define SERVICE_NAME "StorageWatchService"
!define SERVICE_DISPLAY_NAME "StorageWatch Service"
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

Section "StorageWatch Service" SecService
    SetShellVarContext all
    Call StopServiceIfRunning

    SetOutPath "$INSTDIR\Service"
    File /r "${PAYLOAD_DIR}\Service\*"

    SetOutPath "$INSTDIR\Service"
    File /r "${PAYLOAD_DIR}\SQLite\*"

    Call InstallService
SectionEnd

Section "StorageWatch Central Server" SecServer
    SetShellVarContext all
    Call StopServerIfRunning

    SetOutPath "$INSTDIR\Server"
    File /r "${PAYLOAD_DIR}\Server\*"

    CreateDirectory "$INSTDIR\Server\Data"
    CreateDirectory "$INSTDIR\Server\Logs"

    Call GenerateServerConfig
    Call InstallServerService
SectionEnd

Section "StorageWatch UI" SecUI
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

Section "ProgramData" SecProgramData
    SetShellVarContext all
    CreateDirectory "$COMMONAPPDATA\${APP_NAME}\Config"
    CreateDirectory "$COMMONAPPDATA\${APP_NAME}\Plugins"
    CreateDirectory "$COMMONAPPDATA\${APP_NAME}\Logs"
    CreateDirectory "$COMMONAPPDATA\${APP_NAME}\Data"

    Call ApplyFolderPermissions

    IfFileExists "$COMMONAPPDATA\${APP_NAME}\Config\StorageWatchConfig.json" 0 +2
        Goto SkipDefaultConfig
    SetOutPath "$COMMONAPPDATA\${APP_NAME}\Config"
    File "${PAYLOAD_DIR}\Config\StorageWatchConfig.json"
    SkipDefaultConfig:

    SetOutPath "$COMMONAPPDATA\${APP_NAME}\Plugins"
    File /r "${PAYLOAD_DIR}\Plugins\*.dll"
SectionEnd

Section -PostInstall
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
    Call StopAndRemoveService
    Call StopAndRemoveServerService

    Delete "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Dashboard.lnk"
    Delete "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Central Dashboard.lnk"
    Delete "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Server Logs.lnk"
    RMDir "$SMPROGRAMS\${STARTMENU_FOLDER}"
    Delete "$DESKTOP\StorageWatch Dashboard.lnk"

    RMDir /r "$INSTDIR\Service"
    RMDir /r "$INSTDIR\UI"
    RMDir /r "$INSTDIR\Server"
    RMDir "$INSTDIR"

    Call PromptDeleteConfig
    Call PromptDeleteLogs
    Call PromptDeleteData
    Call PromptDeletePlugins
    Call PromptDeleteServerData

    RMDir "$COMMONAPPDATA\${APP_NAME}"

    DeleteRegKey ${REG_ROOT} "${REG_KEY}"
SectionEnd

Function .onInit
    SetShellVarContext all
    IfFileExists "$INSTDIR\Service\StorageWatchService.exe" 0 checkServer
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
    
    ${NSD_CreateText} 110u 48u 100u 12u "$INSTDIR\Server\Data"
    Pop $5
    StrCpy $ServerDataDir "$INSTDIR\Server\Data"
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
    ExecWait '"$SYSDIR\sc.exe" create "${SERVICE_NAME}" binPath= "$INSTDIR\Service\StorageWatchService.exe" start= auto DisplayName= "${SERVICE_DISPLAY_NAME}"'
FunctionEnd

Function StartService
    ExecWait '"$SYSDIR\sc.exe" start "${SERVICE_NAME}"'
FunctionEnd

Function StopServiceIfRunning
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVICE_NAME}"'
FunctionEnd

Function StopAndRemoveService
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVICE_NAME}"'
    ExecWait '"$SYSDIR\sc.exe" delete "${SERVICE_NAME}"'
FunctionEnd

Function InstallServerService
    ExecWait '"$SYSDIR\sc.exe" create "${SERVER_SERVICE_NAME}" binPath= "$INSTDIR\Server\StorageWatchServer.exe" start= auto DisplayName= "${SERVER_SERVICE_DISPLAY_NAME}"'
    
    CreateDirectory "$SMPROGRAMS\${STARTMENU_FOLDER}"
    CreateShortCut "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Central Dashboard.lnk" "http://localhost:$ServerPort" "" "" 0 SW_SHOWNORMAL
    CreateShortCut "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Server Logs.lnk" "$INSTDIR\Server\Logs"
FunctionEnd

Function StartServerService
    ExecWait '"$SYSDIR\sc.exe" start "${SERVER_SERVICE_NAME}"'
FunctionEnd

Function StopServerIfRunning
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVER_SERVICE_NAME}"'
FunctionEnd

Function StopAndRemoveServerService
    ExecWait '"$SYSDIR\sc.exe" stop "${SERVER_SERVICE_NAME}"'
    ExecWait '"$SYSDIR\sc.exe" delete "${SERVER_SERVICE_NAME}"'
FunctionEnd

Function GenerateServerConfig
    FileOpen $0 "$INSTDIR\Server\appsettings.json" w
    FileWrite $0 "{$\r$\n"
    FileWrite $0 '  "Server": {$\r$\n'
    FileWrite $0 '    "ListenUrl": "http://localhost:$ServerPort",$\r$\n'
    FileWrite $0 '    "DatabasePath": "$ServerDataDir/StorageWatchServer.db",$\r$\n'
    FileWrite $0 '    "OnlineTimeoutMinutes": 10$\r$\n'
    FileWrite $0 "  }$\r$\n"
    FileWrite $0 "}"
    FileClose $0
FunctionEnd

Function ApplyFolderPermissions
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}" /grant "SYSTEM:(OI)(CI)F" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Logs" /grant "Users:(OI)(CI)M" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Data" /grant "Users:(OI)(CI)M" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Config" /grant "Users:(OI)(CI)RX" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Plugins" /grant "Users:(OI)(CI)RX" /T'
    
    ${If} $SelectedRole == "Server"
        ExecWait '"$SYSDIR\icacls.exe" "$INSTDIR\Server" /grant "SYSTEM:(OI)(CI)F" /T'
        ExecWait '"$SYSDIR\icacls.exe" "$INSTDIR\Server\Data" /grant "Users:(OI)(CI)M" /T'
        ExecWait '"$SYSDIR\icacls.exe" "$INSTDIR\Server\Logs" /grant "Users:(OI)(CI)M" /T'
    ${EndIf}
FunctionEnd

Function PromptDeleteConfig
    MessageBox MB_YESNO "Delete configuration files?" IDYES deleteConfig IDNO doneConfig
    deleteConfig:
        RMDir /r "$COMMONAPPDATA\${APP_NAME}\Config"
    doneConfig:
FunctionEnd

Function PromptDeleteLogs
    MessageBox MB_YESNO "Delete logs?" IDYES deleteLogs IDNO doneLogs
    deleteLogs:
        RMDir /r "$COMMONAPPDATA\${APP_NAME}\Logs"
    doneLogs:
FunctionEnd

Function PromptDeleteData
    MessageBox MB_YESNO "Delete SQLite data?" IDYES deleteData IDNO doneData
    deleteData:
        RMDir /r "$COMMONAPPDATA\${APP_NAME}\Data"
    doneData:
FunctionEnd

Function PromptDeleteServerData
    ${If} $SelectedRole == "Server"
        MessageBox MB_YESNO "Delete server database?" IDYES deleteServerData IDNO doneServerData
        deleteServerData:
            RMDir /r "$INSTDIR\Server\Data"
        doneServerData:
    ${EndIf}
FunctionEnd

Function PromptDeletePlugins
    MessageBox MB_YESNO "Delete plugins?" IDYES deletePlugins IDNO donePlugins
    deletePlugins:
        RMDir /r "$COMMONAPPDATA\${APP_NAME}\Plugins"
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
