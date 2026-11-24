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
        Stepper perDayNum;
        Label channelHint;

        Dropdown reportChannelCombo;
        ListView reportList;
        Label reportSummary;
        StatTile statShorts, statLive, statViews, statAvg, statHour;

        Page BuildChannelsTab()
        {
            var lv = List("Канал", 240, "ID", 220, "Шортсів/день", 110, "Найкращий час (Київ)", 320, "Оновлено", 130);
            lv.MutedColumn = 1;
            lv.EmptyText = "Каналів ще немає — натисніть «Увійти через Google»";
            channelList = lv;
            channelList.SelectedIndexChanged += (s, e) =>
            {
                var ch = SelectedChannel();
                if (ch != null) perDayNum.Value = Math.Max(1, Math.Min(12, ch.ShortsPerDay));
            };
            perDayNum = new Stepper(104) { Minimum = 1, Maximum = 12, Value = 2 };
            channelHint = Hint("Кожен канал входить через свій Google-акаунт. Найкращий час рахується з переглядів ваших відео за перші 48 годин " +
                               "залежно від дня й години публікації. Якщо даних мало — 17:00–22:00 за Києвом, найсильніші пт і нд.");

            var google = new GoogleButton("Увійти через Google");
            google.Click += (s, e) => AddChannel();
            var connectText = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
            connectText.Controls.Add(new Label { Text = "Підключіть YouTube-канал", AutoSize = true, Font = Theme.Semi(12.5F), ForeColor = Theme.Text, Margin = new Padding(0, 0, 0, 4) });
            connectText.Controls.Add(new Label { Text = "Натисніть кнопку, виберіть акаунт і канал, дозвольте доступ — і готово. Для кожного каналу — свій вхід.", AutoSize = true, ForeColor = Theme.Secondary, MaximumSize = new Size(560, 0), Margin = new Padding(0) });
            var connect = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            connect.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            connect.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            connect.Controls.Add(connectText, 0, 0);
            google.Anchor = AnchorStyles.Right;
            connect.Controls.Add(google, 1, 0);

            var actions = CardOf(Stack(
                connect, 0,
                Row(Btn("Перерахувати найкращий час", (s, e) => RecalcChannel(), ButtonKind.Secondary, Glyphs.Calendar),
                    Btn("Видалити канал", (s, e) => RemoveChannel(), ButtonKind.Danger, Glyphs.Delete)), 0,
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

        async Task LoadAvatarAsync(ChannelInfo ch)
        {
            try
            {
                var api = await YouTubeApi.ForChannelAsync(settings, ch);
                var look = await api.ChannelLookAsync(ch.Id);
                ch.Handle = look.Item2;
                if (look.Item1 != null)
                {
                    var dir = System.IO.Path.Combine(AppSettings.LocalDir, "avatars");
                    System.IO.Directory.CreateDirectory(dir);
                    var path = System.IO.Path.Combine(dir, System.Text.RegularExpressions.Regex.Replace(ch.Id, @"[^\w\-]", "_") + ".jpg");
                    var bytes = await Net.Client.GetByteArrayAsync(look.Item1);
                    System.IO.File.WriteAllBytes(path, bytes);
                    ch.AvatarPath = path;
                }
                settings.Save();
                RefreshAccounts();
            }
            catch (Exception ex) { Log("Аватарка каналу «" + ch.Title + "»: " + ex.Message); }
        }

        async void LoadAvatarsAsync()
        {
            foreach (var ch in settings.Channels.ToList())
                if (ch.AvatarPath == null || !System.IO.File.Exists(ch.AvatarPath)) await LoadAvatarAsync(ch);
        }

        void RefreshChannels()
        {
            RefreshAccounts();
            RefreshCalendarChannels();
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
                var tb = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = Guide.Text, Font = Theme.F(10.5F), BorderStyle = BorderStyle.None, BackColor = Theme.Card, ForeColor = Theme.Text };
                DarkScroll(tb);
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
            if (!settings.HasGoogleClient())
            {
                if (!ShowConnectWizard()) return;
            }
            try
            {
                Log("Відкриваю вхід Google у браузері…");
                var tok = await GoogleAuth.SignInAsync(settings.GoogleClientId(), settings.GoogleClientSecret(), CancellationToken.None);
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
                ch.ClientId = settings.GoogleClientId();
                ch.ClientSecretProtected = AppSettings.Protect(settings.GoogleClientSecret());
                if (settings.DefaultChannelId == null) settings.DefaultChannelId = ch.Id;
                settings.Save();
                Log("Канал додано: " + ch.Title);
                RefreshChannels();
                Activate();
                await LoadAvatarAsync(ch);
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

        HeatMap heatMap;
        FlowLayoutPanel adviceList;
        Label heatNote;
        StatTile statSubs, statChViews, statVideos, stat30;

        Page BuildReportTab()
        {
            reportChannelCombo = Dd("Канал");
            reportChannelCombo.SelectedIndexChanged += (s, e) => { if (currentPage == PReport) LoadReport(); };
            var lv = List("Назва", 400, "Публікація (Київ)", 150, "Перегляди", 100, "Лайки", 80, "Коментарі", 95, "Статус", 100);
            lv.MutedColumn = 1;
            lv.EmptyText = "Тут будуть шортси, опубліковані через AutoShorts";
            reportList = lv;
            reportList.DoubleClick += (s, e) =>
            {
                if (reportList.SelectedItems.Count > 0) OpenUrl("https://youtube.com/shorts/" + reportList.SelectedItems[0].Tag);
            };
            reportSummary = new Label { AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(12, 3, 3, 3), MaximumSize = new Size(700, 0) };
            statSubs = new StatTile("Підписники");
            statChViews = new StatTile("Перегляди каналу");
            statVideos = new StatTile("Відео на каналі");
            stat30 = new StatTile("Відео за 30 днів");
            statAvg = new StatTile("Сер. перегляди (30 ост.)");
            statShorts = new StatTile("Шортсів AutoShorts");
            statLive = new StatTile("Уже вийшло");
            statViews = new StatTile("Їхні перегляди");
            statHour = new StatTile("Найкраща година");
            var tiles = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0) };
            tiles.Controls.AddRange(new Control[] { statSubs, statChViews, statVideos, stat30, statAvg });

            heatMap = new HeatMap { Dock = DockStyle.Fill };
            heatNote = new Label { AutoSize = true, ForeColor = Theme.Tertiary, Font = Theme.F(8.75F), MaximumSize = new Size(560, 0), Margin = new Padding(3, 6, 3, 0) };
            var heatCard = CardOf(Stack(Section("Коли ваші глядачі дивляться найбільше"), 0, heatMap, 0, heatNote, 0));
            adviceList = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            var adviceCard = CardOf(Stack(Section("Поради"), 0, adviceList, 0));
            var two = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0) };
            two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            heatCard.Margin = new Padding(0, 0, 14, 14);
            two.Controls.Add(heatCard, 0, 0);
            two.Controls.Add(adviceCard, 1, 0);

            var top = CardOf(Row(reportChannelCombo,
                Btn("Оновити статистику", (s, e) => LoadReport(true), ButtonKind.Primary, Glyphs.Refresh), reportSummary));
            var list = CardOf(Stack(Section("Шортси, опубліковані через AutoShorts"), 0, reportList, 100));
            list.Margin = new Padding(0);
            return new Page("Статистика", "Показники каналу, найкращий час для публікацій і поради",
                Stack(top, 0, tiles, 0, two, 0, list, 100));
        }

        void Advice(string glyph, string text, Color color)
        {
            var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 4, 0, 6) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Controls.Add(new Label { Text = glyph, AutoSize = true, ForeColor = color, Font = Theme.Semi(11F), Margin = new Padding(0, 0, 8, 0) }, 0, 0);
            row.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = Theme.Text, MaximumSize = new Size(Math.Max(240, adviceList.Parent != null ? adviceList.Parent.Width - 60 : 420), 0), Margin = new Padding(0, 2, 0, 0) }, 1, 0);
            adviceList.Controls.Add(row);
        }

        static string Short(long n)
        {
            if (n < 0) return "приховано";
            if (n >= 1000000) return (n / 1000000.0).ToString(n >= 10000000 ? "0" : "0.#") + " млн";
            if (n >= 10000) return (n / 1000.0).ToString("0") + " тис";
            return n.ToString("N0");
        }

        void SetTiles(string shorts, string live, string views, string avg, string hour)
        {
            statShorts.Set(shorts); statLive.Set(live); statViews.Set(views); statAvg.Set(avg); statHour.Set(hour);
        }

        static readonly string[] DayFull = { "неділю", "понеділок", "вівторок", "середу", "четвер", "п’ятницю", "суботу" };

        async void LoadReport(bool force = false)
        {
            var id = SelectedId(reportChannelCombo);
            var ch = id == null ? null : settings.FindChannel(id);
            adviceList.Controls.Clear();
            if (ch == null)
            {
                reportSummary.Text = settings.Channels.Count == 0 ? "Підключіть канал: «Канали» → «Увійти через Google»." : "Оберіть канал.";
                heatMap.Scores = null; heatMap.Invalidate();
                return;
            }
            reportSummary.Text = "Завантажую…";
            try
            {
                var api = await YouTubeApi.ForChannelAsync(settings, ch);
                // найкращий час — перераховуємо раз на тиждень
                if (force || DateTime.UtcNow - ch.BestTimeUpdatedUtc > TimeSpan.FromDays(7))
                {
                    reportSummary.Text = "Рахую найкращий час за переглядами ваших відео…";
                    try { await BestTime.RecalculateAsync(settings, ch, Log); settings.Save(); RefreshChannels(); } catch (Exception ex) { Log("Найкращий час: " + ex.Message); }
                }
                var cs = await api.ChannelStatsAsync(ch.Id);
                statSubs.Set(Short(cs[0])); statChViews.Set(Short(cs[1])); statVideos.Set(Short(cs[2]));

                // відео каналу (кеш календаря; за потреби — свіжі)
                if (force || CachedVideos(ch.Id).Count == 0 || DateTime.UtcNow - ch.VideosUpdatedUtc > TimeSpan.FromHours(6))
                {
                    var me = await api.MyChannelAsync();
                    var vids = await api.ChannelVideosAsync(ch.Id, me.Item3, 500);
                    channelVideos[ch.Id] = vids;
                    System.IO.File.WriteAllText(VideosCachePath(ch.Id), Json.Serialize(vids));
                    ch.VideosUpdatedUtc = DateTime.UtcNow;
                    settings.Save();
                }
                var all = CachedVideos(ch.Id).Where(v => v.Privacy == "public" && !v.Scheduled && v.WhenUtc <= DateTime.UtcNow).OrderByDescending(v => v.WhenUtc).ToList();
                var last30 = all.Where(v => v.WhenUtc > DateTime.UtcNow.AddDays(-30)).ToList();
                stat30.Set(last30.Count.ToString());
                var recent = all.Take(30).ToList();
                var rstats = recent.Count > 0 ? (await api.StatsAsync(recent.Select(v => v.Id))).ToDictionary(v => v.Id) : new Dictionary<string, VideoStats>();
                var withViews = recent.Where(v => rstats.ContainsKey(v.Id)).Select(v => new { v, views = rstats[v.Id].Views }).ToList();
                statAvg.Set(withViews.Count > 0 ? Short((long)withViews.Average(x => x.views)) : null);

                // теплова карта
                heatMap.Scores = BestTime.Scores(ch);
                heatMap.Invalidate();
                heatNote.Text = ch.UsingFallback
                    ? "Поки замало даних про ваш канал — показано типовий розклад для України (17:00–22:00, найсильніші пт і нд)."
                    : "За переглядами перших 48 годин ваших " + ch.VideosAnalyzed + " відео. Обведено три найкращі години.";

                // поради
                var sc = BestTime.Scores(ch);
                var top = Enumerable.Range(0, 168).OrderByDescending(i => sc[i]).Take(3).Select(i => new { d = i / 24, h = i % 24 }).ToList();
                Advice("★", "Найкраще публікувати в " + string.Join(", ", top.Select(t => DayFull[t.d] + " о " + t.h.ToString("00") + ":00")) + ".", Theme.Accent);
                var next = BestTime.NextSlotUtc(ch, settings.Published.Where(p => p.ChannelId == ch.Id).Select(p => p.PublishAtUtc), DateTime.UtcNow);
                Advice("⏰", "Найближчий вільний гарний слот — " + BestTime.Format(next) + " (за Києвом).", Theme.Text);
                var shorts = withViews.Where(x => x.v.IsShort).ToList();
                var longs = withViews.Where(x => !x.v.IsShort && !x.v.IsLive).ToList();
                if (shorts.Count >= 3 && longs.Count >= 3)
                {
                    double sa = shorts.Average(x => x.views), la = longs.Average(x => x.views);
                    Advice("▶", "Шортси набирають у середньому " + Short((long)sa) + " переглядів, довгі відео — " + Short((long)la) + ". " +
                        (sa > la * 1.3 ? "Шортси у вас працюють краще — варто робити їх частіше." : la > sa * 1.3 ? "Довгі відео сильніші — нарізайте з них шортси, щоб вести глядачів на повні версії." : "Обидва формати працюють приблизно однаково."), Theme.Text);
                }
                if (all.Count > 0)
                {
                    var days = (DateTime.UtcNow - all[0].WhenUtc).TotalDays;
                    if (days >= 3) Advice("!", "Останнє відео вийшло " + (int)days + " дн. тому. Регулярність важлива — краще 1 шортс щодня, ніж 7 за один день.", Theme.Orange);
                    else Advice("✓", "Ви публікуєте регулярно: " + last30.Count + " відео за 30 днів.", Theme.Green);
                }
                if (withViews.Count >= 5)
                {
                    var best = withViews.OrderByDescending(x => x.views).First();
                    var k = BestTime.ToKyiv(best.v.WhenUtc);
                    Advice("🔥", "Найкраще з останніх: «" + (best.v.Title.Length > 60 ? best.v.Title.Substring(0, 58) + "…" : best.v.Title) + "» — " + Short(best.views) +
                        " переглядів, вийшло в " + DayFull[(int)k.DayOfWeek] + " о " + k.ToString("HH:mm") + ". Наріжіть із нього ще шортсів.", Theme.Text);
                }
                Advice("i", "Ставте шортси на " + ch.ShortsPerDay + " на день (змінюється на сторінці «Канали»); AutoShorts сам розкладе їх по найкращих годинах.", Theme.Secondary);

                // шортси AutoShorts
                var mine = settings.Published.Where(p => p.ChannelId == ch.Id).ToList();
                reportList.BeginUpdate();
                reportList.Items.Clear();
                if (mine.Count > 0)
                {
                    var stats = (await api.StatsAsync(mine.Select(p => p.VideoId))).ToDictionary(v => v.Id);
                    foreach (var p in mine.OrderByDescending(p => p.PublishAtUtc))
                    {
                        VideoStats v;
                        stats.TryGetValue(p.VideoId, out v);
                        reportList.Items.Add(new ListViewItem(new[]
                        {
                            v != null ? v.Title : p.Title, BestTime.Format(p.PublishAtUtc),
                            v != null ? v.Views.ToString("N0") : "—", v != null ? v.Likes.ToString("N0") : "—",
                            v != null ? v.Comments.ToString("N0") : "—", v == null ? "видалено" : v.Privacy
                        }) { Tag = p.VideoId });
                    }
                }
                reportList.EndUpdate();
                reportSummary.Text = "Оновлено о " + DateTime.Now.ToString("HH:mm");
            }
            catch (Exception ex)
            {
                reportSummary.Text = "Помилка: " + ex.Message;
            }
        }
    }
}
