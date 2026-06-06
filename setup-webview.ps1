# STS2WebBrowser - WebView 自动化安装脚本 (Windows PowerShell)
# 用于检查环境、克隆和编译 godot_wry

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "🚀 STS2WebBrowser - WebView 安装脚本" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

# ==============================================
# 1. 检查操作系统
# ==============================================
Write-Host "📋 步骤 1: 检查操作系统..." -ForegroundColor Yellow

$osType = "windows"
Write-Host "✅ 检测到 Windows 系统" -ForegroundColor Green
Write-Host ""

# ==============================================
# 2. 检查 Rust
# ==============================================
Write-Host "📋 步骤 2: 检查 Rust 工具链..." -ForegroundColor Yellow

try {
    $rustVersion = rustc --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Rust 已安装: $rustVersion" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Rust 未找到，需要安装" -ForegroundColor Red
        Write-Host ""
        Write-Host "请访问 https://rustup.rs/ 安装 Rust"
        Write-Host ""
        Write-Host "安装后请重新运行此脚本"
        exit 1
    }
} catch {
    Write-Host "⚠️  Rust 未找到，需要安装" -ForegroundColor Red
    Write-Host ""
    Write-Host "请访问 https://rustup.rs/ 安装 Rust"
    Write-Host ""
    exit 1
}

Write-Host ""

# ==============================================
# 3. 检查 Visual Studio Build Tools
# ==============================================
Write-Host "📋 步骤 3: 检查编译工具..." -ForegroundColor Yellow

$vsInstallPath = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\VisualStudio\SxS\VS7" -ErrorAction SilentlyContinue
$hasVs = $false

if ($vsInstallPath) {
    Write-Host "✅ Visual Studio 已找到" -ForegroundColor Green
    $hasVs = $true
} else {
    Write-Host "⚠️  Visual Studio Build Tools 可能未找到" -ForegroundColor Yellow
    Write-Host "请确保已安装 Visual Studio Build Tools 或 Visual Studio 2022"
    Write-Host "下载地址: https://visualstudio.microsoft.com/downloads/"
    Write-Host "选择 '使用 C++ 的桌面开发' 工作负载"
}

Write-Host ""

# ==============================================
# 4. 克隆 godot_wry（如果不存在）
# ==============================================
Write-Host "📋 步骤 4: 准备 godot_wry..." -ForegroundColor Yellow

$godotWryDir = Join-Path $ScriptDir "godot_wry"

if (Test-Path $godotWryDir) {
    Write-Host "✅ godot_wry 已存在于: $godotWryDir" -ForegroundColor Green
    $update = Read-Host "是否要拉取最新更新？(y/n)"
    
    if ($update -eq "y" -or $update -eq "Y") {
        Set-Location $godotWryDir
        git pull
        Write-Host "✅ 已更新" -ForegroundColor Green
        Set-Location $ScriptDir
    }
} else {
    Write-Host "🔄 克隆 godot_wry 仓库..." -ForegroundColor Cyan
    git clone https://github.com/doceazedo/godot_wry.git $godotWryDir
    Write-Host "✅ 克隆完成" -ForegroundColor Green
}

Write-Host ""

# ==============================================
# 5. 创建 lib 目录结构
# ==============================================
Write-Host "📋 步骤 5: 创建输出目录..." -ForegroundColor Yellow

$libDir = Join-Path $ScriptDir "lib"
$windowsDir = Join-Path $libDir "windows_x86_64"
$linuxDir = Join-Path $libDir "linux_x86_64"
$macDir = Join-Path $libDir "osx_universal"

New-Item -ItemType Directory -Path $windowsDir -Force | Out-Null
New-Item -ItemType Directory -Path $linuxDir -Force | Out-Null
New-Item -ItemType Directory -Path $macDir -Force | Out-Null

Write-Host "✅ 输出目录已创建" -ForegroundColor Green
Write-Host "  - $windowsDir"
Write-Host "  - $linuxDir"
Write-Host "  - $macDir"
Write-Host ""

# ==============================================
# 6. 编译前说明
# ==============================================
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "📝 环境检查完成！" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步操作："
Write-Host "1. 进入 godot_wry 目录并编译"
Write-Host ""
Write-Host "   cd $godotWryDir"
Write-Host ""
Write-Host "2. 编译 Windows 版本:"
Write-Host "   cargo build --release --target x86_64-pc-windows-msvc"
Write-Host ""
Write-Host "3. 复制库文件:"
$srcDll = Join-Path $godotWryDir "rust\target\x86_64-pc-windows-msvc\release\godot_wry.dll"
Write-Host "   copy `"$srcDll`" `"$windowsDir`""
Write-Host ""
Write-Host "编译完成后，继续执行集成步骤！"
Write-Host "详见: WEBVIEW_COMPLETE_GUIDE.md"
Write-Host ""
