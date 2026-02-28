;--------------------------------
; Variablen-Definitionen
;--------------------------------
!define AppName "neTiPx"
!define Publisher "Pedro Tepe"
!define InstallBase "$PROGRAMFILES"
!define WorkDir "${__FILEDIR__}.."
!define SourcePath "${WorkDir}\neTiPx.WinUI\bin\Release\net8.0-windows"
!define exe_to_read "${SourcePath}\${AppName}.WinUI.exe"

;--------------------------------
; Dateiversion auslesen
;--------------------------------
!getdllversion "${exe_to_read}" expv_
!define ReleaseVersion "V${expv_1}.${expv_2}.${expv_3}.${expv_4}"
!define OutFilename "${AppName}_Setup_${ReleaseVersion}.exe"
!define AppVersion "V${expv_1}.${expv_2}"

Name "${AppName} ${AppVersion}"
OutFile "${OutFilename}"
InstallDir "${InstallBase}\${AppName}"
InstallDirRegKey HKLM "Software\${AppName}" "Install_Dir"
Icon "${WorkDir}\icons\toolicon.ico"
Caption "${AppName} Installer"
BrandingText "© ${Publisher}"

RequestExecutionLevel admin

;--------------------------------
; GUI-Seiten
;--------------------------------
!include "MUI2.nsh"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "German"

;--------------------------------
; Uninstaller-Funktionen
;--------------------------------
Function un.IsProcessRunning
    ; Prüft, ob ${AppName}.exe läuft.
    ; Rückgabe in $0: "1" = läuft, "0" = nicht laufend
    MessageBox MB_OK|MB_ICONEXCLAMATION " App ${AppName} ist noch Aktiv"
    ; Verwende nsExec::ExecToStack, um die Ausgabe von tasklist zu erhalten.
    ; Befehl (compile-time ersetzt): tasklist /FI "IMAGENAME eq neTiPx.exe" /NH
    nsExec::ExecToStack 'tasklist /FI "IMAGENAME eq ${AppName}.exe" /NH'
    Pop $1 ; Exitcode
    Pop $2 ; erste Ausgabezeile (falls vorhanden)
    ; Wenn $2 nicht leer ist, wurde ein Prozesseintrag gefunden
    StrLen $3 $2
    IntCmp $3 0 NotRunning
        StrCpy $0 "1"
        Return
    NotRunning:
    StrCpy $0 "0"
FunctionEnd

Function CreateStartupShortcut
    StrCpy $0 "$APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
    CreateDirectory "$0"
    CreateShortCut "$0\${AppName}.lnk" "$INSTDIR\${AppName}.exe" "" "$INSTDIR\icons\toolicon.ico" 0
FunctionEnd

;--------------------------------
; Installation
;--------------------------------
Section "Install"

SetOutPath "$INSTDIR"

; Hauptprogramm
File "${SourcePath}\${AppName}.exe"

; Config (nur kopieren wenn nicht vorhanden)
;IfFileExists "$INSTDIR\config.ini" +2
;    File /oname=config.ini "${SourcePath}\config.ini"

; Icons
SetOutPath "$INSTDIR\icons"
File /r "${WorkDir}\icons\*.*"

; Daten
SetOutPath "$INSTDIR"
File /r "${SourcePath}\*.*"

; Startmenü
CreateDirectory "$SMPROGRAMS\${AppName}"
CreateShortCut "$SMPROGRAMS\${AppName}\${AppName}.lnk" "$INSTDIR\${AppName}.exe"

; Desktop-Verknüpfung
CreateShortCut "$DESKTOP\${AppName}.lnk" "$INSTDIR\${AppName}.exe"

; Uninstaller
WriteUninstaller "$INSTDIR\Uninstall.exe"

; Uninstall-Eintrag
SetRegView 64
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "DisplayName" "${AppName}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "DisplayVersion" "${AppVersion}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "Publisher" "${Publisher}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "InstallLocation" "$INSTDIR"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "DisplayIcon" "$INSTDIR\${AppName}.exe"
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "NoModify" 1
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "NoRepair" 1
SetRegView 32

SectionEnd

;--------------------------------
; Uninstaller
;--------------------------------
Section "Uninstall"

LoopCheckProcess:
    StrCpy $0 "${AppName}"
    ;MessageBox MB_OK|MB_ICONEXCLAMATION "${AppName}"
    Call un.IsProcessRunning
    ; $0 == "1" bedeutet: Prozess läuft noch
    StrCmp $0 "1" 0 +3
        ; Prozess läuft noch
        MessageBox MB_OK|MB_ICONEXCLAMATION "${AppName} läuft noch. Bitte schließen Sie das Programm manuell und klicken auf OK, um fortzufahren."
        Goto LoopCheckProcess  ; erneut prüfen

    ; Wenn hier angekommen, Prozess ist beendet, weiter mit Deinstallation

; Verknüpfungen löschen
Delete "$DESKTOP\${AppName}.lnk"
Delete "$SMPROGRAMS\${AppName}\${AppName}.lnk"
Delete "$APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\${AppName}.lnk"

RMDir "$SMPROGRAMS\${AppName}"

; Registry entfernen
SetRegView 64
DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}"
SetRegView 32

; Dateien entfernen
RMDir /r "$INSTDIR"

SectionEnd


;--------------------------------
; Events nach der Installation
;--------------------------------
Function .onInstSuccess

MessageBox MB_YESNO|MB_ICONQUESTION "Möchten Sie ${AppName} jetzt starten?" IDNO +2
    Exec "$INSTDIR\${AppName}.exe"

MessageBox MB_YESNO|MB_ICONQUESTION "Soll ${AppName} automatisch bei Windows starten?" IDNO +2
    Call CreateStartupShortcut

FunctionEnd

;--------------------------------
; Build: Dateien archivieren
;--------------------------------
;!finalize 'cmd /c if not exist "${WorkDir}\${ReleaseVersion}" mkdir "${WorkDir}\Installer\${ReleaseVersion}" & copy /Y "${OutFilename}" "${WorkDir}\Installer\${ReleaseVersion}\"'
