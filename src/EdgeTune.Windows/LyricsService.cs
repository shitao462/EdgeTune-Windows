using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace EdgeTune;
public sealed record LyricLine(double Time, string Text, string? Translation = null);
public sealed record LyricsResult(IReadOnlyList<LyricLine> Lines, bool Instrumental = false)
{
    public static readonly LyricsResult Empty = new(Array.Empty<LyricLine>());
}
public sealed class LyricsService
{
    private static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(12) };
    public async Task<LyricsResult> FetchAsync(Track track, CancellationToken token)
    {
        try
        {
            LyricsResult? result = track.Player switch { Player.NetEase => await NetEase(track, token), Player.QQMusic => await QQ(track, token), _ => null };
            if (result != null) return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) { Log.Write($"Player lyrics fallback: {e.Message}"); }
        try
        {
            string query = $"track_name={Uri.EscapeDataString(track.Title)}&artist_name={Uri.EscapeDataString(track.Artist)}";
            string exact = query + $"&album_name={Uri.EscapeDataString(track.Album)}";
            if (track.Duration > 0) exact += "&duration=" + track.Duration.ToString("F3", CultureInfo.InvariantCulture);
            var item = await Request("https://lrclib.net/api/get?" + exact, token);
            var result = ResolveLRCLIB(item is JsonObject ? new[] { item } : Array.Empty<JsonNode?>(), track);
            if (result != null) return result;
            var search = await Request("https://lrclib.net/api/search?" + query, token);
            return ResolveLRCLIB(search is JsonArray arr ? arr : Array.Empty<JsonNode?>(), track) ?? LyricsResult.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) { Log.Write($"Lyrics: {e.Message}"); return LyricsResult.Empty; }
    }
    private async Task<LyricsResult?> NetEase(Track track, CancellationToken token)
    {
        var root = await Request("https://music.163.com/api/search/get/web?type=1&limit=20&s=" + Uri.EscapeDataString(track.Title + " " + track.Artist), token);
        var songs = (root?["result"]?["songs"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? Array.Empty<JsonObject>();
        var match = SelectSong(songs, track, false);
        if (match == null) return null;
        var lyric = await Request($"https://music.163.com/api/song/lyric?id={match["id"]}&lv=-1&tv=-1&kv=-1", token);
        if (lyric?["uncollected"]?.GetValue<bool>() == true) return null;
        return Merge(Text(lyric?["lrc"]?["lyric"]), Text(lyric?["tlyric"]?["lyric"]), lyric?["nolyric"]?.GetValue<bool>() == true);
    }
    private async Task<LyricsResult?> QQ(Track track, CancellationToken token)
    {
        var payload = new JsonObject { ["comm"] = new JsonObject { ["ct"] = 19, ["cv"] = 1859 }, ["req"] = new JsonObject {
            ["module"] = "music.search.SearchCgiService", ["method"] = "DoSearchForQQMusicDesktop",
            ["param"] = new JsonObject { ["query"] = track.Title + " " + track.Artist, ["num_per_page"] = 20, ["page_num"] = 1, ["search_type"] = 0 } } };
        var root = await Request("https://u.y.qq.com/cgi-bin/musicu.fcg", token, payload);
        if (Number(root?["code"]) != 0 || Number(root?["req"]?["code"]) != 0) return null;
        var songs = (root?["req"]?["data"]?["body"]?["song"]?["list"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? Array.Empty<JsonObject>();
        var match = SelectSong(songs, track, true);
        if (match == null) return null;
        var lyric = await Request("https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?format=json&nobase64=1&songmid=" + Uri.EscapeDataString(Text(match["mid"])), token);
        if (Number(lyric?["code"]) != 0 || Number(lyric?["retcode"]) != 0) return null;
        return Merge(Decode(Text(lyric?["lyric"])), Decode(Text(lyric?["trans"])));
    }
    public static JsonObject? SelectSong(IEnumerable<JsonObject> songs, Track track, bool qq)
    {
        var matches = songs.Where(song => {
            var artists = (song[qq ? "singer" : "artists"] as JsonArray)?.Select(a => Text(a?["name"])).ToArray() ?? Array.Empty<string>();
            bool artist = !string.IsNullOrWhiteSpace(track.Artist) && (artists.Any(a => Normalize(a) == Normalize(track.Artist)) || new[] { "/", ",", " & ", "、" }.Any(s => Normalize(string.Join(s, artists)) == Normalize(track.Artist)));
            double duration = Number(song[qq ? "interval" : "duration"]) / (qq ? 1 : 1000);
            return Normalize(Text(song["name"])) == Normalize(track.Title) && artist && track.Duration > 0 && Math.Abs(duration - track.Duration) <= 2;
        }).ToArray();
        var album = matches.Where(s => !string.IsNullOrEmpty(track.Album) && Normalize(Text(s["album"]?["name"])) == Normalize(track.Album)).ToArray();
        var candidates = album.Length > 0 ? album : matches;
        return candidates.Length == 1 ? candidates[0] : null;
    }
    private static LyricsResult? ResolveLRCLIB(IEnumerable<JsonNode?> records, Track track)
    {
        var matches = records.OfType<JsonObject>().Where(r => Normalize(Text(r["trackName"])) == Normalize(track.Title) && Normalize(Text(r["artistName"])) == Normalize(track.Artist)).ToArray();
        var precise = matches.Where(r => (!string.IsNullOrEmpty(track.Album) && Normalize(Text(r["albumName"])) == Normalize(track.Album)) || (track.Duration > 0 && Math.Abs(Number(r["duration"]) - track.Duration) <= 2)).ToArray();
        foreach (var r in precise.Length > 0 ? precise : matches) { var lines = Parse(Text(r["syncedLyrics"])); if (lines.Count > 0) return new(lines); }
        if (precise.Length > 0 && precise.All(r => r["instrumental"]?.GetValue<bool>() == true && string.IsNullOrWhiteSpace(Text(r["syncedLyrics"])) && string.IsNullOrWhiteSpace(Text(r["plainLyrics"])))) return new(Array.Empty<LyricLine>(), true);
        return null;
    }
    public static LyricsResult? Merge(string original, string translation, bool instrumental = false)
    {
        var lines = Parse(original);
        if ((instrumental && lines.Count == 0) || lines.Any(x => x.Text is "纯音乐，请欣赏" or "纯音乐，请欣赏。")) return new(Array.Empty<LyricLine>(), true);
        if (lines.Count == 0) return null;
        var translated = Parse(translation);
        return new(lines.Select(line => { var nearest = translated.OrderBy(t => Math.Abs(t.Time - line.Time)).FirstOrDefault();
            return nearest != null && Math.Abs(nearest.Time - line.Time) <= .35 && nearest.Text != line.Text && nearest.Text.Length > 0 ? line with { Translation = nearest.Text } : line; }).ToArray());
    }
    public static IReadOnlyList<LyricLine> Parse(string value)
    {
        var output = new List<LyricLine>();
        var offsetMatch = Regex.Match(value, @"\[offset:([+-]?\d+)\]");
        double offset = offsetMatch.Success ? double.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture) / 1000 : 0;
        foreach (string line in value.Split('\n'))
        {
            var stamps = Regex.Matches(line, @"\[(\d+):(\d+(?:\.\d+)?)\]");
            if (stamps.Count == 0 || stamps[0].Index != 0) continue;
            string text = line[(stamps[^1].Index + stamps[^1].Length)..].Trim();
            foreach (Match stamp in stamps) { double minutes = double.Parse(stamp.Groups[1].Value, CultureInfo.InvariantCulture), seconds = double.Parse(stamp.Groups[2].Value, CultureInfo.InvariantCulture);
                if (seconds < 60) output.Add(new(Math.Max(0, minutes * 60 + seconds - offset), text)); }
        }
        return output.OrderBy(x => x.Time).ToArray();
    }
    public static string Decode(string text)
    {
        if (!text.Contains('[')) { try { text = Encoding.UTF8.GetString(Convert.FromBase64String(text)); } catch (FormatException) { } }
        return WebUtility.HtmlDecode(text);
    }
    private static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    private static string Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : "";
    private static double Number(JsonNode? node) => double.TryParse(node?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    private static async Task<JsonNode?> Request(string url, CancellationToken token, JsonObject? body = null)
    {
        using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, url);
        request.Headers.UserAgent.ParseAdd("EdgeTune/0.1");
        if (url.Contains("music.163.com")) request.Headers.Referrer = new Uri("https://music.163.com/");
        if (url.Contains(".y.qq.com")) request.Headers.Referrer = new Uri("https://y.qq.com/");
        if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) return null;
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(token));
    }
}
