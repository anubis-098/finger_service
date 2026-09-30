$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot ('test-data/update-install-'+[guid]::NewGuid().ToString('N'))
$source=Join-Path $root 'stage'
$target=Join-Path $root 'app'
New-Item -ItemType Directory -Path $source,$target -Force | Out-Null
$names=@('CJFingerService.exe','ocr.ps1','update.ps1','README.md')
foreach($name in $names) { Set-Content -LiteralPath (Join-Path $source $name) -Value 'new'; Set-Content -LiteralPath (Join-Path $target $name) -Value 'old' }
Set-Content -LiteralPath (Join-Path $target 'settings.json') -Value 'keep settings'
$finished=Start-Process -FilePath $env:ComSpec -ArgumentList '/c exit 0' -WindowStyle Hidden -PassThru -Wait
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'update.ps1') -Source $source -Target $target -ParentId $finished.Id -NoRestart
if($LASTEXITCODE -ne 0) { throw 'Install failed' }
foreach($name in $names) {
  if((Get-Content -LiteralPath (Join-Path $target $name)) -ne 'new') {throw 'Replacement failed'}
  if((Get-Content -LiteralPath (Join-Path (Join-Path $root 'backup') $name)) -ne 'old') {throw 'Backup failed'}
}
if((Get-Content -LiteralPath (Join-Path $target 'settings.json')) -ne 'keep settings') {throw 'Settings changed'}
Write-Output 'PASS: installer replaces only package files, retains backup and preserves settings.'
