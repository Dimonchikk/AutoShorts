using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    public class VideoStats
    {
        public string Id;
        public string Title;
        public DateTime PublishedUtc;
        public string Privacy;
        public long Views, Likes, Comments;
    }

    class YouTubeApi
    {
        readonly string token;

        public YouTubeApi(string accessToken) { token = accessToken; }

        public static async Task<YouTubeApi> ForChannelAsync(AppSettings s, ChannelInfo ch)
        {
            return new YouTubeApi(await GoogleAuth.AccessTokenAsync(s, ch));
        }

        async Task<object> GetAsync(string url)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await Net.Client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) throw new Exception(ErrorText(body, (int)resp.StatusCode));
            return Json.Parse(body);
        }

        static string ErrorText(string body, int code)
        {
            try
            {
                var o = Json.Parse(body);
                var msg = Json.Str(Json.Get(o, "error"), "message");
                var reason = "";
                var errs = Json.Arr(Json.Get(o, "error"), "errors");
                if (errs.Length > 0) reason = Json.Str(errs[0], "reason");
                if (reason == "quotaExceeded") return "Вичерпано денний ліміт YouTube API (квоту). Спробуйте завтра.";
                if (reason == "uploadLimitExceeded") return "YouTube тимчасово обмежив завантаження для цього каналу.";
                if (!string.IsNullOrEmpty(msg)) return "YouTube API (" + code + "): " + msg;
            }
            catch { }
            return "YouTube API (" + code + "): " + (body.Length > 300 ? body.Substring(0, 300) : body);
        }

        public async Task<Tuple<string, string, string>> MyChannelAsync()
        {
            var o = await GetAsync("https://www.googleapis.com/youtube/v3/channels?part=snippet,contentDetails&mine=true");
            var items = Json.Arr(o, "items");
            if (items.Length == 0) throw new Exception("У цього Google-акаунта немає YouTube-каналу.");
            var it = items[0];
            return Tuple.Create(Json.Str(it, "id"), Json.Str(Json.Get(it, "snippet"), "title"),
                Json.Str(Json.Path(it, "contentDetails", "relatedPlaylists"), "uploads"));
        }

        /// <summary>Підписники, перегляди, кількість відео каналу.</summary>
        public async Task<long[]> ChannelStatsAsync(string channelId)
        {
            var o = await GetAsync("https://www.googleapis.com/youtube/v3/channels?part=statistics&id=" + Uri.EscapeDataString(channelId));
            var items = Json.Arr(o, "items");
            if (items.Length == 0) return new long[] { 0, 0, 0, 0 };
            var st = Json.Get(items[0], "statistics");
            bool hidden = Json.Get(st, "hiddenSubscriberCount") is bool && (bool)Json.Get(st, "hiddenSubscriberCount");
            return new[] { hidden ? -1 : (long)Json.Num(st, "subscriberCount", 0), (long)Json.Num(st, "viewCount", 0), (long)Json.Num(st, "videoCount", 0) };
        }

        /// <summary>Аватарка й @нікнейм каналу.</summary>
        public async Task<Tuple<string, string>> ChannelLookAsync(string channelId)
        {
            var o = await GetAsync("https://www.googleapis.com/youtube/v3/channels?part=snippet&id=" + Uri.EscapeDataString(channelId));
            var items = Json.Arr(o, "items");
            if (items.Length == 0) return Tuple.Create<string, string>(null, null);
            var sn = Json.Get(items[0], "snippet");
            var thumbs = Json.Get(sn, "thumbnails");
            string url = null;
            foreach (var k in new[] { "medium", "default", "high" }) { url = Json.Str(Json.Get(thumbs, k), "url"); if (url != null) break; }
            return Tuple.Create(url, Json.Str(sn, "customUrl"));
        }

        static DateTime? Date(string s)
        {
            DateTime dt;
            if (!string.IsNullOrEmpty(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt)) return dt;
            return null;
        }

        static int Seconds(string iso)
        {
            // PT1H2M3S
            if (string.IsNullOrEmpty(iso)) return 0;
            var m = System.Text.RegularExpressions.Regex.Match(iso, @"P(?:(\d+)D)?T?(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?");
            Func<int, int> g = i => m.Groups[i].Success ? int.Parse(m.Groups[i].Value) : 0;
            return g(1) * 86400 + g(2) * 3600 + g(3) * 60 + g(4);
        }

        /// <summary>Усі відео каналу (опубліковані, заплановані, приватні) — для календаря.</summary>
        public async Task<List<ChannelVideo>> ChannelVideosAsync(string channelId, string uploadsPlaylist, int max)
        {
            var list = new List<ChannelVideo>();
            string page = null;
            do
            {
                var url = "https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&maxResults=50&playlistId=" +
                          Uri.EscapeDataString(uploadsPlaylist) + (page != null ? "&pageToken=" + page : "");
                var o = await GetAsync(url);
                foreach (var it in Json.Arr(o, "items"))
                {
                    var cd = Json.Get(it, "contentDetails");
                    var sn = Json.Get(it, "snippet");
                    var id = Json.Str(cd, "videoId");
                    if (id == null) continue;
                    var when = Date(Json.Str(cd, "videoPublishedAt")) ?? Date(Json.Str(sn, "publishedAt")) ?? DateTime.UtcNow;
                    list.Add(new ChannelVideo { Id = id, ChannelId = channelId, Title = Json.Str(sn, "title") ?? id, WhenUtc = when });
                }
                page = Json.Str(o, "nextPageToken");
            } while (page != null && list.Count < max);
            if (list.Count > max) list = list.Take(max).ToList();

            // статус, запланований час і тривалість — пачками по 50
            for (int i = 0; i < list.Count; i += 50)
            {
                var batch = list.Skip(i).Take(50).ToList();
                var o = await GetAsync("https://www.googleapis.com/youtube/v3/videos?part=status,contentDetails,liveStreamingDetails&id=" + string.Join(",", batch.Select(v => v.Id)));
                var byId = batch.ToDictionary(v => v.Id);
                foreach (var it in Json.Arr(o, "items"))
                {
                    ChannelVideo v;
                    if (!byId.TryGetValue(Json.Str(it, "id") ?? "", out v)) continue;
                    var st = Json.Get(it, "status");
                    var privacy = Json.Str(st, "privacyStatus") ?? "public";
                    var publishAt = Date(Json.Str(st, "publishAt"));
                    v.Privacy = privacy;
                    if (publishAt.HasValue && privacy != "public") { v.Scheduled = true; v.WhenUtc = publishAt.Value; }
                    var secs = Seconds(Json.Str(Json.Get(it, "contentDetails"), "duration"));
                    v.Seconds = secs;
                    v.IsShort = secs > 0 && secs <= 180;
                    var live = Json.Get(it, "liveStreamingDetails");
                    if (live != null) { v.IsLive = true; v.WhenUtc = Date(Json.Str(live, "actualStartTime")) ?? Date(Json.Str(live, "scheduledStartTime")) ?? v.WhenUtc; }
                }
            }
            return list;
        }

        public async Task<List<Tuple<string, DateTime>>> RecentUploadsAsync(string playlistId, int max)
        {
            var res = new List<Tuple<string, DateTime>>();
            string page = null;
            do
            {
                var url = "https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId=" +
                          Uri.EscapeDataString(playlistId) + (page != null ? "&pageToken=" + page : "");
                var o = await GetAsync(url);
                foreach (var it in Json.Arr(o, "items"))
                {
                    var cd = Json.Get(it, "contentDetails");
                    var id = Json.Str(cd, "videoId");
                    var pub = Json.Str(cd, "videoPublishedAt");
                    DateTime dt;
                    if (id != null && pub != null && DateTime.TryParse(pub, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt))
                        res.Add(Tuple.Create(id, dt));
                }
                page = Json.Str(o, "nextPageToken");
            } while (page != null && res.Count < max);
            return res.Take(max).ToList();
        }

        /// <summary>Перегляди за перші ~48 годин (Analytics рахує по днях).</summary>
        public async Task<double> FirstDaysViewsAsync(string videoId, DateTime publishedUtc)
        {
            var start = publishedUtc.Date;
            var end = start.AddDays(2);
            var url = "https://youtubeanalytics.googleapis.com/v2/reports?ids=channel%3D%3DMINE&metrics=views" +
                      "&startDate=" + start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                      "&endDate=" + end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                      "&filters=video%3D%3D" + Uri.EscapeDataString(videoId);
            var o = await GetAsync(url);
            double sum = 0;
            foreach (var row in Json.Arr(o, "rows"))
            {
                var r = Json.AsArr(row);
                if (r.Length > 0) sum += Convert.ToDouble(r[r.Length - 1], CultureInfo.InvariantCulture);
            }
            return sum;
        }

        public async Task<List<VideoStats>> StatsAsync(IEnumerable<string> ids)
        {
            var res = new List<VideoStats>();
            var all = ids.Distinct().ToList();
            for (int i = 0; i < all.Count; i += 50)
            {
                var batch = string.Join(",", all.Skip(i).Take(50));
                var o = await GetAsync("https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics,status&id=" + Uri.EscapeDataString(batch));
                foreach (var it in Json.Arr(o, "items"))
                {
                    var v = new VideoStats();
                    v.Id = Json.Str(it, "id");
                    var sn = Json.Get(it, "snippet");
                    v.Title = Json.Str(sn, "title");
                    DateTime dt;
                    if (DateTime.TryParse(Json.Str(sn, "publishedAt") ?? "", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt)) v.PublishedUtc = dt;
                    var st = Json.Get(it, "statistics");
                    v.Views = (long)Json.Num(st, "viewCount", 0);
                    v.Likes = (long)Json.Num(st, "likeCount", 0);
                    v.Comments = (long)Json.Num(st, "commentCount", 0);
                    v.Privacy = Json.Str(Json.Get(it, "status"), "privacyStatus");
                    res.Add(v);
                }
            }
            return res;
        }

        /// <summary>Завантаження (resumable). publishAtUtc — запланована публікація.</summary>
        public Task<string> UploadAsync(string file, string title, string description, List<string> tags, DateTime? publishAtUtc,
                                        Action<int> progress, CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var len = new FileInfo(file).Length;
                var sb = new StringBuilder();
                sb.Append("{\"snippet\":{\"title\":").Append(Json.Escape(title))
                  .Append(",\"description\":").Append(Json.Escape(description))
                  .Append(",\"categoryId\":\"22\"");
                if (tags != null && tags.Count > 0) sb.Append(",\"tags\":").Append(Json.Serialize(tags));
                sb.Append("},\"status\":{");
                if (publishAtUtc.HasValue)
                    sb.Append("\"privacyStatus\":\"private\",\"publishAt\":").Append(Json.Escape(publishAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));
                else
                    sb.Append("\"privacyStatus\":\"public\"");
                sb.Append(",\"selfDeclaredMadeForKids\":false,\"embeddable\":true}}");
                var meta = Encoding.UTF8.GetBytes(sb.ToString());

                var init = (HttpWebRequest)WebRequest.Create("https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status");
                init.Method = "POST";
                init.Headers["Authorization"] = "Bearer " + token;
                init.Headers["X-Upload-Content-Type"] = "video/mp4";
                init.Headers["X-Upload-Content-Length"] = len.ToString(CultureInfo.InvariantCulture);
                init.ContentType = "application/json; charset=UTF-8";
                init.ContentLength = meta.Length;
                using (var rs = init.GetRequestStream()) rs.Write(meta, 0, meta.Length);
                string location;
                using (var resp = Send(init)) location = resp.Headers["Location"];
                if (string.IsNullOrEmpty(location)) throw new Exception("YouTube не повернув адресу для завантаження.");

                var put = (HttpWebRequest)WebRequest.Create(location);
                put.Method = "PUT";
                put.Headers["Authorization"] = "Bearer " + token;
                put.ContentType = "video/mp4";
                put.ContentLength = len;
                put.AllowWriteStreamBuffering = false;
                put.Timeout = 6 * 60 * 60 * 1000;
                put.ReadWriteTimeout = 10 * 60 * 1000;
                using (var rs = put.GetRequestStream())
                using (var fs = File.OpenRead(file))
                {
                    var buf = new byte[1 << 20];
                    long sent = 0; int n; int last = -1;
                    while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        rs.Write(buf, 0, n);
                        sent += n;
                        int p = (int)(sent * 100 / len);
                        if (p != last && progress != null) { last = p; progress(p); }
                    }
                }
                using (var resp = Send(put))
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var o = Json.Parse(reader.ReadToEnd());
                    var id = Json.Str(o, "id");
                    if (string.IsNullOrEmpty(id)) throw new Exception("YouTube не повернув ID відео.");
                    return id;
                }
            });
        }

        static HttpWebResponse Send(HttpWebRequest req)
        {
            try { return (HttpWebResponse)req.GetResponse(); }
            catch (WebException ex)
            {
                var r = ex.Response as HttpWebResponse;
                if (r == null) throw;
                using (var reader = new StreamReader(r.GetResponseStream(), Encoding.UTF8))
                    throw new Exception(ErrorText(reader.ReadToEnd(), (int)r.StatusCode));
            }
        }

        public async Task SetThumbnailAsync(string videoId, string jpg)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/youtube/v3/thumbnails/set?videoId=" + Uri.EscapeDataString(videoId));
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var content = new ByteArrayContent(File.ReadAllBytes(jpg));
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            req.Content = content;
            var resp = await Net.Client.SendAsync(req);
            if (!resp.IsSuccessStatusCode) throw new Exception(ErrorText(await resp.Content.ReadAsStringAsync(), (int)resp.StatusCode));
        }
    }
}
