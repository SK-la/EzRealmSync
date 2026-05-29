# 从同级 osu-resources 复制 Noto CJK 字体到 Assets/Fonts（约 20MB+）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "..\osu-resources\osu.Game.Resources\Fonts\Noto"
$dst = Join-Path $root "osu.EzRealmSync\Assets\Fonts\Noto"

if (-not (Test-Path $src)) {
    Write-Error "未找到 $src ，请先克隆 osu-resources 仓库。"
}

New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item (Join-Path $src "Noto-CJK-Basic*") $dst -Force
Copy-Item (Join-Path $src "Noto-CJK-Compatibility*") $dst -Force
Copy-Item (Join-Path $src "Noto-Basic*") $dst -Force
Copy-Item (Join-Path $src "LICENSE.txt") $dst -Force -ErrorAction SilentlyContinue
Write-Host "已复制到 $dst"
