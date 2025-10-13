!define APP_NAME "Everywhere"
!define PUBLISHER "DearVa"
!define EXE_NAME "Everywhere.Windows.exe"
!define UNINSTALLER_EXE "Uninstall.exe"
!define MAIN_ICON "Everywhere.ico"

; The version information will be passed in by the build script
!ifndef VERSION
  !define VERSION "1.0.0"
!endif

;--------------------------------
; General

; The name of the installer
Name "${APP_NAME} ${VERSION}"

; The file to write
OutFile "..\Everywhere-Windows-x64-Setup-v${VERSION}.exe"

; The default installation directory
InstallDir "$LOCALAPPDATA\${APP_NAME}"

; Request application privileges for Windows Vista+
RequestExecutionLevel user

; Use modern UI
!include "MUI2.nsh"

; Set compression
SetCompressor zlib

;--------------------------------
; Interface Settings

!define MUI_ABORTWARNING
!define MUI_ICON "..\img\Everywhere.ico"
!define MUI_UNICON "..\img\Everywhere.ico"

;--------------------------------
; Pages

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE_NAME}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

;--------------------------------
; Languages

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "SimpChinese"

LangString Section_Core_Name ${LANG_ENGLISH} "Core Application Files"
LangString Section_Core_Name ${LANG_SIMPCHINESE} "核心程序文件"
LangString Section_Core_Desc ${LANG_ENGLISH} "Installs the core application files."
LangString Section_Core_Desc ${LANG_SIMPCHINESE} "安装核心程序文件。"

LangString Section_StartMenu_Name ${LANG_ENGLISH} "Start Menu Shortcut"
LangString Section_StartMenu_Name ${LANG_SIMPCHINESE} "开始菜单快捷方式"
LangString Section_StartMenu_Desc ${LANG_ENGLISH} "Creates a shortcut in the Start Menu."
LangString Section_StartMenu_Desc ${LANG_SIMPCHINESE} "在开始菜单中创建快捷方式。"

LangString Section_Desktop_Name ${LANG_ENGLISH} "Desktop Shortcut"
LangString Section_Desktop_Name ${LANG_SIMPCHINESE} "桌面快捷方式"
LangString Section_Desktop_Desc ${LANG_ENGLISH} "Creates a shortcut on the Desktop."
LangString Section_Desktop_Desc ${LANG_SIMPCHINESE} "在桌面上创建快捷方式。"

LangString Uninstaller_Name ${LANG_ENGLISH} "Uninstall ${APP_NAME}"
LangString Uninstaller_Name ${LANG_SIMPCHINESE} "卸载 ${APP_NAME}"

;--------------------------------
; Installer Sections

Section "$(Section_Core_Name)" SecCore
  SectionIn RO ; Required section

  SetOutPath "$INSTDIR"
  
  ; Add all files from the publish directory to the installer
  File /r "..\publish\*.*"

  ; Store installation folder
  WriteRegStr HKCU64 "Software\${APP_NAME}" "InstallDir" "$INSTDIR"

  ; Create uninstaller
  WriteUninstaller "$INSTDIR\${UNINSTALLER_EXE}"
  
  ; Add uninstall information to the registry
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" "$INSTDIR\${UNINSTALLER_EXE}"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\${EXE_NAME}"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoModify" "1"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoRepair" "1"
  WriteRegStr HKCU64 "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Identifier" "D66EA41B-8DEB-4E5A-9D32-AB4F8305F664"
SectionEnd

Section "$(Section_StartMenu_Name)" SecStartMenu
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${EXE_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\$(Uninstaller_Name).lnk" "$INSTDIR\${UNINSTALLER_EXE}"
SectionEnd

Section /o "$(Section_Desktop_Name)" SecDesktop
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${EXE_NAME}"
SectionEnd

;--------------------------------
; Descriptions for component page

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "$(Section_Core_Desc)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} "$(Section_StartMenu_Desc)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "$(Section_Desktop_Desc)"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

;--------------------------------
; Uninstaller Section

Section "Uninstall"
  ; Remove registry keys
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
  DeleteRegKey HKCU "Software\${APP_NAME}"

  ; Remove shortcuts, if any
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\$(Uninstaller_Name).lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  
  Delete "$DESKTOP\${APP_NAME}.lnk"

  ; Remove files and directories
  RMDir /r "$INSTDIR"
SectionEnd

;--------------------------------
; Installer Functions

Function .onInit
  ; Read the installation directory from the registry if it exists
  ReadRegStr $0 HKCU "Software\${APP_NAME}" "InstallDir"
  StrCmp $0 "" 0 +2
    StrCpy $INSTDIR $0

  ; Set the language
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd