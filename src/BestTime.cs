using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace AutoShorts
{
    /// <summary>
    /// Найкращий час публікації: перегляди перших 48 годин власних відео, згруповані за днем тижня і годиною публікації (Київ).
    /// Якщо даних мало — 17:00–22:00 за Києвом, найсильніші п'ятниця і неділя.
    /// </summary>
    static class BestTime
    {
        static TimeZoneInfo kyiv;

        public static TimeZoneInfo Kyiv
        {
            get
            {
                if (kyiv == null)
                {
                    foreach (var id in new[] { "FLE Standard Time", "Europe/Kyiv", "Europe/Kiev" })
                    {
                        try { kyiv = TimeZoneInfo.FindSystemTimeZoneById(id); break; } catch { }
                    }
                    if (kyiv == null) kyiv = TimeZoneInfo.Local;
                }
                return kyiv;
            }
        }

        public static DateTime ToKyiv(DateTime utc) { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Kyiv); }
        public static DateTime FromKyiv(DateTime local) { return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Kyiv); }

        static readonly string[] DayNames = { "Нд", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };

        public static List<double> Fallback()
        {
            var s = new List<double>(new double[168]);
            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                {
                    double v;
                    if (h >= 17 && h <= 22) v = (h == 19 || h == 20) ? 1.0 : 0.85;
                    else if (h >= 12 && h < 17) v = 0.45;
                    else if (h > 22 || h < 1) v = 0.35;
                    else if (h >= 8) v = 0.25;
                    else v = 0.05;
                    if (d == 5 || d == 0) v *= 1.25; // пт, нд
                    s[d * 24 + h] = v;
                }
            return Normalize(s);
        }

        static List<double> Normalize(List<double> s)
        {
            double max = s.Max();
            return max <= 0 ? s : s.Select(x => x / max).ToList();
        }

        public static async Task RecalculateAsync(AppSettings settings, ChannelInfo ch, Action<string> log)
        {
            var api = await YouTubeApi.ForChannelAsync(settings, ch);
            var me = await api.MyChannelAsync();
            log("Читаю останні відео каналу «" + ch.Title + "»…");
            var uploads = await api.RecentUploadsAsync(me.Item3, 60);
            var hourSum = new double[24]; var hourCnt = new int[24];
            var daySum = new double[7]; var dayCnt = new int[7];
            int analyzed = 0;
            foreach (var u in uploads)
            {
                if ((DateTime.UtcNow - u.Item2).TotalDays < 3) continue;
                double views;
                try { views = await api.FirstDaysViewsAsync(u.Item1, u.Item2); }
                catch (Exception ex) { log("Аналітика недоступна: " + ex.Message); break; }
                var k = ToKyiv(u.Item2);
                var lv = Math.Log(1 + views);
                hourSum[k.Hour] += lv; hourCnt[k.Hour]++;
                daySum[(int)k.DayOfWeek] += lv; dayCnt[(int)k.DayOfWeek]++;
                analyzed++;
                if (analyzed % 10 == 0) log("Проаналізовано відео: " + analyzed);
            }
            ch.VideosAnalyzed = analyzed;
            ch.BestTimeUpdatedUtc = DateTime.UtcNow;
            var fb = Fallback();
            if (analyzed < 6)
            {
                ch.HourScores = fb;
                ch.UsingFallback = true;
                log("Замало даних (" + analyzed + " відео) — використовую 17:00–22:00, пт і нд.");
                return;
            }
            double avg = hourSum.Sum() / Math.Max(1, hourCnt.Sum());
            var hour = new double[24];
            for (int h = 0; h < 24; h++)
            {
                // згладжування сусідніми годинами + довіра до малої кількості відео
                double s = 0, c = 0;
                for (int dh = -1; dh <= 1; dh++)
                {
                    int hh = (h + dh + 24) % 24;
                    double w = dh == 0 ? 1 : 0.5;
                    s += hourSum[hh] * w; c += hourCnt[hh] * w;
                }
                hour[h] = (s + avg * 2) / (c + 2);
            }
            var day = new double[7];
            for (int d = 0; d < 7; d++) day[d] = (daySum[d] + avg * 2) / (dayCnt[d] + 2);
            double hMin = hour.Min(), hMax = hour.Max(), dMax = day.Max();
            var scores = new List<double>(new double[168]);
            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                {
                    double hn = hMax - hMin < 1e-9 ? 0.5 : (hour[h] - hMin) / (hMax - hMin);
                    double dn = dMax <= 0 ? 1 : day[d] / dMax;
                    double own = hn * (0.7 + 0.3 * dn);
                    scores[d * 24 + h] = 0.7 * own + 0.3 * fb[d * 24 + h];
                }
            ch.HourScores = Normalize(scores);
            ch.UsingFallback = false;
            log("Найкращий час оновлено (" + analyzed + " відео): " + Describe(ch));
        }

        public static List<double> Scores(ChannelInfo ch)
        {
            return ch.HourScores != null && ch.HourScores.Count == 168 ? ch.HourScores : Fallback();
        }

        public static string Describe(ChannelInfo ch)
        {
            var s = Scores(ch);
            var hours = Enumerable.Range(0, 24).OrderByDescending(h => Enumerable.Range(0, 7).Sum(d => s[d * 24 + h])).Take(3).OrderBy(h => h);
            var days = Enumerable.Range(0, 7).OrderByDescending(d => Enumerable.Range(0, 24).Sum(h => s[d * 24 + h])).Take(2);
            return string.Join(", ", hours.Select(h => h.ToString("00") + ":00")) + "; дні: " + string.Join(", ", days.Select(d => DayNames[d])) +
                   (ch.UsingFallback ? " (типово)" : "");
        }

        /// <summary>Наступний вільний слот (UTC) для каналу.</summary>
        public static DateTime NextSlotUtc(ChannelInfo ch, IEnumerable<DateTime> takenUtc, DateTime nowUtc)
        {
            var s = Scores(ch);
            int perDay = Math.Max(1, Math.Min(12, ch.ShortsPerDay));
            var taken = takenUtc.ToList();
            var minUtc = nowUtc.AddMinutes(90);
            var todayKyiv = ToKyiv(nowUtc).Date;
            for (int off = 0; off < 120; off++)
            {
                var date = todayKyiv.AddDays(off);
                int d = (int)date.DayOfWeek;
                var ranked = Enumerable.Range(7, 17).OrderByDescending(h => s[d * 24 + h]).ToList(); // 07:00–23:00
                var picked = new List<int>();
                foreach (var h in ranked)
                {
                    if (picked.Count >= perDay) break;
                    if (picked.Any(p => Math.Abs(p - h) < Math.Max(1, 6 / perDay))) continue;
                    picked.Add(h);
                }
                int takenThatDay = taken.Count(t => ToKyiv(t).Date == date);
                if (takenThatDay >= perDay) continue;
                foreach (var h in picked.OrderBy(h => h))
                {
                    var utc = FromKyiv(date.AddHours(h));
                    if (utc < minUtc) continue;
                    if (taken.Any(t => Math.Abs((t - utc).TotalMinutes) < 50)) continue;
                    return utc;
                }
            }
            return minUtc;
        }

        public static string Format(DateTime utc)
        {
            var k = ToKyiv(utc);
            return DayNames[(int)k.DayOfWeek] + " " + k.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
