#requires -Version 7.0
<#
.SYNOPSIS
    Локальная публикация PoproshaykaBot — повторяет release.yml.

.DESCRIPTION
    Собирает framework-dependent и portable single-file сборки под win-x64/win-x86,
    переименовывает их в PoproshaykaBot-v<version>-<arch>[-portable].exe и упаковывает
    в одноимённые .zip. Артефакты складываются в <repo>/artifacts.

.PARAMETER Version
    Версия для подстановки в -p:Version. По умолчанию читается из Directory.Build.props.

.PARAMETER Arch
    Архитектуры для сборки. По умолчанию — обе (x64, x86).

.PARAMETER Variant
    Варианты сборки: framework-dependent, portable. По умолчанию — оба.

.PARAMETER UiHost
    Хост интерфейса: Wpf (релизный) или WinForms (заморожен 14.09.2026). По умолчанию Wpf – как в release.yml.

.PARAMETER OutputDir
    Куда сложить итоговые .exe и .zip. По умолчанию <repo>/artifacts.

.EXAMPLE
    pwsh ./scripts/publish.ps1
    pwsh ./scripts/publish.ps1 -Arch x64 -Variant portable
    pwsh ./scripts/publish.ps1 -Version 3.0.0.4
    pwsh ./scripts/publish.ps1 -UiHost WinForms -Arch x64
#>

[CmdletBinding()]
param(
    [string]$Version,
    [ValidateSet('x64', 'x86')]
    [string[]]$Arch = @('x64', 'x86'),
    [ValidateSet('framework-dependent', 'portable')]
    [string[]]$Variant = @('framework-dependent', 'portable'),
    [ValidateSet('WinForms', 'Wpf')]
    [string]$UiHost = 'Wpf',
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectDir = Join-Path $repoRoot "PoproshaykaBot.$UiHost"
$csprojPath = Join-Path $projectDir "PoproshaykaBot.$UiHost.csproj"
$propsPath = Join-Path $repoRoot 'Directory.Build.props'

if (-not $Version) {
    [xml]$props = Get-Content -LiteralPath $propsPath
    $versionNode = $props.SelectSingleNode('/Project/PropertyGroup/Version')
    if (-not $versionNode) {
        throw "Не удалось извлечь <Version> из $propsPath."
    }

    $Version = $versionNode.InnerText.Trim()
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'artifacts'
}

Write-Output "Хост: $UiHost"
Write-Output "Версия: $Version"
Write-Output "Архитектуры: $($Arch -join ', ')"
Write-Output "Варианты: $($Variant -join ', ')"
Write-Output "Выходная папка: $OutputDir"

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$publishRoot = Join-Path $projectDir 'publish'
if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

function Invoke-DotnetPublish {
    param(
        [string]$Arch,
        [string]$Variant
    )

    $variantDir = Join-Path $publishRoot $Variant
    $publishArgs = @(
        'publish',
        $csprojPath,
        '--configuration', 'Release',
        '--runtime', "win-$Arch",
        '--output', $variantDir,
        '-p:PublishSingleFile=true',
        '-p:PublishReadyToRun=true',
        '-p:UpdatableBuild=true',
        "-p:Version=$Version"
    )

    if ($Variant -eq 'portable') {
        $publishArgs += @(
            '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:PortableMode=true',
            '--self-contained'
        )
    }
    else {
        $publishArgs += '--no-self-contained'
    }

    Write-Output ""
    Write-Output "==> dotnet $($publishArgs -join ' ')"
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish завершился с кодом $LASTEXITCODE (arch=$Arch, variant=$Variant)."
    }

    $sourceExe = Join-Path $variantDir "PoproshaykaBot.$UiHost.exe"
    if (-not (Test-Path -LiteralPath $sourceExe)) {
        throw "Ожидаемый файл не найден: $sourceExe"
    }

    $suffix = if ($Variant -eq 'portable') { "-$Arch-portable" } else { "-$Arch" }
    $exeName = "PoproshaykaBot-v$Version$suffix.exe"
    $zipName = "PoproshaykaBot-v$Version$suffix.zip"
    $exePath = Join-Path $OutputDir $exeName
    $zipPath = Join-Path $OutputDir $zipName

    if (Test-Path -LiteralPath $exePath) { Remove-Item -LiteralPath $exePath -Force }
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

    Move-Item -LiteralPath $sourceExe -Destination $exePath
    Compress-Archive -Path $exePath -DestinationPath $zipPath

    Write-Output "  -> $exeName"
    Write-Output "  -> $zipName"
}

function Write-ReleaseMetadata {
    param(
        [string]$Arch
    )

    $exes = @(Get-ChildItem -LiteralPath $OutputDir -Filter "PoproshaykaBot-v$Version-$Arch*.exe")
    if ($exes.Count -eq 0) {
        return
    }

    $sumsPath = Join-Path $OutputDir "SHA256SUMS-$Arch.txt"
    $lines = foreach ($exe in $exes) {
        $hash = (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash.ToLower()
        "$hash  $($exe.Name)"
    }
    Set-Content -LiteralPath $sumsPath -Value $lines -Encoding ascii

    $tfm = (Select-String -LiteralPath $csprojPath -Pattern '<TargetFramework>(.*?)</TargetFramework>').Matches[0].Groups[1].Value
    $runtimePath = Join-Path $OutputDir "RUNTIME-$Arch.txt"
    Set-Content -LiteralPath $runtimePath -Value $tfm -Encoding ascii -NoNewline

    Write-Output "  -> SHA256SUMS-$Arch.txt"
    Write-Output "  -> RUNTIME-$Arch.txt"
}

foreach ($a in $Arch) {
    foreach ($v in $Variant) {
        Invoke-DotnetPublish -Arch $a -Variant $v
    }

    Write-ReleaseMetadata -Arch $a
}

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

Write-Output ""
Write-Output "Готово. Артефакты в $OutputDir"
