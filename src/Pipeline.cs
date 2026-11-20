using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    /// <summary>Повний цикл: посилання → завантаження → субтитри → вибір моментів → вертикальні кліпи → обкладинки → назви.</summary>
    class Pipeline
    {
        readonly AppSettings settings;
        readonly Action<string> log;
        readonly Action<Job> changed;

        public Pipeline(AppSettings s, Action<string> log, Action<Job> changed)
        {
            settings = s; this.log = log; this.changed = changed;
        }

        void Status(Job job, string text, int progress)
        {
            job.Status = text;
            job.Progress = Math.Max(0, Math.Min(100, progress));
            changed(job);
        }

        static string SafeName(string s)
        {
            s = Regex.Replace(s ?? "video", "[\\\\/:*?\"<>|\\r\\n\\t]", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            if (s.Length > 60) s = s.Substring(0, 60).Trim();
            return s.Length == 0 ? "video" : s;
        }

        public async Task ProcessAsync(Job job, CancellationToken ct)
        {
            job.Error = null;
            Status(job, "Підготовка інструментів", 1);
            await Tools.EnsureAsync(log, ct);

            Status(job, "Читаю інформацію про відео", 3);
            var info = await Downloader.GetInfoAsync(job.Url, ct);
            job.VideoId = info.Id;
            job.SourceTitle = info.Title;
            job.SourceChannel = info.Channel;
            job.SourceTags = info.Tags;
            job.SourceDuration = info.Duration;
            log("Відео: " + info.Title + " (" + TimeSpan.FromSeconds(info.Duration).ToString(@"h\:mm\:ss") + ")" +
                (info.Heatmap.Count > 0 ? ", є графік «найчастіше переглядають»" : ", графіка переглядів немає"));
            var work = Downloader.WorkDir(info.Id);

            Status(job, "Субтитри", 5);
            List<Word> words;
            try { words = await Downloader.GetWordsAsync(info, work, ct); }
            catch (Exception ex) { words = new List<Word>(); log("Не вдалося отримати субтитри: " + ex.Message); }
            if (words.Count == 0) log("Субтитрів немає — кліпи будуть без тексту.");
            else log("Субтитри (" + info.CaptionLang + (info.CaptionAuto ? ", автоматичні" : "") + "): " + words.Count + " слів");

            Status(job, "Завантаження відео", 6);
            var video = await Downloader.DownloadVideoAsync(info, work, p => Status(job, "Завантаження відео " + p + "%", 6 + p * 34 / 100), ct);

            Status(job, "Аналіз гучності", 41);
            var loud = await Highlights.LoudnessAsync(video, work, ct);

            Status(job, "Пошук цікавих моментів", 45);
            int count = job.ClipCount > 0 ? job.ClipCount : settings.ClipsPerVideo;
            var clips = Highlights.Find(Math.Max(info.Duration, loud.Length), info.Heatmap, loud, words, count, settings.MinSeconds, settings.MaxSeconds);
            if (clips.Count == 0) throw new Exception("Не знайшлося фрагментів потрібної довжини.");
            log("Знайдено фрагментів: " + clips.Count);

            job.OutputDir = Path.Combine(settings.OutputFolder, SafeName(info.Title) + " [" + info.Id + "]");
            Directory.CreateDirectory(job.OutputDir);
            job.Clips = clips;
            var size = await Renderer.ProbeSizeAsync(video, ct);

            for (int i = 0; i < clips.Count; i++)
            {
                int baseP = 46 + i * 54 / clips.Count;
                int span = 54 / clips.Count;
                await BuildClipAsync(job, clips[i], video, size, words, work, p => Status(job, "Кліп " + (i + 1) + "/" + clips.Count + ": " + p + "%", baseP + p * span / 100), ct);
            }
            job.Done = true;
            Status(job, job.AutoPublish ? "Готово, публікую" : "Готово — перегляньте кліпи", 100);
        }

        public async Task BuildClipAsync(Job job, ClipItem clip, string video, Size size, List<Word> words, string work,
                                         Action<int> progress, CancellationToken ct)
        {
            clip.Status = "Рендер";
            changed(job);
            var clipWords = Captions.Slice(words, clip.Start, clip.End);
            clip.Text = Captions.JoinText(clipWords);
            Metadata.Fill(clip, job, settings);

            var framesDir = Path.Combine(work, "frames_" + clip.Index);
            var frames = await Renderer.ExtractFramesAsync(video, clip.Start, clip.Duration, framesDir, ct);
            clip.FaceX = -1;
            if (settings.FaceFraming && FaceFrame.Available)
            {
                try { clip.FaceX = await FaceFrame.CenterXAsync(frames); }
                catch (Exception ex) { log("Пошук облич не спрацював: " + ex.Message); }
            }
            var style = SubtitleStyles.Get(job.StyleName ?? settings.SubtitleStyle);
            clip.VideoPath = Path.Combine(job.OutputDir, "short_" + clip.Index.ToString("00") + ".mp4");
            await Renderer.RenderAsync(video, size, clip, clipWords, style, settings.HookTitle, clip.VideoPath, progress, ct);

            try
            {
                int bi = Renderer.BrightestFrame(frames);
                double at = bi < 0 ? clip.Duration / 3 : Math.Min(clip.Duration - 0.5, bi * Math.Max(1, clip.Duration / 30) + 0.5);
                clip.CoverPath = Path.Combine(job.OutputDir, "cover_" + clip.Index.ToString("00") + ".jpg");
                await Renderer.MakeCoverAsync(video, size, clip, Math.Max(0, at), clip.Hook, clip.CoverPath, ct);
            }
            catch (Exception ex)
            {
                clip.CoverPath = null;
                log("Обкладинка: " + ex.Message);
            }
            clip.Status = "Готовий";
            changed(job);
        }

        public async Task RerenderAsync(Job job, ClipItem clip, CancellationToken ct)
        {
            await Tools.EnsureAsync(log, ct);
            var work = Downloader.WorkDir(job.VideoId);
            var video = Path.Combine(work, "source.mp4");
            if (!File.Exists(video)) throw new Exception("Вихідне відео видалено з кешу. Додайте посилання знову.");
            List<Word> words = new List<Word>();
            var cap = Directory.GetFiles(work, "captions.*.json3").FirstOrDefault();
            if (cap != null) words = Captions.ParseJson3(File.ReadAllText(cap));
            var size = await Renderer.ProbeSizeAsync(video, ct);
            string title = clip.Title, desc = clip.Description, tags = clip.Tags;
            await BuildClipAsync(job, clip, video, size, words, work, p => { clip.Status = "Рендер " + p + "%"; changed(job); }, ct);
            // залишаємо тексти, які користувач відредагував
            if (!string.IsNullOrEmpty(title)) clip.Title = title;
            if (!string.IsNullOrEmpty(desc)) clip.Description = desc;
            if (!string.IsNullOrEmpty(tags)) clip.Tags = tags;
        }

        public async Task PublishAsync(Job job, ClipItem clip, ChannelInfo ch, CancellationToken ct)
        {
            if (clip.Published) return;
            if (clip.VideoPath == null || !File.Exists(clip.VideoPath)) throw new Exception("Файл кліпу не знайдено.");
            var api = await YouTubeApi.ForChannelAsync(settings, ch);
            var taken = settings.Published.Where(p => p.ChannelId == ch.Id).Select(p => p.PublishAtUtc);
            var slot = BestTime.NextSlotUtc(ch, taken, DateTime.UtcNow);
            clip.Status = "Завантаження на YouTube";
            changed(job);
            log("Публікую «" + clip.Title + "» на " + ch.Title + ", заплановано: " + BestTime.Format(slot));
            var id = await api.UploadAsync(clip.VideoPath, clip.Title, clip.Description, Metadata.TagList(clip.Tags), slot,
                p => { clip.Status = "YouTube " + p + "%"; changed(job); }, ct);
            clip.VideoId = id;
            clip.PublishAtUtc = slot;
            clip.Published = true;
            clip.Status = "Заплановано " + BestTime.Format(slot);
            var item = new PublishedItem();
            item.VideoId = id; item.ChannelId = ch.Id; item.Title = clip.Title; item.SourceUrl = job.Url;
            item.PublishAtUtc = slot; item.UploadedUtc = DateTime.UtcNow;
            settings.Published.Add(item);
            settings.Save();
            changed(job);
            if (settings.UploadCover && clip.CoverPath != null && File.Exists(clip.CoverPath))
            {
                try { await api.SetThumbnailAsync(id, clip.CoverPath); }
                catch (Exception ex) { log("Обкладинку не встановлено (потрібен підтверджений канал): " + ex.Message); }
            }
            log("Готово: https://youtube.com/shorts/" + id);
        }
    }
}
