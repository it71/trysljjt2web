using Godot;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using System;
using System.Collections.Generic;
using System.Diagnostics;

[ModInitializer(nameof(Init))]
public static class Entry
{
    private static CanvasLayer _canvas;
    private static Godot.Panel _panel;
    private static Godot.Panel _titleBg;
    private static bool _panelVisible = false;
    private static bool _dragging = false;
    private static bool _resizing = false;
    private static Vector2 _dragOffset;
    private static Vector2 _resizeStartPos;
    private static Vector2 _resizeStartSize;
    private static LineEdit _urlInput;
    private static ItemList _favList;
    private static string _lastUrl = "https://www.bilibili.com";
    private static List<string> _favorites = new List<string>();
    private static string _configPath = "user://weboverlay.cfg";

    public static void Init()
    {
        try
        {
            ScriptManagerBridge.LookupScriptsInAssembly(typeof(Entry).Assembly);
            var harmony = new Harmony("com.tiezhu.weboverlay");
            harmony.PatchAll();
            LoadFavorites();
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree?.Root != null)
            {
                tree.Root.CallDeferred("add_child", BuildUI());
                Log.Info("[WebOverlay] Ready");
            }
        }
        catch (Exception ex) { Log.Warn("[WebOverlay] Init: " + ex.Message); }
    }

    static void LoadFavorites()
    {
        _favorites.Clear();
        try
        {
            var cf = new ConfigFile();
            if (cf.Load(_configPath) == Godot.Error.Ok)
            {
                var keys = cf.GetSectionKeys("favorites");
                if (keys != null)
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        var val = cf.GetValue("favorites", keys[i], "").AsString();
                        if (!string.IsNullOrEmpty(val))
                            _favorites.Add(val);
                    }
                }
            }
        }
        catch { }
        if (_favorites.Count == 0)
        {
            _favorites.Add("https://www.bilibili.com");
            _favorites.Add("https://www.youtube.com");
            _favorites.Add("https://www.douyin.com");
        }
    }

    static void SaveFavorites()
    {
        try
        {
            var cf = new ConfigFile();
            for (int i = 0; i < _favorites.Count; i++)
                cf.SetValue("favorites", "fav_" + i, _favorites[i]);
            cf.Save(_configPath);
        }
        catch { }
    }

    static CanvasLayer BuildUI()
    {
        _canvas = new CanvasLayer();
        _canvas.Name = "WebOverlayCanvas";
        _canvas.Layer = 128;

        new Godot.Button { Text = "CE", Position = new Vector2I(10, 10), Size = new Vector2I(36, 36) }
            .Apply(b => { b.Pressed += OnToggleClick; _canvas.AddChild(b); });

        _panel = new Godot.Panel();
        _panel.Name = "MainPanel";
        _panel.Position = new Vector2I(200, 80);
        _panel.Size = new Vector2I(540, 400);
        _panel.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
        _panel.Visible = false;
        var ps = new StyleBoxFlat { BgColor = new Godot.Color(0.08f, 0.08f, 0.12f, 0.92f) };
        ps.SetCornerRadiusAll(8);
        _panel.AddThemeStyleboxOverride("panel", ps);
        _canvas.AddChild(_panel);

        _titleBg = new Godot.Panel();
        _titleBg.Position = Vector2I.Zero;
        _titleBg.Size = new Vector2I(540, 32);
        _titleBg.MouseFilter = Godot.Control.MouseFilterEnum.Stop;
        var ts = new StyleBoxFlat { BgColor = new Godot.Color(0.12f, 0.12f, 0.2f, 1) };
        ts.CornerRadiusTopLeft = 8; ts.CornerRadiusTopRight = 8;
        _titleBg.AddThemeStyleboxOverride("panel", ts);
        _titleBg.GuiInput += OnTitleInput;
        _panel.AddChild(_titleBg);

        new Godot.Label { Text = "  CE WebOverlay", Position = new Vector2I(4, 0), Size = new Vector2I(200, 32) }
            .Apply(l => { l.AddThemeColorOverride("font_color", Colors.White); _titleBg.AddChild(l); });

        new Godot.Button { Text = "X", Position = new Vector2I(508, 2), Size = new Vector2I(28, 28), Flat = true }
            .Apply(b => { b.Pressed += OnCloseClick; _panel.AddChild(b); });

        int x = 10, y = 42;
        foreach (var (n, u) in new[] {
            ("Bili", "https://www.bilibili.com"),
            ("YT", "https://www.youtube.com"),
            ("Douyin", "https://www.douyin.com"),
        })
        {
            string url = u;
            new Godot.Button { Text = n, Position = new Vector2I(x, y), Size = new Vector2I(56, 24) }
                .Apply(b => { b.Pressed += () => OpenUrl(url); _panel.AddChild(b); });
            x += 60;
        }
        x = 10; y += 30;
        foreach (var (n, u) in new[] {
            ("Douyu", "https://www.douyu.com"),
            ("Huya", "https://www.huya.com"),
            ("Weibo", "https://www.weibo.com"),
        })
        {
            string url = u;
            new Godot.Button { Text = n, Position = new Vector2I(x, y), Size = new Vector2I(56, 24) }
                .Apply(b => { b.Pressed += () => OpenUrl(url); _panel.AddChild(b); });
            x += 60;
        }

        y += 34;
        _urlInput = new LineEdit { PlaceholderText = "粘贴网址...", Position = new Vector2I(10, y), Size = new Vector2I(340, 28), Text = _lastUrl };
        _panel.AddChild(_urlInput);

        new Godot.Button { Text = "Go", Position = new Vector2I(356, y), Size = new Vector2I(60, 28) }
            .Apply(b => { b.Pressed += OnGoClick; _panel.AddChild(b); });

        // ♥ 收藏
        new Godot.Button { Text = "♥", Position = new Vector2I(422, y), Size = new Vector2I(28, 28), Flat = true }
            .Apply(b => { b.Pressed += () => {
                var url = _urlInput.Text.Trim();
                if (!string.IsNullOrEmpty(url) && !_favorites.Contains(url))
                { _favorites.Add(url); SaveFavorites(); RefreshFavList(); }
            }; _panel.AddChild(b); });

        // − 删除收藏
        new Godot.Button { Text = "−", Position = new Vector2I(454, y), Size = new Vector2I(28, 28), Flat = true }
            .Apply(b => { b.Pressed += () => {
                var sel = _favList.GetSelectedItems();
                if (sel.Length > 0 && sel[0] >= 0 && sel[0] < _favorites.Count)
                { _favorites.RemoveAt(sel[0]); SaveFavorites(); RefreshFavList(); }
            }; _panel.AddChild(b); });

        // ▶ 打开选中
        new Godot.Button { Text = "▶", Position = new Vector2I(488, y), Size = new Vector2I(32, 28), Flat = true }
            .Apply(b => { b.Pressed += () => {
                var sel = _favList.GetSelectedItems();
                if (sel.Length > 0 && sel[0] >= 0 && sel[0] < _favorites.Count)
                    OpenUrl(_favorites[sel[0]]);
            }; _panel.AddChild(b); });

        // 收藏夹列表
        y += 34;
        _favList = new ItemList();
        _favList.Position = new Vector2I(10, y);
        _favList.Size = new Vector2I(520, 180);
        _favList.AddThemeColorOverride("font_color", new Godot.Color(0.8f, 0.8f, 0.8f));
        // 用 Connect 而非事件以避免参数类型问题
        _favList.Connect("item_activated", Callable.From((long idx) => {
            int i = (int)idx;
            if (i >= 0 && i < _favorites.Count) OpenUrl(_favorites[i]);
        }));
        _panel.AddChild(_favList);
        RefreshFavList();

        y += 186;
        new Godot.Label { Text = "Opacity", Position = new Vector2I(10, y), Size = new Vector2I(60, 22) }
            .Apply(l => _panel.AddChild(l));
        var slider = new HSlider { Position = new Vector2I(70, y), Size = new Vector2I(140, 22), MinValue = 0.2, MaxValue = 1.0, Step = 0.05, Value = 0.92 };
        slider.ValueChanged += (v) => {
            var s = _panel.GetThemeStylebox("panel") as StyleBoxFlat;
            if (s != null) s.BgColor = new Godot.Color(0.08f, 0.08f, 0.12f, (float)v);
        };
        _panel.AddChild(slider);

        new Godot.Label { Text = "拖拽 | 调整 | CE | ♥收藏 | −删除 | ▶打开", Position = new Vector2I(220, y), Size = new Vector2I(310, 22) }
            .Apply(l => { l.AddThemeColorOverride("font_color", new Godot.Color(0.5f, 0.5f, 0.5f)); _panel.AddChild(l); });

        new ColorRect { Name = "ResizeHandle", Color = new Godot.Color(0.5f, 0.5f, 0.5f, 0.35f), Size = new Vector2I(14, 14),
            MouseFilter = Godot.Control.MouseFilterEnum.Stop }
            .Apply(r => { r.GuiInput += OnResizeInput; _panel.AddChild(r); });

        return _canvas;
    }

    static void RefreshFavList()
    {
        if (_favList == null) return;
        _favList.Clear();
        foreach (var url in _favorites)
        {
            string name = url;
            try { var u = new Uri(url); name = u.Host; } catch { }
            _favList.AddItem(name + "  (" + url + ")");
        }
    }

    static void OnToggleClick()
    {
        _panelVisible = !_panelVisible;
        _panel.Visible = _panelVisible;
        if (_panelVisible) _urlInput.Text = _lastUrl;
    }

    static void OnCloseClick() { _panelVisible = false; _panel.Visible = false; }
    static void OnGoClick()
    {
        var url = _urlInput.Text.Trim();
        if (string.IsNullOrEmpty(url)) url = "https://www.bilibili.com";
        if (!url.StartsWith("http")) url = "https://" + url;
        OpenUrl(url);
    }

    static void OpenUrl(string url)
    {
        _lastUrl = url;
        _urlInput.Text = url;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { Log.Warn("[WebOverlay] Failed: " + url); }
    }

    static void OnTitleInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb) {
            if (mb.ButtonIndex == MouseButton.Left) {
                _dragging = mb.Pressed;
                if (_dragging) _dragOffset = _titleBg.GetGlobalMousePosition() - _panel.Position;
            } return;
        }
        if (_dragging && e is InputEventMouseMotion)
            _panel.Position = _titleBg.GetGlobalMousePosition() - _dragOffset;
    }

    static void OnResizeInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb) {
            if (mb.ButtonIndex == MouseButton.Left) {
                _resizing = mb.Pressed;
                if (_resizing) { _resizeStartPos = _titleBg.GetGlobalMousePosition(); _resizeStartSize = _panel.Size; }
            } return;
        }
        if (_resizing && e is InputEventMouseMotion) {
            var d = _titleBg.GetGlobalMousePosition() - _resizeStartPos;
            var w = (int)Mathf.Max(_resizeStartSize.X + d.X, 300);
            var h = (int)Mathf.Max(_resizeStartSize.Y + d.Y, 200);
            _panel.Size = new Vector2I(w, h);
        }
    }
}

public static class Ext { public static T Apply<T>(this T o, Action<T> f) { f(o); return o; } }
public static class SbfExt { public static void SetCornerRadiusAll(this StyleBoxFlat s, int r) {
    s.CornerRadiusTopLeft = r; s.CornerRadiusTopRight = r;
    s.CornerRadiusBottomLeft = r; s.CornerRadiusBottomRight = r; } }