$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet publish .\AirBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -o .\publish
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Write-Host "Done! Open $PSScriptRoot\publish\AirBridge.exe" -ForegroundColor Green