using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.SessionState;
using System.Text.RegularExpressions;

namespace DepartmentModule.Helpers
{
    public static class OtpChallenge
    {
        private const int MaxRequestsPerWindow = 3;
        private static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
        private static readonly string[] RequiredSmsSettings =
        {
            "SmsGatewayUrl",
            "SmsGatewayAppId",
            "SmsGatewayUserId",
            "SmsGatewayPassword",
            "SmsGatewaySender",
            "SmsGatewayDltTemplateId",
            "SmsOtpMessageTemplate",
            "SmsGatewaySuccessResponsePattern"
        };
        public const string NormalizedMobileSql =
            "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(u.Mobile,N''),N'+',N''),N' ',N''),N'-',N''),N'(',N''),N')',N''),N'.',N'')";
        public const string IndianMobileMatchSql =
            "(" + NormalizedMobileSql + "=@Mobile OR " + NormalizedMobileSql + "=RIGHT(@Mobile,10))";

        public static bool TryReserveRequest(HttpSessionState session, string keyPrefix, DateTime now)
        {
            int retryAfterSeconds;
            return TryReserveRequest(session, keyPrefix, now, out retryAfterSeconds);
        }

        public static bool TryReserveRequest(HttpSessionState session, string keyPrefix, DateTime now,
            out int retryAfterSeconds)
        {
            retryAfterSeconds = GetRequestRetryAfterSeconds(session, keyPrefix, now);
            if (retryAfterSeconds > 0) return false;

            string countKey = keyPrefix + "OtpRequests";
            string windowKey = keyPrefix + "OtpRequestWindowUtc";
            string lastRequestKey = keyPrefix + "OtpLastRequestUtc";
            DateTime windowStart = session[windowKey] is DateTime
                ? (DateTime)session[windowKey]
                : now;
            int requestCount = session[countKey] is int
                ? (int)session[countKey]
                : 0;

            if (now - windowStart >= RequestWindow)
            {
                windowStart = now;
                requestCount = 0;
            }

            session[countKey] = requestCount + 1;
            session[windowKey] = windowStart;
            session[lastRequestKey] = now;
            return true;
        }

        public static int GetRequestRetryAfterSeconds(HttpSessionState session, string keyPrefix, DateTime now)
        {
            string countKey = keyPrefix + "OtpRequests";
            string windowKey = keyPrefix + "OtpRequestWindowUtc";
            string lastRequestKey = keyPrefix + "OtpLastRequestUtc";
            int retryAfterSeconds = 0;

            if (session[lastRequestKey] is DateTime)
            {
                TimeSpan cooldownRemaining = ResendCooldown - (now - (DateTime)session[lastRequestKey]);
                if (cooldownRemaining > TimeSpan.Zero)
                    retryAfterSeconds = (int)Math.Ceiling(cooldownRemaining.TotalSeconds);
            }

            DateTime windowStart = session[windowKey] is DateTime
                ? (DateTime)session[windowKey]
                : now;
            int requestCount = session[countKey] is int
                ? (int)session[countKey]
                : 0;
            if (now - windowStart >= RequestWindow)
                requestCount = 0;

            if (requestCount >= MaxRequestsPerWindow)
            {
                TimeSpan windowRemaining = RequestWindow - (now - windowStart);
                int windowRetry = windowRemaining > TimeSpan.Zero
                    ? (int)Math.Ceiling(windowRemaining.TotalSeconds)
                    : 0;
                retryAfterSeconds = Math.Max(retryAfterSeconds, windowRetry);
            }

            return retryAfterSeconds;
        }

        public static string NormalizeMobile(string mobile)
        {
            StringBuilder digits = new StringBuilder();
            foreach (char character in mobile ?? String.Empty)
            {
                if (character >= '0' && character <= '9') digits.Append(character);
            }
            return digits.Length == 10 ? "91" + digits : digits.ToString();
        }

        public static bool IsValidIndianMobile(string mobile)
        {
            if (mobile == null || mobile.Length != 10 || mobile[0] < '6' || mobile[0] > '9')
                return false;

            for (int i = 0; i < mobile.Length; i++)
            {
                if (mobile[i] < '0' || mobile[i] > '9') return false;
            }
            return true;
        }

        public static bool IsValidLoginMobile(string mobile)
        {
            return IsValidIndianMobile(mobile);
        }

        public static int GetOtpLifetimeMinutes()
        {
            string configuredValue = GetSetting("OtpLifetimeMinutes");
            if (String.IsNullOrWhiteSpace(configuredValue)) return 5;

            int lifetimeMinutes;
            if (Int32.TryParse(configuredValue, out lifetimeMinutes) &&
                lifetimeMinutes >= 1 && lifetimeMinutes <= 15)
                return lifetimeMinutes;

            System.Diagnostics.Trace.TraceWarning(
                "DepartmentModule: OtpLifetimeMinutes must be between 1 and 15; using the 5-minute default.");
            return 5;
        }

        public static string CreateCode()
        {
            byte[] buffer = new byte[4];
            const uint range = 1000000;
            uint limit = UInt32.MaxValue - (UInt32.MaxValue % range);
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                uint value;
                do
                {
                    random.GetBytes(buffer);
                    value = BitConverter.ToUInt32(buffer, 0);
                } while (value >= limit);

                return (value % range).ToString("D6");
            }
        }

        public static byte[] CreateSalt()
        {
            byte[] salt = new byte[16];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
            }
            return salt;
        }

        public static string CreateRandomToken()
        {
            byte[] token = new byte[32];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(token);
            }
            return Convert.ToBase64String(token);
        }

        public static string HashCode(string code, byte[] salt)
        {
            byte[] codeBytes = Encoding.ASCII.GetBytes(code ?? String.Empty);
            byte[] input = new byte[salt.Length + codeBytes.Length];
            System.Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            System.Buffer.BlockCopy(codeBytes, 0, input, salt.Length, codeBytes.Length);
            using (SHA256 sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(input));
            }
        }

        public static bool Matches(string expectedHash, string suppliedCode, byte[] salt)
        {
            if (expectedHash == null || salt == null) return false;
            string actualHash = HashCode(suppliedCode, salt);
            int difference = expectedHash.Length ^ actualHash.Length;
            int length = Math.Max(expectedHash.Length, actualHash.Length);
            for (int i = 0; i < length; i++)
            {
                char expected = i < expectedHash.Length ? expectedHash[i] : (char)0;
                char actual = i < actualHash.Length ? actualHash[i] : (char)0;
                difference |= expected ^ actual;
            }
            return difference == 0;
        }

        public static bool TryDeliver(string purpose, string code, string mobile)
        {
            List<string> missingSettings = new List<string>();
            foreach (string setting in RequiredSmsSettings)
            {
                if (String.IsNullOrWhiteSpace(GetSmsSetting(setting)))
                    missingSettings.Add("SMC_" + setting);
            }

            if (missingSettings.Count > 0)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS OTP delivery is unavailable because these environment variables are missing: {0}.",
                    String.Join(", ", missingSettings));
                return false;
            }

            return TrySendSms(purpose, code, mobile);
        }

        private static bool TrySendSms(string purpose, string code, string mobile)
        {
            if (String.IsNullOrWhiteSpace(code) || code.Length != 6)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS OTP delivery was refused because the OTP format is invalid.");
                return false;
            }

            string configuredUrl = GetSmsSetting("SmsGatewayUrl");
            Uri endpoint;
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out endpoint) ||
                !String.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !String.IsNullOrEmpty(endpoint.Query) ||
                !String.IsNullOrEmpty(endpoint.Fragment))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS OTP delivery requires a valid HTTPS gateway URL without query parameters or fragments.");
                return false;
            }

            string message = GetSmsSetting("SmsOtpMessageTemplate")
                .Replace("{OTP}", code)
                .Replace("{PURPOSE}", purpose);
            string recipient = NormalizeMobile(mobile);
            string localMobile = recipient.Length == 12 && recipient.StartsWith("91", StringComparison.Ordinal)
                ? recipient.Substring(2)
                : recipient;
            if (!IsValidIndianMobile(localMobile))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS OTP delivery was refused because the recipient number is invalid.");
                return false;
            }

            Regex successResponsePattern;
            try
            {
                successResponsePattern = new Regex(
                    GetSmsSetting("SmsGatewaySuccessResponsePattern"),
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS gateway success response pattern is invalid.");
                return false;
            }

            NameValueCollection form = HttpUtility.ParseQueryString(String.Empty);
            form["appid"] = GetSmsSetting("SmsGatewayAppId");
            form["userId"] = GetSmsSetting("SmsGatewayUserId");
            form["pass"] = GetSmsSetting("SmsGatewayPassword");
            form["contenttype"] = "1";
            form["from"] = GetSmsSetting("SmsGatewaySender");
            form["to"] = "91" + localMobile;
            form["text"] = message;
            form["alert"] = "1";
            form["selfid"] = "true";
            form["dlrreq"] = "true";
            form["dtm"] = GetSmsSetting("SmsGatewayDltTemplateId");
            byte[] requestBody = Encoding.UTF8.GetBytes(form.ToString());

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
                request.ContentLength = requestBody.Length;
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.AllowAutoRedirect = false;
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(requestBody, 0, requestBody.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                    {
                        System.Diagnostics.Trace.TraceWarning(
                            "DepartmentModule: SMS gateway returned HTTP status {0}.",
                            (int)response.StatusCode);
                        return false;
                    }

                    using (Stream responseStream = response.GetResponseStream())
                    using (StreamReader responseReader = new StreamReader(responseStream))
                    {
                        char[] responseBuffer = new char[4096];
                        int responseLength = responseReader.Read(responseBuffer, 0, responseBuffer.Length);
                        if (responseLength == 0)
                        {
                            System.Diagnostics.Trace.TraceWarning(
                                "DepartmentModule: SMS gateway returned an empty acknowledgement.");
                            return false;
                        }

                        string responseBody = new string(responseBuffer, 0, responseLength);
                        try
                        {
                            if (!successResponsePattern.IsMatch(responseBody))
                            {
                                System.Diagnostics.Trace.TraceWarning(
                                    "DepartmentModule: SMS gateway response did not match the configured success acknowledgement.");
                                return false;
                            }
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            System.Diagnostics.Trace.TraceWarning(
                                "DepartmentModule: SMS gateway success response pattern timed out.");
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (WebException ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS gateway request failed: {0}",
                    ex.Status);
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS gateway response could not be read: {0}",
                    ex.GetType().Name);
                return false;
            }
        }

        private static string GetSetting(string key)
        {
            string environmentValue = Environment.GetEnvironmentVariable("SMC_" + key);
            return environmentValue ?? ConfigurationManager.AppSettings[key];
        }

        private static string GetSmsSetting(string key)
        {
            return Environment.GetEnvironmentVariable("SMC_" + key);
        }
    }
}
