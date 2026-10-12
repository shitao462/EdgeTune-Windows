using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace EdgeTune;
public sealed class OverlayWindow : Window
{
    private readonly Preferences preferences = Preferences.Load();
    private readonly MediaService media = new();
    private readonly AudioMeter audio = new();
    private readonly LyricsService lyrics = new();
    private readonly Grid root = new(), content = new();
    private readonly Border surface = new(), edge = new(), accent = new();
    private readonly TextBlock title = new(), artist = new(), original = new(), translation = new();
    private readonly Button pin = new();
    private readonly DispatcherTimer polling = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer frame = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Forms.NotifyIcon? tray;
    private IntPtr handle;
    private HwndSource? source;
    private Track? track;
    private LyricsResult lyricResult = LyricsResult.Empty;
    private CancellationTokenSource? lyricRequest;
    private string lyricIdentity = "";
    private bool reading, expanded, hotkeyOK, closing, dragging;
    private double progress, target, pulse = 1;
    private DateTime outsideSince = DateTime.UtcNow, hoverSince = DateTime.MaxValue;
    private DateTime lastFrame = DateTime.UtcNow, lastTopmost;
    private Forms.Screen monitor = Forms.Screen.PrimaryScreen!;
    public static readonly string[] ColorNames = { "莹绿色", "天空蓝", "紫色", "珊瑚色", "金色", "绿蓝渐变", "紫粉渐变", "日落渐变" };
    private static readonly string[][] Colors = { new[]{"#7CF1BB"}, new[]{"#7BCFFF"}, new[]{"#B99AFF"}, new[]{"#FF918A"}, new[]{"#FFD16D"}, new[]{"#7CF1BB","#7BCFFF"}, new[]{"#A594FF","#FF91CF"}, new[]{"#FFCF73","#FF839E"} };
    public OverlayWindow()
    {
        Title = "EdgeTune"; Width = 7; Height = 110; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        surface.CornerRadius = new CornerRadius(16); surface.BorderThickness = new Thickness(1); surface.Child = content;
        root.Children.Add(surface); edge.Width = 7; edge.CornerRadius = new CornerRadius(3.5); edge.HorizontalAlignment = HorizontalAlignment.Right;
        edge.VerticalAlignment = VerticalAlignment.Bottom; edge.Margin = new Thickness(0,0,0,19); root.Children.Add(edge);
        accent.Width = 3; accent.HorizontalAlignment = HorizontalAlignment.Left; accent.VerticalAlignment = VerticalAlignment.Bottom;
        accent.Margin = new Thickness(0,0,0,17); accent.CornerRadius = new CornerRadius(1.5); content.Children.Add(accent);
        SetupText(title, 18, 12, 196, 23, 15); title.FontWeight = FontWeights.SemiBold;
        SetupText(artist, 18, 37, 280, 20, 11);
        SetupText(original, 18, 64, 178, 36, 12); original.TextWrapping = TextWrapping.Wrap;
        SetupText(translation, 18, 82, 178, 18, 11); translation.Visibility = Visibility.Collapsed;
        AddButton("⏮", 216, 10, async () => await media.ControlAsync(preferences.Player, "previous"));
        AddButton("⏯", 247, 10, async () => await media.ControlAsync(preferences.Player, "toggle"));
        AddButton("⏭", 278, 10, async () => await media.ControlAsync(preferences.Player, "next"));
        AddButton("⚙", 208, 70, Settings);
        pin.Content = "◇"; PlaceButton(pin, 242, 70); pin.ToolTip = "常驻 / 自动收起";
        pin.Click += (_, _) => { preferences.Pinned = !preferences.Pinned; preferences.Save(); pin.Content = preferences.Pinned ? "◆" : "◇"; if (preferences.Pinned) Expand(true); };
        AddButton("›", 276, 70, () => Expand(false));
        title.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 1) { dragging=true; try { DragMove(); SavePosition(); } catch { } finally { dragging=false; } } };
        root.ClipToBounds = true; Content = root;
        SourceInitialized += (_, _) => InitializeNative();
        Closed += (_, _) => Cleanup();
        polling.Tick += async (_, _) => await RefreshTrack();
        frame.Tick += (_, _) => Tick();
        ApplyTheme(); pin.Content = preferences.Pinned ? "◆" : "◇";
    }
    private void SetupText(TextBlock text, double x, double y, double width, double height, double size)
    {
        text.HorizontalAlignment = HorizontalAlignment.Left; text.VerticalAlignment = VerticalAlignment.Top;
        text.Margin = new Thickness(x,y,0,0); text.Width = width; text.Height = height; text.FontSize = size;
        text.TextTrimming = TextTrimming.CharacterEllipsis; content.Children.Add(text);
    }
    private void PlaceButton(Button button, double x, double y)
    {
        button.Width = 25; button.Height = 25; button.HorizontalAlignment = HorizontalAlignment.Left; button.VerticalAlignment = VerticalAlignment.Top;
        button.Margin = new Thickness(x,y,0,0); button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.FontSize = 16;
        button.Focusable = false; content.Children.Add(button);
    }
    private void AddButton(string text, double x, double y, Action action) { var button = new Button { Content = text }; PlaceButton(button,x,y); button.Click += (_, _) => action(); }
    private void InitializeNative()
    {
        handle = new WindowInteropHelper(this).Handle; source = HwndSource.FromHwnd(handle); source.AddHook(WndProc);
        Native.SetWindowLong(handle, Native.GWL_EXSTYLE, Native.GetWindowLong(handle, Native.GWL_EXSTYLE) | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
        RegisterHotkey(); InitializeTray(); UpdatePosition(); polling.Start(); frame.Start(); _ = RefreshTrack(); Expand(preferences.Pinned);
    }
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_MOUSEACTIVATE) { handled = true; return new IntPtr(Native.MA_NOACTIVATE); }
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == 1) { handled = true;
            Native.GetCursorPos(out var cursor); monitor = Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X,cursor.Y)); Expand(!expanded); UpdatePosition(); }
        if (msg == 0x7E || msg == 0x2E0) { monitor = Forms.Screen.FromPoint(Forms.Cursor.Position); Dispatcher.BeginInvoke(UpdatePosition); }
        return IntPtr.Zero;
    }
    public bool RegisterHotkey()
    {
        Native.UnregisterHotKey(handle, 1);
        hotkeyOK = Native.RegisterHotKey(handle, 1, (uint)preferences.HotkeyModifiers | 0x4000, (uint)preferences.HotkeyKey);
        if (!hotkeyOK) { Log.Write("Hotkey registration failed: shortcut occupied"); if (tray != null) tray.ShowBalloonTip(4000,"EdgeTune","快捷键被占用，请在设置中更换。",Forms.ToolTipIcon.Warning); }
        return hotkeyOK;
    }
    private void InitializeTray()
    {
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "EdgeTune — Ctrl+Alt+M 呼出", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("呼出 / 收起", null, (_, _) => Dispatcher.Invoke(() => Expand(!expanded)));
        menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(Settings));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(() => { closing = true; Close(); Application.Current.Shutdown(); }));
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => Expand(true));
        if (!hotkeyOK) tray.ShowBalloonTip(4000,"EdgeTune","快捷键被占用，请在设置中更换。",Forms.ToolTipIcon.Warning);
    }
    private void Expand(bool value) { expanded = value; target = value ? 1 : 0; outsideSince = DateTime.UtcNow; hoverSince = DateTime.MaxValue; }
    private void Tick()
    {
        if (closing) return;
        var now = DateTime.UtcNow; double dt = Math.Clamp((now-lastFrame).TotalSeconds,0,.1); lastFrame = now;
        progress += (target-progress) * (1-Math.Exp(-dt*13)); if (Math.Abs(progress-target)<.001) progress=target;
        Width = 7 + 309*progress; content.Width = 316; content.Opacity = progress;
        surface.Visibility = progress < .01 ? Visibility.Collapsed : Visibility.Visible;
        edge.Opacity = 1-progress; surface.Opacity = progress;
        Native.GetCursorPos(out var point);
        var local = PointFromScreen(new System.Windows.Point(point.X,point.Y));
        bool inside = local.X >= 0 && local.X <= ActualWidth && local.Y >= 0 && local.Y <= ActualHeight;
        if (inside) { outsideSince = now; if (!expanded) { if (hoverSince == DateTime.MaxValue) hoverSince=now; if ((now-hoverSince).TotalSeconds>.2) Expand(true); } }
        else { hoverSince = DateTime.MaxValue; if (expanded && !preferences.Pinned && (now-outsideSince).TotalSeconds>1) Expand(false); }
        double value = preferences.Pulse && track?.Playing == true ? audio.Read(preferences.Player) : 1;
        pulse += (value-pulse)*(1-Math.Exp(-dt*(value>pulse?24:6)));
        edge.Height = 72*(.5+.5*pulse); accent.Height = 76*(.5+.5*pulse);
        if (!dragging) UpdatePosition(); RefreshLyric();
        if ((now-lastTopmost).TotalSeconds>1) { Native.SetWindowPos(handle,Native.HWND_TOPMOST,0,0,0,0,0x1|0x2|0x10); lastTopmost=now; }
    }
    private void UpdatePosition()
    {
        if (source?.CompositionTarget == null) return;
        var work = monitor.WorkingArea; var inverse = source.CompositionTarget.TransformFromDevice;
        var right = inverse.Transform(new System.Windows.Point(work.Right, work.Top));
        var bottom = inverse.Transform(new System.Windows.Point(work.Right, work.Bottom));
        Left = right.X-Width; Top = right.Y + Math.Max(0,bottom.Y-right.Y-Height)*preferences.VerticalRatio;
    }
    private void SavePosition()
    {
        var work = monitor.WorkingArea; var inverse = source!.CompositionTarget.TransformFromDevice;
        var top = inverse.Transform(new System.Windows.Point(work.Left,work.Top)); var bottom = inverse.Transform(new System.Windows.Point(work.Right,work.Bottom));
        preferences.VerticalRatio = Math.Clamp((Top-top.Y)/Math.Max(1,bottom.Y-top.Y-Height),0,1); preferences.Save();
    }
    private async Task RefreshTrack()
    {
        if (reading || closing) return; reading=true;
        var selected = preferences.Player;
        try
        {
            var current = await media.ReadAsync(selected); if (selected != preferences.Player || closing) return;
            track = current; title.Text = current?.Title ?? "没有正在播放的音乐";
            artist.Text = current == null ? $"打开{Preferences.Name(selected)}" : $"{current.Artist} · {Preferences.Name(selected)}";
            title.ToolTip = title.Text; artist.ToolTip = media.Status;
            if ((current?.Identity ?? "") != lyricIdentity)
            {
                lyricIdentity = current?.Identity ?? ""; lyricRequest?.Cancel(); lyricRequest?.Dispose(); lyricRequest = new();
                lyricResult = LyricsResult.Empty; original.Text = current == null ? "♪ 歌词会显示在这里" : "正在查找同步歌词…"; translation.Visibility=Visibility.Collapsed;
                if (current != null) _ = LoadLyrics(current,lyricRequest.Token);
            }
        } finally { reading=false; }
    }
    private bool lyricsLoading;
    private async Task LoadLyrics(Track current, CancellationToken token)
    {
        lyricsLoading=true;
        try { var result = await lyrics.FetchAsync(current,token); if (!token.IsCancellationRequested && lyricIdentity == current.Identity) { lyricResult=result; lyricsLoading=false; RefreshLyric(); } }
        catch (OperationCanceledException) { }
    }
    private void RefreshLyric()
    {
        if (track == null || lyricsLoading) return;
        translation.Visibility = Visibility.Collapsed; original.Height=36; original.TextWrapping=TextWrapping.Wrap;
        if (lyricResult.Instrumental) { original.Text="纯音乐，请欣赏"; return; }
        if (lyricResult.Lines.Count==0) { original.Text="暂未找到同步歌词"; return; }
        var line = lyricResult.Lines.LastOrDefault(x=>x.Time<=track.LivePosition) ?? lyricResult.Lines[0]; original.Text=line.Text;
        original.ToolTip = line.Translation == null ? line.Text : line.Text + "\n" + line.Translation;
        if (!string.IsNullOrEmpty(line.Translation)) { original.Height=18; original.TextWrapping=TextWrapping.NoWrap; translation.Text=line.Translation; translation.ToolTip=line.Translation; translation.Visibility=Visibility.Visible; }
    }
    public void ApplyTheme()
    {
        var colors = Colors[preferences.Color].Select(x => (Color)ColorConverter.ConvertFromString(x)).ToArray();
        Brush brush = colors.Length == 1 ? new SolidColorBrush(colors[0]) : new LinearGradientBrush(colors[0],colors[^1],90);
        edge.Background=brush; accent.Background=brush; original.Foreground=brush;
        bool dark=preferences.WindowTheme==WindowTheme.Dark;
        surface.Background = new SolidColorBrush(dark ? Color.FromArgb(245,19,22,31) : preferences.WindowTheme==WindowTheme.Glass ? Color.FromArgb(200,226,234,239) : Color.FromArgb(250,250,250,252));
        surface.BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(35,255,255,255) : Color.FromArgb(40,0,0,0));
        title.Foreground=dark?Brushes.White:Brushes.Black; artist.Foreground=dark?Brushes.LightGray:Brushes.DimGray; translation.Foreground=dark?Brushes.LightGray:Brushes.DarkSlateGray;
        foreach (var b in content.Children.OfType<Button>()) b.Foreground=dark?Brushes.White:Brushes.Black;
        if (handle!=IntPtr.Zero) Native.Glass(handle,preferences.WindowTheme==WindowTheme.Glass);
    }
    private SettingsWindow? settings;
    private void Settings()
    {
        if (settings != null) { settings.Activate(); return; }
        settings = new SettingsWindow(preferences,()=>{ preferences.Save(); ApplyTheme(); RegisterHotkey(); lyricIdentity=""; lyricsLoading=false; _=RefreshTrack(); },()=> $"{media.Status}\n{audio.Status}\n快捷键：{(hotkeyOK?"已注册":"被占用，请更换")}");
        settings.Closed += (_,_) => settings=null; settings.Show();
    }
    private void Cleanup()
    {
        closing=true; polling.Stop(); frame.Stop(); lyricRequest?.Cancel(); lyricRequest?.Dispose(); audio.Dispose();
        Native.UnregisterHotKey(handle,1); source?.RemoveHook(WndProc); tray?.Dispose(); settings?.Close();
    }
}
