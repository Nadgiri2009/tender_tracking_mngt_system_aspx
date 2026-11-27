<%@ Page Title="Tender Details" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="TenderDetails.aspx.cs" Inherits="DepartmentModule.Pages.TenderDetails" %>
<asp:Content ID="Title" ContentPlaceHolderID="TitleContent" runat="server"><asp:Literal ID="PageTitle" runat="server" /></asp:Content>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="page-heading">
        <div><h1><asp:Literal ID="TenderTitle" runat="server" /></h1>
            <div class="muted"><asp:Literal ID="TenderNumber" runat="server" /> · <asp:Literal ID="DepartmentName" runat="server" /></div></div>
        <div class="row">
            <asp:HyperLink ID="ApplyTenderLink" runat="server" CssClass="button" Visible="false">Apply for this tender</asp:HyperLink>
            <asp:HyperLink ID="FinalWorkflowLink" runat="server" NavigateUrl="~/Pages/TenderWorkflow.aspx" CssClass="button secondary" Visible="false">Open finalized workflow</asp:HyperLink>
            <asp:HyperLink ID="EditTenderLink" runat="server" CssClass="button secondary" Visible="false">Edit draft</asp:HyperLink>
            <asp:Button ID="DeleteTenderButton" runat="server" Text="Delete draft" CssClass="button danger" Visible="false"
                OnClick="DeleteTenderButton_Click" OnClientClick="return confirm('Remove this initial-stage draft from active tender records? Its workflow and audit history will be retained.');" />
            <asp:HyperLink ID="BackToTendersLink" runat="server" CssClass="button secondary">Back to tenders</asp:HyperLink>
        </div>
    </div>
    <asp:Literal ID="ApplicationMessage" runat="server" />
    <asp:Literal ID="Message" runat="server" />
    <section class="card tender-workflow-tracker" aria-labelledby="workflowTrackerHeading">
        <div class="workflow-tracker-heading">
            <div>
                <span class="role-section-kicker">TENDER APPROVAL &amp; PROCESS TRACKER</span>
                <h2 id="workflowTrackerHeading">Approval and process tracker</h2>
            </div>
            <div class="workflow-current-stage"><asp:Literal ID="TrackerCurrentStage" runat="server" /></div>
        </div>
        <asp:Panel ID="CurrentDocumentShortfallPanel" runat="server" CssClass="workflow-shortfall" Visible="false">
            <asp:Literal ID="DocumentShortfallSummary" runat="server" />
        </asp:Panel>
        <ol class="workflow-timeline">
            <asp:Repeater ID="WorkflowTimeline" runat="server">
                <ItemTemplate>
                    <li class='workflow-step <%# Eval("StatusClass") %>'>
                        <span class="workflow-step-number"><%#: Eval("StageNumber") %></span>
                        <div class="workflow-step-content">
                            <div class="workflow-step-heading">
                                <div>
                                    <h3><%#: Eval("ResponsibleRole") %> — <%#: Eval("StageTitle") %></h3>
                                    <asp:Panel runat="server" CssClass="workflow-assignee"
                                        Visible='<%# !String.Equals(Convert.ToString(Eval("AssignedOfficer")), "—", StringComparison.Ordinal) %>'>
                                        <p>Assigned officer: <%#: Eval("AssignedOfficer") %></p>
                                    </asp:Panel>
                                </div>
                                <span class="workflow-status"><%#: Eval("Status") %></span>
                            </div>
                            <asp:Panel runat="server" CssClass="workflow-step-meta"
                                Visible='<%# !String.Equals(Convert.ToString(Eval("SubmissionDate")), "—", StringComparison.Ordinal) || !String.Equals(Convert.ToString(Eval("ApprovalDate")), "—", StringComparison.Ordinal) %>'>
                                <span><strong>Submitted:</strong> <%#: Eval("SubmissionDate") %></span>
                                <span><strong>Approved:</strong> <%#: Eval("ApprovalDate") %></span>
                            </asp:Panel>
                            <asp:Panel runat="server" CssClass="workflow-remarks"
                                Visible='<%# !String.Equals(Convert.ToString(Eval("Remarks")), "—", StringComparison.Ordinal) && !String.IsNullOrWhiteSpace(Convert.ToString(Eval("Remarks"))) %>'>
                                <p><strong>Remarks:</strong> <%#: Eval("Remarks") %></p>
                            </asp:Panel>
                            <details class="workflow-history"
                                runat="server" visible='<%# !String.IsNullOrWhiteSpace(Convert.ToString(Eval("ActionHistory"))) %>'>
                                <summary>Action history</summary>
                                <pre><%#: Eval("ActionHistory") %></pre>
                            </details>
                        </div>
                    </li>
                </ItemTemplate>
            </asp:Repeater>
        </ol>
    </section>
    <section class="card tender-details-card">
        <div class="tender-details-heading">
            <div>
                <span class="role-section-kicker">TENDER OVERVIEW</span>
                <h2>Details</h2>
            </div>
            <span class="badge"><asp:Literal ID="CurrentStatus" runat="server" /></span>
        </div>
        <div class="tender-description">
            <h3>Description</h3>
            <p><asp:Literal ID="Description" runat="server" /></p>
        </div>
        <asp:Panel ID="EligibilityCriteriaPanel" runat="server" Visible="false">
            <div class="tender-description">
                <h3>Eligibility criteria</h3>
                <p><asp:Literal ID="EligibilityCriteria" runat="server" /></p>
            </div>
        </asp:Panel>
        <dl class="tender-detail-grid">
            <div><dt>Tender Method</dt><dd><asp:Literal ID="TenderType" runat="server" /></dd></div>
            <div><dt>Tender Category</dt><dd><asp:Literal ID="TenderCategory" runat="server" /></dd></div>
            <div><dt>Tender Subcategory</dt><dd><asp:Literal ID="TenderSubcategory" runat="server" /></dd></div>
            <div><dt>Budgeting Financial Year</dt><dd><asp:Literal ID="FinancialYear" runat="server" /></dd></div>
            <div><dt>Estimated Cost</dt><dd>₹<asp:Literal ID="EstimatedAmount" runat="server" /></dd></div>
            <div><dt>EMD</dt><dd><asp:Literal ID="EMDAmount" runat="server" /></dd></div>
            <div><dt>Tender document fee</dt><dd><asp:Literal ID="TenderDocumentFee" runat="server" /></dd></div>
            <div><dt>Processing fee</dt><dd><asp:Literal ID="ProcessingFee" runat="server" /></dd></div>
            <div><dt>Security deposit</dt><dd><asp:Literal ID="SecurityDepositAmount" runat="server" /></dd></div>
            <div><dt>Planned publication</dt><dd><asp:Literal ID="PublicationScheduledDate" runat="server" /></dd></div>
            <div><dt>Publication Date</dt><dd><asp:Literal ID="PublicationDate" runat="server" /></dd></div>
            <div><dt>Approved publication period (IST)</dt><dd><asp:Literal ID="PublicationPeriod" runat="server" /></dd></div>
            <div><dt>Approved extensions</dt><dd><asp:Literal ID="ExtensionCount" runat="server" /></dd></div>
            <div><dt>Submission Start Date</dt><dd><asp:Literal ID="SubmissionStartDate" runat="server" /></dd></div>
            <div><dt>Legacy submission deadline</dt><dd><asp:Literal ID="SubmissionDeadline" runat="server" /></dd></div>
            <div><dt>Technical bid opening</dt><dd><asp:Literal ID="TechnicalBidOpeningDate" runat="server" /></dd></div>
            <div><dt>Financial bid opening</dt><dd><asp:Literal ID="FinancialBidOpeningDate" runat="server" /></dd></div>
            <div><dt>Pre-bid meeting</dt><dd><asp:Literal ID="PreBidMeetingDate" runat="server" /></dd></div>
            <div><dt>Pre-bid location</dt><dd><asp:Literal ID="PreBidLocation" runat="server" /></dd></div>
            <div><dt>Pre-bid details</dt><dd><asp:Literal ID="PreBidDetails" runat="server" /></dd></div>
            <div><dt>Created By</dt><dd><asp:Literal ID="CreatedBy" runat="server" /></dd></div>
            <div><dt>Created Date</dt><dd><asp:Literal ID="CreatedDate" runat="server" /></dd></div>
        </dl>
    </section>
    <section class="card">
        <h2>Tender requirements and conditions</h2>
        <p><strong>Scope of work</strong><br /><asp:Literal ID="ScopeOfWork" runat="server" /></p>
        <asp:Panel ID="ScopeOfWorkAttachmentPanel" runat="server" CssClass="tender-requirement-attachment">
            <asp:Literal ID="ScopeOfWorkAttachmentStatus" runat="server" Text="No file attached." />
            <asp:HyperLink ID="ScopeOfWorkAttachmentViewLink" runat="server" Target="_blank" rel="noopener" Visible="false" />
            <asp:HyperLink ID="ScopeOfWorkAttachmentDownloadLink" runat="server" CssClass="icon-action" Visible="false"
                ToolTip="Download Scope of Work document" aria-label="Download Scope of Work document">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v12m0 0 5-5m-5 5-5-5M4 17v3h16v-3" /></svg>
            </asp:HyperLink>
        </asp:Panel>
        <p><strong>Technical requirements</strong><br /><asp:Literal ID="TechnicalRequirements" runat="server" /></p>
        <asp:Panel ID="TechnicalRequirementsAttachmentPanel" runat="server" CssClass="tender-requirement-attachment">
            <asp:Literal ID="TechnicalRequirementsAttachmentStatus" runat="server" Text="No file attached." />
            <asp:HyperLink ID="TechnicalRequirementsAttachmentViewLink" runat="server" Target="_blank" rel="noopener" Visible="false" />
            <asp:HyperLink ID="TechnicalRequirementsAttachmentDownloadLink" runat="server" CssClass="icon-action" Visible="false"
                ToolTip="Download Technical Requirements document" aria-label="Download Technical Requirements document">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v12m0 0 5-5m-5 5-5-5M4 17v3h16v-3" /></svg>
            </asp:HyperLink>
        </asp:Panel>
        <p><strong>Financial requirements</strong><br /><asp:Literal ID="FinancialRequirements" runat="server" /></p>
        <asp:Panel ID="FinancialRequirementsAttachmentPanel" runat="server" CssClass="tender-requirement-attachment">
            <asp:Literal ID="FinancialRequirementsAttachmentStatus" runat="server" Text="No file attached." />
            <asp:HyperLink ID="FinancialRequirementsAttachmentViewLink" runat="server" Target="_blank" rel="noopener" Visible="false" />
            <asp:HyperLink ID="FinancialRequirementsAttachmentDownloadLink" runat="server" CssClass="icon-action" Visible="false"
                ToolTip="Download Financial Requirements document" aria-label="Download Financial Requirements document">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v12m0 0 5-5m-5 5-5-5M4 17v3h16v-3" /></svg>
            </asp:HyperLink>
        </asp:Panel>
        <p><strong>Additional conditions</strong><br /><asp:Literal ID="AdditionalConditions" runat="server" /></p>
        <p><strong>Terms and conditions</strong><br /><asp:Literal ID="TermsAndConditions" runat="server" /></p>
    </section>
    <asp:Panel ID="GisLocationPanel" runat="server" CssClass="card" Visible="false">
        <h2>GIS location details</h2>
        <p><strong>Location</strong><br /><asp:Literal ID="GisLocationName" runat="server" /></p>
        <div class="row">
            <p><strong>Centroid latitude</strong><br /><asp:Literal ID="GisLatitude" runat="server" /></p>
            <p><strong>Centroid longitude</strong><br /><asp:Literal ID="GisLongitude" runat="server" /></p>
            <p><strong>Selected site area</strong><br /><asp:Literal ID="GisSelectedArea" runat="server" /></p>
        </div>
        <asp:HyperLink ID="GisImageLink" runat="server" Target="_blank" rel="noopener" Visible="false" />
        <asp:Image ID="GisImagePreview" runat="server" CssClass="gis-image-preview" Visible="false" AlternateText="Tender GIS location image" />
        <asp:Panel ID="GisBoundaryPanel" runat="server" Visible="false">
            <details>
                <summary>View selected boundary coordinates</summary>
                <pre class="gis-boundary-coordinates"><asp:Literal ID="GisBoundaryCoordinatesText" runat="server" /></pre>
            </details>
        </asp:Panel>
        <asp:Panel ID="GisBoundaryLinePanel" runat="server" Visible="false">
            <h3>Two-point boundary (A to B)</h3>
            <p><strong>Starting point A</strong>: <asp:Literal ID="GisBoundaryStartText" runat="server" /></p>
            <p><strong>Ending point B</strong>: <asp:Literal ID="GisBoundaryEndText" runat="server" /></p>
            <details><summary>View saved line geometry</summary>
                <pre class="gis-boundary-coordinates"><asp:Literal ID="GisBoundaryLineGeometryText" runat="server" /></pre>
            </details>
        </asp:Panel>
    </asp:Panel>
    <asp:Panel ID="ApplicationRequirementsPanel" runat="server" CssClass="card" Visible="false">
        <h2>Application document requirements</h2>
        <div class="table-wrap">
            <asp:GridView ID="ApplicationRequirementsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                EmptyDataText="No additional application documents are configured.">
                <Columns>
                    <asp:BoundField DataField="DocumentType" HeaderText="Document" />
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
    </asp:Panel>
    <section id="tender-boq-section" class="card">
        <h2>Bill of Quantities</h2>
        <div class="table-wrap">
            <asp:GridView ID="BoqGrid" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="No BOQ items added yet." OnRowCommand="BoqGrid_RowCommand">
                <Columns>
                    <asp:BoundField DataField="ItemName" HeaderText="Item" />
                    <asp:BoundField DataField="Description" HeaderText="Description" />
                    <asp:BoundField DataField="Unit" HeaderText="Unit" />
                    <asp:BoundField DataField="Quantity" HeaderText="Quantity" DataFormatString="{0:N3}" />
                    <asp:BoundField DataField="Rate" HeaderText="Rate" DataFormatString="₹{0:N2}" />
                    <asp:BoundField DataField="Amount" HeaderText="Amount" DataFormatString="₹{0:N2}" />
                    <asp:TemplateField><ItemTemplate><asp:LinkButton ID="DeleteItem" runat="server" Text="Remove" CommandName="RemoveItem" CommandArgument='<%# Eval("BOQId") %>' CssClass="button secondary" /></ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
        <asp:Panel ID="AddBoqPanel" runat="server" CssClass="card">
            <h3>Add BOQ item</h3>
            <div class="row">
                <div class="field"><label for="ItemName">Item name</label><asp:TextBox ID="ItemName" runat="server" MaxLength="200" /></div>
                <div class="field"><label for="ItemDescription">Description</label><asp:TextBox ID="ItemDescription" runat="server" MaxLength="500" /></div>
            </div>
            <div class="row">
                <div class="field"><label for="Quantity">Quantity</label><asp:TextBox ID="Quantity" runat="server" TextMode="Number" step="0.001" min="0.001" /></div>
                <div class="field"><label for="Unit">Unit</label><asp:TextBox ID="Unit" runat="server" MaxLength="20" /></div>
                <div class="field"><label for="Rate">Rate (₹)</label><asp:TextBox ID="Rate" runat="server" TextMode="Number" step="0.01" min="0.01" /></div>
                <asp:Button ID="AddBoqButton" runat="server" Text="Add item" OnClick="AddBoqButton_Click" />
            </div>
        </asp:Panel>
    </section>
    <section id="tender-documents-section" class="card tender-documents">
        <div class="page-heading">
            <div><h2>Tender documents</h2><p class="muted">Documents attached to this tender.</p></div>
            <asp:Panel ID="UploadPanel" runat="server" CssClass="document-management-actions" Visible="false">
                <asp:LinkButton ID="ShowDocumentFormButton" runat="server" CssClass="button document-upload-trigger"
                    OnClick="ShowDocumentFormButton_Click" CausesValidation="false" ToolTip="Upload a tender document"
                    aria-label="Upload a tender document">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 16V4m0 0L7 9m5-5 5 5M5 14v5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5" /></svg>
                    <span>Upload document</span>
                </asp:LinkButton>
                <asp:LinkButton ID="RefreshDocumentTypesButton" runat="server" CssClass="icon-action"
                    OnClick="RefreshDocumentTypesButton_Click" CausesValidation="false"
                    ToolTip="Refresh document types from Master Management" aria-label="Refresh document types">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 7v5h-5M4 17v-5h5m-4.1-3a8 8 0 0 1 13.2-2L20 12M4 12l1.9 5a8 8 0 0 0 13.2-2" /></svg>
                </asp:LinkButton>
            </asp:Panel>
        </div>
        <div class="table-wrap">
            <asp:GridView ID="DocumentsGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                DataKeyNames="DocumentId" EmptyDataText="No documents are associated with this tender yet."
                AllowPaging="true" PageSize="10" OnRowDataBound="DocumentsGrid_RowDataBound"
                OnPageIndexChanging="DocumentsGrid_PageIndexChanging" OnRowCommand="DocumentsGrid_RowCommand">
                <Columns>
                    <asp:TemplateField HeaderText="Sr. No."><ItemTemplate>
                        <asp:Literal ID="DocumentSerial" runat="server" />
                    </ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="DocumentType" HeaderText="Document type" />
                    <asp:BoundField DataField="DocumentName" HeaderText="Document name" />
                    <asp:BoundField DataField="FileName" HeaderText="File name" />
                    <asp:BoundField DataField="UploadedDate" HeaderText="Upload date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:BoundField DataField="UploadedBy" HeaderText="Uploaded by" />
                    <asp:BoundField DataField="VerificationStatus" HeaderText="Verification status" />
                    <asp:BoundField DataField="VerifiedBy" HeaderText="Verified by" />
                    <asp:BoundField DataField="VerifiedAtUtc" HeaderText="Verified at" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:TemplateField HeaderText="Status"><ItemTemplate>
                        <span class='badge <%# Convert.ToString(Eval("DocumentStatus")) == "Valid" ? "status-approved" : "status-rejected" %>'>
                            <%# HttpUtility.HtmlEncode(Convert.ToString(Eval("DocumentStatus"))) %>
                        </span>
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Actions" ItemStyle-CssClass="document-actions-cell">
                        <ItemTemplate>
                            <div class="document-icon-actions">
                                <asp:HyperLink runat="server" CssClass="icon-action"
                                    NavigateUrl='<%# ResolveUrl("~/Pages/DocumentDownload.aspx?id=" + Eval("DocumentId") + "&preview=true") %>'
                                    Target="_blank" rel="noopener" ToolTip="View document" aria-label="View document">
                                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2.5 12s3.2-6 9.5-6 9.5 6 9.5 6-3.2 6-9.5 6-9.5-6-9.5-6Z" /><circle cx="12" cy="12" r="2.5" /></svg>
                                </asp:HyperLink>
                                <asp:HyperLink runat="server" CssClass="icon-action"
                                    NavigateUrl='<%# ResolveUrl("~/Pages/DocumentDownload.aspx?id=" + Eval("DocumentId")) %>'
                                    ToolTip="Download document" aria-label="Download document">
                                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v12m0 0 5-5m-5 5-5-5M4 17v3h16v-3" /></svg>
                                </asp:HyperLink>
                                <asp:LinkButton runat="server" CssClass="icon-action" CommandName="EditDocument"
                                    CommandArgument='<%# Eval("DocumentId") %>' CausesValidation="false"
                                    ToolTip="Edit document details" aria-label="Edit document details">
                                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m15 5 4 4M4 20l4-.8L19 8a2.1 2.1 0 0 0-3-3L5 16l-1 4Z" /></svg>
                                </asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="icon-action" CommandName="ReplaceDocument"
                                    CommandArgument='<%# Eval("DocumentId") %>' CausesValidation="false"
                                    ToolTip="Re-upload document" aria-label="Re-upload document">
                                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 16V4m0 0L7 9m5-5 5 5M5 14v5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5" /></svg>
                                </asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="icon-action danger" CommandName="DeleteDocument"
                                    CommandArgument='<%# Eval("DocumentId") %>' CausesValidation="false"
                                    ToolTip="Delete document" aria-label="Delete document"
                                    OnClientClick="return confirm('Delete this document and its stored file? This cannot be undone.');">
                                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M10 11v6m4-6v6M6 7l1 14h10l1-14M9 7V4h6v3" /></svg>
                                </asp:LinkButton>
                            </div>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
        <asp:Panel ID="DocumentTypesMissingPanel" runat="server" CssClass="message" Visible="false">
            No active document types are configured. Add or activate types in <a href="AdminMasterData.aspx">Master Management</a>, then refresh the document types.
        </asp:Panel>
        <asp:Panel ID="DocumentFormPanel" runat="server" CssClass="document-editor" Visible="false">
            <asp:HiddenField ID="EditingDocumentId" runat="server" />
            <asp:HiddenField ID="ReplacingDocumentFile" runat="server" Value="false" />
            <div class="document-editor-heading">
                <h3><asp:Literal ID="DocumentFormTitle" runat="server">Upload tender document</asp:Literal></h3>
                <asp:LinkButton ID="CancelDocumentFormButton" runat="server" CssClass="icon-action"
                    OnClick="CancelDocumentFormButton_Click" CausesValidation="false"
                    ToolTip="Close document form" aria-label="Close document form">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18" /></svg>
                </asp:LinkButton>
            </div>
            <div class="row document-form-fields">
                <div class="field">
                    <asp:Label runat="server" AssociatedControlID="DocumentTypeSelect">Document type</asp:Label>
                    <asp:DropDownList ID="DocumentTypeSelect" runat="server" />
                    <asp:RequiredFieldValidator runat="server" ControlToValidate="DocumentTypeSelect"
                        InitialValue="" ValidationGroup="TenderDocument" CssClass="validation"
                        ErrorMessage="Select an active document type." />
                </div>
                <div class="field">
                    <asp:Label runat="server" AssociatedControlID="DocumentNameText">Document name</asp:Label>
                    <asp:TextBox ID="DocumentNameText" runat="server" MaxLength="250" />
                    <asp:RequiredFieldValidator runat="server" ControlToValidate="DocumentNameText"
                        ValidationGroup="TenderDocument" CssClass="validation" ErrorMessage="Document name is required." />
                </div>
                <asp:Panel ID="DocumentFilePanel" runat="server" CssClass="field document-file-field">
                    <asp:Label runat="server" AssociatedControlID="DocumentFile">Document file</asp:Label>
                    <asp:FileUpload ID="DocumentFile" runat="server" />
                    <span class="help">PDF, Office documents, JPEG, or PNG; maximum 10 MB per document.</span>
                </asp:Panel>
            </div>
            <div class="field">
                <asp:Label runat="server" AssociatedControlID="DocumentRemarksText">Remarks <span class="muted">(optional)</span></asp:Label>
                <asp:TextBox ID="DocumentRemarksText" runat="server" TextMode="MultiLine" Rows="2" MaxLength="1000" />
            </div>
            <div class="document-form-footer">
                <asp:Button ID="UploadButton" runat="server" Text="Save document" CssClass="button"
                    ValidationGroup="TenderDocument" OnClick="UploadButton_Click"
                    OnClientClick="return beginTenderDocumentUpload(this);" />
                <span id="TenderDocumentUploadStatus" class="upload-status" role="status" aria-live="polite" hidden="hidden">Uploading document…</span>
                <span class="help">A new document record is created for each upload.</span>
            </div>
        </asp:Panel>
    </section>
    <script type="text/javascript">
        function beginTenderDocumentUpload(button) {
            if (typeof Page_ClientValidate === "function" && !Page_ClientValidate("TenderDocument")) return false;
            if (button.getAttribute("data-submitting") === "true") return false;
            button.setAttribute("data-submitting", "true");
            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            var status = document.getElementById("TenderDocumentUploadStatus");
            if (status) status.hidden = false;
            return true;
        }
    </script>
    <asp:Panel ID="AuthenticatedWorkflowPanel" runat="server">
        <asp:Panel ID="RevisionHistoryPanel" runat="server" CssClass="card" Visible="false">
            <h2>Tender revision history</h2>
            <div class="table-wrap"><asp:GridView ID="RevisionHistoryGrid" runat="server" AutoGenerateColumns="false" GridLines="None"
                EmptyDataText="No revisions have been recorded.">
                <Columns>
                    <asp:BoundField DataField="RevisionNumber" HeaderText="Revision" />
                    <asp:BoundField DataField="ActionName" HeaderText="Action" />
                    <asp:BoundField DataField="RevisionReason" HeaderText="Notes" NullDisplayText="—" />
                    <asp:BoundField DataField="RevisedBy" HeaderText="Changed by" />
                    <asp:BoundField DataField="RevisedAtUtc" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                </Columns>
            </asp:GridView></div>
        </asp:Panel>
        <section class="card">
            <h2>Workflow action</h2>
            <asp:Panel ID="BudgetOpinionPanel" runat="server" CssClass="card" Visible="false">
                <h3>Financial budget opinion</h3>
                <p class="help">Choose positive to approve the amount, or negative to return the proposal for JE revision. Previous decisions remain in the history.</p>
                <div class="field"><label for="ApprovedBudgetAmount">Approved amount (₹) — required for a positive opinion</label><asp:TextBox ID="ApprovedBudgetAmount" runat="server" TextMode="Number" step="0.01" min="0.01" /></div>
            </asp:Panel>
            <div class="field"><label for="Remarks">Remarks (optional)</label><asp:TextBox ID="Remarks" runat="server" TextMode="MultiLine" MaxLength="1000" Rows="3" /></div>
            <div class="row">
                <asp:Button ID="SubmitWorkflowButton" runat="server" Text="Submit / Forward" OnClick="SubmitWorkflowButton_Click" />
                <asp:Button ID="ApproveButton" runat="server" Text="Approve" OnClick="ApproveButton_Click" />
                <asp:Button ID="RejectButton" runat="server" Text="Reject" CssClass="button danger" OnClick="RejectButton_Click" />
            </div>
            <p class="help">Workflow status changes are validated against the current status and the signed-in role before being saved.</p>
        </section>
        <asp:Panel ID="BudgetOpinionHistoryPanel" runat="server" CssClass="card">
            <h2>Financial budget-opinion history</h2>
            <div class="table-wrap"><asp:GridView ID="BudgetOpinionsGrid" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="No budget opinions have been recorded.">
                <Columns>
                    <asp:BoundField DataField="Opinion" HeaderText="Opinion" />
                    <asp:BoundField DataField="ApprovedAmount" HeaderText="Approved amount" DataFormatString="₹{0:N2}" NullDisplayText="—" />
                    <asp:BoundField DataField="ActionTakenBy" HeaderText="Approved by" />
                    <asp:BoundField DataField="OpinionAtUtc" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                    <asp:BoundField DataField="Remarks" HeaderText="Remarks" />
                </Columns>
            </asp:GridView></div>
        </asp:Panel>
        <section class="card">
            <div class="page-heading"><h2>Workflow history</h2>
                <asp:HyperLink ID="BidsLink" runat="server" CssClass="button secondary">Review bids</asp:HyperLink>
                <asp:HyperLink ID="WorkLink" runat="server" CssClass="button secondary">Work management</asp:HyperLink>
            </div>
            <div class="table-wrap"><asp:GridView ID="HistoryGrid" runat="server" AutoGenerateColumns="false" GridLines="None" EmptyDataText="No workflow actions have been recorded.">
                <Columns>
                    <asp:BoundField DataField="ApprovalStage" HeaderText="Stage" />
                    <asp:BoundField DataField="PreviousStatus" HeaderText="Previous status" />
                    <asp:BoundField DataField="NewStatus" HeaderText="New status" />
                    <asp:BoundField DataField="Status" HeaderText="Action" />
                    <asp:BoundField DataField="ActionTakenBy" HeaderText="Action taken by" />
                    <asp:BoundField DataField="RoleAtAction" HeaderText="Role" />
                    <asp:BoundField DataField="Remarks" HeaderText="Remarks" />
                    <asp:BoundField DataField="ApprovalDate" HeaderText="Date" DataFormatString="{0:dd MMM yyyy HH:mm}" />
                </Columns>
            </asp:GridView></div>
        </section>
    </asp:Panel>
</asp:Content>
