<%@ Page Title="Sign in" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="DepartmentModule.Pages.Login" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Sign in</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="auth-page">
        <div class="auth-layout">
            <section class="auth-card auth-card-login">
                <h1>Sign in</h1>
                <p class="muted auth-description">Access your account with a one-time password for your registered mobile.</p>
            <asp:Literal ID="Message" runat="server" />
            <div class="field">
                <asp:Label ID="MobileLabel" runat="server" AssociatedControlID="MobileText">Mobile number</asp:Label>
                <div class="auth-mobile-input">
                    <svg class="auth-field-icon" viewBox="0 0 24 24" aria-hidden="true">
                        <path d="M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z" />
                        <path d="M10 18h4" />
                    </svg>
                    <span class="country-code" aria-label="India country calling code">+91</span>
                    <asp:TextBox ID="MobileText" runat="server" TextMode="Phone" MaxLength="10" autocomplete="tel-national" placeholder="10-digit mobile number" />
                </div>
                <asp:RequiredFieldValidator ID="MobileRequired" runat="server" ControlToValidate="MobileText" ValidationGroup="LoginId" CssClass="validation" Display="Dynamic" ErrorMessage="Mobile number is required." />
                <asp:CustomValidator ID="MobileFormat" runat="server" ControlToValidate="MobileText" ValidationGroup="LoginId" CssClass="validation" Display="Dynamic" OnServerValidate="Mobile_ServerValidate" ErrorMessage="Enter a valid 10-digit mobile number." />
                <span class="field-help">Enter your registered 10-digit mobile number.</span>
            </div>
            <asp:Button ID="RequestOtpButton" runat="server" Text="Send OTP" CssClass="button auth-submit"
                ValidationGroup="LoginId" UseSubmitBehavior="false"
                OnClientClick="if (!beginLoginSubmission(this, 'Sending OTP...', 'LoginId')) return false;"
                OnClick="RequestOtpButton_Click" />
            <span id="LoginOtpCooldown" class="auth-countdown" role="status" aria-live="polite"></span>
            <asp:Panel ID="OtpPanel" runat="server" Visible="false">
                <p class="muted auth-otp-note">Enter the six-digit OTP for your registered mobile number.</p>
                <asp:Panel ID="DevelopmentOtpPanel" runat="server" Visible="false" CssClass="message success" role="status">
                    <strong>DEVELOPMENT MODE</strong><br />
                    Development OTP: <code><asp:Literal ID="DevelopmentOtpText" runat="server" /></code>
                </asp:Panel>
                <div class="field">
                    <asp:Label ID="OtpLabel" runat="server" AssociatedControlID="OtpText">Login OTP</asp:Label>
                    <div class="auth-mobile-input">
                        <svg class="auth-field-icon" viewBox="0 0 24 24" aria-hidden="true">
                            <rect x="4" y="10" width="16" height="11" rx="2" />
                            <path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3" />
                        </svg>
                        <asp:TextBox ID="OtpText" runat="server" MaxLength="6" autocomplete="one-time-code" placeholder="6-digit OTP" />
                    </div>
                    <asp:RequiredFieldValidator ID="OtpRequired" runat="server" ControlToValidate="OtpText" ValidationGroup="LoginOtp" CssClass="validation" Display="Dynamic" ErrorMessage="OTP is required." />
                    <asp:RegularExpressionValidator ID="OtpFormat" runat="server" ControlToValidate="OtpText" ValidationGroup="LoginOtp" CssClass="validation" Display="Dynamic" ValidationExpression="^\d{6}$" ErrorMessage="Enter the six-digit OTP." />
                </div>
                <asp:Button ID="VerifyOtpButton" runat="server" Text="Verify OTP and sign in" CssClass="button auth-submit"
                    ValidationGroup="LoginOtp" UseSubmitBehavior="false"
                    OnClientClick="if (!beginLoginSubmission(this, 'Verifying OTP...', 'LoginId,LoginOtp')) return false;"
                    OnClick="VerifyOtpButton_Click" />
            </asp:Panel>
            <span id="LoginProcessing" class="auth-loading" role="status" aria-live="polite" hidden="hidden"></span>
                <p class="auth-note">New contractor? <asp:HyperLink ID="RegisterLink" runat="server">Create an account</asp:HyperLink></p>
            </section>
        </div>
    </div>
    <script type="text/javascript">
        function beginLoginSubmission(button, message, validationGroups) {
            if (button.getAttribute("data-submitting") === "true") return false;
            if (typeof Page_ClientValidate === "function") {
                var groups = validationGroups.split(",");
                for (var i = 0; i < groups.length; i++) {
                    if (!Page_ClientValidate(groups[i])) return false;
                }
            }
            button.setAttribute("data-submitting", "true");
            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            var status = document.getElementById("LoginProcessing");
            if (status) {
                status.textContent = message;
                status.hidden = false;
            }
            return true;
        }

        (function () {
            var button = document.getElementById("<%= RequestOtpButton.ClientID %>");
            var status = document.getElementById("LoginOtpCooldown");
            if (!button || !status) return;
            var seconds = parseInt(button.getAttribute("data-cooldown-seconds") || "0", 10);
            if (!seconds || seconds < 1) return;

            button.disabled = true;
            function updateCountdown() {
                if (seconds <= 0) {
                    button.disabled = false;
                    status.textContent = "";
                    return;
                }
                status.textContent = "You can request another OTP in " + seconds + " seconds.";
                seconds--;
                window.setTimeout(updateCountdown, 1000);
            }
            updateCountdown();
        }());
    </script>
</asp:Content>
