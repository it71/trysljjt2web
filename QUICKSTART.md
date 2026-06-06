# 🚀 STS2WebBrowser - 5分钟快速开始

## 第 0 步: 选择你的操作系统 🖥️

根据你的系统选择对应指南！

---

## Windows 用户

### ✅ 一步到位（推荐）

```powershell
# 1. 打开 PowerShell（以管理员运行）
cd \你的\项目\路径

# 2. 运行自动化脚本
.\setup-webview.ps1
```

### 或者手动操作

1. **安装 Rust** → https://rustup.rs/
2. **安装 Visual Studio Build Tools** → https://visualstudio.microsoft.com/downloads/
3. **克隆项目**
   ```powershell
   git clone https://github.com/doceazedo/godot_wry.git
   cd godot_wry
   ```
4. **编译**
   ```powershell
   cargo build --release --target x86_64-pc-windows-msvc
   ```
5. **复制文件**
   ```powershell
   copy rust\target\x86_64-pc-windows-msvc\release\godot_wry.dll ..\lib\windows_x86_64\
   ```

---

## Linux 用户

### ✅ 一步到位（推荐）

```bash
# 1. 打开终端
cd /你的/项目/路径

# 2. 运行自动化脚本
chmod +x setup-webview.sh
./setup-webview.sh
```

### 或者手动操作

1. **安装依赖**
   ```bash
   # Ubuntu/Debian
   sudo apt-get update
   sudo apt-get install -y build-essential libwebkit2gtk-4.1-dev

   # Fedora
   sudo dnf install webkit2gtk4.1-devel

   # Arch Linux
   sudo pacman -S webkit2gtk-4.1
   ```

2. **安装 Rust**
   ```bash
   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
   ```

3. **克隆并编译**
   ```bash
   git clone https://github.com/doceazedo/godot_wry.git
   cd godot_wry
   cargo build --release
   ```

4. **复制文件**
   ```bash
   cp rust/target/release/libgodot_wry.so ../lib/linux_x86_64/
   ```

---

## macOS 用户

### ✅ 一步到位（推荐）

```bash
# 1. 打开终端
cd /你的/项目/路径

# 2. 运行自动化脚本
chmod +x setup-webview.sh
./setup-webview.sh
```

### 或者手动操作

1. **安装 Homebrew（如果没有）**
   ```bash
   /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
   ```

2. **安装依赖**
   ```bash
   brew installwebkitgtk
   ```

3. **安装 Rust**
   ```bash
   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
   ```

4. **克隆并编译**
   ```bash
   git clone https://github.com/doceazedo/godot_wry.git
   cd godot_wry
   cargo build --release
   ```

5. **复制文件**
   ```bash
   cp rust/target/release/libgodot_wry.dylib ../lib/osx_universal/
   ```

---

## 🎉 编译完成后的操作

### 1. 目录结构检查

确保你的项目有以下文件：

```
STS2WebBrowser/
├── lib/
│   ├── linux_x86_64/
│   │   └── libgodot_wry.so       (Linux)
│   ├── windows_x86_64/
│   │   └── godot_wry.dll         (Windows)
│   └── osx_universal/
│       └── libgodot_wry.dylib    (macOS)
└── ... 其他文件
```

### 2. 集成 WebView 到模组

按照 [WEBVIEW_INTEGRATION.md](WEBVIEW_INTEGRATION.md) 修改 `WebOverlay.cs`

### 3. 编译模组

```bash
# 使用你的 .NET 编译命令
dotnet build WebOverlay.csproj --configuration Release -p:Sts2Dir="你的游戏路径"
```

### 4. 复制到游戏

编译后将模组文件夹复制到 SlayTheSpire2/mods/ 目录

---

## ❓ 遇到问题？

1. **编译失败** → 查看 [WRY_BUILD_GUIDE.md](WRY_BUILD_GUIDE.md)
2. **集成问题** → 查看 [WEBVIEW_INTEGRATION.md](WEBVIEW_INTEGRATION.md)
3. **完整文档** → [WEBVIEW_COMPLETE_GUIDE.md](WEBVIEW_COMPLETE_GUIDE.md)

---

## ⏰ 预计时间

| 步骤 | 时间 |
|------|------|
| 安装 Rust & 工具 | 5-10 分钟 |
| 克隆 godot_wry | 1 分钟 |
| 编译 | 15-45 分钟（取决于电脑） |
| 集成和测试 | 10 分钟 |
| **总计** | **约 1 小时** |

---

## 🎯 成功后你将获得

- ✅ 游戏内直接浏览网页
- ✅ 观看 B站、YouTube 视频
- ✅ 一键暂停功能（点击最小化时）
- ✅ 悬浮小图标模式

---

**祝你编码愉快！🎮✨**
