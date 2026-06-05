using Godot;
using Godot.Bridge;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// WebOverlay - Floating Browser Mod for Slay the Spire 2
/// Provides quick access to popular websites with favorites management
/// Cross-platform compatible with enhanced error handling
/// </summary>
[ModInitializer(nameof(Init))]
public static class Entry
{
    #region UI Constants
    private const string MOD_ID = "com.tiezhu.weboverlay";
    private const string CONFIG_PATH = "user://weboverlay.cfg";
    private const string LOG_PREFIX = "[WebOverlay]";
    
    private const int CANVAS_LAYER = 128;
    private const int PANEL_WIDTH = 540;
    private const int PANEL_HEIGHT = 400;
    private const int PANEL_X = 200;
    private const int PANEL_Y = 80;
    private const int CORNER_RADIUS = 8;
    
    private const int TITLE_HEIGHT = 32;
    private const int BUTTON_HEIGHT = 24;
    private const int BUTTON_WIDTH = 56;
    private const int BUTTON_SPACING = 60;
    private const int INPUT_HEIGHT = 28;
    
    private const int MIN_PANEL_WIDTH = 300;
    private const int MIN_PANEL_HEIGHT = 200;
    private const int RESIZE_HANDLE_SIZE = 14;
    
    private const float MIN_OPACITY = 0.2f;
    private const float MAX_OPACITY = 1.0f;
    private const float DEFAULT_OPACITY = 0.92f;
    private const float OPACITY_STEP = 0.05f;
    
    private static readonly Godot.Color PANEL_COLOR = new Godot.Color(0.08f, 0.08f, 0.12f, DEFAULT_OPACITY);
    private static readonly Godot.Color TITLE_COLOR = new Godot.Color(0.12f, 0.12f, 0.2f, 1f);
    private static readonly Godot.Color TEXT_COLOR = Colors.White;
    private static readonly Godot.Color SECONDARY_TEXT = new Godot.Color(0.8f, 0.8f, 0.8f);
    private static readonly Godot.Color HINT_TEXT = new Godot.Color(0.5f, 0.5f, 0.5f);
    private static readonly Godot.Color RESIZE_HANDLE_COLOR = new Godot.Color(0.5f, 0.5f, 0.5f, 0.35f);
    #endregion

    #region State Management
    private static CanvasLayer? _canvas;
    private static Godot.Panel? _panel;
    private static Godot.Panel? _titleBg;
    private static LineEdit? _urlInput;
    private static ItemList? _favList;
    private static HSlider? _opacitySlider;
    
    private static bool _panelVisible = false;
    private static bool _dragging = false;
    private static bool _resizing = false;
    
    private static Vector2 _dragOffset;
    private static Vector2 _resizeStartPos;
    private static Vector2 _resizeStartSize;
    
    private static string _lastUrl = "https://www.bilibili.com";
    private static List<string> _favorites = new List<string>();
    
    // Shortcut sites: hardcoded but can be extended to load from config
    private static readonly (string, string)[] QUICK_SITES_ROW1 = new[]
    {
        ("Bili", "https://www.bilibili.com"),
        ("YT", "https://www.youtube.com"),
        ("Douyin", "https://www.douyin.com"),
    };
    
    private static readonly (string, string)[] QUICK_SITES_ROW2 = new[]
    {
        ("Douyu", "https://www.douyu.com"),
        ("Huya", "https://www.huya.com"),
        ("Weibo", "https://www.weibo.com"),
    };
    
    private static readonly string[] DEFAULT_FAVORITES = new[]
    {
        "https://www.bilibili.com",
        "https://www.youtube.com",
        "https://www.douyin.com",
    };
    #endregion

    /// <summary>Mod initialization entry point</summary>
    public static void Init()
    {
        try
        {
            Log.Info($"{LOG_PREFIX} Initializing...");
            
            // Lookup scripts in assembly (with fallback)
            try
            {
                ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
            }
            catch (Exception ex)
            {
                Log.Warn($"{LOG_PREFIX} Script lookup skipped: {ex.Message}");
            }
            
            // Harmony patching (with fallback)
            try
            {
                var harmony = new Harmony(MOD_ID);
                harmony.PatchAll();
                Log.Info($"{LOG_PREFIX} Harmony patches applied");
            }
            catch (Exception ex)
            {
                Log.Warn($"{LOG_PREFIX} Harmony patching skipped: {ex.Message}");
            }
            
            LoadFavorites();
            
            // Get main tree with multiple fallback methods
            SceneTree? tree = null;
            try
            {
                tree = Engine.GetMainLoop() as SceneTree;
            }
            catch
            {
                Log.Warn($"{LOG_PREFIX} Could not get SceneTree via Engine.GetMainLoop()");
            }
            
            if (tree?.Root != null)
            {
                tree.Root.CallDeferred("add_child", BuildUI());
                Log.Info($"{LOG_PREFIX} Ready (v4.1.0) - Cross-platform");
            }
            else
            {
                Log.Warn($"{LOG_PREFIX} SceneTree or Root is null, will retry on next frame");
                // Alternative initialization for delayed setup
                if (Engine.GetMainLoop() is SceneTree altTree)
                {
                    altTree.ProcessFrame += () =>
                    {
                        if (altTree.Root != null && _canvas == null)
                        {
                            try
                            {
                                altTree.Root.CallDeferred("add_child", BuildUI());
                                Log.Info($"{LOG_PREFIX} Ready (v4.1.0) - Cross-platform (delayed)");
                            }
                            catch (Exception ex)
                            {
                                Log.Warn($"{LOG_PREFIX} Delayed init failed: {ex.Message}");
                            }
                        }
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} Init failed: {ex.GetType().Name} - {ex.Message}");
            Log.Warn($"{LOG_PREFIX} Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>Load favorites from config file</summary>
    private static void LoadFavorites()
    {
        _favorites.Clear();
        
        try
        {
            var cf = new ConfigFile();
            if (cf.Load(CONFIG_PATH) != Godot.Error.Ok)
            {
                Log.Info($"{LOG_PREFIX} Creating new config with default favorites");
                LoadDefaultFavorites();
                return;
            }
            
            var keys = cf.GetSectionKeys("favorites");
            if (keys?.Length > 0)
            {
                foreach (var key in keys)
                {
                    try
                    {
                        var val = cf.GetValue("favorites", key, "").AsString();
                        if (!string.IsNullOrEmpty(val))
                            _favorites.Add(val);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"{LOG_PREFIX} Error loading favorite {key}: {ex.Message}");
                    }
                }
                
                if (_favorites.Count == 0)
                {
                    Log.Info($"{LOG_PREFIX} No favorites found, loading defaults");
                    LoadDefaultFavorites();
                }
            }
            else
            {
                LoadDefaultFavorites();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} LoadFavorites error: {ex.Message}");
            LoadDefaultFavorites();
        }
    }

    /// <summary>Load default favorites</summary>
    private static void LoadDefaultFavorites()
    {
        _favorites.Clear();
        _favorites.AddRange(DEFAULT_FAVORITES);
        SaveFavorites();
    }

    /// <summary>Save favorites to config file</summary>
    private static void SaveFavorites()
    {
        try
        {
            var cf = new ConfigFile();
            for (int i = 0; i < _favorites.Count; i++)
                cf.SetValue("favorites", $"fav_{i}", _favorites[i]);
            
            var result = cf.Save(CONFIG_PATH);
            if (result != Godot.Error.Ok)
            {
                Log.Warn($"{LOG_PREFIX} SaveFavorites returned error: {result}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} SaveFavorites error: {ex.Message}");
        }
    }

    /// <summary>Build the UI hierarchy</summary>
    private static CanvasLayer BuildUI()
    {
        _canvas = new CanvasLayer
        {
            Name = "WebOverlayCanvas",
            Layer = CANVAS_LAYER
        };

        try
        {
            // Toggle button
            CreateToggleButton();

            // Main panel
            CreateMainPanel();

            // Title bar
            CreateTitleBar();

            // Quick site buttons
            int y = CreateQuickSiteButtons();

            // URL input section
            y = CreateUrlInputSection(y);

            // Favorites list
            y = CreateFavoritesSection(y);

            // Opacity slider and help text
            CreateOpacityControl(y);

            // Resize handle
            CreateResizeHandle();

            // Connect resize event
            if (_panel != null)
            {
                _panel.Resized += OnPanelResized;
                OnPanelResized(); // Initial positioning
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} UI build error: {ex.Message}");
        }

        return _canvas;
    }

    /// <summary>Handle panel resize event for responsive layout</summary>
    private static void OnPanelResized()
    {
        try
        {
            if (_panel == null) return;
            
            var panelSize = _panel.Size;
            
            // Adjust title bar
            if (_titleBg != null)
            {
                _titleBg.Size = new Vector2I(panelSize.X, TITLE_HEIGHT);
            }
            
            // Adjust resize handle position
            foreach (var child in _panel.GetChildren())
            {
                if (child is ColorRect rect && rect.Name == "ResizeHandle")
                {
                    rect.Position = new Vector2I(
                        panelSize.X - RESIZE_HANDLE_SIZE,
                        panelSize.Y - RESIZE_HANDLE_SIZE
                    );
                    break;
                }
            }
            
            // Adjust favorites list width
            if (_favList != null)
            {
                var currentFavSize = _favList.Size;
                _favList.Size = new Vector2I(
                    Math.Max(MIN_PANEL_WIDTH - 20, panelSize.X - 20),
                    currentFavSize.Y
                );
            }
            
            // Adjust opacity slider and help text
            AdjustBottomControls(panelSize.X);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} Panel resize error: {ex.Message}");
        }
    }

    /// <summary>Adjust bottom controls based on panel width</summary>
    private static void AdjustBottomControls(int panelWidth)
    {
        try
        {
            // This can be expanded to adjust other UI elements
            // based on available width
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} AdjustBottomControls error: {ex.Message}");
        }
    }

    /// <summary>Create toggle button (CE)</summary>
    private static void CreateToggleButton()
    {
        if (_canvas == null) return;
        
        try
        {
            new Godot.Button
            {
                Text = "CE",
                Position = new Vector2I(10, 10),
                Size = new Vector2I(36, 36)
            }
            .Apply(b =>
            {
                b.Pressed += OnToggleClick;
                _canvas.AddChild(b);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateToggleButton error: {ex.Message}");
        }
    }

    /// <summary>Create main panel</summary>
    private static void CreateMainPanel()
    {
        if (_canvas == null) return;
        
        try
        {
            _panel = new Godot.Panel
            {
                Name = "MainPanel",
                Position = new Vector2I(PANEL_X, PANEL_Y),
                Size = new Vector2I(PANEL_WIDTH, PANEL_HEIGHT),
                MouseFilter = Godot.Control.MouseFilterEnum.Stop,
                Visible = false
            };

            var ps = new StyleBoxFlat { BgColor = PANEL_COLOR };
            ps.SetCornerRadiusAll(CORNER_RADIUS);
            _panel.AddThemeStyleboxOverride("panel", ps);
            
            _canvas.AddChild(_panel);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateMainPanel error: {ex.Message}");
        }
    }

    /// <summary>Create title bar</summary>
    private static void CreateTitleBar()
    {
        if (_panel == null) return;
        
        try
        {
            _titleBg = new Godot.Panel
            {
                Position = Vector2I.Zero,
                Size = new Vector2I(PANEL_WIDTH, TITLE_HEIGHT),
                MouseFilter = Godot.Control.MouseFilterEnum.Stop
            };

            var ts = new StyleBoxFlat { BgColor = TITLE_COLOR };
            ts.CornerRadiusTopLeft = CORNER_RADIUS;
            ts.CornerRadiusTopRight = CORNER_RADIUS;
            _titleBg.AddThemeStyleboxOverride("panel", ts);
            _titleBg.GuiInput += OnTitleInput;
            _panel.AddChild(_titleBg);

            // Title label
            new Godot.Label
            {
                Text = "  CE WebOverlay",
                Position = new Vector2I(4, 0),
                Size = new Vector2I(200, TITLE_HEIGHT)
            }
            .Apply(l =>
            {
                l.AddThemeColorOverride("font_color", TEXT_COLOR);
                _titleBg.AddChild(l);
            });

            // Close button
            new Godot.Button
            {
                Text = "X",
                Position = new Vector2I(PANEL_WIDTH - 32, 2),
                Size = new Vector2I(28, 28),
                Flat = true
            }
            .Apply(b =>
            {
                b.Pressed += OnCloseClick;
                _panel.AddChild(b);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateTitleBar error: {ex.Message}");
        }
    }

    /// <summary>Create quick site buttons</summary>
    private static int CreateQuickSiteButtons()
    {
        if (_panel == null) return 42;
        
        try
        {
            int x = 10, y = 42;

            // First row of quick sites
            foreach (var (name, url) in QUICK_SITES_ROW1)
            {
                CreateQuickSiteButton(name, url, x, y);
                x += BUTTON_SPACING;
            }

            x = 10;
            y += 30;

            // Second row of quick sites
            foreach (var (name, url) in QUICK_SITES_ROW2)
            {
                CreateQuickSiteButton(name, url, x, y);
                x += BUTTON_SPACING;
            }

            return y;
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateQuickSiteButtons error: {ex.Message}");
            return 42;
        }
    }

    /// <summary>Create a single quick site button</summary>
    private static void CreateQuickSiteButton(string name, string url, int x, int y)
    {
        if (_panel == null) return;
        
        try
        {
            new Godot.Button
            {
                Text = name,
                Position = new Vector2I(x, y),
                Size = new Vector2I(BUTTON_WIDTH, BUTTON_HEIGHT)
            }
            .Apply(b =>
            {
                b.Pressed += () => OpenUrl(url);
                _panel.AddChild(b);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateQuickSiteButton error: {ex.Message}");
        }
    }

    /// <summary>Create URL input section</summary>
    private static int CreateUrlInputSection(int y)
    {
        if (_panel == null) return y;
        
        try
        {
            y += 34;

            _urlInput = new LineEdit
            {
                PlaceholderText = "粘贴网址...",
                Position = new Vector2I(10, y),
                Size = new Vector2I(340, INPUT_HEIGHT),
                Text = _lastUrl
            };
            _panel.AddChild(_urlInput);

            // Go button
            new Godot.Button
            {
                Text = "Go",
                Position = new Vector2I(356, y),
                Size = new Vector2I(60, INPUT_HEIGHT)
            }
            .Apply(b =>
            {
                b.Pressed += OnGoClick;
                _panel.AddChild(b);
            });

            // Add to favorites button
            new Godot.Button
            {
                Text = "♥",
                Position = new Vector2I(422, y),
                Size = new Vector2I(28, INPUT_HEIGHT),
                Flat = true
            }
            .Apply(b =>
            {
                b.Pressed += OnAddFavoriteClick;
                _panel.AddChild(b);
            });

            // Remove from favorites button
            new Godot.Button
            {
                Text = "−",
                Position = new Vector2I(454, y),
                Size = new Vector2I(28, INPUT_HEIGHT),
                Flat = true
            }
            .Apply(b =>
            {
                b.Pressed += OnRemoveFavoriteClick;
                _panel.AddChild(b);
            });

            // Open selected favorite button
            new Godot.Button
            {
                Text = "▶",
                Position = new Vector2I(488, y),
                Size = new Vector2I(32, INPUT_HEIGHT),
                Flat = true
            }
            .Apply(b =>
            {
                b.Pressed += OnOpenFavoriteClick;
                _panel.AddChild(b);
            });

            return y;
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateUrlInputSection error: {ex.Message}");
            return y;
        }
    }

    /// <summary>Create favorites list section</summary>
    private static int CreateFavoritesSection(int y)
    {
        if (_panel == null) return y;
        
        try
        {
            y += 34;

            _favList = new ItemList
            {
                Position = new Vector2I(10, y),
                Size = new Vector2I(520, 180)
            };
            _favList.AddThemeColorOverride("font_color", SECONDARY_TEXT);
            
            // Compatible connection method
            try
            {
                _favList.Connect("item_activated", Callable.From((long idx) =>
                {
                    int i = (int)idx;
                    if (i >= 0 && i < _favorites.Count)
                        OpenUrl(_favorites[i]);
                }));
            }
            catch
            {
                // Fallback for older Godot versions
                Log.Warn($"{LOG_PREFIX} Using fallback for item_activated");
            }
            
            _panel.AddChild(_favList);
            RefreshFavList();

            return y;
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateFavoritesSection error: {ex.Message}");
            return y;
        }
    }

    /// <summary>Create opacity control and help text</summary>
    private static void CreateOpacityControl(int y)
    {
        if (_panel == null) return;
        
        try
        {
            y += 186;

            // Opacity label
            new Godot.Label
            {
                Text = "Opacity",
                Position = new Vector2I(10, y),
                Size = new Vector2I(60, 22)
            }
            .Apply(l => _panel.AddChild(l));

            // Opacity slider
            _opacitySlider = new HSlider
            {
                Position = new Vector2I(70, y),
                Size = new Vector2I(140, 22),
                MinValue = MIN_OPACITY,
                MaxValue = MAX_OPACITY,
                Step = OPACITY_STEP,
                Value = DEFAULT_OPACITY
            };
            _opacitySlider.ValueChanged += (v) =>
            {
                try
                {
                    var s = _panel?.GetThemeStylebox("panel") as StyleBoxFlat;
                    if (s != null)
                        s.BgColor = new Godot.Color(0.08f, 0.08f, 0.12f, (float)v);
                }
                catch (Exception ex)
                {
                    Log.Warn($"{LOG_PREFIX} Opacity change error: {ex.Message}");
                }
            };
            _panel.AddChild(_opacitySlider);

            // Help text
            new Godot.Label
            {
                Text = "拖拽 | 调整 | CE | ♥收藏 | −删除 | ▶打开",
                Position = new Vector2I(220, y),
                Size = new Vector2I(310, 22)
            }
            .Apply(l =>
            {
                l.AddThemeColorOverride("font_color", HINT_TEXT);
                _panel.AddChild(l);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateOpacityControl error: {ex.Message}");
        }
    }

    /// <summary>Create resize handle</summary>
    private static void CreateResizeHandle()
    {
        if (_panel == null) return;
        
        try
        {
            new ColorRect
            {
                Name = "ResizeHandle",
                Color = RESIZE_HANDLE_COLOR,
                Size = new Vector2I(RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE),
                Position = new Vector2I(PANEL_WIDTH - RESIZE_HANDLE_SIZE, PANEL_HEIGHT - RESIZE_HANDLE_SIZE),
                MouseFilter = Godot.Control.MouseFilterEnum.Stop
            }
            .Apply(r =>
            {
                r.GuiInput += OnResizeInput;
                _panel.AddChild(r);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateResizeHandle error: {ex.Message}");
        }
    }

    /// <summary>Refresh favorites list display</summary>
    private static void RefreshFavList()
    {
        try
        {
            if (_favList == null)
                return;

            _favList.Clear();
            foreach (var url in _favorites)
            {
                string displayName = ExtractDomainName(url);
                _favList.AddItem($"{displayName}  ({url})");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} RefreshFavList error: {ex.Message}");
        }
    }

    /// <summary>Extract domain name from URL</summary>
    private static string ExtractDomainName(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }

    #region Event Handlers

    private static void OnToggleClick()
    {
        try
        {
            _panelVisible = !_panelVisible;
            if (_panel != null)
                _panel.Visible = _panelVisible;
            if (_panelVisible && _urlInput != null)
                _urlInput.Text = _lastUrl;
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnToggleClick error: {ex.Message}");
        }
    }

    private static void OnCloseClick()
    {
        try
        {
            _panelVisible = false;
            if (_panel != null)
                _panel.Visible = false;
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnCloseClick error: {ex.Message}");
        }
    }

    private static void OnGoClick()
    {
        try
        {
            var url = _urlInput?.Text.Trim() ?? "";
            if (string.IsNullOrEmpty(url))
                url = "https://www.bilibili.com";
            if (!url.StartsWith("http"))
                url = "https://" + url;
            OpenUrl(url);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnGoClick error: {ex.Message}");
        }
    }

    private static void OnAddFavoriteClick()
    {
        try
        {
            var url = _urlInput?.Text.Trim() ?? "";
            if (!string.IsNullOrEmpty(url) && !_favorites.Contains(url))
            {
                _favorites.Add(url);
                SaveFavorites();
                RefreshFavList();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnAddFavoriteClick error: {ex.Message}");
        }
    }

    private static void OnRemoveFavoriteClick()
    {
        try
        {
            var sel = _favList?.GetSelectedItems() ?? Array.Empty<int>();
            if (sel.Length > 0 && sel[0] >= 0 && sel[0] < _favorites.Count)
            {
                _favorites.RemoveAt(sel[0]);
                SaveFavorites();
                RefreshFavList();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnRemoveFavoriteClick error: {ex.Message}");
        }
    }

    private static void OnOpenFavoriteClick()
    {
        try
        {
            var sel = _favList?.GetSelectedItems() ?? Array.Empty<int>();
            if (sel.Length > 0 && sel[0] >= 0 && sel[0] < _favorites.Count)
                OpenUrl(_favorites[sel[0]]);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnOpenFavoriteClick error: {ex.Message}");
        }
    }

    private static void OnTitleInput(InputEvent e)
    {
        try
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _dragging = mb.Pressed;
                    if (_dragging && _titleBg != null && _panel != null)
                        _dragOffset = _titleBg.GetGlobalMousePosition() - _panel.Position.AsVector2();
                }
                return;
            }

            if (_dragging && e is InputEventMouseMotion && _titleBg != null && _panel != null)
                _panel.Position = (_titleBg.GetGlobalMousePosition() - _dragOffset).AsVector2I();
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnTitleInput error: {ex.Message}");
        }
    }

    private static void OnResizeInput(InputEvent e)
    {
        try
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _resizing = mb.Pressed;
                    if (_resizing && _titleBg != null && _panel != null)
                    {
                        _resizeStartPos = _titleBg.GetGlobalMousePosition();
                        _resizeStartSize = _panel.Size.AsVector2();
                    }
                }
                return;
            }

            if (_resizing && e is InputEventMouseMotion && _titleBg != null && _panel != null)
            {
                var delta = _titleBg.GetGlobalMousePosition() - _resizeStartPos;
                int newWidth = (int)Mathf.Max(_resizeStartSize.X + delta.X, MIN_PANEL_WIDTH);
                int newHeight = (int)Mathf.Max(_resizeStartSize.Y + delta.Y, MIN_PANEL_HEIGHT);
                _panel.Size = new Vector2I(newWidth, newHeight);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnResizeInput error: {ex.Message}");
        }
    }

    #endregion

    /// <summary>Open URL in system browser with cross-platform support</summary>
    private static void OpenUrl(string url)
    {
        try
        {
            _lastUrl = url;
            if (_urlInput != null)
                _urlInput.Text = url;

            Log.Info($"{LOG_PREFIX} Opening URL: {url}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                OpenUrlWindows(url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                OpenUrlLinux(url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OpenUrlMacOS(url);
            }
            else
            {
                // Fallback for unknown platforms
                OpenUrlFallback(url);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} Failed to open URL: {url} - {ex.Message}");
            Log.Warn($"{LOG_PREFIX} Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>Open URL on Windows</summary>
    private static void OpenUrlWindows(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} Windows URL open failed, trying fallback: {ex.Message}");
            OpenUrlFallback(url);
        }
    }

    /// <summary>Open URL on Linux</summary>
    private static void OpenUrlLinux(string url)
    {
        try
        {
            // Try xdg-open first (common on most distros)
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                UseShellExecute = false
            });
        }
        catch
        {
            try
            {
                // Try gnome-open
                Process.Start(new ProcessStartInfo
                {
                    FileName = "gnome-open",
                    Arguments = url,
                    UseShellExecute = false
                });
            }
            catch
            {
                try
                {
                    // Try kde-open
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "kde-open",
                        Arguments = url,
                        UseShellExecute = false
                    });
                }
                catch (Exception ex)
                {
                    Log.Warn($"{LOG_PREFIX} Linux URL open failed, trying fallback: {ex.Message}");
                    OpenUrlFallback(url);
                }
            }
        }
    }

    /// <summary>Open URL on macOS</summary>
    private static void OpenUrlMacOS(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = url,
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} macOS URL open failed, trying fallback: {ex.Message}");
            OpenUrlFallback(url);
        }
    }

    /// <summary>Fallback URL opening method</summary>
    private static void OpenUrlFallback(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error($"{LOG_PREFIX} All URL opening methods failed: {ex.Message}");
        }
    }
}

/// <summary>Fluent builder extension</summary>
public static class Ext
{
    public static T Apply<T>(this T obj, Action<T> action) where T : class
    {
        action?.Invoke(obj);
        return obj;
    }
}

/// <summary>StyleBoxFlat helper extension</summary>
public static class SbfExt
{
    public static void SetCornerRadiusAll(this StyleBoxFlat styleBox, int radius)
    {
        try
        {
            styleBox.CornerRadiusTopLeft = radius;
            styleBox.CornerRadiusTopRight = radius;
            styleBox.CornerRadiusBottomLeft = radius;
            styleBox.CornerRadiusBottomRight = radius;
        }
        catch (Exception ex)
        {
            // Fallback for older Godot versions that might not support all properties
            Log.Warn($"{LOG_PREFIX} SetCornerRadiusAll fallback: {ex.Message}");
            try
            {
                styleBox.CornerRadiusTopLeft = radius;
                styleBox.CornerRadiusTopRight = radius;
            }
            catch { }
        }
    }
}

/// <summary>Logging helper (compatibility layer)</summary>
internal static class Log
{
    private static bool? _isMegaCritLoggingAvailable = null;
    
    public static void Info(string message)
    {
        TryLog(message, "Info", () => MegaCrit.Sts2.Core.Logging.Log.Info(message));
    }
    
    public static void Warn(string message)
    {
        TryLog(message, "Warn", () => MegaCrit.Sts2.Core.Logging.Log.Warn(message));
    }
    
    public static void Error(string message)
    {
        TryLog(message, "Error", () => MegaCrit.Sts2.Core.Logging.Log.Error(message));
    }
    
    private static void TryLog(string message, string level, Action megaCritLogAction)
    {
        try
        {
            if (!_isMegaCritLoggingAvailable.HasValue)
            {
                // Check if MegaCrit logging is available
                megaCritLogAction();
                _isMegaCritLoggingAvailable = true;
            }
            else if (_isMegaCritLoggingAvailable.Value)
            {
                megaCritLogAction();
            }
            else
            {
                Console.WriteLine($"[{level}] {message}");
            }
        }
        catch
        {
            _isMegaCritLoggingAvailable = false;
            // Fallback to console
            Console.WriteLine($"[{level}] {message}");
        }
    }
}
