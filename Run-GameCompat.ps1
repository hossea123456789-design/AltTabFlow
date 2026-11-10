param([switch]$Check)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$exe = Join-Path $PSScriptRoot 'AltTabFlow.exe'
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw 'Keep Run-GameCompat.ps1 next to AltTabFlow.exe in the extracted release folder.'
}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class AltTabFlowTokenQuery {
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int id);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool GetTokenInformation(IntPtr token, int kind, out int value, int size, out int length);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static bool Elevated(int id) {
        IntPtr process = OpenProcess(0x1000, false, id);
        if (process == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try {
            IntPtr token;
            if (!OpenProcessToken(process, 8, out token)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try {
                int value, length;
                if (!GetTokenInformation(token, 20, out value, 4, out length)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                return value != 0;
            } finally { CloseHandle(token); }
        } finally { CloseHandle(process); }
    }
}
'@
$sessionId = (Get-Process -Id $PID).SessionId
$running = @(Get-Process -Name AltTabFlow -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $sessionId })
if ($running.Count -gt 1) { throw 'Multiple AltTabFlow processes found. Finish any developer tests, then retry.' }
$alreadyElevated = $running.Count -eq 1 -and [AltTabFlowTokenQuery]::Elevated($running[0].Id)
if ($Check) {
    [pscustomobject]@{ executable=$exe; currentPids=@($running | ForEach-Object { $_.Id }); alreadyElevated=$alreadyElevated; action=if($alreadyElevated){'Already running as administrator'}else{'Graceful quit, then Windows UAC approval; restore normal mode if cancelled'} } | ConvertTo-Json
    exit 0
}
if ($alreadyElevated) { Write-Output 'AltTabFlow is already running as administrator.'; exit 0 }
$restoreExe = $null
if ($running.Count -eq 1) {
    $restoreExe = $running[0].Path
    if (!$restoreExe -or !(Test-Path -LiteralPath $restoreExe -PathType Leaf)) { throw 'Cannot determine the current executable. No process was stopped.' }
    # The application's own quit command restores its temporary window effects.
    # Never force-kill the app and leave another application's topmost state changed.
    $quit = Start-Process -FilePath $restoreExe -ArgumentList '--quit' -WindowStyle Hidden -PassThru
    if (!$quit.WaitForExit(5000)) { throw 'The quit command did not finish. No administrator copy was started.' }
    if (!$running[0].WaitForExit(5000)) { throw 'AltTabFlow did not exit cleanly. No administrator copy was started.' }
}
try {
    $started = Start-Process -FilePath $exe -ArgumentList '--background' -Verb RunAs -PassThru
    if ($null -eq $started) { throw 'Windows did not return the new process.' }
    # Acknowledging UAC is not the same as successfully starting the application.
    if ($started.WaitForExit(3000)) { throw "The administrator process exited during startup (code $($started.ExitCode))." }
    if (![AltTabFlowTokenQuery]::Elevated($started.Id)) { throw 'The new process is not elevated.' }
    Write-Output "AltTabFlow is running as administrator (PID $($started.Id))."
} catch {
    $reason = $_.Exception.Message
    # Recover only after the original instance was actually closed, and only if
    # no replacement survived. A UAC cancellation must not leave the user unserved.
    $survivors = @(Get-Process -Name AltTabFlow -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $sessionId })
    if ($restoreExe -and $survivors.Count -eq 0) {
        Start-Process -FilePath $restoreExe -ArgumentList '--background' -WindowStyle Hidden | Out-Null
        Write-Warning 'Administrator launch was cancelled or failed. Normal mode was restored.'
    }
    throw $reason
}
