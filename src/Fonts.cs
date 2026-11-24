using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    /// <summary>Шрифти субтитрів (з ViralCut, OFL). Файл завантажується з репозиторію лише тоді, коли шрифт вибрано.</summary>
    static class Fonts
    {
        public class FontInfo
        {
            public readonly string Label, File, Family;
            public readonly bool Bold;   // для шрифтів, де жирність — окреме накреслення
            public FontInfo(string label, string file, string family, bool bold) { Label = label; File = file; Family = family; Bold = bold; }
            public override string ToString() { return Label; }
        }

        public const string FromStyle = "Як у стилі";
        const string BaseUrl = "https://raw.githubusercontent.com/Dimonchikk/AutoShorts/main/fonts/";

        public static readonly List<FontInfo> All = new List<FontInfo>
        {
            new FontInfo("Montserrat ExtraBold", "Montserrat-ExtraBold.ttf", "Montserrat ExtraBold", false),
            new FontInfo("Montserrat Bold", "Montserrat-Bold.ttf", "Montserrat", true),
            new FontInfo("Montserrat SemiBold", "Montserrat-SemiBold.ttf", "Montserrat SemiBold", false),
            new FontInfo("Inter Black", "Inter-Black.ttf", "Inter Black", false),
            new FontInfo("Rubik ExtraBold", "Rubik-ExtraBold.ttf", "Rubik ExtraBold", false),
            new FontInfo("Unbounded", "Unbounded-Bold.ttf", "Unbounded Bold", false),
            new FontInfo("Onest", "Onest-ExtraBold.ttf", "Onest ExtraBold", false),
            new FontInfo("Manrope", "Manrope-ExtraBold.ttf", "Manrope ExtraBold", false),
            new FontInfo("Golos Text", "GolosText-Bold.ttf", "Golos Text Bold", false),
            new FontInfo("Geologica", "Geologica-Bold.ttf", "Geologica", true),
            new FontInfo("Lato Black", "Lato-Black.ttf", "Lato Black", false),
            new FontInfo("Oswald", "Oswald-Bold.ttf", "Oswald Bold", false),
            new FontInfo("Roboto Condensed", "RobotoCondensed-Bold.ttf", "Roboto Condensed Bold", false),
            new FontInfo("PT Sans Narrow", "PT_Sans-Narrow-Web-Bold.ttf", "PT Sans Narrow", true),
            new FontInfo("IBM Plex Sans Condensed", "IBMPlexSansCondensed-SemiBold.ttf", "IBM Plex Sans Cond SmBld", false),
            new FontInfo("Exo 2", "Exo2-Bold.ttf", "Exo 2 Bold", false),
            new FontInfo("Play", "Play-Bold.ttf", "Play", true),
            new FontInfo("Philosopher", "Philosopher-Bold.ttf", "Philosopher", true),
            new FontInfo("Comfortaa", "Comfortaa-Bold.ttf", "Comfortaa Bold", false),
            new FontInfo("Russo One", "RussoOne-Regular.ttf", "Russo One", false),
            new FontInfo("Rubik Mono One", "RubikMonoOne-Regular.ttf", "Rubik Mono One", false),
            new FontInfo("Stalinist One", "StalinistOne-Regular.ttf", "Stalinist One", false),
            new FontInfo("Ruslan Display", "RuslanDisplay-Regular.ttf", "Ruslan Display", false),
            new FontInfo("Yeseva One", "YesevaOne-Regular.ttf", "Yeseva One", false),
            new FontInfo("Underdog", "Underdog-Regular.ttf", "Underdog", false),
            new FontInfo("Marmelad", "Marmelad-Regular.ttf", "Marmelad", false),
            new FontInfo("Caveat", "Caveat-Bold.ttf", "Caveat Bold", false),
            new FontInfo("Bad Script", "BadScript-Regular.ttf", "Bad Script", false),
            new FontInfo("Marck Script", "MarckScript-Regular.ttf", "Marck Script", false),
        };

        public static FontInfo Find(string label)
        {
            if (string.IsNullOrEmpty(label) || label == FromStyle) return null;
            return All.FirstOrDefault(f => f.Label == label);
        }

        static string Dir
        {
            get { var d = Path.Combine(AppSettings.LocalDir, "fonts"); Directory.CreateDirectory(d); return d; }
        }

        public static string LocalPath(FontInfo f) { return Path.Combine(Dir, f.File); }

        /// <summary>Повертає шлях до файлу шрифту, завантаживши його за потреби.</summary>
        public static async Task<string> EnsureAsync(FontInfo f, CancellationToken ct)
        {
            var path = LocalPath(f);
            if (System.IO.File.Exists(path) && new FileInfo(path).Length > 10000) return path;
            using (var resp = await Net.Client.GetAsync(BaseUrl + f.File, ct))
            {
                if (!resp.IsSuccessStatusCode) throw new Exception("Не вдалося завантажити шрифт " + f.Label + " (" + (int)resp.StatusCode + ")");
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                var tmp = path + ".part";
                System.IO.File.WriteAllBytes(tmp, bytes);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                System.IO.File.Move(tmp, path);
            }
            return path;
        }
    }
}
