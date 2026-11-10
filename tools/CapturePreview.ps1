# Capture a REAL displayed Windows UI. Does not generate or mock an image.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fixturePath=Join-Path $root 'tools\TestWindows\bin\Release\net8.0-windows\AltTabFlowTest.exe'
$exe=Join-Path $root 'artifacts\ci-publish\AltTabFlow.exe'
$output=Join-Path $root 'artifacts\ui-preview\AltTabFlow-1.1.9-actual.png'
if(-not (Test-Path $fixturePath)){throw "Missing fixture: $fixturePath"}
if(-not (Test-Path $exe)){throw "Missing executable: $exe"}
$folder=Join-Path $env:LOCALAPPDATA 'AltTabFlow'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$fixture=$null;$app=$null;$bitmap=$null;$graphics=$null
try {
  $fixture=Start-Process -FilePath $fixturePath -PassThru
  Start-Sleep -Seconds 3
  $app=Start-Process -FilePath $exe -ArgumentList '--preview' -PassThru
  Start-Sleep -Seconds 6
  if($app.HasExited){throw "AltTabFlow preview exited (code: $($app.ExitCode))"}
  $screen=[System.Windows.Forms.Screen]::PrimaryScreen
  if($null -eq $screen){throw 'No primary desktop screen'}
  $bounds=$screen.Bounds
  if($bounds.Width -lt 640 -or $bounds.Height -lt 480){throw "Invalid display: $bounds"}
  $bitmap=New-Object System.Drawing.Bitmap($bounds.Width,$bounds.Height)
  $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.CopyFromScreen($bounds.Location,[System.Drawing.Point]::Empty,$bounds.Size)
  $graphics.Dispose();$graphics=$null
  New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force | Out-Null
  $bitmap.Save($output,[System.Drawing.Imaging.ImageFormat]::Png)
  $bitmap.Dispose();$bitmap=$null
  if((Get-Item $output).Length -lt 3000){throw 'Suspiciously small capture'}
  Write-Host "Captured real Windows screen: $output"
} finally {
  if($graphics){$graphics.Dispose()}
  if($bitmap){$bitmap.Dispose()}
  if($app -and -not $app.HasExited){
    Start-Process -FilePath $exe -ArgumentList '--quit' -Wait -WindowStyle Hidden
    Start-Sleep -Milliseconds 500
    if(-not $app.HasExited){$app.Kill()}
  }
  if($fixture -and -not $fixture.HasExited){$fixture.Kill()}
}
