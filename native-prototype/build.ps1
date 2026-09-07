$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
New-Item -ItemType Directory -Force "$root/build" | Out-Null
$csc = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
& $csc /nologo /target:winexe /platform:x64 /langversion:5 /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /out:"$root/build/YTMusicLite.Native.exe" "$root/NativePlayer.cs"
if ($LASTEXITCODE -ne 0) { throw 'Native prototype compilation failed' }
