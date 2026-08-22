# Removes osu.Game.Resources and unused transitive dependencies from a publish folder.
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$PublishDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PublishDir = $PublishDir.Trim('"').TrimEnd('\', '/')
$publishRoot = (Resolve-Path -LiteralPath $PublishDir).Path

function Get-FolderSizeBytes {
    param([string]$Path)
    (Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
}

function Test-ProtectedFile {
    param([string]$FileName)
    $name = [System.IO.Path]::GetFileName($FileName)
    switch -Wildcard ($name) {
        'EzRealmSync.exe' { return $true }
        'EzRealmSync.dll' { return $true }
        'osu.EzRealmSync.AppModel.dll' { return $true }
        'osu.Game.EzRealmSync.dll' { return $true }
        'osu.Game.dll' { return $true }
        'osu.Framework.dll' { return $true }
        'Realm.dll' { return $true }
        'realm-wrappers.dll' { return $true }
        'e_sqlite3.dll' { return $true }
        default { return $false }
    }
}

$prunePatterns = @(
    'osu.Game.Resources*.dll',
    'Veldrid*.dll',
    'vk.dll',
    'libveldrid*.dll',
    'SharpFNT*.dll',
    'StbiSharp*.dll',
    'SixLabors.ImageSharp*.dll',
    'NAudio*.dll',
    'BASS*.dll',
    'ManagedBass*.dll',
    'FFmpeg*.dll',
    'ppy.SDL*.dll',
    'Microsoft.CodeAnalysis*.dll',
    'Microsoft.AspNetCore.SignalR*.dll',
    'nunit*.dll',
    'NUnit*.dll',
    'OpenTabletDriver*.dll',
    'osuTK*.dll',
    'ppy.osuTK*.dll',
    'McEndu.FreeTypeSharp*.dll',
    'plutosvgft.dll',
    'bass*.dll',
    'SDL2.dll',
    'SDL3.dll'
)

$beforeBytes = Get-FolderSizeBytes -Path $publishRoot
$removedFiles = @()

foreach ($pattern in $prunePatterns) {
    Get-ChildItem -LiteralPath $publishRoot -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        if (Test-ProtectedFile -FileName $_.Name) {
            return
        }

        $removedFiles += $_
    }
}

$removedFiles = $removedFiles | Sort-Object FullName -Unique

foreach ($file in $removedFiles) {
    Remove-Item -LiteralPath $file.FullName -Force
}

$afterBytes = Get-FolderSizeBytes -Path $publishRoot
$removedBytes = [math]::Max(0, $beforeBytes - $afterBytes)
$removedMb = [math]::Round($removedBytes / 1MB, 2)
$afterMb = [math]::Round($afterBytes / 1MB, 2)

Write-Output "PruneEzRealmSyncPublish: removed $($removedFiles.Count) file(s), freed ${removedMb} MB; publish folder now ${afterMb} MB ($publishRoot)"
