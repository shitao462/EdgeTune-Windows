using System;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using EdgeTune;
var lines = LyricsService.Parse("[offset:100]\n[00:01.00][00:03.00]Repeat\n[00:04.00]\n[00:05.00]Next");
Check(lines.Count == 4 && Math.Abs(lines[0].Time-.9)<.001 && lines[2].Text=="", "timestamps, offset and gaps");
var translated=LyricsService.Merge("[00:01.00]Original\n[00:02.00]Next","[00:01.12]中文\n[00:20.00]Wrong")!;
Check(translated.Lines[0].Translation=="中文" && translated.Lines[1].Translation==null,"translation alignment");
Check(LyricsService.Merge("","")==null,"missing lyrics are not instrumental");
Check(LyricsService.Merge("[00:00.00]纯音乐，请欣赏","")!.Instrumental,"explicit instrumental");
Check(LyricsService.Decode(Convert.ToBase64String(Encoding.UTF8.GetBytes("[00:01.00]It&#39;s &amp; good")))=="[00:01.00]It's & good","Base64 and entities");
var track=new Track(Player.QQMusic,"Song","Artist","Album",100,0,false,DateTimeOffset.Now);
var exact=JsonNode.Parse("{\"name\":\"Song\",\"singer\":[{\"name\":\"Artist\"}],\"interval\":100,\"album\":{\"name\":\"Album\"},\"mid\":\"good\"}")!.AsObject();
var wrong=exact.DeepClone().AsObject(); wrong["interval"]=200;
Check(LyricsService.SelectSong(new[]{wrong,exact},track,true)==exact,"version match");
Check(LyricsService.SelectSong(new[]{exact,exact.DeepClone().AsObject()},track,true)==null,"ambiguity");
Console.WriteLine("All lyric regression checks passed.");
static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
namespace EdgeTune
{
    public enum Player { NetEase, QQMusic, Spotify, AppleMusic }
    public sealed record Track(Player Player,string Title,string Artist,string Album,double Duration,double Position,bool Playing,DateTimeOffset SampledAt);
    internal static class Log { public static void Write(string message) => Console.WriteLine(message); }
}
