<%@ Page Title="Tender Bids" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="Bids.aspx.cs" Inherits="DepartmentModule.Pages.Bids" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Tender Bids</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading"><div><h1>Bids</h1><div class="muted"><asp:Literal ID="TenderNumber" runat="server" /> · <asp:Literal ID="TenderTitle" runat="server" /></div></div><asp:HyperLink ID="BackLink" runat="server" CssClass="button secondary">Tender details</asp:HyperLink></div>
    <asp:Literal ID="Message" runat="server" />
    <section class="card" id="TechnicalBidsSection" runat="server">
        <h2>Technical bids</h2>
        <div class="table-wrap">
            <h3>Required bidder documents</h3>
            <asp:GridView ID="RequiredBidDocumentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No version-2 document checklist is configured for this tender.">
                <Columns>
                    <asp:BoundField DataField="DocumentType" HeaderText="Required document type" />
                    <asp:BoundField DataField="Description" HeaderText="Details" NullDisplayText="—" />
                    <asp:TemplateField HeaderText="Required"><ItemTemplate>
                        <%# Convert.ToBoolean(Eval("IsMandatory")) ? "Yes" : "Optional" %>
                    </ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="AllowedExtensions" HeaderText="Accepted files" NullDisplayText="PDF, Office, JPEG, PNG" />
                    <asp:TemplateField HeaderText="Maximum size"><ItemTemplate>
                        <%# (Convert.ToInt32(Eval("MaxFileSizeBytes")) / 1000000.0).ToString("0.#") %> MB
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
        <asp:Panel ID="TechnicalSubmissionPanel" runat="server" CssClass="card">
            <h3>Submit technical bid</h3>
            <asp:Literal ID="ContractorProfileMessage" runat="server" />
            <div class="field" id="TechnicalContractorField" runat="server">
                <label for="TechnicalContractorSelect">Contractor</label>
                <asp:DropDownList ID="TechnicalContractorSelect" runat="server" />
            </div>
            <div class="field" id="LegacyDocumentPathField" runat="server">
                <label for="TechnicalDocumentPathText">Document Path (optional)</label>
                <asp:TextBox ID="TechnicalDocumentPathText" runat="server" MaxLength="400" />
            </div>
            <asp:Panel ID="LegacyDocumentTypeField" runat="server" CssClass="field">
                <label for="TechnicalDocumentTypeText">Document type</label>
                <asp:TextBox ID="TechnicalDocumentTypeText" runat="server" MaxLength="160" />
            </asp:Panel>
            <div class="field">
                <label for="TechnicalDocumentUpload">Technical bid document</label>
                <asp:FileUpload ID="TechnicalDocumentUpload" runat="server" AllowMultiple="true" />
                <span class="help">PDF, Office, JPEG or PNG; maximum 20 MB.</span>
            </div>
            <asp:Button ID="SubmitTechnicalButton" runat="server" Text="Submit technical bid" OnClick="SubmitTechnicalButton_Click" />
            <asp:Button ID="UploadTechnicalDocumentButton" runat="server" Text="Upload additional document"
                OnClick="UploadTechnicalDocumentButton_Click" Visible="false" />
        </asp:Panel>
        <asp:Panel ID="ApplicationSubmissionPanel" runat="server" CssClass="card" Visible="false">
            <h3>Contractor application</h3>
            <asp:Literal ID="ApplicationProfileMessage" runat="server" />
            <asp:Literal ID="ApplicationStatusMessage" runat="server" />
            <p class="help">Save a draft while you gather documents. Each upload is checked against this tender's file type and size rules.</p>
            <div class="field">
                <label for="ApplicationDocumentTypeSelect">Document requirement</label>
                <asp:DropDownList ID="ApplicationDocumentTypeSelect" runat="server" />
            </div>
            <div class="field">
                <label for="ApplicationDocumentUpload">Documents</label>
                <asp:FileUpload ID="ApplicationDocumentUpload" runat="server" AllowMultiple="true" />
                <span class="help">Choose up to 10 files for one requirement per upload. Maximum request size 50 MB.</span>
            </div>
            <div class="row">
                <asp:Button ID="SaveApplicationDraftButton" runat="server" Text="Save draft" OnClick="SaveApplicationDraftButton_Click" />
                <asp:HyperLink ID="ApplicationDetailsLink" runat="server" CssClass="button secondary" Visible="false">Open application</asp:HyperLink>
            </div>
            <div class="field">
                <asp:CheckBox ID="ApplicationTermsAccepted" runat="server" Text="I have read and accept the " />
                <asp:HyperLink ID="TermsLink" runat="server" NavigateUrl="~/Pages/Terms.aspx" Target="_blank">Terms &amp; Conditions</asp:HyperLink>
                <asp:Button ID="SubmitApplicationButton" runat="server" Text="Submit application" OnClick="SubmitApplicationButton_Click" />
            </div>
        </asp:Panel>
        <div class="table-wrap"><asp:GridView ID="TechnicalGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid" EmptyDataText="No technical bids submitted yet.">
            <Columns>
                <asp:BoundField DataField="ContractorName" HeaderText="Contractor" />
                <asp:TemplateField HeaderText="Bid document"><ItemTemplate>
                    <asp:HyperLink ID="TechnicalBidDocumentLink" runat="server" Text="View document"
                        NavigateUrl='<%# ResolveUrl("~/Pages/DocumentDownload.aspx?technicalBidId=" + Eval("TechnicalBidId")) %>'
                        Visible='<%# !String.IsNullOrWhiteSpace(Convert.ToString(Eval("DocumentPath"))) %>' />
                </ItemTemplate></asp:TemplateField>
                <asp:BoundField DataField="Status" HeaderText="Status" />
                <asp:BoundField DataField="SubmittedDate" HeaderText="Submitted" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                <asp:BoundField DataField="Remarks" HeaderText="Remarks" />
            </Columns>
        </asp:GridView></div>
        <div class="table-wrap">
            <asp:GridView ID="TechnicalDocumentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid"
                EmptyDataText="No uploaded technical bid documents.">
                <Columns>
                    <asp:BoundField DataField="DocumentType" HeaderText="Document type" />
                    <asp:TemplateField HeaderText="File"><ItemTemplate>
                        <asp:HyperLink ID="TechnicalDocumentLink" runat="server" Text='<%# Eval("FileName") %>'
                            NavigateUrl='<%# ResolveUrl("~/Pages/DocumentDownload.aspx?technicalBidDocumentId=" + Eval("TechnicalBidDocumentId")) %>' />
                    </ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="UploadedDate" HeaderText="Uploaded" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                </Columns>
            </asp:GridView>
        </div>
        <asp:Panel ID="EvaluatePanel" runat="server" CssClass="card">
            <h3>Evaluate a submitted technical bid</h3>
            <div class="row">
                <div class="field"><label for="TechnicalBidSelect">Bid</label><asp:DropDownList ID="TechnicalBidSelect" runat="server" /></div>
                <div class="field"><label for="TechnicalStatus">Decision</label><asp:DropDownList ID="TechnicalStatus" runat="server"><asp:ListItem Value="">Select decision</asp:ListItem><asp:ListItem Value="Qualified">Qualified</asp:ListItem><asp:ListItem Value="Disqualified">Disqualified</asp:ListItem></asp:DropDownList></div>
            </div>
            <div class="field"><label for="TechnicalRemarks">Remarks</label><asp:TextBox ID="TechnicalRemarks" runat="server" TextMode="MultiLine" MaxLength="1000" Rows="2" /></div>
            <asp:Button ID="EvaluateButton" runat="server" Text="Record evaluation" OnClick="EvaluateButton_Click" />
        </asp:Panel>
    </section>
    <section class="card" id="FinancialBidsSection" runat="server">
        <h2>Financial bids (lowest bid ranks first)</h2>
        <asp:Panel ID="FinancialSubmissionPanel" runat="server" CssClass="card">
            <h3>Submit financial bid</h3>
            <div class="field" id="FinancialContractorField" runat="server">
                <label for="FinancialContractorSelect">Contractor</label>
                <asp:DropDownList ID="FinancialContractorSelect" runat="server" />
            </div>
            <div class="field">
                <label for="FinancialBidAmountText">Bid Amount (₹)</label>
                <asp:TextBox ID="FinancialBidAmountText" runat="server" TextMode="Number" />
            </div>
            <asp:Button ID="SubmitFinancialButton" runat="server" Text="Submit financial bid" OnClick="SubmitFinancialButton_Click" />
        </asp:Panel>
        <div class="table-wrap"><asp:GridView ID="FinancialGrid" runat="server" AutoGenerateColumns="false" GridLines="None" CssClass="compact-grid" EmptyDataText="No financial bids submitted yet.">
            <Columns>
                <asp:BoundField DataField="Rank" HeaderText="Rank" DataFormatString="L{0}" />
                <asp:BoundField DataField="ContractorName" HeaderText="Contractor" />
                <asp:BoundField DataField="BidAmount" HeaderText="Bid Amount" DataFormatString="₹{0:N2}" />
                <asp:BoundField DataField="Status" HeaderText="Status" />
            </Columns>
        </asp:GridView></div>
        <asp:Panel ID="SelectPanel" runat="server" CssClass="card">
            <h3>Committee recommendation</h3>
            <div class="field"><label for="FinancialBidSelect">Select financial bid</label><asp:DropDownList ID="FinancialBidSelect" runat="server" /></div>
            <div class="field"><label for="SelectionRemarks">Remarks (optional)</label><asp:TextBox ID="SelectionRemarks" runat="server" TextMode="MultiLine" MaxLength="1000" Rows="2" /></div>
            <asp:Button ID="SelectContractorButton" runat="server" Text="Recommend contractor" OnClick="SelectContractorButton_Click" />
        </asp:Panel>
    </section>
</asp:Content>
