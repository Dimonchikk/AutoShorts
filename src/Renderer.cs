using System;
using System.Text.RegularExpressions;
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

        public static async Task<double> ProbeDurationAsync(string video, CancellationToken ct)
        {
            var r = await Tools.RunAsync(Tools.FFprobe, "-v error -show_entries format=duration -of csv=p=0 " + Tools.Q(video), null, null, ct);
            double d;
            double.TryParse((r.Output ?? "").Trim().Split('\n')[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d);
            return d;
        }

        public static async Task<List<string>> ExtractFramesAsync(string video, double start, double dur, string dir, CancellationToken ct, double step = 0)
        {
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "f_*.jpg")) { try { File.Delete(f); } catch { } }
            if (step <= 0) step = Math.Max(1, dur / 30);
            var args = "-v error -ss " + F(start) + " -t " + F(dur) + " -i " + Tools.Q(video) +
                       " -vf \"fps=1/" + F(step) + ",scale=480:-2\" -q:v 4 " + Tools.Q(Path.Combine(dir, "f_%03d.jpg"));
            await Tools.RunAsync(Tools.FFmpeg, args, null, null, ct);
            return Directory.GetFiles(dir, "f_*.jpg").OrderBy(x => x).ToList();
        }

        static string CropFilter(Size src, double faceX, string facePath = null)
        {
            int cropW = (int)Math.Round(src.Height * 9.0 / 16.0) / 2 * 2;
            if (cropW <= src.Width && !string.IsNullOrEmpty(facePath))
            {
                // кадр рухається за обличчям: кусково-лінійна траєкторія x(t)
                var pts = new List<double[]>();
                foreach (var part in facePath.Split(';'))
                {
                    var kv = part.Split(':');
                    double t, x;
                    if (kv.Length == 2 && double.TryParse(kv[0], NumberStyles.Float, CultureInfo.InvariantCulture, out t) &&
                        double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
                        pts.Add(new[] { t, Math.Max(0, Math.Min(src.Width - cropW, Math.Round(x * src.Width - cropW / 2.0))) });
                }
                if (pts.Count >= 2)
                {
                    string expr = Num(pts[pts.Count - 1][1]);
                    for (int i = pts.Count - 2; i >= 0; i--)
                    {
                        double t0 = pts[i][0], x0 = pts[i][1], t1 = pts[i + 1][0], x1 = pts[i + 1][1];
                        string seg = Math.Abs(x1 - x0) < 0.5 ? Num(x0) : Num(x0) + "+" + Num(x1 - x0) + "*(t-" + Num(t0) + ")/" + Num(Math.Max(0.01, t1 - t0));
                        expr = "if(lt(t," + Num(t1) + ")," + seg + "," + expr + ")";
                    }
                    return "crop=" + cropW + ":" + src.Height + ":x='" + expr + "':y=0,scale=1080:1920,setsar=1";
                }
            }
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

        const string FitFilter = "split[a][b];[a]scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,boxblur=24:2,eq=brightness=-0.08[bg];" +
                                 "[b]scale=1080:1920:force_original_aspect_ratio=decrease[fg];[bg][fg]overlay=(W-w)/2:(H-h)/2,setsar=1";

        static string Num(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        static readonly Regex MeanVolRx = new Regex(@"mean_volume:\s*(-?[\d\.]+) dB");
        static readonly Regex SilStartRx = new Regex(@"silence_start:\s*(-?[\d\.]+)");
        static readonly Regex SilEndRx = new Regex(@"silence_end:\s*(-?[\d\.]+)");

        /// <summary>
        /// Паузи всередині кліпу (як silence_gaps у ViralCut): тихіше за середню гучність кліпу на 10 дБ довше 0.45 с.
        /// Повертає план склейки (час відносно початку кліпу) або null, якщо вирізати нічого.
        /// </summary>
        public static async Task<List<Smart.Span>> PauseSpansAsync(string video, double start, double dur, CancellationToken ct)
        {
            var common = "-hide_banner -nostats -ss " + F(start) + " -t " + F(dur) + " -i " + Tools.Q(video) + " -vn -sn -dn ";
            var r1 = await Tools.RunAsync(Tools.FFmpeg, common + "-af volumedetect -f null -", null, null, ct);
            var mv = MeanVolRx.Match((r1.Error ?? "") + (r1.Output ?? ""));
            double mean;
            if (!mv.Success || !double.TryParse(mv.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out mean) || mean < -70) return null;
            double noise = Math.Max(-60, mean - 10);
            var r2 = await Tools.RunAsync(Tools.FFmpeg, common + "-af silencedetect=noise=" + Num(noise) + "dB:d=0.45 -f null -", null, null, ct);
            var text = (r2.Error ?? "") + (r2.Output ?? "");
            var starts = SilStartRx.Matches(text).Cast<Match>().Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            var ends = SilEndRx.Matches(text).Cast<Match>().Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            var gaps = new List<Smart.Span>();
            for (int i = 0; i < starts.Count; i++)
                gaps.Add(new Smart.Span { Start = Math.Max(0, starts[i]), End = i < ends.Count ? Math.Min(dur, ends[i]) : dur });
            // паузи на самих краях кліпу не чіпаємо — межі вже стоять на паузах між реченнями
            gaps = gaps.Where(g => g.End - g.Start >= 0.45 && g.Start > 0.3 && g.End < dur - 0.3).ToList();
            if (gaps.Count == 0) return null;
            var plan = Smart.CutPlan(0, dur, gaps);
            double kept = plan.Sum(x => x.Length);
            if (plan.Count < 2 || dur - kept < 0.4) return null;
            return plan;
        }

        // Автонаближення: кожне друге речення трохи ближче — як ручний монтаж «jump zoom»
        static string ZoomFilter(List<Word> words, double duration)
        {
            var segs = new List<string>();
            double segStart = 0;
            int n = 0;
            for (int i = 0; i < words.Count; i++)
            {
                bool end = i == words.Count - 1 || Captions.EndsSentence(words[i].Text) || words[i + 1].Start - words[i].End > 0.5;
                if (!end) continue;
                double segEnd = i + 1 < words.Count ? words[i + 1].Start : duration;
                if (n % 2 == 1 && segEnd - segStart > 0.6) segs.Add("between(it," + Num(segStart) + "," + Num(segEnd) + ")");
                segStart = segEnd;
                n++;
                if (segs.Count >= 24) break;
            }
            if (segs.Count == 0)
            {
                for (double t = 4; t < duration && segs.Count < 24; t += 8) segs.Add("between(it," + Num(t) + "," + Num(Math.Min(duration, t + 4)) + ")");
            }
            return "zoompan=z='if(" + string.Join("+", segs) + ",1.12,1)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1080x1920:fps=30";
        }

        static string LogoOverlay(string pos)
        {
            switch (pos)
            {
                case "Ліворуч угорі": return "overlay=48:64";
                case "Ліворуч унизу": return "overlay=48:H-h-260";
                case "Праворуч унизу": return "overlay=W-w-48:H-h-260";
                default: return "overlay=W-w-48:64";
            }
        }

        public static async Task RenderAsync(string video, Size src, ClipItem clip, List<Word> clipWords, SubStyle style,
                                             Recipe recipe, string outPath, Action<int> progress, CancellationToken ct)
        {
            if (recipe == null) recipe = new Recipe();
            var dir = Path.GetDirectoryName(outPath);
            Directory.CreateDirectory(dir);
            var assName = Path.GetFileNameWithoutExtension(outPath) + ".ass";

            // вирізання пауз (з ViralCut): план склейки й слова під новий час
            List<Smart.Span> spans = null;
            if (recipe.CutPauses)
            {
                try { spans = await PauseSpansAsync(video, clip.Start, clip.Duration, ct); }
                catch (OperationCanceledException) { throw; }
                catch { spans = null; }
            }
            double outDur = spans == null ? clip.Duration : spans.Sum(x => x.Length);
            clip.OutDuration = outDur;
            var words = spans == null ? clipWords : Smart.RemapWords(clipWords, spans);

            // шрифт (з ViralCut): копіюємо файл поруч, щоб libass його знайшов
            string fontsArg = "";
            var font = Fonts.Find(recipe.FontName);
            if (font != null)
            {
                try
                {
                    var fp = await Fonts.EnsureAsync(font, ct);
                    var fdir = Path.Combine(dir, "fonts");
                    Directory.CreateDirectory(fdir);
                    var dst = Path.Combine(fdir, font.File);
                    if (!File.Exists(dst)) File.Copy(fp, dst, true);
                    style = style.With(font.Family, font.Bold);
                    fontsArg = ":fontsdir=fonts";
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            var keywords = new HashSet<string>(Metadata.Keywords(clip.Text, 6));
            var capWords = recipe.Captions ? words : new List<Word>();
            var hook = recipe.Hook ? clip.Hook : null;
            var ass = SubtitleStyles.BuildAss(capWords, style, hook, outDur, keywords, recipe.HookStyle);
            File.WriteAllText(Path.Combine(dir, assName), ass, new UTF8Encoding(false));

            bool logo = !string.IsNullOrEmpty(recipe.LogoPath) && File.Exists(recipe.LogoPath);
            var frame = recipe.Layout == "fit" ? FitFilter : CropFilter(src, clip.FaceX, recipe.TrackFace ? clip.FacePath : null);
            if (recipe.ColorBoost) frame += ",eq=contrast=1.07:saturation=1.2:brightness=0.015:gamma=0.98,unsharp=5:5:0.45:5:5:0";

            var af = new List<string>();
            if (recipe.EnhanceAudio) af.Add("highpass=f=70,afftdn=nf=-25,loudnorm=I=-14:TP=-1.5:LRA=11");
            if (recipe.Fades) af.Add("afade=t=in:st=0:d=0.2,afade=t=out:st=" + Num(Math.Max(0, outDur - 0.35)) + ":d=0.35");

            string vf, audioMap, afArg = "";
            if (spans != null)
            {
                // кадр → розрізати на відрізки → склеїти разом зі звуком
                int n = spans.Count;
                var sb = new StringBuilder();
                sb.Append("[0:v]").Append(frame).Append(",split=").Append(n);
                for (int i = 0; i < n; i++) sb.Append("[cs").Append(i).Append("]");
                sb.Append(";[0:a]asplit=").Append(n);
                for (int i = 0; i < n; i++) sb.Append("[ca").Append(i).Append("]");
                for (int i = 0; i < n; i++)
                {
                    sb.Append(";[cs").Append(i).Append("]trim=").Append(Num(spans[i].Start)).Append(":").Append(Num(spans[i].End)).Append(",setpts=PTS-STARTPTS[cv").Append(i).Append("]");
                    sb.Append(";[ca").Append(i).Append("]atrim=").Append(Num(spans[i].Start)).Append(":").Append(Num(spans[i].End)).Append(",asetpts=PTS-STARTPTS[cx").Append(i).Append("]");
                }
                sb.Append(";");
                for (int i = 0; i < n; i++) sb.Append("[cv").Append(i).Append("][cx").Append(i).Append("]");
                sb.Append("concat=n=").Append(n).Append(":v=1:a=1[vcat][acat];[acat]").Append(af.Count > 0 ? string.Join(",", af) : "anull").Append("[aout];[vcat]");
                vf = sb.ToString();
                audioMap = " -map \"[aout]\"";
                bool first = true;
                Func<string> sep = () => { if (first) { first = false; return ""; } return ","; };
                if (recipe.AutoZoom) vf += sep() + ZoomFilter(words, outDur);
                if (capWords.Count > 0 || hook != null) vf += sep() + "subtitles=" + assName + fontsArg;
                if (first) vf += "null";
            }
            else
            {
                vf = "[0:v]" + frame;
                if (recipe.AutoZoom) vf += "," + ZoomFilter(words, outDur);
                if (capWords.Count > 0 || hook != null) vf += ",subtitles=" + assName + fontsArg;
                audioMap = " -map 0:a?";
                afArg = af.Count > 0 ? " -af \"" + string.Join(",", af) + "\"" : "";
            }
            if (logo)
                vf += "[base];[1:v]scale=220:-1,format=rgba,colorchannelmixer=aa=0.9[lg];[base][lg]" + LogoOverlay(recipe.LogoPos);
            if (recipe.Fades)
                vf += ",fade=t=in:st=0:d=0.25,fade=t=out:st=" + Num(Math.Max(0, outDur - 0.35)) + ":d=0.35";
            vf += "[v]";
            var inputs = " -i " + Tools.Q(video) + (logo ? " -i " + Tools.Q(recipe.LogoPath) : "");
            var tmp = outPath + ".tmp.mp4";
            var args = "-y -v error -nostats -progress pipe:1 -ss " + F(clip.Start) + " -t " + F(clip.Duration) +
                       inputs + " -filter_complex \"" + vf + "\" -map \"[v]\"" + audioMap + afArg + " -sn -dn -t " + F(outDur) +
                       " -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -r 30 -c:a aac -b:a 160k -ar 48000" +
                       " -movflags +faststart " + Tools.Q(tmp);
            double total = outDur;
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
