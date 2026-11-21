using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    /// <summary>Мініатюри проєктів (картинка YouTube або кадр з локального файлу) і кеш зменшених зображень для карток.</summary>
    static class Thumbs
    {
        static string Dir
        {
            get { var d = Path.Combine(AppSettings.LocalDir, "thumbs"); Directory.CreateDirectory(d); return d; }
        }

        public static string YouTubeId(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            var m = Regex.Match(url, @"(?:v=|youtu\.be/|shorts/|live/|embed/)([\w\-]{11})");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static async Task EnsureAsync(Job job, CancellationToken ct)
        {
            if (job.ThumbPath != null && File.Exists(job.ThumbPath)) return;
            if (job.IsLocal()) return;
            var id = job.VideoId ?? YouTubeId(job.Url);
            if (id == null) return;
            var path = Path.Combine(Dir, Regex.Replace(id, @"[^\w\-]", "_") + ".jpg");
            if (!File.Exists(path))
            {
                foreach (var q in new[] { "maxresdefault", "hqdefault", "mqdefault" })
                {
                    try
                    {
                        using (var resp = await Net.Client.GetAsync("https://i.ytimg.com/vi/" + id + "/" + q + ".jpg", ct))
                        {
                            if (!resp.IsSuccessStatusCode) continue;
                            var bytes = await resp.Content.ReadAsByteArrayAsync();
                            if (bytes.Length < 2000) continue;
                            File.WriteAllBytes(path, bytes);
                            break;
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { }
                }
            }
            if (File.Exists(path)) job.ThumbPath = path;
        }

        public static async Task FromVideoAsync(Job job, string video, CancellationToken ct)
        {
            var path = Path.Combine(Dir, Regex.Replace(job.VideoId ?? "local", @"[^\w\-]", "_") + ".jpg");
            if (!File.Exists(path))
            {
                double dur = await Renderer.ProbeDurationAsync(video, ct);
                var at = Math.Max(0, dur * 0.12).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                await Tools.RunAsync(Tools.FFmpeg, "-v error -y -ss " + at + " -i " + Tools.Q(video) + " -frames:v 1 -vf scale=640:-2 -q:v 3 " + Tools.Q(path), null, null, ct);
            }
            if (File.Exists(path)) job.ThumbPath = path;
        }

        // ---------- кеш зменшених картинок ----------

        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

        public static Image Get(string path, int w, int h)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key;
            try
            {
                if (!File.Exists(path)) return null;
                key = path + "|" + File.GetLastWriteTimeUtc(path).Ticks + "|" + w + "x" + h;
            }
            catch { return null; }
            Image img;
            if (cache.TryGetValue(key, out img)) return img;
            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                using (var src = Image.FromStream(ms))
                {
                    // «cover»: обрізаємо, щоб заповнити прямокутник
                    float scale = Math.Max((float)w / src.Width, (float)h / src.Height);
                    float sw = w / scale, sh = h / scale;
                    var srcRect = new RectangleF((src.Width - sw) / 2, (src.Height - sh) / 2, sw, sh);
                    var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h));
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, new RectangleF(0, 0, w, h), srcRect, GraphicsUnit.Pixel);
                    }
                    img = bmp;
                }
            }
            catch { img = null; }
            if (cache.Count > 400)
            {
                foreach (var v in cache.Values) if (v != null) v.Dispose();
                cache.Clear();
            }
            cache[key] = img;
            return img;
        }
    }
}
