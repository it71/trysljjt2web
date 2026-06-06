# STS2 Web Browser - 完整构建与部署指南

## 📋 目录
1. [环境准备](#环境准备)
2. [编译 godot_wry](#编译-godot_wry)
3. [编译模组](#编译模组)
4. [部署到游戏](#部署到游戏)
5. [使用说明](#使用说明)

---

## 🔧 环境准备

### Windows 10/11 (推荐)

1. **安装 .NET 8.0 SDK**
   - 下载地址：https://dotnet.microsoft.com/download/dotnet/8.0
   - 安装后验证：`dotnet --version`

2. **安装 Rust**
   - 下载地址：https://rustup.rs/
   - 运行安装程序，选择默认选项
   - 验证：`rustc --version` 和 `cargo --version`

3. **安装 Visual Studio Build Tools** (如果尚未安装)
   - 下载地址：https://visualstudio.microsoft.com/downloads/
   - 选择 "Build Tools for Visual Studio 2022"
   - 安装时勾选 "使用 C++ 的桌面开发"

4. **安装 Git**
   - 下载地址：https://git-scm.com/downloads

---

## 🛠️ 编译 godot_wry

### Windows

1. 打开 PowerShell 或命令提示符，进入项目目录：
   ```powershell
   cd C:\path\to\STS2WebBrowser
   ```

2. 如果还没有 godot_wry 仓库，克隆它：
   ```powershell
   git clone https://github.com/doceazedo/godot_wry.git
   cd godot_wry
   ```

3. 编译 Windows x86_64 版本：
   ```powershell
   cd rust
   cargo build --release --target x86_64-pc-windows-msvc
   ```

   > 注意：第一次编译可能需要 10-30 分钟，取决于网络和电脑性能

4. 复制编译好的 DLL：
   ```powershell
   # 从 godot_wry/rust/target/x86_64-pc-windows-msvc/release/
   # 复制 godot_wry.dll 到 ../lib/windows_x86_64/
   
   # 示例命令：
   copy target\x86_64-pc-windows-msvc\release\godot_wry.dll ..\..\lib\windows_x86_64\
   ```

### Linux (Ubuntu/Debian)

1. 安装依赖：
   ```bash
   sudo apt update
   sudo apt install -y build-essential libwebkit2gtk-4.1-dev
   ```

2. 编译：
   ```bash
   cd godot_wry/rust
   cargo build --release
   cp target/release/libgodot_wry.so ../../lib/linux_x86_64/
   ```

### macOS

1. 安装依赖：
   ```bash
   brew install webkit2gtk
   ```

2. 编译：
   ```bash
   cd godot_wry/rust
   cargo build --release
   cp target/release/libgodot_wry.dylib ../../lib/osx_universal/
   ```

---

## 📦 编译模组

1. 确保在项目根目录（包含 WebOverlay.csproj 的目录）

2. 编译模组：
   ```powershell
   # Windows
   dotnet build -c Release
   
   # 或 Linux/macOS
   dotnet build -c Release
   ```

3. 编译成功后，输出文件在 `bin/Release/net8.0/` 目录

---

## 🎮 部署到游戏

### 方法 1：手动部署（推荐）

1. 找到你的 Slay the Spire 2 游戏目录：
   - Steam：右键游戏 → 管理 → 浏览本地文件
   - 通常在：`C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire 2\`

2. 在游戏目录下创建 `mods` 文件夹（如果不存在）

3. 在 `mods` 文件夹下创建 `STS2WebBrowser` 文件夹

4. 复制以下文件到 `mods/STS2WebBrowser/`：
   - `WebOverlay.json`
   - `bin/Release/net8.0/STS2WebBrowser.dll`
   - `WebViewManager.cs` (可选，源码文件)
   - `lib/` 文件夹（包含编译好的 godot_wry 库）

5. 最终的目录结构应该是：
   ```
   SlayTheSpire 2/
   └── mods/
       └── STS2WebBrowser/
           ├── STS2WebBrowser.dll
           ├── STS2WebBrowser.json
           ├── WebViewManager.cs
           └── lib/
               ├── windows_x86_64/
               │   └── godot_wry.dll
               ├── linux_x86_64/
               │   └── libgodot_wry.so
               └── osx_universal/
                   └── libgodot_wry.dylib
   ```

### 方法 2：使用自动部署（如果项目配置了）

如果 WebOverlay.csproj 中配置了 `Sts2Dir`，可以直接：
```powershell
dotnet build -c Release
```

---

## 🚀 使用说明

1. **启动游戏**
   - 启动 Slay the Spire 2
   - 在模组菜单中启用 "STS2 Web Browser"

2. **打开浏览器**
   - 游戏中点击屏幕左上角的 🌐 按钮

3. **主要功能**
   - 🔗 点击外部浏览器按钮打开链接
   - — 点击最小化到悬浮图标
   - ⭐ 添加和管理收藏夹
   - 🎚️ 调整窗口透明度

4. **智能暂停**
   - 当队友在行动时，浏览器会自动暂停视频并最小化
   - 当回到你的回合时，点击悬浮图标恢复

---

## ⚠️ 常见问题

### Q: 编译 godot_wry 失败？
A: 确保安装了所有依赖，特别是 Visual Studio Build Tools (Windows) 或 webkit2gtk (Linux)

### Q: 模组加载失败？
A: 检查日志文件，确认所有文件都在正确位置，并且 .NET 8.0 已安装

### Q: WebView 不显示？
A: 
- Windows：确保安装了 Edge WebView2 运行时（通常 Windows 11 自带）
- Linux：安装 libwebkit2gtk-4.1-dev
- macOS：确保系统完整性设置允许

### Q: 如何获取帮助？
A: 查看项目 GitHub Issues 或创建新 Issue

---

## 📝 开发说明

### 文件结构
- `WebOverlay.cs` - 主模组文件，UI 和逻辑
- `WebViewManager.cs` - WebView 管理类
- `WebOverlay.csproj` - 项目配置
- `WebOverlay.json` - 模组元数据

### 贡献
欢迎提交 Issue 和 Pull Request！

---

**祝你玩得开心！🎮✨**
