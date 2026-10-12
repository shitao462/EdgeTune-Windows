using System;
using System.Collections.Generic;
using System.Diagnostics;
using NAudio.CoreAudioApi;
namespace EdgeTune;
// Reads per-player Windows audio-session meters. No screen capture, audio recording or injection.
public sealed class AudioMeter : IDisposable
{
    private readonly MMDeviceEnumerator enumerator = new();
    private readonly List<MMDevice> devices = new();
    private readonly List<AudioSessionControl> sessions = new();
    private DateTime nextRefresh;
    private Player? selected;
    public string Status { get; private set; } = "等待播放器音频";
    private void Refresh(Player player)
    {
        Clear(); selected = player; nextRefresh = DateTime.UtcNow.AddSeconds(3);
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(device);
            var collection = device.AudioSessionManager.Sessions;
            for (int i = 0; i < collection.Count; i++)
            {
                var session = collection[i];
                try { using var process = Process.GetProcessById((int)session.GetProcessID);
                    if (Preferences.Matches(player, process.ProcessName)) sessions.Add(session); else session.Dispose(); }
                catch { session.Dispose(); }
            }
        }
    }
    public double Read(Player player)
    {
        try
        {
            if (selected != player || DateTime.UtcNow >= nextRefresh) Refresh(player);
            double peak = 0;
            foreach (var session in sessions) peak = Math.Max(peak, session.AudioMeterInformation.MasterPeakValue);
            Status = sessions.Count > 0 ? "已连接所选播放器实时音量" : "等待播放器音频会话";
            return peak > 0 ? Math.Clamp((20 * Math.Log10(peak) + 55) / 55, 0, 1) : 0;
        }
        catch (Exception e) { Status = "音频设备变化，正在重连"; nextRefresh = DateTime.MinValue; Log.Write($"Audio: {e.Message}"); return 0; }
    }
    private void Clear() { foreach (var session in sessions) session.Dispose(); sessions.Clear(); foreach (var device in devices) device.Dispose(); devices.Clear(); }
    public void Dispose() { Clear(); enumerator.Dispose(); }
}
