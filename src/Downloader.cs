using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    public class VideoInfo
    {
        public string Id;
        public string Title;
        public string Url;
        public string Channel;
        public string Description;
        public string Language;
        public double Duration;
        public List<string> Tags = new List<string>();
        public List<HeatPoint> Heatmap = new List<HeatPoint>();
        public string CaptionUrl;
        public string CaptionLang;
        public bool CaptionAuto;
        public object Raw;
        public List<string> CaptionLangs = new List<string>();   // мови оригінальних субтитрів
    }

    static class Downloader
    {
        public static string WorkDir(string videoId)
        {
            var safe = Regex.Replace(videoId ?? "video", @"[^\w\-]", "_");
            var d = Path.Combine(AppSettings.LocalDir, "work", safe);
            Directory.CreateDirectory(d);
            return d;
        }

        public static async Task<VideoInfo> GetInfoAsync(string url, CancellationToken ct)
        {
            var args = "-J --no-playlist --no-warnings " + Tools.Q(url);
            var r = await Tools.RunAsync(Tools.YtDlp, args, null, null, ct);
            var json = (r.Output ?? "").Trim();
            int brace = json.IndexOf('{');
            if (r.ExitCode != 0 || brace < 0)
                throw new Exception("yt-dlp не зміг прочитати відео: " + r.Tail(400));
            var o = Json.Parse(json.Substring(brace));

            var info = new VideoInfo();
            info.Id = Json.Str(o, "id");
            info.Title = Json.Str(o, "title") ?? info.Id;
            info.Url = Json.Str(o, "webpage_url") ?? url;
            info.Channel = Json.Str(o, "channel") ?? Json.Str(o, "uploader") ?? "";
            info.Description = Json.Str(o, "description") ?? "";
            info.Language = Json.Str(o, "language");
            info.Duration = Json.Num(o, "duration", 0);
            foreach (var t in Json.Arr(o, "tags"))
                if (t != null) info.Tags.Add(t.ToString());
            foreach (var h in Json.Arr(o, "heatmap"))
            {
                var hp = new HeatPoint();
                hp.Start = Json.Num(h, "start_time", 0);
                hp.End = Json.Num(h, "end_time", 0);
                hp.Value = Json.Num(h, "value", 0);
                info.Heatmap.Add(hp);
            }
            info.Raw = o;
            PickCaptions(o, info, null, null);
            return info;
        }

        static string Json3Url(object list)
        {
            foreach (var f in Json.AsArr(list))
                if (Json.Str(f, "ext") == "json3") return Json.Str(f, "url");
            return null;
        }

        /// <summary>Вибір мови субтитрів (null — автоматично) і автопереклад YouTube (null — без перекладу).</summary>
        public static void ChooseCaptions(VideoInfo info, string lang, string translateTo)
        {
            if (info.Raw == null) return;
            info.CaptionUrl = null;
            PickCaptions(info.Raw, info, lang, translateTo);
        }

        static void PickCaptions(object o, VideoInfo info, string prefer, string translateTo)
        {
            var manual = Json.Get(o, "subtitles") as IDictionary<string, object>;
            var auto = Json.Get(o, "automatic_captions") as IDictionary<string, object>;
            info.CaptionLangs.Clear();
            if (manual != null) foreach (var k in manual.Keys) if (k != "live_chat" && !info.CaptionLangs.Contains(k)) info.CaptionLangs.Add(k);
            if (auto != null) foreach (var k in auto.Keys) if (k.EndsWith("-orig")) { var l = k.Replace("-orig", ""); if (!info.CaptionLangs.Contains(l)) info.CaptionLangs.Add(l); }
            if (!string.IsNullOrEmpty(translateTo) && auto != null && auto.ContainsKey(translateTo))
            {
                var tu = Json3Url(auto[translateTo]);
                if (tu != null)
                {
                    info.CaptionUrl = tu;
                    info.CaptionLang = translateTo + "-tr";
                    info.CaptionAuto = true;
                    return;
                }
            }
            var lang = string.IsNullOrEmpty(prefer) ? info.Language : prefer;
            var tries = new List<Tuple<IDictionary<string, object>, string, bool>>();

            if (!string.IsNullOrEmpty(lang))
            {
                if (manual != null)
                    foreach (var k in manual.Keys)
                        if (k == lang || k.StartsWith(lang + "-")) tries.Add(Tuple.Create(manual, k, false));
                if (auto != null)
                {
                    if (auto.ContainsKey(lang + "-orig")) tries.Add(Tuple.Create(auto, lang + "-orig", true));
                    if (auto.ContainsKey(lang)) tries.Add(Tuple.Create(auto, lang, true));
                }
            }
            if (auto != null)
                foreach (var k in auto.Keys)
                    if (k.EndsWith("-orig")) tries.Add(Tuple.Create(auto, k, true));
            if (manual != null)
                foreach (var k in manual.Keys)
                    if (k != "live_chat") tries.Add(Tuple.Create(manual, k, false));
            if (auto != null)
                foreach (var k in new[] { "uk", "ru", "en" })
                    if (auto.ContainsKey(k)) tries.Add(Tuple.Create(auto, k, true));

            foreach (var t in tries)
            {
                var u = Json3Url(t.Item1[t.Item2]);
                if (u != null)
                {
                    info.CaptionUrl = u;
                    info.CaptionLang = t.Item2.Replace("-orig", "");
                    info.CaptionAuto = t.Item3;
                    return;
                }
            }
        }

        public static async Task<List<Word>> GetWordsAsync(VideoInfo info, string workDir, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(info.CaptionUrl)) return new List<Word>();
            var cache = Path.Combine(workDir, "captions." + info.CaptionLang + ".json3");
            string text;
            if (File.Exists(cache) && new FileInfo(cache).Length > 50)
            {
                text = File.ReadAllText(cache, Encoding.UTF8);
            }
            else
            {
                text = null;
                for (int attempt = 0; attempt < 3 && text == null; attempt++)
                {
                    try
                    {
                        text = await Net.Client.GetStringAsync(info.CaptionUrl);
                    }
                    catch
                    {
                        if (attempt == 2) throw;
                    }
                    if (text == null) await Task.Delay(2000 * (attempt + 1), ct);
                }
                File.WriteAllText(cache, text, Encoding.UTF8);
            }
            return Captions.ParseJson3(text);
        }

        public static async Task<string> DownloadVideoAsync(VideoInfo info, string workDir, Action<int> progress, CancellationToken ct)
        {
            var target = Path.Combine(workDir, "source.mp4");
            if (File.Exists(target) && new FileInfo(target).Length > 100000) return target;
            foreach (var f in Directory.GetFiles(workDir, "source.*")) { try { File.Delete(f); } catch { } }

            var args = "-f \"bv*[height<=1080][vcodec^=avc1]+ba[ext=m4a]/bv*[height<=1080]+ba/b[height<=1080]/b\"" +
                       " --merge-output-format mp4 --no-playlist --no-warnings --newline --no-part" +
                       " --ffmpeg-location " + Tools.Q(Tools.Dir) +
                       " -o " + Tools.Q(Path.Combine(workDir, "source.%(ext)s")) + " " + Tools.Q(info.Url);
            var rx = new Regex(@"\[download\]\s+([\d\.]+)%");
            var r = await Tools.RunAsync(Tools.YtDlp, args, workDir, line =>
            {
                var m = rx.Match(line);
                double p;
                if (m.Success && progress != null && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out p))
                    progress((int)p);
            }, ct);
            if (!File.Exists(target))
            {
                var any = Directory.GetFiles(workDir, "source.*").FirstOrDefault(f => !f.EndsWith(".part") && !f.EndsWith(".ytdl"));
                if (any != null) { File.Move(any, target); }
            }
            if (!File.Exists(target))
                throw new Exception("Не вдалося завантажити відео: " + r.Tail(400));
            return target;
        }

        /// <summary>Копіює локальне відео в робочу папку як source.mp4 (перепаковує без перекодування, якщо це не mp4).</summary>
        public static async Task<string> ImportLocalAsync(string path, string workDir, CancellationToken ct)
        {
            var target = Path.Combine(workDir, "source.mp4");
            if (File.Exists(target) && new FileInfo(target).Length > 100000) return target;
            if (string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Run(() => File.Copy(path, target, true), ct);
                return target;
            }
            var r = await Tools.RunAsync(Tools.FFmpeg, "-v error -y -i " + Tools.Q(path) + " -map 0:v:0 -map 0:a:0? -c:v copy -c:a aac -b:a 192k -movflags +faststart " + Tools.Q(target), workDir, null, ct);
            if (r.ExitCode != 0 || !File.Exists(target))
            {
                r = await Tools.RunAsync(Tools.FFmpeg, "-v error -y -i " + Tools.Q(path) + " -map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 192k -movflags +faststart " + Tools.Q(target), workDir, null, ct);
                if (r.ExitCode != 0 || !File.Exists(target)) throw new Exception("FFmpeg не зміг прочитати файл: " + r.Tail(300));
            }
            return target;
        }
    }
}
