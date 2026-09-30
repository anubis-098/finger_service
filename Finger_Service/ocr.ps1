param([Parameter(Mandatory=$true)][string]$ImagePath,[string]$Language='th')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] > $null
[Windows.Storage.Streams.IRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime] > $null
[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime] > $null
[Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics.Imaging,ContentType=WindowsRuntime] > $null
[Windows.Media.Ocr.OcrEngine,Windows.Foundation,ContentType=WindowsRuntime] > $null
[Windows.Media.Ocr.OcrResult,Windows.Foundation,ContentType=WindowsRuntime] > $null
[Windows.Globalization.Language,Windows.Globalization,ContentType=WindowsRuntime] > $null
$asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetGenericArguments().Count -eq 1 } | Select-Object -First 1
function Await($operation, $resultType) { $task=$asTask.MakeGenericMethod($resultType).Invoke($null,@($operation)); $task.Wait(); $task.Result }
$engine=[Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new($Language))
if (!$engine) { throw "OCR language '$Language' is not installed. Add the language's OCR capability in Windows Settings." }
$file=Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($ImagePath)) ([Windows.Storage.StorageFile])
$stream=Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
try {
  $decoder=Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
  $bitmap=Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
  try {
    $result=Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
    $lines=@($result.Lines | ForEach-Object {
      $left=($_.Words | ForEach-Object { $_.BoundingRect.X } | Measure-Object -Minimum).Minimum
      $top=($_.Words | ForEach-Object { $_.BoundingRect.Y } | Measure-Object -Minimum).Minimum
      $right=($_.Words | ForEach-Object { $_.BoundingRect.X+$_.BoundingRect.Width } | Measure-Object -Maximum).Maximum
      $bottom=($_.Words | ForEach-Object { $_.BoundingRect.Y+$_.BoundingRect.Height } | Measure-Object -Maximum).Maximum
      @{ text=$_.Text; x=($left+$right)/2; y=($top+$bottom)/2 }
    })
    [Console]::OutputEncoding=[Text.Encoding]::UTF8
    ConvertTo-Json -InputObject $lines -Compress
  } finally { $bitmap.Dispose() }
} finally { $stream.Dispose() }
