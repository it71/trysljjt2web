using Godot;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// WebOverlay - Floating Browser Mod for Slay the Spire 2
/// Provides quick access to popular websites with favorites management
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
    private static CanvasLayer _canvas;
    private static Godot.Panel _panel;
    private static Godot.Panel _titleBg;
    private static LineEdit _urlInput;
    private static ItemList _favList;
    
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
            ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
            
            var harmony = new Harmony(MOD_ID);
            harmony.PatchAll();
            
            LoadFavorites();
            
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree?.Root != null)
            {
                tree.Root.CallDeferred("add_child", BuildUI());
                Log.Info($"{LOG_PREFIX} Ready (v4.0.0)");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} Init failed: {ex.GetType().Name} - {ex.Message}");
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
                LoadDefaultFavorites();
                return;
            }
            
            var keys = cf.GetSectionKeys("favorites");
            if (keys?.Length > 0)
            {
                foreach (var key in keys)
                {
                    var val = cf.GetValue("favorites", key, "").AsString();
                    if (!string.IsNullOrEmpty(val))
                        _favorites.Add(val);
                }
                
                if (_favorites.Count == 0)
                    LoadDefaultFavorites();
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
            
            cf.Save(CONFIG_PATH);
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

        return _canvas;
    }

    /// <summary>Create toggle button (CE)</summary>
    private static void CreateToggleButton()
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

    /// <summary>Create main panel</summary>
    private static void CreateMainPanel()
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

    /// <summary>Create title bar</summary>
    private static void CreateTitleBar()
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

    /// <summary>Create quick site buttons</summary>
    private static int CreateQuickSiteButtons()
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

    /// <summary>Create a single quick site button</summary>
    private static void CreateQuickSiteButton(string name, string url, int x, int y)
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

    /// <summary>Create URL input section</summary>
    private static int CreateUrlInputSection(int y)
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

    /// <summary>Create favorites list section</summary>
    private static int CreateFavoritesSection(int y)
    {
        y += 34;

        _favList = new ItemList
        {
            Position = new Vector2I(10, y),
            Size = new Vector2I(520, 180)
        };
        _favList.AddThemeColorOverride("font_color", SECONDARY_TEXT);
        _favList.Connect("item_activated", Callable.From((long idx) =>
        {
            int i = (int)idx;
            if (i >= 0 && i < _favorites.Count)
                OpenUrl(_favorites[i]);
        }));
        _panel.AddChild(_favList);
        RefreshFavList();

        return y;
    }

    /// <summary>Create opacity control and help text</summary>
    private static void CreateOpacityControl(int y)
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
        var slider = new HSlider
        {
            Position = new Vector2I(70, y),
            Size = new Vector2I(140, 22),
            MinValue = MIN_OPACITY,
            MaxValue = MAX_OPACITY,
            Step = OPACITY_STEP,
            Value = DEFAULT_OPACITY
        };
        slider.ValueChanged += (v) =>
        {
            var s = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
            if (s != null)
                s.BgColor = new Godot.Color(0.08f, 0.08f, 0.12f, (float)v);
        };
        _panel.AddChild(slider);

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

    /// <summary>Create resize handle</summary>
    private static void CreateResizeHandle()
    {
        new ColorRect
        {
            Name = "ResizeHandle",
            Color = RESIZE_HANDLE_COLOR,
            Size = new Vector2I(RESIZE_HANDLE_SIZE, RESIZE_HANDLE_SIZE),
            MouseFilter = Godot.Control.MouseFilterEnum.Stop
        }
        .Apply(r =>
        {
            r.GuiInput += OnResizeInput;
            _panel.AddChild(r);
        });
    }

    /// <summary>Refresh favorites list display</summary>
    private static void RefreshFavList()
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
        _panelVisible = !_panelVisible;
        _panel.Visible = _panelVisible;
        if (_panelVisible && _urlInput != null)
            _urlInput.Text = _lastUrl;
    }

    private static void OnCloseClick()
    {
        _panelVisible = false;
        _panel.Visible = false;
    }

    private static void OnGoClick()
    {
        var url = _urlInput.Text.Trim();
        if (string.IsNullOrEmpty(url))
            url = "https://www.bilibili.com";
        if (!url.StartsWith("http"))
            url = "https://" + url;
        OpenUrl(url);
    }

    private static void OnAddFavoriteClick()
    {
        var url = _urlInput.Text.Trim();
        if (!string.IsNullOrEmpty(url) && !_favorites.Contains(url))
        {
            _favorites.Add(url);
            SaveFavorites();
            RefreshFavList();
        }
    }

    private static void OnRemoveFavoriteClick()
    {
        var sel = _favList.GetSelectedItems();
        if (sel.Length > 0 && sel[0] >= 0 && sel[0] < _favorites.Count)
        {
            _favorites.RemoveAt(sel[0]);
            SaveFavorites();
            RefreshFavList();
        }
    }

    private static void OnOpenFavoriteClick()
    {
        var sel = _favList.GetSelectedItems();
        if (sel.Length > 0 && sel[0] >= 0 && sel[0] < _favorites.Count)
            OpenUrl(_favorites[sel[0]]);
    }

    private static void OnTitleInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = mb.Pressed;
                if (_dragging)
                    _dragOffset = _titleBg.GetGlobalMousePosition() - _panel.Position.AsVector2();
            }
            return;
        }

        if (_dragging && e is InputEventMouseMotion)
            _panel.Position = (_titleBg.GetGlobalMousePosition() - _dragOffset).AsVector2I();
    }

    private static void OnResizeInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _resizing = mb.Pressed;
                if (_resizing)
                {
                    _resizeStartPos = _titleBg.GetGlobalMousePosition();
                    _resizeStartSize = _panel.Size.AsVector2();
                }
            }
            return;
        }

        if (_resizing && e is InputEventMouseMotion)
        {
            var delta = _titleBg.GetGlobalMousePosition() - _resizeStartPos;
            int newWidth = (int)Mathf.Max(_resizeStartSize.X + delta.X, MIN_PANEL_WIDTH);
            int newHeight = (int)Mathf.Max(_resizeStartSize.Y + delta.Y, MIN_PANEL_HEIGHT);
            _panel.Size = new Vector2I(newWidth, newHeight);
        }
    }

    #endregion

    /// <summary>Open URL in system browser</summary>
    private static void OpenUrl(string url)
    {
        _lastUrl = url;
        if (_urlInput != null)
            _urlInput.Text = url;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"{LOG_PREFIX} Failed to open URL: {url} - {ex.Message}");
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
        styleBox.CornerRadiusTopLeft = radius;
        styleBox.CornerRadiusTopRight = radius;
        styleBox.CornerRadiusBottomLeft = radius;
        styleBox.CornerRadiusBottomRight = radius;
    }
}
