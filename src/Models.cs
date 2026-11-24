using System;
using System.Collections.Generic;

namespace AutoShorts
{
    public class Word
    {
        public double Start;
        public double End;
        public string Text;

        public Word() { }
        public Word(double s, double e, string t) { Start = s; End = e; Text = t; }
    }

    public class HeatPoint
    {
        public double Start;
        public double End;
        public double Value;
    }

    public class ChannelInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string RefreshTokenProtected { get; set; }
        public string ClientId { get; set; }               // клієнт Google, через який увійшли (токен прив’язаний до нього)
        public string ClientSecretProtected { get; set; }
        public string AvatarPath { get; set; }             // аватарка каналу (локальний файл)
        public string Handle { get; set; }                 // @нікнейм
        public DateTime VideosUpdatedUtc { get; set; }     // коли календар востаннє оновлювався з YouTube
        public DateTime QuotaUntilUtc { get; set; }        // до цього часу денний ліміт YouTube вичерпано
        public int ShortsPerDay { get; set; }
        /// <summary>168 значень: (день тижня за Києвом) * 24 + година.</summary>
        public List<double> HourScores { get; set; }
        public bool UsingFallback { get; set; }
        public int VideosAnalyzed { get; set; }
        public DateTime BestTimeUpdatedUtc { get; set; }

        public ChannelInfo() { ShortsPerDay = 2; UsingFallback = true; }
    }

    /// <summary>Відео каналу для календаря (кеш із YouTube).</summary>
    public class ChannelVideo
    {
        public string Id { get; set; }
        public string ChannelId { get; set; }
        public string Title { get; set; }
        public DateTime WhenUtc { get; set; }
        public string Privacy { get; set; }
        public bool Scheduled { get; set; }
        public bool IsShort { get; set; }
        public bool IsLive { get; set; }
        public int Seconds { get; set; }
    }

    public class PublishedItem
    {
        public string VideoId { get; set; }
        public string ChannelId { get; set; }
        public string Title { get; set; }
        public string SourceUrl { get; set; }
        public DateTime PublishAtUtc { get; set; }
        public DateTime UploadedUtc { get; set; }
    }

    public class ClipItem
    {
        public int Index { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
        public double Score { get; set; }
        public string Text { get; set; }
        public string Hook { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Tags { get; set; }
        public string VideoPath { get; set; }
        public string CoverPath { get; set; }
        public double FaceX { get; set; }
        public string Status { get; set; }
        public string VideoId { get; set; }
        public DateTime PublishAtUtc { get; set; }
        public bool Published { get; set; }
        public string StyleName { get; set; }
        // оцінка віральності 0–100 і її складові
        public int Virality { get; set; }
        public int ScoreHook { get; set; }
        public int ScoreViews { get; set; }
        public int ScoreEnergy { get; set; }
        public int ScoreTempo { get; set; }

        public double Duration { get { return End - Start; } }
        public double OutDuration { get; set; }
        public string FacePath { get; set; }             // траєкторія кадру за обличчям "t:x;…" (null — статичний)
        public string PendingChannelId { get; set; }
        public string SourceFile { get; set; }           // з якого файлу різали (для ефірів — частина запису)     // чекає на публікацію (вичерпано денний ліміт YouTube)
    }

    /// <summary>Набір обробки для проєкту (як «Recipe» в OpusClip). Усе працює без ШІ — FFmpeg і субтитри YouTube.</summary>
    public class Recipe
    {
        public bool Captions { get; set; }
        public string CaptionLang { get; set; }      // null — автоматично
        public string TranslateTo { get; set; }      // null — без перекладу (автопереклад YouTube)
        public string SrtPath { get; set; }          // свій файл субтитрів
        public string Keywords { get; set; }         // моменти, які треба включити (слова через кому)
        public bool Hook { get; set; }
        public string HookStyle { get; set; }
        public string Layout { get; set; }           // "crop" — кадр за обличчям, "fit" — ціле відео на розмитому фоні
        public bool EnhanceAudio { get; set; }
        public bool ColorBoost { get; set; }
        public bool AutoZoom { get; set; }
        public bool Fades { get; set; }
        public string LogoPath { get; set; }
        public string LogoPos { get; set; }
        public bool CutPauses { get; set; }          // вирізати паузи всередині кліпу (з ViralCut)
        public bool SkipAds { get; set; }            // не брати рекламні вставки (з ViralCut)
        public bool TrackFace { get; set; }          // кадр рухається за обличчям (з ViralCut)
        public string FontName { get; set; }         // шрифт субтитрів (null — зі стилю)
        public string Aspect { get; set; }           // "9:16", "1:1", "4:5", "16:9"
        public string SubPosition { get; set; }      // "Низ", "Центр", "Верх"
        public int SubSize { get; set; }             // розмір субтитрів у % (100 — як у стилі)

        public Recipe()
        {
            Captions = true; Hook = true; HookStyle = "Класичний"; Layout = "crop";
            EnhanceAudio = true; ColorBoost = false; AutoZoom = false; Fades = true; LogoPos = "Праворуч угорі";
            CutPauses = true; SkipAds = true; TrackFace = true;
            Aspect = "9:16"; SubPosition = "Низ"; SubSize = 100;
        }

        public static System.Drawing.Size OutSize(string aspect)
        {
            switch (aspect)
            {
                case "1:1": return new System.Drawing.Size(1080, 1080);
                case "4:5": return new System.Drawing.Size(1080, 1350);
                case "16:9": return new System.Drawing.Size(1920, 1080);
                default: return new System.Drawing.Size(1080, 1920);
            }
        }

        public Recipe Clone() { return (Recipe)MemberwiseClone(); }
    }

    public class Job
    {
        public string Url { get; set; }
        public string VideoId { get; set; }
        public string SourceTitle { get; set; }
        public string SourceChannel { get; set; }
        public List<string> SourceTags { get; set; }
        public double SourceDuration { get; set; }
        public string ChannelId { get; set; }
        public string StyleName { get; set; }
        public int ClipCount { get; set; }
        public bool AutoPublish { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public bool Done { get; set; }
        public string Error { get; set; }
        public string OutputDir { get; set; }
        public List<ClipItem> Clips { get; set; }
        // відрізок відео для пошуку моментів (0 — усе відео) і своя довжина кліпів (0 — з налаштувань)
        public double RangeStart { get; set; }
        public double RangeEnd { get; set; }
        public int MinLen { get; set; }
        public int MaxLen { get; set; }
        public string ThumbPath { get; set; }
        public string CaptionFile { get; set; }
        public Recipe Recipe { get; set; }
        public DateTime AddedUtc { get; set; }
        // прямий ефір: запис і нарізка під час трансляції
        public bool IsLive { get; set; }
        public int LiveMaxMinutes { get; set; }      // 0 — до кінця ефіру
        public double LiveProcessed { get; set; }    // до якої секунди запису вже шукали моменти
        public int CamX { get; set; }                // вікно камери стримера в кадрі (0 — не знайдено)
        public int CamY { get; set; }
        public int CamW { get; set; }
        public int CamH { get; set; }

        public bool IsLocal()
        {
            return !string.IsNullOrEmpty(Url) && !Url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        }

        public Job()
        {
            Clips = new List<ClipItem>();
            SourceTags = new List<string>();
            Status = "В черзі";
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(SourceTitle)) return SourceTitle;
                if (IsLocal()) { try { return System.IO.Path.GetFileNameWithoutExtension(Url); } catch { } }
                return Url;
            }
        }
    }
}
