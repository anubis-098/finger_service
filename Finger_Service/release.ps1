param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'release'))
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$files=@('CJFingerService.exe','ocr.ps1','update.ps1','README.md') | ForEach-Object { Join-Path $OutputDirectory $_ }
$zip=Join-Path $OutputDirectory 'CJFingerService-win-x64.zip'
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256.txt')
Write-Output "Release asset: $zip"
