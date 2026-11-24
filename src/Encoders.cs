using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    /// <summary>
    /// Кодування на відеокарті (з ViralCut): NVENC (NVIDIA), AMF (AMD), Quick Sync (Intel).
    /// Кожен кодувальник перевіряється пробним кадром — у списку FFmpeg він є завжди, а працює лише з потрібною картою й драйвером.
    /// </summary>
    static class Encoders
    {
        static string gpu;          // назва робочого кодувальника відеокарти або "" — немає
        static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        public static readonly string[][] Names =
        {
            new[] { "h264_nvenc", "NVIDIA (NVENC)" },
            new[] { "h264_amf", "AMD (AMF)" },
            new[] { "h264_qsv", "Intel (Quick Sync)" },
        };

        public static string Label(string enc)
        {
            var n = Names.FirstOrDefault(x => x[0] == enc);
            return n != null ? n[1] : "процесор (x264)";
        }

        /// <summary>Робочий кодувальник відеокарти або null.</summary>
        public static async Task<string> DetectGpuAsync(CancellationToken ct)
        {
            if (gpu != null) return gpu.Length == 0 ? null : gpu;
            await gate.WaitAsync(ct);
            try
            {
                if (gpu != null) return gpu.Length == 0 ? null : gpu;
                var cache = Path.Combine(AppSettings.LocalDir, "gpu_encoder.txt");
                try
                {
                    // перевіряємо раз на тиждень або після оновлення FFmpeg
                    if (File.Exists(cache) && File.GetLastWriteTimeUtc(cache) > DateTime.UtcNow.AddDays(-7) &&
                        (!File.Exists(Tools.FFmpeg) || File.GetLastWriteTimeUtc(cache) > File.GetLastWriteTimeUtc(Tools.FFmpeg)))
                    {
                        gpu = File.ReadAllText(cache).Trim();
                        return gpu.Length == 0 ? null : gpu;
                    }
                }
                catch { }
                string found = "";
                if (File.Exists(Tools.FFmpeg))
                {
                    var tmp = Path.Combine(Path.GetTempPath(), "autoshorts_enc_test.mp4");
                    foreach (var n in Names)
                    {
                        try
                        {
                            var r = await Tools.RunAsync(Tools.FFmpeg, "-y -v error -f lavfi -i color=c=black:s=640x360:r=30 -t 0.5 -c:v " + n[0] + " -pix_fmt yuv420p " + Tools.Q(tmp), null, null, ct);
                            if (r.ExitCode == 0 && File.Exists(tmp) && new FileInfo(tmp).Length > 0) { found = n[0]; break; }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch { }
                    }
                    try { File.Delete(tmp); } catch { }
                }
                gpu = found;
                try { File.WriteAllText(cache, found); } catch { }
                return found.Length == 0 ? null : found;
            }
            finally { gate.Release(); }
        }

        /// <summary>Яким кодувальником робити кліп за налаштуванням "auto" | "gpu" | "cpu".</summary>
        public static async Task<string> PickAsync(string setting, CancellationToken ct)
        {
            if (setting == "cpu") return "libx264";
            var g = await DetectGpuAsync(ct);
            return g ?? "libx264";
        }

        /// <summary>Параметри відео для FFmpeg під кодувальник (якість близька до x264 crf 20).</summary>
        public static string Args(string enc)
        {
            switch (enc)
            {
                case "h264_nvenc": return "-c:v h264_nvenc -preset p5 -tune hq -rc vbr -cq 21 -b:v 0 -maxrate 16M -bufsize 32M -pix_fmt yuv420p";
                case "h264_amf": return "-c:v h264_amf -quality quality -rc cqp -qp_i 20 -qp_p 22 -qp_b 24 -pix_fmt yuv420p";
                case "h264_qsv": return "-c:v h264_qsv -preset medium -global_quality 21 -look_ahead 0 -pix_fmt nv12";
                default: return "-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -threads " + Tools.Threads;
            }
        }

        /// <summary>Відеокарта відмовила посеред роботи — далі лише процесор (до перезапуску).</summary>
        public static void Disable() { gpu = ""; }
    }
}
