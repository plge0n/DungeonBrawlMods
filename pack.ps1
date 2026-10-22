# 打包发布包到 dist/
#   完整包：BepInEx + DBModKit + SmartSkill，首次安装用
#   仅插件：DBModKit + SmartSkill，已装过 BepInEx 的人更新用
# 用法：pwsh ./pack.ps1 [-GameDir "X:\...\地下城竞技场"]
param(
    [string]$GameDir = "D:\Steam\steamapps\common\地下城竞技场"
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

function New-Package([string]$name, [bool]$withBepInEx) {
    $stage = Join-Path $dist "_stage_$name"
    New-Item -ItemType Directory -Force $stage | Out-Null
    if ($withBepInEx) { Expand-Archive $bepinexZip -DestinationPath $stage }
    foreach ($plugin in $plugins) {
        $pluginDir = Join-Path $stage "BepInEx\plugins\$plugin"
        New-Item -ItemType Directory -Force $pluginDir | Out-Null
        Copy-Item (Join-Path $root "$plugin\bin\Release\netstandard2.1\$plugin.dll") $pluginDir
    }
    Copy-Item $readme $stage
    $zip = Join-Path $dist "SmartSkill-$version-$name.zip"
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
    Remove-Item -Recurse -Force $stage
    Write-Host "  -> $zip"
}

New-Package "完整包" $true
New-Package "仅插件" $false
