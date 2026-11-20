<%@ Page Title="Contractor registration" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="DepartmentModule.Pages.Register" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Contractor registration</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="auth-page auth-registration-page">
        <div class="auth-layout auth-layout-registration">
            <section class="auth-card">
                <h1>Create contractor account</h1>
                <p class="muted auth-description">Verify your Indian mobile number, then complete your contractor profile.</p>
                <asp:Literal ID="Message" runat="server" />
                <asp:ValidationSummary ID="RegistrationSummary" runat="server" ValidationGroup="Registration" CssClass="message auth-validation-summary" HeaderText="Please review these details:" />

                <div class="auth-form-grid">
                    <section class="auth-section" aria-labelledby="personalSectionTitle">
                        <h2 id="personalSectionTitle">Personal / account information</h2>
                        <div class="field">
                            <asp:Label ID="FullNameLabel" runat="server" AssociatedControlID="FullNameText">Full name</asp:Label>
                            <asp:TextBox ID="FullNameText" runat="server" MaxLength="150" autocomplete="name" />
                            <asp:RequiredFieldValidator ID="FullNameRequired" runat="server" ControlToValidate="FullNameText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Full name is required." />
                            <asp:CustomValidator ID="FullNameLength" runat="server" ControlToValidate="FullNameText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="FullName_ServerValidate" ErrorMessage="Enter a name between 2 and 150 characters." />
                        </div>
                        <div class="field">
                            <asp:Label ID="EmailLabel" runat="server" AssociatedControlID="EmailText">Email ID</asp:Label>
                            <asp:TextBox ID="EmailText" runat="server" TextMode="Email" MaxLength="150" autocomplete="email" placeholder="name@example.com" />
                            <asp:RequiredFieldValidator ID="EmailRequired" runat="server" ControlToValidate="EmailText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Email ID is required." />
                            <asp:CustomValidator ID="EmailFormat" runat="server" ControlToValidate="EmailText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="Email_ServerValidate" ErrorMessage="Enter a valid email address." />
                        </div>
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
                            <asp:RequiredFieldValidator ID="MobileRequired" runat="server" ControlToValidate="MobileText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Mobile number is required." />
                            <asp:CustomValidator ID="MobileFormat" runat="server" ControlToValidate="MobileText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="Mobile_ServerValidate" ErrorMessage="Enter a valid 10-digit Indian mobile number." />
                            <asp:Label ID="MobileValidationMessage" runat="server" CssClass="validation" />
                            <asp:Panel ID="MobileVerifiedPanel" runat="server" Visible="false" CssClass="auth-verified" role="status">
                                <span>Mobile number verified</span>
                                <asp:Button ID="ChangeMobileButton" runat="server" Text="Change number" CssClass="button secondary auth-change-mobile" CausesValidation="false" OnClick="ChangeMobileButton_Click" />
                            </asp:Panel>
                            <asp:Panel ID="OtpPanel" runat="server" Visible="false" CssClass="auth-otp-panel">
                                <div class="field">
                                    <asp:Label ID="OtpLabel" runat="server" AssociatedControlID="OtpText">Mobile verification code</asp:Label>
                                    <div class="auth-mobile-input">
                                        <svg class="auth-field-icon" viewBox="0 0 24 24" aria-hidden="true">
                                            <rect x="4" y="10" width="16" height="11" rx="2" />
                                            <path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3" />
                                        </svg>
                                        <asp:TextBox ID="OtpText" runat="server" MaxLength="6" autocomplete="one-time-code" inputmode="numeric" placeholder="6-digit OTP" />
                                    </div>
                                    <asp:RequiredFieldValidator ID="OtpRequired" runat="server" ControlToValidate="OtpText" ValidationGroup="OtpVerification" CssClass="validation" Display="Dynamic" ErrorMessage="OTP is required." />
                                    <asp:RegularExpressionValidator ID="OtpFormat" runat="server" ControlToValidate="OtpText" ValidationGroup="OtpVerification" CssClass="validation" Display="Dynamic" ValidationExpression="^[0-9]{6}$" ErrorMessage="Enter the six-digit OTP." />
                                </div>
                                <asp:Button ID="VerifyOtpButton" runat="server" Text="Verify mobile number" CssClass="button auth-submit"
                                    ValidationGroup="OtpVerification" UseSubmitBehavior="false"
                                    OnClientClick="if (!beginRegistrationSubmission(this, 'Verifying mobile number...', 'OtpVerification')) return false;"
                                    OnClick="VerifyOtpButton_Click" />
                            </asp:Panel>
                            <asp:Button ID="RequestOtpButton" runat="server" Text="Send verification OTP" CssClass="button auth-submit auth-otp-request"
                                CausesValidation="false" UseSubmitBehavior="false"
                                OnClientClick="if (!requestRegistrationOtp(this)) return false;"
                                OnClick="RequestOtpButton_Click" />
                            <span id="RegistrationOtpCooldown" class="auth-countdown" role="status" aria-live="polite"></span>
                        </div>
                    </section>

                    <section class="auth-section" aria-labelledby="companySectionTitle">
                        <h2 id="companySectionTitle">Company information</h2>
                        <div class="field">
                            <asp:Label ID="CompanyNameLabel" runat="server" AssociatedControlID="CompanyNameText">Company name</asp:Label>
                            <asp:TextBox ID="CompanyNameText" runat="server" MaxLength="150" autocomplete="organization" />
                            <asp:RequiredFieldValidator ID="CompanyNameRequired" runat="server" ControlToValidate="CompanyNameText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Company name is required." />
                            <asp:CustomValidator ID="CompanyNameLength" runat="server" ControlToValidate="CompanyNameText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="CompanyName_ServerValidate" ErrorMessage="Enter a company name between 2 and 150 characters." />
                        </div>
                        <div class="field">
                            <asp:Label ID="ContactPersonNameLabel" runat="server" AssociatedControlID="ContactPersonNameText">Company contact person</asp:Label>
                            <asp:TextBox ID="ContactPersonNameText" runat="server" MaxLength="150" autocomplete="name" />
                            <asp:RequiredFieldValidator ID="ContactPersonNameRequired" runat="server" ControlToValidate="ContactPersonNameText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Company contact person is required." />
                            <asp:CustomValidator ID="ContactPersonNameLength" runat="server" ControlToValidate="ContactPersonNameText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="ContactPersonName_ServerValidate" ErrorMessage="Enter a contact person name between 2 and 150 characters." />
                        </div>
                        <div class="field">
                            <asp:Label ID="CompanyContactMobileLabel" runat="server" AssociatedControlID="CompanyContactMobileText">Company contact mobile</asp:Label>
                            <div class="auth-mobile-input">
                                <svg class="auth-field-icon" viewBox="0 0 24 24" aria-hidden="true">
                                    <path d="M7 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2Z" />
                                    <path d="M10 18h4" />
                                </svg>
                                <span class="country-code" aria-label="India country calling code">+91</span>
                                <asp:TextBox ID="CompanyContactMobileText" runat="server" TextMode="Phone" MaxLength="10" autocomplete="tel-national" placeholder="10-digit mobile number" />
                            </div>
                            <asp:RequiredFieldValidator ID="CompanyContactMobileRequired" runat="server" ControlToValidate="CompanyContactMobileText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Company contact mobile is required." />
                            <asp:CustomValidator ID="CompanyContactMobileFormat" runat="server" ControlToValidate="CompanyContactMobileText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="CompanyContactMobile_ServerValidate" ErrorMessage="Enter a valid 10-digit Indian mobile number." />
                        </div>
                        <div class="field">
                            <asp:Label ID="CompanyEmailLabel" runat="server" AssociatedControlID="CompanyEmailText">Company email ID</asp:Label>
                            <asp:TextBox ID="CompanyEmailText" runat="server" TextMode="Email" MaxLength="150" autocomplete="email" placeholder="company@example.com" />
                            <asp:RequiredFieldValidator ID="CompanyEmailRequired" runat="server" ControlToValidate="CompanyEmailText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Company email ID is required." />
                            <asp:CustomValidator ID="CompanyEmailFormat" runat="server" ControlToValidate="CompanyEmailText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="CompanyEmail_ServerValidate" ErrorMessage="Enter a valid company email address." />
                        </div>
                        <div class="field auth-field-full">
                            <asp:Label ID="CompanyAddressLabel" runat="server" AssociatedControlID="CompanyAddressText">Company address</asp:Label>
                            <asp:TextBox ID="CompanyAddressText" runat="server" MaxLength="300" autocomplete="street-address" />
                            <asp:RequiredFieldValidator ID="CompanyAddressRequired" runat="server" ControlToValidate="CompanyAddressText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Company address is required." />
                            <asp:CustomValidator ID="CompanyAddressLength" runat="server" ControlToValidate="CompanyAddressText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="CompanyAddress_ServerValidate" ErrorMessage="Company address must be 300 characters or fewer." />
                        </div>
                        <div class="auth-fields-row">
                            <div class="field">
                                <asp:Label ID="CityLabel" runat="server" AssociatedControlID="CityText">City</asp:Label>
                                <asp:TextBox ID="CityText" runat="server" MaxLength="100" autocomplete="address-level2" />
                                <asp:RequiredFieldValidator ID="CityRequired" runat="server" ControlToValidate="CityText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="City is required." />
                                <asp:CustomValidator ID="CityLength" runat="server" ControlToValidate="CityText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="City_ServerValidate" ErrorMessage="City must be 100 characters or fewer." />
                            </div>
                            <div class="field">
                                <asp:Label ID="StateLabel" runat="server" AssociatedControlID="StateText">State</asp:Label>
                                <asp:TextBox ID="StateText" runat="server" MaxLength="100" autocomplete="address-level1" />
                                <asp:RequiredFieldValidator ID="StateRequired" runat="server" ControlToValidate="StateText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="State is required." />
                                <asp:CustomValidator ID="StateLength" runat="server" ControlToValidate="StateText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="State_ServerValidate" ErrorMessage="State must be 100 characters or fewer." />
                            </div>
                        </div>
                        <div class="field">
                            <asp:Label ID="PinCodeLabel" runat="server" AssociatedControlID="PinCodeText">PIN code</asp:Label>
                            <asp:TextBox ID="PinCodeText" runat="server" MaxLength="6" autocomplete="postal-code" inputmode="numeric" placeholder="6-digit PIN code" />
                            <asp:RequiredFieldValidator ID="PinCodeRequired" runat="server" ControlToValidate="PinCodeText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="PIN code is required." />
                            <asp:CustomValidator ID="PinCodeFormat" runat="server" ControlToValidate="PinCodeText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="PinCode_ServerValidate" ErrorMessage="Enter a valid 6-digit Indian PIN code." />
                        </div>
                    </section>

                    <section class="auth-section auth-section-additional" aria-labelledby="additionalSectionTitle">
                        <h2 id="additionalSectionTitle">Additional information</h2>
                        <div class="auth-fields-row">
                            <div class="field">
                                <asp:Label ID="GstNumberLabel" runat="server" AssociatedControlID="GstNumberText">GST number <span class="auth-optional">(if applicable)</span></asp:Label>
                                <asp:TextBox ID="GstNumberText" runat="server" MaxLength="15" autocomplete="off" placeholder="15-character GSTIN" />
                                <asp:CustomValidator ID="GstNumberFormat" runat="server" ControlToValidate="GstNumberText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="GstNumber_ServerValidate" ErrorMessage="Enter a valid 15-character GSTIN or leave this blank." />
                            </div>
                            <div class="field">
                                <asp:Label ID="PanNumberLabel" runat="server" AssociatedControlID="PanNumberText">PAN number <span class="auth-optional">(if applicable)</span></asp:Label>
                                <asp:TextBox ID="PanNumberText" runat="server" MaxLength="10" autocomplete="off" placeholder="10-character PAN" />
                                <asp:CustomValidator ID="PanNumberFormat" runat="server" ControlToValidate="PanNumberText" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" OnServerValidate="PanNumber_ServerValidate" ErrorMessage="Enter a valid 10-character PAN or leave this blank." />
                            </div>
                        </div>
                        <div class="field">
                            <asp:Label ID="CompanyTypeLabel" runat="server" AssociatedControlID="CompanyTypeSelect">Company type</asp:Label>
                            <asp:DropDownList ID="CompanyTypeSelect" runat="server">
                                <asp:ListItem Text="Select company type" Value="" />
                                <asp:ListItem Text="Proprietorship" Value="Proprietorship" />
                                <asp:ListItem Text="Partnership" Value="Partnership" />
                                <asp:ListItem Text="Private Limited" Value="Private Limited" />
                                <asp:ListItem Text="Public Limited" Value="Public Limited" />
                                <asp:ListItem Text="Other" Value="Other" />
                            </asp:DropDownList>
                            <asp:RequiredFieldValidator ID="CompanyTypeRequired" runat="server" ControlToValidate="CompanyTypeSelect" InitialValue="" ValidationGroup="Registration" CssClass="validation" Display="Dynamic" ErrorMessage="Select a company type." />
                        </div>
                    </section>
                </div>

                <span id="RegistrationProcessing" class="auth-loading" role="status" aria-live="polite" hidden="hidden"></span>
                <asp:Button ID="CreateAccountButton" runat="server" Text="Create contractor account" CssClass="button auth-submit"
                    Visible="false" ValidationGroup="Registration" UseSubmitBehavior="false"
                    OnClientClick="if (!beginRegistrationSubmission(this, 'Creating your account...', 'Registration')) return false;"
                    OnClick="CreateAccountButton_Click" />
                <p class="auth-note">Already registered? <asp:HyperLink ID="LoginLink" runat="server">Sign in</asp:HyperLink></p>
            </section>
        </div>
    </div>
    <script type="text/javascript">
        function beginRegistrationSubmission(button, message, validationGroup) {
            if (button.getAttribute("data-submitting") === "true") return false;
            if (validationGroup && typeof Page_ClientValidate === "function" && !Page_ClientValidate(validationGroup)) return false;
            button.setAttribute("data-submitting", "true");
            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            var status = document.getElementById("RegistrationProcessing");
            if (status) {
                status.textContent = message;
                status.hidden = false;
            }
            return true;
        }

        function requestRegistrationOtp(button) {
            var mobile = document.getElementById("<%= MobileText.ClientID %>");
            var validation = document.getElementById("<%= MobileValidationMessage.ClientID %>");
            if (!mobile || !/^[6-9][0-9]{9}$/.test(mobile.value.trim())) {
                if (validation) validation.textContent = "Enter a valid 10-digit Indian mobile number.";
                if (mobile) mobile.focus();
                return false;
            }
            if (validation) validation.textContent = "";
            return beginRegistrationSubmission(button, "Sending verification OTP...", "");
        }

        (function () {
            var button = document.getElementById("<%= RequestOtpButton.ClientID %>");
            var status = document.getElementById("RegistrationOtpCooldown");
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
