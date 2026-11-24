using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AutoShorts
{
    /// <summary>
    /// Текстові евристики, перенесені з ViralCut (analyze.py): речення, рекламні вставки,
    /// «гачки» на початку, незавершені думки, найкращий заголовок, вирізання пауз.
    /// Словники — українська, російська, англійська.
    /// </summary>
    static class Smart
    {
        public class Sentence
        {
            public double Start, End;
            public string Text;
        }

        public class Span
        {
            public double Start, End, Output; // Output — де відрізок почнеться в готовому кліпі
            public double Length { get { return End - Start; } }
        }

        // ---------- словники ----------

        static readonly string[] HookPatterns =
        {
            // uk
            @"\bчому\b", @"\bяк\b", @"\bщо якщо\b", @"\bніколи\b", @"\bзавжди\b", @"секрет", @"помилк", @"\bніхто не\b",
            @"\bкожен\b", @"головн", @"уяв", @"\bзнаєш", @"виявил", @"\bправда\b", @"заборон", @"безкоштовн", @"грош",
            @"мільйон", @"тисяч", @"\bшок", @"терміново", @"\bувага\b", @"\bстоп\b", @"\bдосить\b",
            // ru
            @"\bпочему\b", @"\bкак\b", @"\bчто если\b", @"\bникогда\b", @"\bвсегда\b", @"ошибк", @"\bникто не\b",
            @"\bкаждый\b", @"представь", @"\bзнаешь", @"выяснил", @"оказалось", @"запрещ", @"бесплатн", @"деньг",
            @"миллион", @"срочно", @"\bвнимание\b", @"\bхватит\b",
            // en
            @"\bwhy\b", @"\bhow\b", @"\bwhat if\b", @"\bnever\b", @"secret", @"mistake", @"\bnobody\b", @"\btruth\b",
            @"\bwarning\b", @"insane", @"\bcrazy\b", @"\bactually\b", @"\bhere'?s\b", @"\bstop\b", @"\bmoney\b", @"million",
        };

        static readonly string[] HookStarters =
        {
            "чому", "як ", "що якщо", "а що", "знаєш", "дивись", "уяви", "увага", "головне", "секрет", "помилка",
            "почему", "как ", "что если", "а что", "знаешь", "смотри", "представь", "внимание", "главное", "ошибка",
            "why", "how ", "what if", "watch", "imagine", "secret", "mistake",
        };

        static readonly string[] FillerStart =
        {
            "і от", "ну", "от ", "ось ", "так ", "коротше", "значить", "еее", "ее ", "е ", "як би", "типу", "загалом", "ладно", "окей",
            "и вот", "вот ", "короче", "значит", "эм", "ээ", "как бы", "в общем",
            "so ", "well", "um", "uh", "like ", "okay", "yeah",
        };

        static readonly string[] ContextStarters =
        {
            "тому що", "тому", "в результаті", "так от", "і от", "ось чому", "ось що", "значить", "коротше", "це", "цей", "ця", "ці",
            "такий", "така", "таке", "тоді", "але", "однак", "а", "і", "й", "він", "вона", "вони", "його", "її", "йому", "їй", "їм", "їх",
            "там", "тут", "теж", "також",
            "потому что", "поэтому", "в итоге", "так вот", "и вот", "вот почему", "вот что", "значит", "это", "этот", "эта", "эти",
            "такой", "такая", "такое", "тогда", "но", "и", "он", "она", "они", "ему", "ей", "им", "тоже",
            "because", "so", "this", "that", "these", "those", "it", "they", "them", "he", "she", "his", "her", "but", "and", "then", "which", "there",
        };

        static readonly string[] ContinuationEnd =
        {
            "тому що", "тому", "так що", "і", "й", "а", "але", "або", "які", "який", "яка", "яке", "що", "щоб", "якщо", "коли", "поки",
            "потому что", "потому", "и", "но", "или", "либо", "которые", "который", "которая", "которое", "что", "чтобы", "если", "когда", "пока",
            "because", "so", "and", "but", "or", "which", "that", "if", "when", "while",
        };

        static readonly string[] AdStrong =
        {
            "промокод", "промо код", "знижк", "купон", "посилання в описі", "в описі", "за посиланням", "спонсор", "реклам",
            "партнер", "за підтримки", "спонсорує", "наш партн",
            "скидк", "ссылка в описании", "в описании", "по ссылке", "рекламодател", "партнёр", "при поддержке", "спонсируется",
            "sponsored", "promo code", "discount", "use code", "link in the description", "in the description", "our sponsor", "thanks to our",
        };

        static readonly string[] AdWeak =
        {
            "підпишись", "підписуйтесь", "підписуйся", "безкоштовн", "спробуй", "замовити", "магазин", "сервіс", "доставка", "тариф",
            "перший місяць", "відсот", "економія", "вигідн", "акція",
            "подпишись", "подписывайтесь", "бесплатн", "попробуй", "заказать", "первый месяц", "процент", "выгодн", "акция",
            "subscribe", "free trial", "check out", "sign up",
        };

        static readonly Regex NumRx = new Regex(@"\d+[.,]?\d*|%|₴|\$|€|грн|тисяч|мільйон|мільярд|тысяч|миллион|відсот|процент", RegexOptions.IgnoreCase);
        static readonly Regex WordRx = new Regex(@"[\w\-']+");

        static string Norm(string t) { return (t ?? "").ToLower(CultureInfo.InvariantCulture).Replace('ё', 'е').Replace('’', '\''); }
        static string Head(string t) { return Norm(t).Trim(' ', '«', '"', '\'', '—', '-', '–'); }

        // ---------- речення ----------

        /// <summary>Речення зі слів: межа — розділовий знак, пауза &gt; 0.8 с або 24 слова (автосубтитри YouTube без розділових знаків).</summary>
        public static List<Sentence> Sentences(List<Word> words)
        {
            var res = new List<Sentence>();
            if (words == null || words.Count == 0) return res;
            int from = 0;
            for (int i = 0; i < words.Count; i++)
            {
                bool last = i == words.Count - 1;
                bool end = last || Captions.EndsSentence(words[i].Text) || words[i + 1].Start - words[i].End > 0.8 || i - from >= 23;
                if (!end) continue;
                res.Add(new Sentence
                {
                    Start = words[from].Start,
                    End = words[i].End,
                    Text = string.Join(" ", words.Skip(from).Take(i - from + 1).Select(w => w.Text))
                });
                from = i + 1;
            }
            return res;
        }

        // ---------- реклама ----------

        public static int AdScore(string text)
        {
            var low = Norm(text);
            return AdStrong.Count(p => low.Contains(p)) * 2 + AdWeak.Count(p => low.Contains(p));
        }

        /// <summary>Спонсорські вставки: підряд речення з рекламними маркерами («промокод», «посилання в описі»…).</summary>
        public static List<Span> AdBlocks(List<Sentence> sents, int threshold = 2)
        {
            var blocks = new List<Span>();
            var cur = new List<int>();
            Action flush = () =>
            {
                if (cur.Count > 0 && cur.Sum(i => AdScore(sents[i].Text)) >= threshold)
                    blocks.Add(new Span { Start = sents[cur[0]].Start, End = sents[cur[cur.Count - 1]].End });
                cur.Clear();
            };
            for (int i = 0; i < sents.Count; i++)
            {
                int sc = AdScore(sents[i].Text);
                if (sc >= 2) cur.Add(i);
                else if (cur.Count > 0 && sc == 1) cur.Add(i);
                else flush();
            }
            flush();
            return blocks;
        }

        public static double Overlap(double start, double end, List<Span> spans)
        {
            double total = 0;
            foreach (var b in spans) total += Math.Max(0, Math.Min(end, b.End) - Math.Max(start, b.Start));
            return total;
        }

        // ---------- гачок і межі думки ----------

        /// <summary>Бонус за чіпкий початок: питання, цифра, обіцянка.</summary>
        public static double HookBonus(string text)
        {
            var low = Head(text);
            double bonus = 0;
            if (HookStarters.Any(h => low.StartsWith(h))) bonus += 3;
            if (Regex.IsMatch(low.Length > 40 ? low.Substring(0, 40) : low, @"\d")) bonus += 1.5;
            if (low.TrimEnd().EndsWith("?")) bonus += 2;
            return bonus;
        }

        public static bool HasFillerStart(string text)
        {
            var low = Head(text) + " ";
            return FillerStart.Any(f => low.StartsWith(f));
        }

        /// <summary>Речення починається із займенника чи зв’язки — без попереднього незрозуміле.</summary>
        public static bool StartsWithContext(string text)
        {
            var words = Head(text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;
            var head = string.Join(" ", words.Take(2));
            foreach (var m in ContextStarters)
            {
                if (m.Contains(" ")) { if (head.StartsWith(m)) return true; }
                else if (words[0].Trim(',', '.') == m) return true;
            }
            return false;
        }

        /// <summary>Речення обривається на зв’язці — думка продовжиться в наступному.</summary>
        public static bool EndsWithContinuation(string text)
        {
            var t = Norm(text).Trim();
            if (t.Length == 0) return true;
            if (t.EndsWith("?") || t.EndsWith("!") || t.EndsWith("…")) return false;
            if (t.EndsWith(",") || t.EndsWith("—") || t.EndsWith("-") || t.EndsWith(":") || t.EndsWith(";") || t.EndsWith("–")) return true;
            var words = t.TrimEnd('.', ',', ';', ':').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return true;
            var tail2 = string.Join(" ", words.Skip(Math.Max(0, words.Length - 2)));
            if (tail2 == "тому що" || tail2 == "так що" || tail2 == "потому что" || tail2 == "так что") return true;
            return ContinuationEnd.Contains(words[words.Length - 1]);
        }

        /// <summary>Множник оцінки фрагмента за його межами (1 — нейтрально).</summary>
        public static double BoundaryFactor(List<Sentence> sents, double start, double end)
        {
            if (sents == null || sents.Count == 0) return 1;
            double f = 1;
            var first = sents.FirstOrDefault(x => x.End > start + 0.3);
            if (first != null && Math.Abs(first.Start - start) < 1.0)
            {
                f *= 1 + 0.04 * HookBonus(first.Text);
                if (StartsWithContext(first.Text)) f *= 0.86;
                if (HasFillerStart(first.Text)) f *= 0.93;
            }
            var last = sents.LastOrDefault(x => x.Start < end - 0.3);
            if (last != null && Math.Abs(last.End - end) < 1.0 && EndsWithContinuation(last.Text)) f *= 0.9;
            return f;
        }

        // ---------- заголовок ----------

        /// <summary>Наскільки фраза годиться в заголовок (короткі переходи на кшталт «Що далі?» — ні).</summary>
        public static double TitleScore(string text)
        {
            var t = (text ?? "").Trim();
            int len = t.Length;
            double sc = 0;
            if (len >= 25 && len <= 72) sc += 2;
            else if (len >= 18 && len < 25) sc += 0.8;
            else if (len < 18) sc -= 1.5;
            if (t.Contains("?")) sc += 1.2;
            if (NumRx.IsMatch(t)) sc += 0.6;
            var low = Norm(t);
            sc += Math.Min(1.2, 0.4 * HookPatterns.Count(p => Regex.IsMatch(low, p)));
            if (HasFillerStart(t)) sc -= 1;
            if (WordRx.Matches(t).Count < 4) sc -= 1.2;
            return sc;
        }

        /// <summary>Найчіпкіше речення серед перших чотирьох — для заголовка-гачка й назви.</summary>
        public static string BestTitle(IEnumerable<string> sentences)
        {
            var cands = sentences.Where(x => !string.IsNullOrWhiteSpace(x)).Take(4).ToList();
            if (cands.Count == 0) return null;
            var best = cands.OrderByDescending(TitleScore).First();
            if (TitleScore(best) < 0) return null;
            return Regex.Replace(best, @"\s+", " ").Trim(' ', '.', ',', '-', '—');
        }

        // ---------- паузи ----------

        /// <summary>План склейки без пауз (cut_plan з ViralCut). Час — відносно початку кліпу.</summary>
        public static List<Span> CutPlan(double start, double end, List<Span> gaps, double padding = 0.12, double minKeep = 0.35)
        {
            var spans = new List<Span>();
            double cursor = start, outT = 0;
            foreach (var g in gaps.OrderBy(x => x.Start))
            {
                double from = Math.Max(start, g.Start + padding), to = Math.Min(end, g.End - padding);
                if (to <= from || from <= cursor) { cursor = Math.Max(cursor, to); continue; }
                if (from - cursor >= minKeep)
                {
                    spans.Add(new Span { Start = cursor, End = from, Output = outT });
                    outT += from - cursor;
                }
                cursor = Math.Max(cursor, to);
            }
            if (end - cursor >= minKeep) spans.Add(new Span { Start = cursor, End = end, Output = outT });
            if (spans.Count == 0) spans.Add(new Span { Start = start, End = end, Output = 0 });
            return spans;
        }

        /// <summary>Слова під склейку: викинути ті, що потрапили в паузи, зсунути решту на новий час.</summary>
        public static List<Word> RemapWords(List<Word> words, List<Span> spans)
        {
            var res = new List<Word>();
            foreach (var w in words)
            {
                double? ns = null, ne = null;
                foreach (var sp in spans)
                {
                    double a = Math.Max(w.Start, sp.Start), b = Math.Min(w.End, sp.End);
                    if (b <= a) continue;
                    double shift = sp.Output - sp.Start;
                    if (ns == null) ns = a + shift;
                    ne = b + shift;
                }
                if (ns != null) res.Add(new Word(ns.Value, ne.Value, w.Text));
            }
            return res;
        }
    }
}
