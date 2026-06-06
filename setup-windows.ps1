# STS2 Web Browser - Windows 快速设置脚本
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  STS2 Web Browser - 快速设置" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查 Rust
Write-Host "[1/5] 检查 Rust..." -ForegroundColor Yellow
try {
    $rustVersion = rustc --version
    Write-Host "✓ Rust 已安装: $rustVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ Rust 未安装，请从 https://rustup.rs/ 下载安装" -ForegroundColor Red
    Read-Host "按 Enter 键退出"
    exit 1
}

# 检查 .NET
Write-Host ""
Write-Host "[2/5] 检查 .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK 已安装: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK 未安装，请从 https://dotnet.microsoft.com/download/dotnet/8.0 下载安装" -ForegroundColor Red
    Read-Host "按 Enter 键退出"
    exit 1
}

# 检查 Git
Write-Host ""
Write-Host "[3/5] 检查 Git..." -ForegroundColor Yellow
try {
    $gitVersion = git --version
    Write-Host "✓ Git 已安装: $gitVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ Git 未安装，请从 https://git-scm.com/downloads 下载安装" -ForegroundColor Red
    Read-Host "按 Enter 键退出"
    exit 1
}

# 克隆 godot_wry（如果不存在）
Write-Host ""
Write-Host "[4/5] 检查 godot_wry..." -ForegroundColor Yellow
if (-not (Test-Path "godot_wry")) {
    Write-Host "正在克隆 godot_wry 仓库..." -ForegroundColor Cyan
    git clone https://github.com/doceazedo/godot_wry.git
    if (-not (Test-Path "godot_wry")) {
        Write-Host "✗ 克隆失败" -ForegroundColor Red
        Read-Host "按 Enter 键退出"
        exit 1
    }
    Write-Host "✓ godot_wry 克隆成功" -ForegroundColor Green
} else {
    Write-Host "✓ godot_wry 已存在" -ForegroundColor Green
}

# 创建 lib 目录
Write-Host ""
Write-Host "[5/5] 创建目录结构..." -ForegroundColor Yellow
$dirs = @("lib\windows_x86_64", "lib\linux_x86_64", "lib\osx_universal")
foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✓ 创建目录: $dir" -ForegroundColor Green
    } else {
        Write-Host "✓ 目录已存在: $dir" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  环境准备完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步：" -ForegroundColor Yellow
Write-Host "1. 进入 godot_wry/rust 目录"
Write-Host "2. 运行: cargo build --release --target x86_64-pc-windows-msvc"
Write-Host "3. 复制 godot_wry.dll 到 lib/windows_x86_64/"
Write-Host "4. 运行: dotnet build -c Release"
Write-Host ""
Write-Host "详细说明请查看 BUILD_AND_DEPLOY.md" -ForegroundColor Cyan
Write-Host ""
Read-Host "按 Enter 键退出"
