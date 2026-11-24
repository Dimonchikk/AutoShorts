using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoShorts
{
    class ComboItem
    {
        public string Text;
        public object Value;
        public ComboItem(string t, object v) { Text = t; Value = v; }
        public override string ToString() { return Text; }
    }

    public partial class MainForm : Form
    {
        readonly AppSettings settings;
        readonly List<Job> jobs;
        readonly Pipeline pipeline;
        CancellationTokenSource cts;
        bool running, publishing;

        SideNav nav;
        readonly List<Page> pages = new List<Page>();
        Panel content;
        LinkLabel updateLink;
        System.Windows.Forms.Timer updateTimer;

        // індекси сторінок
        const int PHome = 0, PClips = 1, PCalendar = 2, PReport = 3, PChannels = 4, PLog = 5, PSettings = 6, PCreate = 7;

        public MainForm()
        {
            settings = AppSettings.Load();
            jobs = AppSettings.LoadJobs();
            pipeline = new Pipeline(settings, Log, OnJobChanged);

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.F(9.75F);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Text = "AutoShorts";
            MinimumSize = new Size(1120, 740);
            Size = new Size(1320, 860);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            nav = new SideNav { Dock = DockStyle.Left, Subtitle = "Версія " + AppVersion.Text };
            nav.Items.Add(new SideNav.Item(Glyphs.Home, "Головна"));
            nav.Items.Add(new SideNav.Item(Glyphs.Clips, "Кліпи"));
            nav.Items.Add(new SideNav.Item(Glyphs.Calendar, "Календар"));
            nav.Items.Add(new SideNav.Item(Glyphs.Chart, "Статистика"));
            nav.Items.Add(new SideNav.Item(Glyphs.People, "Канали"));
            nav.Items.Add(new SideNav.Item(Glyphs.Log, "Журнал") { Bottom = true });
            nav.Items.Add(new SideNav.Item(Glyphs.Settings, "Налаштування") { Bottom = true });
            nav.Collapsed = settings.NavCollapsed;
            nav.Width = nav.Collapsed ? 76 : 232;
            nav.SelectedChanged += (s, e) => { if (!navSync) ShowPage(nav.SelectedIndex); };
            nav.CollapsedChanged += (s, e) =>
            {
                nav.Width = (int)((nav.Collapsed ? 76 : 232) * Theme.Dpi);
                settings.NavCollapsed = nav.Collapsed;
                try { settings.Save(); } catch { }
            };

            pages.Add(BuildHomePage());
            pages.Add(BuildClipsTab());
            pages.Add(BuildCalendarPage());
            pages.Add(BuildReportTab());
            pages.Add(BuildChannelsTab());
            pages.Add(BuildLogPage());
            pages.Add(BuildSettingsTab());
            pages.Add(BuildCreatePage());

            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            foreach (var p in pages) { p.Visible = false; content.Controls.Add(p); }

            updateLink = new LinkLabel
            {
                Dock = DockStyle.Top, Visible = false, Height = 40, TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.SidebarBg, LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentPressed,
                LinkBehavior = LinkBehavior.HoverUnderline, Font = Theme.Semi(10F)
            };
            updateLink.LinkClicked += (s, e) => UpdateClicked();
            content.Controls.Add(updateLink);

            Controls.Add(content);
            Controls.Add(nav);
            ShowPage(PHome);
            ResumeLayout(false);
            HandleCreated += (s, e) => Theme.StyleWindow(this);

            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => OnDropped(e.Data);

            RefreshChannelCombos();
            RefreshJobs();
            RefreshClips();
            RefreshChannels();
            UpdateNav();

            Shown += async (s, e) =>
            {
                Log("AutoShorts " + AppVersion.Text + " запущено. Обличчя: " + (FaceFrame.Available ? "детектор Windows доступний" : "недоступно (кадр по центру)"));
                if (!settings.HasGoogleClient())
                    Log("Щоб публікувати на YouTube, відкрийте «Канали» → «Увійти через Google».");
                LoadThumbsAsync();
                LoadAvatarsAsync();
                if (jobs.Any(j => !j.Done && j.Error == null)) StartStop();
                await CheckUpdatesAsync();
            };
            updateTimer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
            updateTimer.Tick += async (s, e) => await CheckUpdatesAsync();
            updateTimer.Start();
            Tools.Power = settings.Power;
            var pendingTimer = new System.Windows.Forms.Timer { Interval = 15 * 60 * 1000 };
            pendingTimer.Tick += (s, e) => RetryPending();
            pendingTimer.Start();
            Shown += (s, e) => RetryPending();
            FormClosing += OnClosing;
        }

        int currentPage = -1;
        bool navSync;

        void ShowPage(int index)
        {
            SuspendLayout();
            for (int i = 0; i < pages.Count; i++) pages[i].Visible = i == index;
            ResumeLayout(true);
            currentPage = index;
            navSync = true;
            nav.SelectedIndex = index < nav.Items.Count ? index : PHome;
            navSync = false;
            if (index == PCalendar) { RefreshCalendar(); LoadChannelVideosAsync(false); }
            if (index == PClips) RefreshClips();
            if (index == PReport && heatMap != null && heatMap.Scores == null) LoadReport();
        }

        void UpdateNav()
        {
            if (nav == null) return;
            int pending = jobs.Count(j => !j.Done && j.Error == null);
            int unpublished = jobs.Sum(j => j.Clips.Count(c => !c.Published && c.VideoPath != null));
            nav.Items[PHome].Badge = pending > 0 ? pending.ToString() : null;
            nav.Items[PClips].Badge = unpublished > 0 ? unpublished.ToString() : null;
            var cur = running ? jobs.FirstOrDefault(j => !j.Done && j.Error == null) : null;
            nav.Busy = running || publishing;
            nav.Status = publishing ? "Публікую на YouTube…" : running ? (cur != null ? "Обробка · " + cur.Progress + "%" : "Обробка…") : "Готово до роботи";
            nav.Invalidate();
        }

        async Task CheckUpdatesAsync()
        {
            var u = await Updates.CheckAsync();
            if (u == null) return;
            bool setup = u.Item2.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0;
            updateLink.Text = setup && Updates.IsInstalled
                ? "🎉  Вийшла AutoShorts " + u.Item1 + " — натисніть, щоб оновити зараз (1 хвилина)  →"
                : "🎉  Вийшла нова версія AutoShorts " + u.Item1 + " — натисніть, щоб завантажити  →";
            updateLink.Tag = u.Item2;
            updateLink.Visible = true;
        }

        async void UpdateClicked()
        {
            var url = updateLink.Tag as string ?? Updates.ReleasesUrl;
            if (url.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0 || !Updates.IsInstalled) { OpenUrl(url); return; }
            if (running || publishing)
            {
                MessageBox.Show("Зачекайте, поки закінчиться обробка або публікація, — тоді оновіть.", "AutoShorts");
                return;
            }
            try
            {
                updateLink.Enabled = false;
                var ok = await Updates.InstallAsync(url, p => Ui(() => updateLink.Text = "Завантажую оновлення… " + p + "%"));
                if (ok)
                {
                    AppSettings.SaveJobs(jobs);
                    try { settings.Save(); } catch { }
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                updateLink.Enabled = true;
                Log("Оновлення: " + ex.Message);
                OpenUrl(url);
            }
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if ((running || publishing) &&
                MessageBox.Show("Обробка ще триває. Закрити програму?", "AutoShorts", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            if (cts != null) cts.Cancel();
            AppSettings.SaveJobs(jobs);
            try { settings.Save(); } catch { }
        }

        // ---------- спільні помічники ----------

        static AButton Btn(string text, EventHandler click, ButtonKind kind = ButtonKind.Secondary, string glyph = null)
        {
            var b = new AButton(text, kind) { Glyph = glyph };
            b.Click += click;
            return b;
        }

        static Label Lbl(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Theme.Secondary, Margin = new Padding(8, 9, 2, 3) };
        }

        static Label Section(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Font = Theme.Semi(11F), ForeColor = Theme.Text, Margin = new Padding(3, 3, 3, 8) };
        }

        static Dropdown Dd(string prefix, params object[] items)
        {
            var d = new Dropdown { Prefix = prefix };
            d.Items.AddRange(items);
            if (items.Length > 0) d.SelectedIndex = 0;
            return d;
        }

        static void DarkScroll(Control c)
        {
            c.HandleCreated += (s, e) => { try { Theme.SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
        }

        static Label Caption(string text)
        {
            return new Label { Text = text, AutoSize = true, Font = Theme.F(9F), ForeColor = Theme.Secondary, Margin = new Padding(4, 10, 3, 2) };
        }

        static Label Hint(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Theme.Secondary, MaximumSize = new Size(1000, 0), Margin = new Padding(4, 6, 3, 3) };
        }

        static FlowLayoutPanel Row(params Control[] controls)
        {
            var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
            foreach (var c in controls)
            {
                c.Anchor = AnchorStyles.Left; // у рядку — по центру по вертикалі
                c.Margin = new Padding(c.Margin.Left, 3, c.Margin.Right, 3);
            }
            p.Controls.AddRange(controls);
            return p;
        }

        // Рядок: заголовок зліва, кнопки справа
        static Control Header(string title, params Control[] right)
        {
            var t = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var l = Section(title);
            l.Margin = new Padding(3, 3, 3, 3);
            t.Controls.Add(l, 0, 0);
            var f = Row(right);
            f.WrapContents = false;
            f.Dock = DockStyle.None;
            f.Anchor = AnchorStyles.Right;
            t.Controls.Add(f, 1, 0);
            return t;
        }

        static Card CardOf(Control content)
        {
            var c = new Card { Dock = DockStyle.Fill };
            content.Dock = DockStyle.Fill;
            c.Controls.Add(content);
            return c;
        }

        static AListView List(params object[] columns)
        {
            var lv = new AListView { Dock = DockStyle.Fill };
            for (int i = 0; i < columns.Length; i += 2) lv.Columns.Add((string)columns[i], (int)((int)columns[i + 1] * Theme.Dpi));
            return lv;
        }

        static TableLayoutPanel Stack(params object[] rows)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(0), Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows.Length; i += 2)
            {
                var c = (Control)rows[i];
                float pct = Convert.ToSingle(rows[i + 1]);
                t.RowStyles.Add(pct > 0 ? new RowStyle(SizeType.Percent, pct) : new RowStyle(SizeType.AutoSize));
                c.Dock = DockStyle.Fill;
                t.Controls.Add(c, 0, t.RowStyles.Count - 1);
            }
            t.RowCount = t.RowStyles.Count;
            return t;
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        void Ui(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(a); } catch { } }
            else a();
        }

        public void Log(string text)
        {
            var line = DateTime.Now.ToString("HH:mm:ss") + "  " + text;
            try { File.AppendAllText(Path.Combine(AppSettings.LocalDir, "log.txt"), DateTime.Now.ToString("yyyy-MM-dd ") + line + Environment.NewLine); } catch { }
            Ui(() =>
            {
                if (logBox == null) return;
                if (logBox.TextLength > 200000) logBox.Text = logBox.Text.Substring(100000);
                logBox.AppendText(line + Environment.NewLine);
            });
        }

        void OnJobChanged(Job job)
        {
            Ui(() => { UpdateJobRow(job); UpdateClipRows(job); UpdateNav(); });
        }

        void RefreshChannelCombos()
        {
            foreach (var combo in new[] { channelCombo, publishChannelCombo, reportChannelCombo })
            {
                if (combo == null) continue;
                var selected = combo.SelectedItem as ComboItem;
                var selId = selected != null ? selected.Value as string : settings.DefaultChannelId;
                combo.Items.Clear();
                if (combo != reportChannelCombo) combo.Items.Add(new ComboItem("не публікувати", null));
                foreach (var ch in settings.Channels) combo.Items.Add(new ComboItem(ch.Title, ch.Id));
                int idx = 0;
                for (int i = 0; i < combo.Items.Count; i++)
                    if (((ComboItem)combo.Items[i]).Value as string == selId) idx = i;
                combo.SelectedIndex = combo.Items.Count > 0 ? Math.Min(idx, combo.Items.Count - 1) : -1;
                combo.Relayout();
            }
        }

        static string SelectedId(Dropdown c)
        {
            var it = c.SelectedItem as ComboItem;
            return it == null ? null : it.Value as string;
        }
    }
}
