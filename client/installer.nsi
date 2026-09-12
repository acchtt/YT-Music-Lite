Unicode True
RequestExecutionLevel user
SetCompressor /SOLID lzma

!ifndef VERSION
  !define VERSION "7.2.0"
!endif
!ifndef VERSION4
  !define VERSION4 "7.2.0.0"
!endif
!ifndef BUILDDIR
  !error "BUILDDIR must point to the native build directory"
!endif
!ifndef OUTDIR
  !error "OUTDIR must point to the release directory"
!endif

Name "YT Music Lite"
OutFile "${OUTDIR}\YTMusicLite-v${VERSION}-win-x64-setup.exe"
InstallDir "$LOCALAPPDATA\Programs\YTMusicLite"
InstallDirRegKey HKCU "Software\YTMusicLite" "InstallDir"
Icon "${BUILDDIR}\YTMusicLite.ico"
VIProductVersion "${VERSION4}"
VIAddVersionKey "ProductName" "YT Music Lite"
VIAddVersionKey "FileDescription" "YT Music Lite native Windows installer"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "YT Music Lite" MainSection
  SetOutPath "$INSTDIR"
  File "${BUILDDIR}\YTMusicLite.exe"
  File "${BUILDDIR}\YTMusicLite.Updater.exe"
  File "${BUILDDIR}\mpv.exe"
  File "${BUILDDIR}\yt-dlp.exe"
  File "${BUILDDIR}\deno.exe"
  File "${BUILDDIR}\VERSION.txt"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\YTMusicLite" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\YTMusicLite" "DisplayName" "YT Music Lite"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\YTMusicLite" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\YTMusicLite" "Publisher" "YT Music Lite"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\YTMusicLite" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  CreateDirectory "$SMPROGRAMS\YT Music Lite"
  CreateShortcut "$SMPROGRAMS\YT Music Lite\YT Music Lite.lnk" "$INSTDIR\YTMusicLite.exe"
  CreateShortcut "$DESKTOP\YT Music Lite.lnk" "$INSTDIR\YTMusicLite.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\YT Music Lite.lnk"
  Delete "$SMPROGRAMS\YT Music Lite\YT Music Lite.lnk"
  RMDir "$SMPROGRAMS\YT Music Lite"
  Delete "$INSTDIR\YTMusicLite.exe"
  Delete "$INSTDIR\YTMusicLite.Updater.exe"
  Delete "$INSTDIR\mpv.exe"
  Delete "$INSTDIR\yt-dlp.exe"
  Delete "$INSTDIR\deno.exe"
  Delete "$INSTDIR\VERSION.txt"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\YTMusicLite"
  DeleteRegKey HKCU "Software\YTMusicLite"
SectionEnd
