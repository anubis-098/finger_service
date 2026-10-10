param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'release'))
$ErrorActionPreference='Stop'
$folder=Join-Path $OutputDirectory 'folder'
$legacy=Join-Path $OutputDirectory 'legacy'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $folder
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $legacy -LegacySingleFile
$manifest=Get-Content -LiteralPath (Join-Path $folder 'package-manifest.json') -Raw | ConvertFrom-Json
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$folderZip=Join-Path $OutputDirectory 'CJFingerService-win-x64-folder.zip'
if(Test-Path -LiteralPath $folderZip){Remove-Item -LiteralPath $folderZip}
$archive=[IO.Compression.ZipFile]::Open($folderZip,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($name in @($manifest.Files.Path)+@('package-manifest.json')) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $folder $name),$name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
(Get-FileHash -LiteralPath $folderZip -Algorithm SHA256).Hash | Set-Content -LiteralPath (Join-Path $OutputDirectory 'Folder-SHA256.txt')
$files=@('CJFingerService.exe','ocr.ps1','update.ps1','README.md') | ForEach-Object { Join-Path $legacy $_ }
$zip=Join-Path $OutputDirectory 'CJFingerService-win-x64.zip'
Compress-Archive -LiteralPath $files -DestinationPath $zip -Force
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256.txt')
Write-Output "Release asset: $zip"
$ubuntuZip=Join-Path $OutputDirectory 'CJFingerService-UbuntuOCR.zip'
Compress-Archive -LiteralPath (Join-Path $folder 'ubuntu') -DestinationPath $ubuntuZip -Force
(Get-FileHash -LiteralPath $ubuntuZip -Algorithm SHA256).Hash | Set-Content -LiteralPath (Join-Path $OutputDirectory 'UbuntuOCR-SHA256.txt')
