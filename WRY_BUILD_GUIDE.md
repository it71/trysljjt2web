# godot_wry 编译指南

## 🚀 快速开始

### 前置要求

#### Windows
```powershell
# 1. 安装 Rust（如果还没有）
# 下载地址：https://rustup.rs

# 2. 安装 Visual Studio Build Tools
# 下载地址：https://visualstudio.microsoft.com/downloads/
# 选择 "C++ Build Tools"

# 3. 安装 just（可选，但推荐）
cargo install just
```

#### Linux (Ubuntu/Debian)
```bash
# 1. 安装 Rust
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# 2. 安装编译依赖
sudo apt update
sudo apt install -y \
    build-essential \
    libwebkit2gtk-4.1-dev \
    libsoup-3.0-dev \
    libjavascriptcoregtk-4.1-dev \
    libglib2.0-dev \
    libgtk-3-dev

# 3. 安装 just（可选）
cargo install just
```

#### macOS
```bash
# 1. 安装 Rust
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# 2. 安装编译依赖（通过 Homebrew）
brew installwebkitgtk

# 3. 安装 just（可选）
cargo install just
```

---

## 📦 编译步骤

### 方法一：使用 just（推荐）

```bash
# 1. 克隆 godot_wry
git clone https://github.com/doceazedo/godot_wry.git
cd godot_wry

# 2. 编译所有平台
just build

# 3. 编译特定平台
just build-windows    # Windows
just build-linux      # Linux
just build-macos     # macOS
```

### 方法二：手动编译

```bash
# Windows
cargo build --release --target x86_64-pc-windows-msvc
# 输出：rust/target/x86_64-pc-windows-msvc/release/godot_wry.dll

# Linux
cargo build --release --target x86_64-unknown-linux-gnu
# 输出：rust/target/x86_64-unknown-linux-gnu/release/libgodot_wry.so

# macOS (Intel)
cargo build --release --target x86_64-apple-darwin
# 输出：rust/target/x86_64-apple-darwin/release/libgodot_wry.dylib

# macOS (Apple Silicon)
cargo build --release --target aarch64-apple-darwin
# 输出：rust/target/aarch64-apple-darwin/release/libgodot_wry.dylib
```

---

## 📂 输出文件

编译完成后，在 `rust/target/release/` 目录下找到：

| 平台 | 文件名 | 放置位置 |
|------|--------|----------|
| Windows | `godot_wry.dll` | `mods/STS2WebBrowser/lib/` |
| Linux | `libgodot_wry.so` | `mods/STS2WebBrowser/lib/` |
| macOS (Intel) | `libgodot_wry.dylib` | `mods/STS2WebBrowser/lib/` |
| macOS (Apple Silicon) | `libgodot_wry.dylib` | `mods/STS2WebBrowser/lib/` |

---

## 🔧 目录结构

编译后，你的模组目录应该这样组织：

```
SlayTheSpire2/
└── mods/
    └── STS2WebBrowser/
        ├── STS2WebBrowser.dll          # 主模组 DLL
        ├── STS2WebBrowser.dll.config
        ├── STS2WebBrowser.json
        └── lib/
            ├── godot_wry.dll           # Windows
            ├── libgodot_wry.so         # Linux
            └── libgodot_wry.dylib     # macOS
```

---

## ⚠️ 常见问题

### Q: 编译失败，提示缺少库？

**Linux:**
```bash
# Ubuntu/Debian
sudo apt install libwebkit2gtk-4.1-dev

# Fedora
sudo dnf install webkit2gtk4.1-devel

# Arch Linux
sudo pacman -S webkit2gtk-4.1
```

**Windows:**
确保已安装 Visual Studio Build Tools，并选择 "C++ Build Tools" 工作负载。

**macOS:**
```bash
brew installwebkitgtk
```

### Q: 提示 "could not find native static library"？

确保系统已安装所有编译依赖。对于 Linux，确保安装了 `*webkit*` 开发包。

### Q: Linux 编译报错 "webkit2gtk-4.1"？

检查你的 Linux 发行版版本。WebKitGTK 4.1 可能不在旧版本中。使用 WebKitGTK 4.0 作为替代：
```bash
sudo apt install libwebkit2gtk-4.0-dev
```

然后修改 `rust/Cargo.toml` 中的依赖版本。

### Q: 如何验证编译成功？

```bash
# 检查输出文件是否存在
ls -lh rust/target/release/libgodot_wry.*

# 检查是否是有效的动态库
file rust/target/release/libgodot_wry.so  # Linux
file rust/target/release/libgodot_wry.dylib  # macOS
```

---

## 📚 参考资料

- [godot_wry GitHub](https://github.com/doceazedo/godot_wry)
- [WRY 文档](https://docs.rs/wry/)
- [Rust 官方安装](https://www.rust-lang.org/tools/install)
- [Building from source](https://godot-wry.doceazedo.com/contributing/compiling.html)

---

## ✅ 下一步

编译完成后，将输出文件复制到模组的 `lib/` 目录，然后开始使用 C# WebView 管理器！

详见：[WEBVIEW_INTEGRATION.md](WEBVIEW_INTEGRATION.md)
