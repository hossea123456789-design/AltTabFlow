$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet publish 'AltTabFlow.csproj' -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=None -o dist --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
} finally {
    Pop-Location
}
