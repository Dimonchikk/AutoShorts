using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    static class Renderer
    {
        static string F(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        public static async Task<Size> ProbeSizeAsync(string video, CancellationToken ct)
        {
            var r = await Tools.RunAsync(Tools.FFprobe,
                "-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0:s=x " + Tools.Q(video), null, null, ct);
            var line = (r.Output ?? "").Trim().Split('\n')[0].Trim();
            var p = line.Split('x');
            int w, h;
            if (p.Length >= 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h)) return new Size(w, h);
            return new Size(1920, 1080);
        }

        public static async Task<List<string>> ExtractFramesAsync(string video, double start, double dur, string dir, CancellationToken ct)
        {
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f_*.jpg")) { try { File.Delete(f); } catch { } }
            double step = Math.Max(1, dur / 30);
            var args = "-v error -ss " + F(start) + " -t " + F(dur) + " -i " + Tools.Q(video) +
                       " -vf \"fps=1/" + F(step) + ",scale=480:-2\" -q:v 4 " + Tools.Q(Path.Combine(dir, "f_%03d.jpg"));
            await Tools.RunAsync(Tools.FFmpeg, args, null, null, ct);
            return Directory.GetFiles(dir, "f_*.jpg").OrderBy(x => x).ToList();
        }

        static string CropFilter(Size src, double faceX)
        {
            int cropW = (int)Math.Round(src.Height * 9.0 / 16.0) / 2 * 2;
            if (cropW <= src.Width)
            {
                double cx = faceX >= 0 ? faceX : 0.5;
                int x = (int)Math.Round(cx * src.Width - cropW / 2.0);
                x = Math.Max(0, Math.Min(src.Width - cropW, x));
                return "crop=" + cropW + ":" + src.Height + ":" + x + ":0,scale=1080:1920,setsar=1";
            }
            // вже вертикальне чи вужче — вписуємо з розмитим фоном
            return "split[a][b];[a]scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,boxblur=20:2[bg];" +
                   "[b]scale=1080:1920:force_original_aspect_ratio=decrease[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2,setsar=1";
        }

        public static async Task RenderAsync(string video, Size src, ClipItem clip, List<Word> clipWords, SubStyle style,
                                             bool hookTitle, string outPath, Action<int> progress, CancellationToken ct)
        {
            var dir = Path.GetDirectoryName(outPath);
            Directory.CreateDirectory(dir);
            var assName = Path.GetFileNameWithoutExtension(outPath) + ".ass";
            var keywords = new HashSet<string>(Metadata.Keywords(clip.Text, 6));
            var ass = SubtitleStyles.BuildAss(clipWords, style, hookTitle ? clip.Hook : null, clip.Duration, keywords);
            File.WriteAllText(Path.Combine(dir, assName), ass, new UTF8Encoding(false));

            var vf = CropFilter(src, clip.FaceX);
            if (clipWords.Count > 0 || hookTitle) vf += ",subtitles=" + assName;
            vf += "[v]";
            var tmp = outPath + ".tmp.mp4";
            var args = "-y -v error -nostats -progress pipe:1 -ss " + F(clip.Start) + " -t " + F(clip.Duration) +
                       " -i " + Tools.Q(video) + " -filter_complex \"" + vf + "\" -map \"[v]\" -map 0:a? -sn -dn" +
                       " -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -r 30 -c:a aac -b:a 160k -ar 48000" +
                       " -movflags +faststart " + Tools.Q(tmp);
            double total = clip.Duration;
            var r = await Tools.RunAsync(Tools.FFmpeg, args, dir, line =>
            {
                if (line.StartsWith("out_time_ms=") && progress != null)
                {
                    long us;
                    if (long.TryParse(line.Substring(12), out us) && total > 0)
                        progress((int)Math.Min(100, us / 1e6 / total * 100));
                }
            }, ct);
            if (r.ExitCode != 0 || !File.Exists(tmp))
                throw new Exception("FFmpeg не зміг зробити кліп: " + r.Tail(500));
            if (File.Exists(outPath)) File.Delete(outPath);
            File.Move(tmp, outPath);
        }

        /// <summary>Найяскравіший контрастний кадр.</summary>
        public static int BrightestFrame(List<string> frames)
        {
            int best = -1; double bestScore = -1;
            for (int i = 0; i < frames.Count; i++)
            {
                try
                {
                    using (var bmp = new Bitmap(frames[i]))
                    {
                        double sum = 0, sum2 = 0; int n = 0;
                        for (int y = 0; y < bmp.Height; y += 6)
                            for (int x = 0; x < bmp.Width; x += 6)
                            {
                                var c = bmp.GetPixel(x, y);
                                double l = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                                sum += l; sum2 += l * l; n++;
                            }
                        if (n == 0) continue;
                        double mean = sum / n;
                        double sd = Math.Sqrt(Math.Max(0, sum2 / n - mean * mean));
                        double score = mean * (1 - Math.Max(0, mean - 200) / 55.0) + sd * 0.8;
                        if (score > bestScore) { bestScore = score; best = i; }
                    }
                }
                catch { }
            }
            return best;
        }

        public static async Task MakeCoverAsync(string video, Size src, ClipItem clip, double atSecond, string title, string outPath, CancellationToken ct)
        {
            var raw = outPath + ".raw.jpg";
            var vf = CropFilter(src, clip.FaceX);
            var args = "-y -v error -ss " + F(clip.Start + atSecond) + " -i " + Tools.Q(video) +
                       " -frames:v 1 -filter_complex \"" + vf + "\" -q:v 2 " + Tools.Q(raw);
            var r = await Tools.RunAsync(Tools.FFmpeg, args, null, null, ct);
            if (!File.Exists(raw)) throw new Exception("Не вдалося зробити обкладинку: " + r.Tail(300));
            await Task.Run(() => DrawTitle(raw, title, outPath));
            try { File.Delete(raw); } catch { }
        }

        static void DrawTitle(string rawPath, string title, string outPath)
        {
            using (var img = Image.FromFile(rawPath))
            using (var bmp = new Bitmap(1080, 1920))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, 0, 0, 1080, 1920);
                using (var shade = new LinearGradientBrush(new Rectangle(0, 0, 1080, 900), Color.FromArgb(170, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90f))
                    g.FillRectangle(shade, 0, 0, 1080, 900);

                var text = (title ?? "").Replace("#shorts", "").Trim().ToUpperInvariant();
                if (text.Length > 0)
                {
                    var family = FontFamily.Families.Any(f => f.Name == "Arial Black") ? new FontFamily("Arial Black") : FontFamily.GenericSansSerif;
                    float size = 104;
                    var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
                    var area = new RectangleF(60, 220, 960, 900);
                    var style = family.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
                    // зменшуємо, поки текст не влізе
                    while (size > 50)
                    {
                        using (var f = new Font(family, size, style, GraphicsUnit.Pixel))
                        {
                            var m = g.MeasureString(text, f, (int)area.Width, fmt);
                            if (m.Height <= 620) break;
                        }
                        size -= 6;
                    }
                    using (var path = new GraphicsPath())
                    {
                        path.AddString(text, family, (int)style, size, area, fmt);
                        using (var pen = new Pen(Color.Black, 16) { LineJoin = LineJoin.Round })
                            g.DrawPath(pen, path);
                        using (var fill = new SolidBrush(Color.FromArgb(255, 255, 229, 0)))
                            g.FillPath(fill, path);
                    }
                }
                var enc = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                using (var ps = new EncoderParameters(1))
                {
                    ps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                    bmp.Save(outPath, enc, ps);
                }
            }
        }
    }
}
