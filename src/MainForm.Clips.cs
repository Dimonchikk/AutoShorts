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
        PictureBox coverBox;
        TextBox titleBox, descBox, tagsBox, clipTextBox;
        NumericUpDown startNum, endNum;
        ComboBox publishChannelCombo;
        Label clipInfo, slotInfo;
        Button publishBtn;
        bool loadingClip;

        TabPage BuildClipsTab()
        {
            var page = new TabPage("2. Кліпи (перегляд)");
            clipList = List("Відео", 230, "№", 36, "Фрагмент", 110, "Назва", 330, "Статус", 190);
            clipList.CheckBoxes = true;
            clipList.SelectedIndexChanged += (s, e) => LoadClip();
            clipList.DoubleClick += (s, e) => PlayClip();

            coverBox = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black, Height = 300 };
            coverBox.Click += (s, e) => PlayClip();
            coverBox.Cursor = Cursors.Hand;
            clipInfo = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 6, 3, 3) };
            titleBox = new TextBox();
            descBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 90 };
            tagsBox = new TextBox();
            clipTextBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 60, ForeColor = Color.DimGray };
            startNum = new NumericUpDown { DecimalPlaces = 1, Increment = 0.5M, Maximum = 100000, Width = 85 };
            endNum = new NumericUpDown { DecimalPlaces = 1, Increment = 0.5M, Maximum = 100000, Width = 85 };
            foreach (var tb in new Control[] { titleBox, descBox, tagsBox })
                tb.Leave += (s, e) => SaveClipText(false);

            publishChannelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            publishChannelCombo.SelectedIndexChanged += (s, e) => UpdateSlotInfo();
            publishBtn = Btn("🚀 Опублікувати позначені ✓", (s, e) => PublishChecked());
            publishBtn.Font = new Font(Font, FontStyle.Bold);
            slotInfo = new Label { AutoSize = true, ForeColor = Color.DarkGreen, Margin = new Padding(3, 6, 3, 3) };

            var editor = Stack(
                coverBox, 0,
                clipInfo, 0,
                Row(Btn("▶ Переглянути", (s, e) => PlayClip()), Btn("📂 Папка", (s, e) => OpenClipFolder())), 0,
                Lbl("Назва:"), 0, titleBox, 0,
                Lbl("Опис:"), 0, descBox, 0,
                Lbl("Теги (через кому):"), 0, tagsBox, 0,
                Row(Lbl("Початок, с:"), startNum, Lbl("Кінець, с:"), endNum, Btn("↻ Перерендерити", (s, e) => RerenderClip())), 0,
                Lbl("Текст фрагмента:"), 0, clipTextBox, 100);

            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
            split.Panel1.Controls.Add(clipList);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(editor);
            split.Panel2.Controls.Add(scroll);
            split.SizeChanged += (s, e) => { try { split.SplitterDistance = Math.Max(300, split.Width - 440); } catch { } };

            page.Controls.Add(Stack(
                split, 100,
                Row(Lbl("Канал:"), publishChannelCombo, publishBtn, Btn("Позначити всі", (s, e) => CheckAll(true)), Btn("Зняти всі", (s, e) => CheckAll(false)), slotInfo), 0));
            return page;
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
            it.ForeColor = c.Published ? Color.DarkGreen : (c.Status ?? "").StartsWith("Помилка") ? Color.Firebrick : SystemColors.WindowText;
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
            slotInfo.Text = "Наступний слот (Київ): " + BestTime.Format(next);
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
            }
        }
    }
}
