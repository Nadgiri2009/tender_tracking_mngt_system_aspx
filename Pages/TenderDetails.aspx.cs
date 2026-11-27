using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class TenderDetails : ModulePage
    {
        protected override bool RequiresAuthentication
        {
            get { return false; }
        }

        protected Literal PageTitle, TenderTitle, TenderNumber, DepartmentName, Description, EligibilityCriteria,
            TenderType, TenderCategory, EstimatedAmount, TenderSubcategory, FinancialYear,
            EMDAmount, TenderDocumentFee, ProcessingFee, SecurityDepositAmount;
        protected Literal PublicationDate, SubmissionStartDate, SubmissionDeadline, CreatedBy, CreatedDate,
            CurrentStatus, Message, ApplicationMessage, PublicationPeriod, ExtensionCount,
            PublicationScheduledDate, TechnicalBidOpeningDate, FinancialBidOpeningDate,
            PreBidMeetingDate, PreBidLocation, PreBidDetails,
            ScopeOfWork, TechnicalRequirements, FinancialRequirements, AdditionalConditions, TermsAndConditions;
        protected Literal GisLocationName, GisLatitude, GisLongitude, GisSelectedArea,
            GisBoundaryCoordinatesText, GisBoundaryStartText, GisBoundaryEndText, GisBoundaryLineGeometryText;
        protected Literal DocumentShortfallSummary;
        protected GridView BoqGrid, DocumentsGrid, HistoryGrid, ApplicationRequirementsGrid, RevisionHistoryGrid;
        protected GridView BudgetOpinionsGrid;
        protected Repeater WorkflowTimeline;
        protected Literal TrackerCurrentStage;
        protected Panel AddBoqPanel, UploadPanel, AuthenticatedWorkflowPanel, EligibilityCriteriaPanel,
            ApplicationRequirementsPanel, CurrentDocumentShortfallPanel;
        protected Panel ScopeOfWorkAttachmentPanel, TechnicalRequirementsAttachmentPanel,
            FinancialRequirementsAttachmentPanel;
        protected Literal ScopeOfWorkAttachmentStatus, TechnicalRequirementsAttachmentStatus,
            FinancialRequirementsAttachmentStatus;
        protected Panel BudgetOpinionPanel, BudgetOpinionHistoryPanel, DocumentTypesMissingPanel,
            DocumentFormPanel, DocumentFilePanel;
        protected Panel RevisionHistoryPanel, GisLocationPanel, GisBoundaryPanel, GisBoundaryLinePanel;
        protected DropDownList DocumentTypeSelect;
        protected TextBox ItemName, ItemDescription, Quantity, Unit, Rate, Remarks,
            DocumentNameText, DocumentRemarksText;
        protected TextBox ApprovedBudgetAmount;
        protected FileUpload DocumentFile;
        protected Button AddBoqButton, UploadButton, SubmitWorkflowButton, ApproveButton, RejectButton;
        protected Button DeleteTenderButton;
        protected HiddenField EditingDocumentId, ReplacingDocumentFile;
        protected Literal DocumentFormTitle;
        protected LinkButton ShowDocumentFormButton, CancelDocumentFormButton, RefreshDocumentTypesButton;
        protected HyperLink BidsLink, WorkLink, EditTenderLink, ApplyTenderLink, FinalWorkflowLink,
            BackToTendersLink, GisImageLink;
        protected HyperLink ScopeOfWorkAttachmentViewLink, ScopeOfWorkAttachmentDownloadLink,
            TechnicalRequirementsAttachmentViewLink, TechnicalRequirementsAttachmentDownloadLink,
            FinancialRequirementsAttachmentViewLink, FinancialRequirementsAttachmentDownloadLink;
        protected Image GisImagePreview;
        private int TenderId { get { return QueryId("id"); } }
        private string TenderStatus { get; set; }
        private int TenderDepartmentId { get; set; }
        private bool IsFinalWorkflow { get; set; }
        private bool IsDraft { get; set; }
        private bool IsEditableDraft { get; set; }
        private bool IsDocumentCorrectionStage { get; set; }
        private bool IsAuthorizedTender { get; set; }
        private bool IsPublicationWindowOpen { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (TenderId <= 0)
            {
                Response.Redirect("~/Pages/Tenders.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            LoadTender();
            if (IsAuthorizedTender && String.Equals(Request.QueryString["saved"], "1", StringComparison.Ordinal))
            {
                bool documentsSaved = String.Equals(Request.QueryString["documentsSaved"], "1",
                    StringComparison.Ordinal);
                Message.Text = documentsSaved
                    ? "<div class=\"message success\">Tender and requirement documents saved successfully.</div>"
                    : "<div class=\"message success\">Tender details saved successfully.</div>";
            }
        }

        private void LoadTender()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    if (!IsPostBack) BindDocumentTypes(connection);
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT t.TenderId,t.TenderNumber,t.Title,t.Description,t.EligibilityCriteria,t.TenderType,t.TenderCategory,t.EstimatedAmount,t.CurrentStatus,
                                 t.TenderSubcategoryId,sub.SubcategoryName,t.FinancialYear,
                                 t.EMDAmount,t.TenderDocumentFee,t.ProcessingFee,t.SecurityDepositAmount,
                                 t.PublicationScheduledUtc,t.TechnicalBidOpeningScheduledUtc,t.FinancialBidOpeningScheduledUtc,
                                 t.PreBidMeetingUtc,t.PreBidLocation,t.PreBidDetails,
                                 req.ScopeOfWork,req.TechnicalRequirements,req.FinancialRequirements,
                                 req.AdditionalConditions,req.TermsAndConditions,
                                 gis.LocationName AS GisLocationName,gis.Latitude AS GisLatitude,
                                 gis.Longitude AS GisLongitude,gis.SelectedAreaSquareMeters,
                                 gis.BoundaryCoordinatesJson,gis.ImagePath AS GisImagePath,
                                 gis.BoundaryStartLatitude,gis.BoundaryStartLongitude,
                                 gis.BoundaryEndLatitude,gis.BoundaryEndLongitude,
                                 gis.BoundaryLineGeometryJson,
                                 gis.ImageOriginalFileName AS GisImageOriginalFileName,
                                 t.WorkflowVersion,t.DepartmentId,
                                 CAST(CASE WHEN t.CurrentStatus=N'Draft' AND
                                     (t.WorkflowVersion=1 OR
                                      (t.WorkflowVersion=2 AND EXISTS
                                          (SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                           INNER JOIN dbo.WorkflowStages ws ON ws.StageId=a.StageId
                                           WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                             AND ws.StageName=N'Final_JE_SubmitTender'
                                             AND @DepartmentId IS NOT NULL
                                             AND a.DepartmentId=@DepartmentId)))
                                     THEN 1 ELSE 0 END AS bit) AS IsEditableDraft,
                                 CAST(CASE WHEN t.WorkflowVersion=2
                                      AND t.CurrentStatus=N'Pending JE Document Correction'
                                      AND EXISTS
                                      (
                                          SELECT 1
                                          FROM dbo.TenderWorkflowAssignments correctionAssignment
                                          INNER JOIN dbo.WorkflowStages correctionStage
                                            ON correctionStage.StageId=correctionAssignment.StageId
                                          INNER JOIN dbo.TenderDocumentShortfalls documentShortfall
                                            ON documentShortfall.TenderDocumentShortfallId=correctionAssignment.ShortfallId
                                          WHERE correctionAssignment.TenderId=t.TenderId
                                            AND correctionAssignment.IsCurrent=1
                                            AND correctionAssignment.AssignedRoleId=@RoleId
                                            AND (correctionAssignment.AssignedUserId IS NULL
                                                 OR correctionAssignment.AssignedUserId=@CurrentUserId)
                                            AND correctionAssignment.DepartmentId=t.DepartmentId
                                            AND @DepartmentId IS NOT NULL
                                            AND correctionAssignment.DepartmentId=@DepartmentId
                                            AND correctionStage.StageName=N'Final_JE_CorrectTenderDocuments'
                                            AND documentShortfall.ResolutionStatus=N'Open'
                                            AND documentShortfall.DeadlineAtUtc>=SYSUTCDATETIME()
                                      ) THEN 1 ELSE 0 END AS bit) AS IsDocumentCorrectionStage,
                                 COALESCE((SELECT MIN(ah.ApprovalDate) FROM dbo.ApprovalHistory ah
                                           WHERE ah.TenderId=t.TenderId AND ah.NewStatus=N'Tender Published'),
                                          s.PublicationStartUtc) AS PublicationDate,
                                 t.SubmissionStartDate,t.SubmissionDeadline,t.CreatedDate,d.DepartmentName,u.FullName
                                 ,s.PublicationStartUtc,s.OriginalClosingUtc,s.CurrentClosingUtc,
                                 (SELECT COUNT(*) FROM dbo.TenderExtensionRequests e
                                  WHERE e.TenderId=t.TenderId AND e.ReviewStatus=N'Approved') AS ApprovedExtensionCount,
                                 CASE WHEN t.CurrentStatus=N'Tender Published'
                                           AND s.PublicationStartUtc<=SYSUTCDATETIME()
                                           AND s.CurrentClosingUtc>SYSUTCDATETIME() THEN CAST(1 AS bit)
                                      ELSE CAST(0 AS bit) END AS IsBidWindowOpen
                          FROM dbo.Tender t INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                          INNER JOIN dbo.Users u ON u.UserId=t.CreatedBy
                          LEFT JOIN dbo.TenderSubcategories sub ON sub.SubcategoryId=t.TenderSubcategoryId
                          LEFT JOIN dbo.TenderRequirements req ON req.TenderId=t.TenderId
                          LEFT JOIN dbo.TenderGisLocations gis ON gis.TenderId=t.TenderId
                          LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                          WHERE t.TenderId=@TenderId AND t.IsDeleted=0
                            AND
                            (
                                (@CurrentUserId=0 AND
                                 s.PublicationStartUtc<=SYSUTCDATETIME()
                                 AND t.CurrentStatus NOT LIKE N'%cancel%'
                                 AND
                                    (EXISTS(SELECT 1 FROM dbo.ApprovalHistory publicHistory
                                            WHERE publicHistory.TenderId=t.TenderId AND publicHistory.NewStatus=N'Tender Published')
                                     OR EXISTS(SELECT 1 FROM dbo.TenderWorkflowHistory publicWorkflowHistory
                                               WHERE publicWorkflowHistory.TenderId=t.TenderId
                                                 AND publicWorkflowHistory.WorkflowVersion=2
                                                 AND publicWorkflowHistory.NewStatus=N'Tender Published')))
                                OR (@CurrentUserId>0 AND @IsContractor=1 AND
                                    (EXISTS(SELECT 1 FROM dbo.ApprovalHistory publicHistory
                                            WHERE publicHistory.TenderId=t.TenderId AND publicHistory.NewStatus=N'Tender Published')
                                     OR EXISTS(SELECT 1 FROM dbo.TenderWorkflowHistory publicWorkflowHistory
                                               WHERE publicWorkflowHistory.TenderId=t.TenderId
                                                 AND publicWorkflowHistory.WorkflowVersion=2
                                                 AND publicWorkflowHistory.NewStatus=N'Tender Published')
                                     OR (t.WorkflowVersion=2 AND t.CurrentStatus IN (N'Tender Published',N'Completed'))
                                     OR EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments ownAssignment
                                               WHERE ownAssignment.TenderId=t.TenderId AND ownAssignment.IsCurrent=1
                                                 AND ownAssignment.AssignedRoleId=@RoleId
                                                 AND ownAssignment.AssignedUserId=@CurrentUserId)))
                                OR (@CurrentUserId>0 AND @IsContractor=0 AND
                                    (@IsJeAdministrator=1
                                     OR (t.WorkflowVersion=2 AND EXISTS
                                         (SELECT 1
                                          FROM dbo.TenderWorkflowAssignments assigned
                                          WHERE assigned.TenderId=t.TenderId AND assigned.IsCurrent=1
                                            AND assigned.AssignedRoleId=@RoleId
                                            AND (assigned.AssignedUserId IS NULL OR assigned.AssignedUserId=@CurrentUserId)
                                            AND assigned.DepartmentId=t.DepartmentId
                                            AND (@DepartmentId IS NULL OR assigned.DepartmentId=@DepartmentId)))
                                     OR (t.WorkflowVersion=2 AND @CanMonitor=1
                                         AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId)
                                     OR (t.WorkflowVersion=1 AND
                                         (@CurrentRole<>N'DepartmentOfficer' OR t.CreatedBy=@CurrentUserId)
                                         AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)))
                            );", connection))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@CurrentUserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@CurrentRole", SqlDbType.NVarChar, CurrentRole, 50);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        ModuleDb.AddParameter(command, "@IsContractor", SqlDbType.Bit, HasAnyRole("Contractor"));
                        ModuleDb.AddParameter(command, "@IsJeAdministrator", SqlDbType.Bit, IsJeAdministrator);
                        ModuleDb.AddParameter(command, "@CanMonitor", SqlDbType.Bit,
                            !IsJeAdministrator && HasPermission("tenders.monitor"));
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                DenyTenderAccess(connection);
                                return;
                            }
                            IsAuthorizedTender = true;
                            TenderNumber.Text = PageTitle.Text = ModuleDb.Html(reader["TenderNumber"]);
                            TenderTitle.Text = ModuleDb.Html(reader["Title"]);
                            DepartmentName.Text = ModuleDb.Html(reader["DepartmentName"]);
                            Description.Text = ModuleDb.Html(reader["Description"]);
                            EligibilityCriteria.Text = ModuleDb.Html(reader["EligibilityCriteria"]);
                            EligibilityCriteriaPanel.Visible = reader["EligibilityCriteria"] != DBNull.Value &&
                                !String.IsNullOrWhiteSpace(Convert.ToString(reader["EligibilityCriteria"]));
                            TenderType.Text = ModuleDb.Html(reader["TenderType"]);
                            TenderCategory.Text = ModuleDb.Html(reader["TenderCategory"]);
                            TenderSubcategory.Text = ModuleDb.Html(reader["SubcategoryName"]);
                            FinancialYear.Text = ModuleDb.Html(reader["FinancialYear"]);
                            EstimatedAmount.Text = Convert.ToDecimal(reader["EstimatedAmount"]).ToString("N2", CultureInfo.CurrentCulture);
                            EMDAmount.Text = FormatMoney(reader["EMDAmount"]);
                            TenderDocumentFee.Text = FormatMoney(reader["TenderDocumentFee"]);
                            ProcessingFee.Text = FormatMoney(reader["ProcessingFee"]);
                            SecurityDepositAmount.Text = FormatMoney(reader["SecurityDepositAmount"]);
                            PublicationScheduledDate.Text = FormatIndiaDate(reader["PublicationScheduledUtc"]);
                            TechnicalBidOpeningDate.Text = FormatIndiaDate(reader["TechnicalBidOpeningScheduledUtc"]);
                            FinancialBidOpeningDate.Text = FormatIndiaDate(reader["FinancialBidOpeningScheduledUtc"]);
                            PreBidMeetingDate.Text = FormatIndiaDate(reader["PreBidMeetingUtc"]);
                            PreBidLocation.Text = ModuleDb.Html(reader["PreBidLocation"]);
                            PreBidDetails.Text = ModuleDb.Html(reader["PreBidDetails"]);
                            ScopeOfWork.Text = ModuleDb.Html(reader["ScopeOfWork"]);
                            TechnicalRequirements.Text = ModuleDb.Html(reader["TechnicalRequirements"]);
                            FinancialRequirements.Text = ModuleDb.Html(reader["FinancialRequirements"]);
                            AdditionalConditions.Text = ModuleDb.Html(reader["AdditionalConditions"]);
                            TermsAndConditions.Text = ModuleDb.Html(reader["TermsAndConditions"]);
                            GisLocationName.Text = ModuleDb.Html(reader["GisLocationName"]);
                            GisLatitude.Text = FormatCoordinate(reader["GisLatitude"]);
                            GisLongitude.Text = FormatCoordinate(reader["GisLongitude"]);
                            GisSelectedArea.Text = FormatArea(reader["SelectedAreaSquareMeters"]);
                            string boundaryCoordinates = reader["BoundaryCoordinatesJson"] == DBNull.Value
                                ? String.Empty : Convert.ToString(reader["BoundaryCoordinatesJson"]);
                            GisBoundaryCoordinatesText.Text = ModuleDb.Html(boundaryCoordinates);
                            GisBoundaryPanel.Visible = !String.IsNullOrWhiteSpace(boundaryCoordinates);
                            bool hasLinearBoundary = reader["BoundaryLineGeometryJson"] != DBNull.Value;
                            GisBoundaryStartText.Text = FormatCoordinate(reader["BoundaryStartLatitude"]) + ", " +
                                FormatCoordinate(reader["BoundaryStartLongitude"]);
                            GisBoundaryEndText.Text = FormatCoordinate(reader["BoundaryEndLatitude"]) + ", " +
                                FormatCoordinate(reader["BoundaryEndLongitude"]);
                            GisBoundaryLineGeometryText.Text = reader["BoundaryLineGeometryJson"] == DBNull.Value
                                ? String.Empty : ModuleDb.Html(Convert.ToString(reader["BoundaryLineGeometryJson"]));
                            GisBoundaryLinePanel.Visible = hasLinearBoundary;
                            GisLocationPanel.Visible = reader["GisLocationName"] != DBNull.Value ||
                                reader["GisLatitude"] != DBNull.Value || reader["GisLongitude"] != DBNull.Value ||
                                reader["SelectedAreaSquareMeters"] != DBNull.Value || reader["GisImagePath"] != DBNull.Value ||
                                hasLinearBoundary;
                            if (reader["GisImagePath"] != DBNull.Value)
                            {
                                string gisImageUrl = ResolveUrl("~/Pages/DocumentDownload.aspx?gisId=" +
                                    TenderId.ToString(CultureInfo.InvariantCulture) + "&preview=true");
                                GisImageLink.NavigateUrl = gisImageUrl;
                                string gisImageName = reader["GisImageOriginalFileName"] == DBNull.Value
                                    ? String.Empty
                                    : Path.GetFileName(Convert.ToString(reader["GisImageOriginalFileName"]));
                                GisImageLink.Text = String.IsNullOrWhiteSpace(gisImageName)
                                    ? "View GIS location image" : "View " + ModuleDb.Html(gisImageName);
                                GisImageLink.Visible = true;
                                GisImagePreview.ImageUrl = gisImageUrl;
                                GisImagePreview.Visible = true;
                            }
                            TenderStatus = Convert.ToString(reader["CurrentStatus"]);
                            IsFinalWorkflow = Convert.ToInt16(reader["WorkflowVersion"]) == 2;
                            IsEditableDraft = Convert.ToBoolean(reader["IsEditableDraft"]);
                            IsDocumentCorrectionStage = Convert.ToBoolean(reader["IsDocumentCorrectionStage"]);
                            TenderDepartmentId = Convert.ToInt32(reader["DepartmentId"]);
                            CurrentStatus.Text = ModuleDb.Html(TenderStatus);
                            PublicationDate.Text = reader["PublicationDate"] == DBNull.Value ? "—" : FormatIndiaDate(reader["PublicationDate"]);
                            SubmissionStartDate.Text = FormatIndiaDate(reader["SubmissionStartDate"]);
                            SubmissionDeadline.Text = FormatIndiaDate(reader["SubmissionDeadline"]);
                            PublicationPeriod.Text = reader["PublicationStartUtc"] == DBNull.Value
                                ? "Not configured"
                                : HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["PublicationStartUtc"])) +
                                  " to " + HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["CurrentClosingUtc"])) + " IST";
                            ExtensionCount.Text = Convert.ToString(reader["ApprovedExtensionCount"], CultureInfo.InvariantCulture) + " of 3";
                            IsPublicationWindowOpen = Convert.ToBoolean(reader["IsBidWindowOpen"], CultureInfo.InvariantCulture);
                            CreatedBy.Text = ModuleDb.Html(reader["FullName"]);
                            CreatedDate.Text = Convert.ToDateTime(reader["CreatedDate"]).ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
                        }

                    }
                    BindChildTables(connection);
                    BindWorkflowTimeline(connection);
                    BindDocumentShortfallSummary(connection);
                }
                IsDraft = String.Equals(TenderStatus, "Draft", StringComparison.OrdinalIgnoreCase);
                bool isBudgetRevision = String.Equals(TenderStatus, "Budget Revision Required", StringComparison.OrdinalIgnoreCase);
                BackToTendersLink.NavigateUrl = CurrentUserId == 0 || HasAnyRole("Contractor")
                    ? "~/Pages/Home.aspx#published-tenders"
                    : "~/Pages/Tenders.aspx";
                AuthenticatedWorkflowPanel.Visible = CurrentUserId > 0;
                RevisionHistoryPanel.Visible = CurrentUserId > 0 && !HasAnyRole("Contractor");
                FinalWorkflowLink.Visible = IsFinalWorkflow && CurrentUserId > 0 &&
                    (HasPermission("tenders.monitor") || HasPermission("tender.create.department") ||
                     HasPermission("tender.cell.verify") || HasPermission("shortfall.respond.own") ||
                     HasPermission("shortfall.approve") || HasPermission("shortfall.clear") ||
                     HasPermission("financial.open.approve") || HasPermission("financial.open.execute") ||
                     HasPermission("financial.approve.final") || HasPermission("workorder.manage") ||
                     HasPermission("agreement.manage") || HasPermission("system.admin"));
                FinalWorkflowLink.NavigateUrl = "~/Pages/TenderWorkflow.aspx?id=" + TenderId.ToString(CultureInfo.InvariantCulture);
                BudgetOpinionHistoryPanel.Visible = CurrentUserId > 0 && !HasAnyRole("Contractor");
                bool budgetApprovalPending = String.Equals(TenderStatus, "Budget Approval Pending", StringComparison.OrdinalIgnoreCase);
                BudgetOpinionPanel.Visible = budgetApprovalPending &&
                    (IsJeAdministrator || HasAnyRole("CAFOOfficer"));
                if (budgetApprovalPending)
                {
                    ApproveButton.Text = "Positive budget opinion";
                    RejectButton.Text = "Negative budget opinion — return for revision";
                }
                bool isAdministrator = IsJeAdministrator;
                EditTenderLink.Visible = (IsEditableDraft && isAdministrator) ||
                    (isBudgetRevision && !IsFinalWorkflow &&
                     (isAdministrator || HasAnyRole("DepartmentOfficer")));
                EditTenderLink.Text = isBudgetRevision ? "Revise proposal" : "Edit draft";
                EditTenderLink.NavigateUrl = "~/Pages/TenderForm.aspx?id=" + TenderId.ToString(CultureInfo.InvariantCulture);
                DeleteTenderButton.Visible = IsEditableDraft && isAdministrator;
                AddBoqPanel.Visible = CanManageTenderPreparation();
                UploadPanel.Visible = IsFinalWorkflow
                    ? CanUploadTenderDocuments()
                    : IsJeAdministrator || HasAnyRole("DepartmentOfficer");
                DocumentsGrid.Columns[6].Visible = UploadPanel.Visible && !IsDocumentCorrectionStage;
                DocumentTypesMissingPanel.Visible = UploadPanel.Visible &&
                    DocumentTypeSelect.Items.Count <= 1;
                bool acceptingBids = String.Equals(
                    TenderStatus, "Tender Published", StringComparison.OrdinalIgnoreCase) &&
                    IsPublicationWindowOpen;
                ApplyTenderLink.Visible = acceptingBids &&
                    (CurrentUserId == 0 || HasAnyRole("Contractor"));
                ApplicationMessage.Text = String.Empty;
                if (!acceptingBids)
                {
                    ApplicationMessage.Text = "<p class=\"muted\">Bid submission is available only during the approved publication period.</p>";
                }
                else if (CurrentUserId > 0 && !HasAnyRole("Contractor"))
                {
                    ApplicationMessage.Text = "<div class=\"message\">A contractor account is required to submit a bid.</div>";
                }
                if (ApplyTenderLink.Visible)
                {
                    ApplyTenderLink.NavigateUrl = CurrentUserId == 0
                        ? "~/Pages/Login.aspx?applyTenderId=" + TenderId.ToString(CultureInfo.InvariantCulture)
                        : "~/Pages/Bids.aspx?tenderId=" + TenderId.ToString(CultureInfo.InvariantCulture);
                    ApplyTenderLink.Text = CurrentUserId == 0 ? "Sign in to apply" : "Apply for this tender";
                }
                BoqGrid.Columns[6].Visible = AddBoqPanel.Visible;
                WorkflowTransition transition = IsFinalWorkflow ? null : WorkflowRules.ForStatus(TenderStatus);
                SubmitWorkflowButton.Visible = transition != null && transition.SubmitRoles.Length > 0 &&
                    WorkflowRolesAllowed(transition.SubmitRoles);
                if (String.Equals(TenderStatus, "Tender Published", StringComparison.OrdinalIgnoreCase))
                    SubmitWorkflowButton.Visible = false;
                ApproveButton.Visible = transition != null && transition.ApproverRoles.Length > 0 &&
                    WorkflowRolesAllowed(transition.ApproverRoles);
                RejectButton.Visible = ApproveButton.Visible;
                BidsLink.NavigateUrl = "~/Pages/Bids.aspx?tenderId=" + TenderId.ToString(CultureInfo.InvariantCulture);
                BidsLink.Visible = IsBidStageOrLater(TenderStatus);
                WorkLink.NavigateUrl = "~/Pages/WorkManagement.aspx?tenderId=" + TenderId.ToString(CultureInfo.InvariantCulture);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender detail could not be loaded.", ex);
                Message.Text = "<div class=\"message\">Tender details could not be loaded. Please retry or contact support.</div>";
            }
        }

        private void BindDocumentTypes(SqlConnection connection)
        {
            DocumentTypeSelect.Items.Clear();
            DocumentTypeSelect.Items.Add(new ListItem("Select document type", ""));
            using (SqlCommand command = new SqlCommand(
                "SELECT TypeName FROM dbo.DocumentTypes WHERE IsActive=1 ORDER BY SortOrder,TypeName;", connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string value = Convert.ToString(reader["TypeName"]);
                    DocumentTypeSelect.Items.Add(new ListItem(value, value));
                }
            }
            DocumentTypesMissingPanel.Visible = UploadPanel.Visible &&
                DocumentTypeSelect.Items.Count <= 1;
            UploadButton.Enabled = DocumentTypeSelect.Items.Count > 1;
        }

        private void BindChildTables(SqlConnection connection)
        {
            BindRequirementDocumentAttachments(connection);
            BindGrid(connection, BoqGrid,
                @"SELECT BOQId,ItemName,Description,Quantity,Unit,Rate,Amount FROM dbo.BOQ WHERE TenderId=@TenderId ORDER BY BOQId;");
            BindTenderDocuments(connection);
            BindGrid(connection, HistoryGrid,
                @"SELECT h.ApprovalStage,h.PreviousStatus,h.NewStatus,h.Status,h.RoleAtAction,h.Remarks,h.ApprovalDate,u.FullName AS ActionTakenBy
                  FROM dbo.ApprovalHistory h INNER JOIN dbo.Users u ON u.UserId=h.ApprovedBy
                  WHERE h.TenderId=@TenderId ORDER BY h.ApprovalDate;");
            BindGrid(connection, RevisionHistoryGrid,
                @"SELECT r.RevisionNumber,r.ActionName,r.RevisionReason,r.RevisedAtUtc,u.FullName AS RevisedBy
                  FROM dbo.TenderRevisions r INNER JOIN dbo.Users u ON u.UserId=r.RevisedBy
                  WHERE r.TenderId=@TenderId ORDER BY r.RevisionNumber DESC;");
            BindGrid(connection, BudgetOpinionsGrid,
                @"SELECT o.Opinion,o.ApprovedAmount,o.OpinionAtUtc,o.Remarks,u.FullName AS ActionTakenBy
                  FROM dbo.TenderBudgetOpinions o INNER JOIN dbo.Users u ON u.UserId=o.ApprovedBy
                  WHERE o.TenderId=@TenderId ORDER BY o.OpinionAtUtc DESC;");
            ApplicationRequirementsPanel.Visible = IsFinalWorkflow &&
                String.Equals(TenderStatus, "Tender Published", StringComparison.OrdinalIgnoreCase);
            if (ApplicationRequirementsPanel.Visible)
            {
                BindGrid(connection, ApplicationRequirementsGrid,
                    @"SELECT DocumentType,Description,IsMandatory,AllowedExtensions,MaxFileSizeBytes
                      FROM dbo.TenderRequiredBidDocuments WHERE TenderId=@TenderId ORDER BY DocumentType;");
            }
        }

        private void BindWorkflowTimeline(SqlConnection connection)
        {
            string currentStageName = null;
            string assignedOfficer = null;
            if (IsFinalWorkflow)
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT s.StageName,assignedUser.FullName AS AssignedOfficer
                      FROM dbo.TenderWorkflowAssignments a
                      INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                      LEFT JOIN dbo.Users assignedUser ON assignedUser.UserId=a.AssignedUserId
                      WHERE a.TenderId=@TenderId AND a.IsCurrent=1;", connection))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            currentStageName = Convert.ToString(reader["StageName"]);
                            assignedOfficer = reader["AssignedOfficer"] == DBNull.Value
                                ? null : Convert.ToString(reader["AssignedOfficer"]);
                        }
                    }
                }
            }

            DataTable history = new DataTable();
            string historyQuery = IsFinalWorkflow
                ? @"SELECT s.StageName,h.ActionName,h.NewStatus,h.Remarks,h.ActionAtUtc,
                           actor.FullName AS ActionBy,h.ActorRoleCode
                    FROM dbo.TenderWorkflowHistory h
                    LEFT JOIN dbo.WorkflowStages s ON s.StageId=h.StageId
                    INNER JOIN dbo.Users actor ON actor.UserId=h.UserId
                    WHERE h.TenderId=@TenderId AND h.WorkflowVersion=2
                    ORDER BY h.ActionAtUtc,h.WorkflowHistoryId;"
                : @"SELECT h.ApprovalStage AS StageName,h.ApprovalStage AS ActionName,
                           h.NewStatus,h.Remarks,h.ApprovalDate AS ActionAtUtc,
                           actor.FullName AS ActionBy,h.RoleAtAction AS ActorRoleCode
                    FROM dbo.ApprovalHistory h
                    INNER JOIN dbo.Users actor ON actor.UserId=h.ApprovedBy
                    WHERE h.TenderId=@TenderId
                    ORDER BY h.ApprovalDate;";
            using (SqlCommand command = new SqlCommand(historyQuery, connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                adapter.Fill(history);
            }

            int currentGroup = IsFinalWorkflow
                ? TimelineGroupForStage(currentStageName)
                : TimelineGroupForStatus(TenderStatus);
            if (String.Equals(TenderStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                currentGroup = 7;

            DataTable timeline = new DataTable();
            timeline.Columns.Add("StageNumber", typeof(int));
            timeline.Columns.Add("StageTitle", typeof(string));
            timeline.Columns.Add("ResponsibleRole", typeof(string));
            timeline.Columns.Add("AssignedOfficer", typeof(string));
            timeline.Columns.Add("Status", typeof(string));
            timeline.Columns.Add("StatusClass", typeof(string));
            timeline.Columns.Add("SubmissionDate", typeof(string));
            timeline.Columns.Add("ApprovalDate", typeof(string));
            timeline.Columns.Add("Remarks", typeof(string));
            timeline.Columns.Add("ActionHistory", typeof(string));
            timeline.Columns.Add("IsCurrent", typeof(bool));

            string[] titles =
            {
                "Initial document verification",
                "Tender Cell document verification",
                "Tender publication and technical-bid verification",
                "Financial bid approval",
                "Tender data entry",
                "Chief Audit approval",
                "Work Order and Tender Agreement"
            };
            string[] roles =
            {
                "Junior Engineer (JE)",
                "Nivida Kakas / Tender Cell",
                "JE / Tender Cell",
                "Nivida Samiti Adhyaksha",
                "Junior Engineer (JE)",
                "Chief Audit",
                "Junior Engineer (JE)"
            };

            for (int group = 1; group <= 7; group++)
            {
                List<DataRow> groupHistory = new List<DataRow>();
                foreach (DataRow entry in history.Rows)
                {
                    int entryGroup = TimelineGroupForStage(Convert.ToString(entry["StageName"]));
                    if (entryGroup == 0)
                        entryGroup = TimelineGroupForStatus(Convert.ToString(entry["NewStatus"]));
                    if (entryGroup == group) groupHistory.Add(entry);
                }

                DataRow latest = groupHistory.Count == 0 ? null : groupHistory[groupHistory.Count - 1];
                bool isCurrent = currentGroup == group && !String.Equals(
                    TenderStatus, "Completed", StringComparison.OrdinalIgnoreCase);
                string status;
                string statusClass;
                bool returned = latest != null &&
                    (Convert.ToString(latest["NewStatus"]).IndexOf("Reject", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     Convert.ToString(latest["NewStatus"]).IndexOf("Return", StringComparison.OrdinalIgnoreCase) >= 0);
                bool shortfall = isCurrent &&
                    ((TenderStatus ?? String.Empty).IndexOf("Shortfall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (currentStageName ?? String.Empty).IndexOf("Shortfall", StringComparison.OrdinalIgnoreCase) >= 0);

                if (shortfall)
                {
                    status = "Shortfall Raised";
                    statusClass = "shortfall";
                }
                else if (isCurrent)
                {
                    status = "Pending / Current Stage";
                    statusClass = "current";
                }
                else if (group < currentGroup || currentGroup == 7)
                {
                    status = "Completed";
                    statusClass = "completed";
                }
                else if (returned)
                {
                    status = "Rejected / Returned";
                    statusClass = "returned";
                }
                else
                {
                    status = "Upcoming";
                    statusClass = "upcoming";
                }

                string actionHistory = String.Empty;
                int firstHistory = Math.Max(0, groupHistory.Count - 6);
                for (int index = firstHistory; index < groupHistory.Count; index++)
                {
                    DataRow entry = groupHistory[index];
                    string actionDate = Convert.ToDateTime(entry["ActionAtUtc"], CultureInfo.InvariantCulture)
                        .ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
                    string line = actionDate + " — " + Convert.ToString(entry["ActionName"]) +
                        " (" + Convert.ToString(entry["NewStatus"]) + ")";
                    if (entry["Remarks"] != DBNull.Value &&
                        !String.IsNullOrWhiteSpace(Convert.ToString(entry["Remarks"])))
                        line += ": " + Convert.ToString(entry["Remarks"]);
                    actionHistory += (actionHistory.Length == 0 ? String.Empty : Environment.NewLine) + line;
                }

                string submissionDate = groupHistory.Count == 0
                    ? "—"
                    : Convert.ToDateTime(groupHistory[0]["ActionAtUtc"], CultureInfo.InvariantCulture)
                        .ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
                string approvalDate = statusClass == "completed" && latest != null
                    ? Convert.ToDateTime(latest["ActionAtUtc"], CultureInfo.InvariantCulture)
                        .ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)
                    : "—";
                string remarks = latest == null || latest["Remarks"] == DBNull.Value
                    ? "—" : Convert.ToString(latest["Remarks"]);
                string officer = isCurrent
                    ? (String.IsNullOrWhiteSpace(assignedOfficer) ? "Role queue" : assignedOfficer)
                    : "—";
                timeline.Rows.Add(group, titles[group - 1], roles[group - 1], officer,
                    status, statusClass, submissionDate, approvalDate, remarks, actionHistory, isCurrent);
            }

            string currentTitle = currentGroup >= 1 && currentGroup <= titles.Length
                ? roles[currentGroup - 1] + " — " + titles[currentGroup - 1]
                : TenderStatus;
            TrackerCurrentStage.Text = HttpUtility.HtmlEncode(
                String.Equals(TenderStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                    ? "TENDER PROCESS COMPLETED"
                    : "CURRENT STAGE: " + currentTitle);
            WorkflowTimeline.DataSource = timeline;
            WorkflowTimeline.DataBind();
        }

        private void BindDocumentShortfallSummary(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT TOP (1) sf.RaisedAtUtc,sf.DeadlineAtUtc,sf.Remarks,sf.RequiredDocuments,
                         sf.ResolutionStatus,sf.ResubmittedAtUtc,raiser.FullName AS RaisedBy
                  FROM dbo.TenderDocumentShortfalls sf
                  INNER JOIN dbo.Users raiser ON raiser.UserId=sf.RaisedBy
                  WHERE sf.TenderId=@TenderId
                  ORDER BY sf.RaisedAtUtc DESC,sf.TenderDocumentShortfallId DESC;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        CurrentDocumentShortfallPanel.Visible = false;
                        return;
                    }

                    CurrentDocumentShortfallPanel.Visible = true;
                    DocumentShortfallSummary.Text =
                        "<strong>DOCUMENT SHORTFALL RAISED</strong><dl>" +
                        "<dt>Raised by</dt><dd>" + HttpUtility.HtmlEncode(Convert.ToString(reader["RaisedBy"])) +
                        "</dd><dt>Raised date</dt><dd>" +
                        HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["RaisedAtUtc"])) +
                        " IST</dd><dt>Deadline (3 days)</dt><dd>" +
                        HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["DeadlineAtUtc"])) +
                        " IST</dd><dt>Current status</dt><dd>" +
                        HttpUtility.HtmlEncode(Convert.ToString(reader["ResolutionStatus"])) +
                        "</dd><dt>JE resubmission</dt><dd>" +
                        (reader["ResubmittedAtUtc"] == DBNull.Value ? "Pending" :
                            HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["ResubmittedAtUtc"])) + " IST") +
                        "</dd><dt>Required documents</dt><dd>" +
                        HttpUtility.HtmlEncode(Convert.ToString(reader["RequiredDocuments"])) +
                        "</dd><dt>Remarks</dt><dd>" +
                        HttpUtility.HtmlEncode(Convert.ToString(reader["Remarks"])) +
                        "</dd></dl>";
                }
            }
        }

        private static int TimelineGroupForStage(string stageName)
        {
            if (String.IsNullOrWhiteSpace(stageName)) return 0;
            if (stageName == "Final_JE_SubmitTender") return 1;
            if (stageName == "Final_Cell_InitialTenderDocumentVerification" ||
                stageName == "Final_JE_CorrectTenderDocuments") return 2;
            if (stageName == "Final_JE_PublishTender" ||
                stageName == "Final_JE_OpenTechnicalBids" ||
                stageName == "Final_JE_CompletePrimaryVerification" ||
                stageName == "Final_JE_ForwardToTenderCell" ||
                stageName == "Final_Cell_CompleteSecondaryVerification" ||
                stageName == "Final_Cell_CreateShortfall" ||
                stageName == "Final_JE_ApproveShortfall" ||
                stageName == "Final_Contractor_SubmitShortfall" ||
                stageName == "Final_Cell_ClearShortfall" ||
                stageName == "Final_Cell_ResumeVerification" ||
                stageName == "Final_Cell_ReverifyRejectedOpening")
                return 3;
            if (stageName == "Final_Samiti_ApproveFinancialOpening") return 4;
            if (stageName == "Final_JE_OpenFinancialBids" ||
                stageName == "Final_JE_ResubmitFinancialApproval")
                return 5;
            if (stageName == "Final_Auditor_ApproveFinancialBids") return 6;
            if (stageName != null && stageName.StartsWith("Final_JE_", StringComparison.Ordinal) &&
                (stageName.IndexOf("WorkOrder", StringComparison.Ordinal) >= 0 ||
                 stageName.IndexOf("Agreement", StringComparison.Ordinal) >= 0 ||
                 stageName == "Final_JE_CompleteTender"))
                return 7;
            return 0;
        }

        private static int TimelineGroupForStatus(string status)
        {
            if (String.IsNullOrWhiteSpace(status)) return 0;
            if (status == "Draft" || status == "Submitted")
                return 1;
            if (status.IndexOf("Pending JE Document Correction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Pending Tender Cell Verification", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            if (status == "Tender Published" ||
                status.IndexOf("Primary Verification", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Tender Cell Verification", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Shortfall", StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            if (status.IndexOf("Financial Bid Opening", StringComparison.OrdinalIgnoreCase) >= 0)
                return 4;
            if (status.IndexOf("Final Financial Approval Rejected", StringComparison.OrdinalIgnoreCase) >= 0)
                return 5;
            if (status.IndexOf("Tender Cell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Shortfall", StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            if (status.IndexOf("Final Financial Approval Pending", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Final Financial Approval Completed", StringComparison.OrdinalIgnoreCase) >= 0)
                return 6;
            if (status.IndexOf("Financial Approval", StringComparison.OrdinalIgnoreCase) >= 0)
                return 5;
            if (status.IndexOf("Work Order", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Agreement", StringComparison.OrdinalIgnoreCase) >= 0 ||
                status == "Completed")
                return 7;
            return 0;
        }

        private void DenyTenderAccess(SqlConnection connection)
        {
            bool exists;
            using (SqlCommand command = new SqlCommand(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Tender WHERE TenderId=@TenderId AND IsDeleted=0) THEN 1 ELSE 0 END;",
                connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                exists = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }

            if (exists && CurrentUserId > 0 && !HasAnyRole("Contractor"))
            {
                Response.StatusCode = 403;
                Response.TrySkipIisCustomErrors = true;
                ShowError("Your account is not authorized to view this tender based on its department or current workflow assignment.");
                return;
            }

            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            ShowError("This tender is not available.");
        }

        private void BindRequirementDocumentAttachments(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.DocumentId,COALESCE(dt.TypeName,d.DocumentType) AS DocumentType,d.FileName
                  FROM dbo.TenderDocuments d
                  LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                  WHERE d.TenderId=@TenderId
                    AND COALESCE(dt.TypeName,d.DocumentType) IN
                        (N'Scope of Work',N'Technical Requirements',N'Financial Requirements')
                  ORDER BY d.UploadedDate DESC,d.DocumentId DESC;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    HashSet<string> linkedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    while (reader.Read())
                    {
                        string documentType = Convert.ToString(reader["DocumentType"]).Trim();
                        if (!linkedTypes.Add(documentType)) continue;

                        int documentId = Convert.ToInt32(reader["DocumentId"], CultureInfo.InvariantCulture);
                        string fileName = Path.GetFileName(Convert.ToString(reader["FileName"]));
                        switch (documentType.ToLowerInvariant())
                        {
                            case "scope of work":
                                SetRequirementDocumentAttachment(ScopeOfWorkAttachmentPanel,
                                    ScopeOfWorkAttachmentStatus,
                                    ScopeOfWorkAttachmentViewLink, ScopeOfWorkAttachmentDownloadLink,
                                    documentId, fileName, "Scope of Work");
                                break;
                            case "technical requirements":
                                SetRequirementDocumentAttachment(TechnicalRequirementsAttachmentPanel,
                                    TechnicalRequirementsAttachmentStatus,
                                    TechnicalRequirementsAttachmentViewLink, TechnicalRequirementsAttachmentDownloadLink,
                                    documentId, fileName, "Technical Requirements");
                                break;
                            case "financial requirements":
                                SetRequirementDocumentAttachment(FinancialRequirementsAttachmentPanel,
                                    FinancialRequirementsAttachmentStatus,
                                    FinancialRequirementsAttachmentViewLink, FinancialRequirementsAttachmentDownloadLink,
                                    documentId, fileName, "Financial Requirements");
                                break;
                        }
                    }
                }
            }
        }

        private void SetRequirementDocumentAttachment(Panel panel, Literal status, HyperLink viewLink,
            HyperLink downloadLink, int documentId, string fileName, string documentType)
        {
            string documentUrl = ResolveUrl("~/Pages/DocumentDownload.aspx?id=" +
                documentId.ToString(CultureInfo.InvariantCulture));
            panel.Visible = true;
            status.Visible = false;
            viewLink.Text = HttpUtility.HtmlEncode(fileName);
            viewLink.NavigateUrl = documentUrl + "&preview=true";
            viewLink.Visible = true;
            downloadLink.NavigateUrl = documentUrl;
            downloadLink.Visible = true;
            downloadLink.Attributes["aria-label"] = "Download " + documentType + " document";
            downloadLink.Attributes["title"] = "Download " + documentType + " document";
        }

        private void BindTenderDocuments(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.DocumentId,d.DocumentTypeId,COALESCE(dt.TypeName,d.DocumentType) AS DocumentType,
                         d.DocumentName,d.FileName,d.FilePath,d.FileExtension,d.FileSizeBytes,d.Remarks,d.UploadedDate
                  FROM dbo.TenderDocuments d
                  LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                  WHERE d.TenderId=@TenderId
                  ORDER BY d.UploadedDate,d.DocumentId;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable documents = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(documents);
                documents.Columns.Add("DocumentStatus", typeof(string));
                foreach (DataRow document in documents.Rows)
                {
                    string fullPath;
                    document["DocumentStatus"] =
                        TenderLifecycleFiles.TryResolveStoredFilePath(this, Convert.ToString(document["FilePath"]), out fullPath)
                            ? "Uploaded"
                            : "Missing file";
                }
                DocumentsGrid.DataSource = documents;
                DocumentsGrid.DataBind();
            }
        }

        protected void DocumentsGrid_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType != DataControlRowType.DataRow) return;
            Literal serial = e.Row.FindControl("DocumentSerial") as Literal;
            if (serial != null)
                serial.Text = (DocumentsGrid.PageIndex * DocumentsGrid.PageSize + e.Row.RowIndex + 1)
                    .ToString(CultureInfo.InvariantCulture);
        }

        protected void DocumentsGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            DocumentsGrid.PageIndex = e.NewPageIndex;
            LoadTender();
        }

        protected void DocumentsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "EditDocument" && e.CommandName != "ReplaceDocument" &&
                e.CommandName != "DeleteDocument") return;
            if (!RequireTenderAccess() || !CanManageTenderDocuments())
            {
                ShowError("Your role is not authorized to manage documents for this tender.");
                return;
            }

            int documentId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), out documentId) ||
                documentId <= 0)
            {
                ShowError("The selected document is invalid.");
                return;
            }

            if (e.CommandName == "DeleteDocument")
            {
                DeleteTenderDocument(documentId);
                return;
            }

            LoadDocumentForEdit(documentId, e.CommandName == "ReplaceDocument");
        }

        protected void ShowDocumentFormButton_Click(object sender, EventArgs e)
        {
            if (!RequireTenderAccess() || !CanUploadTenderDocuments()) return;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                    BindDocumentTypes(connection);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document types could not be loaded for upload.", ex);
                ShowError("Document types could not be loaded. Please retry.");
                return;
            }
            ResetDocumentForm();
            DocumentFormTitle.Text = "Upload tender document";
            DocumentFilePanel.Visible = true;
            DocumentFormPanel.Visible = true;
        }

        protected void CancelDocumentFormButton_Click(object sender, EventArgs e)
        {
            if (!RequireTenderAccess() || !CanUploadTenderDocuments()) return;
            ResetDocumentForm();
            DocumentFormPanel.Visible = false;
        }

        protected void RefreshDocumentTypesButton_Click(object sender, EventArgs e)
        {
            if (!RequireTenderAccess() || !CanUploadTenderDocuments()) return;
            string selectedType = DocumentTypeSelect.SelectedValue;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                    BindDocumentTypes(connection);
                ListItem selected = DocumentTypeSelect.Items.FindByValue(selectedType);
                if (selected != null) DocumentTypeSelect.SelectedValue = selectedType;
                DocumentTypesMissingPanel.Visible = DocumentTypeSelect.Items.Count <= 1;
                Message.Text = DocumentTypesMissingPanel.Visible
                    ? "<div class=\"message\">There are no active document types. Add or activate one in Master Management.</div>"
                    : "<div class=\"message success\">Document types refreshed from the database.</div>";
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document types could not be refreshed.", ex);
                ShowError("Document types could not be refreshed. Please retry.");
            }
        }

        private void LoadDocumentForEdit(int documentId, bool replaceFile)
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT d.DocumentName,d.DocumentType,d.Remarks,d.DocumentTypeId
                      FROM dbo.TenderDocuments d
                      WHERE d.DocumentId=@DocumentId AND d.TenderId=@TenderId;", connection))
                {
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            ShowError("The selected document no longer exists on this tender.");
                            return;
                        }
                        DocumentNameText.Text = Convert.ToString(reader["DocumentName"]);
                        DocumentRemarksText.Text = reader["Remarks"] == DBNull.Value
                            ? String.Empty
                            : Convert.ToString(reader["Remarks"]);
                        string documentType = Convert.ToString(reader["DocumentType"]);
                        DocumentTypeSelect.ClearSelection();
                        ListItem typeItem = DocumentTypeSelect.Items.FindByValue(documentType);
                        if (typeItem != null) typeItem.Selected = true;
                        else if (DocumentTypeSelect.Items.Count > 0) DocumentTypeSelect.SelectedIndex = 0;
                    }
                }
                EditingDocumentId.Value = documentId.ToString(CultureInfo.InvariantCulture);
                ReplacingDocumentFile.Value = replaceFile ? "true" : "false";
                DocumentFormTitle.Text = replaceFile ? "Replace tender document file" : "Edit tender document details";
                DocumentFilePanel.Visible = replaceFile;
                DocumentFormPanel.Visible = true;
                UploadButton.Text = replaceFile ? "Save replacement" : "Save changes";
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document details could not be loaded for editing.", ex);
                ShowError("The selected document could not be loaded for editing.");
            }
        }

        private void ResetDocumentForm()
        {
            EditingDocumentId.Value = String.Empty;
            ReplacingDocumentFile.Value = "false";
            DocumentNameText.Text = String.Empty;
            DocumentRemarksText.Text = String.Empty;
            DocumentFilePanel.Visible = true;
            UploadButton.Text = "Save document";
            if (DocumentTypeSelect.Items.Count > 0) DocumentTypeSelect.SelectedIndex = 0;
        }

        private static string FormatMoney(object value)
        {
            return value == null || value == DBNull.Value
                ? "—"
                : "₹" + Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("N2", CultureInfo.CurrentCulture);
        }

        private static string FormatCoordinate(object value)
        {
            return value == null || value == DBNull.Value
                ? "—"
                : Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                    .ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatArea(object value)
        {
            if (value == null || value == DBNull.Value) return "—";
            decimal squareMeters = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            return squareMeters >= 10000m
                ? (squareMeters / 10000m).ToString("N2", CultureInfo.CurrentCulture) + " hectares"
                : squareMeters.ToString("N2", CultureInfo.CurrentCulture) + " m²";
        }

        private static string FormatIndiaDate(object value)
        {
            return value == null || value == DBNull.Value
                ? "—"
                : HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(value)) + " IST";
        }

        private void BindGrid(SqlConnection connection, GridView grid, string sql)
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable table = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(table);
                grid.DataSource = table;
                grid.DataBind();
            }
        }

        protected void AddBoqButton_Click(object sender, EventArgs e)
        {
            if (!RequireTenderAccess()) return;
            if (!CanManageTenderPreparation())
            {
                ShowError("BOQ items can only be changed by an authorized department user while the tender is editable.");
                return;
            }
            decimal quantity, rate;
            if (String.IsNullOrWhiteSpace(ItemName.Text) || ItemName.Text.Trim().Length > 200 ||
                ItemDescription.Text.Length > 500 || String.IsNullOrWhiteSpace(Unit.Text) || Unit.Text.Trim().Length > 20 ||
                !Decimal.TryParse(Quantity.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity) || quantity <= 0 ||
                !Decimal.TryParse(Rate.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out rate) || rate <= 0)
            {
                ShowError("Enter an item name (maximum 200 characters), unit (maximum 20 characters), positive quantity and rate, and valid description.");
                return;
            }
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.BOQ (TenderId,ItemName,Description,Quantity,Unit,Rate)
                      VALUES (@TenderId,@ItemName,@Description,@Quantity,@Unit,@Rate);
                      SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    AddText(command, "@ItemName", SqlDbType.NVarChar, ItemName.Text, 200);
                    AddText(command, "@Description", SqlDbType.NVarChar, ItemDescription.Text, 500);
                    ModuleDb.AddParameter(command, "@Quantity", SqlDbType.Decimal, quantity);
                    AddText(command, "@Unit", SqlDbType.NVarChar, Unit.Text, 20);
                    ModuleDb.AddParameter(command, "@Rate", SqlDbType.Decimal, rate);
                    int boqId = Convert.ToInt32(command.ExecuteScalar());
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Create", "BOQItem", boqId, ItemName.Text.Trim(), Request.UserHostAddress);
                    transaction.Commit();
                }
                Message.Text = "<div class=\"message success\">BOQ item added.</div>";
                LoadTender();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "BOQ item insert failed.", ex);
                ShowError("The BOQ item could not be saved.");
            }
        }

        protected void BoqGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "RemoveItem") return;
            if (!RequireTenderAccess()) return;
            int boqId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out boqId) || boqId <= 0 ||
                !CanManageTenderPreparation())
            {
                ShowError("This BOQ item cannot be removed.");
                return;
            }
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand("DELETE FROM dbo.BOQ WHERE BOQId=@BOQId AND TenderId=@TenderId;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@BOQId", SqlDbType.Int, boqId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("The requested BOQ item was not found.");
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Delete", "BOQItem", boqId, null, Request.UserHostAddress);
                    transaction.Commit();
                }
                LoadTender();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "BOQ item delete failed.", ex);
                ShowError("The BOQ item could not be removed.");
            }
        }

        protected void UploadButton_Click(object sender, EventArgs e)
        {
            int editingDocumentId;
            bool isEditing = Int32.TryParse(EditingDocumentId.Value, out editingDocumentId) &&
                editingDocumentId > 0;
            if (!RequireTenderAccess() ||
                (isEditing ? !CanManageTenderDocuments() : !CanUploadTenderDocuments()))
            {
                ShowError("Your role is not authorized to manage documents for this tender.");
                return;
            }
            Page.Validate("TenderDocument");
            if (!Page.IsValid) return;

            bool isReplacement = String.Equals(ReplacingDocumentFile.Value, "true", StringComparison.OrdinalIgnoreCase);
            bool hasFile = DocumentFile.HasFile;
            if (DocumentNameText.Text.Trim().Length == 0 || DocumentNameText.Text.Trim().Length > 250 ||
                DocumentRemarksText.Text.Length > 1000 ||
                (!isEditing && !hasFile) || (isReplacement && !hasFile))
            {
                ShowError("Enter a document name and select a file for a new upload or replacement.");
                return;
            }

            string originalName = null;
            string extension = null;
            long fileSize = 0;
            if (hasFile)
            {
                originalName = Path.GetFileName(DocumentFile.FileName);
                extension = Path.GetExtension(originalName).ToLowerInvariant();
                string[] allowed = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
                fileSize = DocumentFile.PostedFile.ContentLength;
                if (originalName.Length == 0 || originalName.Length > 260 || fileSize <= 0 ||
                    fileSize > 20 * 1024 * 1024 || Array.IndexOf(allowed, extension) < 0 ||
                    !TenderLifecycleFiles.HasAllowedContentSignature(DocumentFile.PostedFile.InputStream, extension))
                {
                    ShowError("Choose a valid PDF, Office document, JPEG, or PNG no larger than 20 MB.");
                    return;
                }
            }

            string savedFullPath = null;
            string savedRelativePath = null;
            string oldStoredPath = null;
            string oldFileName = null;
            bool committed = false;
            try
            {
                if (hasFile)
                {
                    string storedName = Guid.NewGuid().ToString("N") + extension;
                    string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
                    string folder = Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/tenders/" +
                        TenderId.ToString(CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(folder);
                    savedFullPath = Path.Combine(folder, storedName);
                    savedRelativePath = "Uploads/tenders/" +
                        TenderId.ToString(CultureInfo.InvariantCulture) + "/" + storedName;
                    DocumentFile.SaveAs(savedFullPath);
                }

                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    if (!isEditing && IsDocumentCorrectionStage)
                        EnsureDocumentCorrectionStage(connection, transaction);

                    int documentTypeId;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT DocumentTypeId FROM dbo.DocumentTypes WITH (UPDLOCK,HOLDLOCK)
                          WHERE TypeName=@TypeName AND IsActive=1;", connection, transaction))
                    {
                        AddText(command, "@TypeName", SqlDbType.NVarChar, DocumentTypeSelect.SelectedValue, 80);
                        object result = command.ExecuteScalar();
                        if (result == null || result == DBNull.Value)
                            throw new InvalidOperationException("Select an active document type. Refresh the list and retry.");
                        documentTypeId = Convert.ToInt32(result, CultureInfo.InvariantCulture);
                    }

                    int documentId;
                    string auditAction;
                    string auditDetails;
                    if (isEditing)
                    {
                        using (SqlCommand command = new SqlCommand(
                            @"SELECT FilePath,FileName FROM dbo.TenderDocuments WITH (UPDLOCK,HOLDLOCK)
                              WHERE DocumentId=@DocumentId AND TenderId=@TenderId;", connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, editingDocumentId);
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("The selected document no longer exists on this tender.");
                                oldStoredPath = Convert.ToString(reader["FilePath"]);
                                oldFileName = Convert.ToString(reader["FileName"]);
                            }
                        }

                        using (SqlCommand command = new SqlCommand(
                            @"UPDATE dbo.TenderDocuments
                              SET DocumentTypeId=@DocumentTypeId,DocumentType=@DocumentType,
                                  DocumentName=@DocumentName,Remarks=@Remarks,
                                  FileName=CASE WHEN @HasFile=1 THEN @FileName ELSE FileName END,
                                  FilePath=CASE WHEN @HasFile=1 THEN @FilePath ELSE FilePath END,
                                  FileExtension=CASE WHEN @HasFile=1 THEN @FileExtension ELSE FileExtension END,
                                  FileSizeBytes=CASE WHEN @HasFile=1 THEN @FileSizeBytes ELSE FileSizeBytes END
                              WHERE DocumentId=@DocumentId AND TenderId=@TenderId;", connection, transaction))
                        {
                            AddTenderDocumentParameters(command, documentTypeId, originalName, savedRelativePath,
                                extension, fileSize, hasFile);
                            ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, editingDocumentId);
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("The selected document could not be updated.");
                        }
                        documentId = editingDocumentId;
                        auditAction = hasFile ? "ReplaceFile" : "Update";
                        auditDetails = String.Format(CultureInfo.InvariantCulture,
                            "TenderId={0}; type={1}; name={2}; previous file={3}; new file={4}",
                            TenderId, DocumentTypeSelect.SelectedValue, DocumentNameText.Text.Trim(),
                            hasFile ? oldFileName : String.Empty, hasFile ? originalName : String.Empty);
                    }
                    else
                    {
                        using (SqlCommand command = new SqlCommand(
                            @"INSERT INTO dbo.TenderDocuments
                                (TenderId,DocumentTypeId,DocumentType,DocumentName,Remarks,FileName,FilePath,
                                 FileExtension,FileSizeBytes,UploadedBy)
                              VALUES
                                (@TenderId,@DocumentTypeId,@DocumentType,@DocumentName,@Remarks,@FileName,@FilePath,
                                 @FileExtension,@FileSizeBytes,@UploadedBy);
                              SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                        {
                            AddTenderDocumentParameters(command, documentTypeId, originalName, savedRelativePath,
                                extension, fileSize, true);
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                            ModuleDb.AddParameter(command, "@UploadedBy", SqlDbType.Int, CurrentUserId);
                            documentId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        }
                        auditAction = "Upload";
                        auditDetails = String.Format(CultureInfo.InvariantCulture,
                            "TenderId={0}; type={1}; name={2}; file={3}",
                            TenderId, DocumentTypeSelect.SelectedValue, DocumentNameText.Text.Trim(), originalName);
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, auditAction,
                        "TenderDocument", documentId, auditDetails, Request.UserHostAddress);
                    transaction.Commit();
                    committed = true;
                }

                bool oldFileRemoved = !hasFile || DeleteStoredTenderDocument(oldStoredPath);
                ResetDocumentForm();
                DocumentFormPanel.Visible = false;
                LoadTender();
                Message.Text = oldFileRemoved
                    ? "<div class=\"message success\">Tender document saved.</div>"
                    : "<div class=\"message\">The document was saved, but the previous file could not be removed. Contact support to clean up the unused file.</div>";
            }
            catch (SqlException ex)
            {
                if (!committed) CleanupNewDocumentFile(savedFullPath);
                Trace.Warn("DepartmentModule", "Tender document database operation failed.", ex);
                ShowError("The tender document could not be saved. Please retry.");
            }
            catch (IOException ex)
            {
                if (!committed) CleanupNewDocumentFile(savedFullPath);
                Trace.Warn("DepartmentModule", "Tender document file operation failed.", ex);
                ShowError("The tender document file could not be saved. Please retry.");
            }
            catch (UnauthorizedAccessException ex)
            {
                if (!committed) CleanupNewDocumentFile(savedFullPath);
                Trace.Warn("DepartmentModule", "Tender document storage access was denied.", ex);
                ShowError("The file storage location is not available to the application. Contact support.");
            }
            catch (InvalidOperationException ex)
            {
                if (!committed) CleanupNewDocumentFile(savedFullPath);
                ShowError(ex.Message);
            }
        }

        private bool CanManageTenderDocuments()
        {
            return IsFinalWorkflow
                ? CanManageTenderPreparation()
                : IsJeAdministrator || HasAnyRole("DepartmentOfficer");
        }

        private bool CanUploadTenderDocuments()
        {
            return IsFinalWorkflow
                ? CanManageTenderPreparation() || (IsJeAdministrator && IsDocumentCorrectionStage)
                : IsJeAdministrator || HasAnyRole("DepartmentOfficer");
        }

        private void EnsureDocumentCorrectionStage(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
                  INNER JOIN dbo.TenderWorkflowAssignments a WITH (UPDLOCK,HOLDLOCK)
                    ON a.TenderId=t.TenderId AND a.IsCurrent=1 AND a.DepartmentId=t.DepartmentId
                  INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                  INNER JOIN dbo.TenderDocumentShortfalls sf WITH (UPDLOCK,HOLDLOCK)
                    ON sf.TenderDocumentShortfallId=a.ShortfallId AND sf.TenderId=t.TenderId
                  WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                    AND t.CurrentStatus=N'Pending JE Document Correction'
                    AND a.AssignedRoleId=@RoleId
                    AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                    AND @DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId
                    AND s.StageName=N'Final_JE_CorrectTenderDocuments'
                    AND sf.ResolutionStatus=N'Open'
                    AND sf.DeadlineAtUtc>=SYSUTCDATETIME();", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                    throw new InvalidOperationException("The tender correction stage is no longer active or its 3-day deadline has passed.");
            }
        }

        private void DeleteTenderDocument(int documentId)
        {
            string storedPath = null;
            string fileName = null;
            string documentName = null;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT FilePath,FileName,DocumentName
                          FROM dbo.TenderDocuments WITH (UPDLOCK,HOLDLOCK)
                          WHERE DocumentId=@DocumentId AND TenderId=@TenderId;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("The selected document no longer exists on this tender.");
                            storedPath = Convert.ToString(reader["FilePath"]);
                            fileName = Convert.ToString(reader["FileName"]);
                            documentName = Convert.ToString(reader["DocumentName"]);
                        }
                    }

                    using (SqlCommand command = new SqlCommand(
                        "DELETE FROM dbo.TenderDocuments WHERE DocumentId=@DocumentId AND TenderId=@TenderId;",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The selected document could not be deleted.");
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Delete",
                        "TenderDocument", documentId,
                        String.Format(CultureInfo.InvariantCulture,
                            "TenderId={0}; name={1}; file={2}", TenderId, documentName, fileName),
                        Request.UserHostAddress);
                    transaction.Commit();
                }

                DocumentsGrid.PageIndex = 0;
                bool fileRemoved = DeleteStoredTenderDocument(storedPath);
                LoadTender();
                Message.Text = fileRemoved
                    ? "<div class=\"message success\">Tender document deleted.</div>"
                    : "<div class=\"message\">The document record was deleted, but its file could not be removed. Contact support to clean up the unused file.</div>";
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document deletion failed.", ex);
                ShowError("The tender document could not be deleted. Please retry.");
            }
            catch (IOException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document file deletion failed.", ex);
                ShowError("The document record was removed, but the file could not be deleted. Contact support.");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document file deletion was denied.", ex);
                ShowError("The document record was removed, but the file could not be deleted. Contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
        }

        private void AddTenderDocumentParameters(SqlCommand command, int documentTypeId,
            string originalName, string relativePath, string extension, long fileSize, bool hasFile)
        {
            ModuleDb.AddParameter(command, "@DocumentTypeId", SqlDbType.Int, documentTypeId);
            AddText(command, "@DocumentType", SqlDbType.NVarChar, DocumentTypeSelect.SelectedValue, 80);
            AddText(command, "@DocumentName", SqlDbType.NVarChar, DocumentNameText.Text, 250);
            AddText(command, "@Remarks", SqlDbType.NVarChar, DocumentRemarksText.Text, 1000);
            AddText(command, "@FileName", SqlDbType.NVarChar, originalName, 260);
            AddText(command, "@FilePath", SqlDbType.NVarChar, relativePath, 400);
            AddText(command, "@FileExtension", SqlDbType.NVarChar, extension, 10);
            ModuleDb.AddParameter(command, "@FileSizeBytes", SqlDbType.BigInt, hasFile ? (object)fileSize : null);
            ModuleDb.AddParameter(command, "@HasFile", SqlDbType.Bit, hasFile);
        }

        private bool DeleteStoredTenderDocument(string storedPath)
        {
            string fullPath;
            if (!TenderLifecycleFiles.TryResolveStoredFilePath(this, storedPath, out fullPath)) return true;
            try
            {
                File.Delete(fullPath);
                return true;
            }
            catch (IOException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document file could not be removed from storage.", ex);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document file removal was denied.", ex);
                return false;
            }
        }

        private void CleanupNewDocumentFile(string fullPath)
        {
            if (String.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) return;
            try
            {
                File.Delete(fullPath);
            }
            catch (IOException ex)
            {
                Trace.Warn("DepartmentModule", "An uncommitted tender document file could not be cleaned up.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.Warn("DepartmentModule", "Cleanup of an uncommitted tender document file was denied.", ex);
            }
        }

        protected void SubmitWorkflowButton_Click(object sender, EventArgs e) { SaveTransition("Submitted"); }
        protected void ApproveButton_Click(object sender, EventArgs e) { SaveTransition("Approved"); }
        protected void RejectButton_Click(object sender, EventArgs e) { SaveTransition("Rejected"); }

        protected void DeleteTenderButton_Click(object sender, EventArgs e)
        {
            if (!RequireTenderAccess()) return;
            if (!IsJeAdministrator ||
                !IsEditableDraft || !String.Equals(TenderStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Only an administrator can delete a draft at its initial workflow stage.");
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE t
                          SET IsDeleted=1,ModifiedBy=@ModifiedBy,ModifiedDate=SYSUTCDATETIME()
                          FROM dbo.Tender t
                          WHERE t.TenderId=@TenderId AND t.CurrentStatus=N'Draft' AND t.IsDeleted=0
                            AND (t.WorkflowVersion=1 OR
                                 (t.WorkflowVersion=2 AND EXISTS
                                     (SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                      INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                      WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                        AND s.StageName=N'Final_JE_SubmitTender')));", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@ModifiedBy", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Only an initial-stage draft tender can be deleted.");
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Delete", "Tender",
                        TenderId, "Soft deleted draft tender", Request.UserHostAddress);
                    transaction.Commit();
                }

                Response.Redirect("~/Pages/Tenders.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender deletion failed.", ex);
                ShowError("The tender could not be deleted. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
        }

        private void SaveTransition(string action)
        {
            if (!RequireTenderAccess()) return;
            if (String.Equals(TenderStatus, "Tender Published", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Bid opening is managed by the authorized opening workflow after the approved publication closing time.");
                return;
            }
            if (IsFinalWorkflow)
            {
                ShowError("This tender uses the assigned workflow. Use the assigned workflow queue to submit or approve it.");
                return;
            }
            WorkflowTransition transition = WorkflowRules.ForStatus(TenderStatus);
            if (transition == null)
            {
                ShowError("No workflow action is available for the current status.");
                return;
            }

            bool isSubmit = action == "Submitted";
            bool isBudgetOpinion = String.Equals(TenderStatus, "Budget Approval Pending", StringComparison.OrdinalIgnoreCase) &&
                (IsJeAdministrator || HasAnyRole("CAFOOfficer"));
            decimal? approvedBudgetAmount = null;
            if (isBudgetOpinion && action == "Approved")
            {
                decimal amount;
                if (!Decimal.TryParse(ApprovedBudgetAmount.Text, NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out amount) || amount <= 0 ||
                    Decimal.Round(amount, 2) != amount || amount > 9999999999999999.99m)
                {
                    ShowError("Enter a positive approved budget amount with no more than two decimal places.");
                    return;
                }
                approvedBudgetAmount = amount;
            }
            if (isBudgetOpinion && action == "Rejected" && String.IsNullOrWhiteSpace(Remarks.Text))
            {
                ShowError("Enter the reason and remarks for a negative budget opinion.");
                return;
            }
            string[] allowedRoles = isSubmit ? transition.SubmitRoles : transition.ApproverRoles;
            if (allowedRoles.Length == 0 || !WorkflowRolesAllowed(allowedRoles))
            {
                ShowError("Your role is not authorized to perform this workflow action.");
                return;
            }
            if (Remarks.Text.Length > 1000)
            {
                ShowError("Remarks must be no longer than 1,000 characters.");
                return;
            }
            string nextStatus = action == "Rejected"
                ? (isBudgetOpinion ? "Budget Revision Required" : transition.RejectStatus)
                : transition.NextStatus;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    string previousStatus;
                    using (SqlCommand command = new SqlCommand("SELECT CurrentStatus,WorkflowVersion FROM dbo.Tender WITH (UPDLOCK, ROWLOCK) WHERE TenderId=@TenderId AND IsDeleted=0;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read()) throw new InvalidOperationException("Tender no longer exists.");
                            previousStatus = Convert.ToString(reader["CurrentStatus"]);
                            if (Convert.ToInt16(reader["WorkflowVersion"], CultureInfo.InvariantCulture) != 1)
                                throw new InvalidOperationException("This tender uses the assigned workflow. Use the assigned workflow queue.");
                        }
                    }
                    if (!String.Equals(previousStatus, TenderStatus, StringComparison.Ordinal))
                        throw new InvalidOperationException("Tender status changed. Refresh the page before acting.");

                    if (isSubmit && String.Equals(TenderStatus, "Tender Draft Created", StringComparison.OrdinalIgnoreCase))
                        EnsureTenderPreparationComplete(connection, transaction);

                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.Tender SET CurrentStatus=@NewStatus,ModifiedBy=@UserId,ModifiedDate=SYSUTCDATETIME()
                          WHERE TenderId=@TenderId AND CurrentStatus=@PreviousStatus AND WorkflowVersion=1 AND IsDeleted=0;", connection, transaction))
                    {
                        AddText(command, "@NewStatus", SqlDbType.NVarChar, nextStatus, 50);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        AddText(command, "@PreviousStatus", SqlDbType.NVarChar, previousStatus, 50);
                        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Tender status changed. Refresh the page before acting.");
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.ApprovalHistory (TenderId,ApprovalStage,PreviousStatus,NewStatus,Status,ApprovedBy,RoleAtAction,Remarks)
                          VALUES (@TenderId,@Stage,@PreviousStatus,@NewStatus,@Action,@UserId,@Role,@Remarks);", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        AddText(command, "@Stage", SqlDbType.NVarChar, transition.Stage, 80);
                        AddText(command, "@PreviousStatus", SqlDbType.NVarChar, previousStatus, 50);
                        AddText(command, "@NewStatus", SqlDbType.NVarChar, nextStatus, 50);
                        AddText(command, "@Action", SqlDbType.NVarChar, action, 20);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        AddText(command, "@Role", SqlDbType.NVarChar, CurrentRole, 50);
                        AddText(command, "@Remarks", SqlDbType.NVarChar, Remarks.Text, 1000);
                        command.ExecuteNonQuery();
                    }
                    if (isBudgetOpinion)
                    {
                        using (SqlCommand opinion = new SqlCommand(
                            @"INSERT INTO dbo.TenderBudgetOpinions
                                (TenderId,Opinion,ApprovedAmount,Remarks,ApprovedBy)
                              VALUES(@TenderId,@Opinion,@ApprovedAmount,@Remarks,@UserId);", connection, transaction))
                        {
                            ModuleDb.AddParameter(opinion, "@TenderId", SqlDbType.Int, TenderId);
                            AddText(opinion, "@Opinion", SqlDbType.NVarChar,
                                action == "Approved" ? "Positive" : "Negative", 10);
                            ModuleDb.AddParameter(opinion, "@ApprovedAmount", SqlDbType.Decimal, approvedBudgetAmount);
                            AddText(opinion, "@Remarks", SqlDbType.NVarChar, Remarks.Text, 1000);
                            ModuleDb.AddParameter(opinion, "@UserId", SqlDbType.Int, CurrentUserId);
                            opinion.ExecuteNonQuery();
                        }
                    }
                    using (SqlCommand lifecycle = new SqlCommand(
                        @"INSERT INTO dbo.TenderLifecycleHistory
                            (TenderId,PreviousStatus,NewStatus,ActionName,ActionBy,RoleAtAction,DepartmentId,Remarks)
                          VALUES(@TenderId,@PreviousStatus,@NewStatus,@StageAction,@UserId,@Role,@DepartmentId,@Remarks);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(lifecycle, "@TenderId", SqlDbType.Int, TenderId);
                        AddText(lifecycle, "@PreviousStatus", SqlDbType.NVarChar, previousStatus, 50);
                        AddText(lifecycle, "@NewStatus", SqlDbType.NVarChar, nextStatus, 50);
                        AddText(lifecycle, "@StageAction", SqlDbType.NVarChar, transition.Stage, 80);
                        ModuleDb.AddParameter(lifecycle, "@UserId", SqlDbType.Int, CurrentUserId);
                        AddText(lifecycle, "@Role", SqlDbType.NVarChar, CurrentRole, 50);
                        ModuleDb.AddParameter(lifecycle, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        AddText(lifecycle, "@Remarks", SqlDbType.NVarChar, Remarks.Text, 1000);
                        lifecycle.ExecuteNonQuery();
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, action, "Tender", TenderId, nextStatus, Request.UserHostAddress);
                    transaction.Commit();
                }
                Message.Text = "<div class=\"message success\">Tender status changed to " + HttpUtility.HtmlEncode(nextStatus) + ".</div>";
                LoadTender();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender workflow transition failed.", ex);
                ShowError("The workflow action could not be saved. Please refresh and retry.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
        }

        private void EnsureTenderPreparationComplete(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT
                    (SELECT COUNT(*) FROM dbo.BOQ WHERE TenderId=@TenderId) AS BOQCount,
                    COUNT(CASE WHEN DocumentType=N'Tender Proposal' THEN 1 END) AS ProposalCount,
                    COUNT(CASE WHEN DocumentType=N'Tender Document' THEN 1 END) AS TenderDocumentCount,
                    COUNT(CASE WHEN DocumentType=N'Technical Supporting Document' THEN 1 END) AS TechnicalDocumentCount,
                    COUNT(CASE WHEN DocumentType=N'Financial Supporting Document' THEN 1 END) AS FinancialDocumentCount
                  FROM dbo.TenderDocuments
                  WHERE TenderId=@TenderId;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("Tender preparation could not be verified.");

                    List<string> missing = new List<string>();
                    if (Convert.ToInt32(reader["BOQCount"], CultureInfo.InvariantCulture) == 0)
                        missing.Add("at least one BOQ item");
                    if (Convert.ToInt32(reader["ProposalCount"], CultureInfo.InvariantCulture) == 0)
                        missing.Add("a Tender Proposal document");
                    if (Convert.ToInt32(reader["TenderDocumentCount"], CultureInfo.InvariantCulture) == 0)
                        missing.Add("a Tender Document");
                    if (Convert.ToInt32(reader["TechnicalDocumentCount"], CultureInfo.InvariantCulture) == 0)
                        missing.Add("a Technical Supporting Document");
                    if (Convert.ToInt32(reader["FinancialDocumentCount"], CultureInfo.InvariantCulture) == 0)
                        missing.Add("a Financial Supporting Document");

                    if (missing.Count > 0)
                        throw new InvalidOperationException(
                            "Before forwarding to Tender Committee, provide " + String.Join(", ", missing) + ".");
                }
            }
        }

        private bool RequireTenderAccess()
        {
            if (IsAuthorizedTender) return true;
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            ShowError("You are not authorized to access this tender.");
            return false;
        }

        private bool CanManageTenderPreparation()
        {
            if (IsFinalWorkflow)
            {
                if (!IsDraft) return false;
                if (HasPermission("tenders.manage.all")) return true;
                return HasPermission("tender.create.department") && CurrentDepartmentId.HasValue &&
                    CurrentDepartmentId.Value == TenderDepartmentId;
            }

            return (IsJeAdministrator || HasAnyRole("DepartmentOfficer")) &&
                (IsDraft || String.Equals(TenderStatus, "Budget Revision Required", StringComparison.OrdinalIgnoreCase));
        }

        private void ShowError(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }

        private static bool IsBidStageOrLater(string status)
        {
            string[] bidStatuses =
            {
                "Tender Published", "Technical Bid Opening", "Technical Evaluation",
                "Technical Evaluation Approved", "Financial Bid Opening", "CAFO Rate Opinion",
                "Financial Evaluation", "MCA Opinion", "Tender Committee Recommendation",
                "Final Approval", "Agreement Completed", "Work Order Generated", "Work Started",
                "Measurement Book Updated", "Bill Submitted", "Bill Approved", "Payment Completed"
            };
            return Array.IndexOf(bidStatuses, status) >= 0;
        }
    }
}
