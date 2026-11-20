using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AutoShorts
{
    static class Captions
    {
        class Token { public double Start; public double EventEnd; public string Text; }

        /// <summary>Розбирає формат YouTube json3. Автосубтитри мають час кожного слова (tOffsetMs).</summary>
        public static List<Word> ParseJson3(string json)
        {
            var root = Json.Parse(json);
            var tokens = new List<Token>();
            foreach (var ev in Json.Arr(root, "events"))
            {
                var segs = Json.Arr(ev, "segs");
                if (segs.Length == 0) continue;
                double start = Json.Num(ev, "tStartMs", 0) / 1000.0;
                double dur = Json.Num(ev, "dDurationMs", 0) / 1000.0;
                double end = start + Math.Max(dur, 0.2);

                bool hasOffsets = segs.Length > 1 && segs.Skip(1).Any(s => Json.Get(s, "tOffsetMs") != null);
                if (hasOffsets)
                {
                    foreach (var s in segs)
                    {
                        var t = Json.Str(s, "utf8");
                        if (string.IsNullOrWhiteSpace(t)) continue;
                        double off = Json.Num(s, "tOffsetMs", 0) / 1000.0;
                        var parts = Split(t);
                        // декілька слів в одному сегменті — розподіляємо рівномірно на ~0.3с
                        for (int i = 0; i < parts.Count; i++)
                            tokens.Add(new Token { Start = start + off + i * 0.25, EventEnd = end, Text = parts[i] });
                    }
                }
                else
                {
                    var text = string.Join(" ", segs.Select(s => Json.Str(s, "utf8") ?? ""));
                    var parts = Split(text);
                    if (parts.Count == 0) continue;
                    int totalChars = parts.Sum(p => p.Length + 1);
                    double pos = start;
                    foreach (var p in parts)
                    {
                        tokens.Add(new Token { Start = pos, EventEnd = end, Text = p });
                        pos += (end - start) * (p.Length + 1) / totalChars;
                    }
                }
            }

            tokens = tokens.OrderBy(t => t.Start).ToList();
            var words = new List<Word>();
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                double next = i + 1 < tokens.Count ? tokens[i + 1].Start : t.EventEnd;
                double e = Math.Min(Math.Min(next, t.EventEnd), t.Start + 1.6);
                if (e < t.Start + 0.08) e = t.Start + 0.08;
                // пропускаємо дублікати (однаковий текст в той самий момент)
                if (words.Count > 0)
                {
                    var last = words[words.Count - 1];
                    if (Math.Abs(last.Start - t.Start) < 0.01 && last.Text == t.Text) continue;
                }
                words.Add(new Word(t.Start, e, t.Text));
            }
            return words;
        }

        static readonly Regex Noise = new Regex(@"^\[.*\]$");

        static List<string> Split(string text)
        {
            var res = new List<string>();
            foreach (var p in Regex.Split(text.Replace("\n", " "), @"\s+"))
            {
                var w = p.Trim();
                if (w.Length == 0 || Noise.IsMatch(w)) continue;
                res.Add(w);
            }
            return res;
        }

        public static string Clean(string w)
        {
            return Regex.Replace(w ?? "", @"[^\p{L}\p{N}'’\-]", "");
        }

        public static bool EndsSentence(string w)
        {
            if (string.IsNullOrEmpty(w)) return false;
            char c = w[w.Length - 1];
            return c == '.' || c == '!' || c == '?' || c == '…';
        }

        public static List<Word> Slice(List<Word> words, double start, double end)
        {
            var res = new List<Word>();
            foreach (var w in words)
            {
                if (w.End <= start + 0.05 || w.Start >= end - 0.05) continue;
                res.Add(new Word(Math.Max(0, w.Start - start), Math.Min(end - start, w.End - start), w.Text));
            }
            return res;
        }

        public static string JoinText(List<Word> words)
        {
            return string.Join(" ", words.Select(w => w.Text));
        }
    }
}
