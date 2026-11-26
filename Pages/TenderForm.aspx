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
        <div class="tender-wizard-progress-scroll">
            <ol id="TenderWizardProgress" class="tender-wizard-progress" aria-label="Tender creation progress">
                <li data-wizard-nav="1"><button type="button" aria-current="step"><span>1</span><strong>Basic Information</strong><small>Not started</small></button></li>
                <li data-wizard-nav="2"><button type="button"><span>2</span><strong>Important Dates</strong><small>Not started</small></button></li>
                <li data-wizard-nav="3" data-je-only="true"><button type="button"><span>3</span><strong>Approvals</strong><small>Not started</small></button></li>
                <li data-wizard-nav="4"><button type="button"><span>4</span><strong>GIS Location</strong><small>Not started</small></button></li>
                <li data-wizard-nav="5"><button type="button"><span>5</span><strong>Review & Submit</strong><small>Not started</small></button></li>
            </ol>
        </div>
        <asp:HiddenField ID="WizardCurrentStep" runat="server" Value="1" />
        <asp:HiddenField ID="WizardCompletedStepsMask" runat="server" Value="0" />
        <asp:HiddenField ID="RemoveGisImageFlag" runat="server" Value="false" />
        <asp:HiddenField ID="JeAdministratorFlag" runat="server" />
        <asp:HiddenField ID="GisBoundaryCoordinates" runat="server" />
        <p id="TenderWizardNavigationMessage" class="message" role="alert" aria-live="assertive" hidden="hidden"></p>
        <div class="tender-wizard-panel" data-wizard-step="1">
        <h2>Basic tender information</h2>
        <p class="muted">The tender reference number is assigned automatically when the draft is saved.</p>
        <div class="field"><label for="TitleText">Project title</label><asp:TextBox ID="TitleText" runat="server" MaxLength="250" />
            <asp:RequiredFieldValidator ID="TitleRequired" runat="server" ControlToValidate="TitleText" ErrorMessage="Title is required." CssClass="validation" />
            <asp:CustomValidator ID="TitleLength" runat="server" ControlToValidate="TitleText" OnServerValidate="TitleLength_ServerValidate" ErrorMessage="Title must be 5 to 250 characters." CssClass="validation" /></div>
        <div class="field"><label for="DescriptionText">Project description</label><asp:TextBox ID="DescriptionText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
        <div class="row">
            <asp:Panel ID="DepartmentPanel" runat="server" CssClass="field"><label for="DepartmentSelect">Department</label><asp:DropDownList ID="DepartmentSelect" runat="server" />
                <asp:RequiredFieldValidator ID="DepartmentRequired" runat="server" ControlToValidate="DepartmentSelect" InitialValue="" ErrorMessage="A valid Department must be selected." CssClass="validation" /></asp:Panel>
            <asp:Panel ID="DepartmentReadOnlyPanel" runat="server" CssClass="field" Visible="false">
                <span class="field-label">Department</span><strong><span id="DepartmentReadOnlyName"><asp:Literal ID="DepartmentReadOnlyText" runat="server" /></span></strong>
            </asp:Panel>
            <div class="field"><label for="TenderTypeSelect">Tender method</label><asp:DropDownList ID="TenderTypeSelect" runat="server" />
                <asp:RequiredFieldValidator ID="TenderTypeRequired" runat="server" ControlToValidate="TenderTypeSelect" InitialValue="" ErrorMessage="Tender method is required." CssClass="validation" /></div>
            <div class="field"><label for="TenderCategorySelect">Tender category</label><asp:DropDownList ID="TenderCategorySelect" runat="server" />
                <asp:RequiredFieldValidator ID="TenderCategoryRequired" runat="server" ControlToValidate="TenderCategorySelect" InitialValue="" ErrorMessage="Tender category is required." CssClass="validation" /></div>
        </div>
        <div class="row">
            <div class="field"><label for="FinancialYearSelect">Budgeting Financial Year</label><asp:DropDownList ID="FinancialYearSelect" runat="server" />
                <asp:RequiredFieldValidator ID="FinancialYearRequired" runat="server" ControlToValidate="FinancialYearSelect" InitialValue="" ErrorMessage="Budgeting Financial Year is required." CssClass="validation" /></div>
            <div class="field"><label for="AmountText">Estimated tender value (₹)</label><asp:TextBox ID="AmountText" runat="server" TextMode="Number" step="0.01" min="0.01" />
                <asp:RequiredFieldValidator ID="AmountRequired" runat="server" ControlToValidate="AmountText" ErrorMessage="Estimated amount is required." CssClass="validation" />
                <asp:RangeValidator ID="AmountRange" runat="server" ControlToValidate="AmountText" Type="Double" MinimumValue="0.01" MaximumValue="999999999999999" ErrorMessage="Estimated amount must be greater than zero." CssClass="validation" /></div>
        </div>
        <asp:Panel ID="FinancialYearMissingPanel" runat="server" CssClass="message" Visible="false">
            No active Budgeting Financial Years are configured. <a href="AdminMasterData.aspx">Add a Budgeting Financial Year in Master Management</a>.
        </asp:Panel>
        <div class="field requirement-document">
            <asp:Label runat="server" AssociatedControlID="ScopeOfWorkDocumentUpload">Scope of Work document</asp:Label>
            <asp:FileUpload ID="ScopeOfWorkDocumentUpload" runat="server" ClientIDMode="Static" CssClass="requirement-document-input visually-hidden"
                accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png" />
            <label class="icon-action file-upload-action" for="ScopeOfWorkDocumentUpload"
                title="Upload or replace Scope of Work document" aria-label="Upload or replace Scope of Work document">
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 16V4m0 0L7 9m5-5 5 5M4 16v4h16v-4" /></svg>
            </label>
            <span id="ScopeOfWorkSelectedFileName" class="help">No new file selected.</span>
            <asp:Panel ID="ScopeOfWorkCurrentDocumentPanel" runat="server" CssClass="requirement-current-document" Visible="false">
                <asp:Label ID="ScopeOfWorkCurrentFileName" runat="server" CssClass="requirement-current-file-name" />
                <asp:HyperLink ID="ScopeOfWorkViewLink" runat="server" CssClass="icon-action" Target="_blank" rel="noopener"
                    ToolTip="View Scope of Work document" aria-label="View Scope of Work document">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" /><circle cx="12" cy="12" r="3" /></svg>
                </asp:HyperLink>
                <asp:HyperLink ID="ScopeOfWorkDownloadLink" runat="server" CssClass="icon-action"
                    ToolTip="Download Scope of Work document" aria-label="Download Scope of Work document">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4v12m0 0 5-5m-5 5-5-5M4 16v4h16v-4" /></svg>
                </asp:HyperLink>
                <button type="button" class="icon-action danger" data-remove-flag="ScopeOfWorkDocumentRemoveFlag"
                    onclick="markRequirementDocumentForRemoval(this);" title="Remove Scope of Work document"
                    aria-label="Remove Scope of Work document">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M9 7V4h6v3m3 0-1 13H7L6 7m4 4v5m4-5v5" /></svg>
                </button>
            </asp:Panel>
            <asp:HiddenField ID="ScopeOfWorkDocumentRemoveFlag" runat="server" Value="false" />
            <span class="help">PDF, Office documents, JPEG or PNG; maximum 20 MB.</span>
        </div>
        <div class="row">
            <div class="field requirement-document">
                <asp:Label runat="server" AssociatedControlID="TechnicalRequirementsDocumentUpload">Technical Requirements document</asp:Label>
                <asp:FileUpload ID="TechnicalRequirementsDocumentUpload" runat="server" ClientIDMode="Static" CssClass="requirement-document-input visually-hidden"
                    accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png" />
                <label class="icon-action file-upload-action" for="TechnicalRequirementsDocumentUpload"
                    title="Upload or replace Technical Requirements document" aria-label="Upload or replace Technical Requirements document">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 16V4m0 0L7 9m5-5 5 5M4 16v4h16v-4" /></svg>
                </label>
                <span id="TechnicalRequirementsSelectedFileName" class="help">No new file selected.</span>
                <asp:Panel ID="TechnicalRequirementsCurrentDocumentPanel" runat="server" CssClass="requirement-current-document" Visible="false">
                    <asp:Label ID="TechnicalRequirementsCurrentFileName" runat="server" CssClass="requirement-current-file-name" />
                    <asp:HyperLink ID="TechnicalRequirementsViewLink" runat="server" CssClass="icon-action" Target="_blank" rel="noopener"
                        ToolTip="View Technical Requirements document" aria-label="View Technical Requirements document">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" /><circle cx="12" cy="12" r="3" /></svg>
                    </asp:HyperLink>
                    <asp:HyperLink ID="TechnicalRequirementsDownloadLink" runat="server" CssClass="icon-action"
                        ToolTip="Download Technical Requirements document" aria-label="Download Technical Requirements document">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4v12m0 0 5-5m-5 5-5-5M4 16v4h16v-4" /></svg>
                    </asp:HyperLink>
                    <button type="button" class="icon-action danger" data-remove-flag="TechnicalRequirementsDocumentRemoveFlag"
                        onclick="markRequirementDocumentForRemoval(this);" title="Remove Technical Requirements document"
                        aria-label="Remove Technical Requirements document">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M9 7V4h6v3m3 0-1 13H7L6 7m4 4v5m4-5v5" /></svg>
                    </button>
                </asp:Panel>
                <asp:HiddenField ID="TechnicalRequirementsDocumentRemoveFlag" runat="server" Value="false" />
                <span class="help">PDF, Office documents, JPEG or PNG; maximum 20 MB.</span>
            </div>
            <div class="field requirement-document">
                <asp:Label runat="server" AssociatedControlID="FinancialRequirementsDocumentUpload">Financial Requirements document</asp:Label>
                <asp:FileUpload ID="FinancialRequirementsDocumentUpload" runat="server" ClientIDMode="Static" CssClass="requirement-document-input visually-hidden"
                    accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png" />
                <label class="icon-action file-upload-action" for="FinancialRequirementsDocumentUpload"
                    title="Upload or replace Financial Requirements document" aria-label="Upload or replace Financial Requirements document">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 16V4m0 0L7 9m5-5 5 5M4 16v4h16v-4" /></svg>
                </label>
                <span id="FinancialRequirementsSelectedFileName" class="help">No new file selected.</span>
                <asp:Panel ID="FinancialRequirementsCurrentDocumentPanel" runat="server" CssClass="requirement-current-document" Visible="false">
                    <asp:Label ID="FinancialRequirementsCurrentFileName" runat="server" CssClass="requirement-current-file-name" />
                    <asp:HyperLink ID="FinancialRequirementsViewLink" runat="server" CssClass="icon-action" Target="_blank" rel="noopener"
                        ToolTip="View Financial Requirements document" aria-label="View Financial Requirements document">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7z" /><circle cx="12" cy="12" r="3" /></svg>
                    </asp:HyperLink>
                    <asp:HyperLink ID="FinancialRequirementsDownloadLink" runat="server" CssClass="icon-action"
                        ToolTip="Download Financial Requirements document" aria-label="Download Financial Requirements document">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4v12m0 0 5-5m-5 5-5-5M4 16v4h16v-4" /></svg>
                    </asp:HyperLink>
                    <button type="button" class="icon-action danger" data-remove-flag="FinancialRequirementsDocumentRemoveFlag"
                        onclick="markRequirementDocumentForRemoval(this);" title="Remove Financial Requirements document"
                        aria-label="Remove Financial Requirements document">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M9 7V4h6v3m3 0-1 13H7L6 7m4 4v5m4-5v5" /></svg>
                    </button>
                </asp:Panel>
                <asp:HiddenField ID="FinancialRequirementsDocumentRemoveFlag" runat="server" Value="false" />
                <span class="help">PDF, Office documents, JPEG or PNG; maximum 20 MB.</span>
            </div>
        </div>
        <div class="row">
            <div class="field"><label for="AdditionalConditionsText">Additional conditions</label><asp:TextBox ID="AdditionalConditionsText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
            <div class="field"><label for="TermsAndConditionsText">Terms and conditions</label><asp:TextBox ID="TermsAndConditionsText" runat="server" TextMode="MultiLine" Rows="3" MaxLength="4000" /></div>
        </div>
        </div>

        <div class="tender-wizard-panel" data-wizard-step="2" hidden="hidden">
        <h2>Tender schedule dates (IST)</h2>
        <p class="muted">Enter dates only; times are not collected. Dates are optional, but submission start and closing must be entered together. When provided, follow this order: publication, submission start, submission closing, technical bid opening, then financial bid opening. A pre-bid meeting, if applicable, must be on or after publication and before closing.</p>
        <asp:Panel ID="ScheduleValidationPanel" runat="server" CssClass="message schedule-validation" Visible="false">
            <asp:Literal ID="ScheduleValidationMessage" runat="server" />
        </asp:Panel>
        <div class="row">
            <div class="field"><label for="PublicationDateText">Planned publication date</label><asp:TextBox ID="PublicationDateText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="StartText">Submission start date</label><asp:TextBox ID="StartText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="DeadlineText">Submission closing date</label><asp:TextBox ID="DeadlineText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="TechnicalBidOpeningText">Technical bid opening date</label><asp:TextBox ID="TechnicalBidOpeningText" runat="server" TextMode="Date" /></div>
            <div class="field"><label for="FinancialBidOpeningText">Financial bid opening date</label><asp:TextBox ID="FinancialBidOpeningText" runat="server" TextMode="Date" /></div>
        </div>

        <details class="tender-form-optional">
            <summary>Pre-bid meeting details</summary>
            <div class="row">
                <div class="field"><label for="PreBidMeetingText">Pre-bid meeting date</label><asp:TextBox ID="PreBidMeetingText" runat="server" TextMode="Date" /></div>
                <div class="field"><label for="PreBidLocationText">Pre-bid location</label><asp:TextBox ID="PreBidLocationText" runat="server" MaxLength="300" /></div>
            </div>
            <div class="field"><label for="PreBidDetailsText">Meeting details</label><asp:TextBox ID="PreBidDetailsText" runat="server" TextMode="MultiLine" Rows="2" MaxLength="2000" /></div>
        </details>
        </div>

        <asp:Panel ID="ApprovalsWizardPanel" runat="server" CssClass="tender-wizard-panel" data-wizard-step="3" data-je-only="true" Visible="false">
            <h2>Administrative approval details</h2>
            <p class="message">These checkboxes record JE's administrative status only. They are not a substitute for official approval documents or competent-authority approval.</p>
            <div class="approval-wizard-grid">
                <section class="approval-wizard-item">
                    <div class="approval-wizard-check"><asp:CheckBox ID="AdministrativeApprovalCheck" runat="server" Text="Administrative Approval" /></div>
                    <asp:TextBox ID="AdministrativeApprovalReferenceText" runat="server" MaxLength="100" placeholder="Approval reference number" />
                    <asp:TextBox ID="AdministrativeApprovalDateText" runat="server" TextMode="Date" />
                    <asp:FileUpload ID="AdministrativeApprovalDocumentUpload" runat="server" />
                    <asp:HyperLink ID="AdministrativeApprovalDocumentLink" runat="server" Visible="false" Target="_blank" rel="noopener">View current approval document</asp:HyperLink>
                </section>
                <section class="approval-wizard-item">
                    <div class="approval-wizard-check"><asp:CheckBox ID="TechnicalSanctionApprovalCheck" runat="server" Text="Technical Sanction (TS) Approval" /></div>
                    <asp:TextBox ID="TechnicalSanctionApprovalReferenceText" runat="server" MaxLength="100" placeholder="Approval reference number" />
                    <asp:TextBox ID="TechnicalSanctionApprovalDateText" runat="server" TextMode="Date" />
                    <asp:FileUpload ID="TechnicalSanctionApprovalDocumentUpload" runat="server" />
                    <asp:HyperLink ID="TechnicalSanctionApprovalDocumentLink" runat="server" Visible="false" Target="_blank" rel="noopener">View current approval document</asp:HyperLink>
                </section>
                <section class="approval-wizard-item">
                    <div class="approval-wizard-check"><asp:CheckBox ID="GeneralBodyApprovalCheck" runat="server" Text="General Body (GB) Approval" /></div>
                    <asp:TextBox ID="GeneralBodyApprovalReferenceText" runat="server" MaxLength="100" placeholder="Approval reference number" />
                    <asp:TextBox ID="GeneralBodyApprovalDateText" runat="server" TextMode="Date" />
                    <asp:FileUpload ID="GeneralBodyApprovalDocumentUpload" runat="server" />
                    <asp:HyperLink ID="GeneralBodyApprovalDocumentLink" runat="server" Visible="false" Target="_blank" rel="noopener">View current approval document</asp:HyperLink>
                </section>
                <section class="approval-wizard-item">
                    <div class="approval-wizard-check"><asp:CheckBox ID="DpdcApprovalCheck" runat="server" Text="DPDC Approval" /></div>
                    <asp:TextBox ID="DpdcApprovalReferenceText" runat="server" MaxLength="100" placeholder="Approval reference number" />
                    <asp:TextBox ID="DpdcApprovalDateText" runat="server" TextMode="Date" />
                    <asp:FileUpload ID="DpdcApprovalDocumentUpload" runat="server" />
                    <asp:HyperLink ID="DpdcApprovalDocumentLink" runat="server" Visible="false" Target="_blank" rel="noopener">View current approval document</asp:HyperLink>
                </section>
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
        </asp:Panel>

        <div class="tender-wizard-panel" data-wizard-step="4" hidden="hidden">
            <h2>GIS location details</h2>
            <p class="muted">Draw the site boundary with your cursor. The portal calculates its area and centroid, then captures a map image of the selected location. You can upload a site image to replace the map image.</p>
            <div class="row">
                <div class="field"><label for="LatitudeText">Latitude</label><asp:TextBox ID="LatitudeText" runat="server" TextMode="Number" step="0.000001" min="-90" max="90" />
                    <span class="help">From -90 to 90</span></div>
                <div class="field"><label for="LongitudeText">Longitude</label><asp:TextBox ID="LongitudeText" runat="server" TextMode="Number" step="0.000001" min="-180" max="180" />
                    <span class="help">From -180 to 180</span></div>
                <div class="field"><label for="LocationNameText">Location name / site address</label><asp:TextBox ID="LocationNameText" runat="server" MaxLength="300" /></div>
            </div>
            <div class="gis-map-tools" role="group" aria-label="GIS boundary drawing controls">
                <button type="button" class="button" onclick="startGisAreaDrawing();" aria-label="Start drawing the GIS boundary">Draw boundary</button>
                <button type="button" class="button secondary" onclick="undoGisAreaPoint();" aria-label="Undo the last boundary point">Undo point</button>
                <button type="button" class="button secondary" onclick="finishGisAreaDrawing();" aria-label="Finish the GIS boundary">Finish selection</button>
                <button type="button" class="button secondary" onclick="clearGisArea();" aria-label="Clear the GIS boundary">Clear boundary</button>
            </div>
            <div id="TenderLocationMap" class="tender-location-map" role="application" aria-label="Map location picker"></div>
            <p id="TenderMapStatus" class="help" role="status" aria-live="polite">Select Draw boundary, click at least three points around the site, then choose Finish selection.</p>
            <p id="TenderGisAreaSummary" class="gis-area-summary" role="status" aria-live="polite" hidden="hidden"></p>
            <div class="field gis-image-field">
                <label for="GisImageUpload">GIS / site image</label>
                <div class="gis-image-actions">
                    <asp:FileUpload ID="GisImageUpload" runat="server" accept=".jpg,.jpeg,.png,image/jpeg,image/png" />
                    <asp:Panel ID="ExistingGisImageActions" runat="server" CssClass="gis-image-actions" Visible="false">
                        <asp:HyperLink ID="CurrentGisImageLink" runat="server" Target="_blank" rel="noopener"
                            CssClass="icon-action" ToolTip="View current GIS image" aria-label="View current GIS image">
                            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2.5 12s3.2-6 9.5-6 9.5 6 9.5 6-3.2 6-9.5 6-9.5-6-9.5-6Z" /><circle cx="12" cy="12" r="2.5" /></svg>
                        </asp:HyperLink>
                        <button type="button" class="icon-action" title="Remove current GIS image" aria-label="Remove current GIS image"
                            onclick="markGisImageForRemoval();">
                            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M10 11v6m4-6v6M6 7l1 14h10l1-14M9 7V4h6v3" /></svg>
                        </button>
                    </asp:Panel>
                </div>
                <span class="help">JPEG or PNG only; maximum 10 MB. Selecting a replacement preserves the previous image until save succeeds.</span>
                <asp:Image ID="CurrentGisImagePreview" runat="server" CssClass="gis-image-preview" Visible="false" AlternateText="Current GIS site image" />
                <img id="GisImagePreview" class="gis-image-preview" alt="Selected GIS image preview" hidden="hidden" />
            </div>
        </div>

        <div class="tender-wizard-panel" data-wizard-step="5" hidden="hidden">
            <h2>Review and submit</h2>
            <p class="muted">Check the saved values before you continue. Use Edit to return to a section.</p>
            <div id="TenderReviewSummary" class="tender-review-summary"></div>
            <progress id="RequirementDocumentUploadProgress" max="100" hidden="hidden"></progress>
            <p id="RequirementDocumentUploadStatus" class="upload-status" role="status" aria-live="polite" hidden="hidden"></p>
            <div class="tender-wizard-actions">
                <button type="button" class="button secondary" onclick="showTenderWizardStep(1);">
                    Edit details
                </button>
                <asp:Button ID="CreateButton" runat="server" Text="Save as Draft" CssClass="button"
                    OnClick="CreateButton_Click" OnClientClick="return prepareTenderWizardSubmit(this, false);" />
                <asp:Button ID="SubmitTenderButton" runat="server" Text="Submit for processing" CssClass="button"
                    OnClick="CreateButton_Click" OnClientClick="return prepareTenderWizardSubmit(this, true);" />
            </div>
        </div>
        <nav class="tender-wizard-controls" aria-label="Tender form navigation">
            <button type="button" id="TenderWizardPrevious" class="button secondary" onclick="moveTenderWizard(-1);" disabled="disabled">
                Previous
            </button>
            <button type="button" id="TenderWizardNext" class="button" onclick="moveTenderWizard(1);">
                Next
            </button>
        </nav>
    </section>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <script type="text/javascript">
        (function () {
            var wizard = document.querySelector(".tender-form");
            if (!wizard) return;
            var stepField = wizard.querySelector("input[id$='WizardCurrentStep']");
            var completedField = wizard.querySelector("input[id$='WizardCompletedStepsMask']");
            var jeAdministratorField = wizard.querySelector("input[id$='JeAdministratorFlag']");
            var jeOnly = jeAdministratorField && jeAdministratorField.value === "true";
            var panels = Array.prototype.slice.call(wizard.querySelectorAll("[data-wizard-step]"))
                .filter(function (panel) { return panel.getAttribute("data-je-only") !== "true" || jeOnly; });
            var steps = panels.map(function (panel) { return panel.getAttribute("data-wizard-step"); });
            var tenderMap = null;
            var current = stepField ? stepField.value : "1";
            if (steps.indexOf(current) < 0) current = steps[0] || "1";

            function getPanel(step) {
                for (var i = 0; i < panels.length; i++) {
                    if (panels[i].getAttribute("data-wizard-step") === step) return panels[i];
                }
                return null;
            }

            function setMask(step, complete) {
                var mask = parseInt(completedField.value || "0", 10) || 0;
                var bit = 1 << (parseInt(step, 10) - 1);
                completedField.value = String(complete ? (mask | bit) : (mask & ~bit));
            }

            function isMarkedComplete(step) {
                var mask = parseInt(completedField.value || "0", 10) || 0;
                return (mask & (1 << (parseInt(step, 10) - 1))) !== 0;
            }

            function stepIsComplete(step) {
                var panel = getPanel(step);
                if (!panel) return false;
                if (step === "1") {
                    var title = panel.querySelector("[id$='TitleText']");
                    var method = panel.querySelector("[id$='TenderTypeSelect']");
                    var category = panel.querySelector("[id$='TenderCategorySelect']");
                    var department = panel.querySelector("[id$='DepartmentSelect']");
                    var year = panel.querySelector("[id$='FinancialYearSelect']");
                    var amount = panel.querySelector("[id$='AmountText']");
                    return title && title.value.trim().length >= 5 && method && method.value &&
                        category && category.value && year && year.value && amount &&
                        amount.value !== "" && Number(amount.value) > 0 &&
                        (!department || !department.offsetParent || department.value);
                }
                if (step === "2") {
                    return !getScheduleValidationMessage();
                }
                if (step === "4") {
                    var latitude = panel.querySelector("[id$='LatitudeText']");
                    var longitude = panel.querySelector("[id$='LongitudeText']");
                    if (!latitude || !longitude) return true;
                    if ((latitude.value === "") !== (longitude.value === "")) return false;
                    if (latitude.value === "") return true;
                    return latitude.checkValidity() && longitude.checkValidity() &&
                        isFinite(Number(latitude.value)) && Number(latitude.value) >= -90 &&
                        Number(latitude.value) <= 90 && isFinite(Number(longitude.value)) &&
                        Number(longitude.value) >= -180 && Number(longitude.value) <= 180;
                }
                return true;
            }

            function setNavigationMessage(message) {
                var status = document.getElementById("TenderWizardNavigationMessage");
                if (!status) return;
                status.textContent = message || "";
                status.hidden = !message;
            }

            function getScheduleValidationMessage() {
                var publication = wizard.querySelector("[id$='PublicationDateText']").value;
                var submissionStart = wizard.querySelector("[id$='StartText']").value;
                var submissionClosing = wizard.querySelector("[id$='DeadlineText']").value;
                var technicalOpening = wizard.querySelector("[id$='TechnicalBidOpeningText']").value;
                var financialOpening = wizard.querySelector("[id$='FinancialBidOpeningText']").value;
                var preBid = wizard.querySelector("[id$='PreBidMeetingText']").value;
                if (!!submissionStart !== !!submissionClosing)
                    return "Enter both the submission start date and submission closing date.";
                if (publication && submissionStart && publication >= submissionStart)
                    return "Set the publication date before the submission start date.";
                if (submissionStart && submissionClosing && submissionClosing <= submissionStart)
                    return "Set the submission closing date after the submission start date.";
                if (technicalOpening && (!submissionClosing || technicalOpening <= submissionClosing))
                    return "Set the technical bid opening date after the submission closing date.";
                if (financialOpening && (!technicalOpening || financialOpening <= technicalOpening))
                    return "Set the financial bid opening date after the technical bid opening date.";
                if (preBid && submissionClosing && preBid >= submissionClosing)
                    return "Set the pre-bid meeting date before the submission closing date.";
                if (preBid && publication && preBid < publication)
                    return "Set the pre-bid meeting date on or after the publication date.";
                return "";
            }

            function navigateToStep(step) {
                var targetIndex = steps.indexOf(String(step));
                var currentIndex = steps.indexOf(current);
                if (targetIndex < 0) return;
                if (targetIndex > currentIndex) {
                    for (var i = currentIndex; i < targetIndex; i++) {
                        if (!stepIsComplete(steps[i])) {
                            var blockedPanel = getPanel(steps[i]);
                            var message = steps[i] === "1"
                                ? "Complete the required basic tender information before continuing."
                                : (steps[i] === "2"
                                    ? getScheduleValidationMessage()
                                    : (steps[i] === "4"
                                        ? "Enter both valid coordinates, or leave both blank, before continuing."
                                        : "Complete the current step before continuing."));
                            setNavigationMessage(message);
                            current = steps[i];
                            render();
                            if (blockedPanel) wizard.scrollIntoView({ behavior: "smooth", block: "start" });
                            return;
                        }
                        setMask(steps[i], true);
                    }
                } else if (stepIsComplete(current)) {
                    setMask(current, true);
                }
                setNavigationMessage("");
                current = String(step);
                render();
                wizard.scrollIntoView({ behavior: "smooth", block: "start" });
            }

            function render() {
                panels.forEach(function (panel) {
                    panel.hidden = panel.getAttribute("data-wizard-step") !== current;
                });
                if (stepField) stepField.value = current;
                var allNavItems = Array.prototype.slice.call(wizard.querySelectorAll("[data-wizard-nav]"));
                allNavItems.forEach(function (item) {
                    item.hidden = item.getAttribute("data-je-only") === "true" && !jeOnly;
                });
                var navItems = allNavItems.filter(function (item) { return !item.hidden; });
                var activeIndex = Math.max(0, steps.indexOf(current));
                var progressInset = 50 / navItems.length;
                var completedCount = 0;
                for (var completedStepIndex = 0; completedStepIndex < steps.length; completedStepIndex++) {
                    var completedStep = steps[completedStepIndex];
                    if (completedStep === current ||
                        !isMarkedComplete(completedStep) || !stepIsComplete(completedStep)) break;
                    completedCount++;
                }
                var progressBar = document.getElementById("TenderWizardProgress");
                if (progressBar) {
                    progressBar.style.setProperty("--progress-inset", progressInset + "%");
                    progressBar.style.setProperty("--progress-columns", navItems.length);
                    progressBar.style.setProperty("--progress-width",
                        (navItems.length > 1 ? Math.max(0, completedCount - 1) / (navItems.length - 1) *
                            (100 - 2 * progressInset) : 0) + "%");
                    progressBar.setAttribute("aria-label",
                        "Tender creation progress, step " + (activeIndex + 1) + " of " + navItems.length);
                }
                navItems.forEach(function (item, index) {
                    var step = item.getAttribute("data-wizard-nav");
                    var button = item.querySelector("button");
                    button.querySelector("span").textContent = String(index + 1);
                    var label = item.querySelector("small");
                    var complete = index < completedCount;
                    item.classList.toggle("is-active", step === current);
                    item.classList.toggle("is-complete", complete);
                    if (step === current) button.setAttribute("aria-current", "step");
                    else button.removeAttribute("aria-current");
                    label.textContent = complete ? "Complete" : (step === current ? "In progress" : "Not started");
                    button.disabled = step === current;
                    button.onclick = function () { navigateToStep(step); };
                });
                var previous = document.getElementById("TenderWizardPrevious");
                var next = document.getElementById("TenderWizardNext");
                var atFirst = steps.indexOf(current) === 0;
                var atLast = steps.indexOf(current) === steps.length - 1;
                previous.disabled = atFirst;
                next.hidden = atLast;
                var review = document.getElementById("TenderReviewSummary");
                if (review) renderTenderReviewSummary(review);
                if (current === "4" && tenderMap)
                    window.setTimeout(function () { tenderMap.invalidateSize(); }, 0);
            }

            window.showTenderWizardStep = function (step) {
                navigateToStep(step);
            };
            window.moveTenderWizard = function (direction) {
                var index = steps.indexOf(current);
                var target = Math.max(0, Math.min(steps.length - 1, index + direction));
                navigateToStep(steps[target]);
            };
            window.prepareTenderWizardSubmit = function (button, submit) {
                if (!stepIsComplete("1") || !stepIsComplete("2")) {
                    setNavigationMessage(!stepIsComplete("1")
                        ? "Complete the required basic tender information before saving."
                        : getScheduleValidationMessage());
                    var incompleteStep = !stepIsComplete("1") ? "1" : "2";
                    current = incompleteStep;
                    render();
                    wizard.scrollIntoView({ behavior: "smooth", block: "start" });
                    return false;
                }
                if (stepField) stepField.value = "5";
                button.setAttribute("aria-busy", "true");
                var uploadProgress = document.getElementById("RequirementDocumentUploadProgress");
                var uploadStatus = document.getElementById("RequirementDocumentUploadStatus");
                if (uploadProgress) {
                    uploadProgress.hidden = false;
                    uploadProgress.removeAttribute("value");
                }
                if (uploadStatus) {
                    uploadStatus.hidden = false;
                    uploadStatus.textContent = "Saving the tender and selected documents. Please wait.";
                }
                return true;
            };
            wizard.addEventListener("input", function () {
                setMask(current, stepIsComplete(current));
                render();
            });
            wizard.addEventListener("change", function () {
                setMask(current, stepIsComplete(current));
                render();
            });
            render();

            [
                ["ScopeOfWorkDocumentUpload", "ScopeOfWorkSelectedFileName"],
                ["TechnicalRequirementsDocumentUpload", "TechnicalRequirementsSelectedFileName"],
                ["FinancialRequirementsDocumentUpload", "FinancialRequirementsSelectedFileName"]
            ].forEach(function (entry) {
                var input = wizard.querySelector("[id$='" + entry[0] + "']");
                var name = document.getElementById(entry[1]);
                if (input && name) input.addEventListener("change", function () {
                    var flag = wizard.querySelector("input[id$='" + entry[0].replace("Upload", "RemoveFlag") + "']");
                    if (flag) flag.value = "false";
                    var currentDocument = input.parentNode.querySelector(".requirement-current-document");
                    if (currentDocument) currentDocument.classList.remove("is-marked-for-removal");
                    name.textContent = input.files && input.files.length
                        ? "Selected: " + input.files[0].name : "No new file selected.";
                });
            });
            window.markRequirementDocumentForRemoval = function (button) {
                if (!window.confirm("Remove this requirement document when the tender is saved?")) return;
                var flag = wizard.querySelector("input[id$='" + button.getAttribute("data-remove-flag") + "']");
                if (flag) flag.value = "true";
                var currentDocument = button.closest(".requirement-current-document");
                if (currentDocument) currentDocument.classList.add("is-marked-for-removal");
            };

            var latitude = wizard.querySelector("[id$='LatitudeText']");
            var longitude = wizard.querySelector("[id$='LongitudeText']");
            var boundaryField = wizard.querySelector("input[id$='GisBoundaryCoordinates']");
            var imageInput = wizard.querySelector("[id$='GisImageUpload']");
            var preview = document.getElementById("GisImagePreview");
            var mapStatus = document.getElementById("TenderMapStatus");
            var areaSummary = document.getElementById("TenderGisAreaSummary");
            var selectedBoundary = [];
            var draftBoundary = [];
            var boundaryLayer = null;
            var draftLayer = null;
            var drawingArea = false;
            var boundaryAreaSquareMeters = null;
            var generatedGisMapImage = false;
            var attachingGeneratedGisMapImage = false;
            var gisImageCaptureSequence = 0;
            var gisImagePreviewUrl = null;
            if (imageInput && preview) imageInput.addEventListener("change", function () {
                var file = imageInput.files && imageInput.files[0];
                if (!file) return;
                if (!/^image\/(jpeg|png)$/i.test(file.type) || file.size > 10 * 1024 * 1024) {
                    generatedGisMapImage = false;
                    gisImageCaptureSequence++;
                    imageInput.value = "";
                    if (gisImagePreviewUrl) URL.revokeObjectURL(gisImagePreviewUrl);
                    gisImagePreviewUrl = null;
                    preview.removeAttribute("src");
                    preview.hidden = true;
                    window.alert("Choose a JPEG or PNG image no larger than 10 MB.");
                    return;
                }
                if (!attachingGeneratedGisMapImage) {
                    generatedGisMapImage = false;
                    gisImageCaptureSequence++;
                }
                if (gisImagePreviewUrl) URL.revokeObjectURL(gisImagePreviewUrl);
                gisImagePreviewUrl = URL.createObjectURL(file);
                preview.src = gisImagePreviewUrl;
                preview.hidden = false;
            });
            window.markGisImageForRemoval = function () {
                var flag = wizard.querySelector("input[id$='RemoveGisImageFlag']");
                if (flag) flag.value = "true";
                var currentImage = wizard.querySelector("[id$='CurrentGisImageLink']");
                if (currentImage) currentImage.hidden = true;
            };
            if (window.L && latitude && longitude) {
                var initialLat = parseFloat(latitude.value);
                var initialLng = parseFloat(longitude.value);
                var hasLocation = isFinite(initialLat) && isFinite(initialLng);
                tenderMap = L.map("TenderLocationMap").setView(
                    hasLocation ? [initialLat, initialLng] : [17.6599, 75.9064],
                    hasLocation ? 14 : 11);
                L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                    maxZoom: 19, crossOrigin: true, attribution: "&copy; OpenStreetMap contributors"
                }).addTo(tenderMap);
                var centroidMarker = hasLocation ? L.marker([initialLat, initialLng]).addTo(tenderMap) : null;

                function renderDraftBoundary() {
                    if (draftLayer) tenderMap.removeLayer(draftLayer);
                    if (draftBoundary.length >= 3)
                        draftLayer = L.polygon(draftBoundary, { color: "#7c3aed", dashArray: "6 6" }).addTo(tenderMap);
                    else if (draftBoundary.length >= 2)
                        draftLayer = L.polyline(draftBoundary, { color: "#7c3aed", dashArray: "6 6" }).addTo(tenderMap);
                    else if (draftBoundary.length === 1)
                        draftLayer = L.circleMarker(draftBoundary[0], { radius: 6, color: "#7c3aed" }).addTo(tenderMap);
                    if (mapStatus && drawingArea)
                        mapStatus.textContent = draftBoundary.length + " boundary point(s) selected. Add at least three, then finish.";
                }

                function setBoundaryLayer(points, fitMap) {
                    if (boundaryLayer) tenderMap.removeLayer(boundaryLayer);
                    boundaryLayer = points.length >= 3
                        ? L.polygon(points, { color: "#166534", fillOpacity: 0.18 }).addTo(tenderMap)
                        : null;
                    if (fitMap && boundaryLayer)
                        tenderMap.fitBounds(boundaryLayer.getBounds(), { padding: [24, 24], animate: false });
                }

                function calculateBoundaryArea(points) {
                    var earthRadius = 6378137;
                    var twiceArea = 0;
                    var referenceLatitude = 0;
                    for (var i = 0; i < points.length - 1; i++)
                        referenceLatitude += points[i][0];
                    referenceLatitude /= points.length - 1;
                    var longitudeScale = Math.cos(referenceLatitude * Math.PI / 180);
                    var centroidX = 0, centroidY = 0, centroidCrossSum = 0;
                    for (var j = 0; j < points.length - 1; j++) {
                        var latitude1 = points[j][0] * Math.PI / 180;
                        var longitude1 = points[j][1] * Math.PI / 180;
                        var latitude2 = points[j + 1][0] * Math.PI / 180;
                        var longitude2 = points[j + 1][1] * Math.PI / 180;
                        twiceArea += (longitude2 - longitude1) *
                            (2 + Math.sin(latitude1) + Math.sin(latitude2));

                        var x1 = earthRadius * longitude1 * longitudeScale;
                        var y1 = earthRadius * latitude1;
                        var x2 = earthRadius * longitude2 * longitudeScale;
                        var y2 = earthRadius * latitude2;
                        var cross = x1 * y2 - x2 * y1;
                        centroidCrossSum += cross;
                        centroidX += (x1 + x2) * cross;
                        centroidY += (y1 + y2) * cross;
                    }
                    var squareMeters = Math.abs(twiceArea) * earthRadius * earthRadius / 2;
                    if (!isFinite(squareMeters) || squareMeters <= 0 ||
                        !isFinite(centroidCrossSum) || Math.abs(centroidCrossSum) < 0.000001)
                        return null;
                    var centroidLongitude = (centroidX / (3 * centroidCrossSum)) /
                        (earthRadius * longitudeScale) * 180 / Math.PI;
                    var centroidLatitude = (centroidY / (3 * centroidCrossSum)) /
                        earthRadius * 180 / Math.PI;
                    if (!isFinite(centroidLatitude) || !isFinite(centroidLongitude))
                        return null;
                    return {
                        squareMeters: squareMeters,
                        latitude: centroidLatitude,
                        longitude: centroidLongitude
                    };
                }

                function formatArea(squareMeters) {
                    if (squareMeters >= 10000)
                        return (squareMeters / 10000).toLocaleString(undefined, { maximumFractionDigits: 2 }) + " hectares";
                    return squareMeters.toLocaleString(undefined, { maximumFractionDigits: 2 }) + " m²";
                }

                function captureSelectedAreaMap() {
                    if (!imageInput || !preview || !tenderMap || !selectedBoundary.length) return;
                    if (imageInput.files && imageInput.files.length && !generatedGisMapImage) {
                        if (mapStatus) mapStatus.textContent =
                            "Boundary selected. Your uploaded GIS image is kept; upload a replacement if needed.";
                        return;
                    }

                    var captureSequence = ++gisImageCaptureSequence;
                    var mapElement = document.getElementById("TenderLocationMap");
                    var mapBounds = mapElement.getBoundingClientRect();
                    if (!mapBounds.width || !mapBounds.height) {
                        if (mapStatus) mapStatus.textContent =
                            "Boundary saved, but the map image could not be captured because the map is not visible.";
                        return;
                    }

                    var visibleTiles = Array.prototype.slice.call(mapElement.querySelectorAll("img.leaflet-tile"))
                        .filter(function (tile) {
                            var bounds = tile.getBoundingClientRect();
                            return bounds.width && bounds.height &&
                                bounds.right > mapBounds.left && bounds.left < mapBounds.right &&
                                bounds.bottom > mapBounds.top && bounds.top < mapBounds.bottom;
                        });
                    if (!visibleTiles.length) {
                        if (mapStatus) mapStatus.textContent =
                            "Boundary saved, but no map tiles are available to capture. Check the map connection.";
                        return;
                    }

                    if (mapStatus) mapStatus.textContent = "Capturing the selected area map image…";
                    Promise.all(visibleTiles.map(function (tile) {
                        return new Promise(function (resolve) {
                            if (tile.complete) {
                                resolve();
                                return;
                            }
                            var settled = false;
                            var finish = function () {
                                if (settled) return;
                                settled = true;
                                tile.removeEventListener("load", finish);
                                tile.removeEventListener("error", finish);
                                resolve();
                            };
                            tile.addEventListener("load", finish);
                            tile.addEventListener("error", finish);
                            window.setTimeout(finish, 8000);
                        });
                    })).then(function () {
                        if (captureSequence !== gisImageCaptureSequence || !selectedBoundary.length) return;
                        if (visibleTiles.some(function (tile) { return !tile.complete || !tile.naturalWidth; })) {
                            if (mapStatus) mapStatus.textContent =
                                "Boundary saved, but some map tiles could not be loaded. Upload a GIS image manually or retry.";
                            return;
                        }

                        var scale = 2;
                        var canvas = document.createElement("canvas");
                        canvas.width = Math.round(mapBounds.width * scale);
                        canvas.height = Math.round(mapBounds.height * scale);
                        var context = canvas.getContext("2d");
                        if (!context) {
                            if (mapStatus) mapStatus.textContent =
                                "Boundary saved, but this browser could not create the map image.";
                            return;
                        }
                        context.scale(scale, scale);
                        context.fillStyle = "#f8fafc";
                        context.fillRect(0, 0, mapBounds.width, mapBounds.height);
                        try {
                            visibleTiles.forEach(function (tile) {
                                var tileBounds = tile.getBoundingClientRect();
                                context.drawImage(tile, tileBounds.left - mapBounds.left,
                                    tileBounds.top - mapBounds.top, tileBounds.width, tileBounds.height);
                            });
                        } catch (error) {
                            if (mapStatus) mapStatus.textContent =
                                "Boundary saved, but the map tiles cannot be included in an image. Upload a GIS image manually.";
                            return;
                        }

                        var boundaryPoints = selectedBoundary.map(function (point) {
                            return tenderMap.latLngToContainerPoint(L.latLng(point[0], point[1]));
                        });
                        context.beginPath();
                        context.moveTo(boundaryPoints[0].x, boundaryPoints[0].y);
                        for (var pointIndex = 1; pointIndex < boundaryPoints.length; pointIndex++)
                            context.lineTo(boundaryPoints[pointIndex].x, boundaryPoints[pointIndex].y);
                        context.closePath();
                        context.fillStyle = "rgba(22, 101, 52, 0.22)";
                        context.strokeStyle = "#166534";
                        context.lineWidth = 3;
                        context.fill();
                        context.stroke();

                        var centroid = tenderMap.latLngToContainerPoint([
                            Number(latitude.value), Number(longitude.value)
                        ]);
                        context.beginPath();
                        context.arc(centroid.x, centroid.y, 6, 0, Math.PI * 2);
                        context.fillStyle = "#b91c1c";
                        context.fill();
                        context.strokeStyle = "#ffffff";
                        context.lineWidth = 2;
                        context.stroke();

                        context.font = "12px Arial, sans-serif";
                        context.textBaseline = "bottom";
                        var attribution = "© OpenStreetMap contributors";
                        var attributionWidth = context.measureText(attribution).width;
                        context.fillStyle = "rgba(255, 255, 255, 0.88)";
                        context.fillRect(mapBounds.width - attributionWidth - 14,
                            mapBounds.height - 23, attributionWidth + 10, 19);
                        context.fillStyle = "#334155";
                        context.fillText(attribution, mapBounds.width - attributionWidth - 9,
                            mapBounds.height - 8);

                        canvas.toBlob(function (blob) {
                            if (captureSequence !== gisImageCaptureSequence || !selectedBoundary.length) return;
                            if (!blob) {
                                if (mapStatus) mapStatus.textContent =
                                    "Boundary saved, but the map image could not be generated. Upload a GIS image manually.";
                                return;
                            }
                            try {
                                var file = new File([blob], "gis-area-map.png", {
                                    type: "image/png", lastModified: Date.now()
                                });
                                var transfer = new DataTransfer();
                                transfer.items.add(file);
                                imageInput.files = transfer.files;
                                if (!imageInput.files || imageInput.files.length !== 1)
                                    throw new Error("The generated map image could not be attached.");
                                generatedGisMapImage = true;
                                attachingGeneratedGisMapImage = true;
                                try {
                                    imageInput.dispatchEvent(new Event("change", { bubbles: true }));
                                } finally {
                                    attachingGeneratedGisMapImage = false;
                                }
                                if (!imageInput.files || imageInput.files.length !== 1 ||
                                    imageInput.files[0].name !== file.name)
                                    throw new Error("The generated map image did not pass upload validation.");
                                if (mapStatus) mapStatus.textContent =
                                    "Selected area map image captured and ready to save with this tender.";
                            } catch (error) {
                                generatedGisMapImage = false;
                                if (mapStatus) mapStatus.textContent =
                                    "Boundary saved, but the generated map image could not be attached. Upload a GIS image manually.";
                            }
                        }, "image/png");
                    }).catch(function () {
                        if (captureSequence === gisImageCaptureSequence && mapStatus)
                            mapStatus.textContent =
                                "Boundary saved, but the selected area map image could not be generated. Upload a GIS image manually.";
                    });
                }

                function applyBoundary(points) {
                    var result = calculateBoundaryArea((points.map(function (point) {
                        return [point[0], point[1]];
                    })).concat([[points[0][0], points[0][1]]]));
                    if (!result) {
                        if (mapStatus) mapStatus.textContent = "This boundary cannot be measured. Redraw it with a non-crossing shape.";
                        return false;
                    }
                    if (boundaryField) boundaryField.value = JSON.stringify(points.map(function (point) {
                        return [Number(point[1].toFixed(6)), Number(point[0].toFixed(6))];
                    }).concat([[Number(points[0][1].toFixed(6)), Number(points[0][0].toFixed(6))]]));
                    selectedBoundary = points.slice();
                    boundaryAreaSquareMeters = result.squareMeters;
                    latitude.value = result.latitude.toFixed(6);
                    longitude.value = result.longitude.toFixed(6);
                    latitude.readOnly = true;
                    longitude.readOnly = true;
                    if (centroidMarker) centroidMarker.setLatLng([result.latitude, result.longitude]);
                    else centroidMarker = L.marker([result.latitude, result.longitude]).addTo(tenderMap);
                    setBoundaryLayer(selectedBoundary, true);
                    if (areaSummary) {
                        areaSummary.textContent = "Selected area: " + formatArea(boundaryAreaSquareMeters) +
                            " · centroid " + latitude.value + ", " + longitude.value;
                        areaSummary.hidden = false;
                    }
                    if (mapStatus) mapStatus.textContent = "Boundary selected. The displayed coordinates are its calculated centroid.";
                    render();
                    captureSelectedAreaMap();
                    return true;
                }

                window.startGisAreaDrawing = function () {
                    if (!tenderMap) {
                        if (mapStatus) mapStatus.textContent = "Map selection is unavailable; verify the map connection and retry.";
                        return;
                    }
                    drawingArea = true;
                    draftBoundary = [];
                    if (draftLayer) tenderMap.removeLayer(draftLayer);
                    draftLayer = null;
                    tenderMap.doubleClickZoom.disable();
                    if (mapStatus) mapStatus.textContent = "Click at least three points around the site boundary.";
                };
                window.undoGisAreaPoint = function () {
                    if (!drawingArea || !draftBoundary.length) return;
                    draftBoundary.pop();
                    renderDraftBoundary();
                };
                window.finishGisAreaDrawing = function () {
                    if (!drawingArea) {
                        if (mapStatus) mapStatus.textContent = "Choose Draw boundary before selecting points.";
                        return;
                    }
                    if (draftBoundary.length < 3) {
                        if (mapStatus) mapStatus.textContent = "Select at least three points to finish the boundary.";
                        return;
                    }
                    var finished = draftBoundary.slice();
                    drawingArea = false;
                    draftBoundary = [];
                    if (draftLayer) tenderMap.removeLayer(draftLayer);
                    draftLayer = null;
                    tenderMap.doubleClickZoom.enable();
                    applyBoundary(finished);
                };
                window.clearGisArea = function () {
                    if (!window.confirm("Clear the selected GIS boundary and its calculated area?")) return;
                    gisImageCaptureSequence++;
                    var removedGeneratedMapImage = generatedGisMapImage;
                    drawingArea = false;
                    draftBoundary = [];
                    selectedBoundary = [];
                    boundaryAreaSquareMeters = null;
                    if (draftLayer) tenderMap.removeLayer(draftLayer);
                    draftLayer = null;
                    if (boundaryLayer) tenderMap.removeLayer(boundaryLayer);
                    boundaryLayer = null;
                    if (boundaryField) boundaryField.value = "";
                    if (generatedGisMapImage) {
                        imageInput.value = "";
                        generatedGisMapImage = false;
                        if (gisImagePreviewUrl) URL.revokeObjectURL(gisImagePreviewUrl);
                        gisImagePreviewUrl = null;
                        preview.removeAttribute("src");
                        preview.hidden = true;
                    }
                    latitude.readOnly = false;
                    longitude.readOnly = false;
                    if (areaSummary) {
                        areaSummary.textContent = "";
                        areaSummary.hidden = true;
                    }
                    if (mapStatus) mapStatus.textContent = removedGeneratedMapImage
                        ? "Boundary and generated map image cleared. Coordinates may be entered manually or a new boundary drawn."
                        : "Boundary cleared. Coordinates may be entered manually or a new boundary drawn.";
                    tenderMap.doubleClickZoom.enable();
                    render();
                };

                tenderMap.on("click", function (event) {
                    if (!drawingArea) return;
                    draftBoundary.push([event.latlng.lat, event.latlng.lng]);
                    renderDraftBoundary();
                });
                if (boundaryField && boundaryField.value) {
                    try {
                        var storedCoordinates = JSON.parse(boundaryField.value);
                        if (Array.isArray(storedCoordinates) && storedCoordinates.length >= 4) {
                            selectedBoundary = storedCoordinates.slice(0, -1).map(function (coordinate) {
                                return [Number(coordinate[1]), Number(coordinate[0])];
                            });
                            var storedResult = calculateBoundaryArea(storedCoordinates.map(function (coordinate) {
                                return [Number(coordinate[1]), Number(coordinate[0])];
                            }));
                            if (storedResult && selectedBoundary.every(function (point) {
                                return isFinite(point[0]) && isFinite(point[1]);
                            })) {
                                boundaryAreaSquareMeters = storedResult.squareMeters;
                                latitude.value = storedResult.latitude.toFixed(6);
                                longitude.value = storedResult.longitude.toFixed(6);
                                latitude.readOnly = true;
                                longitude.readOnly = true;
                                setBoundaryLayer(selectedBoundary, true);
                                if (centroidMarker) centroidMarker.setLatLng([storedResult.latitude, storedResult.longitude]);
                                else centroidMarker = L.marker([storedResult.latitude, storedResult.longitude]).addTo(tenderMap);
                                if (areaSummary) {
                                    areaSummary.textContent = "Selected area: " + formatArea(boundaryAreaSquareMeters) +
                                        " · centroid " + latitude.value + ", " + longitude.value;
                                    areaSummary.hidden = false;
                                }
                            }
                        }
                    } catch (error) {
                        if (mapStatus) mapStatus.textContent = "The saved boundary could not be displayed; redraw it to replace.";
                    }
                }
                [latitude, longitude].forEach(function (input) {
                    input.addEventListener("change", function () {
                        if (selectedBoundary.length) return;
                        var lat = parseFloat(latitude.value), lng = parseFloat(longitude.value);
                        if (!isFinite(lat) || !isFinite(lng) || lat < -90 || lat > 90 || lng < -180 || lng > 180) return;
                        var point = [lat, lng];
                        if (!centroidMarker) centroidMarker = L.marker(point).addTo(tenderMap);
                        else centroidMarker.setLatLng(point);
                        tenderMap.setView(point, 14);
                    });
                });
            } else if (mapStatus) {
                mapStatus.textContent = "Map selection is unavailable; enter valid coordinates manually.";
            }
        }());

        function renderTenderReviewSummary(container) {
            var groups = [
                {
                    title: "Basic tender information", step: "1",
                    fields: [
                        ["Tender title", "TitleText"], ["Project description", "DescriptionText"],
                        ["Department", "DepartmentSelect"], ["Budgeting Financial Year", "FinancialYearSelect"],
                        ["Tender method", "TenderTypeSelect"], ["Tender category", "TenderCategorySelect"],
                        ["Estimated value (₹)", "AmountText"],
                        ["Additional conditions", "AdditionalConditionsText"],
                        ["Terms and conditions", "TermsAndConditionsText"]
                    ],
                    documents: [
                        ["Scope of Work", "ScopeOfWorkDocumentUpload", "ScopeOfWorkCurrentFileName", "ScopeOfWorkDocumentRemoveFlag"],
                        ["Technical Requirements", "TechnicalRequirementsDocumentUpload", "TechnicalRequirementsCurrentFileName", "TechnicalRequirementsDocumentRemoveFlag"],
                        ["Financial Requirements", "FinancialRequirementsDocumentUpload", "FinancialRequirementsCurrentFileName", "FinancialRequirementsDocumentRemoveFlag"]
                    ]
                },
                {
                    title: "Important dates and times", step: "2",
                    fields: [
                        ["Publication date", "PublicationDateText"], ["Submission start", "StartText"],
                        ["Submission closing", "DeadlineText"], ["Technical bid opening", "TechnicalBidOpeningText"],
                        ["Financial bid opening", "FinancialBidOpeningText"], ["Pre-bid meeting", "PreBidMeetingText"],
                        ["Pre-bid location", "PreBidLocationText"], ["Pre-bid details", "PreBidDetailsText"]
                    ]
                },
                {
                    title: "Administrative approvals", step: "3",
                    fields: [
                        ["Administrative Approval", "AdministrativeApprovalCheck"],
                        ["Administrative approval reference", "AdministrativeApprovalReferenceText"],
                        ["Administrative approval date", "AdministrativeApprovalDateText"],
                        ["Technical Sanction (TS) Approval", "TechnicalSanctionApprovalCheck"],
                        ["Technical sanction approval reference", "TechnicalSanctionApprovalReferenceText"],
                        ["Technical sanction approval date", "TechnicalSanctionApprovalDateText"],
                        ["General Body (GB) Approval", "GeneralBodyApprovalCheck"],
                        ["General Body approval reference", "GeneralBodyApprovalReferenceText"],
                        ["General Body approval date", "GeneralBodyApprovalDateText"],
                        ["DPDC Approval", "DpdcApprovalCheck"],
                        ["DPDC approval reference", "DpdcApprovalReferenceText"],
                        ["DPDC approval date", "DpdcApprovalDateText"],
                        ["Budget information", "BudgetInformationText"],
                        ["Technical sanction number", "TechnicalSanctionNumberText"],
                        ["Technical sanction authority", "TechnicalSanctionAuthorityText"],
                        ["Technical sanction date", "TechnicalSanctionDateText"]
                    ],
                    files: [
                        ["Administrative approval document", "AdministrativeApprovalDocumentUpload", "AdministrativeApprovalDocumentLink"],
                        ["Technical sanction approval document", "TechnicalSanctionApprovalDocumentUpload", "TechnicalSanctionApprovalDocumentLink"],
                        ["General Body approval document", "GeneralBodyApprovalDocumentUpload", "GeneralBodyApprovalDocumentLink"],
                        ["DPDC approval document", "DpdcApprovalDocumentUpload", "DpdcApprovalDocumentLink"]
                    ]
                },
                {
                    title: "GIS location", step: "4",
                    fields: [
                        ["Site address", "LocationNameText"], ["Latitude", "LatitudeText"],
                        ["Longitude", "LongitudeText"]
                    ],
                    files: [["GIS image", "GisImageUpload", "CurrentGisImageLink"]]
                }
            ];
            function fieldRow(item) {
                var input = document.querySelector("[id$='" + item[1] + "']");
                if (!input) return "";
                var value = input.tagName === "SELECT"
                    ? (input.selectedIndex >= 0 ? input.options[input.selectedIndex].text : "")
                    : (input.type === "checkbox" ? (input.checked ? "Approved" : "Pending") :
                        (typeof input.value === "undefined" ? input.textContent : input.value));
                return "<div class='tender-review-row'><dt>" + escapeTenderReviewText(item[0]) +
                    "</dt><dd>" + escapeTenderReviewText(value || "Not provided") + "</dd></div>";
            }
            function requirementDocumentRow(entry) {
                var input = document.querySelector("[id$='" + entry[1] + "']");
                var existing = document.querySelector("[id$='" + entry[2] + "']");
                var removeFlag = document.querySelector("input[id$='" + entry[3] + "']");
                var fileName = input && input.files && input.files.length
                    ? input.files[0].name
                    : (removeFlag && removeFlag.value === "true" ? "Will be removed" :
                        (existing && existing.textContent ? existing.textContent : "Not uploaded"));
                return "<div class='tender-review-row'><dt>" + escapeTenderReviewText(entry[0]) +
                    "</dt><dd>" + escapeTenderReviewText(fileName) + "</dd></div>";
            }
            function fileRow(entry) {
                var input = document.querySelector("[id$='" + entry[1] + "']");
                var existing = document.querySelector("[id$='" + entry[2] + "']");
                var fileName = input && input.files && input.files.length
                    ? input.files[0].name
                    : (existing && !existing.hidden ? existing.textContent : "Not uploaded");
                return "<div class='tender-review-row'><dt>" + escapeTenderReviewText(entry[0]) +
                    "</dt><dd>" + escapeTenderReviewText(fileName || "Not uploaded") + "</dd></div>";
            }
            var department = document.querySelector("[id$='DepartmentSelect']");
            var departmentLabel = document.getElementById("DepartmentReadOnlyName");
            if (!department && departmentLabel)
                groups[0].fields.push(["Department", "DepartmentReadOnlyName"]);
            var groupMarkup = groups.map(function (group) {
                var rows = group.fields.map(fieldRow);
                (group.documents || []).forEach(function (entry) { rows.push(requirementDocumentRow(entry)); });
                (group.files || []).forEach(function (entry) { rows.push(fileRow(entry)); });
                if (group.title === "GIS location") {
                    var selectedArea = document.getElementById("TenderGisAreaSummary");
                    if (selectedArea && !selectedArea.hidden)
                        rows.push("<div class='tender-review-row'><dt>Selected area</dt><dd>" +
                            escapeTenderReviewText(selectedArea.textContent) + "</dd></div>");
                }
                rows = rows.filter(Boolean);
                if (!rows.length) return "";
                return "<section class='tender-review-card'><header><h3>" +
                    escapeTenderReviewText(group.title) + "</h3><button type='button' class='button secondary' " +
                    "onclick='showTenderWizardStep(" + escapeTenderReviewText(group.step) +
                    ")' aria-label='Edit " + escapeTenderReviewText(group.title) + "'>Edit section</button></header>" +
                    "<dl class='tender-review-fields'>" + rows.join("") + "</dl></section>";
            }).join("");
            var method = document.querySelector("[id$='TenderTypeSelect']");
            var offlineNote = method && method.value === "Offline"
                ? "<p class='message'>Offline tender: contractors must submit physical bids. Online applications are disabled.</p>" : "";
            container.innerHTML = "<div class='tender-review-grid'>" + groupMarkup + "</div>" + offlineNote;
        }

        function escapeTenderReviewText(value) {
            var element = document.createElement("span");
            element.textContent = String(value);
            return element.innerHTML;
        }
    </script>
</asp:Content>
