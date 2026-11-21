using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace AutoShorts
{
    public partial class MainForm
    {
        static readonly Regex ClientIdRx = new Regex(@"\b\d{6,}-[a-z0-9]{20,}\.apps\.googleusercontent\.com\b");
        static readonly Regex SecretRx = new Regex(@"\bGOCSPX-[A-Za-z0-9_\-]{20,}\b");

        // Майстер підключення YouTube: відкриває потрібні сторінки Google Cloud і сам ловить Client ID / secret.
        // Повертає true, якщо клієнт налаштовано.
        bool ShowConnectWizard()
        {
            using (var f = new Form
            {
                Text = "Підключення YouTube", Font = Font, BackColor = Theme.Bg, ForeColor = Theme.Text,
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, MinimizeBox = false,
                AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96), Size = new Size(940, 860), MinimumSize = new Size(760, 640)
            })
            {
                f.HandleCreated += (s, e) => Theme.StyleWindow(f);
                var visited = new HashSet<int>();
                var idBox = new TextBox { Text = settings.ClientId ?? "" };
                var secBox = new TextBox { Text = settings.ClientSecret ?? "" };
                var status = new Label { AutoSize = true, ForeColor = Theme.Secondary, MaximumSize = new Size(760, 0), Margin = new Padding(4, 8, 3, 3) };
                var done = Btn("Готово — увійти в канал", null, ButtonKind.Primary, Glyphs.Check);
                done.HeightFactor = 2.3f;
                done.Enabled = false;

                Action check = () =>
                {
                    bool okId = ClientIdRx.IsMatch(idBox.Text.Trim());
                    bool okSec = SecretRx.IsMatch(secBox.Text.Trim());
                    done.Enabled = okId;
                    status.ForeColor = okId && okSec ? Theme.Accent : Theme.Secondary;
                    status.Text = okId && okSec ? "✓ Client ID і secret знайдено — натисніть «Готово»."
                                : okId ? "✓ Client ID є. Скопіюйте ще Client secret (починається з GOCSPX-) — або натисніть «Готово», якщо секрету немає."
                                : "Чекаю на Client ID… Просто натисніть у Google «Download JSON» або скопіюйте Client ID — програма підхопить сама.";
                };
                idBox.TextChanged += (s, e) => check();
                secBox.TextChanged += (s, e) => check();

                Action<string> takeJson = path =>
                {
                    try
                    {
                        var text = File.ReadAllText(path);
                        var m1 = ClientIdRx.Match(text); var m2 = SecretRx.Match(text);
                        if (m1.Success) idBox.Text = m1.Value;
                        if (m2.Success) secBox.Text = m2.Value;
                    }
                    catch { }
                };

                // пошта — підставляється в поля Google
                var emailBox = new TextBox { Text = settings.GoogleEmail ?? "" };
                emailBox.TextChanged += (s, e) => { settings.GoogleEmail = emailBox.Text.Trim(); };
                Func<string> email = () => emailBox.Text.Trim();

                // кроки: заголовок, кнопка-посилання і що саме вписати/вибрати на сторінці
                var steps = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Margin = new Padding(0) };
                steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                // рядок інструкції: "Поле|значення" — значення з кнопкою «Копіювати» ({email} — ваша пошта); "!текст" — дія без копіювання
                Func<string, Control> line = spec =>
                {
                    if (spec.StartsWith("!"))
                        return new Label { Text = "→  " + spec.Substring(1), AutoSize = true, ForeColor = Theme.Secondary, MaximumSize = new Size(640, 0), Margin = new Padding(0, 5, 0, 5) };
                    var parts = spec.Split('|');
                    var t = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0, 1, 0, 1) };
                    t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190 * Theme.Dpi));
                    t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    t.Controls.Add(new Label { Text = parts[0], AutoSize = true, ForeColor = Theme.Secondary, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 8, 3) }, 0, 0);
                    var valLbl = new Label { AutoSize = true, Font = Theme.Semi(9.75F), ForeColor = Theme.Text, Anchor = AnchorStyles.Left, Margin = new Padding(0, 3, 10, 3) };
                    Func<string> val = () => parts[1] == "{email}" ? (email().Length > 0 ? email() : "вкажіть пошту вгорі") : parts[1];
                    valLbl.Text = val();
                    if (parts[1] == "{email}") emailBox.TextChanged += (s, e) => valLbl.Text = val();
                    t.Controls.Add(valLbl, 1, 0);
                    var copy = Btn("Копіювати", null, ButtonKind.Chip, Glyphs.Copy);
                    copy.HeightFactor = 1.55f;
                    copy.Anchor = AnchorStyles.Left;
                    copy.Click += (s, e) =>
                    {
                        var v = parts[1] == "{email}" ? email() : parts[1];
                        if (v.Length == 0) { emailBox.Focus(); return; }
                        try { Clipboard.SetText(v); } catch { }
                        copy.Text = "Скопійовано ✓"; copy.Checked = true; copy.Invalidate();
                        var tm = new Timer { Interval = 1500 };
                        tm.Tick += (s2, e2) => { tm.Dispose(); copy.Text = "Копіювати"; copy.Checked = false; copy.Invalidate(); };
                        tm.Start();
                    };
                    t.Controls.Add(copy, 2, 0);
                    return t;
                };
                var nums = new List<StepNum>();
                Action<int, string, string, string, string[]> step = (n, title, button, url, lines) =>
                {
                    var num = new StepNum(n);
                    nums.Add(num);
                    var b = Btn(button, (s, e) => { OpenUrl(url); num.Done = true; num.Invalidate(); }, ButtonKind.Primary, Glyphs.Open);
                    var head = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0), Dock = DockStyle.Fill };
                    head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    num.Anchor = AnchorStyles.Left;
                    head.Controls.Add(num, 0, 0);
                    head.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.Semi(10.5F), ForeColor = Theme.Text, Anchor = AnchorStyles.Left, Margin = new Padding(10, 0, 10, 0) }, 1, 0);
                    b.Anchor = AnchorStyles.Right;
                    head.Controls.Add(b, 2, 0);
                    var body2 = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding((int)(Font.Height * 1.9f) + 10, 4, 0, 0) };
                    foreach (var l in lines) body2.Controls.Add(line(l));
                    var card = CardOf(Stack(head, 0, body2, 0));
                    card.Margin = new Padding(0, 0, 0, 10);
                    steps.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    steps.Controls.Add(card, 0, steps.RowStyles.Count - 1);
                };
                step(1, "Створіть проєкт", "Новий проєкт", "https://console.cloud.google.com/projectcreate", new[]
                {
                    "Project name|AutoShorts",
                    "!Project ID, Organization і Parent resource не чіпайте (No organization) → Create",
                    "!Зачекайте 10–20 с і вгорі сторінки виберіть проєкт AutoShorts — усе далі робиться в ньому",
                });
                step(2, "Увімкніть YouTube Data API v3", "Відкрити", "https://console.cloud.google.com/apis/library/youtube.googleapis.com", new[]
                {
                    "!Перевірте, що вгорі вибрано ваш проєкт → натисніть Enable (якщо вже є кнопка Manage — увімкнено)",
                });
                step(3, "Увімкніть YouTube Analytics API", "Відкрити", "https://console.cloud.google.com/apis/library/youtubeanalytics.googleapis.com", new[]
                {
                    "!Так само: Enable",
                });
                step(4, "Налаштуйте екран входу", "Відкрити", "https://console.cloud.google.com/auth/overview/create", new[]
                {
                    "!Якщо бачите кнопку Get started — натисніть її",
                    "App name|AutoShorts",
                    "User support email|{email}",
                    "!Next. Audience: виберіть External → Next",
                    "Contact Information · Email|{email}",
                    "!Next. Поставте галочку «I agree to the Google API Services: User Data Policy» → Continue → Create",
                });
                step(5, "Опублікуйте застосунок", "Відкрити", "https://console.cloud.google.com/auth/audience", new[]
                {
                    "!Publishing status → Publish app → Confirm (статус стане In production)",
                    "!Або, якщо не публікуєте: Test users → Add users",
                    "Test user|{email}",
                });
                step(6, "Створіть клієнт і завантажте JSON", "Створити клієнт", "https://console.cloud.google.com/auth/clients/create", new[]
                {
                    "Application type|Desktop app",
                    "Name|AutoShorts",
                    "!Create. У вікні «OAuth client created» натисніть Download JSON — поля нижче заповняться самі",
                });

                var emailRow = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
                emailRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                emailRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                emailRow.Controls.Add(new Label { Text = "Ваша пошта Gmail", AutoSize = true, Anchor = AnchorStyles.Left, Font = Theme.Semi(10F), Margin = new Padding(3, 4, 14, 4) }, 0, 0);
                emailRow.Controls.Add(new Field(emailBox, 0, "name@gmail.com — підставиться в кроки нижче") { Dock = DockStyle.Fill, Glyph = Glyphs.Mail }, 1, 0);

                var fields = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0) };
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                fields.Controls.Add(new Label { Text = "Client ID", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 4, 14, 4) }, 0, 0);
                fields.Controls.Add(new Field(idBox, 0, "підставиться автоматично") { Dock = DockStyle.Fill }, 1, 0);
                fields.Controls.Add(new Label { Text = "Client secret", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 4, 14, 4) }, 0, 1);
                fields.Controls.Add(new Field(secBox, 0, "GOCSPX-…") { Dock = DockStyle.Fill }, 1, 1);

                var pickJson = Btn("Вибрати JSON вручну…", (s, e) =>
                {
                    using (var d = new OpenFileDialog { Title = "Файл client_secret_….json", Filter = "JSON|*.json" })
                        if (d.ShowDialog(f) == DialogResult.OK) takeJson(d.FileName);
                }, ButtonKind.Plain, Glyphs.File);

                var intro = new Label
                {
                    Text = "Це робиться один раз і безкоштовно — близько 3 хвилин. Натискайте кнопки по черзі: відкриється потрібна сторінка Google Cloud. " +
                           "Client ID і secret вводити не треба — програма сама знайде їх у «Завантаженнях» або в буфері обміну.",
                    AutoSize = true, ForeColor = Theme.Secondary, MaximumSize = new Size(800, 0), Margin = new Padding(3, 4, 3, 14)
                };
                var auditNote = new Label
                {
                    Text = "Після підключення: поки ваш проєкт не пройшов безкоштовний аудит YouTube API, YouTube лишає завантажені відео приватними. " +
                           "Форма аудиту — на сторінці «Канали» → «Як підключити YouTube».",
                    AutoSize = true, ForeColor = Theme.Tertiary, Font = Theme.F(8.75F), MaximumSize = new Size(800, 0), Margin = new Padding(3, 10, 3, 3)
                };

                var stepsCard = steps;
                var keyCard = CardOf(Stack(Section("Ваш клієнт Google"), 0, fields, 0, status, 0, Row(pickJson), 0));
                keyCard.Margin = new Padding(0);
                var body = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 10, 0) };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                foreach (var c in new Control[] { new Label { Text = "Підключення YouTube", AutoSize = true, Font = Theme.Display(17F), Margin = new Padding(2, 0, 3, 6) }, intro, emailRow, stepsCard, keyCard, auditNote })
                {
                    body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    if (c is Card || c is TableLayoutPanel) c.Dock = DockStyle.Fill;
                    body.Controls.Add(c, 0, body.RowStyles.Count - 1);
                }
                var scroll = new ScrollHost(body) { Dock = DockStyle.Fill };
                var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0), BackColor = Theme.Bg };
                bar.Controls.Add(done);
                bar.Controls.Add(Btn("Скасувати", (s, e) => f.Close(), ButtonKind.Plain));
                var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(26, 22, 26, 20), BackColor = Theme.Bg };
                host.Controls.Add(scroll);
                host.Controls.Add(bar);
                f.Controls.Add(host);

                // автоматичний пошук: буфер обміну і нові client_secret_*.json у «Завантаженнях»
                var started = DateTime.Now.AddMinutes(-30);
                var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string lastClip = null;
                var timer = new Timer { Interval = 800 };
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            var t = Clipboard.GetText();
                            if (t != lastClip)
                            {
                                lastClip = t;
                                var m1 = ClientIdRx.Match(t); var m2 = SecretRx.Match(t);
                                if (m1.Success && idBox.Text.Trim() != m1.Value) idBox.Text = m1.Value;
                                if (m2.Success && secBox.Text.Trim() != m2.Value) secBox.Text = m2.Value;
                            }
                        }
                    }
                    catch { }
                    try
                    {
                        if (Directory.Exists(downloads))
                        {
                            var j = new DirectoryInfo(downloads).GetFiles("client_secret*.json")
                                .Where(x => x.LastWriteTime > started).OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
                            if (j != null) { started = j.LastWriteTime.AddSeconds(1); takeJson(j.FullName); }
                        }
                    }
                    catch { }
                };
                f.Shown += (s, e) => { check(); timer.Start(); };
                f.FormClosed += (s, e) => { timer.Dispose(); try { settings.Save(); } catch { } };

                bool ok = false;
                done.Click += (s, e) =>
                {
                    settings.ClientId = idBox.Text.Trim();
                    settings.ClientSecret = secBox.Text.Trim();
                    settings.GoogleEmail = emailBox.Text.Trim();
                    try { settings.Save(); } catch { }
                    if (clientIdBox != null) { clientIdBox.Text = settings.ClientId; clientSecretBox.Text = settings.ClientSecret; }
                    Log("Google Client ID збережено.");
                    ok = true;
                    f.Close();
                };
                f.ShowDialog(this);
                return ok;
            }
        }
    }

    // Номер кроку в кружечку (після натискання — лаймова галочка)
    class StepNum : Control
    {
        readonly int n;
        public bool Done;

        public StepNum(int n)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.n = n;
            AutoSize = true;
            Margin = new Padding(0, 2, 0, 2);
        }

        public override Size GetPreferredSize(Size proposedSize) { int s = (int)(Font.Height * 1.9f); return new Size(s, s); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
            using (var b = new SolidBrush(Done ? Theme.Accent : Theme.Fill)) g.FillEllipse(b, r);
            if (Done)
                using (var p = new Pen(Color.FromArgb(14, 14, 16), 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                    g.DrawLines(p, new[] { new PointF(r.X + r.Width * 0.28f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.44f, r.Y + r.Height * 0.68f), new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.34f) });
            else TextRenderer.DrawText(g, n.ToString(), Theme.Semi(Font.SizeInPoints), Rectangle.Round(r), Theme.Text, Theme.Center);
        }
    }
}
