# Capture the actual 1.1.6 updater window displayed on the Windows CI runner.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $root 'artifacts\ci-publish\AltTabFlow.exe'
$out = Join-Path $root 'artifacts\ui-preview\AltTabFlow-1.1.16-update-dialog.png'
if (!(Test-Path $exe)) { throw 'EXE missing' }
$process = $null
try {
    $process = Start-Process -FilePath $exe -ArgumentList '--update-preview' -PassThru
    Start-Sleep -Seconds 5
    if ($process.HasExited) { throw "UI preview exited: $($process.ExitCode)" }
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen
    if ($null -eq $screen) { throw 'No interactive desktop display' }
    $bounds = $screen.Bounds
    if ($bounds.Width -lt 640 -or $bounds.Height -lt 480) { throw 'Display too small for screenshot' }
    New-Item -ItemType Directory -Path (Split-Path $out -Parent) -Force | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap($bounds.Width,$bounds.Height)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try { $graphics.CopyFromScreen($bounds.Location,[System.Drawing.Point]::Empty,$bounds.Size) }
        finally { $graphics.Dispose() }
        $bitmap.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    if ((Get-Item $out).Length -lt 3000) { throw 'Suspiciously small capture' }
    Write-Host "Captured actual updater UI: $out"
} finally {
    if ($process -and !$process.HasExited) { $process.Kill();$process.WaitForExit() }
}
