using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoShorts
{
    public partial class MainForm
    {
        ListView channelList;
        NumericUpDown perDayNum;
        Label channelHint;

        ComboBox reportChannelCombo;
        ListView reportList;
        Label reportSummary;
        StatTile statShorts, statLive, statViews, statAvg, statHour;

        Page BuildChannelsTab()
        {
            var lv = List("Канал", 240, "ID", 220, "Шортсів/день", 110, "Найкращий час (Київ)", 320, "Оновлено", 130);
            lv.MutedColumn = 1;
            lv.EmptyText = "Каналів ще немає — натисніть «Додати канал»";
            channelList = lv;
            channelList.SelectedIndexChanged += (s, e) =>
            {
                var ch = SelectedChannel();
                if (ch != null) perDayNum.Value = Math.Max(1, Math.Min(12, ch.ShortsPerDay));
            };
            perDayNum = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 2, Width = 58 };
            channelHint = Hint("Кожен канал входить через свій Google-акаунт. Найкращий час рахується з переглядів ваших відео за перші 48 годин " +
                               "залежно від дня й години публікації. Якщо даних мало — 17:00–22:00 за Києвом, найсильніші пт і нд.");

            var actions = CardOf(Stack(
                Row(Btn("Додати канал", (s, e) => AddChannel(), ButtonKind.Primary, Glyphs.Add),
                    Btn("Перерахувати найкращий час", (s, e) => RecalcChannel(), ButtonKind.Secondary, Glyphs.Calendar),
                    Btn("Видалити", (s, e) => RemoveChannel(), ButtonKind.Danger, Glyphs.Delete),
                    Btn("Як підключити YouTube", (s, e) => ShowGuide(), ButtonKind.Plain, Glyphs.Help)), 0,
                channelHint, 0));

            var list = CardOf(Stack(
                Header("Підключені канали", Lbl("Шортсів на день"), perDayNum, Btn("Зберегти", (s, e) => SavePerDay(), ButtonKind.Secondary, Glyphs.Save)), 0,
                channelList, 100));
            list.Margin = new Padding(0);

            return new Page("Канали", "Підключіть YouTube-канали й задайте, скільки шортсів на день публікувати",
                Stack(actions, 0, list, 100));
        }

        ChannelInfo SelectedChannel()
        {
            return channelList.SelectedItems.Count == 0 ? null : channelList.SelectedItems[0].Tag as ChannelInfo;
        }

        void RefreshChannels()
        {
            var sel = SelectedChannel();
            channelList.BeginUpdate();
            channelList.Items.Clear();
            foreach (var ch in settings.Channels)
            {
                var it = new ListViewItem(new[]
                {
                    ch.Title, ch.Id, ch.ShortsPerDay.ToString(), BestTime.Describe(ch),
                    ch.BestTimeUpdatedUtc.Year > 2000 ? ch.BestTimeUpdatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "—"
                }) { Tag = ch };
                channelList.Items.Add(it);
                if (sel == ch) it.Selected = true;
            }
            channelList.EndUpdate();
            RefreshChannelCombos();
            UpdateSlotInfo();
        }

        void ShowGuide()
        {
            using (var f = new Form { Text = "Як підключити YouTube", Size = new Size(840, 660), StartPosition = FormStartPosition.CenterParent, Font = Font, BackColor = Theme.Bg, ShowInTaskbar = false })
            {
                f.HandleCreated += (s, e) => Theme.StyleWindow(f);
                var tb = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = Guide.Text, Font = Theme.F(10.5F), BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Theme.Text };
                var card = CardOf(tb);
                card.Margin = new Padding(0);
                var bar = Row(Btn("Відкрити Google Cloud Console", (s, e) => OpenUrl(Guide.ConsoleUrl), ButtonKind.Primary, Glyphs.Link),
                              Btn("Форма аудиту YouTube API", (s, e) => OpenUrl("https://support.google.com/youtube/contact/yt_api_form"), ButtonKind.Secondary),
                              Btn("Закрити", (s, e) => f.Close(), ButtonKind.Plain));
                bar.Margin = new Padding(0, 12, 0, 0);
                var body = Stack(card, 100, bar, 0);
                var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
                host.Controls.Add(body);
                f.Controls.Add(host);
                f.ShowDialog(this);
            }
        }

        async void AddChannel()
        {
            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                if (MessageBox.Show("Спершу потрібен Client ID від Google (робиться один раз). Показати інструкцію?", "AutoShorts",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) ShowGuide();
                ShowPage(4);
                return;
            }
            try
            {
                Log("Відкриваю вхід Google у браузері…");
                var tok = await GoogleAuth.SignInAsync(settings.ClientId, settings.ClientSecret, CancellationToken.None);
                var api = new YouTubeApi(tok.AccessToken);
                var me = await api.MyChannelAsync();
                var ch = settings.FindChannel(me.Item1);
                if (ch == null)
                {
                    ch = new ChannelInfo { Id = me.Item1 };
                    settings.Channels.Add(ch);
                }
                ch.Title = me.Item2;
                ch.RefreshTokenProtected = AppSettings.Protect(tok.RefreshToken);
                if (settings.DefaultChannelId == null) settings.DefaultChannelId = ch.Id;
                settings.Save();
                Log("Канал додано: " + ch.Title);
                RefreshChannels();
                Activate();
                await RecalcAsync(ch);
            }
            catch (Exception ex)
            {
                Log("Помилка входу: " + ex.Message);
                MessageBox.Show(ex.Message, "Вхід Google", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        async Task RecalcAsync(ChannelInfo ch)
        {
            try
            {
                await BestTime.RecalculateAsync(settings, ch, Log);
                settings.Save();
            }
            catch (Exception ex)
            {
                if (ch.HourScores == null) ch.HourScores = BestTime.Fallback();
                Log("Не вдалося порахувати найкращий час: " + ex.Message);
            }
            RefreshChannels();
        }

        async void RecalcChannel()
        {
            var ch = SelectedChannel();
            if (ch == null) { MessageBox.Show("Оберіть канал у списку.", "AutoShorts"); return; }
            await RecalcAsync(ch);
        }

        void SavePerDay()
        {
            var ch = SelectedChannel();
            if (ch == null) return;
            ch.ShortsPerDay = (int)perDayNum.Value;
            settings.Save();
            RefreshChannels();
        }

        void RemoveChannel()
        {
            var ch = SelectedChannel();
            if (ch == null) return;
            if (MessageBox.Show("Видалити канал «" + ch.Title + "» з програми?", "AutoShorts", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            settings.Channels.Remove(ch);
            if (settings.DefaultChannelId == ch.Id) settings.DefaultChannelId = null;
            settings.Save();
            RefreshChannels();
        }

        // ---------- звіт ----------

        Page BuildReportTab()
        {
            reportChannelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
            var lv = List("Назва", 400, "Публікація (Київ)", 150, "Перегляди", 100, "Лайки", 80, "Коментарі", 95, "Статус", 100);
            lv.MutedColumn = 1;
            lv.EmptyText = "Оберіть канал і натисніть «Оновити статистику»";
            reportList = lv;
            reportList.DoubleClick += (s, e) =>
            {
                if (reportList.SelectedItems.Count > 0) OpenUrl("https://youtube.com/shorts/" + reportList.SelectedItems[0].Tag);
            };
            reportSummary = new Label { AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(12, 3, 3, 3), MaximumSize = new Size(700, 0) };
            statShorts = new StatTile("Шортсів");
            statLive = new StatTile("Уже вийшло");
            statViews = new StatTile("Переглядів");
            statAvg = new StatTile("У середньому");
            statHour = new StatTile("Найкраща година");
            var tiles = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            tiles.Controls.AddRange(new Control[] { statShorts, statLive, statViews, statAvg, statHour });

            var top = CardOf(Row(Lbl("Канал"), reportChannelCombo,
                Btn("Оновити статистику", (s, e) => LoadReport(), ButtonKind.Primary, Glyphs.Refresh), reportSummary));
            var list = CardOf(Stack(Section("Шортси"), 0, reportList, 100));
            list.Margin = new Padding(0);
            return new Page("Звіт", "Перегляди, лайки й коментарі шортсів, опублікованих через AutoShorts",
                Stack(top, 0, tiles, 0, list, 100));
        }

        void SetTiles(string shorts, string live, string views, string avg, string hour)
        {
            statShorts.Set(shorts); statLive.Set(live); statViews.Set(views); statAvg.Set(avg); statHour.Set(hour);
        }

        async void LoadReport()
        {
            var id = SelectedId(reportChannelCombo);
            var ch = id == null ? null : settings.FindChannel(id);
            if (ch == null) { reportSummary.Text = "Оберіть канал."; SetTiles(null, null, null, null, null); return; }
            var mine = settings.Published.Where(p => p.ChannelId == ch.Id).ToList();
            if (mine.Count == 0) { reportSummary.Text = "Ще немає шортсів, опублікованих через AutoShorts на цьому каналі."; reportList.Items.Clear(); SetTiles("0", null, null, null, null); return; }
            reportSummary.Text = "Завантажую…";
            try
            {
                var api = await YouTubeApi.ForChannelAsync(settings, ch);
                var stats = (await api.StatsAsync(mine.Select(p => p.VideoId))).ToDictionary(v => v.Id);
                reportList.BeginUpdate();
                reportList.Items.Clear();
                long total = 0; int live = 0;
                var byHour = new Dictionary<int, List<long>>();
                foreach (var p in mine.OrderByDescending(p => p.PublishAtUtc))
                {
                    VideoStats v;
                    stats.TryGetValue(p.VideoId, out v);
                    var it = new ListViewItem(new[]
                    {
                        v != null ? v.Title : p.Title, BestTime.Format(p.PublishAtUtc),
                        v != null ? v.Views.ToString("N0") : "—", v != null ? v.Likes.ToString("N0") : "—",
                        v != null ? v.Comments.ToString("N0") : "—", v == null ? "видалено" : v.Privacy
                    }) { Tag = p.VideoId };
                    reportList.Items.Add(it);
                    if (v != null)
                    {
                        total += v.Views;
                        if (p.PublishAtUtc < DateTime.UtcNow)
                        {
                            live++;
                            int h = BestTime.ToKyiv(p.PublishAtUtc).Hour;
                            if (!byHour.ContainsKey(h)) byHour[h] = new List<long>();
                            byHour[h].Add(v.Views);
                        }
                    }
                }
                reportList.EndUpdate();
                var bestHour = byHour.OrderByDescending(kv => kv.Value.Average()).Select(kv => kv.Key).Cast<int?>().FirstOrDefault();
                reportSummary.Text = "Оновлено о " + DateTime.Now.ToString("HH:mm");
                SetTiles(mine.Count.ToString("N0"), live.ToString("N0"), total.ToString("N0"),
                    live > 0 ? (total / live).ToString("N0") : null,
                    bestHour.HasValue ? bestHour.Value.ToString("00") + ":00" : null);
            }
            catch (Exception ex)
            {
                reportSummary.Text = "Помилка: " + ex.Message;
            }
        }
    }
}
