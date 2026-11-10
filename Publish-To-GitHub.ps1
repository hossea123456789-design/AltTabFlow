# One-time publish of the AltTabFlow repository to the authenticated user's own PRIVATE GitHub repo.
# No credential is read from files or embedded in the source.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$owner = 'hossea123456789-design'
$repo = "$owner/AltTabFlow"
$root = [IO.Path]::GetFullPath($PSScriptRoot)
function Run([string]$exe, [string[]]$argList) {
    & $exe @argList
    if ($LASTEXITCODE -ne 0) { throw "$exe failed (exit $LASTEXITCODE): $($argList -join ' ')" }
}
Push-Location $root
try {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git이 필요합니다. https://git-scm.com/download/win 에서 설치하세요.' }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI(gh)가 필요합니다. https://cli.github.com/ 에서 설치하세요.' }
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & gh auth status --hostname github.com 2>$null | Out-Null; $authenticated = ($LASTEXITCODE -eq 0) }
    finally { $ErrorActionPreference = $previousPreference }
    if (-not $authenticated) {
        Write-Host 'GitHub 브라우저 로그인 절차를 시작합니다.'
        Run 'gh' @('auth','login','--hostname','github.com','--web')
    }
    $login = (& gh api user --jq '.login' | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $login -ne $owner) {
        throw "로그인 계정이 $owner 과 일치하지 않습니다. 현재: $login"
    }
    if (-not (Test-Path '.git')) { Run 'git' @('init','-b','main') }
    # Never override an existing git author identity.
    $localName = (& git config user.name | Out-String).Trim()
    if (-not $localName) { Run 'git' @('config','--local','user.name',$owner) }
    $localEmail = (& git config user.email | Out-String).Trim()
    if (-not $localEmail) {
        $id = (& gh api user --jq '.id' | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or -not ($id -match '^\d+$')) { throw 'GitHub 계정 ID 조회 실패' }
        Run 'git' @('config','--local','user.email',"$id+$owner@users.noreply.github.com")
    }
    Run 'git' @('add','--all')
    & git diff --cached --quiet
    if ($LASTEXITCODE -eq 1) { Run 'git' @('commit','-m','Initial import: AltTabFlow v1.1.2 and maintenance workflows') }
    elseif ($LASTEXITCODE -ne 0) { throw 'git diff 상태를 확인하지 못했습니다.' }

    # Do not overwrite or rewrite any existing remote repository/history.
    $remoteNames = @(& git remote)
    $remote = if ($remoteNames -contains 'origin') { (& git remote get-url origin | Out-String).Trim() } else { '' }
    if ($remote) { throw "origin 원격 저장소가 이미 연결되어 있습니다: $remote. 기존 저장소를 자동으로 덮어쓰지 않습니다." }
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & gh repo view $repo --json name --jq '.name' 2>$null | Out-Null; $repoExists = ($LASTEXITCODE -eq 0) }
    finally { $ErrorActionPreference = $previousPreference }
    if ($repoExists) {
        throw "$repo 저장소가 이미 존재합니다. 기존 내용을 덮어쓰지 않도록 중단했습니다."
    }
    Write-Host "비공개 저장소 $repo 생성 및 main 업로드를 시작합니다." -ForegroundColor Cyan
    Run 'gh' @('repo','create',$repo,'--private','--description','Windows Alt+Tab replacement (WinForms)','--source','.','--remote','origin','--push')
    Write-Host "등록 완료: https://github.com/$repo" -ForegroundColor Green
    Write-Host '첫 Windows CI가 종료되면 Actions에서 컴파일 결과를 확인하세요. 실사용 창 테스트는 Windows PC에서 별도 실행해야 합니다.'
} finally {
    Pop-Location
}
