$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '../.dotnet'
dotnet publish (Join-Path $PSScriptRoot 'FingerDummy.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $PSScriptRoot 'publish')
if ($LASTEXITCODE -ne 0) { throw 'Dummy build failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $PSScriptRoot 'publish/README.md') -Force
