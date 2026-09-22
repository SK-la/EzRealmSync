# 从 osu 仓库提取「官方 mod 缩写白名单」，供 EzRealmSync 判定成绩能否被官方客户端还原。
#
# 为什么用生成而不是反射：产品进程不加载 osu.Game.dll（见 docs/DATA-OPERATIONS.zh.md），
# 所以官方 mod 名录必须以纯数据形式随包发布；本脚本是这份数据的唯一来源。
#
#   pwsh scripts/Generate-OfficialModList.ps1 -OsuRepo "E:\BASE CODE\GitHub\Ez2Lazer\osu"
#
# 提取口径：官方四规则集 + 跨规则集共享 mod 目录里的 `Acronym => "XX"` 字面量。
# 排除 Ez 目录（Ez*）、BMS/Diva 规则集、测试与编译产物——这些不是官方客户端认识的 mod。

param(
    [Parameter(Mandatory = $true)]
    [string]$OsuRepo,

    [string]$Output = (Join-Path $PSScriptRoot '..\osu.Game.EzRealmSync\Realm\OfficialModList.json')
)

$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path $OsuRepo).Path
$rulesets = @('osu', 'mania', 'taiko', 'catch')
$acronymPattern = 'override\s+string\s+Acronym\s*=>\s*@?"([^"]+)"'

function Get-Acronyms {
    param([string]$Root, [string]$RelativePrefix)

    $dir = Join-Path $repo $Root
    if (-not (Test-Path $dir)) {
        throw "找不到目录：$dir"
    }

    $files = Get-ChildItem -Path $dir -Recurse -Filter *.cs -File |
        Where-Object {
            $relative = $_.FullName.Substring($repo.Length).TrimStart('\', '/')

            # Ez 目录 / 测试 / 编译产物：都不是官方客户端认识的 mod。
            if ($relative -split '[\\/]' | Where-Object { $_ -like 'Ez*' -or $_ -eq 'Tests' -or $_ -eq 'bin' -or $_ -eq 'obj' }) {
                return $false
            }

            return $true
        }

    $found = [System.Collections.Generic.SortedSet[string]]::new()

    foreach ($file in $files) {
        foreach ($match in [regex]::Matches((Get-Content -Raw $file.FullName), $acronymPattern)) {
            $acronym = $match.Groups[1].Value.Trim()

            # 空串是 MultiMod 之类的基类占位，不是可解析的 mod。
            if ($acronym) {
                [void]$found.Add($acronym)
            }
        }
    }

    return @($found)
}

$shared = Get-Acronyms -Root 'osu.Game\Rulesets\Mods'
$perRuleset = [ordered]@{}

foreach ($ruleset in $rulesets) {
    $perRuleset[$ruleset] = Get-Acronyms -Root "osu.Game.Rulesets.$ruleset"
}

$head = git -C $repo rev-parse --short HEAD 2>$null
if (-not $head) {
    $head = 'unknown'
}

$payload = [ordered]@{
    '$comment'    = '官方 osu! mod 缩写白名单；由 scripts/Generate-OfficialModList.ps1 从 osu 仓库生成，请勿手改。'
    generatedFrom = "osu @ $head"
    shared        = $shared
}

foreach ($ruleset in $perRuleset.Keys) {
    $payload[$ruleset] = $perRuleset[$ruleset]
}

$json = $payload | ConvertTo-Json -Depth 4
$target = [System.IO.Path]::GetFullPath($Output)
[System.IO.File]::WriteAllText($target, $json + "`n", [System.Text.UTF8Encoding]::new($false))

$total = $shared.Count + ($perRuleset.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
Write-Host "已写入 $target（共享 $($shared.Count) + 规则集 $total 条）"
