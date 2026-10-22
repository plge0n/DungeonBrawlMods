# 打包 SmartSkill 发布包到 dist/
#   完整包：BepInEx + 插件，首次安装用
#   仅插件：只有插件，已装过 BepInEx 的人更新用
# 用法：pwsh ./pack.ps1 [-GameDir "X:\...\地下城竞技场"]
param(
    [string]$GameDir = "D:\Steam\steamapps\common\地下城竞技场"
)
$ErrorActionPreference = "Stop"

$BepInExVersion = "5.4.23.5"
$BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"

$root = $PSScriptRoot
$proj = Join-Path $root "SmartSkill"
$dist = Join-Path $root "dist"
$cache = Join-Path $root ".cache"

# 通过 dotnet-install 装在用户目录的 SDK 不一定在 PATH 里
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $userDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $env:PATH = "$userDotnet;$env:PATH"
    $env:DOTNET_ROOT = $userDotnet
}

$version = [regex]::Match((Get-Content (Join-Path $proj "Plugin.cs") -Raw), 'Version = "([^"]+)"').Groups[1].Value
if (-not $version) { throw "无法从 Plugin.cs 读取版本号" }
if (-not (Select-String -Path (Join-Path $root "CHANGELOG.md") -Pattern "## $([regex]::Escape($version))" -Quiet)) {
    throw "CHANGELOG.md 里还没有 $version 的条目"
}
Write-Host "打包 SmartSkill $version"

dotnet build $proj -c Release -nologo -v q -p:DeployToGame=false "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "编译失败" }
$dll = Join-Path $proj "bin\Release\netstandard2.1\SmartSkill.dll"

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
    $pluginDir = Join-Path $stage "BepInEx\plugins\SmartSkill"
    New-Item -ItemType Directory -Force $pluginDir | Out-Null
    Copy-Item $dll $pluginDir
    Copy-Item $readme $stage
    $zip = Join-Path $dist "SmartSkill-$version-$name.zip"
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
    Remove-Item -Recurse -Force $stage
    Write-Host "  -> $zip"
}

New-Package "完整包" $true
New-Package "仅插件" $false
