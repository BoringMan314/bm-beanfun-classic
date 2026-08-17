using System;
using System.Collections.Specialized;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Beanfun
{
    public partial class BeanfunClient : WebClient
    {
        private const string PppppLiteral =
            "1F552AEAFF976018F942B13690C990F60ED01510DDF89165F1658CCE7BC21DBA";

        public string GetOTP(
            ServiceAccount acc,
            string service_code = "610074",
            string service_region = "T9"
        )
        {
            try
            {
                string response;
                string host;
                string loginHost;
                if (App.LoginRegion == "TW")
                {
                    host = "tw.beanfun.com";
                    loginHost = "tw.newlogin.beanfun.com";
                }
                else
                {
                    host = "bfweb.hk.beanfun.com";
                    loginHost = "login.hk.beanfun.com";
                }
                string step2Url =
                    $"https://{host}/beanfun_block/game_zone/game_start_step2.aspx?service_code={service_code}&service_region={service_region}&sotp={acc.ssn}&dt={GetCurrentTime(2)}";
                response = this.DownloadString(step2Url);
                string referer = this.ResponseUri?.ToString() ?? step2Url;

                // 先看頁面有沒有 m_objData：有就走 v2，沒有才要求舊頁面那些字串
                LaunchHandoff launch = ParseLaunchHandoff(response);
                bool migrated = launch != null;

                Regex regex = new Regex("GetResultByLongPolling&key=(.*)\"");
                string longPollingKey = null;
                if (regex.IsMatch(response))
                    longPollingKey = regex.Match(response).Groups[1].Value;
                if (string.IsNullOrEmpty(longPollingKey) && !migrated)
                {
                    this.errmsg = "OTPNoLongPollingKey:" + response;
                    return null;
                }

                string unkKey = null;
                string unkValue = null;
                regex = new Regex("MyAccountData.ServiceAccountCreateTime \\+ \"(.*)=(.*)\";");
                if (regex.IsMatch(response))
                {
                    unkKey = Uri.UnescapeDataString(regex.Match(response).Groups[1].Value);
                    unkValue = Uri.UnescapeDataString(regex.Match(response).Groups[2].Value);
                }
                else if (App.LoginRegion == "TW" && !migrated)
                {
                    this.errmsg = "OTPNoUnkData";
                    return null;
                }

                if (string.IsNullOrEmpty(acc.screatetime))
                {
                    regex = new Regex("ServiceAccountCreateTime: \"([^\"]+)\"");
                    if (regex.IsMatch(response))
                        acc.screatetime = regex.Match(response).Groups[1].Value;
                    else if (!migrated)
                    {
                        this.errmsg = "OTPNoCreateTime";
                        return null;
                    }
                }

                this.Headers.Set("Referer", referer);

                GgmIntegrity local = GgmIntegrity.TryFromLocalDll();
                GgmIntegrity integrity = local ?? GgmIntegrity.Builtin(); // 本機 DLL 沒有則用內建常數
                bool githubTried = false;

                if (migrated)
                {
                    TryRecordServiceStart(
                        host,
                        acc,
                        service_code,
                        service_region,
                        unkKey,
                        unkValue,
                        referer
                    ); // v2 頁可能缺表單欄，失敗不擋
                    string otp = TryOtpV2(
                        host,
                        launch,
                        service_code,
                        service_region,
                        referer,
                        ref integrity,
                        local,
                        ref githubTried
                    );
                    if (otp != null)
                        return otp;
                    if (string.IsNullOrEmpty(longPollingKey))
                        return null; // 已遷移頁沒有舊 GET 可用
                }

                response = this.DownloadString(
                    $"https://{loginHost}/generic_handlers/get_cookies.ashx"
                );

                regex = new Regex("var m_strSecretCode = '(.*)';");
                if (!regex.IsMatch(response))
                {
                    this.errmsg = "OTPNoSecretCode";
                    return null;
                }
                string secretCode = regex.Match(response).Groups[1].Value;
                if (string.IsNullOrEmpty(secretCode))
                {
                    this.errmsg = "OTPNoSecretCode";
                    return null;
                }

                this.Headers.Set("Referer", referer);
                NameValueCollection payload = new NameValueCollection();
                payload.Add("service_code", service_code);
                payload.Add("service_region", service_region);
                payload.Add("service_account_id", acc.sid);
                payload.Add("sotp", acc.ssn);
                payload.Add("service_account_display_name", acc.sname);
                if (!string.IsNullOrEmpty(acc.screatetime))
                    payload.Add("service_account_create_time", acc.screatetime);
                if (unkKey != null && unkValue != null)
                    payload.Add(unkKey, unkValue);
                System.Net.ServicePointManager.Expect100Continue = false;
                this.UploadString(
                    $"https://{host}/beanfun_block/generic_handlers/record_service_start.ashx",
                    payload
                );
                this.DownloadString(
                    $"https://{host}/generic_handlers/get_result.ashx?meth=GetResultByLongPolling&key={longPollingKey}&_={GetCurrentTime()}"
                );

                response = this.DownloadString(
                    BuildLegacyOtpUrl(
                        host,
                        longPollingKey,
                        secretCode,
                        acc,
                        service_code,
                        service_region,
                        integrity
                    )
                );
                string legacyOtp = DecryptLegacyOtpEnvelope(response);
                if (legacyOtp != null)
                    return legacyOtp;
                if (
                    App.LoginRegion == "TW"
                    && TryNextIntegrity(ref integrity, local, ref githubTried)
                )
                {
                    response = this.DownloadString(
                        BuildLegacyOtpUrl(
                            host,
                            longPollingKey,
                            secretCode,
                            acc,
                            service_code,
                            service_region,
                            integrity
                        )
                    );
                    return DecryptLegacyOtpEnvelope(response);
                }
                return legacyOtp;
            }
            catch (Exception e)
            {
                this.errmsg =
                    (System.Windows.Application.Current.TryFindResource("GetOtpError") as string)
                    + "\n\n"
                    + e.Message
                    + "\n"
                    + e.StackTrace;
                return null;
            }
        }

        private class LaunchHandoff
        {
            public string Sn;
            public string Data;
        }

        private static LaunchHandoff ParseLaunchHandoff(string html)
        {
            Match obj = Regex.Match(
                html,
                @"var m_objData\s*=\s*\{(.*?)\}",
                RegexOptions.Singleline
            );
            if (!obj.Success)
                return null;
            string block = obj.Groups[1].Value;
            Match sn = Regex.Match(block, "\"sn\"\\s*:\\s*\"([^\"]*)\"");
            Match data = Regex.Match(block, "\"data\"\\s*:\\s*\"([^\"]*)\"");
            if (
                !sn.Success
                || !data.Success
                || string.IsNullOrEmpty(sn.Groups[1].Value)
                || string.IsNullOrEmpty(data.Groups[1].Value)
            )
                return null;
            return new LaunchHandoff { Sn = sn.Groups[1].Value, Data = data.Groups[1].Value };
        }

        private void TryRecordServiceStart(
            string host,
            ServiceAccount acc,
            string service_code,
            string service_region,
            string unkKey,
            string unkValue,
            string referer
        )
        {
            try
            {
                if (!string.IsNullOrEmpty(referer))
                    this.Headers.Set("Referer", referer);
                NameValueCollection payload = new NameValueCollection();
                payload.Add("service_code", service_code);
                payload.Add("service_region", service_region);
                payload.Add("service_account_id", acc.sid);
                payload.Add("sotp", acc.ssn);
                payload.Add("service_account_display_name", acc.sname);
                if (!string.IsNullOrEmpty(acc.screatetime))
                    payload.Add("service_account_create_time", acc.screatetime);
                if (unkKey != null && unkValue != null)
                    payload.Add(unkKey, unkValue);
                System.Net.ServicePointManager.Expect100Continue = false;
                this.UploadString(
                    $"https://{host}/beanfun_block/generic_handlers/record_service_start.ashx",
                    payload
                );
            }
            catch { }
        }

        private static bool NeedsIntegrityRetry(string errmsg)
        {
            return IsClientIntegrityFailed(errmsg) || IsQueryStringError(errmsg);
        }

        private static bool IsClientIntegrityFailed(string errmsg)
        {
            return !string.IsNullOrEmpty(errmsg)
                && errmsg.IndexOf("Client_Integrity_Failed", StringComparison.OrdinalIgnoreCase)
                    >= 0;
        }

        private static bool IsQueryStringError(string errmsg)
        {
            return !string.IsNullOrEmpty(errmsg)
                && errmsg.IndexOf("Query String Error", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string TryOtpV2(
            string host,
            LaunchHandoff launch,
            string service_code,
            string service_region,
            string referer,
            ref GgmIntegrity integrity,
            GgmIntegrity local,
            ref bool githubTried
        )
        {
            if (launch == null)
                return null; // 沒有 m_objData 的頁面走舊 GET
            while (true)
            {
                string otp = GetOtpV2(
                    host,
                    launch,
                    service_code,
                    service_region,
                    integrity,
                    referer
                );
                if (otp != null)
                    return otp;
                if (!TryNextIntegrity(ref integrity, local, ref githubTried))
                    return null;
            }
        }

        private bool TryNextIntegrity(
            ref GgmIntegrity integrity,
            GgmIntegrity local,
            ref bool githubTried
        )
        {
            if (!NeedsIntegrityRetry(this.errmsg))
                return false;

            GgmIntegrity builtin = GgmIntegrity.Builtin();
            if (local != null && integrity.SameAs(local) && !local.SameAs(builtin))
            {
                integrity = builtin; // 本機被拒則改內建常數
                return true;
            }

            if (githubTried)
                return false;
            githubTried = true;
            GgmIntegrity remote = GgmIntegrity.TryFromGitHub();
            if (remote == null || remote.SameAs(integrity))
                return false;
            integrity = remote; // 再從 GitHub JSON 取
            return true;
        }

        private string BuildLegacyOtpUrl(
            string host,
            string longPollingKey,
            string secretCode,
            ServiceAccount acc,
            string service_code,
            string service_region,
            GgmIntegrity integrity
        )
        {
            string otpUrl =
                $"https://{host}/beanfun_block/generic_handlers/get_webstart_otp.ashx?SN={longPollingKey}&WebToken={this.WebToken}&SecretCode={secretCode}&ppppp={PppppLiteral}&ServiceCode={service_code}&ServiceRegion={service_region}&ServiceAccount={acc.sid}&CreateTime={acc.screatetime.Replace(" ", "%20")}&d={Environment.TickCount}";
            if (App.LoginRegion == "TW")
                otpUrl +=
                    $"&CV={Uri.EscapeDataString(integrity.Cv)}&Hash={Uri.EscapeDataString(integrity.Hash)}&arch={Uri.EscapeDataString(integrity.Arch)}"; // 台灣舊 GET 必須帶這三個參數
            return otpUrl;
        }

        private string GetOtpV2(
            string host,
            LaunchHandoff launch,
            string service_code,
            string service_region,
            GgmIntegrity integrity,
            string referer
        )
        {
            string ticket = LaunchData.DecodeLaunchTicket(launch.Data); // 從 m_objData.data 解 LaunchTicket
            if (string.IsNullOrEmpty(ticket))
            {
                this.errmsg = "OTPNoLaunchTicket";
                return null;
            }

            if (!string.IsNullOrEmpty(referer))
                this.Headers.Set("Referer", referer);

            int tick = Environment.TickCount;
            try
            {
                this.DownloadString(
                    $"https://{host}/generic_handlers/adapter.ashx?cmd=01004&d={tick}"
                ); // 官方啟動前置 01004
                this.DownloadString(
                    $"https://{host}/generic_handlers/adapter.ashx?cmd=01003&service_code={service_code}&service_region={service_region}&d={tick}&CV={integrity.Cv}&Hash={integrity.Hash}&arch={integrity.Arch}"
                ); // 01003 帶 GGM 完整性
                this.DownloadString(
                    $"https://{host}/generic_handlers/adapter.ashx?cmd=06002&sn={launch.Sn}&result=1&d={tick}"
                ); // 06002
            }
            catch { }

            string body = new JObject
            {
                ["SN"] = launch.Sn,
                ["LaunchTicket"] = ticket,
                ["CV"] = integrity.Cv,
                ["Hash"] = integrity.Hash,
                ["arch"] = integrity.Arch,
            }.ToString(Newtonsoft.Json.Formatting.None);

            this.Headers.Set("User-Agent", userAgent);
            if (!string.IsNullOrEmpty(referer))
                this.Headers.Set("Referer", referer);
            this.Headers[HttpRequestHeader.ContentType] = "application/json; charset=utf-8";
            string response = this.UploadString(
                $"https://{host}/beanfun_block/generic_handlers/get_webstart_otp_v2.ashx",
                "POST",
                body
            );
            this.Headers.Remove(HttpRequestHeader.ContentType);

            if (string.IsNullOrWhiteSpace(response) || !response.TrimStart().StartsWith("{"))
            {
                this.errmsg = "OTPNoResponse";
                return null;
            }

            JObject json = JObject.Parse(response);
            int result = json["result"]?.Value<int>() ?? 0;
            if (result != 1)
            {
                string message = json["message"]?.ToString();
                this.errmsg =
                    (System.Windows.Application.Current.TryFindResource("GetOtpError") as string)
                    + "\r\n"
                    + (string.IsNullOrEmpty(message) ? $"result={result}" : message);
                return null;
            }

            string otpPayload = json["data"]?.ToString();
            if (string.IsNullOrEmpty(otpPayload))
            {
                this.errmsg = "OTPNoResponse";
                return null;
            }
            return DecryptOtpPayload(otpPayload);
        }

        private string DecryptLegacyOtpEnvelope(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                this.errmsg = "OTPNoResponse";
                return null;
            }
            string[] responses = response.Split(';');
            if (responses.Length < 2)
            {
                this.errmsg = "OTPNoResponse";
                return null;
            }
            if (responses[0] != "1")
            {
                this.errmsg =
                    (System.Windows.Application.Current.TryFindResource("GetOtpError") as string)
                    + "\r\n"
                    + responses[1];
                return null;
            }
            return DecryptOtpPayload(responses[1]);
        }

        private string DecryptOtpPayload(string payload)
        {
            if (payload == null || payload.Length < 8)
            {
                this.errmsg = "DecryptOTPError";
                return null;
            }
            string key = payload.Substring(0, 8);
            string plain = payload.Substring(8);
            string otp = WCDESComp.DecryStrHex(plain, key);
            if (otp != null)
            {
                otp = otp.Trim('\0');
                this.errmsg = null;
            }
            else
            {
                this.errmsg = "DecryptOTPError";
            }
            return otp;
        }
    }
}
