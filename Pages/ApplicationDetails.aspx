<%@ Page Title="Application Details" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="ApplicationDetails.aspx.cs" Inherits="DepartmentModule.Pages.ApplicationDetails" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Application Details</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div>
            <h1><asp:Literal ID="TenderTitle" runat="server" /></h1>
            <div class="muted"><asp:Literal ID="ApplicationReference" runat="server" /> · <asp:Literal ID="TenderNumber" runat="server" /></div>
        </div>
        <asp:HyperLink ID="BackLink" runat="server" CssClass="button secondary">Back</asp:HyperLink>
    </div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card">
        <h2>Application overview</h2>
        <div class="row">
            <p><strong>Contractor</strong><br /><asp:Literal ID="ContractorName" runat="server" /></p>
            <p><strong>Representative</strong><br /><asp:Literal ID="RepresentativeName" runat="server" /></p>
            <p><strong>Mobile</strong><br /><asp:Literal ID="ContractorMobile" runat="server" /></p>
            <p><strong>Email</strong><br /><asp:Literal ID="ContractorEmail" runat="server" /></p>
            <p><strong>Department</strong><br /><asp:Literal ID="DepartmentName" runat="server" /></p>
            <p><strong>Estimated amount</strong><br />₹<asp:Literal ID="EstimatedAmount" runat="server" /></p>
            <p><strong>Status</strong><br /><span class="badge"><asp:Literal ID="ApplicationStatus" runat="server" /></span></p>
            <p><strong>Submitted</strong><br /><asp:Literal ID="SubmittedDate" runat="server" /></p>
        </div>
        <p><strong>Address</strong><br /><asp:Literal ID="ContractorAddress" runat="server" /></p>
    </section>
    <asp:Panel ID="ContractorPanel" runat="server">
        <asp:Panel ID="ContractorUploadPanel" runat="server" CssClass="card" Visible="false">
            <h2>Upload application documents</h2>
            <p class="help">Choose one configured requirement for each upload. Corrected documents are retained as additional versions.</p>
            <div class="field">
                <label for="UploadDocumentTypeSelect">Document requirement</label>
                <asp:DropDownList ID="UploadDocumentTypeSelect" runat="server" />
            </div>
            <div class="field">
                <label for="ApplicationDocumentUpload">Documents</label>
                <asp:FileUpload ID="ApplicationDocumentUpload" runat="server" AllowMultiple="true" />
                <span class="help">Up to 10 files per upload; each file follows its requirement's size and format limits.</span>
            </div>
            <asp:Button ID="UploadDocumentsButton" runat="server" Text="Upload documents" OnClick="UploadDocumentsButton_Click" />
        </asp:Panel>
        <asp:Panel ID="ContractorSubmitPanel" runat="server" CssClass="card" Visible="false">
            <h2>Submit application</h2>
            <div class="table-wrap">
                <asp:GridView ID="RequirementsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    EmptyDataText="No additional application documents are configured.">
                    <Columns>
                        <asp:BoundField DataField="DocumentType" HeaderText="Document" />
                        <asp:BoundField DataField="Description" HeaderText="Details" NullDisplayText="—" />
                        <asp:TemplateField HeaderText="Required"><ItemTemplate>
                            <%# Convert.ToBoolean(Eval("IsMandatory")) ? "Yes" : "Optional" %>
                        </ItemTemplate></asp:TemplateField>
                        <asp:TemplateField HeaderText="Uploaded"><ItemTemplate>
                            <%# Convert.ToBoolean(Eval("IsUploaded")) ? "Yes" : "No" %>
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
            <div class="field">
                <asp:CheckBox ID="TermsAcceptedCheck" runat="server" Text="I have read and accept the " />
                <asp:HyperLink ID="TermsLink" runat="server" NavigateUrl="~/Pages/Terms.aspx" Target="_blank">Terms &amp; Conditions</asp:HyperLink>
            </div>
            <asp:Button ID="SubmitApplicationButton" runat="server" Text="Submit application" OnClick="SubmitApplicationButton_Click" />
        </asp:Panel>
        <asp:Panel ID="ContractorResponsePanel" runat="server" CssClass="card" Visible="false">
            <h2>Respond to clarification</h2>
            <div class="field"><label for="ContractorResponseText">Response</label>
                <asp:TextBox ID="ContractorResponseText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="4" /></div>
            <asp:Button ID="SendResponseButton" runat="server" Text="Send response" OnClick="SendResponseButton_Click" />
        </asp:Panel>
        <asp:Panel ID="ContractorShortfallPanel" runat="server" CssClass="card" Visible="false">
            <h2>Submit shortfall corrections</h2>
            <p class="help">Each item is subject to the deadline shown. There is no limit on correction attempts before that deadline.</p>
            <div class="table-wrap">
                <asp:GridView ID="ContractorShortfallGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    DataKeyNames="ShortfallId,ShortfallItemId" OnRowCommand="ContractorShortfallGrid_RowCommand"
                    EmptyDataText="No shortfall items are currently awaiting your response.">
                    <Columns>
                        <asp:BoundField DataField="RequiredDocumentType" HeaderText="Requirement" />
                        <asp:BoundField DataField="ShortfallDescription" HeaderText="Deficiency" />
                        <asp:BoundField DataField="CorrectiveAction" HeaderText="Corrective action" />
                        <asp:BoundField DataField="SubmissionDeadlineUtc" HeaderText="Deadline (UTC)" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:TemplateField HeaderText="Corrected file"><ItemTemplate>
                            <asp:FileUpload ID="ShortfallItemFileUpload" runat="server" Enabled='<%# Convert.ToBoolean(Eval("CanUpload")) %>' />
                            <asp:TextBox ID="ShortfallSubmissionRemarksText" runat="server" TextMode="MultiLine"
                                MaxLength="1000" Rows="2" Enabled='<%# Convert.ToBoolean(Eval("CanUpload")) %>' />
                            <asp:Button ID="SubmitShortfallItemButton" runat="server" Text="Submit correction"
                                CommandName="SubmitShortfallItem" CommandArgument='<%# Container.DataItemIndex %>'
                                Enabled='<%# Convert.ToBoolean(Eval("CanUpload")) %>' />
                            <asp:Literal ID="ShortfallExpiredMessage" runat="server"
                                Visible='<%# !Convert.ToBoolean(Eval("CanUpload")) %>'
                                Text="The submission deadline has passed." />
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>
    </asp:Panel>
    <section class="card">
        <h2>Uploaded documents</h2>
        <div class="table-wrap">
            <asp:GridView ID="DocumentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                EmptyDataText="No documents have been uploaded.">
                <Columns>
                    <asp:BoundField DataField="DocumentType" HeaderText="Requirement" />
                    <asp:HyperLinkField DataTextField="FileName" DataNavigateUrlFields="TechnicalBidDocumentId"
                        DataNavigateUrlFormatString="DocumentDownload.aspx?technicalBidDocumentId={0}" HeaderText="File" />
                    <asp:BoundField DataField="UploadedAt" HeaderText="Uploaded" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:BoundField DataField="UploadedBy" HeaderText="Uploaded by" />
                    <asp:BoundField DataField="VerificationStatus" HeaderText="Verification" NullDisplayText="Pending" />
                    <asp:BoundField DataField="VerificationRemarks" HeaderText="Reviewer remarks" NullDisplayText="—" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <asp:Panel ID="InternalPanel" runat="server">
        <asp:Panel ID="ReviewDocumentsPanel" runat="server" CssClass="card" Visible="false">
            <h2>Tender Cell document verification</h2>
            <p class="help">A reason is required for a shortfall or rejection. Only the assigned Tender Cell can record a decision.</p>
            <div class="table-wrap">
                <asp:GridView ID="ReviewDocumentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    DataKeyNames="TechnicalBidDocumentId,DocumentType,IsMissing" OnRowCommand="ReviewDocumentsGrid_RowCommand"
                    EmptyDataText="No application documents are available for review.">
                    <Columns>
                        <asp:BoundField DataField="DocumentType" HeaderText="Requirement" />
                        <asp:TemplateField HeaderText="Document"><ItemTemplate>
                            <asp:HyperLink ID="SecondaryDocumentLink" runat="server" Text='<%# Eval("FileName") %>'
                                Visible='<%# !Convert.ToBoolean(Eval("IsMissing")) %>'
                                NavigateUrl='<%# ResolveUrl("~/Pages/DocumentDownload.aspx?technicalBidDocumentId=" + Eval("TechnicalBidDocumentId")) %>' />
                            <asp:Literal ID="SecondaryMissingDocumentText" runat="server" Text="Not submitted"
                                Visible='<%# Convert.ToBoolean(Eval("IsMissing")) %>' />
                        </ItemTemplate></asp:TemplateField>
                        <asp:BoundField DataField="VerificationStatus" HeaderText="Current decision" NullDisplayText="Pending" />
                        <asp:TemplateField HeaderText="Decision"><ItemTemplate>
                            <asp:DropDownList ID="DocumentDecisionSelect" runat="server">
                                <asp:ListItem Value="">Select decision</asp:ListItem>
                                <asp:ListItem Value="Verified">Verified</asp:ListItem>
                                <asp:ListItem Value="Not Verified">Not verified</asp:ListItem>
                            </asp:DropDownList>
                            <asp:TextBox ID="DocumentReviewRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="2" />
                            <asp:Button ID="SaveDocumentReviewButton" runat="server" Text="Save decision"
                                CommandName="ReviewDocument" CommandArgument='<%# Container.DataItemIndex %>' />
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>
        <asp:Panel ID="SecondaryApplicationDecisionPanel" runat="server" CssClass="card" Visible="false">
            <h2>Secondary bidder decision</h2>
            <div class="field"><label for="SecondaryApplicationDecisionSelect">Decision</label>
                <asp:DropDownList ID="SecondaryApplicationDecisionSelect" runat="server">
                    <asp:ListItem Value="">Select decision</asp:ListItem>
                    <asp:ListItem Value="Approved">Verified for financial opening</asp:ListItem>
                    <asp:ListItem Value="Shortfall">Requires shortfall</asp:ListItem>
                </asp:DropDownList></div>
            <div class="field"><label for="SecondaryApplicationDecisionRemarksText">Remarks</label>
                <asp:TextBox ID="SecondaryApplicationDecisionRemarksText" runat="server" TextMode="MultiLine"
                    MaxLength="2000" Rows="3" /></div>
            <asp:Button ID="RecordSecondaryApplicationDecisionButton" runat="server"
                Text="Record secondary bidder decision" OnClick="RecordSecondaryApplicationDecisionButton_Click" />
        </asp:Panel>
        <asp:Panel ID="ShortfallRequestPanel" runat="server" CssClass="card" Visible="false">
            <h2>Create shortfall items</h2>
            <p class="help">Select each not-verified document and state the deficiency and exact corrective action.</p>
            <div class="table-wrap">
                <asp:GridView ID="ShortfallRequestItemsGrid" runat="server" AutoGenerateColumns="false"
                    GridLines="None" DataKeyNames="TechnicalBidDocumentId,DocumentType,IsMissing"
                    EmptyDataText="No not-verified documents are available for a shortfall.">
                    <Columns>
                        <asp:TemplateField HeaderText="Include"><ItemTemplate>
                            <asp:CheckBox ID="CreateShortfallItemCheck" runat="server" />
                        </ItemTemplate></asp:TemplateField>
                        <asp:BoundField DataField="DocumentType" HeaderText="Requirement" />
                        <asp:BoundField DataField="FileName" HeaderText="Document" />
                        <asp:BoundField DataField="VerificationRemarks" HeaderText="Verification remarks" />
                        <asp:TemplateField HeaderText="Deficiency"><ItemTemplate>
                            <asp:TextBox ID="ShortfallDescriptionText" runat="server" TextMode="MultiLine"
                                MaxLength="2000" Rows="2" />
                        </ItemTemplate></asp:TemplateField>
                        <asp:TemplateField HeaderText="Corrective action"><ItemTemplate>
                            <asp:TextBox ID="ShortfallCorrectiveActionText" runat="server" TextMode="MultiLine"
                                MaxLength="2000" Rows="2" />
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
            <div class="field"><label for="ShortfallRemarksText">Shortfall request remarks</label>
                <asp:TextBox ID="ShortfallRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="2" /></div>
            <asp:Button ID="CreateShortfallButton" runat="server" Text="Submit shortfall for JE approval"
                OnClick="CreateShortfallButton_Click" />
        </asp:Panel>
        <asp:Panel ID="InternalRemarkPanel" runat="server" CssClass="card" Visible="false">
            <h2>Review remarks</h2>
            <div class="field"><label for="InternalRemarkTypeSelect">Remark type</label>
                <asp:DropDownList ID="InternalRemarkTypeSelect" runat="server">
                    <asp:ListItem Value="Clarification">Clarification for contractor</asp:ListItem>
                    <asp:ListItem Value="Internal">Internal remark</asp:ListItem>
                </asp:DropDownList></div>
            <div class="field"><label for="InternalRemarkText">Remark</label>
                <asp:TextBox ID="InternalRemarkText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="4" /></div>
            <asp:Button ID="AddInternalRemarkButton" runat="server" Text="Add remark" OnClick="AddInternalRemarkButton_Click" />
        </asp:Panel>
        <asp:Panel ID="ApplicationDecisionPanel" runat="server" CssClass="card" Visible="false">
            <h2>Technical decision</h2>
            <div class="field"><label for="DecisionRemarksText">Decision remarks</label>
                <asp:TextBox ID="DecisionRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="3" /></div>
            <div class="row">
                <asp:Button ID="QualifyButton" runat="server" Text="Qualify application" OnClick="QualifyButton_Click" />
                <asp:Button ID="DisqualifyButton" runat="server" Text="Disqualify application" CssClass="button danger"
                    OnClick="DisqualifyButton_Click" />
            </div>
        </asp:Panel>
    </asp:Panel>
    <section class="card">
        <h2>Remarks and clarifications</h2>
        <div class="table-wrap">
            <asp:GridView ID="RemarksGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                EmptyDataText="No remarks have been recorded.">
                <Columns>
                    <asp:BoundField DataField="RemarkType" HeaderText="Type" />
                    <asp:BoundField DataField="Message" HeaderText="Remark" />
                    <asp:BoundField DataField="Author" HeaderText="By" />
                    <asp:BoundField DataField="CreatedAtUtc" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
    <section class="card">
        <h2>Application history</h2>
        <div class="table-wrap">
            <asp:GridView ID="HistoryGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                EmptyDataText="No application history is available.">
                <Columns>
                    <asp:BoundField DataField="EventType" HeaderText="Event" />
                    <asp:BoundField DataField="PreviousStatus" HeaderText="Previous status" />
                    <asp:BoundField DataField="NewStatus" HeaderText="Status" />
                    <asp:BoundField DataField="Remarks" HeaderText="Details" NullDisplayText="—" />
                    <asp:BoundField DataField="Author" HeaderText="By" />
                    <asp:BoundField DataField="CreatedAtUtc" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
