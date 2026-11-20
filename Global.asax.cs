using System;
using System.Configuration;
using System.Web;
using System.Web.Security;

namespace DepartmentModule
{
    public class ModuleApplication : HttpApplication
    {
        protected void Application_Start(object sender, EventArgs e)
        {
            System.Web.UI.ValidationSettings.UnobtrusiveValidationMode =
                System.Web.UI.UnobtrusiveValidationMode.None;
        }

        protected void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {
            string otpMode = Environment.GetEnvironmentVariable("SMC_OtpDeliveryMode")
                ?? ConfigurationManager.AppSettings["OtpDeliveryMode"];
            if (Request.IsLocal && !Request.IsSecureConnection &&
                String.Equals(otpMode, "Console", StringComparison.OrdinalIgnoreCase))
            {
                // Keep HTTP development sessions usable without relaxing cookies for remote requests.
                HttpCookie sessionCookie = Response.Cookies["ASP.NET_SessionId"];
                if (sessionCookie != null)
                    sessionCookie.Secure = false;
            }

            HttpCookie authenticationCookie = Response.Cookies[FormsAuthentication.FormsCookieName];
            if (authenticationCookie != null)
            {
                bool localConsoleHttp = Request.IsLocal && !Request.IsSecureConnection &&
                    String.Equals(otpMode, "Console", StringComparison.OrdinalIgnoreCase);
                authenticationCookie.Secure = !localConsoleHttp;
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception error = Server.GetLastError();
            if (error != null)
            {
                System.Diagnostics.Trace.TraceError("DepartmentModule: Unhandled module error. {0}", error);
            }
        }
    }
}
