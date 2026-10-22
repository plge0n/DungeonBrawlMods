# 地下城竞技场 Mod 运行环境（BepInEx + DBModKit）安装器 / Dungeon Brawl mod runtime installer
# 由创意工坊条目里的 install.bat / uninstall.bat 调用。Windows PowerShell 5.1 可用（本文件需保存为带 BOM 的 UTF-8）。
#   安装/更新：把 files\ 里的内容复制到游戏目录（不动 BepInEx\config、玩家自己装的其他插件）
#   卸载：删掉 Doorstop 加载器（winhttp.dll 等）和 DBModKit，游戏回到原版；BepInEx 文件夹和配置保留
param([switch]$Uninstall, [string]$GameDir)
$ErrorActionPreference = "Stop"

$AppId = "4246640"
$item = Split-Path -Parent $PSScriptRoot
$files = Join-Path $item "files"
$loaderFiles = @("winhttp.dll", "doorstop_config.ini", ".doorstop_version")

function Say([string]$zh, [string]$en) { Write-Host $zh; Write-Host "  $en" -ForegroundColor DarkGray }
function Fail([string]$zh, [string]$en) { Write-Host ""; Write-Host $zh -ForegroundColor Red; Write-Host "  $en" -ForegroundColor DarkGray; exit 1 }

# 条目在 <库>\steamapps\workshop\content\4246640\<条目号>\，游戏在 <库>\steamapps\common\<installdir>\
function Find-GameDir {
    $steamapps = (Resolve-Path (Join-Path $item "..\..\..\..")).Path
    $manifest = Join-Path $steamapps "appmanifest_$AppId.acf"
    if (Test-Path $manifest) {
        $m = Select-String -Path $manifest -Pattern '"installdir"\s+"([^"]+)"' -Encoding UTF8 | Select-Object -First 1
        if ($m) {
            $dir = Join-Path $steamapps ("common\" + $m.Matches[0].Groups[1].Value)
            if (Test-Path (Join-Path $dir "DungeonBrawl.exe")) { return $dir }
        }
    }
    # 游戏和工坊内容不在同一个库里时，让玩家自己选
    Add-Type -AssemblyName System.Windows.Forms
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = "选择游戏目录（里面有 DungeonBrawl.exe） / Select the game folder (contains DungeonBrawl.exe)"
    if ($dlg.ShowDialog() -ne "OK") { return $null }
    return $dlg.SelectedPath
}

if (-not $GameDir) { $GameDir = Find-GameDir }
if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir "DungeonBrawl.exe"))) {
    Fail "没找到游戏目录（需要有 DungeonBrawl.exe）。" "Game folder not found (it must contain DungeonBrawl.exe)."
}
Say "游戏目录：$GameDir" "Game folder: $GameDir"

if (Get-Process DungeonBrawl -ErrorAction SilentlyContinue) {
    Fail "请先退出游戏再运行。" "Please quit the game first."
}

if ($Uninstall) {
    foreach ($f in $loaderFiles) {
        $p = Join-Path $GameDir $f
        if (Test-Path $p) { Remove-Item -Force $p }
    }
    $kit = Join-Path $GameDir "BepInEx\plugins\DBModKit"
    if (Test-Path $kit) { Remove-Item -Recurse -Force $kit }
    Say "已卸载：游戏不会再加载 Mod。BepInEx 文件夹（含配置）保留着，不需要可以手动删除。" `
        "Uninstalled: the game no longer loads mods. The BepInEx folder (with your settings) was kept; delete it manually if you like."
    Say "记得在创意工坊取消订阅，否则以后运行 install.bat 又会装回来。" "Also unsubscribe in the Workshop."
    exit 0
}

if (-not (Test-Path $files)) { Fail "条目文件不完整（缺少 files 文件夹），请在 Steam 里验证或重新订阅。" "Item files are incomplete (missing 'files'). Re-subscribe in Steam." }

# 复制：files\ 下的目录结构和游戏目录一一对应
$count = 0
Get-ChildItem $files -Recurse -File -Force | ForEach-Object {
    $rel = $_.FullName.Substring($files.Length + 1)
    $dest = Join-Path $GameDir $rel
    $dir = Split-Path -Parent $dest
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    Copy-Item -Force $_.FullName $dest
    $count++
}

# 记下装的是哪个版本，DBModKit 据此提示工坊里有没有更新
$meta = Get-Content (Join-Path $item "dbmod.json") -Raw -Encoding UTF8 | ConvertFrom-Json
$marker = Join-Path $GameDir "BepInEx\DBModKit\runtime.json"
New-Item -ItemType Directory -Force (Split-Path -Parent $marker) | Out-Null
[ordered]@{ Version = $meta.Version; Item = (Split-Path -Leaf $item); Installed = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss") } |
    ConvertTo-Json | Set-Content $marker -Encoding UTF8

Write-Host ""
Say "安装完成（$count 个文件，运行环境 $($meta.Version)）。启动游戏即可，第一次启动会慢一点。" `
    "Done ($count files, runtime $($meta.Version)). Start the game; the first launch takes a bit longer."
Say "装了 Mod 后成绩不会上传排行榜（游戏作者的规则）。卸载请运行 uninstall.bat。" `
    "With mods installed, scores are not uploaded to the leaderboard (game author's rule). Run uninstall.bat to remove."
