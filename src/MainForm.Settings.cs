using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoShorts
{
    public partial class MainForm
    {
        TextBox clientIdBox, clientSecretBox, outputBox, titleTplBox, descTplBox, tagsTplBox;
        NumericUpDown minNum, maxNum, defClipsNum;
        ComboBox defStyleCombo;
        CheckBox faceCheck, hookCheck, coverCheck;

        TabPage BuildSettingsTab()
        {
            var page = new TabPage("5. Налаштування");
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Action<string, Control> add = (label, c) =>
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(3, 4, 3, 4);
                t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                t.Controls.Add(Lbl(label), 0, t.RowStyles.Count - 1);
                t.Controls.Add(c, 1, t.RowStyles.Count - 1);
            };

            clientIdBox = new TextBox { Text = settings.ClientId ?? "" };
            clientSecretBox = new TextBox { Text = settings.ClientSecret ?? "", UseSystemPasswordChar = true };
            outputBox = new TextBox { Text = settings.OutputFolder };
            titleTplBox = new TextBox { Text = settings.TitleTemplate };
            descTplBox = new TextBox { Text = settings.DescriptionTemplate, Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical };
            tagsTplBox = new TextBox { Text = settings.TagsTemplate };
            minNum = new NumericUpDown { Minimum = 10, Maximum = 170, Value = Math.Max(10, Math.Min(170, settings.MinSeconds)), Width = 70 };
            maxNum = new NumericUpDown { Minimum = 15, Maximum = 180, Value = Math.Max(15, Math.Min(180, settings.MaxSeconds)), Width = 70 };
            defClipsNum = new NumericUpDown { Minimum = 1, Maximum = 20, Value = Math.Max(1, Math.Min(20, settings.ClipsPerVideo)), Width = 70 };
            defStyleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
            foreach (var st in SubtitleStyles.All) defStyleCombo.Items.Add(st.Name);
            defStyleCombo.SelectedItem = settings.SubtitleStyle;
            faceCheck = new CheckBox { Text = "Кадрувати за обличчям (вбудований детектор Windows)" + (FaceFrame.Available ? "" : " — недоступно на цьому ПК"), Checked = settings.FaceFraming, AutoSize = true };
            hookCheck = new CheckBox { Text = "Показувати заголовок-гачок перші 3 секунди", Checked = settings.HookTitle, AutoSize = true };
            coverCheck = new CheckBox { Text = "Ставити обкладинку (найяскравіший кадр + назва)", Checked = settings.UploadCover, AutoSize = true };

            add("Google Client ID:", clientIdBox);
            add("Google Client secret:", clientSecretBox);
            add("", Row(Btn("❔ Як отримати (інструкція)", (s, e) => ShowGuide()), Btn("Відкрити Google Cloud Console", (s, e) => OpenUrl(Guide.ConsoleUrl))));
            var browse = Btn("Огляд…", (s, e) =>
            {
                using (var d = new FolderBrowserDialog { SelectedPath = outputBox.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) outputBox.Text = d.SelectedPath;
            });
            var outRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            outputBox.Dock = DockStyle.Fill;
            outRow.Controls.Add(outputBox, 0, 0);
            outRow.Controls.Add(browse, 1, 0);
            add("Папка для кліпів:", outRow);
            add("Шаблон назви:", titleTplBox);
            add("Шаблон опису:", descTplBox);
            add("Шаблон тегів:", tagsTplBox);
            add("", new Label
            {
                AutoSize = true, ForeColor = Color.DimGray,
                Text = "Змінні: {hook} — перше речення фрагмента, {source_title}, {source_channel}, {source_url}, {keywords}, {hashtags}, {source_tags}, {n}, {text}"
            });
            add("Довжина кліпу, с:", Row(Lbl("від"), minNum, Lbl("до"), maxNum));
            add("Кліпів з одного відео:", Row(defClipsNum));
            add("Стиль субтитрів:", Row(defStyleCombo));
            add("", faceCheck);
            add("", hookCheck);
            add("", coverCheck);
            var save = Btn("💾 Зберегти налаштування", (s, e) => SaveSettings());
            save.Font = new Font(Font, FontStyle.Bold);
            add("", Row(save, Lbl("Версія " + AppVersion.Text), new LinkLabel { Text = "Сторінка програми", AutoSize = true, Margin = new Padding(6, 9, 3, 3) }));
            var link = (LinkLabel)((FlowLayoutPanel)t.Controls[t.Controls.Count - 1]).Controls[2];
            link.LinkClicked += (s, e) => OpenUrl("https://github.com/" + AppVersion.Repo);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(t);
            page.Controls.Add(scroll);
            return page;
        }

        void SaveSettings()
        {
            if (maxNum.Value < minNum.Value + 5) maxNum.Value = Math.Min(maxNum.Maximum, minNum.Value + 5);
            settings.ClientId = clientIdBox.Text.Trim();
            settings.ClientSecret = clientSecretBox.Text.Trim();
            settings.OutputFolder = string.IsNullOrWhiteSpace(outputBox.Text) ? new AppSettings().OutputFolder : outputBox.Text.Trim();
            settings.TitleTemplate = titleTplBox.Text;
            settings.DescriptionTemplate = descTplBox.Text;
            settings.TagsTemplate = tagsTplBox.Text;
            settings.MinSeconds = (int)minNum.Value;
            settings.MaxSeconds = (int)maxNum.Value;
            settings.ClipsPerVideo = (int)defClipsNum.Value;
            settings.SubtitleStyle = defStyleCombo.SelectedItem as string ?? settings.SubtitleStyle;
            settings.FaceFraming = faceCheck.Checked;
            settings.HookTitle = hookCheck.Checked;
            settings.UploadCover = coverCheck.Checked;
            try
            {
                Directory.CreateDirectory(settings.OutputFolder);
                settings.Save();
                clipsNum.Value = settings.ClipsPerVideo;
                styleCombo.SelectedItem = settings.SubtitleStyle;
                Log("Налаштування збережено.");
                MessageBox.Show("Збережено.", "AutoShorts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "AutoShorts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
