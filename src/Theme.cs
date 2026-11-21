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
        // Темна тема в дусі OpusClip: майже чорний фон, графітові картки, білі головні кнопки, лаймовий акцент
        public static readonly Color Bg = Color.FromArgb(14, 14, 16);
        public static readonly Color SidebarBg = Color.FromArgb(18, 18, 20);
        public static readonly Color Card = Color.FromArgb(26, 26, 29);
        public static readonly Color CardHover = Color.FromArgb(33, 33, 37);
        public static readonly Color FieldBg = Color.FromArgb(19, 19, 22);
        public static readonly Color Border = Color.FromArgb(42, 42, 47);
        public static readonly Color FieldBorder = Color.FromArgb(54, 54, 60);
        public static readonly Color Text = Color.FromArgb(244, 244, 245);
        public static readonly Color Secondary = Color.FromArgb(161, 161, 170);
        public static readonly Color Tertiary = Color.FromArgb(105, 105, 114);
        public static readonly Color Accent = Color.FromArgb(190, 242, 100);
        public static readonly Color AccentHover = Color.FromArgb(205, 248, 130);
        public static readonly Color AccentPressed = Color.FromArgb(163, 214, 76);
        public static readonly Color AccentSoft = Color.FromArgb(44, 52, 33);
        public static readonly Color Primary = Color.FromArgb(250, 250, 250);
        public static readonly Color PrimaryHover = Color.FromArgb(228, 228, 231);
        public static readonly Color PrimaryPressed = Color.FromArgb(200, 200, 205);
        public static readonly Color Fill = Color.FromArgb(39, 39, 43);
        public static readonly Color FillHover = Color.FromArgb(50, 50, 55);
        public static readonly Color FillPressed = Color.FromArgb(62, 62, 68);
        public static readonly Color Green = Color.FromArgb(74, 222, 128);
        public static readonly Color SwitchOn = Color.FromArgb(190, 242, 100);
        public static readonly Color Red = Color.FromArgb(248, 113, 113);
        public static readonly Color Orange = Color.FromArgb(251, 146, 60);
        public static readonly Color Blue = Color.FromArgb(96, 165, 250);
        public static readonly Color Stripe = Color.FromArgb(30, 30, 33);

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
                int off = 1, round = 2, cap = ColorRef(SidebarBg), txt = ColorRef(Text), border = ColorRef(Border);
                DwmSetWindowAttribute(f.Handle, 20, ref off, 4); // темний заголовок
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
        public const string Copy = "\uE8C8", Mail = "\uE715";
        public const string Download = "", Video = "", People = "", Chart = "", Settings = "",
            Add = "", Play = "", Stop = "", Refresh = "", Delete = "", Folder = "",
            Upload = "", Help = "", Save = "", Check = "", Clear = "", Calendar = "", Link = "",
            Home = "", Clips = "", Log = "", Back = "", Expand = "", Collapse = "", Down = "",
            File = "", Cut = "", Sparkle = "", Clock = "", Captions = "", Pause = "",
            More = "", Sort = "", Prev = "", Next = "", Open = "";
    }
}
