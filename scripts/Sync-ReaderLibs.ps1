# 按 readers/sync-libs.config.json 从 NuGet 还原 osu.Game 闭包到 readers/{id}/lib/。
# 在仓库根目录或 Release 解压目录（exe 同目录）运行：pwsh scripts/Sync-ReaderLibs.ps1
param(
    [string]$ConfigPath,
    [string[]]$ReaderDir,
    [switch]$Force,
    [switch]$Prune,
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSScriptRoot
if (-not $ConfigPath) {
    $ConfigPath = Join-Path $scriptRoot 'readers\sync-libs.config.json'
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "未找到配置文件：$ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $config.packages) {
    throw '配置文件缺少 packages 数组。'
}

$cacheRoot = Join-Path $scriptRoot 'obj\SyncReaderLibs\cache'
$stagingRoot = Join-Path $scriptRoot 'obj\SyncReaderLibs\staging'
New-Item -ItemType Directory -Force -Path $cacheRoot, $stagingRoot | Out-Null

function Expand-ArchiveFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $extension = [System.IO.Path]::GetExtension($ArchivePath).ToLowerInvariant()

    if ($extension -eq '.nupkg' -or $extension -eq '.zip') {
        Expand-Archive -LiteralPath $ArchivePath -DestinationPath $Destination -Force
        return
    }

    throw "不支持的归档类型：$ArchivePath"
}

function Find-OsuGameLibRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SearchRoot
    )

    $gameDll = Get-ChildItem -LiteralPath $SearchRoot -Recurse -File -Filter 'osu.Game.dll' -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        Select-Object -First 1

    if (-not $gameDll) {
        throw "在 $SearchRoot 中未找到 osu.Game.dll。"
    }

    return $gameDll.Directory.FullName
}

function Clear-ReaderLibDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LibDirectory
    )

    if (-not (Test-Path -LiteralPath $LibDirectory)) {
        New-Item -ItemType Directory -Force -Path $LibDirectory | Out-Null
        return
    }

    Get-ChildItem -LiteralPath $LibDirectory -File -Filter '*.dll' -ErrorAction SilentlyContinue |
        Remove-Item -Force

    $runtimesDir = Join-Path $LibDirectory 'runtimes'
    if (Test-Path -LiteralPath $runtimesDir) {
        Remove-Item -LiteralPath $runtimesDir -Recurse -Force
    }
}

function Copy-ReaderLibClosure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,
        [Parameter(Mandatory = $true)]
        [string]$DestLib
    )

    Clear-ReaderLibDirectory -LibDirectory $DestLib

    $dlls = Get-ChildItem -LiteralPath $SourceRoot -File -Filter '*.dll' |
        Where-Object { $_.Name -notlike '*Game.Resources*' -and $_.Name -notlike '*Resources.dll' }

    if (-not $dlls) {
        throw "源目录无可用 DLL：$SourceRoot"
    }

    Copy-Item -LiteralPath ($dlls | ForEach-Object FullName) -Destination $DestLib -Force

    foreach ($candidate in @(
            (Join-Path $SourceRoot 'runtimes'),
            (Join-Path (Split-Path -Parent $SourceRoot) 'runtimes')
        )) {
        if (Test-Path -LiteralPath $candidate) {
            Copy-Item -LiteralPath $candidate -Destination (Join-Path $DestLib 'runtimes') -Recurse -Force
            break
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $DestLib 'osu.Game.dll'))) {
        throw "复制后缺少 osu.Game.dll：$DestLib"
    }
}

function Resolve-Template {
    param(
        [string]$Value,
        [hashtable]$Tokens
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    $resolved = $Value
    foreach ($entry in $Tokens.GetEnumerator()) {
        $resolved = $resolved.Replace('{' + $entry.Key + '}', [string]$entry.Value)
    }

    return $resolved
}

function Get-GithubReleaseDownloadUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repo,
        [Parameter(Mandatory = $true)]
        [string]$Tag,
        [Parameter(Mandatory = $true)]
        [string]$Asset
    )

    return "https://github.com/$Repo/releases/download/$Tag/$Asset"
}

function Download-ReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repo,
        [Parameter(Mandatory = $true)]
        [string]$Tag,
        [Parameter(Mandatory = $true)]
        [string]$Asset,
        [Parameter(Mandatory = $true)]
        [string]$DestinationFile
    )

    if ((Test-Path -LiteralPath $DestinationFile) -and -not $Force) {
        Write-Host "  使用缓存：$DestinationFile"
        return
    }

    $parent = Split-Path -Parent $DestinationFile
    New-Item -ItemType Directory -Force -Path $parent | Out-Null

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if ($gh) {
        Write-Host "  gh release download $Tag ($Asset) ..."
        if (Test-Path -LiteralPath $DestinationFile) {
            Remove-Item -LiteralPath $DestinationFile -Force
        }

        & gh release download $Tag --repo $Repo -p $Asset -D $parent | Out-Host
        if (-not (Test-Path -LiteralPath $DestinationFile)) {
            $downloaded = Get-ChildItem -LiteralPath $parent -File | Where-Object { $_.Name -eq $Asset } | Select-Object -First 1
            if ($downloaded -and $downloaded.FullName -ne $DestinationFile) {
                Move-Item -LiteralPath $downloaded.FullName -Destination $DestinationFile -Force
            }
        }

        if (Test-Path -LiteralPath $DestinationFile) {
            return
        }
    }

    $url = Get-GithubReleaseDownloadUrl -Repo $Repo -Tag $Tag -Asset $Asset
    Write-Host "  下载 $url ..."
    Invoke-WebRequest -Uri $url -OutFile $DestinationFile -UseBasicParsing
}

function Invoke-GithubReleaseSource {
    param(
        [Parameter(Mandatory = $true)]
        $Source,
        [Parameter(Mandatory = $true)]
        [string]$ReaderKey,
        [Parameter(Mandatory = $true)]
        [hashtable]$Tokens
    )

    $repo = [string]$Source.repo
    $tag = Resolve-Template -Value ([string]$Source.tag) -Tokens $Tokens
    $asset = Resolve-Template -Value ([string]$Source.asset) -Tokens $Tokens

    if ([string]::IsNullOrWhiteSpace($repo) -or [string]::IsNullOrWhiteSpace($tag) -or [string]::IsNullOrWhiteSpace($asset)) {
        throw 'github-release 需要 repo、tag、asset。'
    }

    $archivePath = Join-Path $cacheRoot "$ReaderKey\$asset"
    Download-ReleaseAsset -Repo $repo -Tag $tag -Asset $asset -DestinationFile $archivePath

    $extractDir = Join-Path $stagingRoot $ReaderKey
    Expand-ArchiveFile -ArchivePath $archivePath -Destination $extractDir
    return Find-OsuGameLibRoot -SearchRoot $extractDir
}

function Invoke-UrlSource {
    param(
        [Parameter(Mandatory = $true)]
        $Source,
        [Parameter(Mandatory = $true)]
        [string]$ReaderKey,
        [Parameter(Mandatory = $true)]
        [hashtable]$Tokens
    )

    $url = Resolve-Template -Value ([string]$Source.url) -Tokens $Tokens
    if ([string]::IsNullOrWhiteSpace($url)) {
        throw 'url 来源需要 url 字段。'
    }

    $fileName = [System.IO.Path]::GetFileName(([uri]$url).AbsolutePath)
    if ([string]::IsNullOrWhiteSpace($fileName)) {
        $fileName = 'download.zip'
    }

    $archivePath = Join-Path $cacheRoot "$ReaderKey\$fileName"
    if (-not (Test-Path -LiteralPath $archivePath) -or $Force) {
        Write-Host "  下载 $url ..."
        $parent = Split-Path -Parent $archivePath
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
        Invoke-WebRequest -Uri $url -OutFile $archivePath -UseBasicParsing
    }
    else {
        Write-Host "  使用缓存：$archivePath"
    }

    $extractDir = Join-Path $stagingRoot $ReaderKey
    Expand-ArchiveFile -ArchivePath $archivePath -Destination $extractDir
    return Find-OsuGameLibRoot -SearchRoot $extractDir
}

function Invoke-LocalDirSource {
    param(
        [Parameter(Mandatory = $true)]
        $Source
    )

    $path = [string]$Source.path
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw 'local-dir 需要 path 字段。'
    }

    if (-not [System.IO.Path]::IsPathRooted($path)) {
        $path = Join-Path $scriptRoot $path
    }

    $path = [System.IO.Path]::GetFullPath($path)
    if (-not (Test-Path -LiteralPath $path)) {
        throw "本地目录不存在：$path"
    }

    if (Test-Path -LiteralPath (Join-Path $path 'osu.Game.dll')) {
        return $path
    }

    return Find-OsuGameLibRoot -SearchRoot $path
}

function Get-SourceProperty {
    param(
        [Parameter(Mandatory = $true)]
        $Source,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($Source.PSObject.Properties.Match($Name).Count -eq 0) {
        return $null
    }

    return [string]$Source.$Name
}

function Invoke-NugetSource {
    param(
        [Parameter(Mandatory = $true)]
        $Source,
        [Parameter(Mandatory = $true)]
        $Package,
        [Parameter(Mandatory = $true)]
        [string]$ReaderKey,
        $Defaults
    )

    $gameId = Get-SourceProperty -Source $Source -Name 'gameId'
    $gameVersion = Get-SourceProperty -Source $Source -Name 'gameVersion'
    if ([string]::IsNullOrWhiteSpace($gameId) -or [string]::IsNullOrWhiteSpace($gameVersion)) {
        throw 'nuget 需要 gameId、gameVersion。'
    }

    $frameworkId = Get-SourceProperty -Source $Source -Name 'frameworkId'
    $frameworkVersion = Get-SourceProperty -Source $Source -Name 'frameworkVersion'
    $realmVersion = Get-SourceProperty -Source $Source -Name 'realmVersion'
    if ([string]::IsNullOrWhiteSpace($realmVersion) -and $Defaults -and $Defaults.PSObject.Properties.Match('realmVersion').Count -gt 0) {
        $realmVersion = [string]$Defaults.realmVersion
    }
    if ([string]::IsNullOrWhiteSpace($realmVersion)) {
        $realmVersion = '20.1.0'
    }

    $probeDir = Join-Path $stagingRoot "$ReaderKey\nuget-probe"
    $publishDir = Join-Path $stagingRoot "$ReaderKey\publish"
    if (Test-Path -LiteralPath $probeDir) {
        Remove-Item -LiteralPath $probeDir -Recurse -Force
    }
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $probeDir | Out-Null

    $packageRefs = @(
        "    <PackageReference Include=`"$gameId`" Version=`"$gameVersion`" />"
    )
    if (-not [string]::IsNullOrWhiteSpace($frameworkId)) {
        if ([string]::IsNullOrWhiteSpace($frameworkVersion)) {
            throw '指定 frameworkId 时需同时提供 frameworkVersion。'
        }
        $packageRefs += "    <PackageReference Include=`"$frameworkId`" Version=`"$frameworkVersion`" />"
    }
    $packageRefs += "    <PackageReference Include=`"Realm`" Version=`"$realmVersion`" />"

    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
$($packageRefs -join "`n")
  </ItemGroup>
</Project>
"@

    $programCs = @'
using osu.Game.Database;

internal static class Program
{
    private static void Main() => _ = typeof(RealmAccess);
}
'@

    Set-Content -LiteralPath (Join-Path $probeDir 'App.csproj') -Value $csproj -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $probeDir 'Program.cs') -Value $programCs -Encoding UTF8

    Write-Host "  dotnet publish NuGet $gameId $gameVersion (+ Realm $realmVersion) ..."
    & dotnet publish (Join-Path $probeDir 'App.csproj') -c Release -o $publishDir 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet 还原/发布失败（exit $LASTEXITCODE）。"
    }

    $libRoot = Find-OsuGameLibRoot -SearchRoot $publishDir
    return ,$libRoot
}

function Invoke-DotnetPublishSource {
    param(
        [Parameter(Mandatory = $true)]
        $Source,
        [Parameter(Mandatory = $true)]
        [string]$ReaderKey
    )

    $project = [string]$Source.project
    if ([string]::IsNullOrWhiteSpace($project)) {
        $project = '..\osu\osu.Game\osu.Game.csproj'
    }

    if (-not [System.IO.Path]::IsPathRooted($project)) {
        $project = Join-Path $scriptRoot $project
    }

    $project = [System.IO.Path]::GetFullPath($project)
    if (-not (Test-Path -LiteralPath $project)) {
        throw "dotnet-publish 项目不存在：$project"
    }

    $publishDir = Join-Path $stagingRoot "$ReaderKey\publish"
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    $gitRef = [string]$Source.gitRef
    $gitRepo = [string]$Source.gitRepo
    if ([string]::IsNullOrWhiteSpace($gitRepo)) {
        $gitRepo = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $project) '..'))
    }

    $worktreeDir = $null
    $publishProject = $project

    try {
        if (-not [string]::IsNullOrWhiteSpace($gitRef)) {
            $git = Get-Command git -ErrorAction SilentlyContinue
            if (-not $git) {
                throw '指定 gitRef 需要安装 git。'
            }

            $worktreeDir = Join-Path $stagingRoot "$ReaderKey\worktree"
            if (Test-Path -LiteralPath $worktreeDir) {
                & git -C $gitRepo worktree remove --force $worktreeDir 2>$null
                Remove-Item -LiteralPath $worktreeDir -Recurse -Force -ErrorAction SilentlyContinue
            }

            Write-Host "  git -C $gitRepo worktree add $worktreeDir $gitRef"
            & git -C $gitRepo fetch --tags --depth 1 2>&1 | Out-Host
            & git -C $gitRepo worktree add --detach $worktreeDir $gitRef
            $publishProject = Join-Path $worktreeDir 'osu.Game\osu.Game.csproj'
            if (-not (Test-Path -LiteralPath $publishProject)) {
                throw "worktree 中未找到 osu.Game.csproj：$publishProject"
            }
        }

        $configuration = if ([string]$Source.configuration) { [string]$Source.configuration } else { $Configuration }
        Write-Host "  dotnet publish $publishProject -c $configuration ..."
        & dotnet publish $publishProject -c $configuration -o $publishDir 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish 失败（exit $LASTEXITCODE）。"
        }

        $libRoot = Find-OsuGameLibRoot -SearchRoot $publishDir
        return ,$libRoot
    }
    finally {
        if ($worktreeDir -and (Test-Path -LiteralPath $worktreeDir)) {
            & git -C $gitRepo worktree remove --force $worktreeDir 2>$null
        }
    }
}

function Resolve-SourceRoot {
    param(
        [Parameter(Mandatory = $true)]
        $Package
    )

    $readerKey = [string]$Package.readerDir
    $source = $Package.source
    if (-not $source) {
        throw "readerDir=$readerKey 缺少 source。"
    }

    $tagValue = $null
    if ($source.PSObject.Properties.Match('tag').Count -gt 0) {
        $tagValue = [string]$source.tag
    }

    $tokens = @{
        tag = $tagValue
    }

    switch ([string]$source.type) {
        'nuget' { return Invoke-NugetSource -Source $source -Package $package -ReaderKey $readerKey -Defaults $config.defaults }
        'github-release' { return Invoke-GithubReleaseSource -Source $source -ReaderKey $readerKey -Tokens $tokens }
        'url' { return Invoke-UrlSource -Source $source -ReaderKey $readerKey -Tokens $tokens }
        'local-dir' { return Invoke-LocalDirSource -Source $source }
        'dotnet-publish' { return Invoke-DotnetPublishSource -Source $source -ReaderKey $readerKey }
        default { throw "未知 source.type：$($source.type)" }
    }
}

$selected = @($config.packages)
if ($ReaderDir -and $ReaderDir.Count -gt 0) {
    $filter = @($ReaderDir)
    $selected = @($selected | Where-Object { $filter -contains [string]$_.readerDir })
}

if ($selected.Count -eq 0) {
    throw '没有匹配的 reader 包。'
}

Write-Host "Sync-ReaderLibs: $($selected.Count) 个包 -> $scriptRoot\readers\{id}\lib"
Write-Host "配置：$ConfigPath"
Write-Host ''

$pruneScript = Join-Path $PSScriptRoot 'prune-publish.ps1'
$failures = @()

foreach ($package in $selected) {
    $readerDirName = [string]$package.readerDir
    $destLib = Join-Path $scriptRoot "readers\$readerDirName\lib"
    $comment = [string]$package.comment

    Write-Host "==> $readerDirName$(if ($comment) { " — $comment" })"

    try {
        $sourceRoot = Resolve-SourceRoot -Package $package
        Write-Host "  源：$sourceRoot"
        Write-Host "  目标：$destLib"

        Copy-ReaderLibClosure -SourceRoot $sourceRoot -DestLib $destLib

        if ($Prune -and (Test-Path -LiteralPath $pruneScript)) {
            Write-Host '  裁剪 dead weight DLL ...'
            & $pruneScript $destLib
        }

        $dllCount = (Get-ChildItem -LiteralPath $destLib -File -Filter '*.dll').Count
        Write-Host "  完成：$dllCount 个 DLL"
    }
    catch {
        $failures += "${readerDirName}: $($_.Exception.Message)"
        Write-Warning $_
    }

    Write-Host ''
}

if ($failures.Count -gt 0) {
    throw ("Sync-ReaderLibs 失败：`n" + ($failures -join "`n"))
}

Write-Host 'Sync-ReaderLibs 全部完成。'
