using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoShorts
{
    public partial class MainForm
    {
        TextBox urlBox, logBox;
        Field urlField;
        AButton getClipsBtn, tabLink, tabFile, pauseBtn;
        Panel linkPane, filePane;
        TileView projectGrid;
        Label projectsCount;
        bool fileMode;

        static readonly string[] VideoExts = { ".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v", ".wmv", ".flv", ".ts" };

        Page BuildHomePage()
        {
            // --- центральний блок: вкладки, поле посилання, біла кнопка ---
            tabLink = Btn("Нарізати відео за посиланням", (s, e) => SetFileMode(false), ButtonKind.Secondary, Glyphs.Link);
            tabFile = Btn("Файл з комп’ютера", (s, e) => SetFileMode(true), ButtonKind.Plain, Glyphs.File);
            var openFolder = Btn("Папка з кліпами", (s, e) => { Directory.CreateDirectory(settings.OutputFolder); Process.Start("explorer.exe", Tools.Q(settings.OutputFolder)); }, ButtonKind.Plain, Glyphs.Folder);
            openFolder.ForeColor = Theme.Secondary;

            urlBox = new TextBox { Multiline = true, Font = Theme.F(11.5F), WordWrap = false };
            urlBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; GetClips(); } };
            urlField = new Field(urlBox, 26, "Вставте посилання на YouTube — можна кілька, кожне з нового рядка") { Glyph = Glyphs.Link, PadFactor = 1.6f };
            urlField.UpdatePadding();
            getClipsBtn = Btn("Отримати кліпи в 1 клік", (s, e) => GetClips(), ButtonKind.Primary);
            getClipsBtn.HeightFactor = 2.6f;
            getClipsBtn.Margin = new Padding(10, 3, 3, 6);
            var inputRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0) };
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            urlField.Dock = DockStyle.Fill;
            inputRow.Controls.Add(urlField, 0, 0);
            getClipsBtn.Anchor = AnchorStyles.Right;
            inputRow.Controls.Add(getClipsBtn, 1, 0);
            linkPane = new Panel { AutoSize = true, Margin = new Padding(0) };
            inputRow.Dock = DockStyle.Top;
            linkPane.Controls.Add(inputRow);

            var pick = Btn("Вибрати відео на комп’ютері…", (s, e) => PickFiles(), ButtonKind.Primary, Glyphs.Upload);
            pick.HeightFactor = 2.6f;
            var dropHint = new Label { Text = "або просто перетягніть файли у вікно · MP4, MOV, MKV, WEBM · субтитри .srt з такою ж назвою підхопляться самі", AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(12, 3, 3, 3) };
            filePane = new Panel { AutoSize = true, Visible = false, Margin = new Padding(0) };
            var fr = Row(pick, dropHint);
            fr.Dock = DockStyle.Top;
            filePane.Controls.Add(fr);

            var under = new Label
            {
                Text = "Працює без ключів ШІ: моменти знаходяться за графіком «найчастіше переглядають», сплесками гучності та щільністю мови.",
                AutoSize = true, ForeColor = Theme.Tertiary, Font = Theme.F(9F), Margin = new Padding(6, 8, 3, 0)
            };

            var heroInner = Stack(Header2(Row(tabLink, tabFile), openFolder), 0, linkPane, 0, filePane, 0, under, 0);
            var hero = CardOf(heroInner);
            hero.Padding = new Padding(20, 16, 20, 18);
            hero.Margin = new Padding(0);
            hero.Fill = Color.FromArgb(22, 22, 25);
            hero.BackColor = hero.Fill;

            // --- швидкі перемикачі рецепту, як «рецепти» OpusClip ---
            var chips = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 16, 0, 0), Anchor = AnchorStyles.None };
            Action<string, string, Func<Recipe, bool>, Action<Recipe, bool>> chip = (text, glyph, get, set) =>
            {
                var b = Btn(text, null, ButtonKind.Chip, glyph);
                b.Checked = get(settings.Recipe);
                b.Click += (s, e) => { b.Checked = !b.Checked; set(settings.Recipe, b.Checked); b.Invalidate(); try { settings.Save(); } catch { } };
                chips.Controls.Add(b);
            };
            chip("Субтитри", Glyphs.Captions, r => r.Captions, (r, v) => r.Captions = v);
            chip("Без пауз", Glyphs.Clock, r => r.CutPauses, (r, v) => r.CutPauses = v);
            chip("Без реклами", Glyphs.Stop, r => r.SkipAds, (r, v) => r.SkipAds = v);
            chip("Кадр за мовцем", Glyphs.People, r => r.TrackFace, (r, v) => r.TrackFace = v);
            chip("Заголовок-гачок", Glyphs.Sparkle, r => r.Hook, (r, v) => r.Hook = v);
            chip("Чистий звук", Glyphs.Refresh, r => r.EnhanceAudio, (r, v) => r.EnhanceAudio = v);
            chip("Автонаближення", Glyphs.Add, r => r.AutoZoom, (r, v) => r.AutoZoom = v);
            chip("Яскраві кольори", Glyphs.Sparkle, r => r.ColorBoost, (r, v) => r.ColorBoost = v);
            chip("Ціле відео на розмитому фоні", Glyphs.Video, r => r.Layout == "fit", (r, v) => r.Layout = v ? "fit" : "crop");
            chip("Плавні переходи", Glyphs.Play, r => r.Fades, (r, v) => r.Fades = v);
            homeChips = chips;

            var heroHost = new TableLayoutPanel { ColumnCount = 3, RowCount = 2, AutoSize = true, Margin = new Padding(0, 6, 0, 26) };
            heroHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            heroHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 820 * Theme.Dpi));
            heroHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            heroHost.Controls.Add(hero, 1, 0);
            heroHost.Controls.Add(chips, 1, 1);
            heroHost.Resize += (s, e) =>
            {
                float w = Math.Max(400, Math.Min(820 * Theme.Dpi, heroHost.Width - 20));
                if (Math.Abs(heroHost.ColumnStyles[1].Width - w) > 1) heroHost.ColumnStyles[1].Width = w;
            };

            // --- проєкти ---
            projectGrid = new TileView
            {
                TileSize = new Size((int)(270 * Theme.Dpi), (int)(244 * Theme.Dpi)),
                HeightFor = w => w * 9 / 16 + Font.Height * 5,
                Gap = (int)(20 * Theme.Dpi),
                Painter = PaintProject,
                EmptyText = "Тут з’являться ваші проєкти",
                EmptyHint = "Вставте посилання вище або перетягніть відео у вікно"
            };
            projectGrid.TileClicked += (o, p, b) =>
            {
                var j = (Job)o;
                if (b == MouseButtons.Right) { ProjectMenu(j); return; }
                if (j.Clips.Count > 0) OpenProject(j);
            };
            projectsCount = new Label { AutoSize = true, ForeColor = Theme.Tertiary, Margin = new Padding(10, 3, 3, 3) };
            pauseBtn = Btn("Пауза", (s, e) => StartStop(), ButtonKind.Plain, Glyphs.Pause);
            var projHead = Header2(Row(Section2("Мої проєкти"), projectsCount),
                pauseBtn,
                Btn("Очистити завершені", (s, e) => ClearDone(), ButtonKind.Plain, Glyphs.Clear));

            var body = Stack(heroHost, 0, projHead, 0, projectGrid, 100);
            return new Page(null, null, body);
        }

        FlowLayoutPanel homeChips;

        static Label Section2(string text)
        {
            return new Label { Text = text, AutoSize = true, Font = Theme.Semi(12.5F), ForeColor = Theme.Text, Margin = new Padding(0, 3, 3, 3) };
        }

        // заголовок рядка: ліворуч один елемент, праворуч кнопки
        static Control Header2(Control left, params Control[] right)
        {
            var t = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.Anchor = AnchorStyles.Left;
            left.Dock = DockStyle.None;
            t.Controls.Add(left, 0, 0);
            var f = Row(right);
            f.WrapContents = false;
            f.Dock = DockStyle.None;
            f.Anchor = AnchorStyles.Right;
            t.Controls.Add(f, 1, 0);
            return t;
        }

        void SetFileMode(bool file)
        {
            fileMode = file;
            tabLink.Kind = file ? ButtonKind.Plain : ButtonKind.Secondary;
            tabFile.Kind = file ? ButtonKind.Secondary : ButtonKind.Plain;
            tabLink.Invalidate(); tabFile.Invalidate();
            linkPane.Visible = !file;
            filePane.Visible = file;
        }

        // ---------- малювання карток проєктів ----------

        static void Pill(Graphics g, string text, Font f, int x, int y, Color bg, Color fg, bool alignRight = false)
        {
            var sz = TextRenderer.MeasureText(g, text, f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int h = f.Height + 6, w = sz.Width + f.Height;
            var r = new Rectangle(alignRight ? x - w : x, y, w, h);
            Theme.FillRound(g, bg, r, h / 2f);
            TextRenderer.DrawText(g, text, f, r, fg, Theme.Center);
        }

        internal static void DrawCover(Graphics g, Image img, Rectangle r, float rad, Color back, string placeholderGlyph)
        {
            Theme.Smooth(g);
            if (img != null) g.DrawImage(img, r);
            else
            {
                using (var br = new LinearGradientBrush(r, Color.FromArgb(38, 38, 44), Color.FromArgb(24, 24, 28), 90f)) g.FillRectangle(br, r);
                var icons = Theme.Icons(18f);
                if (icons != null && placeholderGlyph != null) TextRenderer.DrawText(g, placeholderGlyph, icons, r, Theme.Tertiary, Theme.Center);
            }
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2));
                using (var inner = Theme.Round(new RectangleF(r.X, r.Y, r.Width - 0.5f, r.Height - 0.5f), rad)) path.AddPath(inner, false);
                path.FillMode = FillMode.Alternate;
                using (var b = new SolidBrush(back)) g.FillPath(b, path);
            }
        }

        static string Dur(double s)
        {
            if (s <= 0) return null;
            var ts = TimeSpan.FromSeconds(s);
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }

        void PaintProject(Graphics g, object o, Rectangle r, bool hover, bool sel)
        {
            var j = (Job)o;
            var f = Font;
            float rad = f.Height * 0.9f;
            int th = r.Width * 9 / 16;
            var tr = new Rectangle(r.X, r.Y, r.Width, th);
            DrawCover(g, Thumbs.Get(j.ThumbPath, tr.Width, tr.Height), tr, rad, Theme.Bg, j.IsLocal() ? Glyphs.File : Glyphs.Video);
            Theme.Smooth(g);
            if (hover) using (var p = Theme.Round(tr, rad)) using (var b = new SolidBrush(Color.FromArgb(28, 255, 255, 255))) g.FillPath(b, p);

            var small = Theme.Semi(f.SizeInPoints * 0.85f);
            var pillBg = Color.FromArgb(190, 12, 12, 14);
            int pad = f.Height * 2 / 3;
            bool current = running && jobs.FirstOrDefault(x => !x.Done && x.Error == null) == j;
            if (j.Error != null) Pill(g, "Помилка", small, tr.X + pad, tr.Y + pad, Color.FromArgb(220, 127, 29, 29), Color.White);
            else if (j.Done) Pill(g, j.Clips.Count + " кліпів", small, tr.X + pad, tr.Y + pad, pillBg, Theme.Accent);
            else if (current) Pill(g, "Обробка · " + j.Progress + "%", small, tr.X + pad, tr.Y + pad, pillBg, Theme.Text);
            else Pill(g, "У черзі", small, tr.X + pad, tr.Y + pad, pillBg, Theme.Secondary);
            var d = Dur(j.SourceDuration);
            if (d != null) Pill(g, d, small, tr.Right - pad, tr.Bottom - pad - small.Height - 6, pillBg, Theme.Text, true);
            if (j.Recipe != null && (j.RangeEnd > 0 || j.RangeStart > 0))
                Pill(g, Dur(j.RangeStart) ?? "0:00", small, tr.X + pad, tr.Bottom - pad - small.Height - 6, pillBg, Theme.Secondary);
            if (!j.Done && j.Error == null && j.Progress > 0)
            {
                float bh = Math.Max(4, f.Height / 4f);
                var track = new RectangleF(tr.X + pad, tr.Bottom - pad - bh, tr.Width - pad * 2, bh);
                if (d != null) track.Y -= small.Height + 10;
                Theme.FillRound(g, Color.FromArgb(120, 0, 0, 0), track, bh / 2);
                Theme.FillRound(g, Theme.Accent, new RectangleF(track.X, track.Y, Math.Max(bh, track.Width * j.Progress / 100f), bh), bh / 2);
            }

            var titleR = new Rectangle(r.X + 2, tr.Bottom + f.Height / 2, r.Width - 4, f.Height * 2 + 4);
            TextRenderer.DrawText(g, j.DisplayName, Theme.Semi(f.SizeInPoints), titleR, Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            string sub;
            Color subC = Theme.Secondary;
            if (j.Error != null) { sub = j.Error; subC = Theme.Red; }
            else if (j.Done)
            {
                int pub = j.Clips.Count(c => c.Published);
                int best = j.Clips.Count > 0 ? j.Clips.Max(c => c.Virality) : 0;
                sub = (best > 0 ? "найкращий " + best + "/100 · " : "") + (pub > 0 ? "опубліковано " + pub + " з " + j.Clips.Count : "готово до перегляду");
            }
            else sub = j.Status;
            TextRenderer.DrawText(g, sub ?? "", Theme.F(f.SizeInPoints * 0.9f), new Rectangle(r.X + 2, titleR.Bottom + 2, r.Width - 4, f.Height + 2), subC, Theme.LeftMid);
        }

        void ProjectMenu(Job j)
        {
            var m = DarkRenderer.Menu(Font);
            if (j.Clips.Count > 0) m.Items.Add("Відкрити кліпи", null, (s, e) => OpenProject(j));
            if (j.Error != null || j.Done) m.Items.Add("Обробити заново", null, (s, e) => RetryJob(j));
            m.Items.Add(j.IsLocal() ? "Відкрити файл" : "Відкрити відео на YouTube", null, (s, e) => OpenUrl(j.Url));
            m.Items.Add("Папка з кліпами", null, (s, e) => OpenJobFolder(j));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Видалити проєкт", null, (s, e) => RemoveJob(j));
            m.Closed += (s, e) => BeginInvoke(new Action(() => m.Dispose()));
            m.Show(Cursor.Position);
        }

        void OpenProject(Job j)
        {
            ShowPage(PClips);
            SetClipFilter(j);
        }

        // ---------- додавання відео ----------

        void OnDropped(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = ((string[])data.GetData(DataFormats.FileDrop)).Where(f => VideoExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                if (files.Count == 0) { MessageBox.Show("Перетягніть відеофайл (MP4, MOV, MKV…).", "AutoShorts"); return; }
                OpenCreate(files);
            }
            else if (data.GetDataPresent(DataFormats.Text))
            {
                urlBox.Text = (string)data.GetData(DataFormats.Text);
                SetFileMode(false);
                GetClips();
            }
        }

        void PickFiles()
        {
            using (var d = new OpenFileDialog { Multiselect = true, Title = "Виберіть відео", Filter = "Відео|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.m4v;*.wmv;*.flv;*.ts|Усі файли|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) OpenCreate(d.FileNames.ToList());
        }

        void GetClips()
        {
            var lines = urlBox.Text.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => l.StartsWith("http", StringComparison.OrdinalIgnoreCase) || l.Contains("youtu"))
                .Select(l => l.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? l : "https://" + l).Distinct().ToList();
            if (lines.Count == 0)
            {
                MessageBox.Show("Вставте хоча б одне посилання на відео.", "AutoShorts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OpenCreate(lines);
        }

        // ---------- список проєктів ----------

        void RefreshJobs()
        {
            if (projectGrid == null) return;
            projectGrid.Items.Clear();
            foreach (var j in Enumerable.Reverse(jobs)) projectGrid.Items.Add(j);
            projectGrid.RefreshItems();
            int pending = jobs.Count(j => !j.Done && j.Error == null);
            projectsCount.Text = jobs.Count == 0 ? "" : jobs.Count + (pending > 0 ? " · у роботі " + pending : "");
            pauseBtn.Visible = pending > 0 || running;
            pauseBtn.Text = running ? "Пауза" : "Продовжити";
            pauseBtn.Glyph = running ? Glyphs.Pause : Glyphs.Play;
            UpdateNav();
        }

        void UpdateJobRow(Job j)
        {
            if (projectGrid != null) projectGrid.Invalidate();
        }

        async void LoadThumbsAsync()
        {
            foreach (var j in jobs.ToList())
            {
                if (j.ThumbPath != null && File.Exists(j.ThumbPath)) continue;
                try { await Thumbs.EnsureAsync(j, CancellationToken.None); } catch { }
                if (projectGrid != null) projectGrid.Invalidate();
            }
        }

        void RetryJob(Job j)
        {
            j.Error = null; j.Done = false; j.Status = "В черзі"; j.Progress = 0;
            if (j.Clips.All(c => !c.Published)) j.Clips.Clear();
            AppSettings.SaveJobs(jobs);
            RefreshJobs();
            if (!running) StartStop();
        }

        void RemoveJob(Job j)
        {
            if (running && !j.Done && j.Error == null && jobs.FirstOrDefault(x => !x.Done && x.Error == null) == j)
            {
                MessageBox.Show("Це відео зараз обробляється. Спершу натисніть «Пауза».", "AutoShorts");
                return;
            }
            if (MessageBox.Show("Прибрати проєкт «" + j.DisplayName + "» зі списку? Файли кліпів на диску залишаться.", "AutoShorts", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            jobs.Remove(j);
            AppSettings.SaveJobs(jobs);
            RefreshJobs();
            RefreshClips();
        }

        void ClearDone()
        {
            jobs.RemoveAll(j => j.Done && j.Clips.All(c => c.Published));
            AppSettings.SaveJobs(jobs);
            RefreshJobs();
            RefreshClips();
        }

        void OpenJobFolder(Job j)
        {
            var dir = j != null && j.OutputDir != null && Directory.Exists(j.OutputDir) ? j.OutputDir : settings.OutputFolder;
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", Tools.Q(dir));
        }

        // ---------- журнал ----------

        Page BuildLogPage()
        {
            logBox = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                BackColor = Theme.Card, ForeColor = Theme.Secondary, Font = Theme.Mono(9.5F)
            };
            DarkScroll(logBox);
            var card = CardOf(Stack(Header("Події", Btn("Відкрити файл журналу", (s, e) => OpenUrl(Path.Combine(AppSettings.LocalDir, "log.txt")), ButtonKind.Plain, Glyphs.Open)), 0, logBox, 100));
            card.Margin = new Padding(0);
            return new Page("Журнал", "Усе, що програма робила: завантаження, рендер, публікації й помилки", card);
        }

        // ---------- обробка черги ----------

        async void StartStop()
        {
            if (running)
            {
                if (cts != null) cts.Cancel();
                pauseBtn.Enabled = false;
                return;
            }
            if (!jobs.Any(j => !j.Done && j.Error == null)) return;
            running = true;
            cts = new CancellationTokenSource();
            RefreshJobs();
            var ct = cts.Token;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var job = jobs.FirstOrDefault(j => !j.Done && j.Error == null);
                    if (job == null) break;
                    try
                    {
                        Log("Починаю: " + job.Url);
                        await pipeline.ProcessAsync(job, ct);
                        AppSettings.SaveJobs(jobs);
                        RefreshClips();
                        Log("Готово: " + job.SourceTitle + " — " + job.Clips.Count + " кліпів");
                        if (job.AutoPublish && job.ChannelId != null)
                        {
                            var ch = settings.FindChannel(job.ChannelId);
                            if (ch != null)
                            {
                                publishing = true;
                                var toPublish = job.Clips.Where(c => !c.Published).OrderByDescending(c => c.Virality).ToList();
                                foreach (var c in toPublish)
                                {
                                    try { await pipeline.PublishAsync(job, c, ch, ct); }
                                    catch (OperationCanceledException) { throw; }
                                    catch (Exception ex)
                                    {
                                        c.Status = "Помилка: " + ex.Message; Log("Публікація: " + ex.Message);
                                        if (ex.Message.Contains("ліміт")) MarkPending(ch, toPublish.SkipWhile(x => x != c).Select(x => Tuple.Create(job, x)));
                                        break;
                                    }
                                }
                                publishing = false;
                                job.Status = "Готово, опубліковано";
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        job.Status = "Пауза";
                        Log("Зупинено.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        job.Error = ex.Message;
                        Log("Помилка: " + ex.Message);
                    }
                    finally { publishing = false; }
                    UpdateJobRow(job);
                    UpdateClipRows(job);
                    AppSettings.SaveJobs(jobs);
                }
            }
            finally
            {
                running = false;
                pauseBtn.Enabled = true;
                AppSettings.SaveJobs(jobs);
                RefreshJobs();
                RefreshClips();
                UpdateNav();
            }
        }
    }
}
