using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;
namespace EdgeTune;
public sealed record Track(Player Player, string Title, string Artist, string Album, double Duration, double Position, bool Playing, DateTimeOffset SampledAt)
{
    public string Identity => $"{Player}|{Title}|{Artist}|{Album}|{Duration:F0}";
    public double LivePosition => Position + (Playing ? Math.Max(0, (DateTimeOffset.Now - SampledAt).TotalSeconds) : 0);
}
public sealed class MediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    public string Status { get; private set; } = "等待播放器";
    public async Task<Track?> ReadAsync(Player player)
    {
        try
        {
            manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var sessions = manager.GetSessions().Where(x => Preferences.Matches(player, x.SourceAppUserModelId)).ToArray();
            session = sessions.FirstOrDefault(x => x.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing) ?? sessions.FirstOrDefault();
            if (session == null) { Status = $"等待{Preferences.Name(player)}的 Windows 媒体会话"; return null; }
            var info = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();
            bool playing = session.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            double position = Math.Max(0, (timeline.Position - timeline.StartTime).TotalSeconds);
            if (playing && timeline.LastUpdatedTime > DateTimeOffset.UnixEpoch) position += Math.Max(0, (DateTimeOffset.Now - timeline.LastUpdatedTime).TotalSeconds);
            Status = $"已连接：{session.SourceAppUserModelId}";
            if (string.IsNullOrWhiteSpace(info.Title)) return null;
            return new(player, info.Title, info.Artist, info.AlbumTitle, Math.Max(0, (timeline.EndTime - timeline.StartTime).TotalSeconds), position, playing, DateTimeOffset.Now);
        }
        catch (Exception e) { manager = null; session = null; Status = "媒体连接暂时不可用，正在重连"; Log.Write($"Media: {e.Message}"); return null; }
    }
    public async Task ControlAsync(Player player, string command)
    {
        await ReadAsync(player);
        if (session == null) return;
        try { bool ok = command switch { "previous" => await session.TrySkipPreviousAsync(), "next" => await session.TrySkipNextAsync(), _ => await session.TryTogglePlayPauseAsync() };
            if (!ok) Log.Write($"Player declined {command}"); } catch (Exception e) { Log.Write(e.Message); }
    }
}
