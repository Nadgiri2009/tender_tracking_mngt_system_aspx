using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.SessionState;

namespace DepartmentModule.Helpers
{
    public static class OtpChallenge
    {
        private const int MaxRequestsPerWindow = 3;
        private static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
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
            if (IsValidIndianMobile(mobile)) return true;

            HttpContext context = HttpContext.Current;
            string configuredMode = GetSetting("OtpDeliveryMode")
                ?? GetSetting("LoginOtpDeliveryMode")
                ?? "Auto";
            if (context == null || context.Request == null || !context.Request.IsLocal ||
                !String.Equals(configuredMode, "Console", StringComparison.OrdinalIgnoreCase) ||
                mobile == null || mobile.Length != 10)
                return false;

            for (int i = 0; i < mobile.Length; i++)
            {
                if (mobile[i] < '0' || mobile[i] > '9') return false;
            }
            return true;
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

        public static bool TryDeliver(string purpose, string code, string mobile,
            out string deliveryMode, out string developmentCode)
        {
            developmentCode = String.Empty;
            string configuredMode = GetSetting("OtpDeliveryMode")
                ?? GetSetting("LoginOtpDeliveryMode")
                ?? "Auto";

            bool smsConfigured = HasSmsConfiguration();
            if (String.Equals(configuredMode, "Sms", StringComparison.OrdinalIgnoreCase) ||
                (String.Equals(configuredMode, "Auto", StringComparison.OrdinalIgnoreCase) && smsConfigured))
            {
                deliveryMode = "Sms";
                if (!smsConfigured)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        "DepartmentModule: SMS OTP delivery was selected but required gateway settings are missing.");
                    return false;
                }
                return TrySendSms(purpose, code, mobile);
            }

            if (String.Equals(configuredMode, "Console", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(configuredMode, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                deliveryMode = "Console";
                bool delivered = WriteToDevelopmentOtpLog(purpose, code);
                if (delivered && HttpContext.Current != null &&
                    HttpContext.Current.Request != null && HttpContext.Current.Request.IsLocal)
                    developmentCode = code;
                return delivered;
            }

            deliveryMode = String.Empty;
            System.Diagnostics.Trace.TraceWarning(
                "DepartmentModule: OTP delivery mode is invalid. Use Auto, Sms, or Console.");
            return false;
        }

        private static bool HasSmsConfiguration()
        {
            return !String.IsNullOrWhiteSpace(GetSetting("SmsGatewayUrl")) &&
                !String.IsNullOrWhiteSpace(GetSetting("SmsGatewayAppId")) &&
                !String.IsNullOrWhiteSpace(GetSetting("SmsGatewayUserId")) &&
                !String.IsNullOrWhiteSpace(GetSetting("SmsGatewayPassword")) &&
                !String.IsNullOrWhiteSpace(GetSetting("SmsGatewaySender")) &&
                !String.IsNullOrWhiteSpace(GetSetting("SmsGatewayDltTemplateId")) &&
                !String.IsNullOrWhiteSpace(GetSetting("SmsOtpMessageTemplate"));
        }

        private static bool TrySendSms(string purpose, string code, string mobile)
        {
            string configuredUrl = GetSetting("SmsGatewayUrl");
            Uri endpoint;
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out endpoint) ||
                !String.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: SMS OTP delivery requires a valid HTTPS gateway URL.");
                return false;
            }

            string message = GetSetting("SmsOtpMessageTemplate")
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

            NameValueCollection query = HttpUtility.ParseQueryString(endpoint.Query);
            query["appid"] = GetSetting("SmsGatewayAppId");
            query["userId"] = GetSetting("SmsGatewayUserId");
            query["pass"] = GetSetting("SmsGatewayPassword");
            query["contenttype"] = "1";
            query["from"] = GetSetting("SmsGatewaySender");
            query["to"] = "91" + localMobile;
            query["text"] = message;
            query["alert"] = "1";
            query["selfid"] = "true";
            query["dlrreq"] = "true";
            query["dtm"] = GetSetting("SmsGatewayDltTemplateId");
            endpoint = new UriBuilder(endpoint) { Query = query.ToString() }.Uri;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
                request.Method = "GET";
                request.Timeout = 15000;
                request.ReadWriteTimeout = 15000;
                request.AllowAutoRedirect = false;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                    {
                        System.Diagnostics.Trace.TraceWarning(
                            "DepartmentModule: SMS gateway returned HTTP status {0}.",
                            (int)response.StatusCode);
                        return false;
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

        private static bool WriteToDevelopmentOtpLog(string purpose, string code)
        {
            HttpContext context = HttpContext.Current;
            if (context == null || context.Request == null || !context.Request.IsLocal)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: Development OTP output is restricted to local requests.");
                return false;
            }

            try
            {
                string output = "[DepartmentModule] Development " + purpose + " OTP: " + code;
                string logPath = Path.Combine(Path.GetTempPath(), "TenderManagement-DevelopmentOtp.log");
                File.AppendAllText(logPath, output + Environment.NewLine, new UTF8Encoding(false));
                return true;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: Development OTP could not be written to the local OTP log: {0}",
                    ex.GetType().Name);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "DepartmentModule: Development OTP could not be written to the local OTP log: {0}",
                    ex.GetType().Name);
                return false;
            }
        }

        private static string GetSetting(string key)
        {
            string environmentValue = Environment.GetEnvironmentVariable("SMC_" + key);
            return environmentValue ?? ConfigurationManager.AppSettings[key];
        }
    }
}
