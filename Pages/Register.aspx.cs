using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class Register : Page
    {
        protected Literal Message;
        protected Label MobileValidationMessage;
        protected TextBox FullNameText, EmailText, MobileText, OtpText, CompanyNameText,
            ContactPersonNameText, CompanyContactMobileText, CompanyEmailText,
            CompanyAddressText, CityText, StateText, PinCodeText, GstNumberText, PanNumberText;
        protected DropDownList CompanyTypeSelect;
        protected Panel OtpPanel, MobileVerifiedPanel;
        protected Button RequestOtpButton, VerifyOtpButton, CreateAccountButton, ChangeMobileButton;
        protected HyperLink LoginLink;

        private const string OtpMobileKey = "RegisterOtpMobile";
        private const string OtpHashKey = "RegisterOtpHash";
        private const string OtpSaltKey = "RegisterOtpSalt";
        private const string OtpExpiresKey = "RegisterOtpExpiresUtc";
        private const string OtpAttemptsKey = "RegisterOtpAttempts";
        private const string VerifiedMobileKey = "RegisterOtpVerifiedMobile";
        private const string VerifiedUntilKey = "RegisterOtpVerifiedUntilUtc";
        private const int OtpLifetimeMinutes = 5;
        private const int OtpMaxAttempts = 5;
        private const int VerifiedMobileLifetimeMinutes = 15;

        protected void Page_Load(object sender, EventArgs e)
        {
            bool mobileVerified = IsMobileVerified();
            OtpPanel.Visible = Session[OtpHashKey] != null && !mobileVerified;
            MobileVerifiedPanel.Visible = mobileVerified;
            CreateAccountButton.Visible = mobileVerified;
            RequestOtpButton.Visible = !mobileVerified;
            MobileText.ReadOnly = mobileVerified;
            RequestOtpButton.Text = OtpPanel.Visible ? "Resend verification OTP" : "Send verification OTP";
            RequestOtpButton.Attributes["data-cooldown-seconds"] =
                OtpChallenge.GetRequestRetryAfterSeconds(Session, "Register", DateTime.UtcNow)
                    .ToString(CultureInfo.InvariantCulture);

            OtpText.Attributes["inputmode"] = "numeric";
            OtpText.Attributes["pattern"] = "[0-9]{6}";
            MobileText.Attributes["inputmode"] = "numeric";
            MobileText.Attributes["pattern"] = "[0-9]{10}";
            MobileText.Attributes["maxlength"] = "10";
            CompanyContactMobileText.Attributes["inputmode"] = "numeric";
            CompanyContactMobileText.Attributes["pattern"] = "[0-9]{10}";
            CompanyContactMobileText.Attributes["maxlength"] = "10";
            PinCodeText.Attributes["inputmode"] = "numeric";
            PinCodeText.Attributes["pattern"] = "[1-9][0-9]{5}";

            int applyTenderId = GetApplyTenderId();
            LoginLink.NavigateUrl = ResolveUrl("~/Pages/Login.aspx" +
                (applyTenderId > 0 ? "?applyTenderId=" + applyTenderId.ToString(CultureInfo.InvariantCulture) : String.Empty));
        }

        protected void FullName_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = HasLength(args.Value, 2, 150);
        }

        protected void Email_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = IsValidEmail(args.Value);
        }

        protected void Mobile_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = OtpChallenge.IsValidIndianMobile((args.Value ?? String.Empty).Trim());
        }

        protected void CompanyName_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = HasLength(args.Value, 2, 150);
        }

        protected void ContactPersonName_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = HasLength(args.Value, 2, 150);
        }

        protected void CompanyContactMobile_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = OtpChallenge.IsValidIndianMobile((args.Value ?? String.Empty).Trim());
        }

        protected void CompanyEmail_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = IsValidEmail(args.Value);
        }

        protected void CompanyAddress_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = !String.IsNullOrWhiteSpace(args.Value) && args.Value.Trim().Length <= 300;
        }

        protected void City_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = HasLength(args.Value, 1, 100);
        }

        protected void State_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = HasLength(args.Value, 1, 100);
        }

        protected void PinCode_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = Regex.IsMatch((args.Value ?? String.Empty).Trim(), @"^[1-9][0-9]{5}$");
        }

        protected void GstNumber_ServerValidate(object source, ServerValidateEventArgs args)
        {
            string value = (args.Value ?? String.Empty).Trim();
            args.IsValid = value.Length == 0 ||
                Regex.IsMatch(value.ToUpperInvariant(), @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$");
        }

        protected void PanNumber_ServerValidate(object source, ServerValidateEventArgs args)
        {
            string value = (args.Value ?? String.Empty).Trim();
            args.IsValid = value.Length == 0 ||
                Regex.IsMatch(value.ToUpperInvariant(), @"^[A-Z]{5}[0-9]{4}[A-Z]$");
        }

        protected void RequestOtpButton_Click(object sender, EventArgs e)
        {
            Message.Text = String.Empty;
            MobileValidationMessage.Text = String.Empty;

            string enteredMobile = (MobileText.Text ?? String.Empty).Trim();
            if (!OtpChallenge.IsValidIndianMobile(enteredMobile))
            {
                MobileValidationMessage.Text = "Enter a valid 10-digit Indian mobile number.";
                return;
            }

            string mobile = OtpChallenge.NormalizeMobile(enteredMobile);
            string email = NormalizeEmail(EmailText.Text);
            DateTime now = DateTime.UtcNow;
            int retryAfterSeconds;
            if (!OtpChallenge.TryReserveRequest(Session, "Register", now, out retryAfterSeconds))
            {
                RequestOtpButton.Attributes["data-cooldown-seconds"] =
                    retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                ShowMessage("Please wait " + retryAfterSeconds.ToString(CultureInfo.InvariantCulture) +
                    " seconds before requesting another OTP.");
                return;
            }

            ClearOtpChallenge();
            ClearVerifiedMobile();
            OtpPanel.Visible = false;
            RequestOtpButton.Text = "Resend verification OTP";
            RequestOtpButton.Attributes["data-cooldown-seconds"] =
                OtpChallenge.GetRequestRetryAfterSeconds(Session, "Register", now)
                    .ToString(CultureInfo.InvariantCulture);

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    if (AccountAlreadyExists(connection, email, mobile, String.Empty, null))
                    {
                        ShowMessage("An account already exists with this email or mobile number.");
                        return;
                    }
                }

                string code = OtpChallenge.CreateCode();
                string deliveryMode, developmentCode;
                if (!OtpChallenge.TryDeliver("registration", code, mobile,
                        out deliveryMode, out developmentCode))
                {
                    ShowMessage(String.Equals(deliveryMode, "Sms", StringComparison.OrdinalIgnoreCase)
                        ? "The SMS gateway could not send the OTP. Please retry later or contact support."
                        : "OTP delivery is unavailable on this server. Please contact support.");
                    return;
                }

                byte[] salt = OtpChallenge.CreateSalt();
                Session[OtpMobileKey] = mobile;
                Session[OtpHashKey] = OtpChallenge.HashCode(code, salt);
                Session[OtpSaltKey] = salt;
                Session[OtpExpiresKey] = now.AddMinutes(OtpLifetimeMinutes);
                Session[OtpAttemptsKey] = 0;
                OtpPanel.Visible = true;
                ShowMessage(String.Equals(deliveryMode, "Sms", StringComparison.OrdinalIgnoreCase)
                    ? "A verification OTP has been sent to your mobile number."
                    : "Development OTP (local only): " + developmentCode, true);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Registration OTP could not be issued.", ex);
                ShowMessage("Registration is temporarily unavailable. Please retry or contact support.");
            }
        }

        protected void VerifyOtpButton_Click(object sender, EventArgs e)
        {
            Page.Validate("OtpVerification");
            if (!Page.IsValid) return;

            string mobile = OtpChallenge.NormalizeMobile(MobileText.Text);
            string pendingMobile = Session[OtpMobileKey] as string;
            string expectedHash = Session[OtpHashKey] as string;
            byte[] salt = Session[OtpSaltKey] as byte[];
            DateTime expiresUtc = Session[OtpExpiresKey] is DateTime
                ? (DateTime)Session[OtpExpiresKey]
                : DateTime.MinValue;

            if (expectedHash == null || salt == null ||
                !String.Equals(pendingMobile, mobile, StringComparison.Ordinal))
            {
                ClearOtpChallenge();
                OtpPanel.Visible = false;
                ShowMessage("Request a new registration OTP.");
                return;
            }

            if (DateTime.UtcNow > expiresUtc)
            {
                ClearOtpChallenge();
                OtpPanel.Visible = false;
                ShowMessage("The OTP has expired. Request a new one.");
                return;
            }

            int attempts = Session[OtpAttemptsKey] is int ? (int)Session[OtpAttemptsKey] : 0;
            attempts++;
            Session[OtpAttemptsKey] = attempts;
            if (!OtpChallenge.Matches(expectedHash, OtpText.Text, salt))
            {
                OtpText.Text = String.Empty;
                if (attempts >= OtpMaxAttempts)
                {
                    ClearOtpChallenge();
                    OtpPanel.Visible = false;
                    ShowMessage("Too many incorrect OTP attempts. Request a new OTP.");
                }
                else
                {
                    OtpPanel.Visible = true;
                    ShowMessage("Invalid OTP. Check the code and try again.");
                }
                return;
            }

            ClearOtpChallenge();
            Session[VerifiedMobileKey] = pendingMobile;
            Session[VerifiedUntilKey] = DateTime.UtcNow.AddMinutes(VerifiedMobileLifetimeMinutes);
            MobileText.ReadOnly = true;
            MobileVerifiedPanel.Visible = true;
            CreateAccountButton.Visible = true;
            RequestOtpButton.Visible = false;
            MobileValidationMessage.Text = String.Empty;
            ShowMessage("Your mobile number is verified. Complete the required company details to create your account.", true);
        }

        protected void ChangeMobileButton_Click(object sender, EventArgs e)
        {
            ClearOtpChallenge();
            ClearVerifiedMobile();
            MobileText.ReadOnly = false;
            MobileVerifiedPanel.Visible = false;
            CreateAccountButton.Visible = false;
            RequestOtpButton.Visible = true;
            OtpPanel.Visible = false;
            OtpText.Text = String.Empty;
            Message.Text = String.Empty;
            MobileValidationMessage.Text = String.Empty;
        }

        protected void CreateAccountButton_Click(object sender, EventArgs e)
        {
            Page.Validate("Registration");
            if (!Page.IsValid) return;

            string mobile = Session[VerifiedMobileKey] as string;
            if (!IsMobileVerified() || String.IsNullOrEmpty(mobile) ||
                !String.Equals(mobile, OtpChallenge.NormalizeMobile(MobileText.Text), StringComparison.Ordinal))
            {
                ClearVerifiedMobile();
                MobileText.ReadOnly = false;
                MobileVerifiedPanel.Visible = false;
                CreateAccountButton.Visible = false;
                RequestOtpButton.Visible = true;
                ShowMessage("Verify your mobile number again before creating an account.");
                return;
            }

            string fullName = FullNameText.Text.Trim();
            string email = NormalizeEmail(EmailText.Text);
            string companyName = CompanyNameText.Text.Trim();
            string contactPerson = ContactPersonNameText.Text.Trim();
            string companyMobile = OtpChallenge.NormalizeMobile(CompanyContactMobileText.Text.Trim());
            string companyEmail = NormalizeEmail(CompanyEmailText.Text);
            string address = CompanyAddressText.Text.Trim();
            string city = CityText.Text.Trim();
            string state = StateText.Text.Trim();
            string pinCode = PinCodeText.Text.Trim();
            string gstNumber = (GstNumberText.Text ?? String.Empty).Trim().ToUpperInvariant();
            string panNumber = (PanNumberText.Text ?? String.Empty).Trim().ToUpperInvariant();
            string companyType = CompanyTypeSelect.SelectedValue;
            int userId;
            string destination;

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (AccountAlreadyExists(connection, email, mobile, companyEmail, transaction))
                    {
                        ShowMessage("An account already exists with this email or mobile number, or the company email is already registered.");
                        return;
                    }

                    using (SqlCommand insertCommand = new SqlCommand(
                        @"INSERT INTO dbo.Users
                              (FullName,Email,Mobile,PasswordHash,RoleId,DepartmentId,IsActive,CreatedDate,AccountType)
                          OUTPUT INSERTED.UserId
                          VALUES (@FullName,@Email,@Mobile,@PasswordHash,NULL,NULL,1,SYSUTCDATETIME(),N'ExternalBidder');",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(insertCommand, "@FullName", SqlDbType.NVarChar, fullName, 150);
                        ModuleDb.AddParameter(insertCommand, "@Email", SqlDbType.NVarChar, email, 150);
                        ModuleDb.AddParameter(insertCommand, "@Mobile", SqlDbType.NVarChar, mobile, 20);
                        ModuleDb.AddParameter(insertCommand, "@PasswordHash", SqlDbType.NVarChar,
                            BCrypt.Net.BCrypt.HashPassword(OtpChallenge.CreateRandomToken()), 500);
                        userId = Convert.ToInt32(insertCommand.ExecuteScalar());
                    }

                    int contractorId;
                    string registrationNo = "CTR-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
                    using (SqlCommand insertCommand = new SqlCommand(
                        @"INSERT INTO dbo.Contractors
                              (Name,RegistrationNo,Address,ContactNo,Email,UserId,IsActive,CreatedDate,
                               ContactPersonName,City,State,PinCode,GSTNumber,PANNumber,CompanyType)
                          OUTPUT INSERTED.ContractorId
                          VALUES (@CompanyName,@RegistrationNo,@Address,@ContactNo,@CompanyEmail,@UserId,1,SYSUTCDATETIME(),
                                  @ContactPersonName,@City,@State,@PinCode,@GSTNumber,@PANNumber,@CompanyType);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(insertCommand, "@CompanyName", SqlDbType.NVarChar, companyName, 150);
                        ModuleDb.AddParameter(insertCommand, "@RegistrationNo", SqlDbType.NVarChar, registrationNo, 100);
                        ModuleDb.AddParameter(insertCommand, "@Address", SqlDbType.NVarChar, address, 300);
                        ModuleDb.AddParameter(insertCommand, "@ContactNo", SqlDbType.NVarChar, companyMobile, 20);
                        ModuleDb.AddParameter(insertCommand, "@CompanyEmail", SqlDbType.NVarChar, companyEmail, 150);
                        ModuleDb.AddParameter(insertCommand, "@UserId", SqlDbType.Int, userId);
                        ModuleDb.AddParameter(insertCommand, "@ContactPersonName", SqlDbType.NVarChar, contactPerson, 150);
                        ModuleDb.AddParameter(insertCommand, "@City", SqlDbType.NVarChar, city, 100);
                        ModuleDb.AddParameter(insertCommand, "@State", SqlDbType.NVarChar, state, 100);
                        ModuleDb.AddParameter(insertCommand, "@PinCode", SqlDbType.NVarChar, pinCode, 6);
                        ModuleDb.AddParameter(insertCommand, "@GSTNumber", SqlDbType.NVarChar, String.IsNullOrEmpty(gstNumber) ? null : gstNumber, 15);
                        ModuleDb.AddParameter(insertCommand, "@PANNumber", SqlDbType.NVarChar, String.IsNullOrEmpty(panNumber) ? null : panNumber, 10);
                        ModuleDb.AddParameter(insertCommand, "@CompanyType", SqlDbType.NVarChar, companyType, 30);
                        contractorId = Convert.ToInt32(insertCommand.ExecuteScalar());
                    }

                    ModuleDb.AddAudit(connection, transaction, userId, "Register", "User", userId,
                        "Contractor account registered.", Request.UserHostAddress);
                    ModuleDb.AddAudit(connection, transaction, userId, "Register", "Contractor", contractorId,
                        "Contractor profile registered.", Request.UserHostAddress);
                    transaction.Commit();
                    destination = GetApplyTenderId() > 0
                        ? "~/Pages/Bids.aspx?tenderId=" + GetApplyTenderId().ToString(CultureInfo.InvariantCulture)
                        : "~/Pages/MyApplications.aspx";
                }

                ClearOtpChallenge();
                ClearVerifiedMobile();
                EstablishSession(userId, fullName, "Contractor", null);
                Response.Redirect(ResolveUrl(destination), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Contractor registration failed.", ex);
                if (ex.Number == 2601 || ex.Number == 2627)
                    ShowMessage("An account already exists with this email or mobile number, or the company email is already registered.");
                else if (ex.Number == 207 || ex.Number == 208)
                    ShowMessage("The contractor registration database needs the latest schema upgrade. Apply the updated SMC_TENDER_DATABASE.sql script and retry.");
                else
                    ShowMessage("Registration could not be completed. Please retry or contact support.");
            }
        }

        private static bool AccountAlreadyExists(SqlConnection connection, string email, string mobile,
            string companyEmail, SqlTransaction transaction)
        {
            string lockHint = transaction == null ? String.Empty : " WITH (UPDLOCK,HOLDLOCK)";
            using (SqlCommand command = new SqlCommand(
                @"SELECT CASE WHEN
                    EXISTS
                    (
                        SELECT 1 FROM dbo.Users u" + lockHint + @"
                        WHERE (@Email<>N'' AND u.Email=@Email)
                           OR (@CompanyEmail<>N'' AND u.Email=@CompanyEmail)
                           OR " + OtpChallenge.IndianMobileMatchSql + @"
                    )
                    OR EXISTS
                    (
                        SELECT 1 FROM dbo.Contractors c" + lockHint + @"
                        WHERE (@Email<>N'' AND c.Email=@Email)
                           OR (@CompanyEmail<>N'' AND c.Email=@CompanyEmail)
                    )
                    THEN 1 ELSE 0 END;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@Email", SqlDbType.NVarChar, email ?? String.Empty, 150);
                ModuleDb.AddParameter(command, "@Mobile", SqlDbType.NVarChar, mobile, 20);
                ModuleDb.AddParameter(command, "@CompanyEmail", SqlDbType.NVarChar, companyEmail ?? String.Empty, 150);
                return Convert.ToInt32(command.ExecuteScalar()) != 0;
            }
        }

        private bool IsMobileVerified()
        {
            string verifiedMobile = Session[VerifiedMobileKey] as string;
            DateTime verifiedUntil = Session[VerifiedUntilKey] is DateTime
                ? (DateTime)Session[VerifiedUntilKey]
                : DateTime.MinValue;
            if (!String.IsNullOrEmpty(verifiedMobile) && DateTime.UtcNow <= verifiedUntil)
                return true;

            ClearVerifiedMobile();
            return false;
        }

        private void EstablishSession(int userId, string fullName, string role, int? departmentId)
        {
            string userKey = ConfigurationManager.AppSettings["SessionUserIdKey"] ?? "UserId";
            string roleKey = ConfigurationManager.AppSettings["SessionRoleKey"] ?? "RoleName";
            string departmentKey = ConfigurationManager.AppSettings["SessionDepartmentIdKey"] ?? "DepartmentId";
            Session[userKey] = userId;
            Session[roleKey] = role;
            Session[departmentKey] = departmentId.HasValue ? (object)departmentId.Value : null;
            Session["FullName"] = fullName;
            FormsAuthentication.SetAuthCookie(userId.ToString(CultureInfo.InvariantCulture), false);
        }

        private void ClearOtpChallenge()
        {
            Session.Remove(OtpMobileKey);
            Session.Remove(OtpHashKey);
            Session.Remove(OtpSaltKey);
            Session.Remove(OtpExpiresKey);
            Session.Remove(OtpAttemptsKey);
        }

        private void ClearVerifiedMobile()
        {
            Session.Remove(VerifiedMobileKey);
            Session.Remove(VerifiedUntilKey);
        }

        private static bool HasLength(string value, int minimum, int maximum)
        {
            string trimmed = (value ?? String.Empty).Trim();
            return trimmed.Length >= minimum && trimmed.Length <= maximum;
        }

        private static bool IsValidEmail(string value)
        {
            string email = (value ?? String.Empty).Trim();
            return email.Length <= 150 &&
                Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? String.Empty).Trim().ToLowerInvariant();
        }

        private void ShowMessage(string message, bool success = false)
        {
            Message.Text = "<div class=\"message" + (success ? " success" : String.Empty) + "\">" +
                ModuleDb.Html(message) + "</div>";
        }

        private int GetApplyTenderId()
        {
            int tenderId;
            return Int32.TryParse(Request.QueryString["applyTenderId"], out tenderId) && tenderId > 0
                ? tenderId
                : 0;
        }
    }
}
