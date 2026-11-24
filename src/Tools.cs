using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    public class ProcResult
    {
        public int ExitCode;
        public string Output;
        public string Error;

        public string Tail(int maxChars)
        {
            var s = (Error ?? "").Trim();
            if (s.Length == 0) s = (Output ?? "").Trim();
            return s.Length > maxChars ? s.Substring(s.Length - maxChars) : s;
        }
    }

    static class Net
    {
        static HttpClient client;

        public static HttpClient Client
        {
            get
            {
                if (client == null)
                {
                    var c = new HttpClient();
                    c.Timeout = TimeSpan.FromMinutes(30);
                    c.DefaultRequestHeaders.UserAgent.ParseAdd("AutoShorts/" + AppVersion.Text);
                    client = c;
                }
                return client;
            }
        }

        public static async Task DownloadFileAsync(string url, string path, Action<int> progress, CancellationToken ct)
        {
            var tmp = path + ".part";
            using (var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? -1;
                using (var input = await resp.Content.ReadAsStreamAsync())
                using (var output = File.Create(tmp))
                {
                    var buf = new byte[1 << 16];
                    long done = 0; int last = -1; int n;
                    while ((n = await input.ReadAsync(buf, 0, buf.Length, ct)) > 0)
                    {
                        await output.WriteAsync(buf, 0, n, ct);
                        done += n;
                        if (total > 0 && progress != null)
                        {
                            int p = (int)(done * 100 / total);
                            if (p != last) { last = p; progress(p); }
                        }
                    }
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }

    static class Tools
    {
        public static string Dir
        {
            get
            {
                var d = Path.Combine(AppSettings.LocalDir, "tools");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string YtDlp { get { return Path.Combine(Dir, "yt-dlp.exe"); } }
        public static string FFmpeg { get { return Path.Combine(Dir, "ffmpeg.exe"); } }
        public static string FFprobe { get { return Path.Combine(Dir, "ffprobe.exe"); } }

        const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        const string FFmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        static bool ytDlpUpdated;

        public static async Task EnsureAsync(Action<string> log, CancellationToken ct)
        {
            if (!File.Exists(YtDlp))
            {
                log("Завантажую yt-dlp (один раз)…");
                await Net.DownloadFileAsync(YtDlpUrl, YtDlp, null, ct);
                ytDlpUpdated = true;
            }
            else if (!ytDlpUpdated)
            {
                ytDlpUpdated = true;
                if ((DateTime.Now - File.GetLastWriteTime(YtDlp)).TotalDays > 3)
                {
                    log("Оновлюю yt-dlp…");
                    try { await RunAsync(YtDlp, "-U", Dir, null, ct); } catch (OperationCanceledException) { throw; } catch { }
                    try { File.SetLastWriteTime(YtDlp, DateTime.Now); } catch { }
                }
            }

            if (!File.Exists(FFmpeg) || !File.Exists(FFprobe))
            {
                log("Завантажую FFmpeg (≈130 МБ, один раз)…");
                var zip = Path.Combine(Dir, "ffmpeg.zip");
                int lastLogged = -10;
                await Net.DownloadFileAsync(FFmpegUrl, zip, p =>
                {
                    if (p >= lastLogged + 10) { lastLogged = p; log("FFmpeg: " + p + "%"); }
                }, ct);
                log("Розпаковую FFmpeg…");
                await Task.Run(() =>
                {
                    using (var archive = ZipFile.OpenRead(zip))
                    {
                        foreach (var e in archive.Entries)
                        {
                            var name = e.Name.ToLowerInvariant();
                            if (name == "ffmpeg.exe" || name == "ffprobe.exe")
                                e.ExtractToFile(Path.Combine(Dir, name), true);
                        }
                    }
                });
                try { File.Delete(zip); } catch { }
                if (!File.Exists(FFmpeg)) throw new Exception("Не вдалося розпакувати FFmpeg.");
            }
        }

        public static string Q(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";
        }

        static ProcessStartInfo MakePsi(string exe, string args, string workDir)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            if (!string.IsNullOrEmpty(workDir)) psi.WorkingDirectory = workDir;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            return psi;
        }

        /// <summary>Скільки потужності віддавати FFmpeg, % (з ViralCut: щоб ноутбук не вив і не грівся).</summary>
        public static int Power = 100;

        /// <summary>Скільки потоків дати кодувальнику при поточній потужності.</summary>
        public static int Threads
        {
            get { return Math.Max(1, (int)Math.Round(Environment.ProcessorCount * Math.Max(25, Math.Min(100, Power)) / 100.0)); }
        }

        static void Tame(Process p, string exe)
        {
            if (Power >= 100) return;
            if (!exe.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)) return;
            try { p.PriorityClass = Power <= 50 ? ProcessPriorityClass.Idle : ProcessPriorityClass.BelowNormal; } catch { }
            try
            {
                // обмежуємо ядра: FFmpeg не займе більше, ніж дозволено
                int n = Threads, cores = Environment.ProcessorCount;
                if (n < cores && cores <= 64)
                {
                    long mask = 0;
                    for (int i = 0; i < n; i++) mask |= 1L << (cores - 1 - i);   // старші ядра — молодші лишаються для інтерфейсу
                    p.ProcessorAffinity = (IntPtr)mask;
                }
            }
            catch { }
        }

        public static Task<ProcResult> RunAsync(string exe, string args, string workDir, Action<string> onLine, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var outSb = new StringBuilder();
                var errSb = new StringBuilder();
                using (var p = new Process())
                {
                    p.StartInfo = MakePsi(exe, args, workDir);
                    p.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data == null) return;
                        lock (outSb) outSb.AppendLine(e.Data);
                        if (onLine != null) onLine(e.Data);
                    };
                    p.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data == null) return;
                        lock (errSb) { errSb.AppendLine(e.Data); if (errSb.Length > 200000) errSb.Remove(0, 100000); }
                        if (onLine != null) onLine(e.Data);
                    };
                    p.Start();
                    Tame(p, exe);
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    while (!p.WaitForExit(250))
                    {
                        if (ct.IsCancellationRequested)
                        {
                            try { p.Kill(); } catch { }
                            ct.ThrowIfCancellationRequested();
                        }
                    }
                    p.WaitForExit();
                    var r = new ProcResult();
                    r.ExitCode = p.ExitCode;
                    lock (outSb) r.Output = outSb.ToString();
                    lock (errSb) r.Error = errSb.ToString();
                    return r;
                }
            });
        }

        /// <summary>Запускає процес і передає сирий stdout у обробник (для PCM аудіо).</summary>
        public static Task<int> RunBinaryAsync(string exe, string args, Action<Stream> reader, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                using (var p = new Process())
                {
                    p.StartInfo = MakePsi(exe, args, null);
                    p.StartInfo.StandardOutputEncoding = null;
                    p.ErrorDataReceived += (s, e) => { };
                    p.Start();
                    Tame(p, exe);
                    p.BeginErrorReadLine();
                    using (ct.Register(() => { try { p.Kill(); } catch { } }))
                    {
                        reader(p.StandardOutput.BaseStream);
                        p.WaitForExit();
                    }
                    ct.ThrowIfCancellationRequested();
                    return p.ExitCode;
                }
            });
        }
    }
}
