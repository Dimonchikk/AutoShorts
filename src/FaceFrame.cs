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
    }
}
