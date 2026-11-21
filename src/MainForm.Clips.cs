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
        ListView clipList;
        CoverView coverBox;
        TextBox titleBox, descBox, tagsBox, clipTextBox;
        NumericUpDown startNum, endNum;
        ComboBox publishChannelCombo;
        Label clipInfo, slotInfo;
        AButton publishBtn;
        bool loadingClip;

        Page BuildClipsTab()
        {
            var lv = List("Відео", 170, "№", 36, "Фрагмент", 100, "Назва", 250, "Статус", 150);
            lv.CheckBoxes = true;
            lv.StatusColumn = 4; lv.MutedColumn = 2;
            lv.EmptyText = "Тут з’являться готові кліпи";
            clipList = lv;
            clipList.SelectedIndexChanged += (s, e) => LoadClip();
            clipList.DoubleClick += (s, e) => PlayClip();

            coverBox = new CoverView { Height = 300 };
            coverBox.Click += (s, e) => PlayClip();
            clipInfo = new Label { AutoSize = true, ForeColor = Theme.Secondary, MaximumSize = new Size(420, 0), Margin = new Padding(4, 8, 3, 3) };
            titleBox = new TextBox();
            descBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical };
            tagsBox = new TextBox();
            clipTextBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, ForeColor = Theme.Secondary };
            startNum = new NumericUpDown { DecimalPlaces = 1, Increment = 0.5M, Maximum = 100000, Width = 85 };
            endNum = new NumericUpDown { DecimalPlaces = 1, Increment = 0.5M, Maximum = 100000, Width = 85 };
            foreach (var tb in new Control[] { titleBox, descBox, tagsBox })
                tb.Leave += (s, e) => SaveClipText(false);

            publishChannelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
            publishChannelCombo.SelectedIndexChanged += (s, e) => UpdateSlotInfo();
            publishBtn = Btn("Опублікувати позначені", (s, e) => PublishChecked(), ButtonKind.Primary, Glyphs.Upload);
            slotInfo = new Label { AutoSize = true, ForeColor = Theme.Green, Margin = new Padding(10, 3, 3, 3) };

            coverBox.Size = new Size(168, 298);
            coverBox.Margin = new Padding(0, 0, 16, 0);
            coverBox.Dock = DockStyle.None;
            var side = Stack(
                clipInfo, 0,
                Row(Btn("Переглянути", (s, e) => PlayClip(), ButtonKind.Primary, Glyphs.Play),
                    Btn("У папці", (s, e) => OpenClipFolder(), ButtonKind.Secondary, Glyphs.Folder)), 0,
                Caption("Назва"), 0, new Field(titleBox, 0, "Назва для YouTube"), 0,
                Caption("Теги (через кому)"), 0, new Field(tagsBox, 0, "шортс, відео, …"), 0);
            side.AutoSize = true;
            var top = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.Controls.Add(coverBox, 0, 0);
            side.Dock = DockStyle.Top;
            top.Controls.Add(side, 1, 0);
            clipInfo.MaximumSize = new Size(260, 0);
            clipInfo.Margin = new Padding(4, 0, 3, 3);

            var editor = Stack(
                top, 0,
                Caption("Опис"), 0, new Field(descBox, 70, null), 0,
                Caption("Межі кліпу, секунди"), 0,
                Row(Lbl("Початок"), startNum, Lbl("Кінець"), endNum,
                    Btn("Перерендерити", (s, e) => RerenderClip(), ButtonKind.Secondary, Glyphs.Refresh)), 0,
                Caption("Текст фрагмента"), 0, new Field(clipTextBox, 40, null), 100);

            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, BackColor = Theme.Bg, SplitterWidth = 14, Margin = new Padding(0, 0, 0, 14) };
            split.Panel1.BackColor = Theme.Bg;
            split.Panel2.BackColor = Theme.Bg;
            var listCard = CardOf(Stack(
                Header("Кліпи",
                    Btn("Позначити всі", (s, e) => CheckAll(true), ButtonKind.Plain, Glyphs.Check),
                    Btn("Зняти всі", (s, e) => CheckAll(false), ButtonKind.Plain)), 0,
                clipList, 100));
            listCard.Margin = new Padding(0);
            split.Panel1.Controls.Add(listCard);
            var editCard = CardOf(editor);
            editCard.Margin = new Padding(0);
            split.Panel2.Controls.Add(editCard);
            split.SizeChanged += (s, e) => { try { split.SplitterDistance = Math.Max(300, split.Width - (int)(470 * Theme.Dpi)); } catch { } };

            var bar = CardOf(Row(Lbl("Канал"), publishChannelCombo, publishBtn, slotInfo));
            bar.Margin = new Padding(0);
            return new Page("Кліпи", "Перегляньте, підправте й позначте кліпи — вони вийдуть у найкращий час каналу",
                Stack(split, 100, bar, 0));
        }

        Tuple<Job, ClipItem> SelectedClip()
        {
            return clipList.SelectedItems.Count == 0 ? null : clipList.SelectedItems[0].Tag as Tuple<Job, ClipItem>;
        }

        static string T(double s)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, s));
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }

        void RefreshClips()
        {
            if (clipList == null) return;
            var sel = SelectedClip();
            clipList.BeginUpdate();
            clipList.Items.Clear();
            foreach (var j in jobs)
                foreach (var c in j.Clips)
                {
                    var it = new ListViewItem(new[] { "", "", "", "", "" }) { Tag = Tuple.Create(j, c) };
                    FillClipRow(it);
                    clipList.Items.Add(it);
                    if (sel != null && sel.Item2 == c) it.Selected = true;
                }
            clipList.EndUpdate();
            UpdateSlotInfo();
            UpdateNav();
        }

        void FillClipRow(ListViewItem it)
        {
            var t = (Tuple<Job, ClipItem>)it.Tag;
            var c = t.Item2;
            it.SubItems[0].Text = t.Item1.DisplayName;
            it.SubItems[1].Text = c.Index.ToString();
            it.SubItems[2].Text = T(c.Start) + "–" + T(c.End);
            it.SubItems[3].Text = c.Title ?? "";
            it.SubItems[4].Text = c.Status ?? "";
            it.ForeColor = c.Published ? Theme.Green : (c.Status ?? "").StartsWith("Помилка") ? Theme.Red : Theme.Accent;
        }

        void UpdateClipRows(Job j)
        {
            if (clipList == null) return;
            int existing = clipList.Items.Cast<ListViewItem>().Count(it => ((Tuple<Job, ClipItem>)it.Tag).Item1 == j);
            if (existing != j.Clips.Count) { if (j.Done || j.Clips.All(c => c.Status != null)) RefreshClips(); return; }
            foreach (ListViewItem it in clipList.Items)
                if (((Tuple<Job, ClipItem>)it.Tag).Item1 == j) FillClipRow(it);
        }

        void CheckAll(bool v)
        {
            foreach (ListViewItem it in clipList.Items) it.Checked = v && !((Tuple<Job, ClipItem>)it.Tag).Item2.Published;
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
                if (t == null)
                {
                    titleBox.Text = descBox.Text = tagsBox.Text = clipTextBox.Text = clipInfo.Text = "";
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
                clipTextBox.Text = c.Text ?? "";
                var max = (decimal)Math.Max(t.Item1.SourceDuration, c.End + 1);
                startNum.Maximum = max; endNum.Maximum = max;
                startNum.Value = (decimal)Math.Round(c.Start, 1);
                endNum.Value = (decimal)Math.Round(c.End, 1);
                clipInfo.Text = "Тривалість " + c.Duration.ToString("0.0") + " с · оцінка " + c.Score.ToString("0.00") +
                                (c.FaceX >= 0 ? " · кадр за обличчям" : " · кадр по центру") +
                                (c.Published ? " · YouTube: " + c.VideoId : "");
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
            foreach (ListViewItem it in clipList.Items) if (it.Tag == t) FillClipRow(it);
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
            if (running) { MessageBox.Show("Зачекайте, поки закінчиться обробка черги.", "AutoShorts"); return; }
            var c = t.Item2;
            if (c.Published) { MessageBox.Show("Цей кліп уже опубліковано.", "AutoShorts"); return; }
            double s = (double)startNum.Value, e = (double)endNum.Value;
            if (e - s < 3) { MessageBox.Show("Кліп має бути довшим за 3 секунди.", "AutoShorts"); return; }
            if (e - s > 180) { MessageBox.Show("YouTube Shorts — до 3 хвилин.", "AutoShorts"); return; }
            SaveClipText(true);
            bool timeChanged = Math.Abs(c.Start - s) > 0.05 || Math.Abs(c.End - e) > 0.05;
            c.Start = s; c.End = e;
            running = true;
            startBtn.Enabled = false;
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
                startBtn.Enabled = true;
                AppSettings.SaveJobs(jobs);
                RefreshClips();
                LoadClip();
            }
        }

        void UpdateSlotInfo()
        {
            if (slotInfo == null) return;
            var id = SelectedId(publishChannelCombo);
            var ch = id == null ? null : settings.FindChannel(id);
            if (ch == null) { slotInfo.Text = settings.Channels.Count == 0 ? "Додайте канал у вкладці «Канали»" : ""; return; }
            var next = BestTime.NextSlotUtc(ch, settings.Published.Where(p => p.ChannelId == ch.Id).Select(p => p.PublishAtUtc), DateTime.UtcNow);
            slotInfo.Text = "Наступний слот: " + BestTime.Format(next) + " (Київ)";
        }

        async void PublishChecked()
        {
            SaveClipText(true);
            var id = SelectedId(publishChannelCombo);
            var ch = id == null ? null : settings.FindChannel(id);
            if (ch == null) { MessageBox.Show("Оберіть канал для публікації.", "AutoShorts"); return; }
            var items = clipList.CheckedItems.Cast<ListViewItem>().Select(it => (Tuple<Job, ClipItem>)it.Tag)
                .Where(t => !t.Item2.Published && t.Item2.VideoPath != null && File.Exists(t.Item2.VideoPath)).ToList();
            if (items.Count == 0) { MessageBox.Show("Позначте галочкою готові кліпи, які треба опублікувати.", "AutoShorts"); return; }
            if (MessageBox.Show("Завантажити " + items.Count + " кліп(ів) на «" + ch.Title + "»?\r\nКожен буде заплановано на найкращий вільний час.",
                    "AutoShorts", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            publishing = true;
            publishBtn.Enabled = false;
            UpdateNav();
            try
            {
                foreach (var t in items)
                {
                    try
                    {
                        await pipeline.PublishAsync(t.Item1, t.Item2, ch, CancellationToken.None);
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
                publishBtn.Enabled = true;
                RefreshClips();
                UpdateNav();
            }
        }
    }
}
