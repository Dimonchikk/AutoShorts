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
        public static int AdBlocksFound;

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
                                          int count, int minLen, int maxLen, double rangeStart = 0, double rangeEnd = 0, string include = null, bool skipAds = true)
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
            // «моменти, які треба включити»: секунди, де звучать ці слова, отримують бонус
            var stems = (include ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(p => p.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(w => Captions.Clean(w)).Where(w => w.Length >= 3).Select(w => w.Length > 5 ? w.Substring(0, w.Length - 2) : w).Distinct().ToList();
            if (stems.Count > 0 && hasWords)
            {
                var boost = new double[n];
                foreach (var w in words)
                {
                    var t = Captions.Clean(w.Text).ToLowerInvariant();
                    if (t.Length < 3 || !stems.Any(st => t.StartsWith(st))) continue;
                    for (int k = Math.Max(0, (int)w.Start - 8); k < Math.Min(n, (int)w.Start + 12); k++) boost[k] = 1;
                }
                for (int i = 0; i < n; i++) score[i] += 0.6 * boost[i];
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

            // відрізок, який обрав користувач
            double r0 = Math.Max(0, rangeStart), r1 = rangeEnd > r0 + minLen ? Math.Min(n, rangeEnd) : n;
            if (r0 > 0 || r1 < n)
            {
                starts = starts.Where(t => t >= r0 && t + minLen <= r1).ToList();
                ends = ends.Where(t => t <= r1).ToList();
                if (starts.Count == 0) for (double t = r0; t + minLen <= r1; t += 2) starts.Add(t);
                if (ends.Count == 0) for (double t = r0 + minLen; t <= r1; t += 2) ends.Add(t);
            }

            // з ViralCut: речення, рекламні вставки, гачки й незавершені думки
            var sents = hasWords ? Smart.Sentences(words) : new List<Smart.Sentence>();
            var ads = hasWords && skipAds ? Smart.AdBlocks(sents) : new List<Smart.Span>();
            AdBlocksFound = ads.Count;

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
                    if (ads.Count > 0 && Smart.Overlap(s, e, ads) > 1.5) continue; // реклама в кліп не йде
                    if (sents.Count > 0) sc *= Smart.BoundaryFactor(sents, s, e);
                    var ci = new ClipItem { Start = s, End = e, Score = sc };
                    cands.Add(ci);
                    if (best == null || sc > best.Score) best = ci;
                }
                if (best == null && s + minLen <= r1 && (ads.Count == 0 || Smart.Overlap(s, s + minLen, ads) <= 1.5))
                {
                    double e = Math.Min(r1, s + (minLen + maxLen) / 2.0);
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
            // оцінка віральності 0–100: відносно найкращого фрагмента + абсолютний рівень
            double maxSc = chosen.Count > 0 ? chosen.Max(c => c.Score) : 1, minSc = chosen.Count > 0 ? chosen.Min(c => c.Score) : 0;
            Func<double[], int, int, int> avg = (arr, a, b) =>
            {
                b = Math.Min(arr.Length, b); a = Math.Max(0, a);
                if (b <= a) return 0;
                double sum = 0; for (int k = a; k < b; k++) sum += arr[k];
                return (int)Math.Round(100 * Math.Max(0, Math.Min(1, sum / (b - a))));
            };
            var audioArr = new double[n];
            for (int k = 0; k < n; k++) audioArr[k] = 0.5 * loudN[k] + 0.5 * spikeN[k];
            foreach (var c in chosen)
            {
                double rel = maxSc - minSc > 1e-9 ? (c.Score - minSc) / (maxSc - minSc) : 1;
                c.Virality = Math.Max(1, Math.Min(99, (int)Math.Round(62 + 26 * rel + 11 * Math.Min(1, c.Score / 0.6))));
                int a = (int)c.Start, b = (int)Math.Ceiling(c.End);
                c.ScoreHook = Math.Min(99, 40 + avg(score, a, a + 3) * 6 / 10);
                c.ScoreViews = hasHeat ? Math.Min(99, 40 + avg(heatS, a, b) * 6 / 10) : 0;
                c.ScoreEnergy = Math.Min(99, 40 + avg(audioArr, a, b) * 6 / 10);
                c.ScoreTempo = hasWords ? Math.Min(99, 40 + avg(speech, a, b) * 6 / 10) : 0;
            }
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
