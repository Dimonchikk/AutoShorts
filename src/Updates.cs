using System;
using System.Threading.Tasks;

namespace AutoShorts
{
    static class Updates
    {
        public const string ReleasesUrl = "https://github.com/" + AppVersion.Repo + "/releases/latest";

        /// <summary>Повертає (версія, посилання) якщо на GitHub є новіша версія, інакше null.</summary>
        public static async Task<Tuple<string, string>> CheckAsync()
        {
            try
            {
                var body = await Net.Client.GetStringAsync("https://api.github.com/repos/" + AppVersion.Repo + "/releases/latest");
                var o = Json.Parse(body);
                var tag = (Json.Str(o, "tag_name") ?? "").TrimStart('v', 'V');
                var url = Json.Str(o, "html_url") ?? ReleasesUrl;
                foreach (var a in Json.Arr(o, "assets"))
                {
                    var name = Json.Str(a, "name") ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { url = Json.Str(a, "browser_download_url") ?? url; break; }
                }
                Version latest, current;
                if (Version.TryParse(tag, out latest) && Version.TryParse(AppVersion.Text, out current) && latest > current)
                    return Tuple.Create(tag, url);
            }
            catch { }
            return null;
        }
    }

    static class Guide
    {
        public const string ConsoleUrl = "https://console.cloud.google.com/apis/credentials";

        public const string Text =
"Як підключити YouTube (робиться ОДИН раз, безкоштовно)\r\n" +
"=====================================================\r\n\r\n" +
"1. Відкрийте https://console.cloud.google.com/ і створіть новий проєкт (наприклад «AutoShorts»).\r\n\r\n" +
"2. APIs & Services → Library: знайдіть і увімкніть (Enable):\r\n" +
"   • YouTube Data API v3\r\n" +
"   • YouTube Analytics API\r\n\r\n" +
"3. APIs & Services → OAuth consent screen (Google Auth Platform):\r\n" +
"   • User type: External, назва програми — будь-яка, вкажіть свій e-mail.\r\n" +
"   • Audience → Test users: додайте всі Google-акаунти, з якими будете входити в канали.\r\n" +
"   • Порада: натисніть «Publish app» (In production), інакше Google вимагатиме\r\n" +
"     повторний вхід кожні 7 днів. Під час входу буде попередження\r\n" +
"     «Google не перевірив цю програму» → «Додатково» → «Перейти до …».\r\n\r\n" +
"4. APIs & Services → Credentials → Create credentials → OAuth client ID:\r\n" +
"   • Application type: Desktop app\r\n" +
"   • Скопіюйте Client ID і Client secret у вкладку «Налаштування» цієї програми.\r\n\r\n" +
"5. Вкладка «Канали» → «Додати канал» → увійдіть у потрібний акаунт / канал.\r\n" +
"   Для кількох каналів повторіть вхід для кожного.\r\n\r\n" +
"ВАЖЛИВО про обмеження YouTube API:\r\n" +
"   • Поки проєкт Google Cloud не пройшов перевірку (аудит) YouTube, відео,\r\n" +
"     завантажені через API, YouTube залишає ПРИВАТНИМИ — запланована публікація\r\n" +
"     не спрацює автоматично. Подайте безкоштовну форму аудиту:\r\n" +
"     https://support.google.com/youtube/contact/yt_api_form\r\n" +
"     До проходження аудиту можна вручну зробити відео публічним у YouTube Studio.\r\n" +
"   • Денний ліміт — 10 000 одиниць, одне завантаження ≈ 1 600, тобто ≈ 6 шортсів на день\r\n" +
"     на один проєкт (для всіх каналів разом). Більше — через ту ж форму.\r\n";
    }
}
