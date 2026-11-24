using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if WINRT
using Windows.Graphics.Imaging;
using Windows.Media.FaceAnalysis;
using Windows.Storage;
using Windows.Storage.Streams;
#endif

namespace AutoShorts
{
    /// <summary>Пошук облич вбудованим детектором Windows (Windows.Media.FaceAnalysis) — без моделей.</summary>
    static class FaceFrame
    {
        public static bool Available
        {
            get
            {
#if WINRT
                try { return Environment.OSVersion.Version.Major >= 10 && FaceDetector.IsSupported; } catch { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>Повертає центр обличчя по горизонталі (0..1) або -1, якщо облич немає.</summary>
        public static async Task<double> CenterXAsync(List<string> frames)
        {
#if WINRT
            if (!Available || frames.Count == 0) return -1;
            var xs = new List<double>();
            var detector = await FaceDetector.CreateAsync().AsTask();
            foreach (var path in frames)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(path).AsTask();
                    using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read).AsTask())
                    {
                        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask();
                        var bmp = await decoder.GetSoftwareBitmapAsync().AsTask();
                        var gray = SoftwareBitmap.Convert(bmp, BitmapPixelFormat.Gray8);
                        var faces = await detector.DetectFacesAsync(gray).AsTask();
                        DetectedFace best = null;
                        foreach (var f in faces)
                            if (best == null || f.FaceBox.Width * f.FaceBox.Height > best.FaceBox.Width * best.FaceBox.Height) best = f;
                        if (best != null)
                            xs.Add((best.FaceBox.X + best.FaceBox.Width / 2.0) / bmp.PixelWidth);
                        gray.Dispose();
                        bmp.Dispose();
                    }
                }
                catch { }
            }
            if (xs.Count < Math.Max(1, frames.Count / 5)) return -1;
            xs.Sort();
            return xs[xs.Count / 2];
#else
            await Task.Yield();
            return -1;
#endif
        }

        /// <summary>
        /// Центр обличчя (0..1) на кожному кадрі або -1, якщо обличчя немає. Якщо облич кілька —
        /// береться те, що ближче до попереднього (кадр не стрибає між людьми), з перевагою більшого.
        /// </summary>
        public static async Task<List<double>> TrackAsync(List<string> frames)
        {
            var res = new List<double>();
#if WINRT
            if (!Available || frames.Count == 0) return res;
            var detector = await FaceDetector.CreateAsync().AsTask();
            double prev = -1;
            foreach (var path in frames)
            {
                double x = -1;
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(path).AsTask();
                    using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read).AsTask())
                    {
                        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask();
                        var bmp = await decoder.GetSoftwareBitmapAsync().AsTask();
                        var gray = SoftwareBitmap.Convert(bmp, BitmapPixelFormat.Gray8);
                        var faces = await detector.DetectFacesAsync(gray).AsTask();
                        double bestScore = double.MinValue;
                        double maxArea = faces.Count == 0 ? 1 : faces.Max(f => (double)f.FaceBox.Width * f.FaceBox.Height);
                        foreach (var f in faces)
                        {
                            double cx = (f.FaceBox.X + f.FaceBox.Width / 2.0) / bmp.PixelWidth;
                            double area = (double)f.FaceBox.Width * f.FaceBox.Height / maxArea;
                            double score = area - (prev >= 0 ? Math.Abs(cx - prev) * 2.5 : 0);
                            if (area < 0.25) continue; // дрібні обличчя на фоні
                            if (score > bestScore) { bestScore = score; x = cx; }
                        }
                        gray.Dispose();
                        bmp.Dispose();
                    }
                }
                catch { }
                if (x >= 0) prev = x;
                res.Add(x);
            }
#else
            await Task.Yield();
#endif
            return res;
        }

        /// <summary>
        /// Плавна траєкторія кадру з сирих позицій (як reframe.py у ViralCut): заповнення пропусків,
        /// медіана, згладжування в обидва боки, «мертва зона» від тремтіння, спрощення до ключових точок.
        /// Повертає рядок "t:x;t:x;…" або null, якщо облич замало.
        /// </summary>
        public static string BuildPath(List<double> xs, double step)
        {
            if (xs == null || xs.Count < 3) return null;
            int valid = xs.Count(x => x >= 0);
            if (valid < Math.Max(2, xs.Count * 0.3)) return null;
            var v = xs.ToArray();
            double first = v.First(x => x >= 0);
            double last = first;
            for (int i = 0; i < v.Length; i++) { if (v[i] < 0) v[i] = last; else last = v[i]; }
            for (int i = 0; i < v.Length && xs[i] < 0; i++) v[i] = first;
            // медіана по 5
            var m = new double[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var w = new List<double>();
                for (int k = Math.Max(0, i - 2); k <= Math.Min(v.Length - 1, i + 2); k++) w.Add(v[k]);
                w.Sort();
                m[i] = w[w.Count / 2];
            }
            // мертва зона: дрібні зсуви не рухають кадр
            for (int i = 1; i < m.Length; i++) if (Math.Abs(m[i] - m[i - 1]) < 0.035) m[i] = m[i - 1];
            // EMA вперед і назад — без запізнення
            const double a = 0.3;
            for (int i = 1; i < m.Length; i++) m[i] = m[i - 1] + a * (m[i] - m[i - 1]);
            for (int i = m.Length - 2; i >= 0; i--) m[i] = m[i + 1] + a * (m[i] - m[i + 1]);
            // якщо кадр майже не рухається — статичний
            if (m.Max() - m.Min() < 0.05) return null;
            // спрощення: ключові точки, де траєкторія відхиляється від прямої більше ніж на 0.02
            var keep = new List<int> { 0 };
            Simplify(m, 0, m.Length - 1, 0.02, keep);
            keep.Add(m.Length - 1);
            keep = keep.Distinct().OrderBy(i => i).ToList();
            while (keep.Count > 18) keep.RemoveAt(keep.Count / 2);
            return string.Join(";", keep.Select(i => (i * step).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ":" +
                                                      m[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
        }

        static void Simplify(double[] v, int a, int b, double tol, List<int> keep)
        {
            if (b <= a + 1) return;
            int idx = -1;
            double maxD = 0;
            for (int i = a + 1; i < b; i++)
            {
                double lin = v[a] + (v[b] - v[a]) * (i - a) / (double)(b - a);
                double d = Math.Abs(v[i] - lin);
                if (d > maxD) { maxD = d; idx = i; }
            }
            if (maxD <= tol || idx < 0) return;
            Simplify(v, a, idx, tol, keep);
            keep.Add(idx);
            Simplify(v, idx, b, tol, keep);
        }
    }
}
