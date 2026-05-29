# 将 osu 仓库的项目级 EditorConfig 链接到本目录（需与 osu 同级：Ez2Lazer/osu、Ez2Lazer/EzRealmSync）。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$osuEditorConfig = Join-Path $root '..\osu\.editorconfig'
$linkPath = Join-Path $root '.editorconfig'

if (-not (Test-Path $osuEditorConfig)) {
    Write-Error "找不到 $osuEditorConfig — 请确认 osu 仓库在 EzRealmSync 的上一级目录。"
}

if (Test-Path $linkPath) {
    Remove-Item $linkPath -Force
}

New-Item -ItemType SymbolicLink -Path $linkPath -Target $osuEditorConfig | Out-Null
Write-Host "已链接: $linkPath -> $osuEditorConfig"
