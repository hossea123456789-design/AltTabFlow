param([string]$RuntimeVersion='8.0.32',[string]$PublishedExe='',[string]$PublishedSha256='')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$projectRoot=[IO.Path]::GetFullPath($PSScriptRoot)
$version=([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'AltTabFlow.csproj') -Raw -Encoding UTF8)).Project.PropertyGroup.Version
$kst=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'Korea Standard Time')
$buildId=$kst.ToString('yyyyMMdd-HHmmss')
$releaseRoot=Join-Path $projectRoot 'releases'
$stageRoot=Join-Path $projectRoot "artifacts\packages\$buildId"
$stage=Join-Path $stageRoot 'AltTabFlow'
$verifyRoot=Join-Path $projectRoot "artifacts\verify\$buildId"
if(Test-Path -LiteralPath $stageRoot){throw "Build folder already exists: $stageRoot"}
New-Item -ItemType Directory -Path $stage,$releaseRoot,$verifyRoot -Force | Out-Null
Push-Location $projectRoot
try {
    if($PublishedExe){
        if(!$PublishedSha256 -or (Get-FileHash -LiteralPath $PublishedExe -Algorithm SHA256).Hash -ne $PublishedSha256){throw 'Explicit published executable hash required'}
        if([Diagnostics.FileVersionInfo]::GetVersionInfo($PublishedExe).ProductVersion -ne $version){throw 'Published executable version mismatch'}
        Copy-Item -LiteralPath $PublishedExe -Destination (Join-Path $stage 'AltTabFlow.exe')
    } else {
        dotnet publish 'AltTabFlow.csproj' -c Release -r win-x64 --self-contained true "-p:RuntimeFrameworkVersion=$RuntimeVersion" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $stage --nologo
        if($LASTEXITCODE -ne 0){throw 'Self-contained publish failed'}
    }
    $exe=Join-Path $stage 'AltTabFlow.exe'
    if(!(Test-Path -LiteralPath $exe)){throw 'Executable missing'}
    $test=Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    if($test.ExitCode -ne 0){throw "Self-test failed: $($test.ExitCode)"}
    $testPath=Join-Path $env:LOCALAPPDATA 'AltTabFlow\self-test.json'
    $report=Get-Content -LiteralPath $testPath -Raw | ConvertFrom-Json
    if($report.failures -ne 0){throw 'Self-test failures found'}
    $nativeTest=Start-Process -FilePath $exe -ArgumentList '--peek-self-test' -WindowStyle Hidden -Wait -PassThru
    if($nativeTest.ExitCode -ne 0){throw 'Real-window preview regression failed'}
    $nativeReport=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\peek-test.json') -Raw | ConvertFrom-Json
    if($nativeReport.failures -ne 0){throw 'Real-window preview failures found'}
    $menuTest=Start-Process -FilePath $exe -ArgumentList '--menu-self-test' -WindowStyle Hidden -Wait -PassThru
    if($menuTest.ExitCode -ne 0){throw 'Context menu lifetime regression failed'}
    $menuReport=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\menu-test.json') -Raw | ConvertFrom-Json
    if($menuReport.failures -ne 0){throw 'Context menu lifetime failures found'}
    $focusTest=Start-Process -FilePath $exe -ArgumentList '--focus-self-test' -WindowStyle Hidden -Wait -PassThru
    if($focusTest.ExitCode -ne 0){throw 'Foreground permission and delayed target regression failed'}
    $focusReport=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\focus-test.json') -Raw | ConvertFrom-Json
    if($focusReport.failures -ne 0){throw 'Foreground regression failures found'}
    $uiTest=Start-Process -FilePath $exe -ArgumentList '--ui-self-test' -WindowStyle Hidden -Wait -PassThru
    if($uiTest.ExitCode -ne 0){throw 'Settings and UI interaction regression failed'}
    $uiReport=Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\ui-test.json') -Raw | ConvertFrom-Json
    if($uiReport.failures -ne 0){throw 'UI regression failures found'}
    Copy-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\ui-test.json') -Destination (Join-Path $projectRoot 'verification-ui-after.json')
    Copy-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\focus-test.json') -Destination (Join-Path $projectRoot 'verification-focus-after.json')
    Copy-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\menu-test.json') -Destination (Join-Path $projectRoot 'verification-menu-after.json')
    Copy-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'AltTabFlow\peek-test.json') -Destination (Join-Path $projectRoot 'verification-peek-after.json')
    Copy-Item -LiteralPath $testPath -Destination (Join-Path $projectRoot 'verification-tests.json')
    Copy-Item -LiteralPath (Join-Path $projectRoot '사용법.txt') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Run-GameCompat.ps1') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Game-compat-mode.cmd') -Destination $stage
    $licenseDir=Join-Path $stage 'licenses'
    New-Item -ItemType Directory -Path $licenseDir | Out-Null
    $nugetRoot=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget\packages'}
    Copy-Item -LiteralPath (Join-Path $nugetRoot "microsoft.netcore.app.runtime.win-x64\$RuntimeVersion\LICENSE.TXT") -Destination (Join-Path $licenseDir 'dotnet-runtime-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $nugetRoot "microsoft.netcore.app.runtime.win-x64\$RuntimeVersion\THIRD-PARTY-NOTICES.TXT") -Destination (Join-Path $licenseDir 'dotnet-THIRD-PARTY-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $nugetRoot "microsoft.windowsdesktop.app.runtime.win-x64\$RuntimeVersion\LICENSE") -Destination (Join-Path $licenseDir 'windowsdesktop-LICENSE.txt')
    $info=[ordered]@{product='AltTabFlow';version=$version;build=$buildId;builtAtKst=$kst.ToString('yyyy-MM-ddTHH:mm:ss')+'+09:00';platform='win-x64';selfContained=$true;runtimeVersion=$RuntimeVersion;exeSha256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;testsPassed=@($report.tests | Where-Object { $_.PSObject.Properties.Name -contains 'passed' -and $_.passed }).Count}
    $info.nativeTestsPassed=@($nativeReport.tests | Where-Object { $_.PSObject.Properties.Name -contains 'passed' -and $_.passed }).Count
    $info.menuTestsPassed=@($menuReport.tests | Where-Object { $_.PSObject.Properties.Name -contains 'passed' -and $_.passed }).Count
    $info.focusTestsPassed=@($focusReport.tests | Where-Object { $_.PSObject.Properties.Name -contains 'passed' -and $_.passed }).Count
    $info.uiTestsPassed=@($uiReport.tests | Where-Object { $_.PSObject.Properties.Name -contains 'passed' -and $_.passed }).Count
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'release-info.json') -Encoding UTF8
    $zipName="AltTabFlow-v$version-win-x64-$buildId.zip"
    $zip=Join-Path $releaseRoot $zipName
    Compress-Archive -LiteralPath $stage -DestinationPath $zip -CompressionLevel Optimal
    Expand-Archive -LiteralPath $zip -DestinationPath $verifyRoot
    $extracted=Join-Path $verifyRoot 'AltTabFlow\AltTabFlow.exe'
    if((Get-FileHash -LiteralPath $extracted -Algorithm SHA256).Hash -ne $info.exeSha256){throw 'ZIP executable hash mismatch'}
    $zipTest=Start-Process -FilePath $extracted -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    if($zipTest.ExitCode -ne 0){throw 'Extracted executable self-test failed'}
    Copy-Item -LiteralPath $zip -Destination (Join-Path $releaseRoot 'AltTabFlow-latest-win-x64.zip') -Force
    $manifest=[ordered]@{version=$version;build=$buildId;archive=$zipName;latest='AltTabFlow-latest-win-x64.zip';sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash;bytes=(Get-Item -LiteralPath $zip).Length;exeSha256=$info.exeSha256;selfContained=$true;runtimeVersion=$RuntimeVersion;testsPassed=$info.testsPassed;extractedExe=$extracted}
    $manifest.nativeTestsPassed=$info.nativeTestsPassed
    $manifest.menuTestsPassed=$info.menuTestsPassed
    $manifest.focusTestsPassed=$info.focusTestsPassed
    $manifest.uiTestsPassed=$info.uiTestsPassed
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot 'latest.json') -Encoding UTF8
    "$($manifest.sha256)  $zipName" | Set-Content -LiteralPath ($zip+'.sha256') -Encoding ASCII
    $manifest | ConvertTo-Json
} finally {Pop-Location}

