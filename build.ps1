# HandleGuard 一键编译
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) { throw "找不到 csc.exe：$csc" }

$out  = Join-Path $root "build"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$src  = @("AssemblyVersion.cs","Native.cs","Inspector.cs","Brand.cs","Theme.cs","ConfirmDlg.cs","AboutDlg.cs","MainForm.cs","Program.cs") |
        ForEach-Object { Join-Path $root ("src\" + $_) }
$refs = @("/reference:System.dll","/reference:System.Drawing.dll","/reference:System.Windows.Forms.dll")
$ico  = Join-Path $root "src\app.ico"
if (-not (Test-Path -LiteralPath $ico)) { throw "缺少图标：$ico（先运行 .\make-icon.ps1）" }

# 正式版：要求管理员 + PerMonitorV2 DPI
& $csc /nologo /target:winexe /codepage:65001 "/out:$out\HandleGuard.exe" `
       "/win32manifest:$root\src\app.manifest" "/win32icon:$ico" $refs $src
if ($LASTEXITCODE -ne 0) { throw "正式版编译失败" }

# 免提权调试版：用于看界面与本机采样，处置动作受限
& $csc /nologo /target:winexe /codepage:65001 "/out:$out\HandleGuard-test.exe" "/win32icon:$ico" $refs $src
if ($LASTEXITCODE -ne 0) { throw "调试版编译失败" }

Write-Host "编译完成：$out\HandleGuard.exe"
