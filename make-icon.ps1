# HandleGuard 图标生成：几何来自 src\Brand.cs，编成 build\icon-shot.exe 后输出 src\app.ico
# 用法：.\make-icon.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { throw "找不到 csc.exe：$csc" }

$out  = Join-Path $root "build"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$tool = Join-Path $out "icon-shot.exe"
$ico  = Join-Path $root "src\app.ico"

& $csc /nologo /target:exe /codepage:65001 "/out:$tool" `
       (Join-Path $root "src\Brand.cs") (Join-Path $root "src\IconShot.cs") `
       /reference:System.dll /reference:System.Drawing.dll
if ($LASTEXITCODE -ne 0) { throw "图标生成器编译失败" }

& $tool $ico (Join-Path $out "icon-256.png") (Join-Path $out "icon-sizes.png")
if ($LASTEXITCODE -ne 0) { throw "图标生成失败" }

$fi = Get-Item -LiteralPath $ico
Write-Host ("图标已生成：{0}（{1:N1} KB）" -f $fi.FullName, ($fi.Length / 1024))
