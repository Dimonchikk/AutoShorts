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

        TabPage BuildChannelsTab()
        {
            var page = new TabPage("3. Канали");
            channelList = List("Канал", 260, "ID", 220, "Шортсів/день", 100, "Найкращий час (Київ)", 330, "Оновлено", 120);
            channelList.SelectedIndexChanged += (s, e) =>
            {
                var ch = SelectedChannel();
                if (ch != null) perDayNum.Value = Math.Max(1, Math.Min(12, ch.ShortsPerDay));
            };
            perDayNum = new NumericUpDown { Minimum = 1, Maximum = 12, Value = 2, Width = 55 };
            channelHint = new Label
            {
                AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(1000, 0),
                Text = "Кожен канал входить через свій Google-акаунт. Найкращий час рахується з переглядів ваших відео за перші 48 годин " +
                       "залежно від дня й години публікації. Якщо даних мало — 17:00–22:00 за Києвом, найсильніші пт і нд."
            };
            page.Controls.Add(Stack(
                Row(Btn("＋ Додати канал (вхід Google)", (s, e) => AddChannel()), Btn("↻ Перерахувати найкращий час", (s, e) => RecalcChannel()),
                    Lbl("Шортсів на день:"), perDayNum, Btn("Зберегти", (s, e) => SavePerDay()), Btn("✕ Видалити канал", (s, e) => RemoveChannel()),
                    Btn("❔ Як підключити YouTube", (s, e) => ShowGuide())), 0,
                channelHint, 0,
                channelList, 100));
            return page;
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
            using (var f = new Form { Text = "Як підключити YouTube", Size = new Size(820, 640), StartPosition = FormStartPosition.CenterParent, Font = Font })
            {
                var tb = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Text = Guide.Text, Font = new Font("Segoe UI", 10F), BackColor = SystemColors.Window };
                var bar = Row(Btn("Відкрити Google Cloud Console", (s, e) => OpenUrl(Guide.ConsoleUrl)),
                              Btn("Форма аудиту YouTube API", (s, e) => OpenUrl("https://support.google.com/youtube/contact/yt_api_form")),
                              Btn("Закрити", (s, e) => f.Close()));
                bar.Dock = DockStyle.Bottom;
                f.Controls.Add(tb);
                f.Controls.Add(bar);
                f.ShowDialog(this);
            }
        }

        async void AddChannel()
        {
            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                if (MessageBox.Show("Спершу потрібен Client ID від Google (робиться один раз). Показати інструкцію?", "AutoShorts",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) ShowGuide();
                tabs.SelectedIndex = 4;
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

        TabPage BuildReportTab()
        {
            var page = new TabPage("4. Звіт");
            reportChannelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            reportList = List("Назва", 420, "Публікація (Київ)", 140, "Перегляди", 90, "Лайки", 70, "Коментарі", 85, "Статус", 90);
            reportList.DoubleClick += (s, e) =>
            {
                if (reportList.SelectedItems.Count > 0) OpenUrl("https://youtube.com/shorts/" + reportList.SelectedItems[0].Tag);
            };
            reportSummary = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 8, 3, 8), MaximumSize = new Size(1100, 0) };
            page.Controls.Add(Stack(
                Row(Lbl("Канал:"), reportChannelCombo, Btn("↻ Оновити статистику", (s, e) => LoadReport())), 0,
                reportSummary, 0,
                reportList, 100));
            return page;
        }

        async void LoadReport()
        {
            var id = SelectedId(reportChannelCombo);
            var ch = id == null ? null : settings.FindChannel(id);
            if (ch == null) { reportSummary.Text = "Оберіть канал."; return; }
            var mine = settings.Published.Where(p => p.ChannelId == ch.Id).ToList();
            if (mine.Count == 0) { reportSummary.Text = "Ще немає шортсів, опублікованих через AutoShorts на цьому каналі."; reportList.Items.Clear(); return; }
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
                reportSummary.Text = "Шортсів: " + mine.Count + " (вийшло: " + live + ") · переглядів: " + total.ToString("N0") +
                                     " · в середньому: " + (live > 0 ? (total / live).ToString("N0") : "—") +
                                     (bestHour.HasValue ? " · найкраща година публікації: " + bestHour.Value.ToString("00") + ":00" : "");
            }
            catch (Exception ex)
            {
                reportSummary.Text = "Помилка: " + ex.Message;
            }
        }
    }
}
