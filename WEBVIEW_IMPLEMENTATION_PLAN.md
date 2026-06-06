# STS2WebBrowser - 内置WebView实现方案

## 🎯 目标

在杀戮尖塔2游戏中实现**真正的内置浏览器**，支持：
- ✅ 在游戏内直接浏览网页
- ✅ 播放视频（特别是B站、YouTube）
- ✅ 自动暂停/播放视频（智能检测队友回合）
- ✅ JavaScript 交互控制

---

## 📊 WebView 技术对比

| 方案 | Web引擎 | 包体积 | 难度 | 推荐度 |
|------|---------|--------|------|--------|
| **godot-webview** | Chromium | ~200MB | 高 | ⭐⭐⭐ |
| **gdCEF** | Chromium | ~500MB | 中 | ⭐⭐ |
| **godot_wry** ⭐ | 系统原生 | 0额外依赖 | 中 | ⭐⭐⭐⭐⭐ |

### 🏆 推荐：godot_wry

**优势**：
- 使用系统自带 WebView（Windows Edge / macOS Safari / Linux WebKitGTK）
- **无需打包浏览器引擎** - 模组体积超小！
- 开源免费
- 支持 JavaScript 交互
- 跨平台完美支持

---

## 🔧 技术实现方案

### 方案一：集成 godot_wry（推荐）✅

#### 架构设计

```
┌─────────────────────────────────┐
│   STS2Mod (C#)                  │
│  ┌───────────────────────────┐  │
│  │  WebOverlay.cs            │  │
│  │  - UI 管理               │  │
│  │  - 事件检测              │  │
│  │  - 视频控制逻辑          │  │
│  └───────────┬───────────────┘  │
│              │                  │
│              │ P/Invoke         │
│              ▼                  │
│  ┌───────────────────────────┐  │
│  │  libgodot_wry.so/dll     │  │
│  │  (Rust 编译的共享库)      │  │
│  └───────────┬───────────────┘  │
│              │                  │
│              ▼                  │
│  ┌───────────────────────────┐  │
│  │  系统 WebView             │  │
│  │  Windows: WebView2        │  │
│  │  macOS: WebKit            │  │
│  │  Linux: WebKitGTK        │  │
│  └───────────────────────────┘  │
└─────────────────────────────────┘
```

#### 实现步骤

**阶段1：编译 godot_wry 共享库** ⚠️ 需要你本地环境

```bash
# 1. 克隆 godot_wry
git clone https://github.com/doceazedo/godot_wry.git
cd godot_wry

# 2. 编译 Rust 代码为共享库
cargo build --release

# Windows: 生成 godot_wry.dll
# Linux: 生成 libgodot_wry.so
# macOS: 生成 libgodot_wry.dylib
```

**阶段2：C# 集成代码**

```csharp
// WebViewManager.cs
public class WebViewManager
{
    // P/Invoke 声明
    [DllImport("godot_wry")]
    private static extern IntPtr wry_create(int width, int height);
    
    [DllImport("godot_wry")]
    private static extern void wry_load_url(IntPtr handle, string url);
    
    [DllImport("godot_wry")]
    private static extern void wry_execute_js(IntPtr handle, string js);
    
    [DllImport("godot_wry")]
    private static extern void wry_pause_video(IntPtr handle);
    
    [DllImport("godot_wry")]
    private static extern void wry_destroy(IntPtr handle);
    
    // 获取 WebView 纹理用于渲染
    [DllImport("godot_wry")]
    private static extern IntPtr wry_get_texture(IntPtr handle);
}
```

**阶段3：UI 集成**

```csharp
// 在 WebOverlay.cs 中
private static WebViewManager? _webView;

private static void CreateWebViewContent()
{
    // 创建 WebView
    _webView = new WebViewManager();
    _webView.Initialize(MAIN_PANEL_WIDTH - 40, MAIN_PANEL_HEIGHT - 120);
    
    // 创建 TextureRect 显示 WebView
    var webTexture = new TextureRect
    {
        ExpandMode = TextureRect.ExpandModeEnum.FitScreen,
        StretchMode = TextureRect.StretchModeEnum.KeepAspect
    };
    
    // 将 WebView 纹理绑定到 TextureRect
    webTexture.Texture = _webView.GetTexture();
    
    // 添加到 UI
    _browserContainer.AddChild(webTexture);
}
```

#### 暂停视频的 JavaScript

```csharp
private static void PauseCurrentVideo()
{
    // 通用视频暂停
    string pauseJs = @"
        document.querySelectorAll('video').forEach(v => v.pause());
        document.querySelectorAll('iframe').forEach(iframe => {
            try {
                iframe.contentWindow.postMessage('{\"event\":\"pause\"}', '*');
            } catch(e) {}
        });
    ";
    
    // B站特殊处理
    string bilibiliPauseJs = @"
        if (window.bilibiliPlayer) {
            bilibiliPlayer.pause();
        }
        document.querySelector('video')?.pause();
    ";
    
    _webView?.ExecuteJavaScript(bilibiliPauseJs);
}
```

---

### 方案二：简化方案（无内置WebView）⚠️

如果集成 godot_wry 太复杂，可以：

#### 2a. 使用 HTTP 截图 API（最简单但功能有限）

```csharp
// 获取网页截图作为纹理显示
private async Task<ImageTexture?> LoadWebScreenshot(string url)
{
    using var client = new HttpClient();
    // 调用截图 API（如 html2image、url2img 等服务）
    var imageBytes = await client.GetByteArrayAsync($"https://api.url2img.com?url={url}");
    var image = Image.LoadFromPng(imageBytes);
    return ImageTexture.CreateFromImage(image);
}
```

**缺点**：
- ❌ 不能交互
- ❌ 不能播放视频
- ❌ 只是静态图片

#### 2b. 强制外部浏览器 + 系统级控制（妥协方案）

```csharp
// 在外部浏览器打开，并尝试通过系统 API 暂停
private static void OpenInBrowserWithPause(string url)
{
    // 1. 在外部浏览器打开
    OpenInExternalBrowser(url);
    
    // 2. 检测并暂停系统媒体
    PauseSystemMedia();
}

// Windows: 使用 Windows Media API
// Linux: 使用 dbus MPRIS
// macOS: 使用 Accessibility API
```

---

## 🎯 推荐实施路线

### 路线A：完整内置 WebView（推荐但需要时间）

```
阶段1：编译 godot_wry (1-2小时)
  ↓
阶段2：C# P/Invoke 集成 (2-3小时)
  ↓
阶段3：UI 对接和调试 (1-2小时)
  ↓
阶段4：视频控制 JavaScript (1小时)
  ↓
总计：5-8小时
```

### 路线B：混合方案（快速可用）

```
阶段1：使用方案 2a（截图API）作为过渡 (1-2小时)
  ↓
阶段2：同时开发路线A的完整版
  ↓
阶段3：完成后替换为完整 WebView
```

---

## 📦 需要的资源

### 1. godot_wry 编译

需要你本地环境：
- Rust 工具链（rustup）
- 目标平台的构建工具：
  - Windows: Visual Studio Build Tools
  - Linux: GCC + GTK3 开发包
  - macOS: Xcode

**编译命令**：
```bash
# Linux 安装依赖
sudo apt install libwebkit2gtk-4.1-dev

# 编译
cargo build --release
```

### 2. 测试环境

- Slay the Spire 2 游戏（版本 0.105.0+）
- .NET 8.0 SDK

---

## ⚠️ 风险评估

| 风险 | 影响 | 缓解方案 |
|------|------|----------|
| godot_wry 与游戏冲突 | 高 | 先在干净环境测试 |
| 跨平台兼容性问题 | 中 | 分平台编译和测试 |
| 视频控制不通用 | 低 | 多种网站单独适配 |

---

## ✅ 下一步行动

**需要你确认：**

1. **是否愿意编译 godot_wry？**
   - 需要本地安装 Rust 和相关工具
   - 编译时间约 10-30 分钟

2. **选择哪条路线？**
   - 路线A：完整内置 WebView（时间长但功能完整）
   - 路线B：混合方案（快速可用但功能有限）

3. **测试环境？**
   - 你有 Slay the Spire 2 游戏吗？
   - 是什么操作系统？

---

**请回复你的选择，我可以开始实施方案！**
