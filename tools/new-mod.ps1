# 新建一个依赖 DBModKit 的 Mod 项目并加入解决方案
# 用法：pwsh tools/new-mod.ps1 MyMod [-Description "一句话介绍"]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')][string]$Name,
    [string]$Description = "TODO：一句话介绍这个 Mod"
)
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot
if (Test-Path (Join-Path $root $Name)) { throw "$Name 已存在" }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $userDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $env:PATH = "$userDotnet;$env:PATH"
    $env:DOTNET_ROOT = $userDotnet
}

# 每次都重新安装模板，保证用的是仓库里的最新版本
dotnet new install (Join-Path $root "templates\DBMod") --force | Out-Null
if ($LASTEXITCODE -ne 0) { throw "安装模板失败" }

Push-Location $root
try {
    dotnet new dbmod -n $Name -o $Name --description $Description
    if ($LASTEXITCODE -ne 0) { throw "生成项目失败" }
    dotnet sln DungeonBrawlMods.sln add "$Name\$Name.csproj"
    if ($LASTEXITCODE -ne 0) { throw "加入解决方案失败" }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "已创建 $Name。编译并部署：dotnet build DungeonBrawlMods.sln -c Release"
