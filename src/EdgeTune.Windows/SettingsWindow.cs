using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace EdgeTune;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(Preferences preferences, Action changed, Func<string> status)
    {
        Title="EdgeTune 设置"; Width=420; Height=550; ResizeMode=ResizeMode.NoResize; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var panel=new StackPanel { Margin=new Thickness(24) }; Content=panel;
        void Label(string value) => panel.Children.Add(new TextBlock { Text=value, Margin=new Thickness(0,12,0,6), TextWrapping=TextWrapping.Wrap });
        Label("音乐播放器"); var player=new ComboBox { ItemsSource=new[]{"网易云音乐","QQ 音乐","Spotify","Apple Music"}, SelectedIndex=(int)preferences.Player };
        player.SelectionChanged+=(_,_)=>{ preferences.Player=(Player)player.SelectedIndex; changed(); }; panel.Children.Add(player);
        Label("窗口主题"); var theme=new ComboBox { ItemsSource=new[]{"黑色","白色","玻璃"}, SelectedIndex=(int)preferences.WindowTheme };
        theme.SelectionChanged+=(_,_)=>{ preferences.WindowTheme=(WindowTheme)theme.SelectedIndex; changed(); }; panel.Children.Add(theme);
        Label("主题色 / 渐变"); var color=new ComboBox { ItemsSource=OverlayWindow.ColorNames, SelectedIndex=preferences.Color };
        color.SelectionChanged+=(_,_)=>{ preferences.Color=color.SelectedIndex; changed(); }; panel.Children.Add(color);
        var pulse=new CheckBox { Content="边缘条跟随所选播放器的实时音量", IsChecked=preferences.Pulse, Margin=new Thickness(0,18,0,8) };
        pulse.Click+=(_,_)=>{ preferences.Pulse=pulse.IsChecked==true; changed(); }; panel.Children.Add(pulse);
        var startup=new CheckBox { Content="登录 Windows 后自动启动", IsChecked=Preferences.AutoStart };
        startup.Click+=(_,_)=>{ try { Preferences.AutoStart=startup.IsChecked==true; } catch(Exception e) { startup.IsChecked=Preferences.AutoStart; MessageBox.Show(e.Message,"无法更新启动项"); } }; panel.Children.Add(startup);
        Label("呼出快捷键（点击后按 Ctrl / Alt / Shift + 字母或功能键）");
        var hotkey=new TextBox { IsReadOnly=true, Text=Format(preferences), Height=28 };
        hotkey.PreviewKeyDown+=(_,e)=>{ e.Handled=true; var key=e.Key==Key.System?e.SystemKey:e.Key;
            int modifiers=0; if(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))modifiers|=1; if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control))modifiers|=2; if(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))modifiers|=4;
            if(modifiers==0 || !((key>=Key.A && key<=Key.Z)||(key>=Key.F1 && key<=Key.F12)))return;
            preferences.HotkeyModifiers=modifiers; preferences.HotkeyKey=KeyInterop.VirtualKeyFromKey(key); hotkey.Text=Format(preferences); changed(); };
        panel.Children.Add(hotkey);
        Label("LOL 请使用无边框窗口模式。浮窗不会抢占游戏焦点；独占全屏的覆盖效果需要实机验证。玻璃效果随系统支持情况回退为半透明主题。");
        var diagnostics=new TextBlock { Text=status(), TextWrapping=TextWrapping.Wrap, FontSize=11 }; panel.Children.Add(diagnostics);
        var timer=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromSeconds(1) }; timer.Tick+=(_,_)=>diagnostics.Text=status(); timer.Start(); Closed+=(_,_)=>timer.Stop();
        var logs=new Button { Content="打开诊断日志文件夹", Margin=new Thickness(0,10,0,0) }; logs.Click+=(_,_)=>{ System.IO.Directory.CreateDirectory(Log.Folder); Process.Start(new ProcessStartInfo("explorer.exe",Log.Folder){UseShellExecute=true}); }; panel.Children.Add(logs);
    }
    private static string Format(Preferences p)=>$"{((p.HotkeyModifiers&2)!=0?"Ctrl+":"")}{((p.HotkeyModifiers&1)!=0?"Alt+":"")}{((p.HotkeyModifiers&4)!=0?"Shift+":"")}{KeyInterop.KeyFromVirtualKey(p.HotkeyKey)}";
}
