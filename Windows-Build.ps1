# AltTabFlow Windows 10/11 x64 build helper. Does not modify the application source.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath($PSScriptRoot)
Push-Location $root
try {
    if (-not [Environment]::OSVersion.Platform.Equals([PlatformID]::Win32NT)) {
        throw 'Windows에서만 실행할 수 있습니다.'
    }
    Write-Host 'AltTabFlow 1.1.4 소스 복원본: Windows 배포 빌드' -ForegroundColor Cyan
    Write-Host '원본 동작/소스는 변경하지 않고 package.ps1을 실행합니다.'
    $dotnet = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
    $hasSdk8 = $false
    if ($null -ne $dotnet) {
        $sdkList = @(& $dotnet.Source --list-sdks 2>$null)
        $hasSdk8 = (@($sdkList | Where-Object { $_ -match '^8\.' }).Count -gt 0)
    }
    if (-not $hasSdk8) {
        Write-Host '.NET SDK 8이 발견되지 않았습니다.' -ForegroundColor Yellow
        $choice = Read-Host '공식 Microsoft 설치 스크립트로 이 계정 전용 SDK를 설치할까요? (Y/N)'
        if ($choice.ToUpperInvariant() -ne 'Y') {
            throw '.NET SDK 8이 없어 빌드를 중단했습니다. 소스는 변경되지 않았습니다.'
        }
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $installDir = Join-Path $env:LOCALAPPDATA 'AltTabFlow-build-dotnet'
        $scriptPath = Join-Path $env:TEMP 'AltTabFlow-dotnet-install.ps1'
        Write-Host 'Microsoft 공식 배포 주소에서 .NET 8 SDK 설치 파일을 받습니다.'
        Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $scriptPath
        & $scriptPath -Channel '8.0' -InstallDir $installDir -NoPath
        if ($LASTEXITCODE -ne 0) {throw '.NET 8 SDK 설치가 실패했습니다.'}
        $env:PATH = "$installDir;$env:PATH"
        $dotnet = Get-Command 'dotnet.exe' -ErrorAction Stop
    }
    Write-Host ('사용 SDK: ' + (& $dotnet.Source --version))
    Write-Host '프로그램 빌드 및 자동검증을 실행합니다. 테스트용 Windows 창이 잠깐 표시될 수 있습니다.'
    & (Join-Path $root 'package.ps1') -RuntimeVersion '8.0.32'
    if (-not $?) {throw 'package.ps1 실행이 실패했습니다.'}
    $latest=Join-Path $root 'releases\AltTabFlow-latest-win-x64.zip'
    if (-not (Test-Path -LiteralPath $latest)) {throw '검증된 배포 ZIP이 생성되지 않았습니다.'}
    Write-Host ''
    Write-Host '빌드 및 자동 검증 완료' -ForegroundColor Green
    Write-Host "실행용 배포 ZIP: $latest" -ForegroundColor Green
    Write-Host 'ZIP 압축을 모두 푼 뒤 AltTabFlow\AltTabFlow.exe를 실행하세요.'
    explorer.exe (Join-Path $root 'releases')
} catch {
    Write-Host ('오류: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}
