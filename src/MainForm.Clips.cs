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
    public partial class MainForm
    {
        TileView clipGrid;
        CoverView coverBox;
        ScorePanel scorePanel;
        TextBox titleBox, descBox, tagsBox;
        Stepper startNum, endNum;
        Dropdown publishChannelCombo, projectFilter, sortDd, clipStyleDd;
        Label clipInfo, slotInfo, clipsHeadTitle;
        AButton publishBtn, publishOneBtn;
        Card editCard;
        bool loadingClip;
        readonly HashSet<ClipItem> checkedClips = new HashSet<ClipItem>();

        Page BuildClipsTab()
        {
            projectFilter = Dd(null);
            projectFilter.SelectedIndexChanged += (s, e) => { if (!loadingClip) RefreshClips(); };
            sortDd = Dd(null, "Спершу найкращі", "За часом у відео", "Спершу нові");
            sortDd.Glyph = Glyphs.Sort;
            sortDd.SelectedIndexChanged += (s, e) => RefreshClips();

            clipGrid = new TileView
            {
                TileSize = new Size((int)(176 * Theme.Dpi), (int)(400 * Theme.Dpi)),
                HeightFor = w => w * 16 / 9 + Font.Height * 4,
                Gap = (int)(16 * Theme.Dpi),
                Painter = PaintClip,
                EmptyText = "Тут з’являться готові кліпи",
                EmptyHint = "Створіть проєкт на головній — найкращі моменти будуть зверху"
            };
            clipGrid.SelectionChanged += (s, e) => LoadClip();
            clipGrid.TileClicked += (o, p, b) =>
            {
                var c = ((Tuple<Job, ClipItem>)o).Item2;
                int cs = (int)(Font.Height * 1.7f);
                var check = new Rectangle(clipGrid.TileW - cs - 8, 8, cs, cs);
                if (b == MouseButtons.Left && check.Contains(p) && !c.Published)
                {
                    if (!checkedClips.Remove(c)) checkedClips.Add(c);
                    clipGrid.Invalidate();
                    UpdatePublishBtn();
                }
                if (b == MouseButtons.Right) ClipMenu((Tuple<Job, ClipItem>)o);
            };
            clipGrid.TileDoubleClicked += o => PlayClip();

            // редактор
            coverBox = new CoverView { Size = new Size(150, 266), Margin = new Padding(0, 0, 16, 0) };
            coverBox.Click += (s, e) => PlayClip();
            scorePanel = new ScorePanel { Dock = DockStyle.Fill };
            var top = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.Controls.Add(coverBox, 0, 0);
            clipInfo = new Label { AutoSize = true, ForeColor = Theme.Secondary, Font = Theme.F(9F), MaximumSize = new Size(220, 0), Margin = new Padding(3, 6, 3, 3) };
            var scoreCol = Stack(scorePanel, 0, clipInfo, 0);
            scoreCol.AutoSize = true;
            scoreCol.Dock = DockStyle.Top;
            top.Controls.Add(scoreCol, 1, 0);

            titleBox = new TextBox();
            descBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            DarkScroll(descBox);
            tagsBox = new TextBox();
            foreach (var tb in new Control[] { titleBox, descBox, tagsBox })
                tb.Leave += (s, e) => SaveClipText(false);
            startNum = new Stepper(112) { DecimalPlaces = 1, Increment = 0.5M, Maximum = 100000 };
            endNum = new Stepper(112) { DecimalPlaces = 1, Increment = 0.5M, Maximum = 100000 };
            clipStyleDd = Dd("Субтитри", SubtitleStyles.All.Select(st => (object)st.Name).ToArray());
            clipStyleDd.ItemImage = o => StylePreview.Get(SubtitleStyles.Get(o as string), (int)(120 * Theme.Dpi), (int)(44 * Theme.Dpi));
            publishOneBtn = Btn("Опублікувати", (s, e) => PublishOne(), ButtonKind.Accent, Glyphs.Upload);

            var editor = Stack(
                top, 0,
                Row(Btn("Дивитися", (s, e) => PlayClip(), ButtonKind.Primary, Glyphs.Play),
                    Btn("", (s, e) => OpenClipFolder(), ButtonKind.Secondary, Glyphs.Folder), publishOneBtn), 0,
                Caption("Назва"), 0, new Field(titleBox, 0, "Назва для YouTube"), 0,
                Caption("Опис"), 0, new Field(descBox, 58, null), 0,
                Caption("Теги (через кому)"), 0, new Field(tagsBox, 0, "шортс, відео, …"), 0,
                Caption("Межі й стиль — зміни застосуються після «Перерендерити»"), 0,
                Row(Lbl("від"), startNum, Lbl("до"), endNum), 0,
                Row(clipStyleDd, Btn("Перерендерити", (s, e) => RerenderClip(), ButtonKind.Secondary, Glyphs.Refresh)), 0,
                new Panel { Height = 1 }, 100);
            editCard = CardOf(editor);
            editCard.Margin = new Padding(0);

            // верхня панель
            publishChannelCombo = Dd("Канал");
            publishChannelCombo.SelectedIndexChanged += (s, e) => UpdateSlotInfo();
            publishBtn = Btn("Опублікувати позначені", (s, e) => PublishChecked(), ButtonKind.Primary, Glyphs.Upload);
            slotInfo = new Label { AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(8, 3, 3, 3) };
            clipsHeadTitle = Section2("Кліпи");
            var bar = Header2(Row(clipsHeadTitle, projectFilter, sortDd,
                    Btn("Позначити всі", (s, e) => CheckAll(true), ButtonKind.Plain, Glyphs.Check),
                    Btn("Зняти", (s, e) => CheckAll(false), ButtonKind.Plain)),
                slotInfo, publishChannelCombo, publishBtn);

            var grid = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430 * Theme.Dpi));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            clipGrid.Dock = DockStyle.Fill;
            clipGrid.Margin = new Padding(0, 0, 18, 0);
            grid.Controls.Add(clipGrid, 0, 0);
            grid.Controls.Add(editCard, 1, 0);

            return new Page(null, null, Stack(bar, 0, grid, 100));
        }

        Tuple<Job, ClipItem> SelectedClip()
        {
            return clipGrid == null ? null : clipGrid.Selected as Tuple<Job, ClipItem>;
        }

        static string T(double s)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, s));
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }

        static int VirOf(ClipItem c)
        {
            return c.Virality > 0 ? c.Virality : Math.Max(1, Math.Min(99, (int)Math.Round(60 + 39 * Math.Min(1, c.Score / 0.7))));
        }

        void SetClipFilter(Job j)
        {
            loadingClip = true;
            projectFilter.SelectedIndex = j == null ? 0 : Math.Max(0, projectFilter.Items.FindIndex(x => x is ComboItem && ((ComboItem)x).Value == j));
            loadingClip = false;
            RefreshClips();
        }

        void RefreshClips()
        {
            if (clipGrid == null) return;
            // фільтр проєктів
            var curJob = projectFilter.SelectedItem is ComboItem ? ((ComboItem)projectFilter.SelectedItem).Value as Job : null;
            loadingClip = true;
            projectFilter.Items.Clear();
            projectFilter.Items.Add(new ComboItem("Усі проєкти", null));
            foreach (var j in Enumerable.Reverse(jobs).Where(j => j.Clips.Count > 0))
            {
                var name = j.DisplayName;
                if (name.Length > 38) name = name.Substring(0, 36) + "…";
                projectFilter.Items.Add(new ComboItem(name, j));
            }
            int fi = projectFilter.Items.FindIndex(x => ((ComboItem)x).Value == curJob && curJob != null);
            projectFilter.SelectedIndex = fi < 0 ? 0 : fi;
            projectFilter.Relayout();
            loadingClip = false;
            curJob = fi < 0 ? null : curJob;

            var sel = SelectedClip();
            var list = new List<Tuple<Job, ClipItem>>();
            foreach (var j in jobs)
            {
                if (curJob != null && j != curJob) continue;
                foreach (var c in j.Clips) list.Add(Tuple.Create(j, c));
            }
            if (sortDd.SelectedIndex == 0) list = list.OrderByDescending(t => VirOf(t.Item2)).ToList();
            else if (sortDd.SelectedIndex == 1) list = list.OrderBy(t => jobs.IndexOf(t.Item1)).ThenBy(t => t.Item2.Start).ToList();
            else list = list.OrderByDescending(t => jobs.IndexOf(t.Item1)).ThenBy(t => t.Item2.Index).ToList();
            checkedClips.RemoveWhere(c => !list.Any(t => t.Item2 == c) || c.Published);
            clipGrid.Items.Clear();
            object keep = null;
            foreach (var t in list)
            {
                clipGrid.Items.Add(t);
                if (sel != null && sel.Item2 == t.Item2) keep = t;
            }
            clipGrid.Selected = keep ?? (list.Count > 0 ? list[0] : null);
            clipGrid.RefreshItems();
            clipsHeadTitle.Text = "Кліпи" + (list.Count > 0 ? " · " + list.Count : "");
            UpdateSlotInfo();
            UpdatePublishBtn();
            UpdateNav();
            LoadClip();
        }

        void UpdatePublishBtn()
        {
            if (publishBtn == null) return;
            publishBtn.Text = checkedClips.Count > 0 ? "Опублікувати (" + checkedClips.Count + ")" : "Опублікувати позначені";
            publishBtn.Enabled = checkedClips.Count > 0 && !publishing;
        }

        void UpdateClipRows(Job j)
        {
            if (clipGrid == null) return;
            int existing = clipGrid.Items.Count(o => ((Tuple<Job, ClipItem>)o).Item1 == j);
            if (existing != j.Clips.Count) { if (j.Done || j.Clips.All(c => c.Status != null)) RefreshClips(); return; }
            clipGrid.Invalidate();
        }

        void CheckAll(bool v)
        {
            checkedClips.Clear();
            if (v) foreach (var o in clipGrid.Items) { var c = ((Tuple<Job, ClipItem>)o).Item2; if (!c.Published && c.VideoPath != null) checkedClips.Add(c); }
            clipGrid.Invalidate();
            UpdatePublishBtn();
        }

        void PaintClip(Graphics g, object o, Rectangle r, bool hover, bool sel)
        {
            var t = (Tuple<Job, ClipItem>)o;
            var c = t.Item2;
            var f = Font;
            int ch = r.Width * 16 / 9;
            var cr = new Rectangle(r.X, r.Y, r.Width, ch);
            float rad = f.Height * 0.9f;
            DrawCover(g, Thumbs.Get(c.CoverPath, cr.Width, cr.Height), cr, rad, Theme.Bg, Glyphs.Video);
            Theme.Smooth(g);
            if (hover) using (var p = Theme.Round(cr, rad)) using (var b = new SolidBrush(Color.FromArgb(24, 255, 255, 255))) g.FillPath(b, p);
            if (sel) Theme.StrokeRound(g, Theme.Primary, 2.4f, new RectangleF(cr.X + 1, cr.Y + 1, cr.Width - 2, cr.Height - 2), rad - 1);

            var pillBg = Color.FromArgb(200, 12, 12, 14);
            int pad = 8;
            // оцінка віральності
            int v = VirOf(c);
            var big = Theme.Display(f.SizeInPoints * 1.25f);
            var vs = v.ToString();
            int vw = TextRenderer.MeasureText(g, vs, big, Size.Empty, TextFormatFlags.NoPadding).Width;
            var vr = new Rectangle(cr.X + pad, cr.Y + pad, vw + f.Height * 2 + 4, big.Height + 6);
            Theme.FillRound(g, pillBg, vr, vr.Height / 2f);
            TextRenderer.DrawText(g, vs, big, new Rectangle(vr.X + f.Height / 2, vr.Y, vw + 2, vr.Height), ScorePanel.ScoreColor(v), Theme.LeftMid);
            TextRenderer.DrawText(g, "/100", Theme.F(f.SizeInPoints * 0.75f), new Rectangle(vr.X + f.Height / 2 + vw + 1, vr.Y + 2, f.Height * 2, vr.Height), Theme.Secondary, Theme.LeftMid);

            // кружечок вибору
            int cs = (int)(f.Height * 1.7f);
            var check = new RectangleF(cr.Right - cs - pad, cr.Y + pad, cs, cs);
            if (!c.Published)
            {
                bool on = checkedClips.Contains(c);
                using (var b = new SolidBrush(on ? Theme.Accent : Color.FromArgb(120, 0, 0, 0))) g.FillEllipse(b, check);
                using (var p = new Pen(on ? Theme.Accent : Color.FromArgb(220, 255, 255, 255), 1.6f)) g.DrawEllipse(p, check);
                if (on)
                    using (var p = new Pen(Color.FromArgb(12, 12, 14), 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                        g.DrawLines(p, new[] { new PointF(check.X + cs * 0.28f, check.Y + cs * 0.52f), new PointF(check.X + cs * 0.44f, check.Y + cs * 0.68f), new PointF(check.X + cs * 0.74f, check.Y + cs * 0.34f) });
            }
            var small = Theme.Semi(f.SizeInPoints * 0.82f);
            Pill(g, T(c.Duration), small, cr.Right - pad, cr.Bottom - pad - small.Height - 6, pillBg, Theme.Text, true);
            if (c.Published) Pill(g, "✓ На YouTube", small, cr.X + pad, cr.Bottom - pad - small.Height - 6, Color.FromArgb(220, 22, 101, 52), Color.White);
            else if ((c.Status ?? "").StartsWith("Помилка")) Pill(g, "Помилка", small, cr.X + pad, cr.Bottom - pad - small.Height - 6, Color.FromArgb(220, 127, 29, 29), Color.White);
            else if (c.Status != null && c.Status != "Готовий") Pill(g, c.Status, small, cr.X + pad, cr.Bottom - pad - small.Height - 6, pillBg, Theme.Accent);

            var titleR = new Rectangle(r.X + 2, cr.Bottom + f.Height / 2, r.Width - 4, f.Height * 2 + 4);
            TextRenderer.DrawText(g, c.Title ?? ("Кліп " + c.Index), Theme.Semi(f.SizeInPoints * 0.95f), titleR, Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, T(c.Start) + "–" + T(c.End) + " · " + t.Item1.DisplayName, Theme.F(f.SizeInPoints * 0.85f),
                new Rectangle(r.X + 2, titleR.Bottom + 2, r.Width - 4, f.Height + 2), Theme.Tertiary, Theme.LeftMid);
        }

        void ClipMenu(Tuple<Job, ClipItem> t)
        {
            var m = DarkRenderer.Menu(Font);
            m.Items.Add("Дивитися", null, (s, e) => PlayClip());
            m.Items.Add("Показати у папці", null, (s, e) => OpenClipFolder());
            if (t.Item2.Published) m.Items.Add("Відкрити на YouTube", null, (s, e) => OpenUrl("https://youtube.com/shorts/" + t.Item2.VideoId));
            else m.Items.Add("Опублікувати", null, (s, e) => PublishOne());
            m.Closed += (s, e) => BeginInvoke(new Action(() => m.Dispose()));
            m.Show(Cursor.Position);
        }

        void LoadClip()
        {
            var t = SelectedClip();
            loadingClip = true;
            try
            {
                var old = coverBox.Image;
                coverBox.Image = null;
                if (old != null) old.Dispose();
                editCard.Enabled = t != null;
                if (t == null)
                {
                    titleBox.Text = descBox.Text = tagsBox.Text = clipInfo.Text = "";
                    scorePanel.Set(0);
                    return;
                }
                var c = t.Item2;
                if (c.CoverPath != null && File.Exists(c.CoverPath))
                {
                    try { coverBox.Image = Image.FromStream(new MemoryStream(File.ReadAllBytes(c.CoverPath))); } catch { }
                }
                titleBox.Text = c.Title ?? "";
                descBox.Text = c.Description ?? "";
                tagsBox.Text = c.Tags ?? "";
                var max = (decimal)Math.Max(t.Item1.SourceDuration, c.End + 1);
                startNum.Maximum = max; endNum.Maximum = max;
                startNum.Value = (decimal)Math.Round(c.Start, 1);
                endNum.Value = (decimal)Math.Round(c.End, 1);
                clipStyleDd.SelectedItem = SubtitleStyles.Get(c.StyleName ?? t.Item1.StyleName ?? settings.SubtitleStyle).Name;
                scorePanel.Set(VirOf(c),
                    new KeyValuePair<string, int>("Гачок", c.ScoreHook),
                    new KeyValuePair<string, int>("Перегляди", c.ScoreViews),
                    new KeyValuePair<string, int>("Енергія", c.ScoreEnergy),
                    new KeyValuePair<string, int>("Темп мови", c.ScoreTempo));
                clipInfo.Text = T(c.Start) + "–" + T(c.End) + " · " + c.Duration.ToString("0") + " с" +
                                (c.FaceX >= 0 ? " · кадр за обличчям" : "") +
                                (c.Published ? "\nYouTube: " + BestTime.Format(c.PublishAtUtc) : "");
                publishOneBtn.Visible = !c.Published;
            }
            finally { loadingClip = false; }
        }

        void SaveClipText(bool silent)
        {
            if (loadingClip) return;
            var t = SelectedClip();
            if (t == null) return;
            var c = t.Item2;
            if (c.Title == titleBox.Text && c.Description == descBox.Text && c.Tags == tagsBox.Text) return;
            c.Title = titleBox.Text.Trim();
            c.Description = descBox.Text;
            c.Tags = Metadata.CleanTags(tagsBox.Text);
            AppSettings.SaveJobs(jobs);
            clipGrid.Invalidate();
        }

        void PlayClip()
        {
            var t = SelectedClip();
            if (t == null || t.Item2.VideoPath == null || !File.Exists(t.Item2.VideoPath)) return;
            OpenUrl(t.Item2.VideoPath);
        }

        void OpenClipFolder()
        {
            var t = SelectedClip();
            if (t == null || t.Item2.VideoPath == null) return;
            if (File.Exists(t.Item2.VideoPath)) Process.Start("explorer.exe", "/select," + Tools.Q(t.Item2.VideoPath));
        }

        async void RerenderClip()
        {
            var t = SelectedClip();
            if (t == null) return;
            if (running) { MessageBox.Show("Зачекайте, поки закінчиться обробка черги (або натисніть «Пауза» на головній).", "AutoShorts"); return; }
            var c = t.Item2;
            if (c.Published) { MessageBox.Show("Цей кліп уже опубліковано.", "AutoShorts"); return; }
            double s = (double)startNum.Value, e = (double)endNum.Value;
            if (e - s < 3) { MessageBox.Show("Кліп має бути довшим за 3 секунди.", "AutoShorts"); return; }
            if (e - s > 180) { MessageBox.Show("YouTube Shorts — до 3 хвилин.", "AutoShorts"); return; }
            SaveClipText(true);
            bool timeChanged = Math.Abs(c.Start - s) > 0.05 || Math.Abs(c.End - e) > 0.05;
            c.Start = s; c.End = e;
            c.StyleName = clipStyleDd.SelectedItem as string;
            running = true;
            UpdateNav();
            try
            {
                if (timeChanged) { c.Title = null; c.Description = null; c.Tags = null; }
                await pipeline.RerenderAsync(t.Item1, c, CancellationToken.None);
                Log("Перерендерено кліп " + c.Index + " (" + T(s) + "–" + T(e) + ")");
            }
            catch (Exception ex)
            {
                c.Status = "Помилка: " + ex.Message;
                Log("Помилка рендеру: " + ex.Message);
            }
            finally
            {
                running = false;
                AppSettings.SaveJobs(jobs);
                RefreshClips();
                UpdateNav();
            }
        }

        void UpdateSlotInfo()
        {
            if (slotInfo == null) return;
            var id = SelectedId(publishChannelCombo);
            var ch = id == null ? null : settings.FindChannel(id);
            if (ch == null) { slotInfo.Text = settings.Channels.Count == 0 ? "Підключіть канал у «Каналах»" : ""; return; }
            var next = BestTime.NextSlotUtc(ch, settings.Published.Where(p => p.ChannelId == ch.Id).Select(p => p.PublishAtUtc), DateTime.UtcNow);
            slotInfo.Text = "наступний слот " + BestTime.Format(next);
        }

        void PublishOne()
        {
            var t = SelectedClip();
            if (t == null || t.Item2.Published) return;
            checkedClips.Clear();
            checkedClips.Add(t.Item2);
            PublishChecked();
        }

        async void PublishChecked()
        {
            SaveClipText(true);
            var id = SelectedId(publishChannelCombo);
            var ch = id == null ? null : settings.FindChannel(id);
            if (ch == null) { MessageBox.Show("Оберіть канал для публікації (зверху праворуч).", "AutoShorts"); return; }
            var items = clipGrid.Items.Cast<Tuple<Job, ClipItem>>().Where(t => checkedClips.Contains(t.Item2))
                .Where(t => !t.Item2.Published && t.Item2.VideoPath != null && File.Exists(t.Item2.VideoPath))
                .OrderByDescending(t => VirOf(t.Item2)).ToList();
            if (items.Count == 0) { MessageBox.Show("Позначте кружечком готові кліпи, які треба опублікувати.", "AutoShorts"); return; }
            if (MessageBox.Show("Завантажити " + items.Count + " кліп(ів) на «" + ch.Title + "»?\r\nКожен буде заплановано на найкращий вільний час — кліпи з вищою оцінкою підуть першими.",
                    "AutoShorts", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            publishing = true;
            UpdatePublishBtn();
            UpdateNav();
            try
            {
                foreach (var t in items)
                {
                    try
                    {
                        await pipeline.PublishAsync(t.Item1, t.Item2, ch, CancellationToken.None);
                        checkedClips.Remove(t.Item2);
                    }
                    catch (Exception ex)
                    {
                        t.Item2.Status = "Помилка: " + ex.Message;
                        Log("Помилка публікації: " + ex.Message);
                        UpdateClipRows(t.Item1);
                        if (ex.Message.Contains("ліміт")) break;
                    }
                    AppSettings.SaveJobs(jobs);
                }
            }
            finally
            {
                publishing = false;
                RefreshClips();
                RefreshCalendar();
                UpdateNav();
            }
        }
    }
}
