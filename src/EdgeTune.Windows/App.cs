using System;
using System.Threading;
using System.Windows;
using System.IO;
namespace EdgeTune;
public sealed class App : Application
{
    private static Mutex? instance;
    [STAThread] public static void Main()
    {
        instance = new Mutex(true, "Local\\EdgeTune.Windows", out bool first);
        if (!first) return;
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Log.Write(e.Exception.ToString()); e.Handled = true; };
        app.Run(new OverlayWindow());
        instance.Dispose();
    }
}
internal static class Log
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdgeTune");
    public static void Write(string message)
    {
        try { Directory.CreateDirectory(Folder); var file = Path.Combine(Folder, "diagnostics.log");
            if (File.Exists(file) && new FileInfo(file).Length > 1_000_000) File.WriteAllText(file, "");
            File.AppendAllText(file, $"{DateTimeOffset.Now:O} {message}\n"); } catch { }
    }
}
