using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoShorts
{
    public partial class MainForm
    {
        TextBox clientIdBox, clientSecretBox, outputBox, titleTplBox, descTplBox, tagsTplBox;
        Stepper minNum, maxNum, defClipsNum;
        Dropdown defStyleCombo;
        Toggle faceCheck, hookCheck, coverCheck;

        Page BuildSettingsTab()
        {
            var outer = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 0, 14, 10) };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Func<string, string, TableLayoutPanel> group = (title, note) =>
            {
                outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var l = Section(title);
                l.Margin = new Padding(4, 4, 3, 6);
                outer.Controls.Add(l, 0, outer.RowStyles.Count - 1);
                var g = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
                g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var card = CardOf(g);
                card.Margin = new Padding(0, 0, 0, note == null ? 18 : 4);
                outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                outer.Controls.Add(card, 0, outer.RowStyles.Count - 1);
                if (note != null)
                {
                    outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var n = Hint(note);
                    n.Margin = new Padding(6, 2, 3, 18);
                    n.Font = Theme.F(9F);
                    outer.Controls.Add(n, 0, outer.RowStyles.Count - 1);
                }
                return g;
            };

            Action<TableLayoutPanel, string, Control> add = (g, label, c) =>
            {
                c.Dock = DockStyle.Fill;
                g.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Theme.Text, Margin = new Padding(3, 4, 18, 4) };
                g.Controls.Add(l, 0, g.RowStyles.Count - 1);
                g.Controls.Add(c, 1, g.RowStyles.Count - 1);
            };
            Action<TableLayoutPanel, Control> addWide = (g, c) =>
            {
                c.Dock = DockStyle.Fill;
                g.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                g.Controls.Add(c, 0, g.RowStyles.Count - 1);
                g.SetColumnSpan(c, 2);
            };

            clientIdBox = new TextBox { Text = settings.ClientId ?? "" };
            clientSecretBox = new TextBox { Text = settings.ClientSecret ?? "", UseSystemPasswordChar = true };
            outputBox = new TextBox { Text = settings.OutputFolder };
            titleTplBox = new TextBox { Text = settings.TitleTemplate };
            descTplBox = new TextBox { Text = settings.DescriptionTemplate, Multiline = true, ScrollBars = ScrollBars.Vertical };
            tagsTplBox = new TextBox { Text = settings.TagsTemplate };
            minNum = new Stepper(110) { Minimum = 10, Maximum = 170, Value = Math.Max(10, Math.Min(170, settings.MinSeconds)) };
            maxNum = new Stepper(110) { Minimum = 15, Maximum = 180, Value = Math.Max(15, Math.Min(180, settings.MaxSeconds)) };
            defClipsNum = new Stepper(110) { Minimum = 1, Maximum = 20, Value = Math.Max(1, Math.Min(20, settings.ClipsPerVideo)) };
            defStyleCombo = Dd(null);
            defStyleCombo.ItemImage = o => StylePreview.Get(SubtitleStyles.Get(o as string), (int)(120 * Theme.Dpi), (int)(44 * Theme.Dpi));
            foreach (var st in SubtitleStyles.All) defStyleCombo.Items.Add(st.Name);
            defStyleCombo.SelectedItem = settings.SubtitleStyle;
            defStyleCombo.Relayout();
            faceCheck = new Toggle("Кадрувати за обличчям (вбудований детектор Windows)" + (FaceFrame.Available ? "" : " — недоступно на цьому ПК")) { Checked = settings.FaceFraming };
            hookCheck = new Toggle("Показувати заголовок-гачок перші 3 секунди") { Checked = settings.HookTitle };
            coverCheck = new Toggle("Ставити обкладинку (найяскравіший кадр + назва)") { Checked = settings.UploadCover };

            var yt = group("YouTube", "Client ID потрібен один раз — він дозволяє програмі входити у ваші канали. Ключі ШІ не потрібні.");
            add(yt, "Client ID", new Field(clientIdBox, 0, "xxxxxxxx.apps.googleusercontent.com"));
            add(yt, "Client secret", new Field(clientSecretBox, 0, null));
            add(yt, "", Row(Btn("Майстер підключення", (s, e) => ShowConnectWizard(), ButtonKind.Primary, Glyphs.Help), Btn("Інструкція", (s, e) => ShowGuide(), ButtonKind.Secondary),
                            Btn("Відкрити Google Cloud Console", (s, e) => OpenUrl(Guide.ConsoleUrl), ButtonKind.Plain, Glyphs.Link)));

            var files = group("Файли", null);
            var browse = Btn("Огляд…", (s, e) =>
            {
                using (var d = new FolderBrowserDialog { SelectedPath = outputBox.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) outputBox.Text = d.SelectedPath;
            }, ButtonKind.Secondary, Glyphs.Folder);
            var outRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var outField = new Field(outputBox, 0, null) { Dock = DockStyle.Fill };
            outRow.Controls.Add(outField, 0, 0);
            browse.Anchor = AnchorStyles.Left;
            outRow.Controls.Add(browse, 1, 0);
            add(files, "Папка для кліпів", outRow);

            var tpl = group("Шаблони публікації",
                "Змінні: {hook} — перше речення фрагмента, {source_title}, {source_channel}, {source_url}, {keywords}, {hashtags}, {source_tags}, {n}, {text}");
            add(tpl, "Назва", new Field(titleTplBox, 0, null));
            add(tpl, "Опис", new Field(descTplBox, 86, null));
            add(tpl, "Теги", new Field(tagsTplBox, 0, null));

            var clips = group("Кліпи", null);
            add(clips, "Довжина, с", Row(Lbl("від"), minNum, Lbl("до"), maxNum));
            add(clips, "Кліпів з одного відео", Row(defClipsNum));
            add(clips, "Стиль субтитрів", Row(defStyleCombo));

            var proc = group("Обробка", null);
            addWide(proc, faceCheck);
            addWide(proc, hookCheck);
            addWide(proc, coverCheck);

            var link = new LinkLabel { Text = "Сторінка програми", AutoSize = true, LinkColor = Theme.Accent, BackColor = Theme.Bg, ActiveLinkColor = Theme.AccentPressed, LinkBehavior = LinkBehavior.HoverUnderline };
            link.LinkClicked += (s, e) => OpenUrl("https://github.com/" + AppVersion.Repo);
            var footer = Row(Btn("Зберегти налаштування", (s, e) => SaveSettings(), ButtonKind.Primary, Glyphs.Save),
                             Lbl("AutoShorts " + AppVersion.Text), link);
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.Controls.Add(footer, 0, outer.RowStyles.Count - 1);
            outer.RowCount = outer.RowStyles.Count;

            var scroll = new ScrollHost(outer);
            return new Page("Налаштування", "Вхід Google, шаблони текстів і параметри нарізки", scroll);
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
                settings.Recipe.Hook = settings.HookTitle;
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
