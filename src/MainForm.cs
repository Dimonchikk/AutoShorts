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

        TabControl tabs;
        LinkLabel updateLink;
        System.Windows.Forms.Timer updateTimer;

        public MainForm()
        {
            settings = AppSettings.Load();
            jobs = AppSettings.LoadJobs();
            pipeline = new Pipeline(settings, Log, OnJobChanged);

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.75F);
            Text = "AutoShorts " + AppVersion.Text + " — нарізка відео у шортси";
            MinimumSize = new Size(980, 680);
            Size = new Size(1180, 800);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
            tabs.TabPages.Add(BuildQueueTab());
            tabs.TabPages.Add(BuildClipsTab());
            tabs.TabPages.Add(BuildChannelsTab());
            tabs.TabPages.Add(BuildReportTab());
            tabs.TabPages.Add(BuildSettingsTab());
            Controls.Add(tabs);

            updateLink = new LinkLabel
            {
                Dock = DockStyle.Top, Visible = false, Height = 30, TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(255, 159, 10), LinkColor = Color.Black, ActiveLinkColor = Color.Black,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            updateLink.LinkClicked += (s, e) => OpenUrl(updateLink.Tag as string ?? Updates.ReleasesUrl);
            Controls.Add(updateLink);
            ResumeLayout(false);

            RefreshChannelCombos();
            RefreshJobs();
            RefreshClips();
            RefreshChannels();

            Shown += async (s, e) =>
            {
                Log("AutoShorts " + AppVersion.Text + " запущено. Обличчя: " + (FaceFrame.Available ? "детектор Windows доступний" : "недоступно (кадр по центру)"));
                if (string.IsNullOrWhiteSpace(settings.ClientId))
                    Log("Щоб публікувати на YouTube, заповніть Client ID у «Налаштуваннях» (там є інструкція).");
                await CheckUpdatesAsync();
            };
            updateTimer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
            updateTimer.Tick += async (s, e) => await CheckUpdatesAsync();
            updateTimer.Start();
            FormClosing += OnClosing;
        }

        async Task CheckUpdatesAsync()
        {
            var u = await Updates.CheckAsync();
            if (u == null) return;
            updateLink.Text = "🔄 Доступна нова версія AutoShorts v" + u.Item1 + " — натисніть, щоб завантажити";
            updateLink.Tag = u.Item2;
            updateLink.Visible = true;
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

        static Button Btn(string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 3, 8, 3), Margin = new Padding(3) };
            b.Click += click;
            return b;
        }

        static Label Lbl(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 9, 3, 3) };
        }

        static FlowLayoutPanel Row(params Control[] controls)
        {
            var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0) };
            p.Controls.AddRange(controls);
            return p;
        }

        static ListView List(params object[] columns)
        {
            var lv = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, Dock = DockStyle.Fill, GridLines = false };
            for (int i = 0; i < columns.Length; i += 2) lv.Columns.Add((string)columns[i], (int)columns[i + 1]);
            return lv;
        }

        static TableLayoutPanel Stack(params object[] rows)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(8) };
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
            Ui(() => { UpdateJobRow(job); UpdateClipRows(job); });
        }

        void RefreshChannelCombos()
        {
            foreach (var combo in new[] { channelCombo, publishChannelCombo, reportChannelCombo })
            {
                if (combo == null) continue;
                var selected = combo.SelectedItem as ComboItem;
                var selId = selected != null ? selected.Value as string : settings.DefaultChannelId;
                combo.Items.Clear();
                if (combo != reportChannelCombo) combo.Items.Add(new ComboItem("— не публікувати —", null));
                foreach (var ch in settings.Channels) combo.Items.Add(new ComboItem(ch.Title, ch.Id));
                int idx = 0;
                for (int i = 0; i < combo.Items.Count; i++)
                    if (((ComboItem)combo.Items[i]).Value as string == selId) idx = i;
                if (combo == reportChannelCombo && selId == null && combo.Items.Count > 0) idx = 0;
                if (combo.Items.Count > 0) combo.SelectedIndex = Math.Min(idx, combo.Items.Count - 1);
            }
        }

        static string SelectedId(ComboBox c)
        {
            var it = c.SelectedItem as ComboItem;
            return it == null ? null : it.Value as string;
        }
    }
}
