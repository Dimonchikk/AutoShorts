using System;
using System.Drawing;
using System.IO;
using System.Linq;
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

            var yt = group("YouTube", "Кожен канал входить через свій Google-акаунт. Щоб додати ще один — натисніть кнопку ще раз і виберіть інший акаунт або канал.");
            var google = new GoogleButton("Увійти через Google");
            google.Click += (s, e) => AddChannel();
            var ytHead = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
            ytHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ytHead.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            accountsTitle = new Label { AutoSize = true, Font = Theme.Semi(10.5F), ForeColor = Theme.Text, Anchor = AnchorStyles.Left, Margin = new Padding(3) };
            ytHead.Controls.Add(accountsTitle, 0, 0);
            google.Anchor = AnchorStyles.Right;
            ytHead.Controls.Add(google, 1, 0);
            accountsList = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Margin = new Padding(0) };
            accountsList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            addWide(yt, ytHead);
            addWide(yt, accountsList);
            RefreshAccounts();

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

        TableLayoutPanel accountsList;
        Label accountsTitle;

        // Список підключених акаунтів: аватарка, назва, @нікнейм, кнопка «Відключити»
        void RefreshAccounts()
        {
            if (accountsList == null) return;
            accountsList.SuspendLayout();
            foreach (Control c in accountsList.Controls.Cast<Control>().ToList()) c.Dispose();
            accountsList.Controls.Clear();
            accountsList.RowStyles.Clear();
            accountsTitle.Text = settings.Channels.Count == 0 ? "Жодного каналу ще не підключено" : "Підключені канали · " + settings.Channels.Count;
            int i = 0;
            foreach (var ch in settings.Channels)
            {
                var c0 = ch;
                var row = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                var av = new Avatar { ImagePath = ch.AvatarPath, Letter = string.IsNullOrEmpty(ch.Title) ? "?" : ch.Title.Substring(0, 1).ToUpperInvariant(), Tint = ChannelColor(ch.Id), Anchor = AnchorStyles.Left };
                row.Controls.Add(av, 0, 0);
                var txt = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
                txt.Controls.Add(new Label { Text = ch.Title, AutoSize = true, Font = Theme.Semi(10.5F), ForeColor = Theme.Text, Margin = new Padding(0) });
                txt.Controls.Add(new Label
                {
                    Text = "● Підключено" + (string.IsNullOrEmpty(ch.Handle) ? "" : " · " + ch.Handle) + " · найкращий час: " + BestTime.Describe(ch),
                    AutoSize = true, ForeColor = Theme.Secondary, Font = Theme.F(9F), Margin = new Padding(0, 2, 0, 0)
                });
                row.Controls.Add(txt, 1, 0);
                var off = Btn("Відключити", (s, e) =>
                {
                    if (MessageBox.Show("Відключити канал «" + c0.Title + "» від AutoShorts?", "AutoShorts", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    settings.Channels.Remove(c0);
                    if (settings.DefaultChannelId == c0.Id) settings.DefaultChannelId = null;
                    settings.Save();
                    RefreshChannels();
                }, ButtonKind.Danger);
                off.Anchor = AnchorStyles.Right;
                row.Controls.Add(off, 2, 0);
                accountsList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                accountsList.Controls.Add(row, 0, i++);
            }
            accountsList.RowCount = Math.Max(1, i);
            accountsList.ResumeLayout(true);
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
