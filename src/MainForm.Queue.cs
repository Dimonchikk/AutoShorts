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
        TextBox urlBox, logBox;
        ComboBox channelCombo, styleCombo;
        NumericUpDown clipsNum;
        CheckBox autoPublishCheck;
        ListView jobList;
        Button startBtn;

        TabPage BuildQueueTab()
        {
            var page = new TabPage("1. Черга відео");
            urlBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 64, Font = new Font("Segoe UI", 10.5F) };
            channelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            styleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            foreach (var st in SubtitleStyles.All) styleCombo.Items.Add(st.Name);
            styleCombo.SelectedItem = settings.SubtitleStyle;
            clipsNum = new NumericUpDown { Minimum = 1, Maximum = 20, Value = Math.Min(20, settings.ClipsPerVideo), Width = 55 };
            autoPublishCheck = new CheckBox { Text = "Публікувати одразу, без перегляду", AutoSize = true, Checked = !settings.RequirePreview, Margin = new Padding(10, 7, 3, 3) };

            jobList = List("Відео", 420, "Статус", 330, "Прогрес", 80, "Кліпів", 60, "Канал", 150);
            jobList.DoubleClick += (s, e) => { var j = SelectedJob(); if (j != null) OpenUrl(j.Url); };

            startBtn = Btn("▶ Старт", (s, e) => StartStop());
            startBtn.Font = new Font(Font, FontStyle.Bold);
            logBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.Gainsboro, Font = new Font("Consolas", 9F) };

            page.Controls.Add(Stack(
                Lbl("Вставте посилання на YouTube-відео (по одному на рядок) — програма сама знайде найцікавіші моменти:"), 0,
                urlBox, 0,
                Row(Btn("＋ Додати в чергу", (s, e) => AddLinks()), Lbl("Канал:"), channelCombo, Lbl("Кліпів:"), clipsNum, Lbl("Стиль субтитрів:"), styleCombo, autoPublishCheck), 0,
                jobList, 60,
                Row(startBtn, Btn("↻ Повторити", (s, e) => RetryJob()), Btn("✕ Видалити", (s, e) => RemoveJob()),
                    Btn("Очистити готові", (s, e) => ClearDone()), Btn("📂 Папка з кліпами", (s, e) => OpenJobFolder())), 0,
                logBox, 40));
            return page;
        }

        Job SelectedJob()
        {
            return jobList.SelectedItems.Count == 0 ? null : jobList.SelectedItems[0].Tag as Job;
        }

        void AddLinks()
        {
            var lines = urlBox.Text.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => l.StartsWith("http", StringComparison.OrdinalIgnoreCase) || l.Contains("youtu")).ToList();
            if (lines.Count == 0)
            {
                MessageBox.Show("Вставте хоча б одне посилання на відео.", "AutoShorts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            settings.SubtitleStyle = styleCombo.SelectedItem as string ?? settings.SubtitleStyle;
            settings.RequirePreview = !autoPublishCheck.Checked;
            settings.ClipsPerVideo = (int)clipsNum.Value;
            settings.DefaultChannelId = SelectedId(channelCombo);
            settings.Save();
            foreach (var l in lines)
            {
                var url = l.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? l : "https://" + l;
                var job = new Job
                {
                    Url = url,
                    ChannelId = SelectedId(channelCombo),
                    StyleName = settings.SubtitleStyle,
                    ClipCount = (int)clipsNum.Value,
                    AutoPublish = autoPublishCheck.Checked && SelectedId(channelCombo) != null
                };
                jobs.Add(job);
            }
            urlBox.Clear();
            AppSettings.SaveJobs(jobs);
            RefreshJobs();
            Log("Додано в чергу: " + lines.Count);
            if (!running) StartStop();
        }

        void RefreshJobs()
        {
            jobList.BeginUpdate();
            jobList.Items.Clear();
            foreach (var j in jobs)
            {
                var it = new ListViewItem(new[] { "", "", "", "", "" }) { Tag = j };
                jobList.Items.Add(it);
                FillJobRow(it, j);
            }
            jobList.EndUpdate();
        }

        void FillJobRow(ListViewItem it, Job j)
        {
            var ch = j.ChannelId == null ? null : settings.FindChannel(j.ChannelId);
            it.SubItems[0].Text = j.DisplayName;
            it.SubItems[1].Text = j.Error != null ? "Помилка: " + j.Error : j.Status;
            it.SubItems[2].Text = j.Progress + "%";
            it.SubItems[3].Text = j.Clips.Count > 0 ? j.Clips.Count.ToString() : "";
            it.SubItems[4].Text = ch == null ? "—" : ch.Title + (j.AutoPublish ? " (авто)" : "");
            it.ForeColor = j.Error != null ? Color.Firebrick : j.Done ? Color.DarkGreen : SystemColors.WindowText;
        }

        void UpdateJobRow(Job j)
        {
            foreach (ListViewItem it in jobList.Items)
                if (it.Tag == j) { FillJobRow(it, j); return; }
        }

        void RetryJob()
        {
            var j = SelectedJob();
            if (j == null) return;
            j.Error = null; j.Done = false; j.Status = "В черзі"; j.Progress = 0;
            RefreshJobs();
            if (!running) StartStop();
        }

        void RemoveJob()
        {
            var j = SelectedJob();
            if (j == null) return;
            if (running && !j.Done && j.Error == null && jobs.FirstOrDefault(x => !x.Done && x.Error == null) == j)
            {
                MessageBox.Show("Це відео зараз обробляється. Спершу натисніть «Стоп».", "AutoShorts");
                return;
            }
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

        void OpenJobFolder()
        {
            var j = SelectedJob();
            var dir = j != null && j.OutputDir != null && Directory.Exists(j.OutputDir) ? j.OutputDir : settings.OutputFolder;
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", Tools.Q(dir));
        }

        async void StartStop()
        {
            if (running)
            {
                if (cts != null) cts.Cancel();
                startBtn.Enabled = false;
                return;
            }
            running = true;
            cts = new CancellationTokenSource();
            startBtn.Text = "■ Стоп";
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
                                foreach (var c in job.Clips.Where(c => !c.Published).ToList())
                                {
                                    try { await pipeline.PublishAsync(job, c, ch, ct); }
                                    catch (OperationCanceledException) { throw; }
                                    catch (Exception ex) { c.Status = "Помилка: " + ex.Message; Log("Публікація: " + ex.Message); break; }
                                }
                                job.Status = "Готово, опубліковано";
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        job.Status = "Зупинено";
                        Log("Зупинено.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        job.Error = ex.Message;
                        Log("Помилка: " + ex.Message);
                    }
                    UpdateJobRow(job);
                    UpdateClipRows(job);
                    AppSettings.SaveJobs(jobs);
                }
            }
            finally
            {
                running = false;
                startBtn.Text = "▶ Старт";
                startBtn.Enabled = true;
                AppSettings.SaveJobs(jobs);
                RefreshJobs();
            }
        }
    }
}
