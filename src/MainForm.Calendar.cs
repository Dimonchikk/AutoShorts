using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AutoShorts
{
    public partial class MainForm
    {
        CalendarView calendar;
        Label calTitle, calStats;

        static readonly string[] Months = { "Січень", "Лютий", "Березень", "Квітень", "Травень", "Червень", "Липень", "Серпень", "Вересень", "Жовтень", "Листопад", "Грудень" };
        static readonly Color[] ChannelColors =
        {
            Color.FromArgb(190, 242, 100), Color.FromArgb(96, 165, 250), Color.FromArgb(244, 114, 182),
            Color.FromArgb(251, 191, 36), Color.FromArgb(167, 139, 250), Color.FromArgb(45, 212, 191)
        };

        Color ChannelColor(string id)
        {
            int i = settings.Channels.FindIndex(c => c.Id == id);
            return ChannelColors[(i < 0 ? 0 : i) % ChannelColors.Length];
        }

        Dropdown calChannel;
        AButton calRefreshBtn;
        FlowLayoutPanel calLegend;
        readonly Dictionary<string, List<ChannelVideo>> channelVideos = new Dictionary<string, List<ChannelVideo>>();
        bool calLoading;

        static string VideosCachePath(string channelId)
        {
            var dir = System.IO.Path.Combine(AppSettings.LocalDir, "calendar");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, System.Text.RegularExpressions.Regex.Replace(channelId, @"[^\w\-]", "_") + ".json");
        }

        List<ChannelVideo> CachedVideos(string channelId)
        {
            List<ChannelVideo> l;
            if (channelVideos.TryGetValue(channelId, out l)) return l;
            l = new List<ChannelVideo>();
            try
            {
                var path = VideosCachePath(channelId);
                if (System.IO.File.Exists(path)) l = Json.Deserialize<List<ChannelVideo>>(System.IO.File.ReadAllText(path)) ?? new List<ChannelVideo>();
            }
            catch { }
            channelVideos[channelId] = l;
            return l;
        }

        // Усі події календаря: відео з YouTube (кеш) + шортси, які AutoShorts запланував, але YouTube ще не віддав
        List<CalendarView.Entry> CalendarEntries(DateTime day)
        {
            var only = calChannel != null && calChannel.SelectedItem is ComboItem ? ((ComboItem)calChannel.SelectedItem).Value as string : null;
            var res = new List<KeyValuePair<DateTime, CalendarView.Entry>>();
            var seen = new HashSet<string>();
            foreach (var ch in settings.Channels)
            {
                if (only != null && ch.Id != only) continue;
                foreach (var v in CachedVideos(ch.Id))
                {
                    var k = BestTime.ToKyiv(v.WhenUtc);
                    if (k.Date != day) continue;
                    seen.Add(v.Id);
                    res.Add(new KeyValuePair<DateTime, CalendarView.Entry>(k, new CalendarView.Entry
                    {
                        Time = k.ToString("HH:mm"),
                        Title = (v.Scheduled ? "⏰ " : v.Privacy == "private" ? "🔒 " : v.IsLive ? "● " : !v.IsShort ? "▶ " : "") + v.Title,
                        Color = ChannelColor(ch.Id),
                        Tag = v.Id,
                        Dim = v.Scheduled || v.Privacy == "private" || v.Privacy == "unlisted"
                    }));
                }
            }
            foreach (var p in settings.Published)
            {
                if (seen.Contains(p.VideoId) || (only != null && p.ChannelId != only)) continue;
                var k = BestTime.ToKyiv(p.PublishAtUtc);
                if (k.Date != day) continue;
                res.Add(new KeyValuePair<DateTime, CalendarView.Entry>(k, new CalendarView.Entry
                {
                    Time = k.ToString("HH:mm"), Title = (p.PublishAtUtc > DateTime.UtcNow ? "⏰ " : "") + p.Title,
                    Color = ChannelColor(p.ChannelId), Tag = p.VideoId, Dim = p.PublishAtUtc > DateTime.UtcNow
                }));
            }
            return res.OrderBy(x => x.Key).Select(x => x.Value).ToList();
        }

        Page BuildCalendarPage()
        {
            calendar = new CalendarView { Dock = DockStyle.Fill };
            calendar.EntriesFor = CalendarEntries;
            calendar.EntryClicked += en => OpenUrl("https://youtube.com/watch?v=" + en.Tag);
            calTitle = Section2("");
            calStats = new Label { AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(14, 3, 3, 3) };
            calChannel = Dd(null);
            calChannel.SelectedIndexChanged += (s, e) => RefreshCalendar();
            calRefreshBtn = Btn("Оновити з YouTube", (s, e) => LoadChannelVideosAsync(true), ButtonKind.Secondary, Glyphs.Refresh);
            var bar = Header2(Row(calTitle, calStats),
                calChannel, calRefreshBtn,
                Btn("", (s, e) => MoveMonth(-1), ButtonKind.Secondary, Glyphs.Prev),
                Btn("Сьогодні", (s, e) => { calendar.Month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); RefreshCalendar(); }, ButtonKind.Secondary),
                Btn("", (s, e) => MoveMonth(1), ButtonKind.Secondary, Glyphs.Next));
            calLegend = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 8) };
            return new Page("Календар", "Усі відео ваших каналів за часом Києва: ⏰ — заплановані, 🔒 — приватні, ▶ — довгі відео. Натисніть, щоб відкрити на YouTube",
                Stack(bar, 0, calLegend, 0, calendar, 100));
        }

        void RefreshCalendarChannels()
        {
            if (calChannel == null) return;
            var cur = calChannel.SelectedItem is ComboItem ? ((ComboItem)calChannel.SelectedItem).Value as string : null;
            calChannel.Items.Clear();
            calChannel.Items.Add(new ComboItem("Усі канали", null));
            foreach (var ch in settings.Channels) calChannel.Items.Add(new ComboItem(ch.Title, ch.Id));
            int idx = calChannel.Items.FindIndex(x => ((ComboItem)x).Value as string == cur);
            calChannel.SelectedIndex = Math.Max(0, idx);
            calChannel.Relayout();
            calLegend.Controls.Clear();
            foreach (var ch in settings.Channels)
                calLegend.Controls.Add(new Label { Text = "●  " + ch.Title, AutoSize = true, ForeColor = ChannelColor(ch.Id), Margin = new Padding(2, 0, 16, 0) });
        }

        // Завантажує всі відео кожного каналу (опубліковані, заплановані, приватні). Без force — не частіше ніж раз на 30 хв.
        async void LoadChannelVideosAsync(bool force)
        {
            if (calLoading || settings.Channels.Count == 0) return;
            calLoading = true;
            calRefreshBtn.Enabled = false;
            try
            {
                foreach (var ch in settings.Channels.ToList())
                {
                    if (!force && DateTime.UtcNow - ch.VideosUpdatedUtc < TimeSpan.FromMinutes(30) && CachedVideos(ch.Id).Count > 0) continue;
                    calStats.Text = "Завантажую відео каналу «" + ch.Title + "»…";
                    try
                    {
                        var api = await YouTubeApi.ForChannelAsync(settings, ch);
                        var me = await api.MyChannelAsync();
                        var vids = await api.ChannelVideosAsync(ch.Id, me.Item3, 500);
                        channelVideos[ch.Id] = vids;
                        System.IO.File.WriteAllText(VideosCachePath(ch.Id), Json.Serialize(vids));
                        ch.VideosUpdatedUtc = DateTime.UtcNow;
                        settings.Save();
                        Log("Календар: «" + ch.Title + "» — " + vids.Count + " відео з YouTube");
                    }
                    catch (Exception ex) { Log("Календар, «" + ch.Title + "»: " + ex.Message); }
                    calendar.Invalidate();
                }
            }
            finally
            {
                calLoading = false;
                calRefreshBtn.Enabled = true;
                RefreshCalendar();
            }
        }

        void MoveMonth(int d)
        {
            calendar.Month = calendar.Month.AddMonths(d);
            RefreshCalendar();
        }

        void RefreshCalendar()
        {
            if (calendar == null) return;
            calendar.Today = BestTime.ToKyiv(DateTime.UtcNow).Date;
            calTitle.Text = Months[calendar.Month.Month - 1] + " " + calendar.Month.Year;
            if (calChannel.Items.Count != settings.Channels.Count + 1) RefreshCalendarChannels();
            int inMonth = 0, upcoming = 0;
            var first = calendar.Month;
            for (var d = first; d < first.AddMonths(1); d = d.AddDays(1))
            {
                var list = CalendarEntries(d);
                inMonth += list.Count;
                upcoming += list.Count(x => x.Title.StartsWith("⏰"));
            }
            if (!calLoading)
                calStats.Text = settings.Channels.Count == 0 ? "Підключіть канал, щоб бачити його відео"
                    : inMonth + " відео цього місяця" + (upcoming > 0 ? " · заплановано: " + upcoming : "");
            calendar.Invalidate();
        }
    }
}
