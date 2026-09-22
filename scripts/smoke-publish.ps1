# Smoke-test a pruned publish folder by briefly launching the WPF shell.
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$PublishDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PublishDir = $PublishDir.Trim('"').TrimEnd('\', '/')
$publishRoot = (Resolve-Path -LiteralPath $PublishDir).Path
$exePath = Join-Path $publishRoot 'EzRealmSync.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Missing $exePath"
}

foreach ($required in @('osu.Framework.dll', 'Realm.dll', 'realm-wrappers.dll')) {
    $path = Join-Path $publishRoot $required
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required file: $path"
    }
}

# 产品进程不加载 osu.Game.dll（全部读写走 DynamicRealm + OfficialWrite Worker）：
# 一旦它出现在发布目录，说明有工程又把它引成了产品依赖，必须当场拦下。
foreach ($forbidden in @('osu.Game.dll')) {
    $path = Join-Path $publishRoot $forbidden
    if (Test-Path -LiteralPath $path) {
        throw "Forbidden file in publish output: $path（产品不得依赖 osu.Game.dll）"
    }
}

$process = Start-Process -FilePath $exePath -ArgumentList '--ui-test' -PassThru -WorkingDirectory $publishRoot
try {
    if (-not $process.WaitForExit(8000)) {
        Write-Output 'EzRealmSync.exe --ui-test started successfully (still running after 8s).'
        return
    }

    if ($process.ExitCode -ne 0) {
        throw "EzRealmSync.exe --ui-test exited with code $($process.ExitCode)"
    }

    Write-Output 'EzRealmSync.exe --ui-test exited cleanly.'
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}
