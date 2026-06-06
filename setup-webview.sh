#!/bin/bash
# STS2WebBrowser - WebView 自动化安装脚本
# 用于检查环境、克隆和编译 godot_wry

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=============================================="
echo "🚀 STS2WebBrowser - WebView 安装脚本"
echo "=============================================="
echo ""

# ==============================================
# 1. 检查操作系统
# ==============================================
echo "📋 步骤 1: 检查操作系统..."

OS_TYPE=""
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS_TYPE="linux"
    echo "✅ 检测到 Linux 系统"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    OS_TYPE="macos"
    echo "✅ 检测到 macOS 系统"
elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" || "$OSTYPE" == "win32" ]]; then
    OS_TYPE="windows"
    echo "✅ 检测到 Windows 系统"
else
    echo "❌ 无法识别的操作系统: $OSTYPE"
    exit 1
fi

echo ""

# ==============================================
# 2. 检查 Rust
# ==============================================
echo "📋 步骤 2: 检查 Rust 工具链..."
if command -v rustc &> /dev/null; then
    RUST_VERSION=$(rustc --version)
    echo "✅ Rust 已安装: $RUST_VERSION"
else
    echo "⚠️  Rust 未找到，需要安装"
    echo ""
    echo "请访问 https://rustup.rs/ 安装 Rust"
    echo "Linux 命令:"
    echo "  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh"
    echo ""
    echo "安装后请重新运行此脚本"
    exit 1
fi

echo ""

# ==============================================
# 3. 检查 Linux 特定依赖
# ==============================================
if [ "$OS_TYPE" = "linux" ]; then
    echo "📋 步骤 3: 检查 Linux WebKit 依赖..."
    
    if command -v apt-get &> /dev/null; then
        echo "检测到 Debian/Ubuntu 系统"
        if dpkg -l | grep -q libwebkit2gtk-4.1-dev; then
            echo "✅ libwebkit2gtk-4.1-dev 已安装"
        else
            echo "⚠️  libwebkit2gtk-4.1-dev 未找到"
            echo "运行: sudo apt-get install libwebkit2gtk-4.1-dev"
        fi
    elif command -v dnf &> /dev/null; then
        echo "检测到 Fedora 系统"
        if rpm -q webkit2gtk4.1-devel &> /dev/null; then
            echo "✅ webkit2gtk4.1-devel 已安装"
        else
            echo "⚠️  webkit2gtk4.1-devel 未找到"
            echo "运行: sudo dnf install webkit2gtk4.1-devel"
        fi
    elif command -v pacman &> /dev/null; then
        echo "检测到 Arch Linux 系统"
        if pacman -Q webkit2gtk-4.1 &> /dev/null; then
            echo "✅ webkit2gtk-4.1 已安装"
        else
            echo "⚠️  webkit2gtk-4.1 未找到"
            echo "运行: sudo pacman -S webkit2gtk-4.1"
        fi
    fi
    
    echo ""
fi

# ==============================================
# 4. 克隆 godot_wry（如果不存在）
# ==============================================
echo "📋 步骤 4: 准备 godot_wry..."
GODOT_WRY_DIR="$SCRIPT_DIR/godot_wry"

if [ -d "$GODOT_WRY_DIR" ]; then
    echo "✅ godot_wry 已存在于: $GODOT_WRY_DIR"
    echo "是否要拉取最新更新？(y/n)"
    read -r UPDATE_REPO
    if [ "$UPDATE_REPO" = "y" ] || [ "$UPDATE_REPO" = "Y" ]; then
        cd "$GODOT_WRY_DIR"
        git pull
        echo "✅ 已更新"
        cd "$SCRIPT_DIR"
    fi
else
    echo "🔄 克隆 godot_wry 仓库..."
    git clone https://github.com/doceazedo/godot_wry.git "$GODOT_WRY_DIR"
    echo "✅ 克隆完成"
fi

echo ""

# ==============================================
# 5. 创建 lib 目录结构
# ==============================================
echo "📋 步骤 5: 创建输出目录..."

mkdir -p "$SCRIPT_DIR/lib/linux_x86_64"
mkdir -p "$SCRIPT_DIR/lib/windows_x86_64"
mkdir -p "$SCRIPT_DIR/lib/osx_universal"

echo "✅ 输出目录已创建"
echo "  - $SCRIPT_DIR/lib/linux_x86_64"
echo "  - $SCRIPT_DIR/lib/windows_x86_64"
echo "  - $SCRIPT_DIR/lib/osx_universal"
echo ""

# ==============================================
# 6. 编译前说明
# ==============================================
echo "=============================================="
echo "📝 环境检查完成！"
echo "=============================================="
echo ""
echo "下一步操作："
echo "1. 进入 godot_wry 目录并编译"
echo ""
echo "   cd $GODOT_WRY_DIR"
echo ""

if [ "$OS_TYPE" = "linux" ]; then
    echo "2. 编译 Linux 版本:"
    echo "   cargo build --release"
    echo ""
    echo "3. 复制库文件:"
    echo "   cp $GODOT_WRY_DIR/rust/target/release/libgodot_wry.so $SCRIPT_DIR/lib/linux_x86_64/"
elif [ "$OS_TYPE" = "macos" ]; then
    echo "2. 编译 macOS 版本:"
    echo "   cargo build --release"
    echo ""
    echo "3. 复制库文件:"
    echo "   cp $GODOT_WRY_DIR/rust/target/release/libgodot_wry.dylib $SCRIPT_DIR/lib/osx_universal/"
elif [ "$OS_TYPE" = "windows" ]; then
    echo "2. 编译 Windows 版本:"
    echo "   cargo build --release --target x86_64-pc-windows-msvc"
    echo ""
    echo "3. 复制库文件:"
    echo "   copy $GODOT_WRY_DIR\\rust\\target\\release\\godot_wry.dll $SCRIPT_DIR\\lib\\windows_x86_64\\"
fi

echo ""
echo "编译完成后，继续执行集成步骤！"
echo "详见: WEBVIEW_COMPLETE_GUIDE.md"
echo ""
