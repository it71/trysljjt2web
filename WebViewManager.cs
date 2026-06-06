using Godot;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

/// <summary>
/// WebView 管理器 - 封装 godot_wry 的 P/Invoke 调用
/// 支持跨平台 WebView 渲染和 JavaScript 交互
/// </summary>
public class WebViewManager : Godot.Node
{
    #region P/Invoke 声明
    
    // 根据操作系统加载对应的库
    private static class NativeLib
    {
        private const string LIB_NAME = "godot_wry";
        
        static NativeLib()
        {
            // 设置库搜索路径
            string libPath = GetLibraryPath();
            if (!string.IsNullOrEmpty(libPath))
            {
                // 对于某些平台，可能需要设置 RPATH 或 LD_LIBRARY_PATH
                Log.Info($"[WebViewManager] Loading library from: {libPath}");
            }
        }
        
        private static string GetLibraryPath()
        {
            // 模组目录下的 lib 子目录
            string modDir = OS.GetExecutablePath().GetBaseDir();
            
            if (OS.IsUnixLike())
            {
                if (OS.IsMacOSX())
                    return modDir.Join("lib", "libgodot_wry.dylib");
                else
                    return modDir.Join("lib", "libgodot_wry.so");
            }
            else
            {
                return modDir.Join("lib", "godot_wry.dll");
            }
        }
    }
    
    #endregion
    
    #region WebView 句柄
    
    private IntPtr _webViewHandle = IntPtr.Zero;
    private bool _isInitialized = false;
    private int _width;
    private int _height;
    
    #endregion
    
    #region 信号定义
    
    [Signal]
    public delegate void PageLoadedEventHandler(string url);
    
    [Signal]
    public delegate void PageLoadFailedEventHandler(string error);
    
    [Signal]
    public delegate void ConsoleMessageEventHandler(string message, string level);
    
    [Signal]
    public delegate void IpcMessageEventHandler(string message);
    
    #endregion
    
    #region 生命周期
    
    public override void _Ready()
    {
        Log.Info("[WebViewManager] WebViewManager ready");
    }
    
    public override void _ExitTree()
    {
        Destroy();
    }
    
    #endregion
    
    #region 公共方法
    
    /// <summary>
    /// 初始化 WebView
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns>是否成功</returns>
    public bool Initialize(int width, int height)
    {
        try
        {
            _width = width;
            _height = height;
            
            // 尝试创建 WebView
            _webViewHandle = CreateWebView(width, height);
            
            if (_webViewHandle != IntPtr.Zero)
            {
                _isInitialized = true;
                Log.Info($"[WebViewManager] WebView initialized: {_width}x{_height}");
                return true;
            }
            else
            {
                Log.Warn("[WebViewManager] Failed to create WebView");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] Initialize error: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 加载 URL
    /// </summary>
    public void LoadUrl(string url)
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
        {
            Log.Warn("[WebViewManager] WebView not initialized");
            return;
        }
        
        try
        {
            // 确保 URL 以协议开头
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            
            LoadUrlInternal(_webViewHandle, url);
            Log.Info($"[WebViewManager] Loading URL: {url}");
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] LoadUrl error: {ex.Message}");
            EmitSignal(SignalName.PageLoadFailed, ex.Message);
        }
    }
    
    /// <summary>
    /// 执行 JavaScript
    /// </summary>
    public void ExecuteJavaScript(string script)
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
        {
            Log.Warn("[WebViewManager] WebView not initialized");
            return;
        }
        
        try
        {
            ExecuteJsInternal(_webViewHandle, script);
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] ExecuteJavaScript error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 刷新页面
    /// </summary>
    public void Reload()
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
            return;
        
        try
        {
            ReloadInternal(_webViewHandle);
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] Reload error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 后退
    /// </summary>
    public void GoBack()
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
            return;
        
        try
        {
            GoBackInternal(_webViewHandle);
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] GoBack error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 前进
    /// </summary>
    public void GoForward()
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
            return;
        
        try
        {
            GoForwardInternal(_webViewHandle);
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] GoForward error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 设置缩放级别
    /// </summary>
    public void SetZoom(float level)
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
            return;
        
        try
        {
            SetZoomInternal(_webViewHandle, level);
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] SetZoom error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 设置可见性
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
            return;
        
        try
        {
            SetVisibleInternal(_webViewHandle, visible);
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] SetVisible error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 销毁 WebView
    /// </summary>
    public void Destroy()
    {
        if (_webViewHandle != IntPtr.Zero)
        {
            try
            {
                DestroyInternal(_webViewHandle);
                _webViewHandle = IntPtr.Zero;
                _isInitialized = false;
                Log.Info("[WebViewManager] WebView destroyed");
            }
            catch (Exception ex)
            {
                Log.Warn($"[WebViewManager] Destroy error: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 获取 Texture 用于渲染
    /// </summary>
    public ImageTexture? GetTexture()
    {
        if (!_isInitialized || _webViewHandle == IntPtr.Zero)
            return null;
        
        try
        {
            // 获取 WebView 的图像数据
            IntPtr imageData = GetWebViewImage(_webViewHandle);
            if (imageData != IntPtr.Zero)
            {
                // 这里需要根据实际返回格式处理
                // 可能需要从原生代码返回图像数据并转换为 Godot Image
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[WebViewManager] GetTexture error: {ex.Message}");
        }
        
        return null;
    }
    
    #endregion
    
    #region 视频控制
    
    /// <summary>
    /// 暂停当前页面的所有视频
    /// </summary>
    public void PauseAllVideos()
    {
        string js = @"
            // 暂停所有 <video> 元素
            document.querySelectorAll('video').forEach(function(v) {
                v.pause();
                v.dispatchEvent(new Event('pause'));
            });
            
            // 尝试暂停 iframe 中的视频
            document.querySelectorAll('iframe').forEach(function(iframe) {
                try {
                    iframe.contentWindow.postMessage(JSON.stringify({
                        type: 'video-pause',
                        action: 'pause'
                    }), '*');
                } catch(e) {
                    // 跨域 iframe 可能无法访问
                }
            });
        ";
        
        ExecuteJavaScript(js);
    }
    
    /// <summary>
    /// 播放所有视频
    /// </summary>
    public void PlayAllVideos()
    {
        string js = @"
            document.querySelectorAll('video').forEach(function(v) {
                v.play().catch(function(e) {
                    // 自动播放可能被阻止
                    console.log('Auto-play prevented:', e);
                });
            });
        ";
        
        ExecuteJavaScript(js);
    }
    
    /// <summary>
    /// 暂停 B站视频
    /// </summary>
    public void PauseBilibiliVideo()
    {
        string js = @"
            // B站播放器 API
            if (window.bilibiliPlayer) {
                try {
                    window.bilibiliPlayer.pause();
                } catch(e) {}
            }
            
            // 直接暂停 video 元素
            var video = document.querySelector('video');
            if (video) {
                video.pause();
            }
            
            // 尝试调用 B站 的播放器方法
            try {
                var player = document.querySelector('.bilibili-player-video');
                if (player && player.pause) {
                    player.pause();
                }
            } catch(e) {}
        ";
        
        ExecuteJavaScript(js);
    }
    
    /// <summary>
    /// 暂停 YouTube 视频
    /// </summary>
    public void PauseYouTubeVideo()
    {
        string js = @"
            // YouTube IFrame API
            if (typeof YT !== 'undefined' && YT.Player) {
                var players = document.querySelectorAll('iframe');
                players.forEach(function(iframe) {
                    try {
                        if (iframe.src.indexOf('youtube.com') !== -1) {
                            // 通过 postMessage 控制
                            iframe.contentWindow.postMessage(JSON.stringify({
                                event: 'command',
                                func: 'pauseVideo'
                            }), '*');
                        }
                    } catch(e) {}
                });
            }
            
            // 直接暂停 video 元素
            var video = document.querySelector('video');
            if (video) {
                video.pause();
            }
        ";
        
        ExecuteJavaScript(js);
    }
    
    /// <summary>
    /// 智能暂停 - 检测网站类型并使用相应的暂停方法
    /// </summary>
    public void SmartPauseVideo()
    {
        // 检测当前域名
        string detectJs = @"
            (function() {
                var host = window.location.hostname;
                var result = {
                    domain: host,
                    type: 'generic'
                };
                
                if (host.includes('bilibili.com') || host.includes('bilibili.cn')) {
                    result.type = 'bilibili';
                } else if (host.includes('youtube.com') || host.includes('youtu.be')) {
                    result.type = 'youtube';
                } else if (host.includes('douyin.com')) {
                    result.type = 'douyin';
                } else if (host.includes('twitter.com') || host.includes('x.com')) {
                    result.type = 'twitter';
                } else if (host.includes('weibo.com')) {
                    result.type = 'weibo';
                }
                
                return result;
            })();
        ";
        
        // 由于无法直接获取返回值，我们分多次调用
        PauseAllVideos();
        
        // 针对已知视频网站尝试特殊处理
        string platformSpecificJs = @"
            (function() {
                var host = window.location.hostname;
                
                // B站
                if (host.includes('bilibili')) {
                    if (window.bilibiliPlayer) {
                        try { window.bilibiliPlayer.pause(); } catch(e) {}
                    }
                }
                
                // YouTube
                if (host.includes('youtube')) {
                    var video = document.querySelector('video');
                    if (video) video.pause();
                }
                
                // 抖音
                if (host.includes('douyin')) {
                    var video = document.querySelector('video');
                    if (video) video.pause();
                }
            })();
        ";
        
        ExecuteJavaScript(platformSpecificJs);
    }
    
    #endregion
    
    #region 原生方法声明 (需要 godot_wry 支持)
    
    // 这些方法需要在 godot_wry Rust 代码中实现对应的导出函数
    // 目前是占位符，实际调用会失败
    
    [DllImport("__Internal", EntryPoint = "godot_wry_create")]
    private static extern IntPtr CreateWebView(int width, int height);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_load_url")]
    private static extern void LoadUrlInternal(IntPtr handle, string url);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_execute_js")]
    private static extern void ExecuteJsInternal(IntPtr handle, string script);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_reload")]
    private static extern void ReloadInternal(IntPtr handle);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_go_back")]
    private static extern void GoBackInternal(IntPtr handle);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_go_forward")]
    private static extern void GoForwardInternal(IntPtr handle);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_set_zoom")]
    private static extern void SetZoomInternal(IntPtr handle, float level);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_set_visible")]
    private static extern void SetVisibleInternal(IntPtr handle, bool visible);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_destroy")]
    private static extern void DestroyInternal(IntPtr handle);
    
    [DllImport("__Internal", EntryPoint = "godot_wry_get_image")]
    private static extern IntPtr GetWebViewImage(IntPtr handle);
    
    #endregion
}

/// <summary>
/// 日志助手类（兼容层）
/// </summary>
internal static class Log
{
    public static void Info(string message)
    {
        Godot.GD.Print($"[INFO] {message}");
    }
    
    public static void Warn(string message)
    {
        Godot.GD.Print($"[WARN] {message}");
    }
    
    public static void Error(string message)
    {
        Godot.GD.Print($"[ERROR] {message}");
    }
}
