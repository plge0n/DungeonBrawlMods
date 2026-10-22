# 在游戏目录安装/卸载开发工具（只给开发者用，不随 Mod 分发）
#   UnityExplorer —— 游戏内按 F7：浏览场景对象、查看/修改字段、执行 C#
#   ScriptEngine  —— 从 BepInEx/scripts 加载插件，文件变化时自动重载（或按 F6）
# 用法：pwsh tools/devtools.ps1 [-Remove] [-GameDir "X:\...\地下城竞技场"]
param(
    [switch]$Remove,
    [string]$GameDir = $(if ($env:GameDir) { $env:GameDir } else { "D:\Steam\steamapps\common\地下城竞技场" })
)
$ErrorActionPreference = "Stop"

$UnityExplorer = @{ Repo = "yukieiji/UnityExplorer"; Tag = "v4.13.6"; Asset = "UnityExplorer.BepInEx5.Mono.zip" }
$ScriptEngine = @{ Repo = "BepInEx/BepInEx.Debug"; Tag = "r11.2"; Asset = "ScriptEngine_r11.2.zip" }

$bepinex = Join-Path $GameDir "BepInEx"
if (-not (Test-Path (Join-Path $bepinex "core\BepInEx.dll"))) { throw "$GameDir 里没有装 BepInEx 5" }
if (Get-Process DungeonBrawl -ErrorAction SilentlyContinue) { throw "请先关闭游戏" }

$explorerDir = Join-Path $bepinex "plugins\sinai-dev-UnityExplorer"
$scriptEngineDll = Join-Path $bepinex "plugins\ScriptEngine.dll"
$scriptEngineCfg = Join-Path $bepinex "config\com.bepis.bepinex.scriptengine.cfg"
$scriptsDir = Join-Path $bepinex "scripts"

if ($Remove) {
    # 热重载目录里的 Mod 要移回 plugins，否则卸载 ScriptEngine 后它们不会被加载
    if (Test-Path $scriptsDir) {
        foreach ($dll in Get-ChildItem $scriptsDir -Filter *.dll) {
            $target = Join-Path $bepinex "plugins\$($dll.BaseName)"
            New-Item -ItemType Directory -Force $target | Out-Null
            Move-Item $dll.FullName $target -Force
            Write-Host "移回 plugins：$($dll.Name)"
        }
        Remove-Item $scriptsDir -Recurse -Force
    }
    foreach ($p in $explorerDir, $scriptEngineDll, $scriptEngineCfg) {
        if (Test-Path $p) { Remove-Item $p -Recurse -Force; Write-Host "已删除 $p" }
    }
    Write-Host "开发工具已卸载"
    return
}

$cache = Join-Path (Split-Path $PSScriptRoot) ".cache"
New-Item -ItemType Directory -Force $cache | Out-Null

function Get-Release($r) {
    $zip = Join-Path $cache $r.Asset
    if (-not (Test-Path $zip)) {
        Write-Host "下载 $($r.Repo) $($r.Tag)"
        Invoke-WebRequest "https://github.com/$($r.Repo)/releases/download/$($r.Tag)/$($r.Asset)" -OutFile $zip
    }
    return $zip
}

# UnityExplorer 包内是 plugins/sinai-dev-UnityExplorer/...
Expand-Archive (Get-Release $UnityExplorer) -DestinationPath $bepinex -Force
Write-Host "UnityExplorer -> $explorerDir"

# ScriptEngine 包内路径用的是反斜杠，Expand-Archive 会当成文件名，手动取出 dll
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Get-Release $ScriptEngine))
try {
    $entry = $archive.Entries | Where-Object { $_.Name -eq "ScriptEngine.dll" -or $_.FullName -like "*ScriptEngine.dll" } | Select-Object -First 1
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $scriptEngineDll, $true)
}
finally {
    $archive.Dispose()
}
New-Item -ItemType Directory -Force $scriptsDir | Out-Null
Write-Host "ScriptEngine -> $scriptEngineDll"

# 启动时加载 scripts 里的 Mod，文件变化 1 秒后自动重载
@"
[General]
LoadOnStart = true
ReloadKey = F6
QuietMode = false
IncludeSubdirectories = false

[AutoReload]
EnableFileSystemWatcher = true
AutoReloadDelay = 1
DumpAssemblies = false
"@ | Set-Content $scriptEngineCfg -Encoding utf8
Write-Host "ScriptEngine 配置 -> $scriptEngineCfg"

Write-Host ""
Write-Host "完成。之后 dotnet build 会把可热重载的 Mod 部署到 BepInEx/scripts（DBModKit 仍在 plugins）。"
Write-Host "现在重新编译一次，把已有 Mod 移到 scripts：dotnet build DungeonBrawlMods.sln -c Release"
