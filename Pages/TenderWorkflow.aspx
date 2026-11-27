<%@ Page Title="Finalized Tender Workflow" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="TenderWorkflow.aspx.cs" Inherits="DepartmentModule.Pages.TenderWorkflow" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server">Finalized Tender Workflow</asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading"><div><h1>Finalized tender workflow</h1><div class="muted">Assigned tenders for the current role and department.</div></div><a href="Default.aspx" class="button secondary">Dashboard</a></div>
    <asp:Literal ID="Message" runat="server" />
    <asp:Panel ID="QueuePanel" runat="server" CssClass="card">
        <h2>My assigned queue</h2>
        <div class="table-wrap">
            <asp:GridView ID="WorkflowQueue" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="No tenders are currently assigned to you.">
                <Columns>
                    <asp:HyperLinkField DataTextField="TenderNumber" DataNavigateUrlFields="TenderId" DataNavigateUrlFormatString="TenderDetails.aspx?id={0}" HeaderText="Tender" />
                    <asp:BoundField DataField="Title" HeaderText="Title" />
                    <asp:BoundField DataField="DepartmentName" HeaderText="Department" />
                    <asp:BoundField DataField="StageName" HeaderText="Assigned stage" />
                    <asp:BoundField DataField="CurrentStatus" HeaderText="Status" />
                    <asp:BoundField DataField="AssignedAtUtc" HeaderText="Assigned" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:HyperLinkField Text="Open workflow" DataNavigateUrlFields="TenderId"
                        DataNavigateUrlFormatString="TenderWorkflow.aspx?id={0}" HeaderText="Action" />
                </Columns>
            </asp:GridView>
        </div>
    </asp:Panel>
    <asp:Panel ID="DetailPanel" runat="server" Visible="false">
        <section class="card">
            <div class="page-heading"><div><h2><asp:Literal ID="TenderNumber" runat="server" /> — <asp:Literal ID="TenderTitle" runat="server" /></h2><div class="muted"><asp:Literal ID="DepartmentName" runat="server" /> · <asp:Literal ID="StageName" runat="server" /></div></div><a class="button secondary" href="TenderDetails.aspx?id=<%= Request.QueryString["id"] %>">Tender details</a></div>
            <p><strong>Current status:</strong> <asp:Literal ID="CurrentStatus" runat="server" /></p>
            <p><strong>Estimated cost:</strong> ₹<asp:Literal ID="EstimatedAmount" runat="server" /></p>
        </section>
        <asp:Panel ID="SubmitDraftPanel" runat="server" CssClass="card" Visible="false">
            <h2>JE initial document verification</h2>
            <p class="help">Review and mark every uploaded tender document as verified before forwarding the tender to Tender Cell.</p>
            <section class="submission-readiness">
                <h3>Pre-submission checklist</h3>
                <p class="help">The tender stays with JE until every required field, document, and preparation step below is complete.</p>
                <p id="TenderSubmissionReadinessSummary" class="submission-readiness-summary" role="status" aria-live="polite">
                    <asp:Literal ID="TenderSubmissionReadinessSummaryText" runat="server" />
                </p>
                <div id="TenderSubmissionReadinessItems" class="submission-readiness-list">
                    <asp:Repeater ID="TenderSubmissionReadiness" runat="server">
                        <ItemTemplate>
                            <div class='submission-readiness-item <%# Convert.ToBoolean(Eval("IsComplete")) ? "is-complete" : "is-pending" %>'
                                data-readiness-key='<%# Eval("Key") %>'
                                data-complete='<%# Convert.ToBoolean(Eval("IsComplete")) ? "true" : "false" %>'>
                                <span class="submission-readiness-indicator" aria-hidden="true"></span>
                                <div class="submission-readiness-copy">
                                    <strong><%#: Eval("Name") %></strong>
                                    <span>Step: <%#: Eval("Step") %></span>
                                </div>
                                <span class='submission-readiness-status <%# Convert.ToBoolean(Eval("IsComplete")) ? "is-complete" : "is-pending" %>'
                                    data-readiness-status><%#: Eval("Status") %></span>
                                <asp:HyperLink ID="ReadinessActionLink" runat="server"
                                    CssClass="submission-readiness-action" Text="Open section"
                                    NavigateUrl='<%# Eval("ActionUrl") %>'
                                    Visible='<%# !Convert.ToBoolean(Eval("IsComplete")) && !String.IsNullOrEmpty(Convert.ToString(Eval("ActionUrl"))) %>' />
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>
            </section>
            <asp:HyperLink ID="ApplicationRequirementsLink" runat="server" CssClass="button secondary">Manage application requirements</asp:HyperLink>
            <div class="field"><label for="RequiredBidDocumentsText">Required bidder technical documents</label>
                <span class="help">Enter one document name per line. Use the requirements manager to set descriptions, optional items, accepted formats, and file-size limits.</span>
                <asp:TextBox ID="RequiredBidDocumentsText" runat="server" TextMode="MultiLine" MaxLength="8000" Rows="6" /></div>
            <div class="field"><label for="SubmitRemarksText">Remarks</label><asp:TextBox ID="SubmitRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="3" /></div>
            <asp:Button ID="SubmitDraftButton" runat="server" Text="Approve initial verification and forward to Tender Cell"
                OnClick="SubmitDraftButton_Click" OnClientClick="return validateTenderSubmissionReadiness();" />
        </asp:Panel>
        <asp:Panel ID="InitialTenderCellVerificationPanel" runat="server" CssClass="card" Visible="false">
            <h2>Tender Cell independent document verification</h2>
            <p class="help">Open the tender documents, inspect or download each file, and record a separate Tender Cell decision for every document.</p>
            <asp:HyperLink ID="TenderDocumentVerificationLink" runat="server" CssClass="button secondary">
                Review tender documents
            </asp:HyperLink>
            <asp:Literal ID="TenderDocumentShortfallSummary" runat="server" />
            <div class="field">
                <label for="CellDocumentVerificationRemarksText">Verification remarks</label>
                <asp:TextBox ID="CellDocumentVerificationRemarksText" runat="server" TextMode="MultiLine"
                    MaxLength="2000" Rows="3" />
            </div>
            <div class="field">
                <label for="RequiredTenderDocumentsText">Required documents for a shortfall</label>
                <span class="help">If a document is missing or incorrect, list each required document and explain the deficiency in the remarks above.</span>
                <asp:TextBox ID="RequiredTenderDocumentsText" runat="server" TextMode="MultiLine"
                    MaxLength="2000" Rows="3" />
            </div>
            <div class="row">
                <asp:Button ID="ApproveInitialTenderDocumentsButton" runat="server"
                    Text="Approve document verification"
                    OnClick="ApproveInitialTenderDocumentsButton_Click" />
                <asp:Button ID="RaiseTenderDocumentShortfallButton" runat="server"
                    Text="Raise Document Shortfall" CssClass="button secondary"
                    OnClick="RaiseTenderDocumentShortfallButton_Click" />
            </div>
        </asp:Panel>
        <asp:Panel ID="JeDocumentCorrectionPanel" runat="server" CssClass="card" Visible="false">
            <h2>JE document correction</h2>
            <p class="help">Upload the requested corrections as new files. Previous documents are retained for audit history. Tender Cell must re-verify the resubmission.</p>
            <asp:Literal ID="TenderDocumentCorrectionShortfallSummary" runat="server" />
            <asp:HyperLink ID="CorrectTenderDocumentsLink" runat="server" CssClass="button secondary">
                Open tender documents
            </asp:HyperLink>
            <asp:Button ID="ResubmitTenderDocumentsButton" runat="server"
                Text="Submit corrected documents for Tender Cell re-verification"
                OnClick="ResubmitTenderDocumentsButton_Click" />
        </asp:Panel>
        <asp:Panel ID="PublishPanel" runat="server" CssClass="card" Visible="false">
            <h2>Publication schedule</h2>
            <p class="help">Tender Cell manages the publication window. Review the saved schedule below before publishing.</p>
            <p><asp:Literal ID="PublicationScheduleSummary" runat="server" /></p>
            <div class="field"><label for="PublishRemarksText">Remarks</label><asp:TextBox ID="PublishRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="3" /></div>
            <asp:Button ID="PublishButton" runat="server" Text="Publish tender" OnClick="PublishButton_Click" />
        </asp:Panel>
        <asp:Panel ID="PrimaryOpeningPanel" runat="server" CssClass="card" Visible="false">
            <h2>Initial technical bid opening</h2>
            <p class="help">This records the initial technical opening only. Financial bid information remains unavailable.</p>
            <div class="field"><label for="PrimaryOpenRemarksText">Opening remarks</label>
                <asp:TextBox ID="PrimaryOpenRemarksText" runat="server" TextMode="MultiLine" MaxLength="1000" Rows="3" /></div>
            <asp:Button ID="OpenTechnicalBidsButton" runat="server" Text="Record technical bid opening"
                OnClick="OpenTechnicalBidsButton_Click" />
        </asp:Panel>
        <asp:Panel ID="PrimaryVerificationPanel" runat="server" CssClass="card" Visible="false">
            <h2>JE primary document verification</h2>
            <p class="help">Review technical and eligibility documents only. Each submitted document and each bidder requires a recorded review before forwarding.</p>
            <h3>Bidder registration and primary review</h3>
            <div class="table-wrap">
                <asp:GridView ID="PrimaryBidderGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    DataKeyNames="TechnicalBidId,ContractorId" OnRowCommand="PrimaryBidderGrid_RowCommand"
                    EmptyDataText="No technical bids were received.">
                    <Columns>
                        <asp:BoundField DataField="ContractorName" HeaderText="Contractor" />
                        <asp:BoundField DataField="RegistrationNo" HeaderText="Registration" />
                        <asp:BoundField DataField="ContactNo" HeaderText="Contact" />
                        <asp:BoundField DataField="Email" HeaderText="Email" />
                        <asp:BoundField DataField="SubmittedDate" HeaderText="Bid submitted" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:BoundField DataField="DocumentCount" HeaderText="Documents" />
                        <asp:BoundField DataField="PrimaryDecision" HeaderText="JE status" />
                        <asp:BoundField DataField="PrimaryRemarks" HeaderText="Previous JE remarks" />
                        <asp:TemplateField HeaderText="Bidder review"><ItemTemplate>
                            <asp:TextBox ID="PrimaryBidderRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000"
                                Rows="2" Text='<%# Eval("PrimaryRemarks") %>' />
                            <asp:Button ID="RecordPrimaryBidderReviewButton" runat="server" Text="Record bidder review"
                                CommandName="RecordPrimaryBidderReview" CommandArgument='<%# Container.DataItemIndex %>'
                                Visible='<%# String.IsNullOrEmpty(Convert.ToString(Eval("PrimaryDecision"))) %>' />
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
            <h3>Submitted technical documents</h3>
            <div class="table-wrap">
                <asp:GridView ID="PrimaryDocumentGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    DataKeyNames="TechnicalBidDocumentId,ContractorId,DocumentType,IsMissing" OnRowCommand="PrimaryDocumentGrid_RowCommand"
                    EmptyDataText="No technical bid documents were uploaded. Record bidder reviews for missing-document cases.">
                    <Columns>
                        <asp:BoundField DataField="ContractorName" HeaderText="Contractor" />
                        <asp:BoundField DataField="DocumentType" HeaderText="Document type" />
                        <asp:TemplateField HeaderText="Document"><ItemTemplate>
                            <asp:HyperLink ID="PrimaryDocumentPreviewLink" runat="server" Text="Preview"
                                Visible='<%# !Convert.ToBoolean(Eval("IsMissing")) && SupportsInlinePreview(Eval("FileName")) %>'
                                NavigateUrl='<%# ResolveUrl("~/Pages/DocumentDownload.aspx?technicalBidDocumentId=" + Eval("TechnicalBidDocumentId") + "&amp;preview=true") %>' />
                            <asp:HyperLink ID="PrimaryDocumentDownloadLink" runat="server" Text='<%# Eval("FileName") %>'
                                Visible='<%# !Convert.ToBoolean(Eval("IsMissing")) %>'
                                NavigateUrl='<%# ResolveUrl("~/Pages/DocumentDownload.aspx?technicalBidDocumentId=" + Eval("TechnicalBidDocumentId")) %>' />
                        </ItemTemplate></asp:TemplateField>
                        <asp:BoundField DataField="UploadedDate" HeaderText="Uploaded" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:BoundField DataField="VerificationStatus" HeaderText="Previous decision" />
                        <asp:TemplateField HeaderText="JE decision"><ItemTemplate>
                            <asp:DropDownList ID="PrimaryDocumentDecision" runat="server" SelectedValue='<%# Eval("VerificationStatus") %>'>
                                <asp:ListItem Value="">Select decision</asp:ListItem>
                                <asp:ListItem Value="Verified">Verified</asp:ListItem>
                                <asp:ListItem Value="Not Verified">Not Verified</asp:ListItem>
                            </asp:DropDownList>
                            <asp:TextBox ID="PrimaryDocumentRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000"
                                Rows="2" Text='<%# Eval("VerificationRemarks") %>' />
                            <asp:Button ID="RecordPrimaryDocumentButton" runat="server" Text="Save decision"
                                CommandName="RecordPrimaryDocument" CommandArgument='<%# Container.DataItemIndex %>'
                                Visible='<%# String.IsNullOrEmpty(Convert.ToString(Eval("VerificationStatus"))) %>' />
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
            <div class="field"><label for="PrimaryCompletionRemarksText">Verification completion remarks</label>
                <asp:TextBox ID="PrimaryCompletionRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="2" /></div>
            <asp:Button ID="CompletePrimaryVerificationButton" runat="server" Text="Complete primary verification"
                OnClick="CompletePrimaryVerificationButton_Click" />
        </asp:Panel>
        <asp:Panel ID="PrimaryForwardPanel" runat="server" CssClass="card" Visible="false">
            <h2>Forward to Tender Cell</h2>
            <p class="help">Tender Cell will conduct a separate secondary review.</p>
            <div class="field"><label for="PrimaryForwardRemarksText">Forwarding remarks</label>
                <asp:TextBox ID="PrimaryForwardRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="2" /></div>
            <asp:Button ID="ForwardToTenderCellButton" runat="server" Text="Forward for secondary verification"
                OnClick="ForwardToTenderCellButton_Click" />
        </asp:Panel>
        <asp:Panel ID="SecondaryVerificationPanel" runat="server" CssClass="card" Visible="false">
            <h2>Tender Cell secondary verification</h2>
            <p class="help">Open each submitted application, record a decision for every uploaded document and missing mandatory document, then record the bidder decision. The tender can advance only when every bidder is reviewed.</p>
            <div class="table-wrap">
                <asp:GridView ID="SecondaryApplicationsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    EmptyDataText="No submitted applications are available for secondary review.">
                    <Columns>
                        <asp:HyperLinkField DataTextField="ContractorName" DataNavigateUrlFields="TechnicalBidId"
                            DataNavigateUrlFormatString="ApplicationDetails.aspx?id={0}" HeaderText="Application" />
                        <asp:BoundField DataField="RegistrationNo" HeaderText="Registration" />
                        <asp:BoundField DataField="SubmittedDate" HeaderText="Submitted" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:BoundField DataField="SecondaryDecision" HeaderText="Secondary decision" />
                        <asp:BoundField DataField="SecondaryRemarks" HeaderText="Remarks" />
                    </Columns>
                </asp:GridView>
            </div>
            <div class="field"><label for="SecondaryRemarksText">Tender Cell completion remarks</label>
                <asp:TextBox ID="SecondaryRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="2" /></div>
            <div class="row">
                <asp:Button ID="CompleteSecondaryVerificationButton" runat="server" Text="Complete verified review"
                    OnClick="CompleteSecondaryVerificationButton_Click" />
                <asp:Button ID="SendToShortfallCreationButton" runat="server" Text="Create required shortfall"
                    CssClass="button secondary" OnClick="SendToShortfallCreationButton_Click" />
                <asp:Button ID="ReverifiedOpeningToChairmanButton" runat="server" Text="Return verified review to Chairman"
                    OnClick="ReverifiedOpeningToChairmanButton_Click" />
                <asp:Button ID="ReverifiedOpeningToShortfallButton" runat="server" Text="Create required shortfall"
                    CssClass="button secondary" OnClick="ReverifiedOpeningToShortfallButton_Click" />
            </div>
        </asp:Panel>
        <asp:Panel ID="RejectedOpeningReverificationPanel" runat="server" CssClass="card" Visible="false">
            <h2>Financial opening return — Tender Cell re-verification</h2>
            <p class="help">Recheck every submitted application and document. Open each application below to update its bidder and document decisions before returning the verified result to the Chairman or creating a shortfall.</p>
        </asp:Panel>
        <asp:Panel ID="ShortfallCreationPanel" runat="server" CssClass="card" Visible="false">
            <h2>Shortfall requests requiring correction</h2>
            <p class="help">Open a bidder marked Shortfall, select each deficient item, and describe the deficiency and corrective action. A request is not sent until the Junior Engineer approves it.</p>
            <div class="table-wrap">
                <asp:GridView ID="ShortfallApplicationsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    EmptyDataText="No bidder has a secondary review marked Shortfall.">
                    <Columns>
                        <asp:HyperLinkField DataTextField="ContractorName" DataNavigateUrlFields="TechnicalBidId"
                            DataNavigateUrlFormatString="ApplicationDetails.aspx?id={0}" HeaderText="Application" />
                        <asp:BoundField DataField="RegistrationNo" HeaderText="Registration" />
                        <asp:BoundField DataField="SecondaryRemarks" HeaderText="Tender Cell remarks" />
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>
        <asp:Panel ID="ShortfallApprovalPanel" runat="server" CssClass="card" Visible="false">
            <h2>Junior Engineer shortfall approval</h2>
            <p class="help">Approval starts the fixed three-calendar-day response period. Returning the request sends it back to Tender Cell for correction.</p>
            <div class="table-wrap">
                <asp:GridView ID="ShortfallItemsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    EmptyDataText="This shortfall does not contain any items.">
                    <Columns>
                        <asp:BoundField DataField="RequiredDocumentType" HeaderText="Requirement" />
                        <asp:BoundField DataField="ShortfallDescription" HeaderText="Deficiency" />
                        <asp:BoundField DataField="CorrectiveAction" HeaderText="Corrective action" />
                        <asp:BoundField DataField="ClearanceStatus" HeaderText="Status" />
                    </Columns>
                </asp:GridView>
            </div>
            <div class="field"><label for="ShortfallDecisionRemarksText">Decision remarks</label>
                <asp:TextBox ID="ShortfallDecisionRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="2" /></div>
            <div class="row">
                <asp:Button ID="ApproveShortfallButton" runat="server" Text="Approve and notify contractor"
                    OnClick="ApproveShortfallButton_Click" />
                <asp:Button ID="ReturnShortfallButton" runat="server" Text="Return to Tender Cell"
                    CssClass="button secondary" OnClick="ReturnShortfallButton_Click" />
            </div>
        </asp:Panel>
        <asp:Panel ID="ShortfallClearancePanel" runat="server" CssClass="card" Visible="false">
            <h2>Tender Cell shortfall clearance</h2>
            <p class="help">Review the latest corrected file for each item. A continued deficiency may be returned while the original deadline remains open; expired requests disqualify that application.</p>
            <div class="table-wrap">
                <asp:GridView ID="ShortfallClearanceGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    DataKeyNames="ShortfallItemId" OnRowCommand="ShortfallClearanceGrid_RowCommand"
                    EmptyDataText="No submitted items are waiting for clearance.">
                    <Columns>
                        <asp:BoundField DataField="RequiredDocumentType" HeaderText="Requirement" />
                        <asp:BoundField DataField="ShortfallDescription" HeaderText="Deficiency" />
                        <asp:HyperLinkField DataTextField="FileName" DataNavigateUrlFields="ShortfallSubmissionId"
                            DataNavigateUrlFormatString="DocumentDownload.aspx?shortfallSubmissionId={0}" HeaderText="Latest corrected document" />
                        <asp:BoundField DataField="ClearanceStatus" HeaderText="Status" />
                        <asp:TemplateField HeaderText="Decision"><ItemTemplate>
                            <asp:DropDownList ID="ShortfallClearanceDecisionSelect" runat="server">
                                <asp:ListItem Value="">Select decision</asp:ListItem>
                                <asp:ListItem Value="Cleared">Cleared</asp:ListItem>
                                <asp:ListItem Value="Continued Deficiency">Continued deficiency</asp:ListItem>
                            </asp:DropDownList>
                            <asp:TextBox ID="ShortfallClearanceRemarksText" runat="server" TextMode="MultiLine"
                                MaxLength="2000" Rows="2" />
                            <asp:Button ID="ReviewShortfallItemButton" runat="server" Text="Record item decision"
                                CommandName="ReviewShortfallItem" CommandArgument='<%# Container.DataItemIndex %>' />
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>
        <asp:Panel ID="FinancialOpeningApprovalPanel" runat="server" CssClass="card" Visible="false">
            <h2>Chairman financial-opening decision</h2>
            <p class="help">Financial bid amounts remain hidden until this approval is recorded.</p>
            <div class="field"><label for="FinancialOpeningRemarksText">Decision remarks</label>
                <asp:TextBox ID="FinancialOpeningRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="3" /></div>
            <div class="row">
                <asp:Button ID="ApproveFinancialOpeningButton" runat="server" Text="Approve financial opening"
                    OnClick="ApproveFinancialOpeningButton_Click" />
                <asp:Button ID="RejectFinancialOpeningButton" runat="server" Text="Return to Tender Cell"
                    CssClass="button secondary" OnClick="RejectFinancialOpeningButton_Click" />
            </div>
        </asp:Panel>
        <asp:Panel ID="FinancialBidEntryPanel" runat="server" CssClass="card" Visible="false">
            <h2>JE financial bid opening and data entry</h2>
            <p class="help">Enter the amount for every technically approved bidder. This entry is available only after Chairman approval.</p>
            <div class="table-wrap">
                <asp:GridView ID="FinancialEntryGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    DataKeyNames="ContractorId" EmptyDataText="No approved bidders are available for financial opening.">
                    <Columns>
                        <asp:BoundField DataField="ContractorName" HeaderText="Contractor" />
                        <asp:BoundField DataField="RegistrationNo" HeaderText="Registration" />
                        <asp:TemplateField HeaderText="Quoted amount (₹)"><ItemTemplate>
                            <asp:TextBox ID="FinancialBidAmountText" runat="server" TextMode="Number"
                                Text="" />
                        </ItemTemplate></asp:TemplateField>
                        <asp:TemplateField HeaderText="Remarks"><ItemTemplate>
                            <asp:TextBox ID="FinancialBidRemarksText" runat="server" MaxLength="1000" />
                        </ItemTemplate></asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
            <div class="field"><label for="FinancialEntryRemarksText">Opening remarks</label>
                <asp:TextBox ID="FinancialEntryRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="2" /></div>
            <asp:Button ID="SaveFinancialBidsButton" runat="server" Text="Record financial opening and submit for audit"
                OnClick="SaveFinancialBidsButton_Click" />
        </asp:Panel>
        <asp:Panel ID="AuditorFinancialReviewPanel" runat="server" CssClass="card" Visible="false">
            <h2>Director Auditor financial review</h2>
            <div class="table-wrap">
                <asp:GridView ID="FinancialReviewGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    EmptyDataText="No opened financial bids are available for review.">
                    <Columns>
                        <asp:BoundField DataField="ContractorName" HeaderText="Contractor" />
                        <asp:BoundField DataField="RegistrationNo" HeaderText="Registration" />
                        <asp:BoundField DataField="QuotedAmount" HeaderText="Quoted amount" DataFormatString="{0:N2}" />
                        <asp:BoundField DataField="Remarks" HeaderText="JE remarks" />
                    </Columns>
                </asp:GridView>
            </div>
            <div class="field"><label for="SelectedFinancialBidSelect">Selected bid</label>
                <asp:DropDownList ID="SelectedFinancialBidSelect" runat="server" /></div>
            <div class="field"><label for="FinancialDecisionRemarksText">Audit decision remarks</label>
                <asp:TextBox ID="FinancialDecisionRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="3" /></div>
            <div class="row">
                <asp:Button ID="ApproveFinancialReviewButton" runat="server" Text="Approve selected bid"
                    OnClick="ApproveFinancialReviewButton_Click" />
                <asp:Button ID="ReturnFinancialReviewButton" runat="server" Text="Return to JE"
                    CssClass="button secondary" OnClick="ReturnFinancialReviewButton_Click" />
            </div>
        </asp:Panel>
        <asp:Panel ID="FinancialResubmitPanel" runat="server" CssClass="card" Visible="false">
            <h2>Resubmit financial review</h2>
            <p class="help">Review the Auditor's return remarks in the workflow history and provide the Junior Engineer's response.</p>
            <div class="field"><label for="FinancialResubmissionRemarksText">Resubmission remarks</label>
                <asp:TextBox ID="FinancialResubmissionRemarksText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="3" /></div>
            <asp:Button ID="ResubmitFinancialReviewButton" runat="server" Text="Resubmit to Director Auditor"
                OnClick="ResubmitFinancialReviewButton_Click" />
        </asp:Panel>
        <asp:Panel ID="WorkOrderCreatePanel" runat="server" CssClass="card" Visible="false">
            <h2>Create work order</h2>
            <p>Approved contractor: <strong><asp:Literal ID="WorkOrderBidderName" runat="server" /></strong></p>
            <p>Auditor-approved amount: ₹<strong><asp:Literal ID="WorkOrderBidAmount" runat="server" /></strong></p>
            <div class="row">
                <div class="field"><label for="WorkOrderNumberText">Work order number</label><asp:TextBox ID="WorkOrderNumberText" runat="server" MaxLength="50" /></div>
                <div class="field"><label for="WorkOrderIssueDateText">Issue date and time (IST)</label><asp:TextBox ID="WorkOrderIssueDateText" runat="server" TextMode="DateTimeLocal" /></div>
            </div>
            <div class="field"><label for="WorkDescriptionText">Work description</label><asp:TextBox ID="WorkDescriptionText" runat="server" TextMode="MultiLine" MaxLength="2000" Rows="3" /></div>
            <div class="field"><label for="WorkOrderTermsText">Terms and conditions</label><asp:TextBox ID="WorkOrderTermsText" runat="server" TextMode="MultiLine" MaxLength="4000" Rows="4" /></div>
            <div class="field"><label for="WorkOrderSignatoryText">Authorized signatory</label><asp:TextBox ID="WorkOrderSignatoryText" runat="server" MaxLength="200" /></div>
            <asp:Button ID="CreateWorkOrderButton" runat="server" Text="Create work order record"
                OnClick="CreateWorkOrderButton_Click" />
        </asp:Panel>
        <asp:Panel ID="WorkOrderGeneratePanel" runat="server" CssClass="card" Visible="false">
            <h2>Generate work order PDF</h2>
            <p class="help">The PDF uses the approved contractor and amount and records a versioned copy in the tender history.</p>
            <asp:Button ID="GenerateWorkOrderButton" runat="server" Text="Generate Work Order PDF"
                OnClick="GenerateWorkOrderButton_Click" />
        </asp:Panel>
        <asp:Panel ID="AgreementCreatePanel" runat="server" CssClass="card" Visible="false">
            <h2>Create tender agreement</h2>
            <p>Approved contractor: <strong><asp:Literal ID="AgreementBidderName" runat="server" /></strong></p>
            <p>Auditor-approved amount: ₹<strong><asp:Literal ID="AgreementBidAmount" runat="server" /></strong></p>
            <div class="row">
                <div class="field"><label for="AgreementNumberText">Agreement number</label><asp:TextBox ID="AgreementNumberText" runat="server" MaxLength="50" /></div>
                <div class="field"><label for="AgreementDateText">Agreement date and time (IST)</label><asp:TextBox ID="AgreementDateText" runat="server" TextMode="DateTimeLocal" /></div>
            </div>
            <div class="field"><label for="AgreementTermsText">Terms and conditions</label><asp:TextBox ID="AgreementTermsText" runat="server" TextMode="MultiLine" MaxLength="4000" Rows="4" /></div>
            <div class="field"><label for="AgreementSignatoryText">Authorized signatory</label><asp:TextBox ID="AgreementSignatoryText" runat="server" MaxLength="200" /></div>
            <asp:Button ID="CreateAgreementButton" runat="server" Text="Create agreement record"
                OnClick="CreateAgreementButton_Click" />
        </asp:Panel>
        <asp:Panel ID="AgreementGeneratePanel" runat="server" CssClass="card" Visible="false">
            <h2>Generate tender agreement PDF</h2>
            <p class="help">The approved agreement and final BOQ are generated and versioned together after financial approval.</p>
            <asp:Button ID="GenerateAgreementButton" runat="server" Text="Generate Agreement PDF"
                OnClick="GenerateAgreementButton_Click" />
        </asp:Panel>
        <asp:Panel ID="CompleteTenderPanel" runat="server" CssClass="card" Visible="false">
            <h2>Complete tender workflow</h2>
            <p class="help">Completion is enabled after both the agreement and final BOQ PDFs are generated and recorded.</p>
            <asp:Button ID="CompleteTenderButton" runat="server" Text="Complete tender"
                OnClick="CompleteTenderButton_Click" />
        </asp:Panel>
        <section class="card">
            <h2>Versioned generated documents</h2>
            <div class="table-wrap">
                <asp:GridView ID="GeneratedDocumentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                    EmptyDataText="No generated work order, agreement, or BOQ documents have been recorded.">
                    <Columns>
                        <asp:BoundField DataField="DocumentKind" HeaderText="Document" />
                        <asp:BoundField DataField="VersionNumber" HeaderText="Version" />
                        <asp:HyperLinkField DataTextField="FileName" DataNavigateUrlFields="GeneratedDocumentId"
                            DataNavigateUrlFormatString="DocumentDownload.aspx?generatedDocumentId={0}" HeaderText="PDF" />
                        <asp:BoundField DataField="GeneratedAtUtc" HeaderText="Generated" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    </Columns>
                </asp:GridView>
            </div>
        </section>
        <section class="card">
            <h2>Immutable workflow history</h2>
            <div class="table-wrap">
                <asp:GridView ID="HistoryGrid" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="No workflow history has been recorded.">
                    <Columns>
                        <asp:BoundField DataField="ActionAtUtc" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                        <asp:BoundField DataField="StageName" HeaderText="Stage" />
                        <asp:BoundField DataField="PreviousStatus" HeaderText="Previous status" />
                        <asp:BoundField DataField="NewStatus" HeaderText="New status" />
                        <asp:BoundField DataField="ActionName" HeaderText="Action" />
                        <asp:BoundField DataField="Actor" HeaderText="By" />
                        <asp:BoundField DataField="Remarks" HeaderText="Remarks" />
                    </Columns>
                </asp:GridView>
            </div>
        </section>
    </asp:Panel>
    <script type="text/javascript">
        function hasValidBidDocumentTypes() {
            var input = document.querySelector("[id$='RequiredBidDocumentsText']");
            if (!input || input.value.length > 8000) return false;
            var names = input.value.split(/\r?\n/).map(function (name) { return name.trim(); })
                .filter(function (name) { return name.length > 0; });
            if (names.length < 1 || names.length > 30) return false;
            var uniqueNames = {};
            return names.every(function (name) {
                var key = name.toLowerCase();
                if (name.length > 160 || uniqueNames[key]) return false;
                uniqueNames[key] = true;
                return true;
            });
        }

        function updateBidDocumentReadiness() {
            var row = document.querySelector("[data-readiness-key='bid-document-types']");
            if (!row) return;
            var complete = hasValidBidDocumentTypes();
            row.setAttribute("data-complete", complete ? "true" : "false");
            row.classList.toggle("is-complete", complete);
            row.classList.toggle("is-pending", !complete);
            var status = row.querySelector("[data-readiness-status]");
            if (status) {
                status.textContent = complete ? "Complete" : "Pending";
                status.classList.toggle("is-complete", complete);
                status.classList.toggle("is-pending", !complete);
            }
        }

        function updateTenderReadinessSummary() {
            var rows = Array.prototype.slice.call(
                document.querySelectorAll("#TenderSubmissionReadinessItems .submission-readiness-item"));
            var pending = rows.filter(function (row) {
                return row.getAttribute("data-complete") !== "true";
            });
            var summary = document.getElementById("TenderSubmissionReadinessSummary");
            if (summary) {
                summary.classList.toggle("has-pending", pending.length > 0);
                summary.textContent = pending.length
                    ? pending.length + " required item" + (pending.length === 1 ? " is" : "s are") +
                        " still pending. Complete these items before forwarding to Tender Cell."
                    : "All required items are complete. This tender is ready for Tender Cell review.";
            }
            return pending;
        }

        document.addEventListener("input", function (event) {
            if (event.target && event.target.id &&
                /RequiredBidDocumentsText$/.test(event.target.id)) {
                updateBidDocumentReadiness();
                updateTenderReadinessSummary();
            }
        });

        function validateTenderSubmissionReadiness() {
            updateBidDocumentReadiness();
            var pending = updateTenderReadinessSummary();
            var summary = document.getElementById("TenderSubmissionReadinessSummary");
            if (pending.length) {
                if (summary) {
                    summary.setAttribute("tabindex", "-1");
                    summary.focus();
                    summary.scrollIntoView({ behavior: "smooth", block: "center" });
                }
                return false;
            }
            return true;
        }
    </script>
</asp:Content>
