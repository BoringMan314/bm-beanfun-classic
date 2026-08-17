using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;

namespace Beanfun.Update
{
    class ApplicationUpdater
    {
        private static readonly string[] GH_PROXIES = new[]
        {
            "https://ghproxy.vip/",
            "https://ghproxy.net/",
            "https://ghfast.top/",
        };

        private const int ProbeTimeoutMs = 5000;

        private static readonly Lazy<string> _cachedProxy = new Lazy<string>(
            DiscoverProxy,
            LazyThreadSafetyMode.ExecutionAndPublication
        );

        private static bool TryProbe(string url)
        {
            try
            {
                var req = WebRequest.CreateHttp(url);
                req.Method = "HEAD";
                req.Timeout = ProbeTimeoutMs;
                req.UserAgent = $"BeanfunClassic(V{App.AssemblyVersion})";
                using (req.GetResponse()) { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string DiscoverProxy()
        {
            if (TryProbe("https://api.github.com")) // 先測直連 GitHub
                return "";

            foreach (var proxy in GH_PROXIES) // 直連失敗再試 proxy
            {
                if (TryProbe(proxy + "https://api.github.com"))
                    return proxy;
            }

            return "";
        }

        private static string GetProxy() => _cachedProxy.Value;

        internal static string TryDownloadGitHubText(string githubUrl, int timeoutMs = 5000)
        {
            if (string.IsNullOrEmpty(githubUrl))
                return null;

            var prefixes = new List<string> { "" };
            prefixes.AddRange(GH_PROXIES);
            foreach (string prefix in prefixes)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(prefix + githubUrl); // 獨立請求，不沿用 beanfun cookies
                    req.Method = "GET";
                    req.Timeout = timeoutMs; // 短逾時
                    req.ReadWriteTimeout = timeoutMs;
                    req.UserAgent = $"BeanfunClassic(V{App.AssemblyVersion})";
                    using var resp = (HttpWebResponse)req.GetResponse();
                    using var stream = resp.GetResponseStream();
                    if (stream == null)
                        continue;
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
                catch { } // 改試下一個來源
            }
            return null;
        }

        public class GitHubRelease
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("tag_name")]
            public string TagName { get; set; }

            [JsonProperty("prerelease")]
            public bool Prerelease { get; set; }

            [JsonProperty("body")]
            public string Body { get; set; }

            [JsonProperty("assets")]
            public List<GitHubAsset> Assets { get; set; }
        }

        public class GitHubAsset
        {
            [JsonProperty("browser_download_url")]
            public string BrowserDownloadUrl { get; set; }
        }

        private static int _checkRunning;

        internal static void CheckApplicationUpdate(bool show)
        {
            var thread = new Thread(() =>
            {
                if (show)
                {
                    while (Interlocked.CompareExchange(ref _checkRunning, 1, 0) != 0)
                        Thread.Sleep(50); // 手動檢查等啟動檢查跑完，不要直接丢掉
                }
                else if (Interlocked.CompareExchange(ref _checkRunning, 1, 0) != 0)
                    return;

                try
                {
                    RunCheck(show);
                }
                finally
                {
                    Interlocked.Exchange(ref _checkRunning, 0);
                }
            })
            {
                IsBackground = true,
                Name = "UpdateCheck",
            };
            thread.Start();
        }

        private static void RunCheck(bool show)
        {
            try
            {
                string json = TryDownloadReleasesJson(out string proxy);
                if (string.IsNullOrEmpty(json))
                {
                    if (show)
                        ShowMessage(
                            FindText("ConnectionFailed", "Connection failed"),
                            FindText("UpdateCheck", "Update Check"),
                            MessageBoxButton.OK
                        );
                    return;
                }

                var releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(json);
                GitHubRelease release = GetLastRelease(releases);

                if (release == null)
                {
                    if (show)
                        ShowNoUpdate();
                    return;
                }

                var match = Regex.Match(release.TagName, @"^v(\d+)\.(\d+)\.(\d+)\.(\d+)$"); // tag 如 v5.9.2.2
                if (!match.Success)
                {
                    if (show)
                        ShowNoUpdate();
                    return;
                }

                string major = match.Groups[1].Value;
                string minor = match.Groups[2].Value;
                string patch = match.Groups[3].Value;
                string revision = match.Groups[4].Value;
                string newVerDisplay = $"{major}.{minor}.{patch}.{revision}";

                if (!IsNewerVersion(App.AssemblyVersion, major, minor, patch, revision))
                {
                    if (show)
                        ShowNoUpdate();
                    return;
                }

                string template = FindText(
                    "NewVersionDetected",
                    "Detect New Version {0} (Current: {1})\n\n{2}"
                );
                string msg = string.Format(
                    Regex.Unescape(template),
                    newVerDisplay,
                    App.AssemblyVersion,
                    EscapeFormat(release.Body)
                );

                MessageBoxResult result = ShowMessage(
                    msg,
                    FindText("UpdateCheck", "Update Check"),
                    MessageBoxButton.OKCancel
                );

                if (result != MessageBoxResult.OK)
                    return;

                string downloadUrl =
                    (release.Assets != null && release.Assets.Count > 0)
                        ? proxy + release.Assets[0].BrowserDownloadUrl
                        : $"https://github.com/BoringMan314/bm-beanfun-classic/releases/tag/{release.TagName}";

                Process.Start(
                    new ProcessStartInfo { FileName = downloadUrl, UseShellExecute = true }
                );

                RunOnUi(() => Application.Current?.Shutdown());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Update check failed: " + ex.Message);
                if (show)
                    ShowMessage(
                        FindText("ConnectionFailed", "Connection failed"),
                        FindText("UpdateCheck", "Update Check"),
                        MessageBoxButton.OK
                    );
            }
        }

        private static string TryDownloadReleasesJson(out string usedProxy)
        {
            const string api =
                "https://api.github.com/repos/BoringMan314/bm-beanfun-classic/releases";
            var prefixes = new List<string>();
            string cached = GetProxy();
            if (cached != null)
                prefixes.Add(cached);
            if (!prefixes.Contains(""))
                prefixes.Insert(0, "");
            foreach (string proxy in GH_PROXIES)
            {
                if (!prefixes.Contains(proxy))
                    prefixes.Add(proxy);
            }

            foreach (string prefix in prefixes)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(prefix + api);
                    req.Method = "GET";
                    req.Timeout = 15000;
                    req.ReadWriteTimeout = 15000;
                    req.UserAgent = $"BeanfunClassic(V{App.AssemblyVersion})";
                    req.Accept = "application/vnd.github.v3+json";
                    using var resp = (HttpWebResponse)req.GetResponse();
                    using var stream = resp.GetResponseStream();
                    if (stream == null)
                        continue;
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("["))
                    {
                        usedProxy = prefix;
                        return json;
                    }
                }
                catch { }
            }

            usedProxy = "";
            return null;
        }

        private static void ShowNoUpdate()
        {
            ShowMessage(
                FindText("NoUpdatesDetected", "No Updates Found"),
                FindText("UpdateCheck", "Update Check"),
                MessageBoxButton.OK
            );
        }

        private static string FindText(string key, string fallback)
        {
            return RunOnUi(() => Application.Current?.TryFindResource(key) as string ?? fallback);
        }

        private static MessageBoxResult ShowMessage(
            string msg,
            string title,
            MessageBoxButton buttons
        )
        {
            return RunOnUi(() =>
            {
                Window owner = Application.Current?.MainWindow;
                if (owner != null && owner.IsVisible)
                    return MessageBox.Show(owner, msg, title, buttons);
                return MessageBox.Show(msg, title, buttons);
            });
        }

        private static T RunOnUi<T>(Func<T> func)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return func();
            return dispatcher.Invoke(func);
        }

        private static void RunOnUi(Action action)
        {
            RunOnUi(() =>
            {
                action();
                return 0;
            });
        }

        private static string EscapeFormat(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Replace("{", "{{").Replace("}", "}}");
        }

        private static GitHubRelease GetLastRelease(List<GitHubRelease> releases)
        {
            if (releases == null)
                return null;

            string channel = ConfigAppSettings.GetValue("updateChannel", "Stable");
            bool isBeta = channel.Equals("Beta") || channel.Equals("Preview");

            foreach (var release in releases)
            {
                if (release == null || string.IsNullOrEmpty(release.TagName))
                    continue;
                if (!Regex.IsMatch(release.TagName, @"^v(\d+)\.(\d+)\.(\d+)\.(\d+)$"))
                    continue;
                if (isBeta || !release.Prerelease)
                    return release;
            }
            return null;
        }

        private static bool IsNewerVersion(
            string localVer,
            string major,
            string minor,
            string patch,
            string revision
        )
        {
            try
            {
                var remoteVersion = new Version(
                    int.Parse(major),
                    int.Parse(minor),
                    int.Parse(patch),
                    int.Parse(revision)
                );

                string normalizedLocal = localVer;
                int metadataIndex = normalizedLocal.IndexOfAny(new[] { '+', '(' });
                if (metadataIndex > 0)
                    normalizedLocal = normalizedLocal.Substring(0, metadataIndex);

                if (!Version.TryParse(normalizedLocal, out var localVersion))
                    return false;

                return remoteVersion > localVersion;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Version comparison failed: " + ex.Message);
                return false;
            }
        }
    }
}
