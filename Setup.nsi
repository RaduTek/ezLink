; ezLink installer (NSIS Modern UI 2)
; Build: "C:\ProgramData\NSIS\makensis.exe" Setup.nsi   (after build.cmd Release)
; Packs everything in bin\Release except *.pdb into bin\Setup.

!define APPNAME   "ezLink Software"
!define FULLAPPNAME "Remotek ezLink Software"
!define VERSION   "1.0.0"
!define PUBLISHER "Remotek Corp."
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\ezLink"

Unicode True
Name "${FULLAPPNAME}"
BrandingText "${PUBLISHER}"
!system 'if not exist "bin\Setup" mkdir "bin\Setup"'
OutFile "bin\Setup\ezLink-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES\Remotek\ezLink"
InstallDirRegKey HKLM "Software\ezLink" "InstallDir"
RequestExecutionLevel admin
SetCompressor zlib

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APPNAME} Setup"
VIAddVersionKey "FileVersion" "${VERSION}"

!include "MUI2.nsh"
!include "WinVer.nsh"

!define MUI_ICON   "ezLink\Resources\ezLink.ico"
!define MUI_UNICON "ezLink\Resources\ezLink.ico"
!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING

!define MUI_FINISHPAGE_RUN "$INSTDIR\ezLink.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Run ezLink"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Section "ezLink" SecMain
  SectionIn RO
  SetShellVarContext all
  SetOutPath "$INSTDIR"
  File /r /x "*.pdb" "bin\Release\*.*"

  ; Admin helper: machine UUID, firewall rule, data dir
  ExecWait '"$INSTDIR\setuputil.exe" /install' $0
  ${If} $0 != 0
    DetailPrint "Warning: setup helper /install returned $0 (see %ALLUSERSPROFILE%\ezLink\setup.log)"
  ${EndIf}

  ; Windows 10 and later need no driver package
  ${If} ${AtMostWin8.1}
    ExecWait '"$INSTDIR\setuputil.exe" /drivers' $0
    ${If} $0 != 0
      DetailPrint "Warning: driver install returned $0"
    ${EndIf}
  ${EndIf}

  CreateDirectory "$SMPROGRAMS\Remotek"
  CreateShortCut "$SMPROGRAMS\Remotek\ezLink.lnk" "$INSTDIR\ezLink.exe"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\ezLink" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\ezLink.exe"
  WriteRegStr HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext all

  ${If} ${AtMostWin8.1}
    ExecWait '"$INSTDIR\setuputil.exe" /remove-drivers'
  ${EndIf}
  ; Removes the firewall rule; UUID and trust store are kept
  ExecWait '"$INSTDIR\setuputil.exe" /uninstall'

  ; Files may be locked if ezLink is still running; fall back to delete on reboot
  Delete "$INSTDIR\ezLink.exe"
  Delete /REBOOTOK "$INSTDIR\ezLink.exe"
  RMDir /r /REBOOTOK "$INSTDIR"
  RMDir "$PROGRAMFILES\Remotek"

  Delete "$SMPROGRAMS\Remotek\ezLink.lnk"
  RMDir "$SMPROGRAMS\Remotek"

  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "ezLink"
  DeleteRegKey HKLM "${UNINSTKEY}"
  DeleteRegValue HKLM "Software\ezLink" "InstallDir"
SectionEnd
