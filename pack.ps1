# 打包发布包到 dist/
#   完整包：BepInEx + DBModKit + SmartSkill，首次安装用
#   仅插件：DBModKit + SmartSkill，已装过 BepInEx 的人更新用
#   内容包：仓库 content/ 下的每个内容包，加上 -Content 指定的文件夹，原样做成工坊条目
# 用法：pwsh ./pack.ps1 [-GameDir "X:\...\地下城竞技场"] [-Content "X:\我的内容包", ...]
param(
    [string]$GameDir = "D:\Steam\steamapps\common\地下城竞技场",
    [string[]]$Content = @()
)
$ErrorActionPreference = "Stop"

$BepInExVersion = "5.4.23.5"
$BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"

$root = $PSScriptRoot
$sln = Join-Path $root "DungeonBrawlMods.sln"
$dist = Join-Path $root "dist"
$cache = Join-Path $root ".cache"

# 通过 dotnet-install 装在用户目录的 SDK 不一定在 PATH 里
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $userDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $env:PATH = "$userDotnet;$env:PATH"
    $env:DOTNET_ROOT = $userDotnet
}

# 解决方案里的每个项目都是一个插件
$plugins = @(dotnet sln $sln list | Where-Object { $_ -like "*.csproj" } |
    ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) } | Sort-Object)
if ($plugins -notcontains "DBModKit") { throw "解决方案里找不到 DBModKit" }

# 每个插件的版本号是源码里的 Version 常量，更新日志在各自目录的 CHANGELOG.md
$versions = @{}
foreach ($name in $plugins) {
    $source = Get-ChildItem (Join-Path $root $name) -Filter *.cs |
        Where-Object { Select-String -Path $_.FullName -Pattern 'const string Version = ' -Quiet } |
        Select-Object -First 1
    $v = [regex]::Match((Get-Content $source.FullName -Raw), 'const string Version = "([^"]+)"').Groups[1].Value
    if (-not $v) { throw "无法读取 $name 的版本号" }
    if (-not (Select-String -Path (Join-Path $root "$name\CHANGELOG.md") -Pattern "## $([regex]::Escape($v))" -Quiet)) {
        throw "$name/CHANGELOG.md 里还没有 $v 的条目"
    }
    $versions[$name] = $v
}
$version = $versions["SmartSkill"]
Write-Host "打包 SmartSkill $version（DBModKit $($versions["DBModKit"])）"

dotnet build $sln -c Release -nologo -v q -p:DeployToGame=false "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "编译失败" }

New-Item -ItemType Directory -Force $cache | Out-Null
$bepinexZip = Join-Path $cache "BepInEx_win_x64_$BepInExVersion.zip"
if (-not (Test-Path $bepinexZip)) {
    Write-Host "下载 BepInEx $BepInExVersion"
    Invoke-WebRequest $BepInExUrl -OutFile $bepinexZip
}

if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }
New-Item -ItemType Directory -Force $dist | Out-Null
$readme = Join-Path $root "安装说明.txt"

# 插件项目里的 lang/（翻译）和 assets/（图片、AssetBundle）放在 dll 旁边，ModContext.Folder 从这里找
function Copy-ModContent([string]$plugin, [string]$dest) {
    foreach ($sub in "lang", "assets") {
        $src = Join-Path $root "$plugin\$sub"
        if (Test-Path $src) { Copy-Item -Recurse $src (Join-Path $dest $sub) }
    }
}

function New-Package([string]$name, [bool]$withBepInEx) {
    $stage = Join-Path $dist "_stage_$name"
    New-Item -ItemType Directory -Force $stage | Out-Null
    if ($withBepInEx) { Expand-Archive $bepinexZip -DestinationPath $stage }
    foreach ($plugin in $plugins) {
        $pluginDir = Join-Path $stage "BepInEx\plugins\$plugin"
        New-Item -ItemType Directory -Force $pluginDir | Out-Null
        Copy-Item (Join-Path $root "$plugin\bin\Release\netstandard2.1\$plugin.dll") $pluginDir
        Copy-ModContent $plugin $pluginDir
    }
    Copy-Item $readme $stage
    $zip = Join-Path $dist "SmartSkill-$version-$name.zip"
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
    Remove-Item -Recurse -Force $stage
    Write-Host "  -> $zip"
}

New-Package "完整包" $true
New-Package "仅插件" $false

# 创意工坊条目：每个插件一个文件夹（dll + dbmod.json），DBModKit 除外（它和 BepInEx 一起装在游戏目录）
$workshop = Join-Path $dist "workshop"
foreach ($plugin in $plugins | Where-Object { $_ -ne "DBModKit" }) {
    $itemDir = Join-Path $workshop $plugin
    New-Item -ItemType Directory -Force $itemDir | Out-Null
    Copy-Item (Join-Path $root "$plugin\bin\Release\netstandard2.1\$plugin.dll") $itemDir
    Copy-ModContent $plugin $itemDir
    [ordered]@{
        Format      = 1
        Type        = "plugin"
        Name        = $plugin
        Author      = ""
        Version     = $versions[$plugin]
        Description = ""
        MinModKit   = $versions["DBModKit"]
        Assemblies  = @("$plugin.dll")
    } | ConvertTo-Json | Set-Content (Join-Path $itemDir "dbmod.json") -Encoding utf8
    Write-Host "  -> $itemDir"
}

# 内容包条目：不用编译，检查 dbmod.json 后整个文件夹复制过去
$contentDirs = @($Content | ForEach-Object { Get-Item $_ })
$contentRoot = Join-Path $root "content"
if (Test-Path $contentRoot) { $contentDirs += @(Get-ChildItem $contentRoot -Directory) }
foreach ($dir in $contentDirs) {
    $manifestPath = Join-Path $dir.FullName "dbmod.json"
    if (-not (Test-Path $manifestPath)) { throw "$($dir.FullName) 里没有 dbmod.json" }
    $manifest = Get-Content $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
    if ($manifest.Type -ne "content") { throw "$manifestPath 的 Type 不是 content" }
    if (-not $manifest.Id) { Write-Warning "$($dir.Name)：dbmod.json 没写 Id，会用工坊条目编号当 Id，和本地测试时的技能编号不一样" }
    foreach ($file in $manifest.Content) {
        if (-not (Test-Path (Join-Path $dir.FullName $file))) { throw "$($dir.Name)：找不到 Content 里列出的 $file" }
    }
    $itemDir = Join-Path $workshop $dir.Name
    Copy-Item -Recurse $dir.FullName $itemDir
    Write-Host "  -> $itemDir（内容包 $($manifest.Name) $($manifest.Version)）"
}

# 运行环境条目：BepInEx + DBModKit 的安装包（游戏工坊不加载 dll，玩家订阅后运行 install.bat 复制进游戏目录）
$runtime = Join-Path $workshop "Runtime"
$template = Join-Path $root "workshop\Runtime"
New-Item -ItemType Directory -Force $runtime | Out-Null
Copy-Item -Recurse (Join-Path $template "*") $runtime -Exclude "preview.png"
$runtimeFiles = Join-Path $runtime "files"
Expand-Archive $bepinexZip -DestinationPath $runtimeFiles
# BepInEx 自己的更新日志不需要放进游戏目录
Remove-Item (Join-Path $runtimeFiles "changelog.txt") -ErrorAction SilentlyContinue
$kitDir = Join-Path $runtimeFiles "BepInEx\plugins\DBModKit"
New-Item -ItemType Directory -Force $kitDir | Out-Null
Copy-Item (Join-Path $root "DBModKit\bin\Release\netstandard2.1\DBModKit.dll") $kitDir
[ordered]@{
    Format      = 1
    Type        = "runtime"
    Name        = "DBModKit Runtime"
    Author      = ""
    Version     = $versions["DBModKit"]
    Description = "BepInEx $BepInExVersion + DBModKit"
    MinModKit   = ""
    Assemblies  = @()
} | ConvertTo-Json | Set-Content (Join-Path $runtime "dbmod.json") -Encoding utf8
Write-Host "  -> $runtime（运行环境 $($versions["DBModKit"])）"
