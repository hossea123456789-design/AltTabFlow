# Verify the tracked source/resource manifest without modifying files.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifest = Join-Path $root 'SOURCE-SHA256.txt'
if (-not (Test-Path -LiteralPath $manifest)) { throw 'Missing source SHA-256 manifest' }
$ok = 0
foreach ($line in (Get-Content -LiteralPath $manifest -Encoding UTF8)) {
    if ($line -notmatch '^([A-Fa-f0-9]{64})  (.+)$') { continue }
    $expected = $matches[1].ToLowerInvariant()
    $name = $matches[2]
    $path = [IO.Path]::GetFullPath((Join-Path $root $name))
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw "Manifest path escapes repository: $name" }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing manifest entry: $name" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw "SHA-256 mismatch: $name (expected $expected, actual $actual)" }
    $ok++
}
if ($ok -lt 20) { throw "Unexpectedly few manifest entries: $ok" }
Write-Host "SHA-256 manifest: $ok files verified" -ForegroundColor Green
