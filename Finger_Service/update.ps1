param([Parameter(Mandatory)][string]$Source,[Parameter(Mandatory)][string]$Target,[Parameter(Mandatory)][int]$ParentId,[switch]$NoRestart)
$ErrorActionPreference='Stop'
$sourcePath=(Resolve-Path -LiteralPath $Source).Path
$targetPath=(Resolve-Path -LiteralPath $Target).Path
$files=@('CJFingerService.exe','ocr.ps1','update.ps1','README.md')
$backup=Join-Path (Split-Path $sourcePath -Parent) 'backup'
$log=Join-Path (Split-Path $sourcePath -Parent) 'install.log'
$changed=@()
try {
    $parent=Get-Process -Id $ParentId -ErrorAction SilentlyContinue
    if($parent -and !$parent.WaitForExit(30000)) { throw 'Current application did not exit. Update cancelled.' }
    foreach($name in $files) { if(!(Test-Path -LiteralPath (Join-Path $sourcePath $name) -PathType Leaf)) { throw "Missing file: $name" } }
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    foreach($name in $files) {
        $destination=Join-Path $targetPath $name
        if(Test-Path -LiteralPath $destination) { Copy-Item -LiteralPath $destination -Destination (Join-Path $backup $name) }
        $changed+=$name
        Copy-Item -LiteralPath (Join-Path $sourcePath $name) -Destination $destination -Force
    }
    if(!$NoRestart) { Start-Process -FilePath (Join-Path $targetPath 'CJFingerService.exe') -WorkingDirectory $targetPath -WindowStyle Hidden }
    'Update installed. Backup retained.' | Set-Content -LiteralPath $log
} catch {
    $failure=$_.Exception.Message
    $restoreErrors=@()
    foreach($name in $changed) {
        $original=Join-Path $backup $name
        if(Test-Path -LiteralPath $original) { try { Copy-Item -LiteralPath $original -Destination (Join-Path $targetPath $name) -Force } catch { $restoreErrors+=$name } }
    }
    "Update failed: $failure. Restore failures: $($restoreErrors -join ','). Backup: $backup" | Set-Content -LiteralPath $log
    if(!$NoRestart -and $changed.Count -gt 0 -and $restoreErrors.Count -eq 0) { Start-Process -FilePath (Join-Path $targetPath 'CJFingerService.exe') -WorkingDirectory $targetPath -WindowStyle Hidden }
    exit 1
}
