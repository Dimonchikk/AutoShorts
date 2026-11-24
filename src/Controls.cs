using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace AutoShorts
{
    enum ButtonKind { Primary, Accent, Secondary, Plain, Danger, Chip }

    // Кнопка: біла головна, лаймова, графітова, прозора, «чип» з рамкою
    class AButton : Button
    {
        public ButtonKind Kind;
        public string Glyph;
        public float HeightFactor = 1.85f;
        public bool Checked;
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

        Font PaintFont { get { return Kind == ButtonKind.Primary || Kind == ButtonKind.Accent ? Theme.Semi(Font.SizeInPoints) : Font; } }

        int GlyphWidth()
        {
            if (string.IsNullOrEmpty(Glyph) || Theme.Icons(Font.SizeInPoints) == null) return 0;
            return (int)(Font.Height * 0.95f) + (string.IsNullOrEmpty(Text) ? 0 : Font.Height / 3);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int h = (int)Math.Round(Font.Height * HeightFactor);
            int tw = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, PaintFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            int w = tw + GlyphWidth() + (int)(Font.Height * (Kind == ButtonKind.Chip ? 1.6 : 1.3) * Math.Max(1, HeightFactor / 1.85f));
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
            Color bg, fg, border = Color.Empty;
            var dark = Color.FromArgb(12, 12, 14);
            switch (Kind)
            {
                case ButtonKind.Primary:
                    bg = down ? Theme.PrimaryPressed : hover ? Theme.PrimaryHover : Theme.Primary; fg = dark; break;
                case ButtonKind.Accent:
                    bg = down ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent; fg = dark; break;
                case ButtonKind.Plain:
                    bg = down ? Theme.FillPressed : hover ? Theme.Fill : Color.Empty; fg = Theme.Text; break;
                case ButtonKind.Danger:
                    bg = down ? Theme.FillPressed : hover ? Theme.FillHover : Theme.Fill; fg = Theme.Red; break;
                case ButtonKind.Chip:
                    bg = down ? Theme.FillPressed : hover ? Theme.Fill : Checked ? Color.FromArgb(30, 36, 24) : Color.Empty;
                    fg = Checked ? Theme.Accent : Theme.Secondary; border = Checked ? Color.FromArgb(110, Theme.Accent) : Theme.FieldBorder; break;
                default:
                    bg = down ? Theme.FillPressed : hover ? Theme.FillHover : Theme.Fill; fg = Theme.Text; break;
            }
            if (!Enabled)
            {
                if (bg != Color.Empty) bg = Theme.Mix(bg, back, 0.55f);
                fg = Kind == ButtonKind.Primary || Kind == ButtonKind.Accent ? Color.FromArgb(90, 90, 96) : Theme.Tertiary;
            }
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Kind == ButtonKind.Chip ? Height / 2f : Height * 0.3f;
            if (bg != Color.Empty) Theme.FillRound(g, bg, r, rad);
            if (border != Color.Empty) Theme.StrokeRound(g, border, 1f, r, rad);

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

    // Перемикач
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
            var back = Theme.BackOf(this);
            g.Clear(back);
            Theme.Smooth(g);
            int w = SwW, h = SwH, y = (Height - h) / 2;
            var track = Theme.Mix(Color.FromArgb(58, 58, 64), Theme.Accent, pos);
            if (!Enabled) track = Theme.Mix(track, back, 0.5f);
            Theme.FillRound(g, track, new RectangleF(1, y, w - 1, h), h / 2f);
            float k = h - 4;
            float kx = 3 + pos * (w - h);
            using (var b = new SolidBrush(Theme.Mix(Color.FromArgb(225, 225, 230), Color.FromArgb(14, 14, 16), pos))) g.FillEllipse(b, kx, y + 2, k, k);
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

    // Графітова картка із заокругленими кутами
    class Card : Panel
    {
        public Color Fill = Theme.Card;

        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(18, 16, 18, 16);
            Margin = new Padding(0, 0, 0, 14);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Font.Height * 0.9f;
            Theme.FillRound(g, Fill, r, rad);
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

    // Поле вводу
    class Field : Panel
    {
        public readonly TextBox Box;
        public string Glyph;
        readonly Label hint;
        readonly int fixedHeight;
        bool focused;

        public Field(TextBox tb, int height, string placeholder)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.FieldBg;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Box = tb;
            fixedHeight = height > 0 ? (int)(height * Theme.Dpi) : 0;
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = Theme.FieldBg;
            if (tb.ForeColor == SystemColors.WindowText || tb.ForeColor == Control.DefaultForeColor) tb.ForeColor = Theme.Text;
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

        public float PadFactor = 1f;

        public void UpdatePadding()
        {
            int h = (int)(Font.Height * PadFactor);
            int gl = string.IsNullOrEmpty(Glyph) || Theme.Icons(Font.SizeInPoints) == null ? 0 : Font.Height + Font.Height / 2;
            Padding = new Padding(h / 2 + 4 + gl, h / 3 + 2, h / 3, h / 3 + 2);
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
            float rad = Math.Min(Font.Height * 0.6f, Height / 2f - 3);
            var r = new RectangleF(2.5f, 2.5f, Width - 5.5f, Height - 5.5f);
            if (focused) Theme.FillRound(g, Color.FromArgb(60, Theme.Accent), new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), rad + 2);
            Theme.FillRound(g, Theme.FieldBg, r, rad);
            Theme.StrokeRound(g, focused ? Theme.Accent : Theme.FieldBorder, 1f, r, rad);
            if (!string.IsNullOrEmpty(Glyph) && Theme.Icons(Font.SizeInPoints) != null)
                TextRenderer.DrawText(g, Glyph, Theme.Icons(Font.SizeInPoints), new Rectangle(Font.Height / 2 + 4, 0, Font.Height + 4, Height), Theme.Secondary, Theme.Center);
        }
    }

    // Меню у темних тонах
    class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.FieldBorder; } }
        public override Color MenuItemBorder { get { return Theme.FillHover; } }
        public override Color MenuItemSelected { get { return Theme.FillHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.FillHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.FillHover; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
        public override Color CheckBackground { get { return Theme.Card; } }
        public override Color CheckSelectedBackground { get { return Theme.FillHover; } }
        public override Color CheckPressedBackground { get { return Theme.FillHover; } }
    }

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Tertiary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var r = e.ImageRectangle;
            using (var p = new Pen(Theme.Accent, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(p, new[] { new PointF(r.X + r.Width * 0.2f, r.Y + r.Height * 0.52f), new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.74f), new PointF(r.X + r.Width * 0.82f, r.Y + r.Height * 0.3f) });
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.Secondary;
            base.OnRenderArrow(e);
        }

        public static ContextMenuStrip Menu(Font font)
        {
            return new ContextMenuStrip { Renderer = new DarkRenderer(), BackColor = Theme.Card, ForeColor = Theme.Text, Font = font, ShowImageMargin = true };
        }
    }

    // Випадний список-чип: «Довжина: 30–60 с ▾»
    class Dropdown : Control
    {
        public readonly List<object> Items = new List<object>();
        public string Prefix, Glyph;
        public Func<object, Image> ItemImage;
        public bool Chip = true;
        int index = -1;
        bool hover;
        public event EventHandler SelectedIndexChanged;

        public Dropdown()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            AutoSize = true;
            Margin = new Padding(3);
        }

        public int SelectedIndex
        {
            get { return index; }
            set
            {
                if (value < -1 || value >= Items.Count) value = -1;
                bool changed = value != index;
                index = value;
                Relayout();
                if (changed && SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return index >= 0 && index < Items.Count ? Items[index] : null; }
            set { SelectedIndex = value == null ? -1 : Items.FindIndex(x => Equals(x, value)); }
        }

        public void Relayout()
        {
            Invalidate();
            if (AutoSize) Size = GetPreferredSize(Size.Empty);
        }

        string Pre { get { return string.IsNullOrEmpty(Prefix) ? "" : Prefix; } }
        int GlyphW { get { return string.IsNullOrEmpty(Glyph) || Theme.Icons(Font.SizeInPoints) == null ? 0 : Font.Height + Font.Height / 3; } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int h = (int)Math.Round(Font.Height * 1.85);
            int maxw = 0;
            var all = Items.Count == 0 ? new List<object> { "—" } : Items;
            foreach (var it in all) maxw = Math.Max(maxw, TextRenderer.MeasureText(Convert.ToString(it), Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width);
            int pw = Pre.Length == 0 ? 0 : TextRenderer.MeasureText(Pre, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + Font.Height / 3;
            return new Size(Font.Height + GlyphW + pw + maxw + Font.Height * 2, h);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled || Items.Count == 0) return;
            var menu = DarkRenderer.Menu(Font);
            for (int i = 0; i < Items.Count; i++)
            {
                int k = i;
                var mi = new ToolStripMenuItem(Convert.ToString(Items[i])) { Checked = i == index };
                if (ItemImage != null)
                {
                    var img = ItemImage(Items[i]);
                    if (img != null) { mi.Image = img; mi.ImageScaling = ToolStripItemImageScaling.None; }
                }
                mi.Click += (s, a) => SelectedIndex = k;
                menu.Items.Add(mi);
            }
            menu.Closed += (s, a) => BeginInvoke(new Action(() => menu.Dispose()));
            menu.Show(this, new Point(0, Height + 2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Theme.BackOf(this);
            g.Clear(back);
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Chip ? Height / 2f : Height * 0.3f;
            Theme.FillRound(g, hover && Enabled ? Theme.Fill : Theme.FieldBg, r, rad);
            Theme.StrokeRound(g, hover && Enabled ? Theme.Tertiary : Theme.FieldBorder, 1f, r, rad);
            int x = Font.Height * 2 / 3 + 2;
            if (GlyphW > 0)
            {
                TextRenderer.DrawText(g, Glyph, Theme.Icons(Font.SizeInPoints), new Rectangle(x, 0, Font.Height, Height), Theme.Secondary, Theme.Center);
                x += GlyphW;
            }
            if (Pre.Length > 0)
            {
                int pw = TextRenderer.MeasureText(g, Pre, Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                TextRenderer.DrawText(g, Pre, Font, new Rectangle(x, 0, pw + 2, Height), Theme.Secondary, Theme.LeftMid);
                x += pw + Font.Height / 3;
            }
            var t = SelectedItem == null ? "—" : Convert.ToString(SelectedItem);
            int chev = Font.Height;
            TextRenderer.DrawText(g, t, Font, new Rectangle(x, 0, Math.Max(0, Width - x - chev - 6), Height), Enabled ? Theme.Text : Theme.Tertiary, Theme.LeftMid);
            float cx = Width - chev * 0.95f, cy = Height / 2f, d = Font.Height * 0.22f;
            using (var p = new Pen(Theme.Secondary, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(p, new[] { new PointF(cx - d, cy - d / 2), new PointF(cx, cy + d / 2), new PointF(cx + d, cy - d / 2) });
        }
    }

    // Числове поле «− 30 +»
    class Stepper : Control
    {
        readonly TextBox box;
        readonly int logicalWidth;
        decimal val, min, max = 100, inc = 1;
        int decimals;
        int hoverSide;
        readonly Timer repeat = new Timer();
        int repeatDir;
        public event EventHandler ValueChanged;

        public Stepper(int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            logicalWidth = width;
            AutoSize = true;
            Margin = new Padding(3);
            box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.FieldBg, ForeColor = Theme.Text, TextAlign = HorizontalAlignment.Center };
            Controls.Add(box);
            box.Leave += (s, e) => Commit();
            box.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { Commit(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { Value += inc; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down) { Value -= inc; e.SuppressKeyPress = true; }
            };
            box.MouseWheel += (s, e) => Value += e.Delta > 0 ? inc : -inc;
            repeat.Tick += (s, e) => { repeat.Interval = 60; Value += repeatDir * inc; };
            ShowValue();
        }

        public decimal Minimum { get { return min; } set { min = value; if (max < min) max = min; Value = val; } }
        public decimal Maximum { get { return max; } set { max = value; if (min > max) min = max; Value = val; } }
        public decimal Increment { get { return inc; } set { inc = value; } }
        public int DecimalPlaces { get { return decimals; } set { decimals = value; ShowValue(); } }

        public decimal Value
        {
            get { return val; }
            set
            {
                var v = Math.Round(Math.Max(min, Math.Min(max, value)), decimals);
                bool changed = v != val;
                val = v;
                ShowValue();
                if (changed && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        void ShowValue() { if (box != null) box.Text = val.ToString("F" + decimals, CultureInfo.CurrentCulture); }

        void Commit()
        {
            decimal v;
            var t = box.Text.Trim().Replace(',', '.');
            if (decimal.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) Value = v;
            else ShowValue();
        }

        int Btn { get { return Height; } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            return new Size((int)(logicalWidth * Theme.Dpi), (int)Math.Round(Font.Height * 1.85));
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            box.Font = Font;
            box.Bounds = new Rectangle(Btn, (Height - box.PreferredHeight) / 2, Math.Max(10, Width - 2 * Btn), box.PreferredHeight);
        }

        int Side(Point p) { return p.X < Btn ? -1 : p.X > Width - Btn ? 1 : 0; }

        protected override void OnMouseMove(MouseEventArgs e) { int s = Side(e.Location); if (s != hoverSide) { hoverSide = s; Invalidate(); } Cursor = s != 0 ? Cursors.Hand : Cursors.Default; base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverSide = 0; repeat.Stop(); Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int s = Side(e.Location);
            if (s == 0 || e.Button != MouseButtons.Left) return;
            Commit();
            repeatDir = s;
            Value += s * inc;
            repeat.Interval = 400;
            repeat.Start();
        }

        protected override void OnMouseUp(MouseEventArgs e) { repeat.Stop(); base.OnMouseUp(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { Value += e.Delta > 0 ? inc : -inc; base.OnMouseWheel(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Height * 0.3f;
            Theme.FillRound(g, Theme.FieldBg, r, rad);
            if (hoverSide != 0)
            {
                var hr = hoverSide < 0 ? new RectangleF(2, 2, Btn - 4, Height - 4.5f) : new RectangleF(Width - Btn + 2, 2, Btn - 4.5f, Height - 4.5f);
                Theme.FillRound(g, Theme.Fill, hr, rad - 2);
            }
            Theme.StrokeRound(g, Theme.FieldBorder, 1f, r, rad);
            float d = Font.Height * 0.28f, cy = Height / 2f;
            using (var p = new Pen(Theme.Secondary, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = Btn / 2f;
                g.DrawLine(p, cx - d, cy, cx + d, cy);
                cx = Width - Btn / 2f;
                g.DrawLine(p, cx - d, cy, cx + d, cy);
                g.DrawLine(p, cx, cy - d, cx, cy + d);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) repeat.Dispose();
            base.Dispose(disposing);
        }
    }

    // Темний список (канали, звіт)
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
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
            DoubleBuffered = true;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { Theme.SetWindowTheme(Handle, "DarkMode_Explorer", null); } catch { }
            UpdateRowHeight();
            FillLast();
        }

        void UpdateRowHeight()
        {
            var old = SmallImageList;
            SmallImageList = new ImageList { ImageSize = new Size(1, Math.Min(255, (int)(Font.Height * 2.2f))) };
            if (old != null) old.Dispose();
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (IsHandleCreated) UpdateRowHeight(); }

        void FillLast()
        {
            if (filling || Columns.Count == 0 || !IsHandleCreated) return;
            filling = true;
            try
            {
                int others = 0;
                for (int i = 0; i < Columns.Count - 1; i++) others += Columns[i].Width;
                int w = Math.Max((int)(70 * Theme.Dpi), ClientSize.Width - others - 1);
                var last = Columns[Columns.Count - 1];
                if (last.Width != w) last.Width = w;
            }
            finally { filling = false; }
        }

        protected override void OnClientSizeChanged(EventArgs e) { base.OnClientSizeChanged(e); FillLast(); Invalidate(); }
        protected override void OnColumnWidthChanged(ColumnWidthChangedEventArgs e) { base.OnColumnWidthChanged(e); if (!filling) FillLast(); }

        int Pad { get { return Font.Height / 2 + 2; } }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, e.Bounds);
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            int pad = e.ColumnIndex == 0 ? col0Indent : Pad;
            var r = new Rectangle(e.Bounds.X + pad, e.Bounds.Y, Math.Max(0, e.Bounds.Width - pad - 4), e.Bounds.Height);
            TextRenderer.DrawText(g, e.Header.Text, Theme.Semi(Font.SizeInPoints * 0.88f), r, Theme.Secondary, Theme.LeftMid);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e) { e.DrawDefault = false; }

        int TotalWidth() { int w = 0; foreach (ColumnHeader c in Columns) w += c.Width; return w; }

        void PaintRow(Graphics g, ListViewItem item)
        {
            var b = item.Bounds;
            var row = new Rectangle(b.X, b.Y, Math.Max(ClientSize.Width - b.X, TotalWidth()), b.Height);
            using (var br = new SolidBrush(BackColor)) g.FillRectangle(br, row);
            Color bg = item.Selected ? Theme.FillHover : (item.Index % 2 == 1 ? Theme.Stripe : BackColor);
            if (bg != BackColor)
            {
                Theme.Smooth(g);
                Theme.FillRound(g, bg, new RectangleF(row.X + 2, row.Y + 1, row.Width - 4, row.Height - 2), Font.Height * 0.45f);
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Default;
            }
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
                x = icon.Right + Pad - 2;
                col0Indent = Math.Max(4, x - cell.X);
            }
            else x = cell.X + Pad;
            var text = e.SubItem != null ? e.SubItem.Text : "";
            int right = cell.Right - 6;

            if (e.ColumnIndex == StatusColumn && !string.IsNullOrEmpty(text))
            {
                var dotC = item.ForeColor;
                if (dotC == Theme.Text || dotC == SystemColors.WindowText) dotC = Theme.Tertiary;
                float d = Math.Max(6, Font.Height * 0.42f);
                Theme.Smooth(g);
                using (var b = new SolidBrush(dotC)) g.FillEllipse(b, x, cell.Y + (cell.Height - d) / 2f, d, d);
                g.SmoothingMode = SmoothingMode.None;
                x += (int)d + Font.Height / 3;
                TextRenderer.DrawText(g, text, Font, new Rectangle(x, cell.Y, Math.Max(0, right - x), cell.Height), item.ForeColor == Theme.Red ? Theme.Red : Theme.Text, Theme.LeftMid);
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
                        TextRenderer.DrawText(g, EmptyText, Font, r, Theme.Tertiary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    }
                }
                catch { }
            }
        }
    }

    // Вузька панель з іконками, яку можна розгорнути
    class SideNav : Control
    {
        public class Item
        {
            public string Glyph, Text, Badge;
            public bool Bottom;
            public Item(string glyph, string text) { Glyph = glyph; Text = text; }
        }

        public readonly List<Item> Items = new List<Item>();
        public string Title = "AutoShorts", Subtitle, Status;
        public bool Busy;
        public event EventHandler SelectedChanged, CollapsedChanged;
        readonly ToolTip tip = new ToolTip { InitialDelay = 250 };
        int selected, hover = -1;
        bool collapsed = true, hoverToggle;

        public SideNav()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.SidebarBg;
        }

        public bool Collapsed
        {
            get { return collapsed; }
            set { if (collapsed == value) return; collapsed = value; Invalidate(); if (CollapsedChanged != null) CollapsedChanged(this, EventArgs.Empty); }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < -1 || value >= Items.Count || value == selected) return;
                selected = value;
                Invalidate();
                if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty);
            }
        }

        int ItemH { get { return (int)(Font.Height * 2.4f); } }
        int Top0 { get { return (int)(Font.Height * 7.2f); } }
        int M { get { return Font.Height * 2 / 3; } }

        Rectangle ToggleRect { get { int s = (int)(Font.Height * 2.1f); return collapsed ? new Rectangle((Width - s) / 2, (int)(Font.Height * 4.4f), s, s) : new Rectangle(Width - s - M, (int)(Font.Height * 1.3f), s, s); } }

        Rectangle ItemRect(int i)
        {
            var it = Items[i];
            int topCount = Items.Count(x => !x.Bottom);
            int y;
            if (!it.Bottom) y = Top0 + i * (ItemH + 4);
            else
            {
                int bi = Items.Where(x => x.Bottom).ToList().IndexOf(it);
                int bc = Items.Count(x => x.Bottom);
                y = Height - Font.Height * 3 - (bc - bi) * (ItemH + 4);
            }
            if (collapsed) { int s = ItemH; return new Rectangle((Width - s) / 2, y, s, ItemH); }
            return new Rectangle(M, y, Width - 2 * M - 1, ItemH);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Items.Count; i++) if (ItemRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            bool ht = ToggleRect.Contains(e.Location);
            if (h != hover || ht != hoverToggle)
            {
                hover = h; hoverToggle = ht; Invalidate();
                tip.SetToolTip(this, h >= 0 && collapsed ? Items[h].Text : ht ? (collapsed ? "Розгорнути панель" : "Згорнути панель") : null);
            }
            Cursor = h >= 0 || ht ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; hoverToggle = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (ToggleRect.Contains(e.Location)) Collapsed = !Collapsed;
                else { int h = HitTest(e.Location); if (h >= 0) SelectedIndex = h; }
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            using (var p = new Pen(Theme.Border)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
            Theme.Smooth(g);
            int fh = Font.Height;

            // логотип
            float s = fh * 2.2f;
            var ir = collapsed ? new RectangleF((Width - s) / 2, fh * 1.2f, s, s) : new RectangleF(M + 4, fh * 1.2f, s, s);
            using (var path = Theme.Round(ir, s * 0.28f))
            using (var br = new LinearGradientBrush(ir, Color.FromArgb(190, 242, 100), Color.FromArgb(52, 211, 153), 45f))
                g.FillPath(br, path);
            var icons = Theme.Icons(Font.SizeInPoints * 1.05f);
            if (icons != null) TextRenderer.DrawText(g, Glyphs.Cut, icons, Rectangle.Round(ir), Color.FromArgb(14, 14, 16), Theme.Center);
            else TextRenderer.DrawText(g, "A", Theme.Display(Font.SizeInPoints * 1.1f), Rectangle.Round(ir), Color.FromArgb(14, 14, 16), Theme.Center);
            if (!collapsed)
            {
                int tx = (int)(ir.Right + fh * 0.6f);
                TextRenderer.DrawText(g, Title, Theme.Display(Font.SizeInPoints * 1.25f), new Rectangle(tx, (int)ir.Y - 2, Width - tx - fh * 3, (int)(s * 0.6f)), Theme.Text, Theme.LeftMid);
                if (!string.IsNullOrEmpty(Subtitle))
                    TextRenderer.DrawText(g, Subtitle, Theme.F(Font.SizeInPoints * 0.82f), new Rectangle(tx, (int)(ir.Y + s * 0.52f), Width - tx - fh * 3, (int)(s * 0.48f)), Theme.Tertiary, Theme.LeftMid);
            }

            // кнопка «згорнути/розгорнути»
            var tr = ToggleRect;
            if (hoverToggle) Theme.FillRound(g, Theme.Fill, tr, fh * 0.45f);
            Theme.StrokeRound(g, Theme.Border, 1f, tr, fh * 0.45f);
            float cx = tr.X + tr.Width / 2f, cy = tr.Y + tr.Height / 2f, d = fh * 0.25f;
            using (var p = new Pen(Theme.Secondary, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float dir = collapsed ? 1 : -1;
                g.DrawLine(p, cx - d * dir, cy - d, cx + d * 0.2f * dir, cy);
                g.DrawLine(p, cx + d * 0.2f * dir, cy, cx - d * dir, cy + d);
                g.DrawLine(p, cx + d * dir, cy - d * 1.2f, cx + d * dir, cy + d * 1.2f);
            }

            var iconFont = Theme.Icons(Font.SizeInPoints * 1.1f);
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                var r = ItemRect(i);
                bool sel = i == selected;
                if (sel) Theme.FillRound(g, Theme.FillHover, r, fh * 0.55f);
                else if (i == hover) Theme.FillRound(g, Theme.Fill, r, fh * 0.55f);
                var fg = sel ? Theme.Text : Theme.Secondary;
                int iconW = fh + 6;
                int x = collapsed ? r.X + (r.Width - iconW) / 2 : r.X + fh * 2 / 3;
                var icRect = new Rectangle(x, r.Y, iconW, r.Height);
                if (iconFont != null && it.Glyph != null) TextRenderer.DrawText(g, it.Glyph, iconFont, icRect, sel ? Theme.Accent : fg, Theme.Center);
                else TextRenderer.DrawText(g, it.Text.Substring(0, 1), Theme.Semi(Font.SizeInPoints), icRect, sel ? Theme.Accent : fg, Theme.Center);
                if (collapsed)
                {
                    if (!string.IsNullOrEmpty(it.Badge))
                    {
                        float bd = fh * 0.5f;
                        using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, r.Right - bd - fh * 0.35f, r.Y + fh * 0.35f, bd, bd);
                    }
                    continue;
                }
                x += iconW + fh / 2;
                int bw = 0;
                if (!string.IsNullOrEmpty(it.Badge))
                {
                    var bf = Theme.Semi(Font.SizeInPoints * 0.82f);
                    bw = TextRenderer.MeasureText(g, it.Badge, bf, Size.Empty, TextFormatFlags.NoPadding).Width + fh * 2 / 3;
                    var brc = new RectangleF(r.Right - bw - fh / 2, r.Y + (r.Height - fh * 1.15f) / 2, bw, fh * 1.15f);
                    Theme.FillRound(g, Theme.AccentSoft, brc, brc.Height / 2);
                    TextRenderer.DrawText(g, it.Badge, bf, Rectangle.Round(brc), Theme.Accent, Theme.Center);
                }
                TextRenderer.DrawText(g, it.Text, sel ? Theme.Semi(Font.SizeInPoints) : Font,
                    new Rectangle(x, r.Y, Math.Max(0, r.Right - x - bw - fh), r.Height), fg, Theme.LeftMid);
            }

            if (!string.IsNullOrEmpty(Status))
            {
                int y = Height - fh * 2;
                float d2 = fh * 0.5f;
                var dotC = Busy ? Theme.Accent : Theme.Green;
                float dx = collapsed ? (Width - d2) / 2 : M + 8;
                using (var b = new SolidBrush(Color.FromArgb(Busy ? 70 : 40, dotC))) g.FillEllipse(b, dx - d2 / 2, y + (fh * 1.4f - d2 * 2) / 2, d2 * 2, d2 * 2);
                using (var b = new SolidBrush(dotC)) g.FillEllipse(b, dx, y + (fh * 1.4f - d2) / 2, d2, d2);
                if (!collapsed)
                    TextRenderer.DrawText(g, Status, Theme.F(Font.SizeInPoints * 0.88f), new Rectangle((int)(dx + d2 + fh / 2), y, Width - M * 2 - fh * 2, (int)(fh * 1.4f)), Theme.Secondary, Theme.LeftMid);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }

    // Сторінка із заголовком (або без нього)
    class Page : Panel
    {
        public readonly FlowLayoutPanel Head;

        public Page(string title, string subtitle, Control body)
        {
            BackColor = Theme.Bg;
            Dock = DockStyle.Fill;
            Padding = new Padding(36, 24, 36, 20);
            body.Dock = DockStyle.Fill;
            Controls.Add(body);
            if (title != null)
            {
                Head = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, 0, 0, 16) };
                Head.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.Display(20f), ForeColor = Theme.Text, Margin = new Padding(0) });
                if (!string.IsNullOrEmpty(subtitle))
                    Head.Controls.Add(new Label { Text = subtitle, AutoSize = true, Font = Theme.F(10f), ForeColor = Theme.Secondary, Margin = new Padding(1, 3, 0, 0) });
                Controls.Add(Head);
            }
        }
    }

    // Плитка з одним числом
    class StatTile : Control
    {
        public string Caption, Value = "—";

        public StatTile(string caption)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Caption = caption;
            Size = new Size(184, 88);
            Margin = new Padding(0, 0, 14, 14);
        }

        public void Set(string v) { Value = string.IsNullOrEmpty(v) ? "—" : v; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            float rad = Font.Height * 0.9f;
            Theme.FillRound(g, Theme.Card, r, rad);
            Theme.StrokeRound(g, Theme.Border, 1f, r, rad);
            int p = Font.Height;
            TextRenderer.DrawText(g, Caption, Theme.F(Font.SizeInPoints * 0.9f), new Rectangle(p, p * 2 / 3, Width - p * 2, p + 4), Theme.Secondary, Theme.LeftMid);
            TextRenderer.DrawText(g, Value, Theme.Display(Font.SizeInPoints * 1.9f), new Rectangle(p, p * 2 / 3 + p + 2, Width - p * 2, Height - p * 2 - 4), Theme.Text, Theme.LeftMid);
        }
    }

    // Сітка карток із власною прокруткою (проєкти, кліпи)
    class TileView : Control
    {
        public readonly List<object> Items = new List<object>();
        public Size TileSize = new Size(220, 200);
        public int Gap = 18;
        public Action<Graphics, object, Rectangle, bool, bool> Painter; // g, item, rect, hover, selected
        public object Selected;
        public string EmptyText, EmptyHint;
        public event Action<object, Point, MouseButtons> TileClicked;
        public event Action<object> TileDoubleClicked;
        public event EventHandler SelectionChanged;
        int scroll, hover = -1;
        bool dragThumb;
        int dragY, dragScroll;

        public TileView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Bg;
        }

        public Func<int, int> HeightFor; // висота плитки залежно від ширини
        int SbW { get { return (int)(14 * Theme.Dpi); } }
        int Cols { get { return Math.Max(1, (Width - SbW + Gap) / (TileSize.Width + Gap)); } }
        int Rows { get { return (Items.Count + Cols - 1) / Cols; } }
        public int TileW { get { return HeightFor == null ? TileSize.Width : Math.Max(TileSize.Width, Math.Min(TileSize.Width * 3 / 2, (Width - SbW - Gap * (Cols - 1)) / Cols)); } }
        public int TileH { get { return HeightFor == null ? TileSize.Height : HeightFor(TileW); } }
        int ContentH { get { return Items.Count == 0 ? 0 : Rows * (TileH + Gap) - Gap + 4; } }
        int MaxScroll { get { return Math.Max(0, ContentH - Height); } }

        public Rectangle TileRect(int i)
        {
            int c = i % Cols, r = i / Cols, w = TileW, h = TileH;
            return new Rectangle(c * (w + Gap), r * (h + Gap) - scroll, w, h);
        }

        public void RefreshItems()
        {
            if (Selected != null && !Items.Contains(Selected)) Selected = null;
            scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
            Invalidate();
        }

        public void EnsureVisible(object item)
        {
            int i = Items.IndexOf(item);
            if (i < 0) return;
            var r = TileRect(i);
            if (r.Top < 0) scroll += r.Top;
            else if (r.Bottom > Height) scroll += r.Bottom - Height;
            RefreshItems();
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Items.Count; i++) if (TileRect(i).Contains(p)) return i;
            return -1;
        }

        Rectangle Thumb()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            int th = Math.Max((int)(40 * Theme.Dpi), Height * Height / Math.Max(1, ContentH));
            int ty = (int)((long)scroll * (Height - th) / MaxScroll);
            int w = (int)(6 * Theme.Dpi);
            return new Rectangle(Width - w - 3, ty, w, th);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (Items.Count == 0)
            {
                if (!string.IsNullOrEmpty(EmptyText))
                {
                    var r = new Rectangle(0, Height / 2 - Font.Height * 2, Width, Font.Height * 2);
                    TextRenderer.DrawText(g, EmptyText, Theme.Semi(Font.SizeInPoints * 1.1f), r, Theme.Secondary, Theme.Center);
                    if (!string.IsNullOrEmpty(EmptyHint))
                        TextRenderer.DrawText(g, EmptyHint, Font, new Rectangle(0, r.Bottom, Width, Font.Height * 2), Theme.Tertiary, Theme.Center);
                }
                return;
            }
            for (int i = 0; i < Items.Count; i++)
            {
                var r = TileRect(i);
                if (r.Bottom < -4 || r.Top > Height + 4) continue;
                var st = g.Save();
                try { if (Painter != null) Painter(g, Items[i], r, i == hover, Items[i] == Selected); }
                catch { }
                g.Restore(st);
            }
            var t = Thumb();
            if (!t.IsEmpty)
            {
                Theme.Smooth(g);
                Theme.FillRound(g, dragThumb ? Color.FromArgb(110, 110, 118) : Color.FromArgb(70, 70, 77), t, t.Width / 2f);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll -= e.Delta * Math.Max((int)(60 * Theme.Dpi), TileH / 3) / 120;
            RefreshItems();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragThumb)
            {
                var t = Thumb();
                int range = Math.Max(1, Height - t.Height);
                scroll = dragScroll + (e.Y - dragY) * MaxScroll / range;
                RefreshItems();
                return;
            }
            int h = HitTest(e.Location);
            if (h != hover) { hover = h; Invalidate(); }
            Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { if (hover != -1) { hover = -1; Invalidate(); } base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            var t = Thumb();
            if (!t.IsEmpty && e.X >= Width - SbW)
            {
                if (t.Contains(e.Location)) { dragThumb = true; dragY = e.Y; dragScroll = scroll; }
                else { scroll += e.Y < t.Top ? -Height : Height; RefreshItems(); }
                return;
            }
            int i = HitTest(e.Location);
            if (i >= 0)
            {
                var item = Items[i];
                var r = TileRect(i);
                if (e.Button == MouseButtons.Left && Selected != item)
                {
                    Selected = item;
                    Invalidate();
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                }
                if (TileClicked != null) TileClicked(item, new Point(e.X - r.X, e.Y - r.Y), e.Button);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { if (dragThumb) { dragThumb = false; Invalidate(); } base.OnMouseUp(e); }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            int i = HitTest(e.Location);
            if (i >= 0 && TileDoubleClicked != null) TileDoubleClicked(Items[i]);
            base.OnMouseDoubleClick(e);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); RefreshItems(); }
    }

    // Календар публікацій на місяць
    class CalendarView : Control
    {
        public class Entry
        {
            public string Time, Title;
            public Color Color;
            public object Tag;
            public bool Dim;      // заплановане / приватне — блідіше
        }

        public DateTime Month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public DateTime Today = DateTime.Today;
        public Func<DateTime, List<Entry>> EntriesFor;
        public event Action<Entry> EntryClicked;
        readonly List<KeyValuePair<Rectangle, Entry>> hits = new List<KeyValuePair<Rectangle, Entry>>();
        Entry hoverEntry;

        static readonly string[] Days = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };

        public CalendarView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            hits.Clear();
            int fh = Font.Height, gap = (int)(6 * Theme.Dpi), headH = fh + 10;
            float cw = (Width - gap * 6) / 7f, ch = (Height - headH - gap * 5) / 6f;
            for (int d = 0; d < 7; d++)
                TextRenderer.DrawText(g, Days[d], Theme.Semi(Font.SizeInPoints * 0.88f), new Rectangle((int)(d * (cw + gap)) + fh / 2, 0, (int)cw, headH), d >= 5 ? Theme.Accent : Theme.Secondary, Theme.LeftMid);
            int offset = ((int)Month.DayOfWeek + 6) % 7;
            var first = Month.AddDays(-offset);
            Theme.Smooth(g);
            var small = Theme.F(Font.SizeInPoints * 0.85f);
            int lineH = (int)(small.Height * 1.45f);
            for (int i = 0; i < 42; i++)
            {
                var day = first.AddDays(i);
                int col = i % 7, row = i / 7;
                var r = new RectangleF(col * (cw + gap), headH + row * (ch + gap), cw, ch);
                bool inMonth = day.Month == Month.Month;
                bool today = day.Date == Today.Date;
                Theme.FillRound(g, inMonth ? Theme.Card : Theme.SidebarBg, r, fh * 0.6f);
                Theme.StrokeRound(g, today ? Theme.Accent : Theme.Border, today ? 1.5f : 1f, r, fh * 0.6f);
                var numR = new Rectangle((int)r.X + fh / 2, (int)r.Y + 4, (int)r.Width - fh, fh + 4);
                TextRenderer.DrawText(g, day.Day.ToString(), today ? Theme.Semi(Font.SizeInPoints) : Font, numR, today ? Theme.Accent : inMonth ? Theme.Text : Theme.Tertiary, Theme.LeftMid);
                if (EntriesFor == null) continue;
                var list = EntriesFor(day.Date);
                if (list == null || list.Count == 0) continue;
                int y = numR.Bottom + 2;
                int maxLines = Math.Max(0, (int)((r.Bottom - y - 4) / lineH));
                for (int k = 0; k < list.Count && k < maxLines; k++)
                {
                    var en = list[k];
                    bool more = k == maxLines - 1 && list.Count > maxLines;
                    var lr = new Rectangle((int)r.X + 4, y, (int)r.Width - 8, lineH - 2);
                    if (more)
                    {
                        TextRenderer.DrawText(g, "+ ще " + (list.Count - k), small, new Rectangle(lr.X + fh / 3, lr.Y, lr.Width, lr.Height), Theme.Secondary, Theme.LeftMid);
                        break;
                    }
                    Theme.FillRound(g, en == hoverEntry ? Theme.FillHover : Color.FromArgb(en.Dim ? 16 : 40, en.Color), lr, lr.Height / 2.5f);
                    if (en.Dim) Theme.StrokeRound(g, Color.FromArgb(90, en.Color), 1f, lr, lr.Height / 2.5f);
                    float dd = small.Height * 0.45f;
                    using (var b = new SolidBrush(en.Color)) g.FillEllipse(b, lr.X + fh / 3f, lr.Y + (lr.Height - dd) / 2, dd, dd);
                    int tx = lr.X + fh / 3 + (int)dd + 5;
                    int tw = TextRenderer.MeasureText(g, en.Time, small, Size.Empty, TextFormatFlags.NoPadding).Width;
                    TextRenderer.DrawText(g, en.Time, small, new Rectangle(tx, lr.Y, tw + 2, lr.Height), Theme.Secondary, Theme.LeftMid);
                    TextRenderer.DrawText(g, en.Title, small, new Rectangle(tx + tw + 5, lr.Y, Math.Max(0, lr.Right - tx - tw - 9), lr.Height), en.Dim ? Theme.Secondary : Theme.Text, Theme.LeftMid);
                    hits.Add(new KeyValuePair<Rectangle, Entry>(lr, en));
                    y += lineH;
                }
            }
        }

        Entry Hit(Point p)
        {
            foreach (var h in hits) if (h.Key.Contains(p)) return h.Value;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var h = Hit(e.Location);
            if (h != hoverEntry) { hoverEntry = h; Invalidate(); }
            Cursor = h != null ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var h = Hit(e.Location);
            if (h != null && EntryClicked != null) EntryClicked(h);
            base.OnMouseDown(e);
        }
    }

    // Оцінка віральності: велике число й складові
    class ScorePanel : Control
    {
        public int Virality;
        public readonly List<KeyValuePair<string, int>> Parts = new List<KeyValuePair<string, int>>();

        public ScorePanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            Margin = new Padding(3);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int w = proposedSize.Width > 0 && proposedSize.Width < 20000 ? proposedSize.Width : (int)(200 * Theme.Dpi);
            return new Size(w, (int)(Font.Height * (4.2f + 1.7f * Math.Max(1, Parts.Count))));
        }

        public void Set(int virality, params KeyValuePair<string, int>[] parts)
        {
            Virality = virality;
            Parts.Clear();
            Parts.AddRange(parts.Where(p => p.Value > 0));
            Size = GetPreferredSize(new Size(Width, 0));
            Invalidate();
        }

        public static Color ScoreColor(int v)
        {
            return v >= 85 ? Theme.Accent : v >= 70 ? Color.FromArgb(250, 204, 21) : Theme.Orange;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            int fh = Font.Height;
            if (Virality <= 0)
            {
                TextRenderer.DrawText(g, "Оцінка з’явиться для нових кліпів", Font, new Rectangle(0, 0, Width, fh * 2), Theme.Tertiary, Theme.LeftMid);
                return;
            }
            var big = Theme.Display(Font.SizeInPoints * 2.6f);
            var vs = Virality.ToString();
            int bw = TextRenderer.MeasureText(g, vs, big, Size.Empty, TextFormatFlags.NoPadding).Width;
            int bh = big.Height;
            TextRenderer.DrawText(g, vs, big, new Rectangle(0, 0, bw + 4, bh), ScoreColor(Virality), Theme.LeftMid);
            TextRenderer.DrawText(g, "/100", Theme.Semi(Font.SizeInPoints * 1.1f), new Rectangle(bw + 4, bh / 3, fh * 4, bh * 2 / 3), Theme.Secondary, Theme.LeftMid);
            TextRenderer.DrawText(g, "Віральність", Theme.F(Font.SizeInPoints * 0.9f), new Rectangle(0, bh - 2, Width, fh + 4), Theme.Secondary, Theme.LeftMid);
            int y = bh + fh + 8;
            Theme.Smooth(g);
            int labelW = (int)(fh * 7.5f);
            foreach (var p in Parts)
            {
                TextRenderer.DrawText(g, p.Key, Font, new Rectangle(0, y, labelW, fh + 4), Theme.Text, Theme.LeftMid);
                float bx = labelW, bwid = Math.Max(20, Width - labelW - fh * 2.4f), bhh = Math.Max(4, fh / 3.2f);
                var track = new RectangleF(bx, y + (fh + 4 - bhh) / 2, bwid, bhh);
                Theme.FillRound(g, Theme.Fill, track, bhh / 2);
                Theme.FillRound(g, ScoreColor(p.Value), new RectangleF(bx, track.Y, Math.Max(bhh, bwid * p.Value / 100f), bhh), bhh / 2);
                TextRenderer.DrawText(g, p.Value.ToString(), Theme.Semi(Font.SizeInPoints * 0.9f), new Rectangle((int)(bx + bwid + 6), y, fh * 2, fh + 4), Theme.Secondary, Theme.LeftMid);
                y += (int)(fh * 1.7f);
            }
        }
    }

    // Мініатюри стилів субтитрів
    static class StylePreview
    {
        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

        static Color Hex(string rgb, Color fallback)
        {
            if (string.IsNullOrEmpty(rgb) || rgb.Length < 6) return fallback;
            try { return Color.FromArgb(Convert.ToInt32(rgb.Substring(0, 2), 16), Convert.ToInt32(rgb.Substring(2, 2), 16), Convert.ToInt32(rgb.Substring(4, 2), 16)); }
            catch { return fallback; }
        }

        public static Image Get(SubStyle st, int w, int h)
        {
            var key = st.Name + "|" + w + "x" + h;
            Image img;
            if (cache.TryGetValue(key, out img)) return img;
            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h));
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Theme.Card);
                using (var path = Theme.Round(new RectangleF(0, 0, w - 1, h - 1), h * 0.18f))
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(58, 52, 74), Color.FromArgb(24, 30, 40), 60f))
                    g.FillPath(br, path);
                var words = st.Upper ? new[] { "ЦЕ", "ПРОСТО", "ВОГОНЬ" } : new[] { "Це", "просто", "вогонь" };
                var colors = new[] { Hex(st.Primary, Color.White), Hex(st.Active, Hex(st.Primary, Color.White)), Hex(st.Keyword, Hex(st.Primary, Color.White)) };
                FontFamily fam;
                try { fam = new FontFamily(st.Font); } catch { fam = FontFamily.GenericSansSerif; }
                float em = h * 0.3f;
                var fmt = StringFormat.GenericTypographic;
                using (fam)
                {
                    var style = fam.IsStyleAvailable(FontStyle.Bold) && !st.Font.Contains("Black") && !st.Font.Contains("Impact") ? FontStyle.Bold : FontStyle.Regular;
                    if (!fam.IsStyleAvailable(style)) style = FontStyle.Regular;
                    var paths = new List<GraphicsPath>();
                    float total = 0, space = em * 0.28f;
                    foreach (var wd in words)
                    {
                        var p = new GraphicsPath();
                        p.AddString(wd, fam, (int)style, em, new PointF(0, 0), fmt);
                        paths.Add(p);
                        total += p.GetBounds().Width + space;
                    }
                    total -= space;
                    float scale = Math.Min(1f, (w * 0.88f) / Math.Max(1, total));
                    float x = (w - total * scale) / 2;
                    for (int i = 0; i < paths.Count; i++)
                    {
                        var p = paths[i];
                        var b = p.GetBounds();
                        using (var m = new Matrix())
                        {
                            m.Translate(x - b.X * scale, h / 2f - (b.Y + b.Height / 2) * scale);
                            m.Scale(scale, scale);
                            p.Transform(m);
                        }
                        var nb = p.GetBounds();
                        if (st.Box)
                        {
                            using (var bp = Theme.Round(RectangleF.Inflate(nb, em * 0.12f, em * 0.1f), em * 0.12f))
                            using (var bb = new SolidBrush(Hex(st.Outline, Color.Black))) g.FillPath(bb, bp);
                        }
                        else if (st.OutlineWidth > 0)
                        {
                            using (var pen = new Pen(Hex(st.Outline, Color.Black), Math.Max(1.5f, st.OutlineWidth * em / 90f * scale)) { LineJoin = LineJoin.Round })
                                g.DrawPath(pen, p);
                        }
                        using (var fb = new SolidBrush(colors[i])) g.FillPath(fb, p);
                        x += nb.Width + space * scale;
                        p.Dispose();
                    }
                }
            }
            cache[key] = bmp;
            return bmp;
        }
    }

    // Діалог: відрізок відео
    class RangeDialog : Form
    {
        readonly TextBox fromBox = new TextBox(), toBox = new TextBox();
        public double From, To;

        public RangeDialog(double from, double to, Font font)
        {
            Text = "Відрізок відео";
            Font = font;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(440, 230);
            HandleCreated += (s, e) => Theme.StyleWindow(this);
            fromBox.Text = from > 0 ? Fmt(from) : "";
            toBox.Text = to > 0 ? Fmt(to) : "";

            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(22, 18, 22, 14) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.Controls.Add(new Label { Text = "Шукати моменти лише в цьому відрізку. Формат — хв:сек або год:хв:сек. Порожнє поле — від початку / до кінця.", AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = Theme.Secondary, Margin = new Padding(3, 0, 3, 12) }, 0, 0);
            t.SetColumnSpan(t.GetControlFromPosition(0, 0), 2);
            t.Controls.Add(new Label { Text = "Від", AutoSize = true, ForeColor = Theme.Secondary }, 0, 1);
            t.Controls.Add(new Label { Text = "До", AutoSize = true, ForeColor = Theme.Secondary }, 1, 1);
            var f1 = new Field(fromBox, 0, "0:00") { Dock = DockStyle.Fill };
            var f2 = new Field(toBox, 0, "кінець") { Dock = DockStyle.Fill };
            t.Controls.Add(f1, 0, 2);
            t.Controls.Add(f2, 1, 2);
            var ok = new AButton("Готово", ButtonKind.Primary);
            ok.Click += (s, e) => { if (Parse()) { DialogResult = DialogResult.OK; Close(); } };
            var reset = new AButton("Усе відео", ButtonKind.Secondary);
            reset.Click += (s, e) => { From = 0; To = 0; DialogResult = DialogResult.OK; Close(); };
            var bar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 14, 0, 0) };
            bar.Controls.Add(ok);
            bar.Controls.Add(reset);
            t.Controls.Add(bar, 0, 3);
            t.SetColumnSpan(bar, 2);
            Controls.Add(t);
            AcceptButton = ok;
        }

        static string Fmt(double s)
        {
            var ts = TimeSpan.FromSeconds(s);
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }

        public static double ParseTime(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return 0;
            double total = 0;
            foreach (var part in s.Split(':'))
            {
                double v;
                if (!double.TryParse(part.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return -1;
                total = total * 60 + v;
            }
            return total;
        }

        bool Parse()
        {
            double a = ParseTime(fromBox.Text), b = ParseTime(toBox.Text);
            if (a < 0 || b < 0) { MessageBox.Show(this, "Не вдалося розібрати час. Приклад: 1:30 або 1:02:15.", "AutoShorts"); return false; }
            if (b > 0 && b - a < 15) { MessageBox.Show(this, "Відрізок має бути довшим за 15 секунд.", "AutoShorts"); return false; }
            From = a; To = b;
            return true;
        }
    }

    // Галерея варіантів із картинками (стилі субтитрів, стилі гачка)
    class ChoiceGallery : Control
    {
        public readonly List<string> Items = new List<string>();
        public Func<string, Size, Image> ImageFor;
        public Size Tile = new Size(150, 84);
        int selected = -1, hover = -1;
        public event EventHandler SelectedIndexChanged;

        public ChoiceGallery()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            Margin = new Padding(3, 6, 3, 6);
            Cursor = Cursors.Hand;
        }

        Size T { get { return new Size((int)(Tile.Width * Theme.Dpi), (int)(Tile.Height * Theme.Dpi)); } }
        int Gap { get { return (int)(12 * Theme.Dpi); } }
        int CapH { get { return Font.Height + 8; } }

        public string SelectedItem
        {
            get { return selected >= 0 && selected < Items.Count ? Items[selected] : null; }
            set { SelectedIndex = Items.IndexOf(value); }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set { if (value == selected) return; selected = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }

        int Cols(int width) { return Math.Max(1, (width + Gap) / (T.Width + Gap)); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int w = proposedSize.Width > 0 && proposedSize.Width < 20000 ? proposedSize.Width : Items.Count * (T.Width + Gap);
            int rows = (Items.Count + Cols(w) - 1) / Math.Max(1, Cols(w));
            return new Size(w, Math.Max(1, rows) * (T.Height + CapH + Gap));
        }

        Rectangle R(int i)
        {
            int c = Cols(Width);
            return new Rectangle((i % c) * (T.Width + Gap), (i / c) * (T.Height + CapH + Gap), T.Width, T.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Theme.BackOf(this);
            g.Clear(back);
            for (int i = 0; i < Items.Count; i++)
            {
                var r = R(i);
                var img = ImageFor != null ? ImageFor(Items[i], r.Size) : null;
                MainForm.DrawCover(g, img, r, Font.Height * 0.7f, back, null);
                Theme.Smooth(g);
                var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1.5f, r.Height - 1.5f);
                if (i == selected) Theme.StrokeRound(g, Theme.Primary, 2.2f, rf, Font.Height * 0.7f);
                else if (i == hover) Theme.StrokeRound(g, Theme.Tertiary, 1.4f, rf, Font.Height * 0.7f);
                if (i == selected)
                {
                    var f = Theme.Semi(Font.SizeInPoints * 0.78f);
                    var pr = new Rectangle(r.X + 6, r.Y + 6, TextRenderer.MeasureText("Вибрано", f).Width + 4, f.Height + 4);
                    Theme.FillRound(g, Color.FromArgb(200, 12, 12, 14), pr, pr.Height / 2f);
                    TextRenderer.DrawText(g, "Вибрано", f, pr, Theme.Text, Theme.Center);
                }
                TextRenderer.DrawText(g, Items[i], Theme.F(Font.SizeInPoints * 0.9f), new Rectangle(r.X, r.Bottom + 2, r.Width, CapH), i == selected ? Theme.Text : Theme.Secondary, Theme.Center | TextFormatFlags.EndEllipsis);
            }
        }

        int Hit(Point p) { for (int i = 0; i < Items.Count; i++) if (R(i).Contains(p)) return i; return -1; }
        protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { int h = Hit(e.Location); if (h >= 0 && Enabled) SelectedIndex = h; base.OnMouseDown(e); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    }

    // Мініатюри стилів заголовка-гачка
    static class HookPreview
    {
        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

        public static Image Get(string style, int w, int h)
        {
            var key = style + "|" + w + "x" + h;
            Image img;
            if (cache.TryGetValue(key, out img)) return img;
            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h));
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(96, 120, 140), Color.FromArgb(40, 36, 52), 90f)) g.FillRectangle(br, 0, 0, w, h);
                using (var b = new SolidBrush(Color.FromArgb(70, 20, 20, 24))) g.FillEllipse(b, w * 0.28f, h * 0.34f, w * 0.44f, w * 0.44f);
                using (var b = new SolidBrush(Color.FromArgb(70, 20, 20, 24))) g.FillEllipse(b, w * 0.12f, h * 0.62f, w * 0.76f, h * 0.6f);
                Color box = Color.Black, fg = Color.White;
                bool hasBox = true;
                if (style == "Біла плашка") { box = Color.White; fg = Color.FromArgb(20, 20, 20); }
                else if (style == "Рожева плашка") { box = Color.FromArgb(255, 46, 136); }
                else if (style == "Без плашки") hasBox = false;
                using (var f = new Font("Arial Black", Math.Max(6, h * 0.055f), FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    var lines = new[] { "ЯК ЦЕ", "ПРАЦЮЄ?" };
                    float y = h * 0.09f;
                    foreach (var l in lines)
                    {
                        var sz = g.MeasureString(l, f);
                        float x = (w - sz.Width) / 2;
                        if (hasBox) using (var bb = new SolidBrush(box)) g.FillRectangle(bb, x - 3, y, sz.Width + 6, sz.Height);
                        else using (var sb = new SolidBrush(Color.FromArgb(160, 0, 0, 0))) g.DrawString(l, f, sb, x + 1.5f, y + 1.5f);
                        using (var fb = new SolidBrush(fg)) g.DrawString(l, f, fb, x, y);
                        y += sz.Height + (hasBox ? 0 : 1);
                    }
                }
            }
            cache[key] = bmp;
            return bmp;
        }
    }

    // Вибір відрізка поверх графіка «найчастіше переглядають»
    class RangeBar : Control
    {
        public double Duration = 600, Start, End;
        public List<HeatPoint> Heat = new List<HeatPoint>();
        public event EventHandler RangeChanged;
        int drag; // 1 — початок, 2 — кінець, 3 — переміщення
        double dragOffset;

        public RangeBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 58;
            Margin = new Padding(3, 6, 3, 6);
        }

        double E { get { return End > 0 ? End : Duration; } }
        float X(double t) { return (float)(Hw + (Width - 2 * Hw) * Math.Max(0, Math.Min(1, t / Math.Max(1, Duration)))); }
        double TimeAt(int x) { return Math.Max(0, Math.Min(Duration, (x - Hw) / (double)Math.Max(1, Width - 2 * Hw) * Duration)); }
        int Hw { get { return (int)(9 * Theme.Dpi); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var track = new RectangleF(Hw, 2, Width - 2 * Hw, Height - 4);
            float rad = Font.Height * 0.55f;
            Theme.FillRound(g, Theme.FieldBg, track, rad);
            // крива переглядів
            using (var clip = Theme.Round(track, rad))
            {
                var st = g.Save();
                g.SetClip(clip);
                if (Heat != null && Heat.Count > 2)
                {
                    double max = Heat.Max(h => h.Value);
                    var pts = new List<PointF> { new PointF(track.X, track.Bottom) };
                    foreach (var h in Heat) pts.Add(new PointF(X((h.Start + h.End) / 2), track.Bottom - (float)(h.Value / Math.Max(1e-9, max)) * (track.Height - 8)));
                    pts.Add(new PointF(track.Right, track.Bottom));
                    using (var b = new SolidBrush(Color.FromArgb(70, Theme.Accent))) g.FillPolygon(b, pts.ToArray());
                    using (var p = new Pen(Color.FromArgb(200, Theme.Accent), 1.5f)) g.DrawLines(p, pts.Skip(1).Take(pts.Count - 2).ToArray());
                }
                else
                {
                    int step = Duration > 3600 ? 600 : Duration > 900 ? 300 : 60;
                    using (var p = new Pen(Theme.Border))
                        for (double t = step; t < Duration; t += step) g.DrawLine(p, X(t), track.Bottom - track.Height * 0.35f, X(t), track.Bottom - 3);
                }
                // затемнення поза вибраним відрізком
                using (var dim = new SolidBrush(Color.FromArgb(150, 8, 8, 10)))
                {
                    g.FillRectangle(dim, track.X, track.Y, X(Start) - track.X, track.Height);
                    g.FillRectangle(dim, X(E), track.Y, track.Right - X(E), track.Height);
                }
                g.Restore(st);
            }
            // рамка відрізка й ручки
            var sel = new RectangleF(X(Start), 1, X(E) - X(Start), Height - 2);
            using (var p = new Pen(Theme.Primary, 2f)) { g.DrawLine(p, sel.X, 1, sel.Right, 1); g.DrawLine(p, sel.X, Height - 2, sel.Right, Height - 2); }
            foreach (var hx in new[] { X(Start) - Hw, X(E) })
            {
                var hr = new RectangleF(hx, 0, Hw, Height - 1);
                Theme.FillRound(g, Theme.Primary, hr, Hw / 2.2f);
                using (var p = new Pen(Color.FromArgb(120, 120, 126), 1.5f)) g.DrawLine(p, hr.X + hr.Width / 2, Height * 0.38f, hr.X + hr.Width / 2, Height * 0.62f);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            float xs = X(Start), xe = X(E);
            if (Math.Abs(e.X - (xs - Hw / 2f)) <= Hw * 1.2f) drag = 1;
            else if (Math.Abs(e.X - (xe + Hw / 2f)) <= Hw * 1.2f) drag = 2;
            else if (e.X > xs && e.X < xe) { drag = 3; dragOffset = TimeAt(e.X) - Start; }
            else
            {
                double t = TimeAt(e.X);
                if (t < Start) { Start = t; drag = 1; } else { End = t; drag = 2; }
                Changed();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            float xs = X(Start), xe = X(E);
            Cursor = drag == 3 || (e.X > xs && e.X < xe) ? Cursors.SizeAll : Cursors.SizeWE;
            if (drag == 0) { base.OnMouseMove(e); return; }
            double t = TimeAt(e.X), minGap = Math.Min(15, Duration / 4);
            if (drag == 1) Start = Math.Max(0, Math.Min(t, E - minGap));
            else if (drag == 2) End = Math.Min(Duration, Math.Max(t, Start + minGap));
            else
            {
                double len = E - Start;
                double ns = Math.Max(0, Math.Min(Duration - len, t - dragOffset));
                Start = ns; End = ns + len;
            }
            Changed();
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = 0; base.OnMouseUp(e); }

        void Changed()
        {
            if (End >= Duration - 0.5) End = 0;
            Invalidate();
            if (RangeChanged != null) RangeChanged(this, EventArgs.Empty);
        }
    }

    // Обкладинка із заокругленими кутами та кнопкою «відтворити»
    class CoverView : PictureBox
    {
        public string EmptyText = "";
        public bool ShowPlay = true;

        public CoverView()
        {
            SizeMode = PictureBoxSizeMode.Zoom;
            BackColor = Color.FromArgb(32, 32, 36);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
            var g = pe.Graphics;
            Theme.Smooth(g);
            if (Image == null)
            {
                var icons = Theme.Icons(Font.SizeInPoints * 2f);
                if (icons != null) TextRenderer.DrawText(g, Glyphs.Video, icons, new Rectangle(0, 0, Width, Height - (EmptyText.Length > 0 ? Font.Height * 2 : 0)), Theme.Tertiary, Theme.Center);
                if (EmptyText.Length > 0) TextRenderer.DrawText(g, EmptyText, Font, new Rectangle(0, Height / 2 + Font.Height, Width, Font.Height * 2), Theme.Tertiary, Theme.Center);
            }
            else if (ShowPlay)
            {
                float d = Font.Height * 2.6f;
                var c = new RectangleF((Width - d) / 2, (Height - d) / 2, d, d);
                using (var b = new SolidBrush(Color.FromArgb(130, 0, 0, 0))) g.FillEllipse(b, c);
                using (var b = new SolidBrush(Color.White))
                    g.FillPolygon(b, new[] { new PointF(c.X + d * 0.40f, c.Y + d * 0.30f), new PointF(c.X + d * 0.40f, c.Y + d * 0.70f), new PointF(c.X + d * 0.72f, c.Y + d * 0.50f) });
            }
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new Rectangle(-1, -1, Width + 2, Height + 2));
                using (var inner = Theme.Round(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), Font.Height * 0.85f)) path.AddPath(inner, false);
                path.FillMode = FillMode.Alternate;
                using (var b = new SolidBrush(Theme.BackOf(this))) g.FillPath(b, path);
            }
        }
    }

    // Прокручуваний контейнер з тонким темним повзунком (замість системних смуг прокрутки)
    class ScrollHost : Control
    {
        readonly Control child;
        int scroll, lastH = -1;
        bool drag, inLayout;
        int dragY, dragScroll;

        public ScrollHost(Control content)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            child = content;
            child.Dock = DockStyle.None;
            child.AutoSize = false;
            Controls.Add(child);
            child.Layout += (s, e) =>
            {
                if (inLayout) return;
                int h = ContentH();
                if (h == lastH) return;
                if (IsHandleCreated) BeginInvoke(new Action(() => PerformLayout()));
                else PerformLayout();
            };
            HookWheel(child);
        }

        void HookWheel(Control c)
        {
            c.MouseWheel += (s, e) =>
            {
                if (s is TextBox && ((TextBox)s).Multiline) return;
                if (s is Stepper) return;
                Scroll(-e.Delta * (int)(70 * Theme.Dpi) / 120);
            };
            c.ControlAdded += (s, e) => HookWheel(e.Control);
            foreach (Control k in c.Controls) HookWheel(k);
        }

        int SbW { get { return (int)(14 * Theme.Dpi); } }
        int ContentH() { return child.GetPreferredSize(new Size(Math.Max(1, Width - SbW), 0)).Height; }
        int MaxScroll { get { return Math.Max(0, lastH - Height); } }

        void Scroll(int d)
        {
            int ns = Math.Max(0, Math.Min(MaxScroll, scroll + d));
            if (ns == scroll) return;
            scroll = ns;
            child.Top = -scroll;
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (inLayout) return;
            inLayout = true;
            try
            {
                lastH = ContentH();
                scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
                child.Bounds = new Rectangle(0, -scroll, Math.Max(1, Width - SbW), Math.Max(Height, lastH));
            }
            finally { inLayout = false; }
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); PerformLayout(); }

        Rectangle Thumb()
        {
            if (MaxScroll <= 0) return Rectangle.Empty;
            int th = Math.Max((int)(40 * Theme.Dpi), Height * Height / Math.Max(1, lastH));
            int ty = (int)((long)scroll * (Height - th) / MaxScroll);
            int w = (int)(6 * Theme.Dpi);
            return new Rectangle(Width - w - 3, ty, w, th);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            var t = Thumb();
            if (t.IsEmpty) return;
            Theme.Smooth(e.Graphics);
            Theme.FillRound(e.Graphics, drag ? Color.FromArgb(110, 110, 118) : Color.FromArgb(70, 70, 77), t, t.Width / 2f);
        }

        protected override void OnMouseWheel(MouseEventArgs e) { Scroll(-e.Delta * (int)(70 * Theme.Dpi) / 120); base.OnMouseWheel(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var t = Thumb();
            if (t.IsEmpty) return;
            if (t.Contains(e.Location)) { drag = true; dragY = e.Y; dragScroll = scroll; Invalidate(); }
            else Scroll(e.Y < t.Top ? -Height : Height);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!drag) return;
            var t = Thumb();
            Scroll(dragScroll + (e.Y - dragY) * MaxScroll / Math.Max(1, Height - t.Height) - scroll);
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = false; Invalidate(); }
    }

    // Кнопка «Увійти через Google» (за правилами оформлення Google для темної теми)
    class GoogleButton : Button
    {
        bool hover, down;
        public float HeightFactor = 2.4f;

        public GoogleButton(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(3);
        }

        protected override bool ShowFocusCues { get { return false; } }
        Font F2 { get { return Theme.Semi(Font.SizeInPoints * 1.05f); } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int h = (int)(Font.Height * HeightFactor);
            int tw = TextRenderer.MeasureText(Text, F2, Size.Empty, TextFormatFlags.NoPadding).Width;
            return new Size(tw + h + Font.Height * 2, h);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        public static void DrawG(Graphics g, RectangleF r)
        {
            float w = r.Width * 0.2f;
            var c = new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
            using (var p = new Pen(Color.FromArgb(234, 67, 53), w)) g.DrawArc(p, c, 213, 105);
            using (var p = new Pen(Color.FromArgb(251, 188, 5), w)) g.DrawArc(p, c, 145, 70);
            using (var p = new Pen(Color.FromArgb(52, 168, 83), w)) g.DrawArc(p, c, 42, 105);
            using (var p = new Pen(Color.FromArgb(66, 133, 244), w)) g.DrawArc(p, c, -2, 46);
            using (var b = new SolidBrush(Color.FromArgb(66, 133, 244)))
                g.FillRectangle(b, r.X + r.Width / 2f, r.Y + r.Height / 2f - w / 2, r.Width / 2f, w);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            Theme.Smooth(g);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var bg = down ? Color.FromArgb(48, 48, 52) : hover ? Color.FromArgb(38, 38, 42) : Color.FromArgb(19, 19, 20);
            Theme.FillRound(g, bg, r, Height / 2f);
            Theme.StrokeRound(g, Color.FromArgb(142, 145, 143), 1f, r, Height / 2f);
            float gs = Height * 0.42f;
            float gx = Height * 0.45f;
            var gr = new RectangleF(gx, (Height - gs) / 2, gs, gs);
            if (Enabled) DrawG(g, gr);
            int tx = (int)(gx + gs + Font.Height * 0.7f);
            TextRenderer.DrawText(g, Text, F2, new Rectangle(tx, 0, Width - tx - Font.Height, Height), Enabled ? Color.FromArgb(227, 227, 227) : Theme.Tertiary, Theme.LeftMid);
        }
    }

    // Кругла аватарка (або перша літера на кольоровому тлі)
    class Avatar : Control
    {
        public string ImagePath, Letter = "?";
        public Color Tint = Theme.Accent;

        public Avatar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            Margin = new Padding(0, 2, 12, 2);
        }

        public override Size GetPreferredSize(Size proposedSize) { int s = (int)(Font.Height * 2.4f); return new Size(s, s); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var back = Theme.BackOf(this);
            g.Clear(back);
            Theme.Smooth(g);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            var img = Thumbs.Get(ImagePath, r.Width, r.Height);
            if (img != null) g.DrawImage(img, r);
            else
            {
                using (var b = new SolidBrush(Theme.Mix(Theme.Card, Tint, 0.35f))) g.FillEllipse(b, r);
                TextRenderer.DrawText(g, Letter, Theme.Semi(Font.SizeInPoints * 1.2f), r, Tint, Theme.Center);
            }
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new Rectangle(-1, -1, Width + 2, Height + 2));
                path.AddEllipse(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f));
                path.FillMode = FillMode.Alternate;
                using (var b = new SolidBrush(back)) g.FillPath(b, path);
            }
        }
    }

    // Теплова карта «день тижня × година» (найкращий час публікації)
    class HeatMap : Control
    {
        public List<double> Scores;          // 168 значень: DayOfWeek(Нд=0) * 24 + година
        static readonly string[] Days = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Нд" };
        readonly ToolTip tip = new ToolTip();
        int hoverD = -1, hoverH = -1;

        public HeatMap()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            Margin = new Padding(3);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int w = proposedSize.Width > 0 && proposedSize.Width < 20000 ? proposedSize.Width : (int)(520 * Theme.Dpi);
            return new Size(w, Font.Height * 2 + (int)(7 * CellH()) + 6);
        }

        float LabelW { get { return Font.Height * 2.2f; } }
        float CellW() { return (Width - LabelW) / 24f; }
        float CellH() { return Font.Height * 1.45f; }

        static int Idx(int row) { return (row + 1) % 7; } // рядок 0 = Пн → DayOfWeek 1

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BackOf(this));
            if (Scores == null || Scores.Count != 168) return;
            Theme.Smooth(g);
            float cw = CellW(), ch = CellH(), lw = LabelW;
            var top3 = Enumerable.Range(0, 168).OrderByDescending(i => Scores[i]).Take(3).ToList();
            var small = Theme.F(Font.SizeInPoints * 0.82f);
            for (int r = 0; r < 7; r++)
            {
                TextRenderer.DrawText(g, Days[r], small, new Rectangle(0, (int)(r * ch), (int)lw, (int)ch), r >= 4 && r != 6 - 2 ? Theme.Secondary : Theme.Secondary, Theme.LeftMid);
                for (int h = 0; h < 24; h++)
                {
                    int i = Idx(r) * 24 + h;
                    double v = Math.Max(0, Math.Min(1, Scores[i]));
                    var c = Theme.Mix(Theme.FieldBg, Theme.Accent, (float)Math.Pow(v, 1.6));
                    var rc = new RectangleF(lw + h * cw + 1.5f, r * ch + 1.5f, cw - 3, ch - 3);
                    Theme.FillRound(g, c, rc, 3);
                    if (top3.Contains(i)) Theme.StrokeRound(g, Theme.Primary, 1.6f, rc, 3);
                    if (r == hoverD && h == hoverH) Theme.StrokeRound(g, Theme.Text, 1f, rc, 3);
                }
            }
            for (int h = 0; h < 24; h += 3)
                TextRenderer.DrawText(g, h.ToString("00"), small, new Rectangle((int)(lw + h * cw), (int)(7 * ch + 2), (int)(cw * 3), Font.Height + 4), Theme.Tertiary, Theme.LeftMid);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = (int)((e.X - LabelW) / CellW()), d = (int)(e.Y / CellH());
            if (h < 0 || h > 23 || d < 0 || d > 6 || Scores == null || Scores.Count != 168) { h = -1; d = -1; }
            if (h != hoverH || d != hoverD)
            {
                hoverH = h; hoverD = d;
                Invalidate();
                tip.SetToolTip(this, d < 0 ? null : Days[d] + " " + h.ToString("00") + ":00 — " + Math.Round(Scores[Idx(d) * 24 + h] * 100) + "% від найкращого часу");
            }
            base.OnMouseMove(e);
        }

        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }
}
