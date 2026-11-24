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

        /// <summary>Усі обличчя на кожному кадрі (прямокутники 0..1 від розміру кадру).</summary>
        public static async Task<List<List<System.Drawing.RectangleF>>> FaceBoxesAsync(List<string> frames)
        {
            var res = new List<List<System.Drawing.RectangleF>>();
#if WINRT
            if (!Available || frames.Count == 0) return res;
            var detector = await FaceDetector.CreateAsync().AsTask();
            try { detector.MinDetectableFaceSize = new BitmapSize { Width = 24, Height = 24 }; } catch { }
            foreach (var path in frames)
            {
                var list = new List<System.Drawing.RectangleF>();
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(path).AsTask();
                    using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read).AsTask())
                    {
                        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask();
                        var bmp = await decoder.GetSoftwareBitmapAsync().AsTask();
                        var gray = SoftwareBitmap.Convert(bmp, BitmapPixelFormat.Gray8);
                        var faces = await detector.DetectFacesAsync(gray).AsTask();
                        float w = bmp.PixelWidth, h = bmp.PixelHeight;
                        foreach (var f in faces)
                            list.Add(new System.Drawing.RectangleF(f.FaceBox.X / w, f.FaceBox.Y / h, f.FaceBox.Width / w, f.FaceBox.Height / h));
                        gray.Dispose();
                        bmp.Dispose();
                    }
                }
                catch { }
                res.Add(list);
            }
#else
            await Task.Yield();
#endif
            return res;
        }

        /// <summary>
        /// Вікно камери стримера (з ViralCut pip.py): невелике обличчя, що весь час стоїть на одному місці ближче до краю кадру.
        /// Повертає прямокутник камери (0..1) або пустий, якщо такого немає.
        /// </summary>
        public static System.Drawing.RectangleF FindCam(List<List<System.Drawing.RectangleF>> boxes)
        {
            var empty = System.Drawing.RectangleF.Empty;
            if (boxes == null || boxes.Count < 3) return empty;
            // кандидати: дрібні обличчя (менше 13% ширини), не в центральній смузі
            var cands = new List<System.Drawing.RectangleF>();
            foreach (var fr in boxes)
                foreach (var b in fr)
                {
                    float cx = b.X + b.Width / 2, cy = b.Y + b.Height / 2;
                    if (b.Width < 0.13f && b.Width > 0.012f && (cx < 0.36f || cx > 0.64f || cy < 0.3f || cy > 0.7f)) cands.Add(b);
                }
            if (cands.Count == 0) return empty;
            // найбільше скупчення центрів (радіус 6% кадру)
            System.Drawing.RectangleF best = empty;
            int bestFrames = 0;
            foreach (var c in cands)
            {
                float cx = c.X + c.Width / 2, cy = c.Y + c.Height / 2;
                int frames = 0;
                var near = new List<System.Drawing.RectangleF>();
                foreach (var fr in boxes)
                {
                    bool hit = false;
                    foreach (var b in fr)
                    {
                        float bx = b.X + b.Width / 2, by = b.Y + b.Height / 2;
                        if (Math.Abs(bx - cx) < 0.06f && Math.Abs(by - cy) < 0.06f && b.Width < 0.13f) { near.Add(b); hit = true; }
                    }
                    if (hit) frames++;
                }
                if (frames > bestFrames)
                {
                    bestFrames = frames;
                    var xs = near.Select(b => b.X + b.Width / 2).OrderBy(v => v).ToList();
                    var ys = near.Select(b => b.Y + b.Height / 2).OrderBy(v => v).ToList();
                    var ws = near.Select(b => b.Width).OrderBy(v => v).ToList();
                    var hs = near.Select(b => b.Height).OrderBy(v => v).ToList();
                    float mx = xs[xs.Count / 2], my = ys[ys.Count / 2], mw = ws[ws.Count / 2], mh = hs[hs.Count / 2];
                    best = new System.Drawing.RectangleF(mx - mw / 2, my - mh / 2, mw, mh);
                }
            }
            // камера має бути в кадрі більшу частину часу
            if (bestFrames < Math.Max(3, (int)Math.Ceiling(boxes.Count * 0.55))) return empty;
            // вікно камери ≈ обличчя × 4 по ширині і × 3.4 по висоті (плечі, трохи фону)
            float fcx = best.X + best.Width / 2, fcy = best.Y + best.Height / 2;
            float camW = Math.Min(0.5f, best.Width * 4.0f), camH = Math.Min(0.6f, best.Height * 3.4f);
            float x0 = fcx - camW / 2, y0 = fcy - camH * 0.45f;
            // вікно камери зазвичай притиснуте до краю — дотягуємо, якщо поруч
            if (x0 < 0.06f) x0 = 0; if (x0 + camW > 0.94f) x0 = 1 - camW;
            if (y0 < 0.06f) y0 = 0; if (y0 + camH > 0.94f) y0 = 1 - camH;
            x0 = Math.Max(0, Math.Min(1 - camW, x0));
            y0 = Math.Max(0, Math.Min(1 - camH, y0));
            return new System.Drawing.RectangleF(x0, y0, camW, camH);
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
