<%@ Page Title="New Tender" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="TenderForm.aspx.cs" Inherits="DepartmentModule.Pages.TenderForm" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server"><asp:Literal ID="FormTitle" runat="server" /></asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1><asp:Literal ID="FormHeading" runat="server" /></h1><div class="muted"><asp:Literal ID="FormDescription" runat="server" /></div></div>
        <a href="Tenders.aspx" class="button secondary">Back to tenders</a>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card tender-form">
        <asp:ValidationSummary ID="ValidationSummary" runat="server" CssClass="message" HeaderText="Please correct the following:" />
        <h2>Basic tender information</h2>
        <div class="field"><label for="TitleText">Tender title</label><asp:TextBox ID="TitleText" runat="server" MaxLength="250" />
            <asp:RequiredFieldValidator ID="TitleRequired" runat="server" ControlToValidate="TitleText" ErrorMessage="Title is required." CssClass="validation" />
            <asp:CustomValidator ID="TitleLength" runat="server" ControlToValidate="TitleText" OnServerValidate="TitleLength_ServerValidate" ErrorMessage="Title must be 5 to 250 characters." CssClass="validation" /></div>
        <div class="field"><label for="DescriptionText">Tender description</label><asp:TextBox ID="DescriptionText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
        <div class="field"><label for="EligibilityCriteriaText">Eligibility criteria</label><asp:TextBox ID="EligibilityCriteriaText" runat="server" TextMode="MultiLine" Rows="2" MaxLength="4000" /></div>
        <div class="row">
            <asp:Panel ID="DepartmentPanel" runat="server" CssClass="field"><label for="DepartmentSelect">Department</label><asp:DropDownList ID="DepartmentSelect" runat="server" />
                <asp:RequiredFieldValidator ID="DepartmentRequired" runat="server" ControlToValidate="DepartmentSelect" InitialValue="" ErrorMessage="A valid Department must be selected." CssClass="validation" /></asp:Panel>
            <div class="field"><label for="TenderTypeSelect">Tender method</label><asp:DropDownList ID="TenderTypeSelect" runat="server" />
                <asp:RequiredFieldValidator ID="TenderTypeRequired" runat="server" ControlToValidate="TenderTypeSelect" InitialValue="" ErrorMessage="Tender method is required." CssClass="validation" /></div>
            <div class="field"><label for="TenderCategorySelect">Tender category</label><asp:DropDownList ID="TenderCategorySelect" runat="server" AutoPostBack="true" OnSelectedIndexChanged="TenderCategorySelect_SelectedIndexChanged" />
                <asp:RequiredFieldValidator ID="TenderCategoryRequired" runat="server" ControlToValidate="TenderCategorySelect" InitialValue="" ErrorMessage="Tender category is required." CssClass="validation" /></div>
            <div class="field"><label for="TenderSubcategorySelect">Tender subcategory</label><asp:DropDownList ID="TenderSubcategorySelect" runat="server" /></div>
            <div class="field"><label for="FinancialYearSelect">Financial year</label><asp:DropDownList ID="FinancialYearSelect" runat="server" />
                <asp:RequiredFieldValidator ID="FinancialYearRequired" runat="server" ControlToValidate="FinancialYearSelect" InitialValue="" ErrorMessage="Financial year is required." CssClass="validation" /></div>
        </div>
        <asp:Panel ID="FinancialYearMissingPanel" runat="server" CssClass="message" Visible="false">
            No active financial years are configured. <a href="AdminMasterData.aspx">Add a financial year in Master Management</a>.
        </asp:Panel>

        <h2>Financial information</h2>
        <div class="row">
            <div class="field"><label for="AmountText">Estimated tender value (₹)</label><asp:TextBox ID="AmountText" runat="server" TextMode="Number" step="0.01" min="0.01" />
                <asp:RequiredFieldValidator ID="AmountRequired" runat="server" ControlToValidate="AmountText" ErrorMessage="Estimated amount is required." CssClass="validation" />
                <asp:RangeValidator ID="AmountRange" runat="server" ControlToValidate="AmountText" Type="Double" MinimumValue="0.01" MaximumValue="999999999999999" ErrorMessage="Estimated amount must be greater than zero." CssClass="validation" /></div>
            <div class="field"><label for="EMDAmountText">Earnest money deposit (₹)</label><asp:TextBox ID="EMDAmountText" runat="server" TextMode="Number" step="0.01" min="0" /></div>
            <div class="field"><label for="DocumentFeeText">Tender document fee (₹)</label><asp:TextBox ID="DocumentFeeText" runat="server" TextMode="Number" step="0.01" min="0" /></div>
            <div class="field"><label for="ProcessingFeeText">Processing fee (₹)</label><asp:TextBox ID="ProcessingFeeText" runat="server" TextMode="Number" step="0.01" min="0" /></div>
            <div class="field"><label for="SecurityDepositText">Security deposit (₹)</label><asp:TextBox ID="SecurityDepositText" runat="server" TextMode="Number" step="0.01" min="0" /></div>
        </div>

        <h2>Important dates and times (IST)</h2>
        <div class="row">
            <div class="field"><label for="PublicationDateText">Planned publication date</label><asp:TextBox ID="PublicationDateText" runat="server" TextMode="DateTimeLocal" /></div>
            <div class="field"><label for="StartText">Submission start date and time</label><asp:TextBox ID="StartText" runat="server" TextMode="DateTimeLocal" /></div>
            <div class="field"><label for="DeadlineText">Submission closing date and time</label><asp:TextBox ID="DeadlineText" runat="server" TextMode="DateTimeLocal" /></div>
            <div class="field"><label for="TechnicalBidOpeningText">Technical bid opening date and time</label><asp:TextBox ID="TechnicalBidOpeningText" runat="server" TextMode="DateTimeLocal" /></div>
            <div class="field"><label for="FinancialBidOpeningText">Financial bid opening date and time</label><asp:TextBox ID="FinancialBidOpeningText" runat="server" TextMode="DateTimeLocal" /></div>
        </div>

        <h2>Tender requirements</h2>
        <div class="field"><label for="ScopeOfWorkText">Scope of work</label><asp:TextBox ID="ScopeOfWorkText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
        <div class="row">
            <div class="field"><label for="TechnicalRequirementsText">Technical requirements</label><asp:TextBox ID="TechnicalRequirementsText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
            <div class="field"><label for="FinancialRequirementsText">Financial requirements</label><asp:TextBox ID="FinancialRequirementsText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
        </div>
        <div class="row">
            <div class="field"><label for="AdditionalConditionsText">Additional conditions</label><asp:TextBox ID="AdditionalConditionsText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
            <div class="field"><label for="TermsAndConditionsText">Terms and conditions</label><asp:TextBox ID="TermsAndConditionsText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
        </div>

        <details class="tender-form-optional">
            <summary>Budget and technical sanction details</summary>
            <div class="field"><label for="BudgetInformationText">Budget information</label><asp:TextBox ID="BudgetInformationText" runat="server" TextMode="MultiLine" Rows="2" MaxLength="2000" /></div>
            <div class="row">
                <div class="field"><label for="TechnicalSanctionNumberText">Technical sanction number</label><asp:TextBox ID="TechnicalSanctionNumberText" runat="server" MaxLength="100" /></div>
                <div class="field"><label for="TechnicalSanctionAuthorityText">Technical sanction authority</label><asp:TextBox ID="TechnicalSanctionAuthorityText" runat="server" MaxLength="200" /></div>
                <div class="field"><label for="TechnicalSanctionDateText">Technical sanction date</label><asp:TextBox ID="TechnicalSanctionDateText" runat="server" TextMode="Date" /></div>
            </div>
        </details>
        <details class="tender-form-optional">
            <summary>Pre-bid meeting details</summary>
            <div class="row">
                <div class="field"><label for="PreBidMeetingText">Pre-bid meeting date and time</label><asp:TextBox ID="PreBidMeetingText" runat="server" TextMode="DateTimeLocal" /></div>
                <div class="field"><label for="PreBidLocationText">Pre-bid location</label><asp:TextBox ID="PreBidLocationText" runat="server" MaxLength="300" /></div>
            </div>
            <div class="field"><label for="PreBidDetailsText">Meeting details</label><asp:TextBox ID="PreBidDetailsText" runat="server" TextMode="MultiLine" Rows="2" MaxLength="2000" /></div>
        </details>

        <asp:Panel ID="AttachmentPanel" runat="server">
            <h2>Supporting documents</h2>
            <asp:Panel ID="DocumentTypesMissingPanel" runat="server" CssClass="message" Visible="false">
                No active document types are configured. <a href="AdminMasterData.aspx">Add document types in Master Management</a>.
            </asp:Panel>
            <div class="field"><label for="DocumentTypeSelect">Attachment type</label><asp:DropDownList ID="DocumentTypeSelect" runat="server" /></div>
            <div class="field"><label for="Attachments">Attachments</label><asp:FileUpload ID="Attachments" runat="server" AllowMultiple="true" />
                <span class="help">Optional documents. PDF, Office documents, JPEG, or PNG; maximum 20 MB each.</span></div>
        </asp:Panel>
        <asp:Button ID="CreateButton" runat="server" Text="Save as Draft" CssClass="button" OnClick="CreateButton_Click" />
    </section>
</asp:Content>
