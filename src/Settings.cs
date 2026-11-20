using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AutoShorts
{
    public class AppSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string OutputFolder { get; set; }
        public string TitleTemplate { get; set; }
        public string DescriptionTemplate { get; set; }
        public string TagsTemplate { get; set; }
        public string SubtitleStyle { get; set; }
        public int ClipsPerVideo { get; set; }
        public int MinSeconds { get; set; }
        public int MaxSeconds { get; set; }
        public bool FaceFraming { get; set; }
        public bool HookTitle { get; set; }
        public bool RequirePreview { get; set; }
        public bool UploadCover { get; set; }
        public string DefaultChannelId { get; set; }
        public List<ChannelInfo> Channels { get; set; }
        public List<PublishedItem> Published { get; set; }

        public AppSettings()
        {
            OutputFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AutoShorts");
            TitleTemplate = "{hook} #shorts";
            DescriptionTemplate = "{hook}\r\n\r\nПовне відео: {source_url}\r\n\r\n{hashtags} #shorts";
            TagsTemplate = "{keywords}, {source_tags}, shorts";
            SubtitleStyle = SubtitleStyles.All[0].Name;
            ClipsPerVideo = 3;
            MinSeconds = 30;
            MaxSeconds = 60;
            FaceFraming = true;
            HookTitle = true;
            RequirePreview = true;
            UploadCover = true;
            Channels = new List<ChannelInfo>();
            Published = new List<PublishedItem>();
        }

        public static string AppDir
        {
            get
            {
                var d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoShorts");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string LocalDir
        {
            get
            {
                var d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoShorts");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static string FilePath { get { return System.IO.Path.Combine(AppDir, "settings.json"); } }
        static string JobsPath { get { return System.IO.Path.Combine(AppDir, "jobs.json"); } }

        public static AppSettings Load()
        {
            AppSettings s = null;
            try
            {
                if (File.Exists(FilePath)) s = Json.Deserialize<AppSettings>(File.ReadAllText(FilePath, Encoding.UTF8));
            }
            catch { }
            if (s == null) s = new AppSettings();
            if (s.Channels == null) s.Channels = new List<ChannelInfo>();
            if (s.Published == null) s.Published = new List<PublishedItem>();
            if (s.MinSeconds < 10) s.MinSeconds = 30;
            if (s.MaxSeconds < s.MinSeconds + 5) s.MaxSeconds = Math.Max(60, s.MinSeconds + 5);
            if (s.ClipsPerVideo < 1) s.ClipsPerVideo = 3;
            if (SubtitleStyles.Find(s.SubtitleStyle) == null) s.SubtitleStyle = SubtitleStyles.All[0].Name;
            return s;
        }

        public void Save()
        {
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, Json.Serialize(this), Encoding.UTF8);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        public static List<Job> LoadJobs()
        {
            try
            {
                if (File.Exists(JobsPath))
                {
                    var l = Json.Deserialize<List<Job>>(File.ReadAllText(JobsPath, Encoding.UTF8));
                    if (l != null)
                    {
                        foreach (var j in l)
                        {
                            if (j.Clips == null) j.Clips = new List<ClipItem>();
                            if (j.SourceTags == null) j.SourceTags = new List<string>();
                            if (!j.Done && j.Error == null) { j.Status = "В черзі"; j.Progress = 0; }
                        }
                        return l;
                    }
                }
            }
            catch { }
            return new List<Job>();
        }

        public static void SaveJobs(List<Job> jobs)
        {
            try
            {
                var tmp = JobsPath + ".tmp";
                File.WriteAllText(tmp, Json.Serialize(jobs), Encoding.UTF8);
                if (File.Exists(JobsPath)) File.Delete(JobsPath);
                File.Move(tmp, JobsPath);
            }
            catch { }
        }

        public ChannelInfo FindChannel(string id)
        {
            foreach (var c in Channels) if (c.Id == id) return c;
            return null;
        }

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return null;
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        public static string Unprotect(string enc)
        {
            if (string.IsNullOrEmpty(enc)) return null;
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(enc), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
