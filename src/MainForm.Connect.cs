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
                AutoScaleMode = AutoScaleMode.Dpi, AutoScaleDimensions = new SizeF(96, 96), Size = new Size(900, 820), MinimumSize = new Size(760, 640)
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

                // кроки
                var steps = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Margin = new Padding(0) };
                steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var nums = new List<StepNum>();
                Action<int, string, string, string, string> step = (n, title, desc, button, url) =>
                {
                    var num = new StepNum(n);
                    nums.Add(num);
                    var b = Btn(button, (s, e) => { OpenUrl(url); num.Done = true; num.Invalidate(); }, n == 6 ? ButtonKind.Primary : ButtonKind.Secondary, Glyphs.Open);
                    var row = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0, 4, 0, 4), Dock = DockStyle.Fill };
                    row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    num.Anchor = AnchorStyles.Left;
                    row.Controls.Add(num, 0, 0);
                    var txt = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(10, 0, 10, 0) };
                    txt.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.Semi(10F), ForeColor = Theme.Text, Margin = new Padding(0) });
                    txt.Controls.Add(new Label { Text = desc, AutoSize = true, ForeColor = Theme.Tertiary, Font = Theme.F(8.75F), MaximumSize = new Size(520, 0), Margin = new Padding(0, 2, 0, 0) });
                    row.Controls.Add(txt, 1, 0);
                    b.Anchor = AnchorStyles.Right;
                    row.Controls.Add(b, 2, 0);
                    steps.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    steps.Controls.Add(row, 0, steps.RowStyles.Count - 1);
                };
                step(1, "Створіть проєкт", "Назва — будь-яка, наприклад «AutoShorts». Потім переконайтеся, що він вибраний угорі консолі.", "Новий проєкт", "https://console.cloud.google.com/projectcreate");
                step(2, "Увімкніть YouTube Data API v3", "Кнопка «Enable». Потрібно для завантаження шортсів.", "Відкрити", "https://console.cloud.google.com/apis/library/youtube.googleapis.com");
                step(3, "Увімкніть YouTube Analytics API", "Кнопка «Enable». Потрібно, щоб рахувати найкращий час публікації.", "Відкрити", "https://console.cloud.google.com/apis/library/youtubeanalytics.googleapis.com");
                step(4, "Налаштуйте екран входу", "«Get started»: назва — AutoShorts, e-mail — ваш, Audience — External, ще раз e-mail, погодитися → Create.", "Відкрити", "https://console.cloud.google.com/auth/overview/create");
                step(5, "Опублікуйте застосунок", "«Publish app» → Confirm. Тоді вхід не злітатиме кожні 7 днів. (Або додайте свої акаунти в Test users.)", "Відкрити", "https://console.cloud.google.com/auth/audience");
                step(6, "Створіть клієнт і завантажте JSON", "Application type — Desktop app → Create → у вікні натисніть «Download JSON». Програма підхопить файл сама.", "Створити клієнт", "https://console.cloud.google.com/auth/clients/create");

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

                var stepsCard = CardOf(steps);
                var keyCard = CardOf(Stack(Section("Ваш клієнт Google"), 0, fields, 0, status, 0, Row(pickJson), 0));
                keyCard.Margin = new Padding(0);
                var body = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 10, 0) };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                foreach (var c in new Control[] { new Label { Text = "Підключення YouTube", AutoSize = true, Font = Theme.Display(17F), Margin = new Padding(2, 0, 3, 6) }, intro, stepsCard, keyCard, auditNote })
                {
                    body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    if (c is Card) c.Dock = DockStyle.Fill;
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
                f.FormClosed += (s, e) => timer.Dispose();

                bool ok = false;
                done.Click += (s, e) =>
                {
                    settings.ClientId = idBox.Text.Trim();
                    settings.ClientSecret = secBox.Text.Trim();
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
