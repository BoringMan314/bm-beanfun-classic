using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace Beanfun
{
    public partial class AccountLoginBrowser : Window
    {
        private readonly string _skey;
        private readonly string _account;
        private readonly string _password;
        private bool _loginCompleted;

        public AccountLoginBrowser(string skey, string account, string password)
        {
            InitializeComponent();
            _skey = skey;
            _account = account ?? "";
            _password = password ?? "";
            Environment.SetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                Path.GetTempPath() + "\\BeanfunClassic\\WebView2\\"
            );
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (!_loginCompleted)
                App.MainWnd.bfClient = null;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            wb_Main.CoreWebView2InitializationCompleted += OnWebViewReady;

            if (bool.Parse(ConfigAppSettings.GetValue("disableHardwareAcceleration", "false")))
            {
                string userDataFolder = Path.Combine(
                    Path.GetTempPath(),
                    "BeanfunClassic",
                    "WebView2"
                );
                var options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments = "--disable-gpu --disable-gpu-compositing";
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await wb_Main.EnsureCoreWebView2Async(env);
            }

            wb_Main.Source = new Uri(
                $"https://login.beanfun.com/Login/Index?pSKey={_skey}"
            );
        }

        private async void OnWebViewReady(
            object sender,
            CoreWebView2InitializationCompletedEventArgs e
        )
        {
            if (!e.IsSuccess || wb_Main.CoreWebView2 == null)
                return;

            wb_Main.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                wb_Main.CoreWebView2.Navigate(args.Uri);
                args.Handled = true;
            };

            wb_Main.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            try
            {
                await wb_Main.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    BuildAutofillScript(_account, _password)
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountLoginBrowser] autofill script failed: {ex.Message}");
            }

            if (App.MainWnd.bfClient != null)
            {
                foreach (Cookie cookie in App.MainWnd.bfClient.GetCookies())
                {
                    wb_Main.CoreWebView2.CookieManager.AddOrUpdateCookie(
                        wb_Main.CoreWebView2.CookieManager.CreateCookie(
                            cookie.Name,
                            cookie.Value,
                            cookie.Domain,
                            cookie.Path
                        )
                    );
                }
            }
        }

        private static string BuildAutofillScript(string account, string password)
        {
            string accountJs = JsonConvert.SerializeObject(account);
            string passwordJs = JsonConvert.SerializeObject(password);

            return $@"
(() => {{
  const ACC = {accountJs};
  const PW = {passwordJs};
  const setVal = (el, val) => {{
    if (!el || !val) return false;
    try {{
      const desc = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(el), 'value');
      if (desc && desc.set) {{ desc.set.call(el, val); }} else {{ el.value = val; }}
      el.dispatchEvent(new Event('input', {{ bubbles: true }}));
      el.dispatchEvent(new Event('change', {{ bubbles: true }}));
      return true;
    }} catch (e) {{ return false; }}
  }};
  let accDone = false, pwDone = false;
  const tryFill = () => {{
    if (!accDone) {{
      const a = document.querySelector('input[placeholder=""\\u8acb\\u8f38\\u5165\\u5e33\\u865f""]')
        || document.querySelector('input[name=""aaa""]');
      if (a) accDone = setVal(a, ACC);
    }}
    if (!pwDone) {{
      const p = document.querySelector('input[type=""password""]')
        || document.querySelector('input[placeholder=""\\u8acb\\u8f38\\u5165\\u5bc6\\u78bc""]')
        || document.querySelector('input[name=""inputName""]');
      if (p) pwDone = setVal(p, PW);
    }}
    return accDone && pwDone;
  }};
  const start = () => {{
    if (tryFill()) return;
    const obs = new MutationObserver(() => {{ if (tryFill()) obs.disconnect(); }});
    obs.observe(document.documentElement, {{ childList: true, subtree: true }});
    setTimeout(() => obs.disconnect(), 300000);
  }};
  if (document.readyState === 'loading') {{
    document.addEventListener('DOMContentLoaded', start, {{ once: true }});
  }} else {{
    start();
  }}
}})();";
        }

        private async void OnNavigationCompleted(
            object sender,
            CoreWebView2NavigationCompletedEventArgs e
        )
        {
            string url = wb_Main.Source?.ToString() ?? "";
            if (
                url.Contains("beanfun.com")
                && (
                    url.Contains("return.aspx")
                    || url.Contains("index.aspx")
                    || url.Contains("SendLogin")
                )
            )
            {
                await TryCompleteLogin();
            }
        }

        private async Task TryCompleteLogin()
        {
            try
            {
                var twCookies = await wb_Main.CoreWebView2.CookieManager.GetCookiesAsync(
                    "https://tw.beanfun.com"
                );
                var loginCookies = await wb_Main.CoreWebView2.CookieManager.GetCookiesAsync(
                    "https://login.beanfun.com"
                );
                var newLoginCookies = await wb_Main.CoreWebView2.CookieManager.GetCookiesAsync(
                    "https://tw.newlogin.beanfun.com"
                );

                string webToken = null;
                foreach (var cookie in twCookies)
                {
                    if (cookie.Name == "bfWebToken")
                    {
                        webToken = cookie.Value;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(webToken))
                    return;

                var allCookies = new System.Collections.Generic.List<Cookie>();
                ConvertCookies(twCookies, allCookies);
                ConvertCookies(loginCookies, allCookies);
                ConvertCookies(newLoginCookies, allCookies);

                Dispatcher.Invoke(() =>
                {
                    _loginCompleted = true;
                    Close();
                    App.MainWnd.RecaptchaLoginCompleted(webToken, allCookies);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AccountLoginBrowser] TryCompleteLogin failed: {ex.Message}");
            }
        }

        private static void ConvertCookies(
            System.Collections.Generic.IReadOnlyList<CoreWebView2Cookie> source,
            System.Collections.Generic.List<Cookie> target
        )
        {
            foreach (var wv2Cookie in source)
            {
                try
                {
                    target.Add(
                        new Cookie(
                            wv2Cookie.Name,
                            wv2Cookie.Value,
                            string.IsNullOrEmpty(wv2Cookie.Path) ? "/" : wv2Cookie.Path,
                            wv2Cookie.Domain.TrimStart('.')
                        )
                    );
                }
                catch { }
            }
        }

        private void wb_Main_NavigationStarting(
            object sender,
            CoreWebView2NavigationStartingEventArgs e
        )
        {
            Title =
                Application.Current.TryFindResource("AccountLoginTitle") as string
                ?? "Account Login";
        }
    }
}
