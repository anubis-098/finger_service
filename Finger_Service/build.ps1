param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'publish'))
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
dotnet publish (Join-Path $PSScriptRoot 'FingerService.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $OutputDirectory 'README.md') -Force
Write-Output "Built: $OutputDirectory\CJFingerService.exe"
