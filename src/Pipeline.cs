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

        string encoder;          // яким кодувальником робимо кліпи (вибирається на початку проєкту)

        async Task PrepareEncoderAsync(CancellationToken ct)
        {
            Tools.Power = settings.Power;
            var e = await Encoders.PickAsync(settings.Encoder, ct);
            if (e != encoder) log("Кодування: " + Encoders.Label(e) + (settings.Power < 100 ? ", потужність " + settings.Power + "%" : ""));
            encoder = e;
        }

        static Rectangle CamOf(Job job)
        {
            return job.CamW > 0 ? new Rectangle(job.CamX, job.CamY, job.CamW, job.CamH) : Rectangle.Empty;
        }

        /// <summary>Шукає вікно камери стримера на 12 кадрах з різних місць відео.</summary>
        async Task DetectCamAsync(Job job, string video, Size size, double from, double to, string work, CancellationToken ct)
        {
            job.CamX = job.CamY = job.CamW = job.CamH = 0;
            if (!FaceFrame.Available) { log("Макет «Стрім»: детектор облич Windows недоступний — буде звичайний кадр."); return; }
            var dir = Path.Combine(work, "cam");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.jpg")) { try { File.Delete(f); } catch { } }
            var frames = new List<string>();
            double len = Math.Max(1, to - from);
            for (int i = 0; i < 12; i++)
            {
                double t = from + len * (i + 0.5) / 12;
                var p = Path.Combine(dir, "c_" + i.ToString("00") + ".jpg");
                await Tools.RunAsync(Tools.FFmpeg, "-y -v error -ss " + t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                    " -i " + Tools.Q(video) + " -frames:v 1 -vf scale=1280:-2 -q:v 3 " + Tools.Q(p), null, null, ct);
                if (File.Exists(p)) frames.Add(p);
            }
            var boxes = await FaceFrame.FaceBoxesAsync(frames);
            var cam = FaceFrame.FindCam(boxes);
            if (cam.Width <= 0) { log("Макет «Стрім»: вікна камери не знайдено — буде звичайний кадр за обличчям."); return; }
            job.CamX = (int)(cam.X * size.Width); job.CamY = (int)(cam.Y * size.Height);
            job.CamW = (int)(cam.Width * size.Width); job.CamH = (int)(cam.Height * size.Height);
            log("Знайдено камеру стримера: " + job.CamW + "×" + job.CamH + " у точці " + job.CamX + "," + job.CamY);
        }

        void Status(Job job, string text, int progress)
        {
            job.Status = text;
            job.Progress = Math.Max(0, Math.Min(100, progress));
            changed(job);
        }

        Recipe RecipeOf(Job job)
        {
            if (job.Recipe == null)
            {
                job.Recipe = settings.Recipe != null ? settings.Recipe.Clone() : new Recipe();
                job.Recipe.Hook = settings.HookTitle;
            }
            return job.Recipe;
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
            await PrepareEncoderAsync(ct);

            VideoInfo info;
            List<Word> words;
            string work, video;
            if (job.IsLocal())
            {
                Status(job, "Читаю файл", 3);
                if (!File.Exists(job.Url)) throw new Exception("Файл не знайдено: " + job.Url);
                var fi = new FileInfo(job.Url);
                info = new VideoInfo
                {
                    Id = "local_" + ((uint)(fi.FullName.ToLowerInvariant() + fi.Length + fi.LastWriteTimeUtc.Ticks).GetHashCode()).ToString("x8"),
                    Title = Path.GetFileNameWithoutExtension(fi.Name),
                    Url = fi.FullName,
                    Channel = "",
                    Description = ""
                };
                job.VideoId = info.Id;
                job.SourceTitle = info.Title;
                job.SourceChannel = "";
                work = Downloader.WorkDir(info.Id);
                Status(job, "Готую відео", 5);
                video = await Downloader.ImportLocalAsync(fi.FullName, work, ct);
                info.Duration = await Renderer.ProbeDurationAsync(video, ct);
                job.SourceDuration = info.Duration;
                var rcl = RecipeOf(job);
                words = !string.IsNullOrEmpty(rcl.SrtPath) && File.Exists(rcl.SrtPath) ? Captions.ParseSrtFile(rcl.SrtPath) : Captions.FromSidecar(fi.FullName);
                                log("Файл: " + info.Title + " (" + TimeSpan.FromSeconds(info.Duration).ToString(@"h\:mm\:ss") + ")" +
                    (words.Count > 0 ? ", субтитри з .srt: " + words.Count + " слів" : ", без субтитрів (покладіть поруч .srt з такою ж назвою, щоб були)"));
                try { await Thumbs.FromVideoAsync(job, video, ct); } catch { }
                changed(job);
            }
            else
            {
                Status(job, "Читаю інформацію про відео", 3);
                info = await Downloader.GetInfoAsync(job.Url, ct);
                job.VideoId = info.Id;
                job.SourceTitle = info.Title;
                job.SourceChannel = info.Channel;
                job.SourceTags = info.Tags;
                job.SourceDuration = info.Duration;
                log("Відео: " + info.Title + " (" + TimeSpan.FromSeconds(info.Duration).ToString(@"h\:mm\:ss") + ")" +
                    (info.Heatmap.Count > 0 ? ", є графік «найчастіше переглядають»" : ", графіка переглядів немає"));
                work = Downloader.WorkDir(info.Id);
                try { await Thumbs.EnsureAsync(job, ct); } catch { }
                changed(job);
                if (info.IsLive)
                {
                    job.IsLive = true;
                    await LiveAsync(job, info, work, ct);
                    return;
                }
                if (job.IsLive && job.Clips.Count > 0)
                {
                    // ефір, який записували, уже завершився — кліпи готові
                    job.Done = true;
                    Status(job, "Ефір завершився — " + job.Clips.Count + " кліпів", 100);
                    return;
                }

                Status(job, "Субтитри", 5);
                var rc = RecipeOf(job);
                if (!string.IsNullOrEmpty(rc.SrtPath) && File.Exists(rc.SrtPath))
                {
                    words = Captions.ParseSrtFile(rc.SrtPath);
                    job.CaptionFile = rc.SrtPath;
                    log("Субтитри зі свого файлу: " + Path.GetFileName(rc.SrtPath) + " (" + words.Count + " слів)");
                }
                else
                {
                    if (!string.IsNullOrEmpty(rc.CaptionLang) || !string.IsNullOrEmpty(rc.TranslateTo))
                        Downloader.ChooseCaptions(info, rc.CaptionLang, rc.TranslateTo);
                    try { words = await Downloader.GetWordsAsync(info, work, ct); }
                    catch (Exception ex) { words = new List<Word>(); log("Не вдалося отримати субтитри: " + ex.Message); }
                    if (info.CaptionUrl != null) job.CaptionFile = Path.Combine(work, "captions." + info.CaptionLang + ".json3");
                    if (!string.IsNullOrEmpty(rc.TranslateTo) && (info.CaptionLang ?? "").EndsWith("-tr"))
                        log("Субтитри перекладено автоперекладом YouTube: " + rc.TranslateTo);
                }
                if (words.Count == 0) log("Субтитрів немає — кліпи будуть без тексту.");
                else log("Субтитри (" + info.CaptionLang + (info.CaptionAuto ? ", автоматичні" : "") + "): " + words.Count + " слів");

                Status(job, "Завантаження відео", 6);
                video = await Downloader.DownloadVideoAsync(info, work, p => Status(job, "Завантаження відео " + p + "%", 6 + p * 34 / 100), ct);
            }

            Status(job, "Аналіз гучності", 41);
            var loud = await Highlights.LoudnessAsync(video, work, ct);

            Status(job, "Пошук цікавих моментів", 45);
            int count = job.ClipCount > 0 ? job.ClipCount : settings.ClipsPerVideo;
            int minLen = job.MinLen > 0 ? job.MinLen : settings.MinSeconds, maxLen = job.MaxLen > 0 ? job.MaxLen : settings.MaxSeconds;
            var clips = Highlights.Find(Math.Max(info.Duration, loud.Length), info.Heatmap, loud, words, count, minLen, maxLen, job.RangeStart, job.RangeEnd, RecipeOf(job).Keywords, RecipeOf(job).SkipAds);
            if (Highlights.AdBlocksFound > 0) log("Пропущено рекламних вставок: " + Highlights.AdBlocksFound);
            if (clips.Count == 0) throw new Exception("Не знайшлося фрагментів потрібної довжини.");
            if (clips.Count < count)
                log("Знайдено фрагментів: " + clips.Count + " з " + count + " — довжини відео вистачило лише на стільки кліпів без повторів.");
            else log("Знайдено фрагментів: " + clips.Count);

            job.OutputDir = Path.Combine(settings.OutputFolder, SafeName(info.Title) + " [" + info.Id + "]");
            Directory.CreateDirectory(job.OutputDir);
            job.Clips = clips;
            var size = await Renderer.ProbeSizeAsync(video, ct);
            if (RecipeOf(job).Layout == "stream")
                await DetectCamAsync(job, video, size, job.RangeStart, job.RangeEnd > 0 ? job.RangeEnd : info.Duration, work, ct);

            for (int i = 0; i < clips.Count; i++)
            {
                double baseP = 46 + i * 54.0 / clips.Count;
                double span = 54.0 / clips.Count;
                int n = i + 1;
                await BuildClipAsync(job, clips[i], video, size, words, work, p => Status(job, "Кліп " + n + "/" + clips.Count + ": " + p + "%", (int)(baseP + p * span / 100)), ct);
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

            var recipe = RecipeOf(job);
            clip.SourceFile = video;
            var cam = recipe.Layout == "stream" ? CamOf(job) : Rectangle.Empty;
            bool track = recipe.TrackFace && recipe.Layout != "fit" && cam.Width == 0 && settings.FaceFraming && FaceFrame.Available;
            // для руху кадру за обличчям — кадр щосекунди (щопівсекунди на коротких кліпах)
            double step = track ? Math.Max(0.5, clip.Duration / 60) : Math.Max(1, clip.Duration / 30);
            var framesDir = Path.Combine(work, "frames_" + clip.Index);
            var frames = await Renderer.ExtractFramesAsync(video, clip.Start, clip.Duration, framesDir, ct, step);
            clip.FaceX = -1;
            clip.FacePath = null;
            if (settings.FaceFraming && FaceFrame.Available && cam.Width == 0)
            {
                try
                {
                    if (track)
                    {
                        var xs = await FaceFrame.TrackAsync(frames);
                        clip.FacePath = FaceFrame.BuildPath(xs, step);
                        var found = xs.Where(x => x >= 0).OrderBy(x => x).ToList();
                        if (found.Count >= Math.Max(1, xs.Count / 5)) clip.FaceX = found[found.Count / 2];
                    }
                    else clip.FaceX = await FaceFrame.CenterXAsync(frames);
                }
                catch (Exception ex) { log("Пошук облич не спрацював: " + ex.Message); }
            }
            var style = SubtitleStyles.Get(clip.StyleName ?? job.StyleName ?? settings.SubtitleStyle);
            clip.VideoPath = Path.Combine(job.OutputDir, "short_" + clip.Index.ToString("00") + ".mp4");
            if (encoder == null) await PrepareEncoderAsync(ct);
            await Renderer.RenderAsync(video, size, clip, clipWords, style, recipe, clip.VideoPath, progress, ct, encoder, cam);
            if (clip.OutDuration > 0 && clip.OutDuration < clip.Duration - 0.3)
                log("Кліп " + clip.Index + ": вирізано паузи — " + clip.Duration.ToString("0.0") + " → " + clip.OutDuration.ToString("0.0") + " с");

            try
            {
                int bi = Renderer.BrightestFrame(frames);
                double at = bi < 0 ? clip.Duration / 3 : Math.Min(clip.Duration - 0.5, bi * step + 0.5);
                clip.CoverPath = Path.Combine(job.OutputDir, "cover_" + clip.Index.ToString("00") + ".jpg");
                await Renderer.MakeCoverAsync(video, size, clip, Math.Max(0, at), clip.Hook, clip.CoverPath, ct, recipe, cam);
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
            await PrepareEncoderAsync(ct);
            var work = Downloader.WorkDir(job.VideoId);
            var video = !string.IsNullOrEmpty(clip.SourceFile) && File.Exists(clip.SourceFile) ? clip.SourceFile : Path.Combine(work, "source.mp4");
            if (!File.Exists(video)) throw new Exception("Вихідне відео видалено з кешу. Додайте посилання знову.");
            List<Word> words = new List<Word>();
            var rcr = RecipeOf(job);
            if (!string.IsNullOrEmpty(rcr.SrtPath) && File.Exists(rcr.SrtPath)) words = Captions.ParseSrtFile(rcr.SrtPath);
            else if (job.IsLocal()) words = Captions.FromSidecar(job.Url);
            else if (job.CaptionFile != null && File.Exists(job.CaptionFile)) words = Captions.ParseJson3(File.ReadAllText(job.CaptionFile));
            else
            {
                var cap = Directory.GetFiles(work, "captions.*.json3").FirstOrDefault();
                if (cap != null) words = Captions.ParseJson3(File.ReadAllText(cap));
            }
            var size = await Renderer.ProbeSizeAsync(video, ct);
            string title = clip.Title, desc = clip.Description, tags = clip.Tags;
            await BuildClipAsync(job, clip, video, size, words, work, p => { clip.Status = "Рендер " + p + "%"; changed(job); }, ct);
            // залишаємо тексти, які користувач відредагував
            if (!string.IsNullOrEmpty(title)) clip.Title = title;
            if (!string.IsNullOrEmpty(desc)) clip.Description = desc;
            if (!string.IsNullOrEmpty(tags)) clip.Tags = tags;
        }

        const double LiveWindow = 600;   // аналізуємо ефір шматками по 10 хвилин

        /// <summary>
        /// Прямий ефір (з ViralCut live.py): FFmpeg пише потік у файл, а кожні 10 хвилин запису
        /// шукаємо найгучніші моменти й одразу робимо з них кліпи. Субтитрів під час ефіру YouTube не дає.
        /// </summary>
        async Task LiveAsync(Job job, VideoInfo info, string work, CancellationToken ct)
        {
            var recipe = RecipeOf(job);
            int perHour = job.ClipCount > 0 ? job.ClipCount : settings.ClipsPerVideo;
            int minLen = job.MinLen > 0 ? job.MinLen : Math.Min(settings.MinSeconds, 30), maxLen = job.MaxLen > 0 ? job.MaxLen : Math.Min(settings.MaxSeconds, 60);
            job.OutputDir = Path.Combine(settings.OutputFolder, SafeName(info.Title) + " [" + info.Id + "]");
            Directory.CreateDirectory(job.OutputDir);
            var startedUtc = DateTime.UtcNow;
            var deadline = job.LiveMaxMinutes > 0 ? startedUtc.AddMinutes(job.LiveMaxMinutes) : DateTime.MaxValue;
            log("Прямий ефір: записую і нарізаю по ходу (" + perHour + " кліпів на годину ефіру" +
                (job.LiveMaxMinutes > 0 ? ", до " + job.LiveMaxMinutes + " хв" : ", до кінця трансляції") + "). Субтитрів під час ефіру немає — моменти шукаю за звуком.");

            int part = Directory.GetFiles(work, "live_*.ts").Length;
            bool ended = false;
            while (!ended && !ct.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                part++;
                var file = Path.Combine(work, "live_" + part.ToString("00") + ".ts");
                Status(job, "Підключаюсь до ефіру", 2);
                string stream;
                try { stream = await Downloader.LiveStreamUrlAsync(job.Url, ct); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { log(ex.Message); break; }

                using (var rec = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var recStart = DateTime.UtcNow;
                    var recTask = Tools.RunAsync(Tools.FFmpeg, "-y -v error -i " + Tools.Q(stream) + " -map 0:v:0? -map 0:a:0? -c copy -f mpegts " + Tools.Q(file), work, null, rec.Token);
                    double processed = 0;
                    Size size = Size.Empty;
                    bool recDone = false;
                    while (true)
                    {
                        // чекаємо 10 хвилин запису (або кінця запису)
                        while (!recTask.IsCompleted && (DateTime.UtcNow - recStart).TotalSeconds < processed + LiveWindow + 15)
                        {
                            if (DateTime.UtcNow >= deadline) { rec.Cancel(); break; }
                            int rec_s = (int)(DateTime.UtcNow - recStart).TotalSeconds;
                            Status(job, "Запис ефіру " + TimeSpan.FromSeconds(rec_s).ToString(@"h\:mm\:ss") + " · кліпів: " + job.Clips.Count, 5);
                            bool stop = false;
                            try { await Task.Delay(5000, ct); }
                            catch (OperationCanceledException) { stop = true; }
                            if (stop)
                            {
                                rec.Cancel();
                                try { await recTask; } catch { }
                                ct.ThrowIfCancellationRequested();
                            }
                        }
                        recDone = recTask.IsCompleted;
                        if (recDone) { try { await recTask; } catch { } }
                        if (!File.Exists(file) || new FileInfo(file).Length < 100000) break;
                        if (size.IsEmpty)
                        {
                            size = await Renderer.ProbeSizeAsync(file, ct);
                            if (recipe.Layout == "stream" && job.CamW == 0) await DetectCamAsync(job, file, size, 0, Math.Min(LiveWindow, (DateTime.UtcNow - recStart).TotalSeconds - 5), work, ct);
                        }
                        double window = recDone ? 1e7 : LiveWindow;
                        var loud = await LiveLoudnessAsync(file, processed, window, ct);
                        if (loud.Length >= minLen)
                        {
                            int want = Math.Max(1, (int)Math.Round(perHour * loud.Length / 3600.0));
                            var found = Highlights.Find(loud.Length, new List<HeatPoint>(), loud, new List<Word>(), want, minLen, maxLen, 0, 0, null, false);
                            foreach (var c in found.OrderBy(c => c.Start))
                            {
                                c.Start += processed; c.End += processed;
                                c.Index = job.Clips.Count + 1;
                                job.Clips.Add(c);
                                int n = c.Index;
                                await BuildClipAsync(job, c, file, size, new List<Word>(), work, p => Status(job, "Ефір: кліп " + n + " — " + p + "%", 5), ct);
                                log("Ефір: готовий кліп " + n + " (" + TimeSpan.FromSeconds(c.Start).ToString(@"h\:mm\:ss") + ")");
                                if (job.AutoPublish && job.ChannelId != null)
                                {
                                    var ch = settings.FindChannel(job.ChannelId);
                                    if (ch != null)
                                    {
                                        try { await PublishAsync(job, c, ch, ct); }
                                        catch (OperationCanceledException) { throw; }
                                        catch (Exception ex) { c.Status = "Помилка: " + ex.Message; log("Публікація: " + ex.Message); }
                                    }
                                }
                            }
                            processed += loud.Length;
                            changed(job);
                        }
                        if (recDone) break;
                    }
                    if (!recDone) { rec.Cancel(); try { await recTask; } catch { } }
                }
                if (ct.IsCancellationRequested) break;
                // запис обірвався: ефір закінчився чи просто збій мережі?
                try
                {
                    var again = await Downloader.GetInfoAsync(job.Url, ct);
                    ended = !again.IsLive;
                }
                catch (OperationCanceledException) { throw; }
                catch { ended = true; }
                if (!ended && DateTime.UtcNow < deadline) log("Ефір: з’єднання обірвалось — підключаюсь знову.");
            }
            ct.ThrowIfCancellationRequested();
            job.Done = true;
            Status(job, "Ефір записано — " + job.Clips.Count + " кліпів", 100);
        }

        /// <summary>Гучність кожної секунди шматка запису (рахує стільки, скільки вже записано).</summary>
        static async Task<double[]> LiveLoudnessAsync(string file, double from, double len, CancellationToken ct)
        {
            const int rate = 8000;
            var list = new List<double>();
            var args = "-v error -ss " + from.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                       (len < 1e6 ? " -t " + len.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "") +
                       " -i " + Tools.Q(file) + " -vn -ac 1 -ar " + rate + " -f s16le -";
            await Tools.RunBinaryAsync(Tools.FFmpeg, args, stream =>
            {
                var buf = new byte[rate * 2];
                double sum = 0; int count = 0, n, carry = 0;
                while ((n = stream.Read(buf, carry, buf.Length - carry)) > 0)
                {
                    int total = carry + n, samples = total / 2;
                    for (int i = 0; i < samples; i++)
                    {
                        double v = BitConverter.ToInt16(buf, i * 2) / 32768.0;
                        sum += v * v; count++;
                        if (count == rate) { list.Add(Math.Sqrt(sum / count)); sum = 0; count = 0; }
                    }
                    carry = total - samples * 2;
                    if (carry > 0) buf[0] = buf[total - 1];
                }
            }, ct);
            return list.ToArray();
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
