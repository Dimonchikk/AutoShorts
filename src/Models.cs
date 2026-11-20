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
        public int ShortsPerDay { get; set; }
        /// <summary>168 значень: (день тижня за Києвом) * 24 + година.</summary>
        public List<double> HourScores { get; set; }
        public bool UsingFallback { get; set; }
        public int VideosAnalyzed { get; set; }
        public DateTime BestTimeUpdatedUtc { get; set; }

        public ChannelInfo() { ShortsPerDay = 2; UsingFallback = true; }
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

        public double Duration { get { return End - Start; } }
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

        public Job()
        {
            Clips = new List<ClipItem>();
            SourceTags = new List<string>();
            Status = "В черзі";
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(SourceTitle) ? Url : SourceTitle; }
        }
    }
}
