param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'publish-folder'),[switch]$LegacySingleFile)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.dotnet'
$singleFile=if($LegacySingleFile){'true'}else{'false'}
# Use a fresh staging directory so stale files never enter a release manifest.
$stage=Join-Path $PSScriptRoot ('obj/package-'+[Guid]::NewGuid().ToString('N'))
dotnet publish (Join-Path $PSScriptRoot 'FingerService.csproj') -c Release -r win-x64 --self-contained true "-p:PublishSingleFile=$singleFile" "-p:EnableCompressionInSingleFile=$singleFile" "-p:IncludeNativeLibrariesForSelfExtract=$singleFile" -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $stage 'README.md') -Force
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if(!$LegacySingleFile) {
    $stagePath=(Resolve-Path -LiteralPath $stage).Path
    $entries=@(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { !$_.FullName.StartsWith((Join-Path $stagePath 'ubuntu')+[IO.Path]::DirectorySeparatorChar) } | Sort-Object FullName | ForEach-Object {
        @{Path=$_.FullName.Substring($stagePath.Length+1).Replace('\','/');Size=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    [xml]$project=Get-Content (Join-Path $PSScriptRoot 'FingerService.csproj')
    @{Format=1;Version=[string]$project.Project.PropertyGroup.Version;Files=$entries} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'package-manifest.json') -Encoding UTF8
}
Get-ChildItem -LiteralPath $stage | Copy-Item -Destination $OutputDirectory -Recurse -Force
Write-Output "Built: $OutputDirectory\CJFingerService.exe"
