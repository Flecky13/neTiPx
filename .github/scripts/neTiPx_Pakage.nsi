;
;--------------------------------
; Variablen-Definitionen
;--------------------------------
Unicode true
!define AppName "neTiPx"
!define Publisher "Pedro Tepe"
!define InstallBase "$PROGRAMFILES64"
!ifndef ProjectRoot
  !define ProjectRoot "..\..\"
!endif
!define SourcePath "${ProjectRoot}\publish\windows-x64"
!define PackagePath "${ProjectRoot}\packages"
!define ExeName "neTiPx.UI.Avalonia.exe"
!define exe_to_read "${SourcePath}\${ExeName}"

;--------------------------------
; Dateiversion auslesen
;--------------------------------
!ifndef AppVersion
  !getdllversion "${exe_to_read}" expv_
  !define AppVersion "V${expv_1}.${expv_2}.${expv_3}.${expv_4}"
!endif

!define OutFilename "${AppName}_Setup_${AppVersion}.exe"

Name "${AppName} ${AppVersion}"
OutFile "${PackagePath}\${OutFilename}"
InstallDir "${InstallBase}\${AppName}"
InstallDirRegKey HKLM "Software\${AppName}" "Install_Dir"
Icon "${ProjectRoot}\src\neTiPx.UI.Avalonia\Assets\toolicon.ico"
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
; Installation
;--------------------------------
Section "Install"

SetOutPath "$INSTDIR"

; Alle Dateien aus dem Build-Verzeichnis
File /r "${SourcePath}\*.*"

; Startmenü
CreateDirectory "$SMPROGRAMS\${AppName}"
CreateShortCut "$SMPROGRAMS\${AppName}\${AppName}.lnk" "$INSTDIR\${ExeName}" "" "$INSTDIR\${ExeName}" 0

; Desktop-Verknüpfung
CreateShortCut "$DESKTOP\${AppName}.lnk" "$INSTDIR\${ExeName}" "" "$INSTDIR\${ExeName}" 0

; Uninstaller
WriteUninstaller "$INSTDIR\Uninstall.exe"

; Registry-Eintrag
SetRegView 64
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "DisplayName" "${AppName}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "DisplayVersion" "${AppVersion}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "Publisher" "${Publisher}"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "InstallLocation" "$INSTDIR"
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "DisplayIcon" "$INSTDIR\${ExeName}"
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "NoModify" 1
WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}" "NoRepair" 1
SetRegView 32

SectionEnd

;--------------------------------
; Uninstaller
;--------------------------------
Section "Uninstall"

; Verknüpfungen löschen
Delete "$DESKTOP\${AppName}.lnk"
Delete "$SMPROGRAMS\${AppName}\${AppName}.lnk"
RMDir "$SMPROGRAMS\${AppName}"

; Registry entfernen
SetRegView 64
DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${AppName}"
SetRegView 32

; Installationsverzeichnis löschen
RMDir /r "$INSTDIR"

SectionEnd

;--------------------------------
; Nach Installation: App starten
;--------------------------------
Function .onInstSuccess
MessageBox MB_YESNO|MB_ICONQUESTION "Moechten Sie ${AppName} jetzt starten?" IDNO +2
    Exec "$INSTDIR\${ExeName}"
FunctionEnd
