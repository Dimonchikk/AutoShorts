using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AutoShorts
{
    /// <summary>Назва, опис і теги з шаблонів — без ШІ, на основі тексту фрагмента і даних джерела.</summary>
    static class Metadata
    {
        static readonly HashSet<string> Stop = new HashSet<string>((
            "і й та але або що як це той ця ці цей там тут так ще вже не ні ну от ось бо би б же ж чи то для від до на в у з із зі по за під над про при через після перед між ми ви вони він вона воно я ти мене тебе його її їх нам вам нас вас був була було були буде будуть є має мати можна треба дуже просто також тому коли якщо щоб який яка яке які свій своя своє свої весь вся все всі більш менш " +
            "и а но или что как это тот эта эти этот там тут так еще уже не ни ну вот бы же ли то для от до на в во с со по за под над про при через после перед между мы вы они он она оно я ты меня тебя его ее их нам вам нас вас был была было были будет есть очень просто тоже поэтому когда если чтобы какой какая какое какие свой все всё весь вообще значит короче типа " +
            "the a an and or but so to of in on at for with from by is are was were be been being it its this that these those i you he she we they me him her us them my your our their not no yes do does did have has had will would can could just very really like okay yeah gonna wanna"
            ).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        public static List<string> Keywords(string text, int max)
        {
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var raw in Regex.Split(text ?? "", @"\s+"))
            {
                var w = Captions.Clean(raw).ToLowerInvariant().Trim('-', '\'', '’');
                if (w.Length < 4 || Stop.Contains(w) || w.All(char.IsDigit)) continue;
                if (!counts.ContainsKey(w)) { counts[w] = 0; order.Add(w); }
                counts[w]++;
            }
            return order.OrderByDescending(w => counts[w] * 2 + Math.Min(w.Length, 10) / 3.0)
                        .ThenBy(w => order.IndexOf(w)).Take(max).ToList();
        }

        public static string Hook(string clipText, string sourceTitle)
        {
            var text = (clipText ?? "").Trim();
            if (text.Length == 0) return Trim(sourceTitle, 60);
            var m = Regex.Match(text, @"^(.{12,55}?[\.\!\?…])(\s|$)");
            string s;
            if (m.Success) s = m.Groups[1].Value;
            else
            {
                var words = Regex.Split(text, @"\s+").Take(7).ToArray();
                s = string.Join(" ", words);
            }
            s = s.Trim().TrimEnd(',', ';', ':');
            if (s.Length > 0) s = char.ToUpper(s[0], CultureInfo.CurrentCulture) + s.Substring(1);
            return Trim(s, 55);
        }

        static string Trim(string s, int max)
        {
            s = (s ?? "").Trim();
            if (s.Length <= max) return s;
            var cut = s.Substring(0, max);
            int sp = cut.LastIndexOf(' ');
            if (sp > max / 2) cut = cut.Substring(0, sp);
            return cut.TrimEnd(',', '.', ' ') + "…";
        }

        static string Hashtag(string w)
        {
            return "#" + Regex.Replace(w, @"[^\p{L}\p{N}_]", "");
        }

        public static void Fill(ClipItem clip, Job job, AppSettings s)
        {
            var kws = Keywords(clip.Text + " " + clip.Text, 6);
            var hook = Hook(clip.Text, job.SourceTitle);
            clip.Hook = hook;
            var vars = new Dictionary<string, string>();
            vars["hook"] = hook;
            vars["source_title"] = job.SourceTitle ?? "";
            vars["source_channel"] = job.SourceChannel ?? "";
            vars["source_url"] = job.Url ?? "";
            vars["keywords"] = string.Join(", ", kws);
            vars["hashtags"] = string.Join(" ", kws.Take(4).Select(Hashtag));
            vars["source_tags"] = string.Join(", ", (job.SourceTags ?? new List<string>()).Take(10));
            vars["n"] = clip.Index.ToString(CultureInfo.InvariantCulture);
            vars["text"] = clip.Text ?? "";

            var title = Apply(s.TitleTemplate, vars).Replace("<", "").Replace(">", "");
            title = Regex.Replace(title, @"\s+", " ").Trim();
            if (title.Length > 100)
            {
                bool tagShorts = title.EndsWith("#shorts");
                title = Trim(title.Replace("#shorts", "").Trim(), tagShorts ? 90 : 99) + (tagShorts ? " #shorts" : "");
            }
            clip.Title = title;
            var desc = Apply(s.DescriptionTemplate, vars).Replace("<", "").Replace(">", "");
            clip.Description = desc.Length > 4900 ? desc.Substring(0, 4900) : desc;
            clip.Tags = CleanTags(Apply(s.TagsTemplate, vars));
        }

        static string Apply(string template, Dictionary<string, string> vars)
        {
            return Regex.Replace(template ?? "", @"\{(\w+)\}", m =>
            {
                string v;
                return vars.TryGetValue(m.Groups[1].Value, out v) ? v : m.Value;
            });
        }

        public static string CleanTags(string tags)
        {
            var seen = new HashSet<string>();
            var res = new List<string>();
            int total = 0;
            foreach (var t in (tags ?? "").Split(','))
            {
                var tag = t.Replace("<", "").Replace(">", "").Replace("\"", "").Trim();
                if (tag.Length == 0 || tag.Length > 60 || !seen.Add(tag.ToLowerInvariant())) continue;
                int cost = tag.Length + (tag.Contains(" ") ? 2 : 0) + 1;
                if (total + cost > 480) break;
                total += cost;
                res.Add(tag);
            }
            return string.Join(", ", res);
        }

        public static List<string> TagList(string tags)
        {
            return CleanTags(tags).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        }
    }
}
