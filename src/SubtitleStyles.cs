using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AutoShorts
{
    public class SubStyle
    {
        public string Name;
        public string Font = "Arial Black";
        public int Size = 92;
        public string Primary = "FFFFFF";     // RGB
        public string Active = "2BFF4A";      // колір слова, яке звучить зараз (null = без підсвітки)
        public string Keyword = "FFE500";     // колір ключових слів (null = без)
        public string Outline = "000000";
        public int OutlineWidth = 7;
        public int Shadow = 3;
        public bool Box;                      // підкладка-плашка замість обведення
        public bool Pop = true;               // слово трохи збільшується
        public bool Progressive;              // слова з'являються по одному
        public bool Upper = true;
        public int MaxWords = 3;
        public int MarginV = 560;             // відступ знизу (з 1920)
        public bool Bold = true;
        public bool Karaoke;                  // слово, що звучить, заливається кольором (\k), рядок стоїть на місці
        public int Align = 2;                 // 2 — низ, 5 — центр, 8 — верх
        public float Scale = 1;               // масштаб гачка під формат кадру

        /// <summary>Розміщення під формат кадру, позицію («Низ», «Центр», «Верх») і розмір у %.</summary>
        public SubStyle Placed(string position, int sizePct, System.Drawing.Size o)
        {
            var c = (SubStyle)MemberwiseClone();
            double k = Math.Sqrt(o.Width * (double)o.Height / (1080.0 * 1920.0));
            if (sizePct < 50 || sizePct > 200) sizePct = 100;
            c.Size = (int)Math.Round(Size * k * sizePct / 100.0);
            c.OutlineWidth = Math.Max(1, (int)Math.Round(OutlineWidth * k * Math.Sqrt(sizePct / 100.0)));
            c.Scale = (float)k;
            double hk = o.Height / 1920.0;
            switch (position)
            {
                case "Центр": c.Align = 5; c.MarginV = 0; break;
                case "Верх": c.Align = 8; c.MarginV = (int)Math.Round(o.Height * 0.21); break;
                default: c.Align = 2; c.MarginV = (int)Math.Round(MarginV * hk); break;
            }
            return c;
        }

        /// <summary>Копія стилю з іншим шрифтом.</summary>
        public SubStyle With(string font, bool bold)
        {
            var c = (SubStyle)MemberwiseClone();
            c.Font = font;
            c.Bold = bold;
            return c;
        }
    }

    static class SubtitleStyles
    {
        public static readonly List<SubStyle> All = new List<SubStyle>
        {
            new SubStyle { Name = "Опус (зелений)", Active = "2BFF4A", Keyword = "FFE500" },
            new SubStyle { Name = "Жовтий акцент", Active = "FFE500", Keyword = "2BFF4A", Font = "Impact", Size = 104, OutlineWidth = 6 },
            new SubStyle { Name = "Караоке", Active = "FFE500", Keyword = null, Progressive = true, MaxWords = 4, Pop = false },
            new SubStyle { Name = "Караоке-заливка", Active = "FFE500", Keyword = null, Karaoke = true, MaxWords = 4, Pop = false },
            new SubStyle { Name = "Плашка", Font = "Arial", Size = 80, Active = "FFE500", Keyword = null, Box = true, Pop = false, OutlineWidth = 14, Shadow = 0 },
            new SubStyle { Name = "Мінімал", Font = "Segoe UI Semibold", Size = 70, Active = null, Keyword = null, Pop = false, Upper = false, MaxWords = 5, OutlineWidth = 4, Shadow = 1, MarginV = 380 },
        };

        public static SubStyle Find(string name)
        {
            return All.FirstOrDefault(s => s.Name == name);
        }

        public static SubStyle Get(string name)
        {
            return Find(name) ?? All[0];
        }

        static string Color(string rgb, string alpha)
        {
            rgb = (rgb ?? "FFFFFF").TrimStart('#');
            return "&H" + alpha + rgb.Substring(4, 2) + rgb.Substring(2, 2) + rgb.Substring(0, 2);
        }

        static string Inline(string rgb)
        {
            rgb = rgb.TrimStart('#');
            return "&H" + rgb.Substring(4, 2) + rgb.Substring(2, 2) + rgb.Substring(0, 2) + "&";
        }

        static string Time(double t)
        {
            if (t < 0) t = 0;
            int cs = (int)Math.Round(t * 100);
            int h = cs / 360000; cs -= h * 360000;
            int m = cs / 6000; cs -= m * 6000;
            int s = cs / 100; cs -= s * 100;
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}", h, m, s, cs);
        }

        static string Display(string w, SubStyle st)
        {
            var t = (w ?? "").Replace("{", "").Replace("}", "").Replace("\\", "");
            t = t.TrimEnd(',', '.', ';', ':', '…');
            if (st.Upper) t = t.ToUpperInvariant();
            return t;
        }

        /// <summary>Групи по 2–4 слова, розірвані на паузах і кінцях речень.</summary>
        static List<List<Word>> Group(List<Word> words, SubStyle st)
        {
            var groups = new List<List<Word>>();
            var cur = new List<Word>();
            int chars = 0;
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                if (cur.Count > 0)
                {
                    var prev = cur[cur.Count - 1];
                    bool brk = cur.Count >= st.MaxWords || chars + w.Text.Length > 18 || w.Start - prev.End > 0.5
                               || Captions.EndsSentence(prev.Text) || prev.Text.EndsWith(",");
                    if (brk) { groups.Add(cur); cur = new List<Word>(); chars = 0; }
                }
                cur.Add(w);
                chars += w.Text.Length + 1;
            }
            if (cur.Count > 0) groups.Add(cur);
            return groups;
        }

        public static readonly string[] HookStyles = { "Класичний", "Біла плашка", "Рожева плашка", "Без плашки" };

        static string HookStyleLine(string name, float k, int marginV, int marginLR)
        {
            string line;
            switch (name)
            {
                case "Біла плашка": line = "Style: Hook,Arial Black,{S76},&H00141414,&H00141414,&H00FFFFFF,&H00FFFFFF,-1,0,0,0,100,100,0,0,3,{S18},0,8,{M},{M},{V},1"; break;
                case "Рожева плашка": line = "Style: Hook,Arial Black,{S76},&H00FFFFFF,&H00FFFFFF,&H00882EFF,&H00882EFF,-1,0,0,0,100,100,0,0,3,{S18},0,8,{M},{M},{V},1"; break;
                case "Без плашки": line = "Style: Hook,Arial Black,{S84},&H00FFFFFF,&H00FFFFFF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,{S7},3,8,{M},{M},{V},1"; break;
                default: line = "Style: Hook,Arial Black,{S78},&H00FFFFFF,&H00FFFFFF,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,3,{S18},0,8,{M},{M},{V},1"; break;
            }
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\{S(\d+)\}", m => Math.Max(1, (int)Math.Round(int.Parse(m.Groups[1].Value) * k)).ToString());
            return line.Replace("{M}", marginLR.ToString()).Replace("{V}", marginV.ToString());
        }

        public static string BuildAss(List<Word> words, SubStyle st, string hook, double duration, HashSet<string> keywords, string hookStyle = null,
                                      System.Drawing.Size outSize = default(System.Drawing.Size))
        {
            if (outSize.Width == 0) outSize = new System.Drawing.Size(1080, 1920);
            var sb = new StringBuilder();
            sb.AppendLine("[Script Info]");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine("PlayResX: " + outSize.Width);
            sb.AppendLine("PlayResY: " + outSize.Height);
            sb.AppendLine("WrapStyle: 0");
            sb.AppendLine("ScaledBorderAndShadow: yes");
            sb.AppendLine();
            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Style: Main,{0},{1},{2},{10},{3},{4},{9},0,0,0,100,100,1,0,{5},{6},{7},{11},{12},{12},{8},1",
                st.Font, st.Size, Color(st.Karaoke && st.Active != null ? st.Active : st.Primary, "00"),
                st.Box ? Color("000000", "40") : Color(st.Outline, "00"),
                Color("000000", "80"), st.Box ? 3 : 1, st.OutlineWidth, st.Shadow, st.MarginV, st.Bold ? -1 : 0,
                Color(st.Primary, "00"), st.Align, (int)Math.Round(outSize.Width * 0.065)));
            sb.AppendLine(HookStyleLine(hookStyle, st.Scale, (int)Math.Round(outSize.Height * 0.135), (int)Math.Round(outSize.Width * 0.074)));
            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            if (!string.IsNullOrWhiteSpace(hook))
            {
                var h = hook.Replace("{", "").Replace("}", "").Replace("\\", "").ToUpperInvariant();
                sb.AppendLine("Dialogue: 1," + Time(0) + "," + Time(Math.Min(3.2, duration)) +
                              ",Hook,,0,0,0,,{\\fad(150,250)}" + h);
            }

            var groups = Group(words, st);
            for (int g = 0; g < groups.Count; g++)
            {
                var grp = groups[g];
                double gEnd = grp[grp.Count - 1].End;
                if (g + 1 < groups.Count)
                {
                    double nextStart = groups[g + 1][0].Start;
                    if (nextStart - gEnd < 0.6) gEnd = nextStart;
                }
                else gEnd = Math.Min(duration, gEnd + 0.4);

                if (st.Karaoke)
                {
                    // караоке (з ViralCut): уся група на екрані, слово заливається кольором у момент, коли звучить
                    var kt = new StringBuilder();
                    double g0 = grp[0].Start;
                    for (int k = 0; k < grp.Count; k++)
                    {
                        double ws = grp[k].Start, we = k + 1 < grp.Count ? grp[k + 1].Start : gEnd;
                        int cs = Math.Max(1, (int)Math.Round((we - ws) * 100));
                        if (k > 0) kt.Append(' ');
                        kt.Append("{\\k").Append(cs).Append("}").Append(Display(grp[k].Text, st));
                    }
                    if (gEnd > g0 + 0.05)
                        sb.AppendLine("Dialogue: 0," + Time(g0) + "," + Time(gEnd) + ",Main,,0,0,0,," + kt);
                    continue;
                }

                for (int k = 0; k < grp.Count; k++)
                {
                    double s = grp[k].Start;
                    double e = k + 1 < grp.Count ? grp[k + 1].Start : gEnd;
                    if (k == 0 && g > 0) s = Math.Max(s, 0);
                    if (e <= s + 0.02) continue;
                    var text = new StringBuilder();
                    int upTo = st.Progressive ? k : grp.Count - 1;
                    for (int j = 0; j <= upTo; j++)
                    {
                        if (j > 0) text.Append(' ');
                        var disp = Display(grp[j].Text, st);
                        bool isKey = st.Keyword != null && keywords != null && keywords.Contains(Captions.Clean(grp[j].Text).ToLowerInvariant());
                        if (j == k && st.Active != null)
                        {
                            text.Append("{\\c" + Inline(st.Active));
                            if (st.Pop) text.Append("\\fscx112\\fscy112");
                            text.Append("}" + disp + "{\\r}");
                        }
                        else if (isKey)
                            text.Append("{\\c" + Inline(st.Keyword) + "}" + disp + "{\\r}");
                        else
                            text.Append(disp);
                    }
                    sb.AppendLine("Dialogue: 0," + Time(s) + "," + Time(e) + ",Main,,0,0,0,," + text);
                }
            }
            return sb.ToString();
        }
    }
}
