using System;
using System.Collections.Generic;
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
        // стан сторінки «Створення»
        List<string> createLinks = new List<string>();
        VideoInfo createInfo;
        Recipe recipe;
        int createToken;

        CoverView srcCover;
        Label srcTitle, srcMeta, srcSrt, rangeInfo, logoInfo;
        Dropdown langDd, layoutDd, clipsDd, lenDd, translateDd, logoPosDd, channelCombo, aspectDd, subPosDd, subSizeDd, liveDd;
        Control liveRow;
        static readonly int[] ClipCounts = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25, 30, 40, 50, 60, 80, 100, 150, 200 };
        static readonly string[][] Aspects =
        {
            new[] { "9:16 — Shorts, Reels, TikTok", "9:16" },
            new[] { "4:5 — Instagram", "4:5" },
            new[] { "1:1 — квадрат", "1:1" },
            new[] { "16:9 — звичайне відео", "16:9" },
        };
        static readonly string[] SubPositions = { "Низ", "Центр", "Верх" };
        static readonly string[][] SubSizes = { new[] { "Малий", "80" }, new[] { "Звичайний", "100" }, new[] { "Великий", "120" }, new[] { "Дуже великий", "140" } };
        static readonly string[][] LiveLimits = { new[] { "До кінця ефіру", "0" }, new[] { "30 хвилин", "30" }, new[] { "1 година", "60" }, new[] { "2 години", "120" }, new[] { "4 години", "240" } };
        RangeBar rangeBar;
        TextBox rangeFromBox, rangeToBox, keywordsBox;
        Toggle captionsToggle, hookToggle, audioToggle, zoomToggle, colorToggle, fadeToggle, autoPublishCheck, pausesToggle, adsToggle, trackToggle;
        Dropdown fontDd;
        ChoiceGallery styleGallery, hookGallery;
        TableLayoutPanel summary;
        AButton createBtn;
        Control rangeBlock;
        bool loadingCreate;

        static readonly string[][] Lengths =
        {
            new[] { "Авто (з налаштувань)", "0", "0" },
            new[] { "до 30 с", "15", "30" },
            new[] { "30–60 с", "30", "60" },
            new[] { "60–90 с", "60", "90" },
            new[] { "90 с – 3 хв", "90", "175" },
        };

        static readonly string[][] Languages =
        {
            new[] { "Без перекладу", "" }, new[] { "Українська", "uk" }, new[] { "English", "en" }, new[] { "Polski", "pl" },
            new[] { "Deutsch", "de" }, new[] { "Español", "es" }, new[] { "Français", "fr" }, new[] { "Italiano", "it" },
            new[] { "Português", "pt" }, new[] { "Türkçe", "tr" }, new[] { "Русский", "ru" },
        };

        static string LangName(string code)
        {
            foreach (var l in Languages) if (l[1] == code) return l[0];
            return code;
        }

        Toggle T(string text) { return new Toggle(text) { Margin = new Padding(3, 6, 3, 6) }; }

        // рядок налаштування: значок-плитка, назва, опис, праворуч — керування
        Control SettingRow(string glyph, string title, string desc, params Control[] right)
        {
            var t = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0, 6, 0, 6) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var icon = new GlyphTile(glyph) { Anchor = AnchorStyles.Left };
            t.Controls.Add(icon, 0, 0);
            var txt = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(8, 0, 0, 0) };
            txt.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.Semi(10F), ForeColor = Theme.Text, Margin = new Padding(0) });
            if (desc != null) txt.Controls.Add(new Label { Text = desc, AutoSize = true, ForeColor = Theme.Tertiary, Font = Theme.F(8.75F), MaximumSize = new Size(520, 0), Margin = new Padding(0, 2, 0, 0) });
            t.Controls.Add(txt, 1, 0);
            var f = Row(right);
            f.WrapContents = false;
            f.Dock = DockStyle.None;
            f.Anchor = AnchorStyles.Right;
            t.Controls.Add(f, 2, 0);
            return t;
        }

        static Label Group(string text)
        {
            return new Label { Text = text, AutoSize = true, Font = Theme.Semi(9.5F), ForeColor = Theme.Tertiary, Margin = new Padding(2, 18, 3, 4) };
        }

        Page BuildCreatePage()
        {
            recipe = settings.Recipe.Clone();

            // --- джерело: обкладинка, назва, мова, відрізок, макет, SRT ---
            srcCover = new CoverView { Size = new Size(340, 191), Margin = new Padding(0, 0, 22, 0), Cursor = Cursors.Default, ShowPlay = false };
            srcTitle = new Label { AutoSize = true, Font = Theme.Semi(11.5F), ForeColor = Theme.Text, MaximumSize = new Size(340, 0), Margin = new Padding(0, 10, 0, 2) };
            srcMeta = new Label { AutoSize = true, ForeColor = Theme.Secondary, MaximumSize = new Size(340, 0), Margin = new Padding(0, 0, 0, 6) };
            langDd = Dd("Мова мовлення");
            langDd.SelectedIndexChanged += (s, e) => { if (!loadingCreate) { recipe.CaptionLang = SelectedLang(); UpdateSummary(); } };
            var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            left.Controls.AddRange(new Control[] { srcCover, srcTitle, srcMeta, langDd });

            rangeBar = new RangeBar();
            rangeBar.RangeChanged += (s, e) => { if (!loadingCreate) { ShowRangeText(); UpdateSummary(); } };
            rangeFromBox = new TextBox { TextAlign = HorizontalAlignment.Center };
            rangeToBox = new TextBox { TextAlign = HorizontalAlignment.Center };
            EventHandler applyRange = (s, e) =>
            {
                double a = RangeDialog.ParseTime(rangeFromBox.Text), b = RangeDialog.ParseTime(rangeToBox.Text);
                if (a >= 0) rangeBar.Start = Math.Min(a, Math.Max(0, rangeBar.Duration - 15));
                if (b >= 0) rangeBar.End = b >= rangeBar.Duration || b <= rangeBar.Start ? 0 : b;
                rangeBar.Invalidate();
                ShowRangeText();
                UpdateSummary();
            };
            rangeFromBox.Leave += applyRange; rangeToBox.Leave += applyRange;
            rangeFromBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { applyRange(s, e); e.SuppressKeyPress = true; } };
            rangeToBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { applyRange(s, e); e.SuppressKeyPress = true; } };
            rangeInfo = new Label { AutoSize = true, ForeColor = Theme.Secondary, Anchor = AnchorStyles.None };
            var times = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0) };
            times.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110 * Theme.Dpi));
            times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            times.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110 * Theme.Dpi));
            times.Controls.Add(new Field(rangeFromBox, 0, null) { Dock = DockStyle.Fill }, 0, 0);
            times.Controls.Add(rangeInfo, 1, 0);
            times.Controls.Add(new Field(rangeToBox, 0, null) { Dock = DockStyle.Fill }, 2, 0);

            layoutDd = Dd("Макет", "Кадр за обличчям", "Ціле відео на розмитому фоні", "Стрім: камера зверху, гра знизу");
            layoutDd.SelectedIndexChanged += (s, e) => { if (!loadingCreate) { recipe.Layout = layoutDd.SelectedIndex == 1 ? "fit" : layoutDd.SelectedIndex == 2 ? "stream" : "crop"; UpdateSummary(); } };
            srtBtnLabel();
            var srtBtn = Btn("Завантажити SRT", (s, e) => PickSrt(), ButtonKind.Plain, Glyphs.Upload);
            var rangeHead = Header("Відрізок відео", Btn("Усе відео", (s, e) => { rangeBar.Start = 0; rangeBar.End = 0; rangeBar.Invalidate(); ShowRangeText(); UpdateSummary(); }, ButtonKind.Plain));
            rangeBlock = Stack(rangeHead, 0, rangeBar, 0, times, 0);
            ((TableLayoutPanel)rangeBlock).AutoSize = true;
            var right = Stack(rangeBlock, 0,
                Hint("Моменти шукаються лише у вибраному відрізку — зручно для довгих стрімів і подкастів. Зелена крива — графік «найчастіше переглядають»."), 0,
                Row(layoutDd, srtBtn, srcSrt), 0);
            right.AutoSize = true;

            var srcRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0) };
            srcRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            srcRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            srcRow.Controls.Add(left, 0, 0);
            right.Dock = DockStyle.Fill;
            srcRow.Controls.Add(right, 1, 0);
            var srcCard = CardOf(srcRow);
            srcCard.Padding = new Padding(20);

            // --- нарізка ---
            clipsDd = Dd("Кліпів", ClipCounts.Select(i => (object)i).ToArray());
            aspectDd = Dd("Формат", Aspects.Select(a => (object)a[0]).ToArray());
            aspectDd.SelectedIndexChanged += (s, e) => { if (!loadingCreate) { recipe.Aspect = Aspects[Math.Max(0, aspectDd.SelectedIndex)][1]; UpdateSummary(); } };
            liveDd = Dd("Записувати", LiveLimits.Select(a => (object)a[0]).ToArray());
            liveDd.SelectedIndex = 0;
            liveDd.SelectedIndexChanged += (s, e) => UpdateSummary();
            lenDd = Dd("Довжина", Lengths.Select(l => (object)l[0]).ToArray());
            clipsDd.SelectedIndexChanged += (s, e) => UpdateSummary();
            lenDd.SelectedIndexChanged += (s, e) => UpdateSummary();
            keywordsBox = new TextBox();
            keywordsBox.TextChanged += (s, e) => { recipe.Keywords = keywordsBox.Text; UpdateSummary(); };
            pausesToggle = T(null); pausesToggle.CheckedChanged += (s, e) => { recipe.CutPauses = pausesToggle.Checked; UpdateSummary(); };
            adsToggle = T(null); adsToggle.CheckedChanged += (s, e) => { recipe.SkipAds = adsToggle.Checked; UpdateSummary(); };
            var cutCard = CardOf(Stack(
                SettingRow(Glyphs.Cut, "Нарізка", "До 200 кліпів з одного відео", clipsDd, lenDd), 0,
                SettingRow(Glyphs.Play, "Формат кадру", "9:16, 4:5, 1:1 або 16:9", aspectDd), 0,
                (liveRow = SettingRow(Glyphs.Clock, "Прямий ефір", "Іде трансляція: запишу її і наріжу моменти по ходу — кількість кліпів рахується на годину ефіру", liveDd)), 0,
                SettingRow(Glyphs.Clock, "Вирізати паузи", "Прибирає мовчання всередині кліпу — темп як у топових шортсах", pausesToggle), 0,
                SettingRow(Glyphs.Stop, "Пропускати рекламу", "Вставки з «промокод», «посилання в описі», «спонсор» не потрапляють у кліпи", adsToggle), 0,
                Caption("Моменти, які обов’язково включити (необов’язково)"), 0,
                new Field(keywordsBox, 0, "Наприклад: гроші, бізнес, порада — фрагменти з цими словами отримають пріоритет"), 0));

            // --- головне: субтитри, переклад, гачок ---
            captionsToggle = T(null);
            captionsToggle.CheckedChanged += (s, e) => { recipe.Captions = captionsToggle.Checked; styleGallery.Enabled = captionsToggle.Checked; styleGallery.Invalidate(); UpdateSummary(); };
            styleGallery = new ChoiceGallery { Tile = new Size(148, 82) };
            foreach (var st in SubtitleStyles.All) styleGallery.Items.Add(st.Name);
            styleGallery.ImageFor = (name, sz) => StylePreview.Get(SubtitleStyles.Get(name), sz.Width, sz.Height);
            styleGallery.SelectedIndexChanged += (s, e) => UpdateSummary();
            translateDd = Dd(null, Languages.Select(l => (object)l[0]).ToArray());
            translateDd.SelectedIndexChanged += (s, e) => { if (!loadingCreate) { recipe.TranslateTo = Languages[Math.Max(0, translateDd.SelectedIndex)][1]; if (recipe.TranslateTo == "") recipe.TranslateTo = null; UpdateSummary(); } };
            hookToggle = T(null);
            hookToggle.CheckedChanged += (s, e) => { recipe.Hook = hookToggle.Checked; UpdateSummary(); };
            hookGallery = new ChoiceGallery { Tile = new Size(84, 150) };
            hookGallery.Items.AddRange(SubtitleStyles.HookStyles);
            hookGallery.ImageFor = (name, sz) => HookPreview.Get(name, sz.Width, sz.Height);
            hookGallery.SelectedIndexChanged += (s, e) => { recipe.HookStyle = hookGallery.SelectedItem; UpdateSummary(); };

            subPosDd = Dd("Де", SubPositions.Select(p => (object)p).ToArray());
            subPosDd.SelectedIndexChanged += (s, e) => { if (!loadingCreate) { recipe.SubPosition = subPosDd.SelectedItem as string; UpdateSummary(); } };
            subSizeDd = Dd("Розмір", SubSizes.Select(p => (object)p[0]).ToArray());
            subSizeDd.SelectedIndexChanged += (s, e) => { if (!loadingCreate) { recipe.SubSize = int.Parse(SubSizes[Math.Max(0, subSizeDd.SelectedIndex)][1]); UpdateSummary(); } };
            fontDd = Dd(null, new object[] { Fonts.FromStyle }.Concat(Fonts.All.Select(f => (object)f.Label)).ToArray());
            fontDd.ItemImage = o => FontPreview(o as string);
            fontDd.SelectedIndexChanged += async (s, e) =>
            {
                if (loadingCreate) return;
                recipe.FontName = fontDd.SelectedIndex <= 0 ? null : fontDd.SelectedItem as string;
                UpdateSummary();
                var f = Fonts.Find(recipe.FontName);
                if (f != null) { try { await Fonts.EnsureAsync(f, CancellationToken.None); } catch (Exception ex) { Log("Шрифт: " + ex.Message); } }
            };
            trackToggle = T(null); trackToggle.CheckedChanged += (s, e) => { recipe.TrackFace = trackToggle.Checked; UpdateSummary(); };
            audioToggle = T(null); audioToggle.CheckedChanged += (s, e) => { recipe.EnhanceAudio = audioToggle.Checked; UpdateSummary(); };
            zoomToggle = T(null); zoomToggle.CheckedChanged += (s, e) => { recipe.AutoZoom = zoomToggle.Checked; UpdateSummary(); };
            colorToggle = T(null); colorToggle.CheckedChanged += (s, e) => { recipe.ColorBoost = colorToggle.Checked; UpdateSummary(); };
            fadeToggle = T(null); fadeToggle.CheckedChanged += (s, e) => { recipe.Fades = fadeToggle.Checked; UpdateSummary(); };

            logoInfo = new Label { AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(6, 3, 3, 3) };
            logoPosDd = Dd(null, "Праворуч угорі", "Ліворуч угорі", "Праворуч унизу", "Ліворуч унизу");
            logoPosDd.SelectedIndexChanged += (s, e) => { if (!loadingCreate) recipe.LogoPos = logoPosDd.SelectedItem as string; };

            channelCombo = Dd("Канал");
            autoPublishCheck = T(null);
            autoPublishCheck.CheckedChanged += (s, e) => UpdateSummary();
            channelCombo.SelectedIndexChanged += (s, e) => UpdateSummary();

            var recipeCard = CardOf(Stack(
                Group("ГОЛОВНЕ"), 0,
                SettingRow(Glyphs.Captions, "Субтитри", "Великі слова по 2–4 з підсвіткою того, що звучить, і ключових слів", captionsToggle), 0,
                styleGallery, 0,
                SettingRow(Glyphs.Captions, "Шрифт субтитрів", "29 шрифтів з кирилицею — завантажується лише вибраний", fontDd), 0,
                SettingRow(Glyphs.Captions, "Положення і розмір", "Внизу, по центру чи вгорі кадру — і наскільки великі слова", subPosDd, subSizeDd), 0,
                SettingRow(Glyphs.Link, "Переклад субтитрів", "Автопереклад YouTube — без ключів і без моделей", translateDd), 0,
                SettingRow(Glyphs.Sparkle, "Заголовок-гачок", "Перше речення фрагмента великим текстом у перші 3 секунди", hookToggle), 0,
                hookGallery, 0,
                Group("ЗВУК"), 0,
                SettingRow(Glyphs.Refresh, "Чистий звук", "Прибирає гул і шум, вирівнює гучність до стандарту шортсів (−14 LUFS)", audioToggle), 0,
                Group("КАРТИНКА"), 0,
                SettingRow(Glyphs.People, "Кадр за мовцем", "Камера плавно їде за обличчям, а не стоїть на місці (детектор облич Windows)", trackToggle), 0,
                SettingRow(Glyphs.Add, "Автонаближення", "Кожне друге речення трохи ближче — динаміка, як при ручному монтажі", zoomToggle), 0,
                SettingRow(Glyphs.Sparkle, "Яскраві кольори", "Трохи більше контрасту, насиченості й різкості", colorToggle), 0,
                SettingRow(Glyphs.Play, "Плавний початок і кінець", "Коротке з’явлення й згасання картинки та звуку", fadeToggle), 0,
                Group("БРЕНД"), 0,
                SettingRow(Glyphs.Settings, "Логотип", "PNG з прозорістю — з’явиться в кутку кожного кліпу",
                    logoInfo, logoPosDd, Btn("Вибрати…", (s, e) => PickLogo(), ButtonKind.Secondary), Btn("", (s, e) => { recipe.LogoPath = null; ShowLogo(); }, ButtonKind.Plain, Glyphs.Delete)), 0,
                Group("ПУБЛІКАЦІЯ"), 0,
                SettingRow(Glyphs.Upload, "Канал YouTube", "Куди викладати кліпи. Час — найкращий для каналу", channelCombo), 0,
                SettingRow(Glyphs.Clock, "Публікувати одразу", "Без перегляду: кліпи з найвищою оцінкою підуть у розклад автоматично", autoPublishCheck), 0));
            recipeCard.Margin = new Padding(0, 0, 0, 30);

            var leftCol = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 0, 18, 0) };
            leftCol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var c in new Control[] { srcCard, cutCard, recipeCard })
            {
                leftCol.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                c.Dock = DockStyle.Fill;
                leftCol.Controls.Add(c, 0, leftCol.RowStyles.Count - 1);
            }
            var scroll = new ScrollHost(leftCol) { Dock = DockStyle.Fill };

            // --- підсумок праворуч ---
            summary = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            createBtn = Btn("Створити", (s, e) => CreateProjects(), ButtonKind.Primary);
            createBtn.HeightFactor = 2.7f;
            createBtn.Margin = new Padding(0, 16, 0, 8);
            var legal = new Label { Text = "Продовжуючи, ви підтверджуєте, що маєте права на це відео.", AutoSize = true, ForeColor = Theme.Tertiary, Font = Theme.F(8.5F), MaximumSize = new Size(280, 0), Margin = new Padding(0, 4, 0, 0) };
            var sumCard = CardOf(Stack(Section("Підсумок"), 0, summary, 0, createBtn, 0, legal, 0));
            sumCard.Padding = new Padding(20, 18, 20, 20);
            var back = Btn("Назад", (s, e) => ShowPage(PHome), ButtonKind.Plain, Glyphs.Back);
            var rightCol = Stack(sumCard, 0, back, 0);
            rightCol.AutoSize = true;

            var grid = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330 * Theme.Dpi));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.Controls.Add(scroll, 0, 0);
            rightCol.Dock = DockStyle.Top;
            grid.Controls.Add(rightCol, 1, 0);
            return new Page("Новий проєкт", "Налаштуйте, що й як нарізати — потім натисніть «Створити»", grid);
        }

        // превʼю шрифту в списку (якщо файл уже завантажено)
        readonly Dictionary<string, Image> fontPreviews = new Dictionary<string, Image>();
        Image FontPreview(string label)
        {
            var f = Fonts.Find(label);
            if (f == null || !File.Exists(Fonts.LocalPath(f))) return null;
            Image img;
            if (fontPreviews.TryGetValue(label, out img)) return img;
            try
            {
                using (var pfc = new System.Drawing.Text.PrivateFontCollection())
                {
                    pfc.AddFontFile(Fonts.LocalPath(f));
                    var fam = pfc.Families[0];
                    var style = fam.IsStyleAvailable(FontStyle.Bold) && f.Bold ? FontStyle.Bold : FontStyle.Regular;
                    if (!fam.IsStyleAvailable(style)) style = fam.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                    var bmp = new Bitmap((int)(150 * Theme.Dpi), (int)(30 * Theme.Dpi));
                    using (var g = Graphics.FromImage(bmp))
                    using (var font = new Font(fam, 15 * Theme.Dpi, style, GraphicsUnit.Pixel))
                    using (var b = new SolidBrush(Theme.Text))
                    {
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        g.DrawString("ЦЕ ВОГОНЬ", font, b, 2, (bmp.Height - font.GetHeight(g)) / 2);
                    }
                    img = bmp;
                }
            }
            catch { img = null; }
            fontPreviews[label] = img;
            return img;
        }

        void srtBtnLabel()
        {
            srcSrt = new Label { AutoSize = true, ForeColor = Theme.Accent, Margin = new Padding(3) };
        }

        string SelectedLang()
        {
            var it = langDd.SelectedItem as ComboItem;
            return it == null ? null : it.Value as string;
        }

        void ShowRangeText()
        {
            rangeFromBox.Text = Dur(rangeBar.Start) ?? "0:00";
            rangeToBox.Text = Dur(rangeBar.End > 0 ? rangeBar.End : rangeBar.Duration) ?? "кінець";
            double len = (rangeBar.End > 0 ? rangeBar.End : rangeBar.Duration) - rangeBar.Start;
            rangeInfo.Text = len > 0 ? "вибрано " + Dur(len) + " з " + Dur(rangeBar.Duration) : "";
        }

        void ShowLogo()
        {
            logoInfo.Text = string.IsNullOrEmpty(recipe.LogoPath) ? "не вибрано" : Path.GetFileName(recipe.LogoPath);
            logoPosDd.Visible = !string.IsNullOrEmpty(recipe.LogoPath);
        }

        void PickLogo()
        {
            using (var d = new OpenFileDialog { Title = "Логотип", Filter = "Зображення|*.png;*.jpg;*.jpeg;*.webp" })
                if (d.ShowDialog(this) == DialogResult.OK) { recipe.LogoPath = d.FileName; ShowLogo(); UpdateSummary(); }
        }

        void PickSrt()
        {
            using (var d = new OpenFileDialog { Title = "Файл субтитрів", Filter = "Субтитри|*.srt;*.vtt" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    var words = Captions.ParseSrtFile(d.FileName);
                    if (words.Count == 0) { MessageBox.Show("У файлі не знайшлося жодної репліки.", "AutoShorts"); return; }
                    recipe.SrtPath = d.FileName;
                    srcSrt.Text = Path.GetFileName(d.FileName) + " · " + words.Count + " слів";
                    UpdateSummary();
                }
        }

        // ---------- відкриття сторінки «Створення» ----------

        async void OpenCreate(List<string> links)
        {
            createLinks = links;
            createInfo = null;
            int token = ++createToken;
            loadingCreate = true;
            recipe = settings.Recipe.Clone();
            recipe.SrtPath = null;
            recipe.CaptionLang = null;
            srcSrt.Text = "";

            // значення за замовчуванням
            clipsDd.SelectedItem = ClipCounts.OrderBy(c => Math.Abs(c - settings.ClipsPerVideo)).First();
            aspectDd.SelectedIndex = Math.Max(0, Array.FindIndex(Aspects, a => a[1] == (recipe.Aspect ?? "9:16")));
            subPosDd.SelectedItem = SubPositions.Contains(recipe.SubPosition) ? recipe.SubPosition : "Низ";
            subSizeDd.SelectedIndex = Math.Max(0, Array.FindIndex(SubSizes, a => a[1] == (recipe.SubSize > 0 ? recipe.SubSize : 100).ToString()));
            if (subSizeDd.SelectedIndex < 0) subSizeDd.SelectedIndex = 1;
            liveDd.SelectedIndex = 0;
            liveRow.Visible = false;
            lenDd.SelectedIndex = 0;
            keywordsBox.Text = recipe.Keywords ?? "";
            captionsToggle.Checked = recipe.Captions;
            styleGallery.SelectedItem = SubtitleStyles.Get(settings.SubtitleStyle).Name;
            translateDd.SelectedIndex = Math.Max(0, Array.FindIndex(Languages, l => l[1] == (recipe.TranslateTo ?? "")));
            hookToggle.Checked = recipe.Hook;
            hookGallery.SelectedItem = recipe.HookStyle ?? SubtitleStyles.HookStyles[0];
            audioToggle.Checked = recipe.EnhanceAudio;
            zoomToggle.Checked = recipe.AutoZoom;
            colorToggle.Checked = recipe.ColorBoost;
            fadeToggle.Checked = recipe.Fades;
            pausesToggle.Checked = recipe.CutPauses;
            adsToggle.Checked = recipe.SkipAds;
            trackToggle.Checked = recipe.TrackFace;
            fontDd.SelectedItem = Fonts.Find(recipe.FontName) != null ? recipe.FontName : Fonts.FromStyle;
            layoutDd.SelectedIndex = recipe.Layout == "fit" ? 1 : recipe.Layout == "stream" ? 2 : 0;
            logoPosDd.SelectedItem = recipe.LogoPos ?? "Праворуч угорі";
            ShowLogo();
            autoPublishCheck.Checked = !settings.RequirePreview;
            RefreshChannelCombos();

            bool single = links.Count == 1;
            bool local = single && !links[0].StartsWith("http", StringComparison.OrdinalIgnoreCase);
            SetCover(null);
            srcTitle.Text = single ? (local ? Path.GetFileNameWithoutExtension(links[0]) : "Читаю відео…") : links.Count + " відео";
            srcMeta.Text = single ? (local ? Path.GetFileName(links[0]) : links[0]) : string.Join("\n", links.Take(4).Select(l => local || !l.StartsWith("http") ? Path.GetFileName(l) : l)) + (links.Count > 4 ? "\n…" : "");
            langDd.Items.Clear();
            langDd.Items.Add(new ComboItem("Авто", null));
            langDd.SelectedIndex = 0;
            langDd.Visible = single && !local;
            rangeBlock.Visible = single;
            rangeBar.Heat = new List<HeatPoint>();
            rangeBar.Start = 0; rangeBar.End = 0; rangeBar.Duration = 600;
            rangeBar.Invalidate();
            ShowRangeText();
            loadingCreate = false;
            UpdateSummary();
            ShowPage(PCreate);

            if (!single) return;
            createBtn.Enabled = false;
            try
            {
                await Tools.EnsureAsync(Log, CancellationToken.None);
                if (token != createToken) return;
                if (local)
                {
                    double d = await Renderer.ProbeDurationAsync(links[0], CancellationToken.None);
                    var tmp = Path.Combine(Path.GetTempPath(), "autoshorts_preview.jpg");
                    await Tools.RunAsync(Tools.FFmpeg, "-v error -y -ss " + (d * 0.12).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " -i " + Tools.Q(links[0]) + " -frames:v 1 -vf scale=640:-2 " + Tools.Q(tmp), null, null, CancellationToken.None);
                    if (token != createToken) return;
                    rangeBar.Duration = Math.Max(30, d);
                    srcMeta.Text = Dur(d) + " · файл з комп’ютера";
                    if (File.Exists(tmp)) SetCover(tmp);
                    var side = Captions.FromSidecar(links[0]);
                    if (side.Count > 0) srcSrt.Text = "знайдено .srt · " + side.Count + " слів";
                }
                else
                {
                    var info = await Downloader.GetInfoAsync(links[0], CancellationToken.None);
                    if (token != createToken) return;
                    createInfo = info;
                    srcTitle.Text = info.Title;
                    liveRow.Visible = info.IsLive;
                    if (info.IsLive) rangeBlock.Visible = false;
                    srcMeta.Text = (string.IsNullOrEmpty(info.Channel) ? "" : info.Channel + " · ") + Dur(info.Duration) +
                                   (info.Heatmap.Count > 0 ? " · є графік переглядів" : "") + (info.CaptionUrl != null ? " · є субтитри" : " · без субтитрів");
                    rangeBar.Duration = Math.Max(30, info.Duration);
                    rangeBar.Heat = info.Heatmap;
                    loadingCreate = true;
                    foreach (var l in info.CaptionLangs) langDd.Items.Add(new ComboItem(LangName(l) == l ? l : LangName(l), l));
                    langDd.Relayout();
                    loadingCreate = false;
                    var job = new Job { Url = links[0], VideoId = info.Id };
                    try
                    {
                        await Thumbs.EnsureAsync(job, CancellationToken.None);
                        if (token == createToken && job.ThumbPath != null) SetCover(job.ThumbPath);
                    }
                    catch { }
                }
                rangeBar.Invalidate();
                ShowRangeText();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                if (token != createToken) return;
                srcTitle.Text = single && !local ? "Не вдалося прочитати відео" : srcTitle.Text;
                srcMeta.Text = ex.Message.Length > 200 ? ex.Message.Substring(0, 200) + "…" : ex.Message;
                rangeBlock.Visible = false;
            }
            finally
            {
                if (token == createToken) createBtn.Enabled = true;
            }
        }

        void SetCover(string path)
        {
            var old = srcCover.Image;
            srcCover.Image = null;
            if (old != null) old.Dispose();
            if (path == null || !File.Exists(path)) return;
            try { srcCover.Image = Image.FromStream(new MemoryStream(File.ReadAllBytes(path))); } catch { }
        }

        void UpdateSummary()
        {
            if (summary == null || loadingCreate) return;
            summary.SuspendLayout();
            summary.Controls.Clear();
            summary.RowStyles.Clear();
            Action<string, string, Color> add = (k, v, c) =>
            {
                summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                int r = summary.RowStyles.Count - 1;
                summary.Controls.Add(new Label { Text = k, AutoSize = true, ForeColor = Theme.Secondary, Margin = new Padding(0, 5, 8, 5) }, 0, r);
                summary.Controls.Add(new Label { Text = v, AutoSize = true, ForeColor = c, MaximumSize = new Size(170, 0), TextAlign = ContentAlignment.TopRight, Anchor = AnchorStyles.Right, Margin = new Padding(0, 5, 0, 5) }, 1, r);
            };
            bool single = createLinks.Count == 1;
            double dur = rangeBar.Duration;
            double len = (rangeBar.End > 0 ? rangeBar.End : dur) - rangeBar.Start;
            add("Відео", single ? (createInfo != null || dur != 600 ? Dur(dur) : "…") : createLinks.Count + " шт.", Theme.Text);
            if (single && (rangeBar.Start > 0 || rangeBar.End > 0)) add("Відрізок", Dur(rangeBar.Start) + " – " + Dur(rangeBar.End > 0 ? rangeBar.End : dur) + " (" + Dur(len) + ")", Theme.Text);
            bool live = createInfo != null && createInfo.IsLive && single;
            add("Кліпів", clipsDd.SelectedItem + (live ? " на годину" : "") + " × " + (lenDd.SelectedIndex <= 0 ? settings.MinSeconds + "–" + settings.MaxSeconds + " с" : lenDd.SelectedItem), Theme.Text);
            if (live) add("Ефір", "запис: " + (liveDd.SelectedItem ?? "").ToString().ToLowerInvariant(), Theme.Text);
            add("Формат", recipe.Aspect ?? "9:16", Theme.Text);
            int cnt = clipsDd.SelectedItem is int ? (int)clipsDd.SelectedItem : 0;
            if (single && !live && cnt > 1 && dur > 0 && dur != 600)
            {
                int fits = (int)(len / Math.Max(15, lenDd.SelectedIndex <= 0 ? (settings.MinSeconds + settings.MaxSeconds) / 2.0 : (double.Parse(Lengths[lenDd.SelectedIndex][1]) + double.Parse(Lengths[lenDd.SelectedIndex][2])) / 2));
                if (cnt > fits) add("Увага", "у відео поміститься ~" + Math.Max(1, fits) + " кліпів без повторів", Theme.Orange);
            }
            add("Субтитри", !recipe.Captions ? "вимкнено" : (styleGallery.SelectedItem ?? "") + (Fonts.Find(recipe.FontName) != null ? " · " + recipe.FontName : "") + (recipe.TranslateTo != null ? " · " + LangName(recipe.TranslateTo) : ""), Theme.Text);
            add("Гачок", recipe.Hook ? recipe.HookStyle ?? "так" : "ні", Theme.Text);
            var fx = new List<string>();
            if (recipe.CutPauses) fx.Add("без пауз");
            if (recipe.SkipAds) fx.Add("без реклами");
            if (recipe.TrackFace && recipe.Layout != "fit") fx.Add("кадр за мовцем");
            if (recipe.EnhanceAudio) fx.Add("чистий звук");
            if (recipe.AutoZoom) fx.Add("наближення");
            if (recipe.ColorBoost) fx.Add("кольори");
            if (recipe.Fades) fx.Add("переходи");
            if (recipe.Layout == "fit") fx.Add("розмитий фон");
            if (recipe.Layout == "stream") fx.Add("камера стримера зверху");
            if (recipe.SubPosition != null && recipe.SubPosition != "Низ") fx.Add("субтитри: " + recipe.SubPosition.ToLowerInvariant());
            if (!string.IsNullOrEmpty(recipe.LogoPath)) fx.Add("логотип");
            add("Обробка", fx.Count == 0 ? "—" : string.Join(", ", fx), Theme.Text);
            var ch = SelectedId(channelCombo);
            add("Публікація", ch == null ? "лише зберегти" : (autoPublishCheck.Checked ? "одразу, " : "після перегляду, ") + "найкращий час", Theme.Text);
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var line = new Panel { Height = 1, BackColor = Theme.Border, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 8) };
            summary.Controls.Add(line, 0, summary.RowStyles.Count - 1);
            summary.SetColumnSpan(line, 2);
            add("Разом", "безкоштовно ⚡", Theme.Accent);
            summary.ResumeLayout(true);
        }

        void CreateProjects()
        {
            if (createLinks.Count == 0) return;
            var styleName = styleGallery.SelectedItem ?? settings.SubtitleStyle;
            int count = clipsDd.SelectedItem is int ? (int)clipsDd.SelectedItem : settings.ClipsPerVideo;
            var len = Lengths[Math.Max(0, lenDd.SelectedIndex)];
            var chId = SelectedId(channelCombo);
            recipe.Keywords = keywordsBox.Text.Trim();
            settings.Recipe = recipe.Clone();
            settings.Recipe.SrtPath = null;
            settings.Recipe.CaptionLang = null;
            settings.SubtitleStyle = styleName;
            settings.ClipsPerVideo = count;
            settings.DefaultChannelId = chId;
            settings.RequirePreview = !autoPublishCheck.Checked;
            settings.HookTitle = recipe.Hook;
            try { settings.Save(); } catch { }

            bool single = createLinks.Count == 1;
            foreach (var l in createLinks)
            {
                var job = new Job
                {
                    Url = l,
                    ChannelId = chId,
                    StyleName = styleName,
                    ClipCount = count,
                    AutoPublish = autoPublishCheck.Checked && chId != null,
                    Recipe = recipe.Clone(),
                    MinLen = int.Parse(len[1]),
                    MaxLen = int.Parse(len[2]),
                    LiveMaxMinutes = int.Parse(LiveLimits[Math.Max(0, liveDd.SelectedIndex)][1]),
                    AddedUtc = DateTime.UtcNow,
                    RangeStart = single ? rangeBar.Start : 0,
                    RangeEnd = single ? rangeBar.End : 0,
                };
                if (single && createInfo != null) { job.VideoId = createInfo.Id; job.SourceTitle = createInfo.Title; job.SourceDuration = createInfo.Duration; }
                jobs.Add(job);
            }
            urlBox.Clear();
            AppSettings.SaveJobs(jobs);
            Log("Створено проєктів: " + createLinks.Count);
            createLinks = new List<string>();
            RefreshJobs();
            ShowPage(PHome);
            LoadThumbsAsync();
            if (!running) StartStop();
        }
    }

    // Квадратна плитка зі значком для рядків налаштувань
    class GlyphTile : Control
    {
        readonly string glyph;

        public GlyphTile(string glyph)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.glyph = glyph;
            AutoSize = true;
            Margin = new Padding(0, 2, 6, 2);
        }

        public override Size GetPreferredSize(Size proposedSize) { int s = (int)(Font.Height * 2.0f); return new Size(s, s); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            Theme.FillRound(g, Theme.Fill, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Height * 0.28f);
            var f = Theme.Icons(Font.SizeInPoints);
            if (f != null) TextRenderer.DrawText(g, glyph, f, ClientRectangle, Theme.Text, Theme.Center);
        }
    }
}
