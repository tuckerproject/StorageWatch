Unicode true
RequestExecutionLevel admin

!include "MUI2.nsh"
!include "x64.nsh"

!define APP_NAME "StorageWatch"
!define COMPANY_NAME "StorageWatch"
!define SERVICE_NAME "StorageWatchService"
!define SERVICE_DISPLAY_NAME "StorageWatch Service"
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
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Var DesktopShortcutSelected

Section "StorageWatch Service" SecService
    SetShellVarContext all
    Call StopServiceIfRunning

    SetOutPath "$INSTDIR\Service"
    File /r "${PAYLOAD_DIR}\Service\*"

    SetOutPath "$INSTDIR\Service"
    File /r "${PAYLOAD_DIR}\SQLite\*"

    Call InstallService
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
    Call StartService
SectionEnd

Section "Uninstall"
    SetShellVarContext all
    Call StopAndRemoveService

    Delete "$SMPROGRAMS\${STARTMENU_FOLDER}\StorageWatch Dashboard.lnk"
    RMDir "$SMPROGRAMS\${STARTMENU_FOLDER}"
    Delete "$DESKTOP\StorageWatch Dashboard.lnk"

    RMDir /r "$INSTDIR\Service"
    RMDir /r "$INSTDIR\UI"
    RMDir "$INSTDIR"

    Call PromptDeleteConfig
    Call PromptDeleteLogs
    Call PromptDeleteData
    Call PromptDeletePlugins

    RMDir "$COMMONAPPDATA\${APP_NAME}"

    DeleteRegKey ${REG_ROOT} "${REG_KEY}"
SectionEnd

Function .onInit
    SetShellVarContext all
    IfFileExists "$INSTDIR\Service\StorageWatchService.exe" 0 done
    Call StopServiceIfRunning
    done:
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

Function ApplyFolderPermissions
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}" /grant "SYSTEM:(OI)(CI)F" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Logs" /grant "Users:(OI)(CI)M" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Data" /grant "Users:(OI)(CI)M" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Config" /grant "Users:(OI)(CI)RX" /T'
    ExecWait '"$SYSDIR\icacls.exe" "$COMMONAPPDATA\${APP_NAME}\Plugins" /grant "Users:(OI)(CI)RX" /T'
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
