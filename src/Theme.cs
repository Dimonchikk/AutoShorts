using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShorts
{
    // Світла тема у стилі Apple: системні кольори macOS/iOS, заокруглені форми, шрифти Segoe UI Variable.
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(245, 245, 247);
        public static readonly Color SidebarBg = Color.FromArgb(236, 236, 240);
        public static readonly Color Border = Color.FromArgb(226, 226, 231);
        public static readonly Color FieldBorder = Color.FromArgb(206, 206, 212);
        public static readonly Color Text = Color.FromArgb(29, 29, 31);
        public static readonly Color Secondary = Color.FromArgb(110, 110, 115);
        public static readonly Color Tertiary = Color.FromArgb(160, 160, 166);
        public static readonly Color Accent = Color.FromArgb(0, 122, 255);
        public static readonly Color AccentHover = Color.FromArgb(0, 108, 235);
        public static readonly Color AccentPressed = Color.FromArgb(0, 92, 205);
        public static readonly Color AccentSoft = Color.FromArgb(222, 236, 255);
        public static readonly Color Fill = Color.FromArgb(232, 232, 237);
        public static readonly Color FillHover = Color.FromArgb(222, 222, 228);
        public static readonly Color FillPressed = Color.FromArgb(208, 208, 215);
        public static readonly Color Green = Color.FromArgb(36, 160, 70);
        public static readonly Color SwitchOn = Color.FromArgb(52, 199, 89);
        public static readonly Color Red = Color.FromArgb(230, 52, 42);
        public static readonly Color Orange = Color.FromArgb(255, 149, 0);
        public static readonly Color Stripe = Color.FromArgb(249, 249, 251);

        static HashSet<string> installed;
        static bool Has(string name)
        {
            if (installed == null)
            {
                installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try { using (var c = new InstalledFontCollection()) foreach (var f in c.Families) installed.Add(f.Name); } catch { }
            }
            return installed.Contains(name);
        }

        static string Pick(params string[] names)
        {
            foreach (var n in names) if (Has(n)) return n;
            return null;
        }

        static string textFamily, semiFamily, displayFamily, iconFamily, monoFamily;
        static bool picked;
        static void PickFamilies()
        {
            if (picked) return;
            picked = true;
            textFamily = Pick("Segoe UI Variable Text", "Segoe UI") ?? SystemFonts.MessageBoxFont.Name;
            semiFamily = Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold");
            displayFamily = Pick("Segoe UI Variable Display Semibold", "Segoe UI Variable Display Semib", "Segoe UI Semibold");
            iconFamily = Pick("Segoe Fluent Icons", "Segoe MDL2 Assets");
            monoFamily = Pick("Cascadia Mono", "Consolas") ?? "Courier New";
        }

        static readonly Dictionary<string, Font> cache = new Dictionary<string, Font>();
        static Font Get(string family, float size, FontStyle style)
        {
            var key = family + "|" + size + "|" + (int)style;
            Font f;
            if (!cache.TryGetValue(key, out f)) { f = new Font(family, size, style); cache[key] = f; }
            return f;
        }

        public static Font F(float size) { PickFamilies(); return Get(textFamily, size, FontStyle.Regular); }
        public static Font Semi(float size) { PickFamilies(); return semiFamily != null ? Get(semiFamily, size, FontStyle.Regular) : Get(textFamily, size, FontStyle.Bold); }
        public static Font Display(float size) { PickFamilies(); return displayFamily != null ? Get(displayFamily, size, FontStyle.Regular) : Get(textFamily, size, FontStyle.Bold); }
        public static Font Mono(float size) { PickFamilies(); return Get(monoFamily, size, FontStyle.Regular); }
        public static Font Icons(float size) { PickFamilies(); return iconFamily == null ? null : Get(iconFamily, size, FontStyle.Regular); }

        static float dpi;
        public static float Dpi
        {
            get
            {
                if (dpi <= 0)
                {
                    try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX / 96f; } catch { dpi = 1f; }
                    if (dpi <= 0) dpi = 1f;
                }
                return dpi;
            }
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Color c, float width, RectangleF r, float radius)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(c, width)) g.DrawPath(pen, p);
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static Color BackOf(Control c)
        {
            for (var p = c.Parent; p != null; p = p.Parent)
                if (p.BackColor.A == 255) return p.BackColor;
            return Bg;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public const TextFormatFlags LeftMid = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                               TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                              TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        // Windows 11: світлий заголовок вікна в колір бічної панелі й заокруглені кути
        public static void StyleWindow(Form f)
        {
            try
            {
                int off = 0, round = 2, cap = ColorRef(SidebarBg), txt = ColorRef(Text), border = ColorRef(Border);
                DwmSetWindowAttribute(f.Handle, 20, ref off, 4);
                DwmSetWindowAttribute(f.Handle, 33, ref round, 4);
                DwmSetWindowAttribute(f.Handle, 35, ref cap, 4);
                DwmSetWindowAttribute(f.Handle, 36, ref txt, 4);
                DwmSetWindowAttribute(f.Handle, 34, ref border, 4);
            }
            catch { }
        }
    }

    static class Glyphs
    {
        public const string Download = "", Video = "", People = "", Chart = "", Settings = "",
            Add = "", Play = "", Stop = "", Refresh = "", Delete = "", Folder = "",
            Upload = "", Help = "", Save = "", Check = "", Clear = "", Calendar = "", Link = "";
    }

    enum ButtonKind { Primary, Secondary, Plain, Danger }

    // Кнопка у стилі macOS: заокруглена, синя (головна) або світло-сіра
    class AButton : Button
    {
        public ButtonKind Kind;
        public string Glyph;
        bool hover, down;

        public AButton(string text, ButtonKind kind)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Text = text;
            Kind = kind;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(3);
        }

        protected override bool ShowFocusCues { get { return false; } }

        Font PaintFont { get { return Kind == ButtonKind.Primary ? Theme.Semi(Font.SizeInPoints) : Font; } }

        int GlyphWidth()
        {
            if (string.IsNullOrEmpty(Glyph) || Theme.Icons(Font.SizeInPoints) == null) return 0;
            return (int)(Font.Height * 0.95f) + (string.IsNullOrEmpty(Text) ? 0 : Font.Height / 3);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int h = (int)Math.Round(Font.Height * 1.85);
            int tw = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, PaintFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            int w = tw + GlyphWidth() + (int)(Font.Height * 1.3);
            return new Size(Math.Max(w, h), h);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Theme.BackOf(this);
            g.Clear(back);
            Theme.Smooth(g);
            Color bg, fg;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    bg = down ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent; fg = Color.White; break;
                case ButtonKind.Plain:
                    bg = down ? Theme.FillHover : hover ? Theme.Fill : Color.Empty; fg = Theme.Accent; break;
                case ButtonKind.Danger:
                    bg = down ? Theme.FillPressed : hover ? Theme.FillHover : Theme.Fill; fg = Theme.Red; break;
                default:
                    bg = down ? Theme.FillPressed : hover ? Theme.FillHover : Theme.Fill; fg = Theme.Text; break;
            }
            if (!Enabled)
            {
                if (bg != Color.Empty) bg = Theme.Mix(bg, back, 0.55f);
                fg = Kind == ButtonKind.Primary ? Color.FromArgb(235, 244, 255) : Theme.Tertiary;
            }
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            if (bg != Color.Empty) Theme.FillRound(g, bg, r, Height * 0.3f);

            var font = PaintFont;
            int gw = GlyphWidth();
            int tw = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(g, Text, font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            int x = (Width - gw - tw) / 2;
            if (gw > 0)
            {
                var ir = new Rectangle(x, 0, (int)(Font.Height * 0.95f), Height);
                TextRenderer.DrawText(g, Glyph, Theme.Icons(Font.SizeInPoints * 0.95f), ir, fg, Theme.Center);
                x += gw;
            }
            if (tw > 0) TextRenderer.DrawText(g, Text, font, new Rectangle(x, 0, tw + 2, Height), fg, Theme.LeftMid);
        }
    }

    // Перемикач як в iOS
    class Toggle : CheckBox
    {
        float pos;
        readonly Timer anim = new Timer { Interval = 15 };

        public Toggle(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            AutoSize = true;
            Cursor = Cursors.Hand;
            Margin = new Padding(3, 5, 3, 5);
            anim.Tick += (s, e) =>
            {
                float target = Checked ? 1 : 0;
                pos += (target - pos) * 0.35f;
                if (Math.Abs(target - pos) < 0.02f) { pos = target; anim.Stop(); }
                Invalidate();
            };
        }

        protected override bool ShowFocusCues { get { return false; } }

        int SwW { get { return (int)(Font.Height * 2.0f); } }
        int SwH { get { return (int)(Font.Height * 1.2f); } }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            if (!IsHandleCreated || !Visible) { pos = Checked ? 1 : 0; Invalidate(); }
            else anim.Start();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int tw = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + Font.Height / 2 + 2;
            return new Size(SwW + tw + 2, Math.Max(SwH, Font.Height) + 4);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            int w = SwW, h = SwH, y = (Height - h) / 2;
            var off = Color.FromArgb(226, 226, 232);
            var track = Theme.Mix(off, Theme.SwitchOn, pos);
            if (!Enabled) track = Theme.Mix(track, Theme.BackOf(this), 0.5f);
            Theme.FillRound(g, track, new RectangleF(1, y, w - 1, h), h / 2f);
            float k = h - 4;
            float kx = 3 + pos * (w - h);
            using (var sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(sh, kx, y + 2.6f, k, k);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, y + 2, k, k);
            if (!string.IsNullOrEmpty(Text))
            {
                var tr = new Rectangle(w + Font.Height / 2, 0, Width - w - Font.Height / 2, Height);
                TextRenderer.DrawText(g, Text, Font, tr, Enabled ? Theme.Text : Theme.Tertiary, Theme.LeftMid);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) anim.Dispose();
            base.Dispose(disposing);
        }
    }

    // Біла картка з заокругленими кутами
    class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16, 14, 16, 14);
            Margin = new Padding(0, 0, 0, 14);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Font.Height * 0.75f;
            Theme.FillRound(g, Color.White, r, rad);
            Theme.StrokeRound(g, Theme.Border, 1f, r, rad);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (Controls.Count != 1) return base.GetPreferredSize(proposedSize);
            int pw = proposedSize.Width > 0 && proposedSize.Width < 20000 ? proposedSize.Width : 0;
            var inner = Controls[0].GetPreferredSize(new Size(Math.Max(0, pw - Padding.Horizontal), 0));
            return new Size(pw > 0 ? pw : inner.Width + Padding.Horizontal, inner.Height + Padding.Vertical);
        }
    }

    // Поле вводу із заокругленою рамкою, синім фокусом і підказкою
    class Field : Panel
    {
        public readonly TextBox Box;
        readonly Label hint;
        readonly int fixedHeight;
        bool focused;

        public Field(TextBox tb, int height, string placeholder)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Box = tb;
            fixedHeight = height > 0 ? (int)(height * Theme.Dpi) : 0;
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = Color.White;
            tb.Dock = DockStyle.Fill;
            Controls.Add(tb);
            Cursor = Cursors.IBeam;
            Margin = new Padding(3, 3, 3, 6);
            if (!string.IsNullOrEmpty(placeholder))
            {
                hint = new Label { Text = placeholder, AutoSize = true, ForeColor = Theme.Tertiary, BackColor = Color.Transparent, Cursor = Cursors.IBeam };
                hint.Click += (s, e) => tb.Focus();
                Controls.Add(hint);
                hint.BringToFront();
                tb.TextChanged += (s, e) => hint.Visible = tb.TextLength == 0;
            }
            tb.GotFocus += (s, e) => { focused = true; Invalidate(); };
            tb.LostFocus += (s, e) => { focused = false; Invalidate(); };
            Click += (s, e) => tb.Focus();
            UpdatePadding();
        }

        void UpdatePadding()
        {
            int h = Font.Height;
            Padding = new Padding(h / 2 + 2, h / 3 + 1, h / 3, h / 3 + 1);
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdatePadding(); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (hint != null) { hint.Font = Box.Font; hint.Location = new Point(Box.Left + 3, Box.Top); hint.Visible = Box.TextLength == 0; }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int h = fixedHeight > 0 ? fixedHeight : Box.PreferredHeight;
            int w = proposedSize.Width > 0 && proposedSize.Width < 20000 ? proposedSize.Width : 200;
            return new Size(w, h + Padding.Vertical);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            float rad = Font.Height * 0.45f;
            if (focused)
            {
                Theme.FillRound(g, Color.FromArgb(70, Theme.Accent), new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), rad + 2);
                Theme.FillRound(g, Color.White, new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f), rad);
                Theme.StrokeRound(g, Theme.Accent, 1.2f, new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f), rad);
            }
            else
            {
                var r = new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f);
                Theme.FillRound(g, Color.White, r, rad);
                Theme.StrokeRound(g, Theme.FieldBorder, 1f, r, rad);
            }
        }
    }

    // Список із м'якими рядками, кружечками-галочками, крапками статусу і смужкою прогресу
    class AListView : ListView
    {
        public int StatusColumn = -1, ProgressColumn = -1, MutedColumn = -1;
        public string EmptyText;
        int col0Indent = 10;
        bool filling;

        public AListView()
        {
            OwnerDraw = true;
            View = View.Details;
            FullRowSelect = true;
            HideSelection = false;
            MultiSelect = false;
            BorderStyle = BorderStyle.None;
            BackColor = Color.White;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { Theme.SetWindowTheme(Handle, "Explorer", null); } catch { }
            UpdateRowHeight();
            FillLast();
        }

        void UpdateRowHeight()
        {
            var old = SmallImageList;
            SmallImageList = new ImageList { ImageSize = new Size(1, Math.Min(255, (int)(Font.Height * 2.1f))) };
            if (old != null) old.Dispose();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (IsHandleCreated) UpdateRowHeight();
        }

        void FillLast()
        {
            if (filling || Columns.Count == 0 || !IsHandleCreated) return;
            filling = true;
            try
            {
                int others = 0;
                for (int i = 0; i < Columns.Count - 1; i++) others += Columns[i].Width;
                int w = ClientSize.Width - others - 1;
                int min = (int)(70 * Theme.Dpi);
                var last = Columns[Columns.Count - 1];
                if (last.Width != Math.Max(min, w)) last.Width = Math.Max(min, w);
            }
            finally { filling = false; }
        }

        protected override void OnClientSizeChanged(EventArgs e) { base.OnClientSizeChanged(e); FillLast(); Invalidate(); }
        protected override void OnColumnWidthChanged(ColumnWidthChangedEventArgs e) { base.OnColumnWidthChanged(e); if (!filling) FillLast(); }

        int Pad { get { return Font.Height / 2 + 2; } }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, e.Bounds);
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            int pad = e.ColumnIndex == 0 ? col0Indent : Pad;
            var r = new Rectangle(e.Bounds.X + pad, e.Bounds.Y, Math.Max(0, e.Bounds.Width - pad - 4), e.Bounds.Height);
            TextRenderer.DrawText(g, e.Header.Text, Theme.Semi(Font.SizeInPoints * 0.88f), r, Theme.Secondary, Theme.LeftMid);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e) { e.DrawDefault = false; }

        int TotalWidth()
        {
            int w = 0;
            foreach (ColumnHeader c in Columns) w += c.Width;
            return w;
        }

        void PaintRow(Graphics g, ListViewItem item)
        {
            var b = item.Bounds;
            var row = new Rectangle(b.X, b.Y, Math.Max(ClientSize.Width - b.X, TotalWidth()), b.Height);
            using (var white = new SolidBrush(Color.White)) g.FillRectangle(white, row);
            Color bg = item.Selected ? Theme.AccentSoft : (item.Index % 2 == 1 ? Theme.Stripe : Color.White);
            if (bg != Color.White)
            {
                Theme.Smooth(g);
                Theme.FillRound(g, bg, new RectangleF(row.X + 4, row.Y + 1, row.Width - 8, row.Height - 2), Font.Height * 0.4f);
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Default;
            }
        }

        void DrawCheck(Graphics g, Rectangle area, bool on)
        {
            float d = Math.Min(Font.Height * 1.05f, Math.Min(area.Width, area.Height) - 2);
            if (d < 8) d = Math.Min(area.Height - 2, Font.Height);
            var r = new RectangleF(area.X + (area.Width - d) / 2f, area.Y + (area.Height - d) / 2f, d, d);
            Theme.Smooth(g);
            if (on)
            {
                using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, r);
                using (var p = new Pen(Color.White, Math.Max(1.6f, d / 9f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(p, new[]
                    {
                        new PointF(r.X + d * 0.28f, r.Y + d * 0.52f),
                        new PointF(r.X + d * 0.44f, r.Y + d * 0.68f),
                        new PointF(r.X + d * 0.73f, r.Y + d * 0.36f)
                    });
            }
            else
            {
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, r);
                using (var p = new Pen(Color.FromArgb(190, 190, 197), 1.4f)) g.DrawEllipse(p, r);
            }
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Default;
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            var g = e.Graphics;
            var item = e.Item;
            var cell = e.Bounds;
            if (e.ColumnIndex == 0 && Columns.Count > 0) cell.Width = Columns[0].Width;

            var st = g.Save();
            g.SetClip(cell);
            PaintRow(g, item);
            g.Restore(st);

            int x;
            if (e.ColumnIndex == 0)
            {
                var icon = item.GetBounds(ItemBoundsPortion.Icon);
                if (CheckBoxes)
                {
                    var sr = new Rectangle(item.Bounds.X, item.Bounds.Y, Math.Max(icon.X - item.Bounds.X, Font.Height), item.Bounds.Height);
                    DrawCheck(g, sr, item.Checked);
                    x = Math.Max(icon.Right, sr.Right) + Font.Height / 3;
                }
                else x = icon.Right + Pad - 2;
                col0Indent = Math.Max(4, x - cell.X);
            }
            else x = cell.X + Pad;

            var text = e.SubItem != null ? e.SubItem.Text : "";
            int right = cell.Right - 6;

            if (e.ColumnIndex == ProgressColumn && !string.IsNullOrEmpty(text))
            {
                int pct;
                int.TryParse(text.TrimEnd('%', ' '), out pct);
                pct = Math.Max(0, Math.Min(100, pct));
                var font = Theme.F(Font.SizeInPoints * 0.9f);
                int tw = TextRenderer.MeasureText(g, "100%", font, Size.Empty, TextFormatFlags.NoPadding).Width;
                int bw = Math.Max(10, right - x - tw - 6);
                float bh = Math.Max(4, Font.Height / 3.2f);
                var bar = new RectangleF(x, cell.Y + (cell.Height - bh) / 2f, bw, bh);
                Theme.Smooth(g);
                Theme.FillRound(g, Color.FromArgb(229, 229, 234), bar, bh / 2);
                if (pct > 0) Theme.FillRound(g, pct >= 100 ? Theme.SwitchOn : Theme.Accent, new RectangleF(bar.X, bar.Y, Math.Max(bh, bw * pct / 100f), bh), bh / 2);
                g.SmoothingMode = SmoothingMode.None;
                TextRenderer.DrawText(g, pct + "%", font, new Rectangle(x + bw + 6, cell.Y, tw + 2, cell.Height), Theme.Secondary, Theme.LeftMid);
                return;
            }

            if (e.ColumnIndex == StatusColumn && !string.IsNullOrEmpty(text))
            {
                var dotC = item.ForeColor;
                if (dotC == Theme.Text || dotC == SystemColors.WindowText) dotC = Theme.Tertiary;
                float d = Math.Max(6, Font.Height * 0.42f);
                Theme.Smooth(g);
                using (var b = new SolidBrush(dotC)) g.FillEllipse(b, x, cell.Y + (cell.Height - d) / 2f, d, d);
                g.SmoothingMode = SmoothingMode.None;
                x += (int)d + Font.Height / 3;
                var tc = item.ForeColor == Theme.Red ? Theme.Red : Theme.Text;
                TextRenderer.DrawText(g, text, Font, new Rectangle(x, cell.Y, Math.Max(0, right - x), cell.Height), tc, Theme.LeftMid);
                return;
            }

            var color = e.ColumnIndex == MutedColumn ? Theme.Secondary : Theme.Text;
            TextRenderer.DrawText(g, text, Font, new Rectangle(x, cell.Y, Math.Max(0, right - x), cell.Height), color, Theme.LeftMid);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F && Items.Count == 0 && !string.IsNullOrEmpty(EmptyText) && IsHandleCreated)
            {
                try
                {
                    using (var g = CreateGraphics())
                    {
                        var r = new Rectangle(20, Font.Height * 2, Math.Max(0, ClientSize.Width - 40), Math.Max(0, ClientSize.Height - Font.Height * 2));
                        TextRenderer.DrawText(g, EmptyText, Theme.F(Font.SizeInPoints * 1.05f), r, Theme.Tertiary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    }
                }
                catch { }
            }
        }
    }

    // Бічна панель як у Налаштуваннях macOS
    class SideNav : Control
    {
        public class Item
        {
            public string Glyph, Text, Badge;
            public Item(string glyph, string text) { Glyph = glyph; Text = text; }
        }

        public readonly List<Item> Items = new List<Item>();
        public string Title = "AutoShorts", Subtitle, Status;
        public bool Busy;
        public event EventHandler SelectedChanged;
        int selected, hover = -1;

        public SideNav()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.SidebarBg;
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < 0 || value >= Items.Count || value == selected) return;
                selected = value;
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        int ItemH { get { return (int)(Font.Height * 2.1f); } }
        int Top0 { get { return (int)(Font.Height * 5.4f); } }

        Rectangle ItemRect(int i)
        {
            int m = Font.Height * 2 / 3;
            return new Rectangle(m, Top0 + i * (ItemH + 3), Width - 2 * m - 1, ItemH);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Items.Count; i++) if (ItemRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0 && e.Button == MouseButtons.Left) SelectedIndex = h;
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
            Theme.Smooth(g);

            int fh = Font.Height;
            int m = fh * 2 / 3;
            // значок програми
            float s = fh * 2.3f;
            var ir = new RectangleF(m + 2, fh * 1.3f, s, s);
            using (var path = Theme.Round(ir, s * 0.26f))
            using (var br = new LinearGradientBrush(ir, Color.FromArgb(255, 69, 94), Color.FromArgb(255, 149, 0), 45f))
                g.FillPath(br, path);
            var icons = Theme.Icons(Font.SizeInPoints * 1.15f);
            if (icons != null) TextRenderer.DrawText(g, Glyphs.Play, icons, Rectangle.Round(ir), Color.White, Theme.Center);
            int tx = (int)(ir.Right + fh * 0.6f);
            TextRenderer.DrawText(g, Title, Theme.Display(Font.SizeInPoints * 1.3f), new Rectangle(tx, (int)ir.Y - 2, Width - tx - 4, (int)(s * 0.6f)), Theme.Text, Theme.LeftMid);
            if (!string.IsNullOrEmpty(Subtitle))
                TextRenderer.DrawText(g, Subtitle, Theme.F(Font.SizeInPoints * 0.85f), new Rectangle(tx, (int)(ir.Y + s * 0.52f), Width - tx - 4, (int)(s * 0.48f)), Theme.Secondary, Theme.LeftMid);

            var iconFont = Theme.Icons(Font.SizeInPoints * 1.02f);
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                var r = ItemRect(i);
                bool sel = i == selected;
                if (sel) Theme.FillRound(g, Theme.Accent, r, fh * 0.45f);
                else if (i == hover) Theme.FillRound(g, Color.FromArgb(222, 222, 228), r, fh * 0.45f);
                int x = r.X + fh * 2 / 3;
                if (iconFont != null && it.Glyph != null)
                {
                    TextRenderer.DrawText(g, it.Glyph, iconFont, new Rectangle(x, r.Y, fh + 4, r.Height), sel ? Color.White : Theme.Accent, Theme.Center);
                    x += fh + 4 + fh / 2;
                }
                int bw = 0;
                if (!string.IsNullOrEmpty(it.Badge))
                {
                    var bf = Theme.Semi(Font.SizeInPoints * 0.85f);
                    bw = TextRenderer.MeasureText(g, it.Badge, bf, Size.Empty, TextFormatFlags.NoPadding).Width + fh * 2 / 3;
                    var brc = new RectangleF(r.Right - bw - fh / 2, r.Y + (r.Height - fh * 1.15f) / 2, bw, fh * 1.15f);
                    Theme.FillRound(g, sel ? Color.FromArgb(60, 255, 255, 255) : Color.FromArgb(214, 214, 220), brc, brc.Height / 2);
                    TextRenderer.DrawText(g, it.Badge, bf, Rectangle.Round(brc), sel ? Color.White : Theme.Secondary, Theme.Center);
                }
                TextRenderer.DrawText(g, it.Text, sel ? Theme.Semi(Font.SizeInPoints) : Font,
                    new Rectangle(x, r.Y, Math.Max(0, r.Right - x - bw - fh), r.Height), sel ? Color.White : Theme.Text, Theme.LeftMid);
            }

            if (!string.IsNullOrEmpty(Status))
            {
                int y = Height - fh * 3;
                float d = fh * 0.5f;
                using (var b = new SolidBrush(Busy ? Theme.Accent : Theme.SwitchOn)) g.FillEllipse(b, m + 6, y + (fh * 1.4f - d) / 2, d, d);
                TextRenderer.DrawText(g, Status, Theme.F(Font.SizeInPoints * 0.9f), new Rectangle((int)(m + 6 + d + fh / 2), y, Width - m * 2 - fh * 2, (int)(fh * 1.4f)), Theme.Secondary, Theme.LeftMid);
            }
        }
    }

    // Сторінка з великим заголовком
    class Page : Panel
    {
        public Page(string title, string subtitle, Control body)
        {
            BackColor = Theme.Bg;
            Dock = DockStyle.Fill;
            Padding = new Padding(30, 22, 30, 18);
            var head = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, 0, 0, 14) };
            head.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.Display(21f), ForeColor = Theme.Text, Margin = new Padding(0) });
            if (!string.IsNullOrEmpty(subtitle))
                head.Controls.Add(new Label { Text = subtitle, AutoSize = true, Font = Theme.F(10.5f), ForeColor = Theme.Secondary, Margin = new Padding(1, 2, 0, 0) });
            body.Dock = DockStyle.Fill;
            Controls.Add(body);
            Controls.Add(head);
        }
    }

    // Обкладинка із заокругленими кутами та кнопкою «відтворити»
    class CoverView : PictureBox
    {
        public CoverView()
        {
            SizeMode = PictureBoxSizeMode.Zoom;
            BackColor = Color.FromArgb(28, 28, 30);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
            var g = pe.Graphics;
            Theme.Smooth(g);
            if (Image == null)
                TextRenderer.DrawText(g, "Оберіть кліп у списку", Font, ClientRectangle, Color.FromArgb(150, 150, 156), Theme.Center);
            else
            {
                float d = Font.Height * 2.6f;
                var c = new RectangleF((Width - d) / 2, (Height - d) / 2, d, d);
                using (var b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillEllipse(b, c);
                using (var b = new SolidBrush(Color.White))
                    g.FillPolygon(b, new[]
                    {
                        new PointF(c.X + d * 0.40f, c.Y + d * 0.30f),
                        new PointF(c.X + d * 0.40f, c.Y + d * 0.70f),
                        new PointF(c.X + d * 0.72f, c.Y + d * 0.50f)
                    });
            }
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new Rectangle(-1, -1, Width + 2, Height + 2));
                using (var inner = Theme.Round(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), Font.Height * 0.8f)) path.AddPath(inner, false);
                path.FillMode = FillMode.Alternate;
                using (var b = new SolidBrush(Theme.BackOf(this))) g.FillPath(b, path);
            }
        }
    }

    // Плитка з одним числом (звіт)
    class StatTile : Control
    {
        public string Caption, Value = "—";

        public StatTile(string caption)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Caption = caption;
            Size = new Size(178, 84);
            Margin = new Padding(0, 0, 14, 14);
        }

        public void Set(string v) { Value = string.IsNullOrEmpty(v) ? "—" : v; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Font.Height * 0.75f;
            Theme.FillRound(g, Color.White, r, rad);
            Theme.StrokeRound(g, Theme.Border, 1f, r, rad);
            int p = Font.Height;
            TextRenderer.DrawText(g, Caption, Theme.F(Font.SizeInPoints * 0.9f), new Rectangle(p, p * 2 / 3, Width - p * 2, p + 4), Theme.Secondary, Theme.LeftMid);
            TextRenderer.DrawText(g, Value, Theme.Display(Font.SizeInPoints * 1.9f), new Rectangle(p, p * 2 / 3 + p + 2, Width - p * 2, Height - p * 2 - 4), Theme.Text, Theme.LeftMid);
        }
    }
}
