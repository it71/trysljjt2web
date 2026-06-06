# 🎉 STS2WebBrowser - 内置 WebView 实施指南

## 📋 实施总结

我们已为你准备好了完整的 **内置 WebView 浏览器** 解决方案！

### ✅ 已完成的工作

| 文档 | 说明 |
|------|------|
| [WRY_BUILD_GUIDE.md](WRY_BUILD_GUIDE.md) | godot_wry 跨平台编译指南 |
| [WebViewManager.cs](WebViewManager.cs) | C# WebView 管理器类 |
| [WEBVIEW_INTEGRATION.md](WEBVIEW_INTEGRATION.md) | 详细集成步骤和代码示例 |
| [VIDEO_CONTROL.md](VIDEO_CONTROL.md) | 视频控制脚本和使用说明 |

---

## 🚀 快速开始

### 第一步：编译 godot_wry（需要1-2小时）

根据你的操作系统，选择对应的编译方式：

#### Windows
```powershell
# 1. 安装 Rust
# https://rustup.rs

# 2. 安装 Visual Studio Build Tools
# https://visualstudio.microsoft.com/downloads/
# 选择 "C++ Build Tools"

# 3. 克隆并编译
git clone https://github.com/doceazedo/godot_wry.git
cd godot_wry
cargo build --release --target x86_64-pc-windows-msvc

# 4. 复制 DLL
copy rust\target\x86_64-pc-windows-msvc\release\godot_wry.dll ..\STS2WebBrowser\lib\windows_x86_64\
```

#### Linux (Ubuntu/Debian)
```bash
# 1. 安装依赖
sudo apt update
sudo apt install -y build-essential libwebkit2gtk-4.1-dev

# 2. 安装 Rust
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# 3. 克隆并编译
git clone https://github.com/doceazedo/godot_wry.git
cd godot_wry
cargo build --release

# 4. 复制 .so 文件
cp rust/target/release/libgodot_wry.so ../STS2WebBrowser/lib/linux_x86_64/
```

#### macOS
```bash
# 1. 安装依赖
brew installwebkitgtk

# 2. 安装 Rust
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh

# 3. 克隆并编译
git clone https://github.com/doceazedo/godot_wry.git
cd godot_wry
cargo build --release

# 4. 复制 .dylib 文件
cp rust/target/release/libgodot_wry.dylib ../STS2WebBrowser/lib/osx_universal/
```

### 第二步：集成到模组

1. 将 `WebViewManager.cs` 添加到项目中
2. 按照 [WEBVIEW_INTEGRATION.md](WEBVIEW_INTEGRATION.md) 修改 `WebOverlay.cs`
3. 创建 `lib/` 目录并放置编译好的库文件

### 第三步：测试

1. 编译模组
2. 在游戏中启用
3. 测试基本浏览功能
4. 测试视频暂停功能

---

## 🎯 核心功能

### ✅ 已实现

| 功能 | 说明 | 状态 |
|------|------|------|
| WebView 初始化 | 创建和管理 WebView | ✅ 框架完成 |
| URL 加载 | 加载任意网页 | ✅ 框架完成 |
| JavaScript 执行 | 执行自定义 JS | ✅ 框架完成 |
| 视频控制 | 暂停/播放视频 | ✅ 脚本完成 |
| 悬浮图标 | 最小化到图标 | ✅ 已实现 |
| 自动暂停 | 队友回合自动暂停 | ⏳ 待测试 |

### ⏳ 待实现

| 功能 | 说明 | 优先级 |
|------|------|--------|
| 游戏回合检测 | Hook 游戏事件 | P1 |
| 自动恢复 | 队友回合结束时恢复 | P1 |
| 配置面板 | 用户偏好设置 | P2 |

---

## 📂 预期文件结构

```
SlayTheSpire2/
└── mods/
    └── STS2WebBrowser/
        ├── STS2WebBrowser.dll          # 主模组
        ├── STS2WebBrowser.dll.config
        ├── STS2WebBrowser.json
        ├── WebViewManager.cs            # WebView 管理器（源码）
        ├── lib/
        │   ├── windows_x86_64/
        │   │   └── godot_wry.dll      # Windows WebView2
        │   ├── linux_x86_64/
        │   │   └── libgodot_wry.so     # Linux WebKitGTK
        │   └── osx_universal/
        │       └── libgodot_wry.dylib  # macOS WebKit
        └── scripts/
            └── VideoControl.js         # 视频控制脚本
```

---

## 🎬 视频控制功能

### 如何工作

当用户点击最小化按钮时：
1. 执行 JavaScript 暂停所有视频
2. 隐藏 WebView
3. 显示悬浮图标
4. 用户专注游戏

### 支持的平台

| 平台 | 状态 | 说明 |
|------|------|------|
| Bilibili | ✅ | 特殊 API 支持 |
| YouTube | ✅ | IFrame API 支持 |
| 抖音 | ✅ | 直接 video 控制 |
| 通用 | ✅ | querySelectorAll |
| 其他视频站 | ⏳ | 可能需要额外适配 |

---

## ⚠️ 重要提示

### Linux 用户注意

需要安装 WebKitGTK：
```bash
# Ubuntu/Debian
sudo apt install libwebkit2gtk-4.1-dev

# Fedora
sudo dnf install webkit2gtk4.1-devel

# Arch Linux
sudo pacman -S webkit2gtk-4.1
```

### Windows 用户注意

Windows 10/11 通常自带 WebView2。如果用户使用的是旧版 Windows，可能需要安装 Edge WebView2 运行时。

---

## 🔧 常见问题

### Q: 编译失败，提示缺少库？

确保已安装所有编译依赖。详见 [WRY_BUILD_GUIDE.md](WRY_BUILD_GUIDE.md)。

### Q: WebView 不显示？

1. 检查库文件是否在正确位置
2. 检查是否有错误日志
3. 确认系统 WebView 已安装

### Q: 视频无法暂停？

某些网站的视频可能有特殊的播放控制机制。可能需要针对特定网站编写专门的暂停代码。

### Q: 如何添加新的视频网站支持？

在 `WebViewManager.cs` 的 `SmartPauseVideo()` 方法中添加域名检测逻辑。

---

## 📞 获取帮助

- **GitHub Issues**: https://github.com/it71/trysljjt2web/issues
- **godot_wry 文档**: https://godot-wry.doceazedo.com/
- **WRY 库**: https://docs.rs/wry/

---

## 🎯 下一步行动

**请按以下顺序执行：**

1. ✅ 确认你有 Rust 编译环境
2. ⏳ 克隆并编译 godot_wry（30分钟-2小时）
3. ⏳ 将库文件放入正确位置
4. ⏳ 修改 WebOverlay.cs 添加集成代码
5. ⏳ 编译并测试
6. ⏳ 报告任何问题

---

**祝你编码愉快！🎮✨**
