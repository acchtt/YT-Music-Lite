$ErrorActionPreference = 'Stop'
$nativeRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$nativeBuild = Join-Path $nativeRoot 'build'
New-Item -ItemType Directory -Force $nativeBuild | Out-Null
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$source = Join-Path $nativeRoot 'NativePlayer.cs'
$output = Join-Path $nativeBuild 'YTMusicLite.Native.exe'
& $csc /nologo /target:winexe /platform:x64 /langversion:5 /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$output" $source
if ($LASTEXITCODE -ne 0) { throw 'Native prototype compilation failed' }
