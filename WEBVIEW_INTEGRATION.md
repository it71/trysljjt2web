# WebView 集成指南

## 📋 概述

本文档说明如何将 godot_wry WebView 集成到 STS2WebBrowser 模组中。

---

## 🏗️ 架构

```
┌─────────────────────────────────────────┐
│  WebOverlay.cs                          │
│  ┌───────────────────────────────────┐ │
│  │  UI 管理                          │ │
│  │  - 面板显示/隐藏                  │ │
│  │  - 导航控制                      │ │
│  │  - 事件处理                      │ │
│  └───────────┬───────────────────────┘ │
│              │                         │
│              │ 调用                    │
│              ▼                         │
│  ┌───────────────────────────────────┐ │
│  │  WebViewManager.cs                │ │
│  │  - WebView 生命周期管理           │ │
│  │  - JavaScript 执行                │ │
│  │  - 视频控制                      │ │
│  └───────────┬───────────────────────┘ │
│              │                         │
│              │ P/Invoke               │
│              ▼                         │
│  ┌───────────────────────────────────┐ │
│  │  godot_wry (Rust)                 │ │
│  │  - libgodot_wry.so/dll/dylib     │ │
│  └───────────┬───────────────────────┘ │
│              │                         │
│              ▼                         │
│  ┌───────────────────────────────────┐ │
│  │  系统 WebView                     │ │
│  │  Windows: WebView2 (Edge)        │ │
│  │  macOS: WebKit (Safari)          │ │
│  │  Linux: WebKitGTK                │ │
│  └───────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

---

## 📁 文件结构

```
STS2WebBrowser/
├── WebOverlay.cs          # 主模组代码
├── WebViewManager.cs      # WebView 管理器
├── WebViewIntegration.cs  # WebView 集成代码
├── VideoControl.js       # 视频控制脚本（加载到页面）
└── lib/
    ├── linux_x86_64/
    │   └── libgodot_wry.so
    ├── windows_x86_64/
    │   └── godot_wry.dll
    └── osx_universal/
        └── libgodot_wry.dylib
```

---

## 🔧 集成步骤

### 步骤 1: 添加 WebViewManager.cs

将 `WebViewManager.cs` 复制到项目中。

### 步骤 2: 修改 WebOverlay.cs

在 `BuildUI()` 函数中添加 WebView 初始化：

```csharp
private static WebViewManager? _webView;

private static CanvasLayer BuildUI()
{
    // ... 现有代码 ...
    
    // 初始化 WebView
    InitializeWebView();
    
    return _canvas;
}

private static void InitializeWebView()
{
    try
    {
        // 创建 WebView 管理器节点
        var webViewNode = new WebViewManager();
        
        // 添加到 Canvas
        _canvas.AddChild(webViewNode);
        
        // 计算 WebView 尺寸（留出标题栏和工具栏的空间）
        int webViewWidth = MAIN_PANEL_WIDTH - 40;
        int webViewHeight = MAIN_PANEL_HEIGHT - TITLE_HEIGHT - TOOLBAR_HEIGHT - 60;
        
        // 初始化 WebView
        if (webViewNode.Initialize(webViewWidth, webViewHeight))
        {
            _webView = webViewNode;
            
            // 加载默认页面
            _webView.LoadUrl(_lastUrl);
            
            Log.Info($"{LOG_PREFIX} WebView initialized successfully");
        }
        else
        {
            Log.Warn($"{LOG_PREFIX} WebView initialization failed");
        }
    }
    catch (Exception ex)
    {
        Log.Warn($"{LOG_PREFIX} WebView initialization error: {ex.Message}");
    }
}
```

### 步骤 3: 替换浏览器内容显示

修改 `CreateBrowserContent()` 函数：

```csharp
private static void CreateBrowserContent()
{
    if (_mainPanel == null) return;
    
    try
    {
        int contentY = TITLE_HEIGHT + TOOLBAR_HEIGHT;
        int contentHeight = MAIN_PANEL_HEIGHT - contentY - 40;
        int contentWidth = _compactView ? MAIN_PANEL_WIDTH - 320 : MAIN_PANEL_WIDTH - 20;
        
        // WebView 容器
        _browserContainer = new Control
        {
            Name = "BrowserContainer",
            Position = new Vector2I(10, contentY),
            Size = new Vector2I(contentWidth, contentHeight),
            MouseFilter = Godot.Control.MouseFilterEnum.Stop
        };
        _mainPanel.AddChild(_browserContainer);
        
        // WebView 将在这里被初始化
        // 初始化逻辑移到 InitializeWebView() 中
        
        // 如果 WebView 不可用，显示提示
        if (_webView == null)
        {
            _webContentPanel = new Panel
            {
                Position = Vector2I.Zero,
                Size = _browserContainer.Size
            };
            
            var webStyle = new StyleBoxFlat { BgColor = new Godot.Color(0.02f, 0.02f, 0.03f, 1f) };
            _webContentPanel.AddThemeStyleboxOverride("panel", webStyle);
            _browserContainer.AddChild(_webContentPanel);
            
            _statusLabel = new Label
            {
                Text = "⚠️ WebView not available\n\nPlease ensure godot_wry library is installed.",
                Position = new Vector2I(20, 20),
                Size = new Vector2I(contentWidth - 40, 100)
            };
            _webContentPanel.AddChild(_statusLabel);
        }
    }
    catch (Exception ex)
    {
        Log.Warn($"{LOG_PREFIX} CreateBrowserContent error: {ex.Message}");
    }
}
```

### 步骤 4: 实现视频控制

在悬浮图标最小化时暂停视频：

```csharp
private static void OnMinimizeClick()
{
    try
    {
        _isMinimized = true;
        
        // 暂停当前视频（如果正在播放）
        if (_webView != null)
        {
            _webView.SmartPauseVideo();
            _webView.SetVisible(false);
        }
        
        // 隐藏主面板
        if (_mainPanel != null)
            _mainPanel.Visible = false;
        
        // 显示悬浮图标
        if (_floatingIconBtn != null)
        {
            if (_mainPanel != null)
            {
                _floatingIconBtn.Position = new Vector2I(
                    _mainPanel.Position.X + 10,
                    _mainPanel.Position.Y + 10
                );
            }
            _floatingIconBtn.Visible = true;
        }
        
        Log.Info($"{LOG_PREFIX} Minimized to floating icon (video paused)");
    }
    catch (Exception ex)
    {
        Log.Warn($"{LOG_PREFIX} OnMinimizeClick error: {ex.Message}");
    }
}
```

### 步骤 5: 恢复时显示视频

```csharp
private static void OnFloatingIconClick()
{
    try
    {
        _isMinimized = false;
        
        // 隐藏悬浮图标
        if (_floatingIconBtn != null)
            _floatingIconBtn.Visible = false;
        
        // 显示 WebView
        if (_webView != null)
        {
            _webView.SetVisible(true);
        }
        
        // 显示主面板
        if (_mainPanel != null)
            _mainPanel.Visible = _panelVisible;
        
        Log.Info($"{LOG_PREFIX} Restored from floating icon");
    }
    catch (Exception ex)
    {
        Log.Warn($"{LOG_PREFIX} OnFloatingIconClick error: {ex.Message}");
    }
}
```

---

## 🎬 视频控制 JavaScript

### VideoControl.js

创建此文件用于高级视频控制：

```javascript
// VideoControl.js - 视频控制脚本
// 会被加载到 WebView 的页面中

(function() {
    'use strict';
    
    // 等待 DOM 加载完成
    function init() {
        // 监听页面上的 video 元素
        observeVideos();
        
        // 监听新增的 video 元素
        observeDOMChanges();
    }
    
    // 暂停所有视频
    function pauseAll() {
        document.querySelectorAll('video').forEach(function(video) {
            video.pause();
            video.muted = true; // 同时静音
        });
    }
    
    // 播放所有视频
    function playAll() {
        document.querySelectorAll('video').forEach(function(video) {
            video.play().catch(function(e) {
                console.log('Auto-play prevented:', e);
            });
        });
    }
    
    // 观察现有视频
    function observeVideos() {
        var videos = document.querySelectorAll('video');
        videos.forEach(function(video) {
            setupVideoListeners(video);
        });
    }
    
    // 监听 DOM 变化（用于动态加载的视频）
    function observeDOMChanges() {
        var observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeName === 'VIDEO') {
                        setupVideoListeners(node);
                    }
                    if (node.querySelectorAll) {
                        node.querySelectorAll('video').forEach(function(video) {
                            setupVideoListeners(video);
                        });
                    }
                });
            });
        });
        
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }
    
    // 为视频添加监听器
    function setupVideoListeners(video) {
        video.addEventListener('play', function() {
            console.log('Video started playing');
        });
        
        video.addEventListener('pause', function() {
            console.log('Video paused');
        });
    }
    
    // B站 特殊处理
    function handleBilibili() {
        if (window.location.hostname.includes('bilibili')) {
            // 尝试获取 B站播放器实例
            window.addEventListener('message', function(event) {
                if (event.data && event.data.type === 'player') {
                    // 处理播放器消息
                }
            });
            
            // 监听 player 组件
            var bilibiliPlayer = document.querySelector('.bilibili-player-video');
            if (bilibiliPlayer) {
                // 可以在这里添加 B站 特定的播放控制
            }
        }
    }
    
    // YouTube 特殊处理
    function handleYouTube() {
        if (window.location.hostname.includes('youtube')) {
            // YouTube 使用 IFrame API
            window.addEventListener('message', function(event) {
                if (event.origin.indexOf('youtube.com') !== -1) {
                    if (event.data && event.data.event === 'infoDelivery') {
                        // YouTube 播放器信息更新
                    }
                }
            });
        }
    }
    
    // 接收来自 Godot 的消息
    window.addEventListener('message', function(event) {
        try {
            var data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
            
            switch (data.type) {
                case 'pause':
                    pauseAll();
                    break;
                case 'play':
                    playAll();
                    break;
                case 'ping':
                    window.parent.postMessage(JSON.stringify({
                        type: 'pong',
                        domain: window.location.hostname
                    }), '*');
                    break;
            }
        } catch (e) {
            // 忽略解析错误
        }
    });
    
    // 页面加载完成后初始化
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
```

### 在 WebView 中加载脚本

```csharp
// 在 WebViewManager.cs 中添加方法
public void LoadVideoControlScript()
{
    // 加载视频控制脚本
    string script = ResourceLoader.Load("res://scripts/VideoControl.js").AsString();
    ExecuteJavaScript(script);
}
```

---

## 🔧 事件系统

### 游戏回合检测

当实现游戏回合检测后，可以在以下时机调用视频控制：

```csharp
// 检测到玩家回合开始
private static void OnPlayerTurnStart()
{
    // 自动暂停视频
    if (_webView != null)
    {
        _webView.SmartPauseVideo();
        _webView.SetVisible(false);
    }
    
    // 最小化到图标
    if (!_isMinimized)
    {
        OnMinimizeClick();
    }
}

// 检测到玩家回合结束（队友回合开始）
private static void OnPlayerTurnEnd()
{
    // 显示浏览器
    if (_isMinimized)
    {
        OnFloatingIconClick();
    }
    
    // WebView 已经可见了
}
```

---

## 📦 部署检查清单

在发布模组前，确保：

- [ ] godot_wry 库已为所有目标平台编译
- [ ] 库文件放在正确的 `lib/` 子目录中
- [ ] Windows DLL 依赖已安装（WebView2 通常系统自带）
- [ ] Linux WebKitGTK 依赖已告知用户
- [ ] macOS WebKit 正常工作（系统自带）

---

## ⚠️ 已知问题

1. **Linux 透明度**：godot_wry 在 Linux 上不支持透明度
2. **跨平台差异**：不同平台的 WebView 行为可能有细微差异
3. **视频自动播放**：大多数浏览器会阻止带声音的自动播放
4. **iframe 内容**：跨域 iframe 的视频控制可能无效

---

## 🔗 参考资料

- [godot_wry 官方文档](https://godot-wry.doceazedo.com/)
- [WRY 库文档](https://docs.rs/wry/)
- [WebView2 开发者文档](https://docs.microsoft.com/en-us/microsoft-edge/webview2/)
- [WebKitGTK 文档](https://webkitgtk.org/)

---

## ✅ 下一步

1. 编译 godot_wry 库（见 [WRY_BUILD_GUIDE.md](WRY_BUILD_GUIDE.md)）
2. 将库文件复制到项目的 `lib/` 目录
3. 修改 `WebOverlay.cs` 添加 WebView 初始化
4. 测试基本浏览功能
5. 测试视频暂停功能
6. 实现游戏回合检测（可选）
