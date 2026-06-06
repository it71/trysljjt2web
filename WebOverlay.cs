using Godot;
using Godot.Bridge;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// STS2WebBrowser - In-Game Browser for Slay the Spire 2
/// Built-in browser with favorites management and cross-platform support
/// </summary>
[ModInitializer(nameof(Init))]
public static class Entry
{
    #region UI Constants
    private const string MOD_ID = "com.sts2webbrowser";
    private const string CONFIG_PATH = "user://sts2webbrowser.cfg";
    private const string LOG_PREFIX = "[STS2WebBrowser]";
    
    private const int CANVAS_LAYER = 128;
    private const int MAIN_PANEL_WIDTH = 1100;
    private const int MAIN_PANEL_HEIGHT = 700;
    private const int MAIN_PANEL_X = 150;
    private const int MAIN_PANEL_Y = 50;
    private const int CORNER_RADIUS = 8;
    
    private const int TITLE_HEIGHT = 32;
    private const int TOOLBAR_HEIGHT = 40;
    private const int BUTTON_HEIGHT = 28;
    private const int BUTTON_WIDTH = 36;
    private const int BUTTON_SPACING = 8;
    private const int INPUT_HEIGHT = 28;
    
    private const int MIN_PANEL_WIDTH = 600;
    private const int MIN_PANEL_HEIGHT = 400;
    private const int RESIZE_HANDLE_SIZE = 16;
    
    private const float MIN_OPACITY = 0.3f;
    private const float MAX_OPACITY = 1.0f;
    private const float DEFAULT_OPACITY = 0.95f;
    private const float OPACITY_STEP = 0.05f;
    
    private static readonly Godot.Color PANEL_COLOR = new Godot.Color(0.05f, 0.05f, 0.08f, DEFAULT_OPACITY);
    private static readonly Godot.Color TITLE_COLOR = new Godot.Color(0.10f, 0.10f, 0.16f, 1f);
    private static readonly Godot.Color TOOLBAR_COLOR = new Godot.Color(0.08f, 0.08f, 0.12f, 1f);
    private static readonly Godot.Color TEXT_COLOR = Colors.White;
    private static readonly Godot.Color SECONDARY_TEXT = new Godot.Color(0.85f, 0.85f, 0.85f);
    private static readonly Godot.Color HINT_TEXT = new Godot.Color(0.55f, 0.55f, 0.55f);
    private static readonly Godot.Color RESIZE_HANDLE_COLOR = new Godot.Color(0.6f, 0.6f, 0.6f, 0.4f);
    #endregion

    #region State Management
    private static CanvasLayer? _canvas;
    private static Godot.Panel? _mainPanel;
    private static Godot.Panel? _titleBg;
    private static Godot.Panel? _toolbarBg;
    private static LineEdit? _urlInput;
    private static ItemList? _favList;
    private static HSlider? _opacitySlider;
    private static Control? _browserContainer;
    private static Panel? _webContentPanel;
    private static Label? _statusLabel;
    
    // Navigation controls
    private static Button? _backBtn;
    private static Button? _forwardBtn;
    private static Button? _refreshBtn;
    private static Button? _homeBtn;
    private static Button? _externalBtn;
    private static Button? _toggleViewBtn;
    
    // History
    private static List<string> _history = new List<string>();
    private static int _historyIndex = -1;
    
    private static bool _panelVisible = false;
    private static bool _dragging = false;
    private static bool _resizing = false;
    private static bool _compactView = false;
    
    private static Vector2 _dragOffset;
    private static Vector2 _resizeStartPos;
    private static Vector2 _resizeStartSize;
    
    private static string _lastUrl = "https://www.bilibili.com";
    private static List<string> _favorites = new List<string>();
    
    // Shortcut sites
    private static readonly (string, string)[] QUICK_SITES = new[]
    {
        ("Bili", "https://www.bilibili.com"),
        ("YT", "https://www.youtube.com"),
        ("Douyin", "https://www.douyin.com"),
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

    #region Initialization
    /// <summary>Mod initialization entry point</summary>
    public static void Init()
    {
        try
        {
            Log.Info($"{LOG_PREFIX} Initializing built-in browser...");
            
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
                Log.Info($"{LOG_PREFIX} Ready (v5.0.0) - Built-in Browser");
            }
            else
            {
                Log.Warn($"{LOG_PREFIX} SceneTree or Root is null, will retry on next frame");
                if (Engine.GetMainLoop() is SceneTree altTree)
                {
                    altTree.ProcessFrame += () =>
                    {
                        if (altTree.Root != null && _canvas == null)
                        {
                            try
                            {
                                altTree.Root.CallDeferred("add_child", BuildUI());
                                Log.Info($"{LOG_PREFIX} Ready (v5.0.0) - Built-in Browser (delayed)");
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
    #endregion

    #region Configuration
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
    #endregion

    #region UI Construction
    /// <summary>Build the UI hierarchy</summary>
    private static CanvasLayer BuildUI()
    {
        _canvas = new CanvasLayer
        {
            Name = "STS2WebBrowserCanvas",
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

            // Toolbar
            CreateToolbar();

            // Browser content area
            CreateBrowserContent();

            // Compact view (favorites panel)
            CreateCompactView();

            // Resize handle
            CreateResizeHandle();

            // Connect resize event
            if (_mainPanel != null)
            {
                _mainPanel.Resized += OnPanelResized;
                OnPanelResized(); // Initial positioning
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} UI build error: {ex.Message}");
        }

        return _canvas;
    }

    /// <summary>Create toggle button</summary>
    private static void CreateToggleButton()
    {
        if (_canvas == null) return;
        
        try
        {
            new Godot.Button
            {
                Text = "🌐",
                Position = new Vector2I(10, 10),
                Size = new Vector2I(40, 40),
                TooltipText = "Open Web Browser"
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
            _mainPanel = new Godot.Panel
            {
                Name = "MainBrowserPanel",
                Position = new Vector2I(MAIN_PANEL_X, MAIN_PANEL_Y),
                Size = new Vector2I(MAIN_PANEL_WIDTH, MAIN_PANEL_HEIGHT),
                MouseFilter = Godot.Control.MouseFilterEnum.Stop,
                Visible = false
            };

            var ps = new StyleBoxFlat { BgColor = PANEL_COLOR };
            ps.SetCornerRadiusAll(CORNER_RADIUS);
            _mainPanel.AddThemeStyleboxOverride("panel", ps);
            
            _canvas.AddChild(_mainPanel);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateMainPanel error: {ex.Message}");
        }
    }

    /// <summary>Create title bar</summary>
    private static void CreateTitleBar()
    {
        if (_mainPanel == null) return;
        
        try
        {
            _titleBg = new Godot.Panel
            {
                Position = Vector2I.Zero,
                Size = new Vector2I(MAIN_PANEL_WIDTH, TITLE_HEIGHT),
                MouseFilter = Godot.Control.MouseFilterEnum.Stop
            };

            var ts = new StyleBoxFlat { BgColor = TITLE_COLOR };
            ts.CornerRadiusTopLeft = CORNER_RADIUS;
            ts.CornerRadiusTopRight = CORNER_RADIUS;
            _titleBg.AddThemeStyleboxOverride("panel", ts);
            _titleBg.GuiInput += OnTitleInput;
            _mainPanel.AddChild(_titleBg);

            // Title label
            new Godot.Label
            {
                Text = "  🚀 CE Built-in Browser",
                Position = new Vector2I(4, 0),
                Size = new Vector2I(300, TITLE_HEIGHT)
            }
            .Apply(l =>
            {
                l.AddThemeColorOverride("font_color", TEXT_COLOR);
                _titleBg.AddChild(l);
            });

            // Toggle view button
            _toggleViewBtn = new Godot.Button
            {
                Text = "📑",
                Position = new Vector2I(MAIN_PANEL_WIDTH - 120, 2),
                Size = new Vector2I(28, 28),
                Flat = true,
                TooltipText = "Toggle Favorites Panel"
            };
            _toggleViewBtn.Pressed += OnToggleViewClick;
            _mainPanel.AddChild(_toggleViewBtn);

            // External browser button
            _externalBtn = new Godot.Button
            {
                Text = "🔗",
                Position = new Vector2I(MAIN_PANEL_WIDTH - 88, 2),
                Size = new Vector2I(28, 28),
                Flat = true,
                TooltipText = "Open in External Browser"
            };
            _externalBtn.Pressed += OnOpenExternalClick;
            _mainPanel.AddChild(_externalBtn);

            // Close button
            new Godot.Button
            {
                Text = "✕",
                Position = new Vector2I(MAIN_PANEL_WIDTH - 56, 2),
                Size = new Vector2I(28, 28),
                Flat = true,
                TooltipText = "Close Browser"
            }
            .Apply(b =>
            {
                b.Pressed += OnCloseClick;
                _mainPanel.AddChild(b);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateTitleBar error: {ex.Message}");
        }
    }

    /// <summary>Create toolbar with navigation controls</summary>
    private static void CreateToolbar()
    {
        if (_mainPanel == null) return;
        
        try
        {
            _toolbarBg = new Godot.Panel
            {
                Position = new Vector2I(0, TITLE_HEIGHT),
                Size = new Vector2I(MAIN_PANEL_WIDTH, TOOLBAR_HEIGHT),
                MouseFilter = Godot.Control.MouseFilterEnum.Ignore
            };

            var tbStyle = new StyleBoxFlat { BgColor = TOOLBAR_COLOR };
            _toolbarBg.AddThemeStyleboxOverride("panel", tbStyle);
            _mainPanel.AddChild(_toolbarBg);

            int x = 10;
            
            // Back button
            _backBtn = new Godot.Button
            {
                Text = "◀",
                Position = new Vector2I(x, 6),
                Size = new Vector2I(BUTTON_WIDTH, BUTTON_HEIGHT),
                Disabled = true,
                TooltipText = "Back"
            };
            _backBtn.Pressed += OnBackClick;
            _mainPanel.AddChild(_backBtn);
            x += BUTTON_WIDTH + BUTTON_SPACING;

            // Forward button
            _forwardBtn = new Godot.Button
            {
                Text = "▶",
                Position = new Vector2I(x, 6),
                Size = new Vector2I(BUTTON_WIDTH, BUTTON_HEIGHT),
                Disabled = true,
                TooltipText = "Forward"
            };
            _forwardBtn.Pressed += OnForwardClick;
            _mainPanel.AddChild(_forwardBtn);
            x += BUTTON_WIDTH + BUTTON_SPACING;

            // Refresh button
            _refreshBtn = new Godot.Button
            {
                Text = "🔄",
                Position = new Vector2I(x, 6),
                Size = new Vector2I(BUTTON_WIDTH, BUTTON_HEIGHT),
                TooltipText = "Refresh"
            };
            _refreshBtn.Pressed += OnRefreshClick;
            _mainPanel.AddChild(_refreshBtn);
            x += BUTTON_WIDTH + BUTTON_SPACING;

            // Home button
            _homeBtn = new Godot.Button
            {
                Text = "🏠",
                Position = new Vector2I(x, 6),
                Size = new Vector2I(BUTTON_WIDTH, BUTTON_HEIGHT),
                TooltipText = "Go Home"
            };
            _homeBtn.Pressed += OnHomeClick;
            _mainPanel.AddChild(_homeBtn);
            x += BUTTON_WIDTH + BUTTON_SPACING + 10;

            // URL Input
            int urlWidth = _compactView ? 450 : 680;
            _urlInput = new LineEdit
            {
                PlaceholderText = "Enter URL...",
                Position = new Vector2I(x, 6),
                Size = new Vector2I(urlWidth, INPUT_HEIGHT),
                Text = _lastUrl
            };
            _urlInput.TextSubmitted += OnUrlSubmitted;
            _mainPanel.AddChild(_urlInput);
            x += urlWidth + BUTTON_SPACING;

            // Go button
            var goBtn = new Godot.Button
            {
                Text = "Go",
                Position = new Vector2I(x, 6),
                Size = new Vector2I(50, BUTTON_HEIGHT)
            };
            goBtn.Pressed += OnGoClick;
            _mainPanel.AddChild(goBtn);
            x += 58;

            // Add to favorites button
            var favBtn = new Godot.Button
            {
                Text = "♥",
                Position = new Vector2I(x, 6),
                Size = new Vector2I(32, BUTTON_HEIGHT),
                Flat = true,
                TooltipText = "Add to Favorites"
            };
            favBtn.Pressed += OnAddFavoriteClick;
            _mainPanel.AddChild(favBtn);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateToolbar error: {ex.Message}");
        }
    }

    /// <summary>Create browser content area</summary>
    private static void CreateBrowserContent()
    {
        if (_mainPanel == null) return;
        
        try
        {
            int contentY = TITLE_HEIGHT + TOOLBAR_HEIGHT;
            int contentHeight = MAIN_PANEL_HEIGHT - contentY - 40;
            int contentWidth = _compactView ? MAIN_PANEL_WIDTH - 320 : MAIN_PANEL_WIDTH - 20;

            // Browser container
            _browserContainer = new Control
            {
                Name = "BrowserContainer",
                Position = new Vector2I(10, contentY),
                Size = new Vector2I(contentWidth, contentHeight),
                MouseFilter = Godot.Control.MouseFilterEnum.Stop
            };
            _mainPanel.AddChild(_browserContainer);

            // Web content panel (placeholder for actual web view)
            _webContentPanel = new Panel
            {
                Name = "WebContent",
                Position = Vector2I.Zero,
                Size = _browserContainer.Size
            };
            
            var webStyle = new StyleBoxFlat { BgColor = new Godot.Color(0.02f, 0.02f, 0.03f, 1f) };
            _webContentPanel.AddThemeStyleboxOverride("panel", webStyle);
            _browserContainer.AddChild(_webContentPanel);

            // Status/Info label
            _statusLabel = new Label
            {
                Text = "💡 Click \"🔗\" to open in external browser\n\nFor built-in web rendering, Godot 4's WebView would be used.\nThis mod provides the complete browser UI framework.",
                Position = new Vector2I(20, 20),
                Size = new Vector2I(contentWidth - 40, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusLabel.AddThemeColorOverride("font_color", SECONDARY_TEXT);
            _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            _webContentPanel.AddChild(_statusLabel);

            // Quick site buttons in browser area
            CreateQuickSiteButtons(contentWidth);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateBrowserContent error: {ex.Message}");
        }
    }

    /// <summary>Create quick site buttons</summary>
    private static void CreateQuickSiteButtons(int containerWidth)
    {
        if (_webContentPanel == null) return;
        
        try
        {
            int startY = 150;
            int btnWidth = 140;
            int btnHeight = 50;
            int spacing = 15;
            int cols = 4;
            
            for (int i = 0; i < QUICK_SITES.Length; i++)
            {
                var (name, url) = QUICK_SITES[i];
                int row = i / cols;
                int col = i % cols;
                int x = (containerWidth - (cols * btnWidth + (cols - 1) * spacing)) / 2 + col * (btnWidth + spacing);
                int y = startY + row * (btnHeight + spacing);

                var btn = new Button
                {
                    Text = name,
                    Position = new Vector2I(x, y),
                    Size = new Vector2I(btnWidth, btnHeight)
                };
                var siteUrl = url; // Capture for closure
                btn.Pressed += () => NavigateTo(siteUrl);
                _webContentPanel.AddChild(btn);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateQuickSiteButtons error: {ex.Message}");
        }
    }

    /// <summary>Create compact view (favorites panel)</summary>
    private static void CreateCompactView()
    {
        if (_mainPanel == null) return;
        
        try
        {
            int contentY = TITLE_HEIGHT + TOOLBAR_HEIGHT;
            int contentHeight = MAIN_PANEL_HEIGHT - contentY - 40;
            int panelWidth = 300;
            int panelX = MAIN_PANEL_WIDTH - panelWidth - 10;

            // Favorites panel container
            var favPanel = new Panel
            {
                Name = "FavoritesPanel",
                Position = new Vector2I(panelX, contentY),
                Size = new Vector2I(panelWidth, contentHeight),
                Visible = !_compactView
            };
            
            var favStyle = new StyleBoxFlat { BgColor = new Godot.Color(0.06f, 0.06f, 0.09f, 0.95f) };
            favPanel.AddThemeStyleboxOverride("panel", favStyle);
            _mainPanel.AddChild(favPanel);

            // Favorites header
            var favHeader = new Label
            {
                Text = "⭐ Favorites",
                Position = new Vector2I(10, 10),
                Size = new Vector2I(panelWidth - 20, 24)
            };
            favHeader.AddThemeColorOverride("font_color", TEXT_COLOR);
            favPanel.AddChild(favHeader);

            // Favorites list
            _favList = new ItemList
            {
                Position = new Vector2I(10, 40),
                Size = new Vector2I(panelWidth - 20, contentHeight - 90)
            };
            _favList.AddThemeColorOverride("font_color", SECONDARY_TEXT);
            
            try
            {
                _favList.Connect("item_activated", Callable.From((long idx) =>
                {
                    int i = (int)idx;
                    if (i >= 0 && i < _favorites.Count)
                        NavigateTo(_favorites[i]);
                }));
            }
            catch
            {
                Log.Warn($"{LOG_PREFIX} Using fallback for item_activated");
            }
            
            favPanel.AddChild(_favList);
            RefreshFavList();

            // Remove favorite button
            var removeBtn = new Button
            {
                Text = "Remove Selected",
                Position = new Vector2I(10, contentHeight - 45),
                Size = new Vector2I(panelWidth - 20, 30)
            };
            removeBtn.Pressed += OnRemoveFavoriteClick;
            favPanel.AddChild(removeBtn);

            // Bottom info bar
            CreateBottomInfoBar();
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateCompactView error: {ex.Message}");
        }
    }

    /// <summary>Create bottom info bar</summary>
    private static void CreateBottomInfoBar()
    {
        if (_mainPanel == null) return;
        
        try
        {
            int y = MAIN_PANEL_HEIGHT - 36;
            
            // Opacity label
            var opacityLabel = new Label
            {
                Text = "Opacity:",
                Position = new Vector2I(10, y),
                Size = new Vector2I(60, 24)
            };
            opacityLabel.AddThemeColorOverride("font_color", HINT_TEXT);
            _mainPanel.AddChild(opacityLabel);

            // Opacity slider
            _opacitySlider = new HSlider
            {
                Position = new Vector2I(75, y),
                Size = new Vector2I(150, 24),
                MinValue = MIN_OPACITY,
                MaxValue = MAX_OPACITY,
                Step = OPACITY_STEP,
                Value = DEFAULT_OPACITY
            };
            _opacitySlider.ValueChanged += (v) =>
            {
                try
                {
                    var s = _mainPanel?.GetThemeStylebox("panel") as StyleBoxFlat;
                    if (s != null)
                        s.BgColor = new Godot.Color(0.05f, 0.05f, 0.08f, (float)v);
                }
                catch (Exception ex)
                {
                    Log.Warn($"{LOG_PREFIX} Opacity change error: {ex.Message}");
                }
            };
            _mainPanel.AddChild(_opacitySlider);

            // Help text
            var helpLabel = new Label
            {
                Text = "Drag title bar to move | Drag corner to resize | 📑 Toggle panel",
                Position = new Vector2I(240, y),
                Size = new Vector2I(500, 24)
            };
            helpLabel.AddThemeColorOverride("font_color", HINT_TEXT);
            _mainPanel.AddChild(helpLabel);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateBottomInfoBar error: {ex.Message}");
        }
    }

    /// <summary>Create resize handle</summary>
    private static void CreateResizeHandle()
    {
        if (_mainPanel == null) return;
        
        try
        {
            new ColorRect
            {
                Name = "ResizeHandle",
                Color = RESIZE_HANDLE_COLOR,
                Size = new Vector2I(RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE),
                Position = new Vector2I(MAIN_PANEL_WIDTH - RESIZE_HANDLE_SIZE, MAIN_PANEL_HEIGHT - RESIZE_HANDLE_SIZE),
                MouseFilter = Godot.Control.MouseFilterEnum.Stop
            }
            .Apply(r =>
            {
                r.GuiInput += OnResizeInput;
                _mainPanel.AddChild(r);
            });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} CreateResizeHandle error: {ex.Message}");
        }
    }
    #endregion

    #region Navigation
    /// <summary>Navigate to URL</summary>
    private static void NavigateTo(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url))
                url = "https://www.bilibili.com";
            if (!url.StartsWith("http"))
                url = "https://" + url;

            _lastUrl = url;
            if (_urlInput != null)
                _urlInput.Text = url;

            // Add to history
            if (_historyIndex < _history.Count - 1)
            {
                // Remove forward history if we're not at the end
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            }
            _history.Add(url);
            _historyIndex = _history.Count - 1;
            
            UpdateNavigationButtons();
            UpdateStatus($"Navigating to: {url}");
            
            Log.Info($"{LOG_PREFIX} Navigating to: {url}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} NavigateTo error: {ex.Message}");
        }
    }

    /// <summary>Update navigation button states</summary>
    private static void UpdateNavigationButtons()
    {
        if (_backBtn != null)
            _backBtn.Disabled = _historyIndex <= 0;
        if (_forwardBtn != null)
            _forwardBtn.Disabled = _historyIndex >= _history.Count - 1;
    }

    /// <summary>Update status text</summary>
    private static void UpdateStatus(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text + "\n\n💡 Click \"🔗\" to open in external browser";
        }
    }
    #endregion

    #region Event Handlers
    private static void OnToggleClick()
    {
        try
        {
            _panelVisible = !_panelVisible;
            if (_mainPanel != null)
                _mainPanel.Visible = _panelVisible;
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
            if (_mainPanel != null)
                _mainPanel.Visible = false;
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnCloseClick error: {ex.Message}");
        }
    }

    private static void OnToggleViewClick()
    {
        try
        {
            _compactView = !_compactView;
            // Toggle favorites panel visibility and update layout
            OnPanelResized();
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnToggleViewClick error: {ex.Message}");
        }
    }

    private static void OnBackClick()
    {
        try
        {
            if (_historyIndex > 0)
            {
                _historyIndex--;
                string url = _history[_historyIndex];
                _lastUrl = url;
                if (_urlInput != null)
                    _urlInput.Text = url;
                UpdateNavigationButtons();
                UpdateStatus($"Back to: {url}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnBackClick error: {ex.Message}");
        }
    }

    private static void OnForwardClick()
    {
        try
        {
            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                string url = _history[_historyIndex];
                _lastUrl = url;
                if (_urlInput != null)
                    _urlInput.Text = url;
                UpdateNavigationButtons();
                UpdateStatus($"Forward to: {url}");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnForwardClick error: {ex.Message}");
        }
    }

    private static void OnRefreshClick()
    {
        try
        {
            UpdateStatus($"Refreshing: {_lastUrl}");
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnRefreshClick error: {ex.Message}");
        }
    }

    private static void OnHomeClick()
    {
        try
        {
            NavigateTo("https://www.bilibili.com");
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnHomeClick error: {ex.Message}");
        }
    }

    private static void OnUrlSubmitted(string text)
    {
        try
        {
            NavigateTo(text);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnUrlSubmitted error: {ex.Message}");
        }
    }

    private static void OnGoClick()
    {
        try
        {
            var url = _urlInput?.Text.Trim() ?? "";
            NavigateTo(url);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnGoClick error: {ex.Message}");
        }
    }

    private static void OnOpenExternalClick()
    {
        try
        {
            OpenUrlExternal(_lastUrl);
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnOpenExternalClick error: {ex.Message}");
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
                UpdateStatus($"Added to favorites: {url}");
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

    private static void OnTitleInput(InputEvent e)
    {
        try
        {
            if (e is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _dragging = mb.Pressed;
                    if (_dragging && _titleBg != null && _mainPanel != null)
                        _dragOffset = _titleBg.GetGlobalMousePosition() - _mainPanel.Position.AsVector2();
                }
                return;
            }

            if (_dragging && e is InputEventMouseMotion && _titleBg != null && _mainPanel != null)
                _mainPanel.Position = (_titleBg.GetGlobalMousePosition() - _dragOffset).AsVector2I();
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
                    if (_resizing && _mainPanel != null)
                    {
                        _resizeStartPos = _mainPanel.GetGlobalMousePosition();
                        _resizeStartSize = _mainPanel.Size.AsVector2();
                    }
                }
                return;
            }

            if (_resizing && e is InputEventMouseMotion && _mainPanel != null)
            {
                var delta = _mainPanel.GetGlobalMousePosition() - _resizeStartPos;
                int newWidth = (int)Mathf.Max(_resizeStartSize.X + delta.X, MIN_PANEL_WIDTH);
                int newHeight = (int)Mathf.Max(_resizeStartSize.Y + delta.Y, MIN_PANEL_HEIGHT);
                _mainPanel.Size = new Vector2I(newWidth, newHeight);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnResizeInput error: {ex.Message}");
        }
    }

    private static void OnPanelResized()
    {
        try
        {
            if (_mainPanel == null) return;
            
            var panelSize = _mainPanel.Size;
            
            // Adjust title bar
            if (_titleBg != null)
            {
                _titleBg.Size = new Vector2I(panelSize.X, TITLE_HEIGHT);
            }
            
            // Adjust toolbar
            if (_toolbarBg != null)
            {
                _toolbarBg.Position = new Vector2I(0, TITLE_HEIGHT);
                _toolbarBg.Size = new Vector2I(panelSize.X, TOOLBAR_HEIGHT);
            }
            
            // Move header buttons
            if (_toggleViewBtn != null)
                _toggleViewBtn.Position = new Vector2I(panelSize.X - 120, 2);
            if (_externalBtn != null)
                _externalBtn.Position = new Vector2I(panelSize.X - 88, 2);
            
            // Adjust URL input width dynamically
            if (_urlInput != null)
            {
                int urlX = 194;
                int urlWidth = _compactView ? panelSize.X - urlX - 110 : panelSize.X - urlX - 340;
                _urlInput.Position = new Vector2I(urlX, TITLE_HEIGHT + 6);
                _urlInput.Size = new Vector2I(Math.Max(200, urlWidth), INPUT_HEIGHT);
            }
            
            // Adjust resize handle
            foreach (var child in _mainPanel.GetChildren())
            {
                if (child is ColorRect rect && rect.Name == "ResizeHandle")
                {
                    rect.Position = new Vector2I(panelSize.X - RESIZE_HANDLE_SIZE, panelSize.Y - RESIZE_HANDLE_SIZE);
                    break;
                }
            }
            
            // Adjust browser container
            if (_browserContainer != null)
            {
                int contentY = TITLE_HEIGHT + TOOLBAR_HEIGHT;
                int contentHeight = panelSize.Y - contentY - 40;
                int contentWidth = _compactView ? panelSize.X - 20 : panelSize.X - 330;
                _browserContainer.Position = new Vector2I(10, contentY);
                _browserContainer.Size = new Vector2I(contentWidth, contentHeight);
                
                if (_webContentPanel != null)
                {
                    _webContentPanel.Size = _browserContainer.Size;
                }
            }
            
            // Toggle favorites panel visibility
            foreach (var child in _mainPanel.GetChildren())
            {
                if (child is Panel favPanel && favPanel.Name == "FavoritesPanel")
                {
                    favPanel.Visible = !_compactView;
                    if (!_compactView)
                    {
                        int contentY = TITLE_HEIGHT + TOOLBAR_HEIGHT;
                        int contentHeight = panelSize.Y - contentY - 40;
                        int panelWidth = 300;
                        int panelX = panelSize.X - panelWidth - 10;
                        favPanel.Position = new Vector2I(panelX, contentY);
                        favPanel.Size = new Vector2I(panelWidth, contentHeight);
                        
                        // Resize favorites list
                        foreach (var grandChild in favPanel.GetChildren())
                        {
                            if (grandChild is ItemList list && list == _favList)
                            {
                                list.Size = new Vector2I(panelWidth - 20, contentHeight - 90);
                                break;
                            }
                        }
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} OnPanelResized error: {ex.Message}");
        }
    }
    #endregion

    #region Favorites
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
                _favList.AddItem($"⭐ {displayName}\n  {url}");
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
    #endregion

    #region External Browser
    /// <summary>Open URL in external system browser</summary>
    private static void OpenUrlExternal(string url)
    {
        try
        {
            _lastUrl = url;
            if (_urlInput != null)
                _urlInput.Text = url;

            Log.Info($"{LOG_PREFIX} Opening external browser for: {url}");

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
                OpenUrlFallback(url);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} Failed to open URL: {url} - {ex.Message}");
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
    #endregion
}

#region Helper Extensions
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
            Log.Warn($"SetCornerRadiusAll fallback: {ex.Message}");
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
            Console.WriteLine($"[{level}] {message}");
        }
    }
}
#endregion
