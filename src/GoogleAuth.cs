using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoShorts
{
    public class TokenResult
    {
        public string AccessToken;
        public string RefreshToken;
        public DateTime ExpiresUtc;
    }

    /// <summary>Вхід через Google (OAuth для десктопних програм, loopback + PKCE).</summary>
    static class GoogleAuth
    {
        public const string Scopes = "https://www.googleapis.com/auth/youtube.upload " +
                                     "https://www.googleapis.com/auth/youtube.readonly " +
                                     "https://www.googleapis.com/auth/yt-analytics.readonly";

        static readonly Dictionary<string, TokenResult> cache = new Dictionary<string, TokenResult>();

        static string B64Url(byte[] b)
        {
            return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static string Random(int bytes)
        {
            var b = new byte[bytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
            return B64Url(b);
        }

        public static async Task<TokenResult> SignInAsync(string clientId, string clientSecret, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new Exception("Спершу вкажіть Client ID у вкладці «Налаштування».");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var redirect = "http://127.0.0.1:" + port;
                var verifier = Random(32);
                string challenge;
                using (var sha = SHA256.Create()) challenge = B64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
                var state = Random(16);
                var url = "https://accounts.google.com/o/oauth2/v2/auth?response_type=code" +
                          "&client_id=" + Uri.EscapeDataString(clientId.Trim()) +
                          "&redirect_uri=" + Uri.EscapeDataString(redirect) +
                          "&scope=" + Uri.EscapeDataString(Scopes) +
                          "&code_challenge=" + challenge + "&code_challenge_method=S256" +
                          "&access_type=offline&prompt=" + Uri.EscapeDataString("select_account consent") +
                          "&state=" + state;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

                string code = null;
                var deadline = DateTime.UtcNow.AddMinutes(5);
                while (code == null)
                {
                    var acceptTask = listener.AcceptTcpClientAsync();
                    var done = await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(1), ct));
                    if (ct.IsCancellationRequested) throw new OperationCanceledException();
                    if (DateTime.UtcNow > deadline) throw new Exception("Час на вхід вичерпано. Спробуйте ще раз.");
                    if (done != acceptTask) { WaitAndDiscard(acceptTask); continue; }
                    using (var client = acceptTask.Result)
                    {
                        var stream = client.GetStream();
                        var reader = new StreamReader(stream, Encoding.ASCII);
                        var requestLine = reader.ReadLine() ?? "";
                        string line;
                        while (!string.IsNullOrEmpty(line = reader.ReadLine())) { }
                        var parts = requestLine.Split(' ');
                        var query = parts.Length > 1 && parts[1].Contains("?") ? parts[1].Substring(parts[1].IndexOf('?') + 1) : "";
                        var q = ParseQuery(query);
                        string html;
                        string err;
                        if (q.TryGetValue("error", out err))
                        {
                            html = Page("Вхід скасовано", "Можна закрити цю вкладку.");
                            Respond(stream, html);
                            throw new Exception("Google повернув помилку: " + err);
                        }
                        string c, st;
                        if (q.TryGetValue("code", out c) && q.TryGetValue("state", out st) && st == state)
                        {
                            code = c;
                            html = Page("Готово ✔", "Вхід виконано. Поверніться в AutoShorts — цю вкладку можна закрити.");
                        }
                        else html = Page("AutoShorts", "Очікую на вхід…");
                        Respond(stream, html);
                    }
                }

                var form = new Dictionary<string, string>();
                form["code"] = code;
                form["client_id"] = clientId.Trim();
                if (!string.IsNullOrWhiteSpace(clientSecret)) form["client_secret"] = clientSecret.Trim();
                form["redirect_uri"] = redirect;
                form["grant_type"] = "authorization_code";
                form["code_verifier"] = verifier;
                var tok = await PostTokenAsync(form);
                if (string.IsNullOrEmpty(tok.RefreshToken))
                    throw new Exception("Google не видав refresh token. Видаліть доступ AutoShorts у myaccount.google.com/permissions і увійдіть знову.");
                lock (cache) cache[tok.RefreshToken] = tok;
                return tok;
            }
            finally
            {
                listener.Stop();
            }
        }

        static void WaitAndDiscard(Task<TcpClient> t)
        {
            t.ContinueWith(x => { if (x.Status == TaskStatus.RanToCompletion) x.Result.Close(); });
        }

        static string Page(string title, string text)
        {
            return "<!doctype html><html><head><meta charset=\"utf-8\"><title>AutoShorts</title></head>" +
                   "<body style=\"font-family:Segoe UI,Arial;background:#111;color:#eee;display:flex;align-items:center;justify-content:center;height:100vh;margin:0\">" +
                   "<div style=\"text-align:center\"><h1 style=\"color:#2bff4a\">" + title + "</h1><p>" + text + "</p></div></body></html>";
        }

        static void Respond(Stream stream, string html)
        {
            var body = Encoding.UTF8.GetBytes(html);
            var head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        static Dictionary<string, string> ParseQuery(string q)
        {
            var d = new Dictionary<string, string>();
            foreach (var p in q.Split('&'))
            {
                if (p.Length == 0) continue;
                int i = p.IndexOf('=');
                var k = Uri.UnescapeDataString((i < 0 ? p : p.Substring(0, i)).Replace('+', ' '));
                var v = i < 0 ? "" : Uri.UnescapeDataString(p.Substring(i + 1).Replace('+', ' '));
                d[k] = v;
            }
            return d;
        }

        static async Task<TokenResult> PostTokenAsync(Dictionary<string, string> form)
        {
            var resp = await Net.Client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(form));
            var body = await resp.Content.ReadAsStringAsync();
            var o = Json.Parse(body);
            if (!resp.IsSuccessStatusCode)
                throw new Exception("Помилка входу Google: " + (Json.Str(o, "error_description") ?? Json.Str(o, "error") ?? body));
            var t = new TokenResult();
            t.AccessToken = Json.Str(o, "access_token");
            t.RefreshToken = Json.Str(o, "refresh_token");
            t.ExpiresUtc = DateTime.UtcNow.AddSeconds(Json.Num(o, "expires_in", 3600) - 120);
            return t;
        }

        public static async Task<string> AccessTokenAsync(AppSettings s, ChannelInfo ch)
        {
            var refresh = AppSettings.Unprotect(ch.RefreshTokenProtected);
            if (string.IsNullOrEmpty(refresh)) throw new Exception("Канал «" + ch.Title + "» не авторизовано. Додайте його знову.");
            TokenResult t;
            lock (cache) cache.TryGetValue(refresh, out t);
            if (t != null && t.AccessToken != null && t.ExpiresUtc > DateTime.UtcNow) return t.AccessToken;
            var form = new Dictionary<string, string>();
            form["client_id"] = (s.ClientId ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(s.ClientSecret)) form["client_secret"] = s.ClientSecret.Trim();
            form["refresh_token"] = refresh;
            form["grant_type"] = "refresh_token";
            var nt = await PostTokenAsync(form);
            nt.RefreshToken = refresh;
            lock (cache) cache[refresh] = nt;
            return nt.AccessToken;
        }
    }
}
