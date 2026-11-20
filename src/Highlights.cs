using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    static class Highlights
    {
        /// <summary>Гучність (RMS) кожної секунди аудіо через FFmpeg, без жодних моделей.</summary>
        public static async Task<double[]> LoudnessAsync(string video, string workDir, CancellationToken ct)
        {
            var cache = Path.Combine(workDir, "loudness.bin");
            if (File.Exists(cache))
            {
                var b = File.ReadAllBytes(cache);
                var r = new double[b.Length / 8];
                for (int i = 0; i < r.Length; i++) r[i] = BitConverter.ToDouble(b, i * 8);
                if (r.Length > 0) return r;
            }
            const int rate = 8000;
            var list = new List<double>();
            var args = "-v error -i " + Tools.Q(video) + " -vn -ac 1 -ar " + rate + " -f s16le -";
            await Tools.RunBinaryAsync(Tools.FFmpeg, args, stream =>
            {
                var buf = new byte[rate * 2];
                int filled = 0;
                double sum = 0; int count = 0;
                int n;
                while ((n = stream.Read(buf, filled, buf.Length - filled)) > 0)
                {
                    filled += n;
                    int samples = filled / 2;
                    for (int i = 0; i < samples; i++)
                    {
                        double v = BitConverter.ToInt16(buf, i * 2) / 32768.0;
                        sum += v * v; count++;
                        if (count == rate) { list.Add(Math.Sqrt(sum / count)); sum = 0; count = 0; }
                    }
                    int rest = filled - samples * 2;
                    if (rest > 0) buf[0] = buf[filled - 1];
                    filled = rest;
                }
                if (count > rate / 4) list.Add(Math.Sqrt(sum / count));
            }, ct);
            var res = list.ToArray();
            var bytes = new byte[res.Length * 8];
            for (int i = 0; i < res.Length; i++) Array.Copy(BitConverter.GetBytes(res[i]), 0, bytes, i * 8, 8);
            File.WriteAllBytes(cache, bytes);
            return res;
        }

        static double[] Normalize(double[] a)
        {
            double max = a.Length == 0 ? 0 : a.Max();
            double min = a.Length == 0 ? 0 : a.Min();
            var r = new double[a.Length];
            if (max - min < 1e-9) return r;
            for (int i = 0; i < a.Length; i++) r[i] = (a[i] - min) / (max - min);
            return r;
        }

        static double[] Smooth(double[] a, int radius)
        {
            var r = new double[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                double s = 0; int c = 0;
                for (int j = Math.Max(0, i - radius); j <= Math.Min(a.Length - 1, i + radius); j++) { s += a[j]; c++; }
                r[i] = c == 0 ? 0 : s / c;
            }
            return r;
        }

        /// <summary>
        /// Шукає найцікавіші фрагменти: графік «найчастіше переглядають», сплески гучності і щільність мови.
        /// </summary>
        public static List<ClipItem> Find(double duration, List<HeatPoint> heat, double[] loud, List<Word> words,
                                          int count, int minLen, int maxLen)
        {
            int n = (int)Math.Ceiling(Math.Max(duration, loud.Length));
            if (n < minLen) { minLen = Math.Max(5, n - 1); maxLen = Math.Max(minLen, n); }
            var heatS = new double[n];
            foreach (var h in heat)
                for (int i = (int)h.Start; i < Math.Min(n, (int)Math.Ceiling(h.End)); i++) heatS[i] = Math.Max(heatS[i], h.Value);
            bool hasHeat = heat.Count > 0 && heatS.Max() > 0;
            heatS = Normalize(Smooth(heatS, 2));

            // гучність: рівень відносно сусідніх 20 секунд (сплеск) + загальний рівень
            var l = new double[n];
            for (int i = 0; i < n; i++) l[i] = i < loud.Length ? loud[i] : 0;
            var local = Smooth(l, 10);
            var spike = new double[n];
            for (int i = 0; i < n; i++) spike[i] = local[i] > 1e-6 ? Math.Max(0, l[i] / local[i] - 1) : 0;
            var loudN = Normalize(l);
            var spikeN = Normalize(Smooth(spike, 1));

            var speech = new double[n];
            foreach (var w in words) { int i = (int)w.Start; if (i >= 0 && i < n) speech[i] += 1; }
            bool hasWords = words.Count > 10;
            speech = Normalize(Smooth(speech, 3));

            var score = new double[n];
            for (int i = 0; i < n; i++)
            {
                double audio = 0.5 * loudN[i] + 0.5 * spikeN[i];
                if (hasHeat && hasWords) score[i] = 0.5 * heatS[i] + 0.3 * audio + 0.2 * speech[i];
                else if (hasHeat) score[i] = 0.6 * heatS[i] + 0.4 * audio;
                else if (hasWords) score[i] = 0.6 * audio + 0.4 * speech[i];
                else score[i] = audio;
                if (i < 5) score[i] *= 0.5; // перші секунди часто інтро
                if (i > n - 8) score[i] *= 0.5; // кінцівка з «підписуйтесь»
            }
            var prefix = new double[n + 1];
            for (int i = 0; i < n; i++) prefix[i + 1] = prefix[i] + score[i];

            // можливі початки й кінці — межі речень/пауз, щоб не різати посеред слова
            var starts = new List<double>();
            var ends = new List<double>();
            if (hasWords)
            {
                for (int i = 0; i < words.Count; i++)
                {
                    bool afterPause = i == 0 || words[i].Start - words[i - 1].End > 0.35 || Captions.EndsSentence(words[i - 1].Text);
                    if (afterPause) starts.Add(Math.Max(0, words[i].Start - 0.15));
                    bool beforePause = i == words.Count - 1 || words[i + 1].Start - words[i].End > 0.35 || Captions.EndsSentence(words[i].Text);
                    if (beforePause) ends.Add(Math.Min(n, words[i].End + 0.25));
                }
                if (starts.Count < 20) { starts.Clear(); foreach (var w in words) starts.Add(Math.Max(0, w.Start - 0.1)); }
                if (ends.Count < 20) { ends.Clear(); foreach (var w in words) ends.Add(Math.Min(n, w.End + 0.2)); }
            }
            else
            {
                for (double t = 0; t < n; t += 2) { starts.Add(t); ends.Add(t); }
            }
            ends.Sort();

            var cands = new List<ClipItem>();
            foreach (var s in starts)
            {
                int lo = LowerBound(ends, s + minLen);
                ClipItem best = null;
                for (int k = lo; k < ends.Count && ends[k] <= s + maxLen; k++)
                {
                    double e = ends[k];
                    int a = (int)s, b = Math.Min(n, (int)Math.Ceiling(e));
                    if (b <= a) continue;
                    double mean = (prefix[b] - prefix[a]) / (b - a);
                    // гачок: сильні перші 3 секунди
                    int hb = Math.Min(b, a + 3);
                    double hook = (prefix[hb] - prefix[a]) / Math.Max(1, hb - a);
                    double lenBonus = 1 + 0.08 * (e - s - minLen) / Math.Max(1, maxLen - minLen);
                    double sc = (mean * 0.85 + hook * 0.15) * lenBonus;
                    var ci = new ClipItem { Start = s, End = e, Score = sc };
                    cands.Add(ci);
                    if (best == null || sc > best.Score) best = ci;
                }
                if (best == null && s + minLen <= n)
                {
                    double e = Math.Min(n, s + (minLen + maxLen) / 2.0);
                    int a = (int)s, b = Math.Min(n, (int)Math.Ceiling(e));
                    if (b > a) cands.Add(new ClipItem { Start = s, End = e, Score = (prefix[b] - prefix[a]) / (b - a) * 0.9 });
                }
            }

            var chosen = new List<ClipItem>();
            foreach (var c in cands.OrderByDescending(c => c.Score))
            {
                if (chosen.Count >= count) break;
                bool overlap = chosen.Any(x => c.Start < x.End + 3 && c.End > x.Start - 3);
                if (!overlap) chosen.Add(c);
            }
            chosen = chosen.OrderBy(c => c.Start).ToList();
            for (int i = 0; i < chosen.Count; i++)
            {
                chosen[i].Index = i + 1;
                chosen[i].Text = Captions.JoinText(Captions.Slice(words, chosen[i].Start, chosen[i].End));
            }
            return chosen;
        }

        static int LowerBound(List<double> a, double v)
        {
            int lo = 0, hi = a.Count;
            while (lo < hi) { int m = (lo + hi) / 2; if (a[m] < v) lo = m + 1; else hi = m; }
            return lo;
        }
    }
}
