#Requires -Version 7.0
<#
.SYNOPSIS
Снимает окно PoproshaykaBot.Wpf в PNG режимом --gallery.

.DESCRIPTION
Поднимает приложение headless (без сети, как --ui-smoke), проходит указанные страницы, диалоги и темы
и складывает кадры в каталог вывода вместе с index.json. Кейсы users:selected и streams:selected –
та же страница с выбранной строкой: выбор ставится на время кадра и снимается после него, поэтому
users и users:selected снимаются в одном прогоне. Кейс streams:cards – та же страница видом карточек
с выбранной сессией: вид и выбор возвращаются к прежним после кадра. Кейс streams:hidden – та же
страница с включённым показом убранных из статистики стримов; переключатель гасится после кадра.
Кейсы commands:selected и commands:params – страница «Команды» с выбранной командой: первый берёт
команду без своих параметров, второй – донат, у которого инспектор показывает раздел параметров.
Настройки и данные человека не трогаются:
прогон идёт в собственной базовой директории (POPROSHAYKA_BASE_DIR), если она не задана снаружи.
С -Profile скрипт копирует профиль во временную директорию и снимает кадры с данными этой копии
(settings\**, *.json и *.txt из корня; без logs\, WebView2\, gallery\, *.bak, *.legacy-* и exe);
копия удаляется после прогона, -KeepState её оставляет и печатает путь. Сама съёмка идёт headless и
в сеть не ходит, но копия – полный профиль с токенами, поэтому по умолчанию она не переживает прогон.
-Section принимает и ключ раздела, и ключ вида раздел/блок (подпункт рейла): значение ложится в
ui-preferences как есть. На кадр доезжает только раздел – прогон галереи отматывает все ScrollViewer к
началу перед съёмкой, поэтому подсвечен будет первый подпункт раздела, а не выбранный.
Перед съёмкой хост собирается тем же -Configuration и -Slot, по которым ищется exe; упавшая сборка
отменяет съёмку. -NoBuild сборку пропускает, -Exe отменяет её сам – указан готовый бинарник.
Печатает пути снятых кадров.

.EXAMPLE
tools/capture-view.ps1 -Page overview

.EXAMPLE
tools/capture-view.ps1 -Page settings -Theme dark -Scale 2 -Size 1440x900

.EXAMPLE
tools/capture-view.ps1 -Profile 'C:\Downloads\PoproshaykaBot-profile' -Page streams -KeepState

.EXAMPLE
tools/capture-view.ps1 -Profile 'C:\Downloads\PoproshaykaBot-profile' -Page users,users:selected

.EXAMPLE
tools/capture-view.ps1 -Profile 'C:\Downloads\PoproshaykaBot-profile' -Page settings -Section obs

.EXAMPLE
tools/capture-view.ps1 -Profile 'C:\Downloads\PoproshaykaBot-profile' -Page settings -Section basic/messages

.EXAMPLE
tools/capture-view.ps1 -Profile 'C:\Downloads\PoproshaykaBot-profile' -Dialog broadcast-profile,chat-blockers

.EXAMPLE
tools/capture-view.ps1 -Page overview -NoBuild

.EXAMPLE
tools/capture-view.ps1 -Exe C:\publish\Wpf\PoproshaykaBot.Wpf.exe -Page overview
#>
param(
    [ValidateSet('overview', 'overview:obs', 'overview:obs-full', 'overview:edit','users', 'users:selected', 'streams', 'streams:selected', 'streams:cards', 'streams:hidden', 'commands', 'commands:selected', 'commands:params', 'logs', 'diagnostics', 'settings')]
    [string[]]$Page,

    [ValidateSet('broadcast-profile', 'poll-profile', 'poll-from-profile', 'point-term', 'chat-blockers', 'color-picker')]
    [string[]]$Dialog,

    [ValidateSet('light', 'dark')]
    [string[]]$Theme,

    [string]$Element,

    [ValidateRange(0.5, 3)]
    [double]$Scale = 1,

    [ValidateRange(0.8, 1.6)]
    [double]$FontScale = 1,

    [ValidatePattern('^\d+x\d+$')]
    [string]$Size,

    [string]$Output,

    [string]$Profile,

    [switch]$KeepState,

    [ValidatePattern('^(basic|oauth|obs|stream|dashboard|update|debug|mcp|appearance|misc)(/[a-z]+)?$')]
    [string]$Section,

    [string]$Exe,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$Slot = $env:POPROSHAYKA_BUILD_SLOT,

    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

function Copy-GalleryProfile {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    $skipped = {
        param($name)

        $name -like '*.bak' -or
        $name -like '*.legacy-*' -or
        $name -like '*.invalid-*' -or
        $name -like '*.pre-migration-*' -or
        $name -like '*.tmp' -or
        $name -like '*.old' -or
        $name -like '*.exe'
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    Get-ChildItem -LiteralPath $Source -File |
        Where-Object { $_.Extension -in '.json', '.txt' -and -not (& $skipped $_.Name) } |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) }

    foreach ($tree in @('settings', 'cache\box-art')) {
        $treeSource = Join-Path $Source $tree

        if (-not (Test-Path -LiteralPath $treeSource)) { continue }

        $treeDestination = Join-Path $Destination $tree
        New-Item -ItemType Directory -Force -Path $treeDestination | Out-Null

        Get-ChildItem -LiteralPath $treeSource -File -Recurse |
            Where-Object { -not (& $skipped $_.Name) } |
            ForEach-Object {
                $relative = $_.FullName.Substring($treeSource.Length).TrimStart([IO.Path]::DirectorySeparatorChar)
                $target = Join-Path $treeDestination $relative
                New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
                Copy-Item -LiteralPath $_.FullName -Destination $target
            }
    }
}

function Set-UiSettingsSection {
    param(
        [Parameter(Mandatory)][string]$File,
        [Parameter(Mandatory)][string]$Key
    )

    $header = '[ui.settings]'
    $entry = "section = `"$Key`""
    $lines = [Collections.Generic.List[string]]::new()

    if (Test-Path -LiteralPath $File) {
        $lines.AddRange([string[]]@(Get-Content -LiteralPath $File))
    }

    $headerIndex = -1

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq $header) { $headerIndex = $i; break }
    }

    if ($headerIndex -lt 0) {
        if ($lines.Count -eq 0) { $lines.Add('[ui]') }
        $lines.Add($header)
        $lines.Add($entry)
    }
    else {
        $end = $lines.Count

        for ($i = $headerIndex + 1; $i -lt $lines.Count; $i++) {
            if ($lines[$i].Trim().StartsWith('[')) { $end = $i; break }
        }

        $replaced = $false

        for ($i = $headerIndex + 1; $i -lt $end; $i++) {
            if ($lines[$i] -match '^\s*section\s*=') { $lines[$i] = $entry; $replaced = $true; break }
        }

        if (-not $replaced) { $lines.Insert($end, $entry) }
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $File -Parent) | Out-Null
    Set-Content -LiteralPath $File -Value $lines -Encoding utf8
}

function Invoke-HostBuild {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Configuration,
        [AllowEmptyString()][AllowNull()][string]$Slot
    )

    $PSNativeCommandUseErrorActionPreference = $false
    $previousSlot = $env:POPROSHAYKA_BUILD_SLOT
    if ($Slot) { $env:POPROSHAYKA_BUILD_SLOT = $Slot }

    try {
        $log = & dotnet build $Project -c $Configuration --nologo -v quiet 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        if ($Slot) {
            if ($null -eq $previousSlot) { Remove-Item Env:\POPROSHAYKA_BUILD_SLOT -ErrorAction SilentlyContinue }
            else { $env:POPROSHAYKA_BUILD_SLOT = $previousSlot }
        }
    }

    if ($code -ne 0) {
        $errors = @($log | ForEach-Object { $_.ToString() } | Where-Object { $_ -match ':\s*(error|ошибка)\s' } | Select-Object -First 20)
        if (-not $errors) { $errors = @($log | ForEach-Object { $_.ToString() } | Select-Object -Last 20) }
        $errors | ForEach-Object { Write-Host $_ }

        throw "Сборка вернула код $code – съёмка не запускалась."
    }

    $slotSuffix = if ($Slot) { ", слот $Slot" } else { '' }
    Write-Host "Сборка готова: $Configuration$slotSuffix."
}

$root = Split-Path $PSScriptRoot -Parent

if ($Exe) {
    if (-not (Test-Path -LiteralPath $Exe)) {
        throw "Указанного exe нет: $Exe"
    }

    $exePath = (Resolve-Path -LiteralPath $Exe).Path
}
else {
    $hostProject = Join-Path $root 'PoproshaykaBot.Wpf\PoproshaykaBot.Wpf.csproj'
    $tfm = (Select-String -LiteralPath $hostProject -Pattern '<TargetFramework>(.*?)</TargetFramework>').Matches[0].Groups[1].Value
    $binRelative = if ($Slot) { "PoproshaykaBot.Wpf\bin\$Slot\$Configuration" } else { "PoproshaykaBot.Wpf\bin\$Configuration" }
    $exePath = Join-Path $root "$binRelative\$tfm\PoproshaykaBot.Wpf.exe"

    if (-not $NoBuild) {
        Invoke-HostBuild -Project $hostProject -Configuration $Configuration -Slot $Slot
    }

    if (-not (Test-Path -LiteralPath $exePath)) {
        $buildCommand = "dotnet build PoproshaykaBot.Wpf/PoproshaykaBot.Wpf.csproj -c $Configuration"

        if ($Slot) {
            $buildCommand = '$env:POPROSHAYKA_BUILD_SLOT=' + "'$Slot'; " + $buildCommand
        }

        throw "Сборки нет: $exePath – запусти без -NoBuild или собери сам: ``$buildCommand``."
    }
}

$externalBaseDirectory = [bool]$env:POPROSHAYKA_BASE_DIR

if ($Profile -and $externalBaseDirectory) {
    throw 'Одновременно заданы -Profile и переменная POPROSHAYKA_BASE_DIR – две правды о том, где профиль. Убери переменную или запусти без -Profile.'
}

if ($Profile -and -not (Test-Path -LiteralPath $Profile -PathType Container)) {
    throw "Профиля нет: $Profile"
}

if ($externalBaseDirectory) {
    Write-Warning "POPROSHAYKA_BASE_DIR задана снаружи – прогон читает и пишет в $env:POPROSHAYKA_BASE_DIR. Чтобы снять кадры с копии профиля, убери переменную и передай -Profile."
}

if ($Section -and $externalBaseDirectory) {
    throw 'Ключ -Section правит ui-preferences.toml базовой директории прогона, а она задана снаружи. Запусти с -Profile или без POPROSHAYKA_BASE_DIR.'
}

if (-not $Output) { $Output = Join-Path $root 'artifacts\gallery' }

$pages = @()
if ($Page) { $pages += $Page }
if ($Dialog) { $pages += ($Dialog | ForEach-Object { "dialog:$_" }) }

$arguments = @('--gallery', $Output)
if ($pages) { $arguments += @('--pages', ($pages -join ',')) }
if ($Theme) { $arguments += @('--themes', ($Theme -join ',')) }
if ($Element) { $arguments += @('--element', $Element) }
if ($Size) { $arguments += @('--size', $Size) }
if ($Scale -ne 1) { $arguments += @('--scale', $Scale.ToString([cultureinfo]::InvariantCulture)) }
if ($FontScale -ne 1) { $arguments += @('--font-scale', $FontScale.ToString([cultureinfo]::InvariantCulture)) }

$ownBaseDirectory = -not $externalBaseDirectory
$runState = $null

if ($ownBaseDirectory) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $tail = [IO.Path]::GetRandomFileName().Substring(0, 8)
    $runState = Join-Path ([IO.Path]::GetTempPath()) "poproshayka-gallery-$stamp-$tail"
    $env:POPROSHAYKA_BASE_DIR = $runState
}

try {
    if ($Profile) {
        Copy-GalleryProfile -Source (Resolve-Path -LiteralPath $Profile).Path -Destination $runState
    }
    elseif ($ownBaseDirectory) {
        New-Item -ItemType Directory -Force -Path (Join-Path $runState 'settings') | Out-Null
        '{"twitch":{"clientId":"gallery"}}' | Set-Content -LiteralPath (Join-Path $runState 'settings\settings.json') -Encoding utf8
    }

    if ($Section) {
        Set-UiSettingsSection -File (Join-Path $env:POPROSHAYKA_BASE_DIR 'settings\ui-preferences.toml') -Key $Section
    }

    $process = Start-Process -FilePath $exePath -ArgumentList $arguments -PassThru -Wait

    if ($process.ExitCode -ne 0) {
        $bindingErrorsFile = Join-Path $Output 'binding-errors.json'

        if (Test-Path -LiteralPath $bindingErrorsFile) {
            $bindingErrors = Get-Content -LiteralPath $bindingErrorsFile -Raw | ConvertFrom-Json

            if ($bindingErrors.count -gt 0) {
                Write-Host "Ошибок привязки: $($bindingErrors.count), записано $($bindingErrors.errors.Count)."
                $bindingErrors.errors | Select-Object -First 5 | ForEach-Object {
                    Write-Host "  [$($_.case)/$($_.theme)] $($_.message)"
                }

                throw "Съёмка вернула код $($process.ExitCode) – ошибки привязки выше, полный перечень в $bindingErrorsFile."
            }
        }

        throw "Съёмка вернула код $($process.ExitCode) – смотри logs\bot_log_*.log в $env:POPROSHAYKA_BASE_DIR."
    }
}
finally {
    if ($ownBaseDirectory) { Remove-Item Env:\POPROSHAYKA_BASE_DIR }

    if ($runState -and (Test-Path -LiteralPath $runState)) {
        if ($KeepState) {
            Write-Host "Состояние прогона оставлено: $runState"

            if ($Profile) {
                Write-Warning 'В копии лежат токены Twitch и пароль OBS из исходного профиля – удали её, когда разберёшь логи.'
            }
        }
        else {
            Remove-Item -LiteralPath $runState -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Get-ChildItem $Output -Filter '*.png' | Sort-Object Name | ForEach-Object { $_.FullName }
