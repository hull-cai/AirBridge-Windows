param(
  [Parameter(Mandatory=$true)][string]$UxPlayExe,
  [string]$MsysBin = "C:\msys64\ucrt64\bin",
  [string]$OutDir = "publish\backend"
)
$ErrorActionPreference = 'Stop'
$MsysBin = (Resolve-Path $MsysBin).Path
$OutDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutDir))
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$destExe = Join-Path $OutDir "uxplay.exe"
Copy-Item -LiteralPath $UxPlayExe -Destination $destExe -Force
$pluginSource = Join-Path $MsysBin "..\lib\gstreamer-1.0"
$pluginOut = Join-Path $OutDir "gstreamer-1.0"
if (-not (Test-Path $pluginSource)) { throw "GStreamer plugins not installed: $pluginSource" }
New-Item -ItemType Directory -Force -Path $pluginOut | Out-Null
# Curated set for AirPlay video/audio with GStreamer. We dynamically copy dependencies.
$pluginNames = @(
 "coreelements","playback","app","typefindfunctions","autodetect",
 "audioconvert","audioresample","volume","level","videoconvertscale","videoconvert","videoscale","videorate",
 "wasapi","wasapi2","d3d11","d3d12","directsound","directdraw","dshow",
 "libav","rtp","rtpmanager","udp","tcp","opus","audioparsers","videoparsersbad",
 "isomp4","matroska","rawparse","openh264","nice","gio","soup",
 "videofilter","audiofx","interleave","audiomixer","videomixer","compositor"
)
$pluginFiles = @()
foreach ($p in $pluginNames) {
   $file = Join-Path $pluginSource "libgst$p.dll"
   if (Test-Path $file) {
      $to = Join-Path $pluginOut ([IO.Path]::GetFileName($file))
      Copy-Item $file $to -Force
      $pluginFiles += $to
   }
}
foreach ($required in @("libgstcoreelements.dll","libgstplayback.dll","libgstlibav.dll","libgstlevel.dll")) {
   if (-not (Test-Path (Join-Path $pluginOut $required))) { throw "Missing critical GStreamer plugin: $required" }
}
$scannerSource = Join-Path $MsysBin "..\libexec\gstreamer-1.0\gst-plugin-scanner.exe"
$extra = @()
if (Test-Path $scannerSource) {
   $scanner = Join-Path $OutDir "gst-plugin-scanner.exe"
   Copy-Item $scannerSource $scanner -Force
   $extra += $scanner
}
$inspectSource = Join-Path $MsysBin "gst-inspect-1.0.exe"
if (Test-Path $inspectSource) {
   $inspect = Join-Path $OutDir "gst-inspect-1.0.exe"
   Copy-Item $inspectSource $inspect -Force
   $extra += $inspect
}
$objdump = Join-Path $MsysBin "objdump.exe"
if (-not (Test-Path $objdump)) { throw "Missing objdump in MSYS2: $objdump" }
$queue = [System.Collections.Generic.Queue[string]]::new()
@($destExe) + $pluginFiles + $extra | ForEach-Object { $queue.Enqueue($_) }
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$systemDlls = '^(kernel32|user32|gdi32|advapi32|ws2_32|ole32|oleaut32|shell32|shlwapi|comdlg32|comctl32|crypt32|secur32|bcrypt|ncrypt|rpcrt4|iphlpapi|dnsapi|winmm|version|msvcrt|ucrtbase|ntdll|imm32|setupapi|cfgmgr32|winhttp|wininet|powrprof|normaliz|dwmapi|d3d11|dxgi|d3d12|d2d1|dwrite|mfplat|mfreadwrite|mfuuid|avrt|propsys|userenv|vcruntime140|api-ms-win-.*|ext-ms-.*)\.dll$'
while ($queue.Count -gt 0) {
  $file = $queue.Dequeue()
  if (-not $seen.Add($file)) { continue }
  $imports = & $objdump -p $file 2>$null | Select-String '^\s*DLL Name:\s*(\S+)' 
  foreach ($entry in $imports) {
     $dll = $entry.Matches[0].Groups[1].Value
     $source = Join-Path $MsysBin $dll
     if (Test-Path $source) {
       $target = Join-Path $OutDir $dll
       if (-not (Test-Path $target)) { Copy-Item $source $target }
       $queue.Enqueue($target)
     } elseif ($dll -notmatch $systemDlls) {
       Write-Warning "Unresolved imported DLL $dll in $file"
     }
  }
}
if (-not (Test-Path (Join-Path $OutDir "libgstreamer-1.0-0.dll"))) { throw "Missing libgstreamer runtime after copy" }
Write-Host ("Bundled " + @(Get-ChildItem $OutDir -Recurse -File).Count + " backend files.")
