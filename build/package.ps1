<#
.SYNOPSIS
    CalendarWork 설치 프로그램(setup.exe)을 만든다.

.DESCRIPTION
    1. 테스트 실행
    2. dotnet publish (win-x64, .NET 런타임 포함, ReadyToRun)
    3. Inno Setup으로 설치 프로그램 컴파일 → artifacts\installer\CalendarWork-Setup-<버전>.exe

    버전·게시자는 src\CalendarWork.App\CalendarWork.App.csproj 의 <Version>·<Company>만 고치면 된다.
    Inno Setup이 없으면: winget install --id JRSoftware.InnoSetup -e --scope user

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\package.ps1
    powershell -ExecutionPolicy Bypass -File build\package.ps1 -SkipTests
#>
param(
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$appProject = Join-Path $root 'src\CalendarWork.App\CalendarWork.App.csproj'
$publishDir = Join-Path $root 'artifacts\publish\win-x64'
$issFile = Join-Path $root 'installer\CalendarWork.iss'

function Find-Iscc {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) {
        throw 'Inno Setup 6을 찾을 수 없습니다. 설치: winget install --id JRSoftware.InnoSetup -e --scope user'
    }
    return $found
}

$props = ([xml](Get-Content $appProject -Raw -Encoding UTF8)).Project.PropertyGroup
$version = $props.Version | Where-Object { $_ } | Select-Object -First 1
$publisher = $props.Company | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "$appProject 에서 <Version>을 찾을 수 없습니다." }
if (-not $publisher) { throw "$appProject 에서 <Company>를 찾을 수 없습니다." }
Write-Host "CalendarWork $version 패키징" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host '[1/3] 테스트' -ForegroundColor Cyan
    dotnet test (Join-Path $root 'CalendarWork.sln') -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw '테스트 실패' }
}

Write-Host '[2/3] 게시 (self-contained, win-x64)' -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $appProject -p:PublishProfile=win-x64 -nologo -v q
if ($LASTEXITCODE -ne 0) { throw '게시 실패' }

Write-Host '[3/3] 설치 프로그램 컴파일' -ForegroundColor Cyan
$iscc = Find-Iscc
& $iscc /Qp "/DAppVersion=$version" "/DAppPublisher=$publisher" "/DPublishDir=$publishDir" $issFile
if ($LASTEXITCODE -ne 0) { throw '설치 프로그램 컴파일 실패' }

$setup = Join-Path $root "artifacts\installer\CalendarWork-Setup-$version.exe"
$sizeMb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host "완료: $setup ($sizeMb MB)" -ForegroundColor Green
