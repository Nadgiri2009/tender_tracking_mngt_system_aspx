using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class TenderWorkflow : ModulePage
    {
        protected Literal Message, TenderNumber, TenderTitle, DepartmentName, StageName, CurrentStatus, EstimatedAmount;
        protected Panel QueuePanel, DetailPanel, SubmitDraftPanel, PublishPanel, PrimaryOpeningPanel,
            PrimaryVerificationPanel, PrimaryForwardPanel, InitialTenderCellVerificationPanel,
            JeDocumentCorrectionPanel, SecondaryVerificationPanel,
            RejectedOpeningReverificationPanel,
            ShortfallCreationPanel, ShortfallApprovalPanel, FinancialOpeningApprovalPanel,
            FinancialBidEntryPanel, AuditorFinancialReviewPanel, FinancialResubmitPanel,
            WorkOrderCreatePanel, WorkOrderGeneratePanel, AgreementCreatePanel,
            AgreementGeneratePanel, CompleteTenderPanel, ShortfallClearancePanel;
        protected GridView WorkflowQueue, HistoryGrid, PrimaryBidderGrid, PrimaryDocumentGrid,
            SecondaryApplicationsGrid, ShortfallItemsGrid, FinancialEntryGrid, FinancialReviewGrid,
            GeneratedDocumentsGrid, ShortfallClearanceGrid, ShortfallApplicationsGrid;
        protected Repeater TenderSubmissionReadiness;
        protected HyperLink ApplicationRequirementsLink;
        protected TextBox SubmitRemarksText, PublishRemarksText,
            PrimaryOpenRemarksText, PrimaryCompletionRemarksText, PrimaryForwardRemarksText,
            RequiredBidDocumentsText, SecondaryRemarksText, ShortfallRemarksText, ShortfallDecisionRemarksText,
            CellDocumentVerificationRemarksText, RequiredTenderDocumentsText,
            FinancialOpeningRemarksText, FinancialEntryRemarksText, FinancialDecisionRemarksText,
            FinancialResubmissionRemarksText, WorkOrderNumberText, WorkOrderIssueDateText,
            WorkDescriptionText, WorkOrderTermsText, WorkOrderSignatoryText, AgreementNumberText,
            AgreementDateText, AgreementTermsText, AgreementSignatoryText;
        protected DropDownList SelectedFinancialBidSelect;
        protected Literal WorkOrderBidderName, WorkOrderBidAmount,
            AgreementBidderName, AgreementBidAmount, PublicationScheduleSummary,
            TenderSubmissionReadinessSummaryText;
        protected Button SubmitDraftButton, PublishButton, OpenTechnicalBidsButton,
            CompletePrimaryVerificationButton, ForwardToTenderCellButton,
            CompleteSecondaryVerificationButton, SendToShortfallCreationButton,
            ApproveShortfallButton, ReturnShortfallButton, ApproveFinancialOpeningButton,
            RejectFinancialOpeningButton, SaveFinancialBidsButton, ApproveFinancialReviewButton,
            ReturnFinancialReviewButton, ResubmitFinancialReviewButton, CreateWorkOrderButton,
            GenerateWorkOrderButton, CreateAgreementButton, GenerateAgreementButton,
            CompleteTenderButton;
        protected Button ApproveInitialTenderDocumentsButton, RaiseTenderDocumentShortfallButton,
            ResubmitTenderDocumentsButton;
        protected HyperLink TenderDocumentVerificationLink, CorrectTenderDocumentsLink;
        protected Literal TenderDocumentShortfallSummary, TenderDocumentCorrectionShortfallSummary;
        protected Button ReverifiedOpeningToChairmanButton, ReverifiedOpeningToShortfallButton;

        private sealed class TenderSubmissionReadinessItem
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public string Step { get; set; }
            public string Status { get; set; }
            public string ActionUrl { get; set; }
            public bool IsComplete { get; set; }
        }

        private int TenderId { get { return QueryId("id"); } }
        private bool IsSystemAdministrator { get { return HasPermission("system.admin"); } }
        private bool IsTenderCell { get { return TenderFinalizedWorkflowService.IsTenderCell(CurrentRoleCode); } }
        private string CurrentWorkflowStage { get; set; }
        private long? CurrentShortfallId { get; set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RequireAnyPermission(
                "tender.create.department", "tender.publication.manage", "tender.primary.open",
                "tender.primary.verify", "tender.cell.verify", "shortfall.create", "shortfall.approve",
                "shortfall.respond.own", "shortfall.clear", "financial.open.approve",
                "financial.open.execute", "financial.approve.final", "workorder.manage",
                "agreement.manage", "tenders.monitor", "system.admin")) return;

            if (!IsPostBack)
            {
                if (TenderId > 0) LoadTender();
                else LoadQueue();
            }
        }

        private void LoadTenderDocumentShortfallSummary(SqlConnection connection)
        {
            if (!CurrentShortfallId.HasValue)
            {
                TenderDocumentShortfallSummary.Text = String.Empty;
                TenderDocumentCorrectionShortfallSummary.Text = String.Empty;
                return;
            }

            using (SqlCommand command = new SqlCommand(
                @"SELECT sf.RaisedAtUtc,sf.DeadlineAtUtc,sf.Remarks,sf.RequiredDocuments,
                             sf.ResolutionStatus,sf.ResubmittedAtUtc,raiser.FullName AS RaisedBy
                      FROM dbo.TenderDocumentShortfalls sf
                      INNER JOIN dbo.Users raiser ON raiser.UserId=sf.RaisedBy
                      WHERE sf.TenderDocumentShortfallId=@ShortfallId AND sf.TenderId=@TenderId;",
                connection))
            {
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, CurrentShortfallId.Value);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        TenderDocumentShortfallSummary.Text =
                            "<div class=\"message\">The linked document shortfall could not be found.</div>";
                        TenderDocumentCorrectionShortfallSummary.Text = TenderDocumentShortfallSummary.Text;
                        return;
                    }
                    TenderDocumentShortfallSummary.Text =
                        "<div class=\"workflow-shortfall\"><strong>DOCUMENT SHORTFALL RAISED</strong>" +
                        "<dl><dt>Raised by</dt><dd>" + HttpUtility.HtmlEncode(Convert.ToString(reader["RaisedBy"])) +
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
                        "</dd></dl></div>";
                    TenderDocumentCorrectionShortfallSummary.Text = TenderDocumentShortfallSummary.Text;
                }
            }
        }

        private void LoadQueue()
        {
            string stageFilter = QueueStageFilter();
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT t.TenderId,t.TenderNumber,t.Title,d.DepartmentName,s.StageName,t.CurrentStatus,a.AssignedAtUtc
                      FROM dbo.TenderWorkflowAssignments a
                      INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                      INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId AND s.WorkflowVersion=2
                      INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                      LEFT JOIN dbo.TenderShortfalls sf ON sf.ShortfallId=a.ShortfallId
                      WHERE a.IsCurrent=1
                        AND
                        (
                            @IsAdmin=1
                            OR
                            (
                                (
                                    a.AssignedRoleId=@RoleId
                                    AND
                                    (
                                        (@IsContractor=1 AND a.AssignedUserId=@UserId)
                                        OR
                                        (@IsContractor=0
                                         AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                                         AND (@IsDepartmentScopedRole=0 OR
                                              (@DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId)))
                                    )
                                )
                                OR
                                (@IsTenderCell=1 AND s.StageName=N'Final_Contractor_SubmitShortfall'
                                 AND @DepartmentId=t.DepartmentId
                                 AND sf.SubmissionDeadlineUtc<=SYSUTCDATETIME())
                            )
                        )" + stageFilter + @"
                      ORDER BY a.AssignedAtUtc,t.TenderId;", connection))
                {
                    ModuleDb.AddParameter(command, "@IsAdmin", SqlDbType.Bit, IsSystemAdministrator);
                    ModuleDb.AddParameter(command, "@IsContractor", SqlDbType.Bit, HasAnyRole("ExternalBidder"));
                    ModuleDb.AddParameter(command, "@IsTenderCell", SqlDbType.Bit, IsTenderCell);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@IsDepartmentScopedRole", SqlDbType.Bit, false);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                        CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        DataTable data = new DataTable();
                        adapter.Fill(data);
                        WorkflowQueue.DataSource = data;
                        WorkflowQueue.DataBind();
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Finalized workflow queue could not be loaded.", ex);
                ShowMessage("The workflow queue could not be loaded. Please retry or contact support.");
            }
        }

        private string QueueStageFilter()
        {
            string filter;
            switch ((Request.QueryString["queue"] ?? String.Empty).Trim().ToLowerInvariant())
            {
                case "verification":
                    filter = @" AND s.StageName IN
                        (N'Final_JE_OpenTechnicalBids',N'Final_JE_CompletePrimaryVerification',
                         N'Final_Cell_InitialTenderDocumentVerification',N'Final_JE_CorrectTenderDocuments',
                         N'Final_Cell_CompleteSecondaryVerification',N'Final_Cell_ClearShortfall')";
                    break;
                case "shortfall":
                    filter = @" AND s.StageName IN
                        (N'Final_Cell_CreateShortfall',N'Final_JE_ApproveShortfall',
                         N'Final_Contractor_SubmitShortfall',N'Final_Cell_ClearShortfall')";
                    break;
                case "approval":
                    filter = @" AND s.StageName IN
                        (N'Final_Samiti_ApproveFinancialOpening',N'Final_Auditor_ApproveFinancialBids')";
                    break;
                default:
                    filter = String.Empty;
                    break;
            }

            string requestedStage = (Request.QueryString["stage"] ?? String.Empty).Trim();
            string exactStage = null;
            switch (requestedStage)
            {
                case "Final_Contractor_SubmitShortfall":
                    exactStage = "Final_Contractor_SubmitShortfall";
                    break;
                case "Final_Samiti_ApproveFinancialOpening":
                    exactStage = "Final_Samiti_ApproveFinancialOpening";
                    break;
                case "Final_Auditor_ApproveFinancialBids":
                    exactStage = "Final_Auditor_ApproveFinancialBids";
                    break;
                case "Final_JE_GenerateWorkOrder":
                    exactStage = "Final_JE_GenerateWorkOrder";
                    break;
                case "Final_JE_GenerateAgreement":
                    exactStage = "Final_JE_GenerateAgreement";
                    break;
            }
            if (exactStage != null)
                filter += " AND s.StageName=N'" + exactStage + "'";
            return filter;
        }

        private void LoadTender()
        {
            try
            {
                if (IsTenderCell && CurrentDepartmentId.HasValue)
                    TenderFinalizedWorkflowService.ExpireOverdueShortfall(TenderId, CurrentUserId,
                        CurrentRoleId, CurrentRoleCode, CurrentDepartmentId, Request.UserHostAddress);

                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT t.TenderNumber,t.Title,t.CurrentStatus,t.EstimatedAmount,d.DepartmentName,
                                 s.StageName,s.StageOrder,a.AssignedRoleId,a.AssignedUserId,a.DepartmentId,a.ShortfallId
                          FROM dbo.TenderWorkflowAssignments a
                          INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                          INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId AND s.WorkflowVersion=2
                          INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                          WHERE a.TenderId=@TenderId AND a.IsCurrent=1
                            AND (@IsAdmin=1 OR
                                 (a.AssignedRoleId=@RoleId AND
                                  ((@IsContractor=1 AND a.AssignedUserId=@UserId)
                                   OR (@IsContractor=0 AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                                       AND (@IsDepartmentScopedRole=0 OR
                                            (@DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId))))));", connection))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@IsAdmin", SqlDbType.Bit, IsSystemAdministrator);
                        ModuleDb.AddParameter(command, "@IsContractor", SqlDbType.Bit, HasAnyRole("ExternalBidder"));
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@IsDepartmentScopedRole", SqlDbType.Bit, false);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                DenyAccess();
                                return;
                            }
                            TenderNumber.Text = ModuleDb.Html(reader["TenderNumber"]);
                            TenderTitle.Text = ModuleDb.Html(reader["Title"]);
                            DepartmentName.Text = ModuleDb.Html(reader["DepartmentName"]);
                            CurrentStatus.Text = ModuleDb.Html(reader["CurrentStatus"]);
                            EstimatedAmount.Text = Convert.ToDecimal(reader["EstimatedAmount"])
                                .ToString("N2", CultureInfo.CurrentCulture);
                            StageName.Text = ModuleDb.Html(reader["StageName"]);
                            string stageName = Convert.ToString(reader["StageName"]);
                            CurrentWorkflowStage = stageName;
                            CurrentShortfallId = reader["ShortfallId"] == DBNull.Value
                                ? (long?)null : Convert.ToInt64(reader["ShortfallId"], CultureInfo.InvariantCulture);
                            bool assignedToCurrentRole = Convert.ToInt32(reader["AssignedRoleId"]) == CurrentRoleId;
                            bool assignedToCurrentUser = reader["AssignedUserId"] == DBNull.Value ||
                                Convert.ToInt32(reader["AssignedUserId"]) == CurrentUserId;
                            bool assignedToCurrentDepartment = CurrentDepartmentId.HasValue &&
                                Convert.ToInt32(reader["DepartmentId"]) == CurrentDepartmentId.Value;
                            bool canAct = assignedToCurrentRole && assignedToCurrentUser &&
                                (IsJeAdministrator || !HasAnyRole("Junior Engineer (JE)") ||
                                 assignedToCurrentDepartment);
                            SubmitDraftPanel.Visible = canAct && stageName == "Final_JE_SubmitTender" &&
                                HasPermission("tender.create.department");
                            InitialTenderCellVerificationPanel.Visible = canAct &&
                                stageName == "Final_Cell_InitialTenderDocumentVerification" &&
                                HasPermission("tender.cell.verify");
                            JeDocumentCorrectionPanel.Visible = canAct &&
                                stageName == "Final_JE_CorrectTenderDocuments" &&
                                HasPermission("tender.create.department");
                            PublishPanel.Visible = canAct && stageName == "Final_JE_PublishTender" &&
                                HasPermission("tender.publication.manage");
                            PrimaryOpeningPanel.Visible = canAct && stageName == "Final_JE_OpenTechnicalBids" &&
                                HasPermission("tender.primary.open");
                            PrimaryVerificationPanel.Visible = canAct &&
                                stageName == "Final_JE_CompletePrimaryVerification" &&
                                HasPermission("tender.primary.verify");
                            PrimaryForwardPanel.Visible = canAct && stageName == "Final_JE_ForwardToTenderCell" &&
                                HasPermission("tender.primary.verify");
                            SecondaryVerificationPanel.Visible = canAct &&
                                (stageName == "Final_Cell_CompleteSecondaryVerification" ||
                                 stageName == "Final_Cell_ReverifyRejectedOpening") &&
                                HasPermission("tender.cell.verify");
                            RejectedOpeningReverificationPanel.Visible = canAct &&
                                stageName == "Final_Cell_ReverifyRejectedOpening" &&
                                HasPermission("tender.cell.verify");
                            ShortfallCreationPanel.Visible = canAct &&
                                stageName == "Final_Cell_CreateShortfall" && HasPermission("shortfall.create");
                            ShortfallApprovalPanel.Visible = canAct &&
                                stageName == "Final_JE_ApproveShortfall" && HasPermission("shortfall.approve");
                            ShortfallClearancePanel.Visible = canAct &&
                                stageName == "Final_Cell_ClearShortfall" && HasPermission("shortfall.clear");
                            FinancialOpeningApprovalPanel.Visible = canAct &&
                                stageName == "Final_Samiti_ApproveFinancialOpening" &&
                                HasPermission("financial.open.approve");
                            FinancialBidEntryPanel.Visible = canAct &&
                                stageName == "Final_JE_OpenFinancialBids" &&
                                HasPermission("financial.open.execute");
                            AuditorFinancialReviewPanel.Visible = canAct &&
                                stageName == "Final_Auditor_ApproveFinancialBids" &&
                                HasPermission("financial.approve.final");
                            FinancialResubmitPanel.Visible = canAct &&
                                stageName == "Final_JE_ResubmitFinancialApproval" &&
                                HasPermission("financial.open.execute");
                            WorkOrderCreatePanel.Visible = canAct && stageName == "Final_JE_StartWorkOrder" &&
                                HasPermission("workorder.manage");
                            WorkOrderGeneratePanel.Visible = canAct && stageName == "Final_JE_GenerateWorkOrder" &&
                                HasPermission("workorder.manage");
                            AgreementCreatePanel.Visible = canAct && stageName == "Final_JE_StartAgreement" &&
                                HasPermission("agreement.manage");
                            AgreementGeneratePanel.Visible = canAct && stageName == "Final_JE_GenerateAgreement" &&
                                HasPermission("agreement.manage");
                            CompleteTenderPanel.Visible = canAct && stageName == "Final_JE_CompleteTender" &&
                                HasPermission("agreement.manage");
                        }
                    }
                    if (SubmitDraftPanel.Visible)
                    {
                        ApplicationRequirementsLink.NavigateUrl = "~/Pages/ApplicationRequirements.aspx?tenderId=" +
                            TenderId.ToString(CultureInfo.InvariantCulture);
                        LoadRequiredBidDocumentTypes(connection);
                        BindTenderSubmissionReadiness(connection);
                    }
                    if (InitialTenderCellVerificationPanel.Visible)
                    {
                        TenderDocumentVerificationLink.NavigateUrl =
                            "~/Pages/TenderForm.aspx?id=" + TenderId.ToString(CultureInfo.InvariantCulture);
                        LoadTenderDocumentShortfallSummary(connection);
                    }
                    if (JeDocumentCorrectionPanel.Visible)
                    {
                        CorrectTenderDocumentsLink.NavigateUrl =
                            "~/Pages/TenderDetails.aspx?id=" + TenderId.ToString(CultureInfo.InvariantCulture);
                        LoadTenderDocumentShortfallSummary(connection);
                    }
                    if (PublishPanel.Visible)
                        LoadPublicationScheduleSummary(connection);
                    if (PrimaryVerificationPanel.Visible)
                        BindPrimaryVerification(connection);
                    if (SecondaryVerificationPanel.Visible || ShortfallCreationPanel.Visible)
                        BindSecondaryApplications(connection);
                    CompleteSecondaryVerificationButton.Visible =
                        CurrentWorkflowStage == "Final_Cell_CompleteSecondaryVerification";
                    SendToShortfallCreationButton.Visible =
                        CurrentWorkflowStage == "Final_Cell_CompleteSecondaryVerification";
                    ReverifiedOpeningToChairmanButton.Visible =
                        CurrentWorkflowStage == "Final_Cell_ReverifyRejectedOpening";
                    ReverifiedOpeningToShortfallButton.Visible =
                        CurrentWorkflowStage == "Final_Cell_ReverifyRejectedOpening";
                    if (ShortfallApprovalPanel.Visible)
                        BindShortfallItems(connection);
                    if (ShortfallClearancePanel.Visible)
                        BindShortfallClearanceItems(connection);
                    if (FinancialBidEntryPanel.Visible)
                        BindFinancialEntryGrid(connection);
                    if (AuditorFinancialReviewPanel.Visible)
                        BindFinancialReview(connection);
                    if (WorkOrderCreatePanel.Visible)
                        BindApprovedBidder(connection);
                    if (AgreementCreatePanel.Visible)
                        BindApprovedBidder(connection);
                    BindGeneratedDocuments(connection);
                    BindHistory(connection);
                }
                QueuePanel.Visible = false;
                DetailPanel.Visible = true;
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Finalized workflow details could not be loaded.", ex);
                ShowMessage("Workflow details could not be loaded. Please retry or contact support.");
            }
        }

        private void BindHistory(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT h.ActionAtUtc,s.StageName,h.PreviousStatus,h.NewStatus,h.ActionName,
                         u.FullName AS Actor,h.Remarks
                  FROM dbo.TenderWorkflowHistory h
                  LEFT JOIN dbo.WorkflowStages s ON s.StageId=h.StageId
                  INNER JOIN dbo.Users u ON u.UserId=h.UserId
                  WHERE h.TenderId=@TenderId ORDER BY h.WorkflowHistoryId DESC;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                adapter.Fill(data);
                HistoryGrid.DataSource = data;
                HistoryGrid.DataBind();
            }
        }

        private void LoadRequiredBidDocumentTypes(SqlConnection connection)
        {
            List<string> documentTypes = new List<string>();
            using (SqlCommand command = new SqlCommand(
                @"SELECT DocumentType FROM dbo.TenderRequiredBidDocuments
                  WHERE TenderId=@TenderId ORDER BY DocumentType;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read()) documentTypes.Add(Convert.ToString(reader["DocumentType"]));
                }
            }
            RequiredBidDocumentsText.Text = String.Join(Environment.NewLine, documentTypes.ToArray());
        }

        private void BindTenderSubmissionReadiness(SqlConnection connection)
        {
            List<TenderSubmissionReadinessItem> items =
                GetTenderSubmissionReadiness(connection, null);
            TenderSubmissionReadiness.DataSource = items;
            TenderSubmissionReadiness.DataBind();

            int pendingCount = 0;
            foreach (TenderSubmissionReadinessItem item in items)
                if (!item.IsComplete) pendingCount++;
            TenderSubmissionReadinessSummaryText.Text = pendingCount == 0
                ? "All required items are complete. This tender is ready for Tender Cell review."
                : pendingCount.ToString(CultureInfo.InvariantCulture) + " required item" +
                    (pendingCount == 1 ? " is" : "s are") +
                    " pending. The tender will remain with JE until these are complete.";
        }

        private List<TenderSubmissionReadinessItem> GetTenderSubmissionReadiness(
            SqlConnection connection, SqlTransaction transaction)
        {
            List<TenderSubmissionReadinessItem> items = new List<TenderSubmissionReadinessItem>();
            string tenderDetailsUrl = "TenderDetails.aspx?id=" + TenderId.ToString(CultureInfo.InvariantCulture);
            string tenderFormUrl = "TenderForm.aspx?id=" + TenderId.ToString(CultureInfo.InvariantCulture);
            string documentVerificationUrl = tenderFormUrl;
            string basicStep = "Step 1 — Basic tender information";
            string documentStep = "Tender details — Tender documents";
            string boqStep = "Tender details — Bill of Quantities";
            string verificationStep = "JE initial document verification";

            using (SqlCommand command = new SqlCommand(
                @"SELECT t.Title,t.DepartmentId,
                         COALESCE(NULLIF(t.TenderMethod,N''),t.TenderType) AS TenderMethod,
                         t.TenderCategory,t.FinancialYear,t.EstimatedAmount,
                         (SELECT COUNT(*) FROM dbo.BOQ b WHERE b.TenderId=t.TenderId) AS BOQCount,
                         (SELECT COUNT(*) FROM dbo.TenderDocuments d
                          LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                          WHERE d.TenderId=t.TenderId
                            AND COALESCE(dt.TypeName,d.DocumentType)=N'Scope of Work') AS ScopeCount,
                         (SELECT COUNT(*) FROM dbo.TenderDocuments d
                          LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                          WHERE d.TenderId=t.TenderId
                            AND COALESCE(dt.TypeName,d.DocumentType)=N'Technical Requirements') AS TechnicalCount,
                         (SELECT COUNT(*) FROM dbo.TenderDocuments d
                          LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                          WHERE d.TenderId=t.TenderId
                            AND COALESCE(dt.TypeName,d.DocumentType)=N'Financial Requirements') AS FinancialCount
                  FROM dbo.Tender t
                  WHERE t.TenderId=@TenderId AND t.IsDeleted=0;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Tender readiness could not be checked.");

                    AddSubmissionReadinessItem(items, "title", "Project title",
                        Convert.ToString(reader["Title"]).Trim().Length >= 5, basicStep, tenderFormUrl);
                    AddSubmissionReadinessItem(items, "department", "Department",
                        Convert.ToInt32(reader["DepartmentId"], CultureInfo.InvariantCulture) > 0,
                        basicStep, tenderFormUrl);
                    AddSubmissionReadinessItem(items, "tender-method", "Tender method",
                        !String.IsNullOrWhiteSpace(Convert.ToString(reader["TenderMethod"])),
                        basicStep, tenderFormUrl);
                    AddSubmissionReadinessItem(items, "tender-category", "Tender category",
                        !String.IsNullOrWhiteSpace(Convert.ToString(reader["TenderCategory"])),
                        basicStep, tenderFormUrl);
                    AddSubmissionReadinessItem(items, "financial-year", "Budgeting financial year",
                        !String.IsNullOrWhiteSpace(Convert.ToString(reader["FinancialYear"])),
                        basicStep, tenderFormUrl);
                    AddSubmissionReadinessItem(items, "estimated-value", "Estimated tender value",
                        reader["EstimatedAmount"] != DBNull.Value &&
                            Convert.ToDecimal(reader["EstimatedAmount"], CultureInfo.InvariantCulture) > 0,
                        basicStep, tenderFormUrl);

                    AddSubmissionReadinessItem(items, "boq", "At least one BOQ item",
                        Convert.ToInt32(reader["BOQCount"], CultureInfo.InvariantCulture) > 0,
                        boqStep, tenderDetailsUrl + "#tender-boq-section");
                    AddSubmissionReadinessItem(items, "scope-of-work", "Scope of Work document",
                        Convert.ToInt32(reader["ScopeCount"], CultureInfo.InvariantCulture) > 0,
                        documentStep, tenderDetailsUrl + "#tender-documents-section");
                    AddSubmissionReadinessItem(items, "technical-requirements", "Technical Requirements document",
                        Convert.ToInt32(reader["TechnicalCount"], CultureInfo.InvariantCulture) > 0,
                        documentStep, tenderDetailsUrl + "#tender-documents-section");
                    AddSubmissionReadinessItem(items, "financial-requirements", "Financial Requirements document",
                        Convert.ToInt32(reader["FinancialCount"], CultureInfo.InvariantCulture) > 0,
                        documentStep, tenderDetailsUrl + "#tender-documents-section");
                }
            }

            bool foundDocuments = false;
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.DocumentId,
                         COALESCE(NULLIF(d.DocumentName,N''),NULLIF(d.FileName,N''),N'Untitled document') AS DocumentName,
                         CASE WHEN ISNULL(COALESCE(latest.VerificationStatus,d.VerificationStatus),N'')=N'Verified'
                              THEN 1 ELSE 0 END AS IsVerified
                  FROM dbo.TenderDocuments d
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus
                      FROM dbo.TenderRequirementVerificationHistory h
                      WHERE h.TenderId=d.TenderId AND h.DocumentId=d.DocumentId
                        AND h.ReviewStage=N'JE'
                      ORDER BY h.ReviewedAtUtc DESC,h.VerificationHistoryId DESC
                  ) latest
                  WHERE d.TenderId=@TenderId
                  ORDER BY d.UploadedDate,d.DocumentId;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        foundDocuments = true;
                        bool verified = Convert.ToInt32(reader["IsVerified"], CultureInfo.InvariantCulture) == 1;
                        AddSubmissionReadinessItem(items,
                            "je-document-" + Convert.ToString(reader["DocumentId"], CultureInfo.InvariantCulture),
                            "Verify document: " + Convert.ToString(reader["DocumentName"]),
                            verified, verificationStep, documentVerificationUrl);
                    }
                }
            }
            if (!foundDocuments)
                AddSubmissionReadinessItem(items, "je-document-upload",
                    "Upload tender documents before JE verification", false,
                    verificationStep, tenderDetailsUrl + "#tender-documents-section");

            AddSubmissionReadinessItem(items, "bid-document-types",
                "Required bidder technical document types (one per line)",
                HasValidRequiredBidDocumentTypes(RequiredBidDocumentsText.Text),
                "Field: Required bidder technical documents — JE initial document verification",
                String.Empty);
            return items;
        }

        private static void AddSubmissionReadinessItem(List<TenderSubmissionReadinessItem> items,
            string key, string name, bool isComplete, string step, string actionUrl)
        {
            items.Add(new TenderSubmissionReadinessItem
            {
                Key = key,
                Name = name,
                Step = step,
                IsComplete = isComplete,
                Status = isComplete ? "Complete" : "Pending",
                ActionUrl = actionUrl
            });
        }

        private static bool HasValidRequiredBidDocumentTypes(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length > 8000) return false;
            string[] names = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            HashSet<string> uniqueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (string line in names)
            {
                string name = line.Trim();
                if (name.Length == 0) continue;
                if (name.Length > 160 || !uniqueNames.Add(name)) return false;
                count++;
            }
            return count > 0 && count <= 30;
        }

        private void BindSecondaryApplications(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT b.TechnicalBidId,b.ContractorId,c.Name AS ContractorName,c.RegistrationNo,
                         b.SubmittedDate,COALESCE(latest.Decision,N'Pending') AS SecondaryDecision,
                         COALESCE(latest.Remarks,N'') AS SecondaryRemarks
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  OUTER APPLY
                  (
                      SELECT TOP (1) r.Decision,r.Remarks
                      FROM dbo.TenderVerificationReviews r
                      WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                        AND r.ReviewStage=N'Secondary'
                      ORDER BY r.ReviewedAtUtc DESC,r.VerificationReviewId DESC
                  ) latest
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                    AND (@DeficientOnly=0 OR latest.Decision=N'Shortfall')
                  ORDER BY b.SubmittedDate,b.TechnicalBidId;",
                connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@DeficientOnly", SqlDbType.Bit,
                    CurrentWorkflowStage == "Final_Cell_CreateShortfall");
                DataTable data = new DataTable();
                adapter.Fill(data);
                SecondaryApplicationsGrid.DataSource = data;
                SecondaryApplicationsGrid.DataBind();
                ShortfallApplicationsGrid.DataSource = data;
                ShortfallApplicationsGrid.DataBind();
            }
        }

        private void BindShortfallItems(SqlConnection connection)
        {
            if (!CurrentShortfallId.HasValue)
                throw new InvalidOperationException("The assigned shortfall was not found. Refresh the workflow queue.");

            using (SqlCommand command = new SqlCommand(
                @"SELECT i.ShortfallItemId,i.RequiredDocumentType,i.ShortfallDescription,
                         i.CorrectiveAction,i.ClearanceStatus,i.Remarks,
                         latest.FileName,latest.ShortfallSubmissionId
                  FROM dbo.TenderShortfallItems i
                  OUTER APPLY
                  (
                      SELECT TOP (1) s.FileName,s.ShortfallSubmissionId
                      FROM dbo.TenderShortfallSubmissions s
                      WHERE s.ShortfallItemId=i.ShortfallItemId
                      ORDER BY s.SubmittedAtUtc DESC,s.ShortfallSubmissionId DESC
                  ) latest
                  WHERE i.ShortfallId=@ShortfallId
                  ORDER BY i.ShortfallItemId;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, CurrentShortfallId.Value);
                DataTable data = new DataTable();
                adapter.Fill(data);
                ShortfallItemsGrid.DataSource = data;
                ShortfallItemsGrid.DataBind();
            }
        }

        private void BindShortfallClearanceItems(SqlConnection connection)
        {
            if (!CurrentShortfallId.HasValue)
                throw new InvalidOperationException("The assigned shortfall was not found. Refresh the workflow queue.");

            using (SqlCommand command = new SqlCommand(
                @"SELECT i.ShortfallItemId,i.RequiredDocumentType,i.ShortfallDescription,
                         i.CorrectiveAction,i.ClearanceStatus,i.Remarks,
                         latest.FileName,latest.ShortfallSubmissionId
                  FROM dbo.TenderShortfallItems i
                  OUTER APPLY
                  (
                      SELECT TOP (1) s.FileName,s.ShortfallSubmissionId
                      FROM dbo.TenderShortfallSubmissions s
                      WHERE s.ShortfallItemId=i.ShortfallItemId
                      ORDER BY s.SubmittedAtUtc DESC,s.ShortfallSubmissionId DESC
                  ) latest
                  WHERE i.ShortfallId=@ShortfallId
                  ORDER BY i.ShortfallItemId;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, CurrentShortfallId.Value);
                DataTable data = new DataTable();
                adapter.Fill(data);
                ShortfallClearanceGrid.DataSource = data;
                ShortfallClearanceGrid.DataBind();
            }
        }

        private void BindFinancialEntryGrid(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT b.ContractorId,c.Name AS ContractorName,c.RegistrationNo,
                         COALESCE(existing.BidAmount,0) AS ExistingAmount
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  OUTER APPLY
                  (
                      SELECT TOP (1) r.Decision
                      FROM dbo.TenderVerificationReviews r
                      WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                        AND r.ReviewStage=N'Secondary'
                      ORDER BY r.ReviewedAtUtc DESC,r.VerificationReviewId DESC
                  ) latest
                  LEFT JOIN dbo.FinancialBid existing
                    ON existing.TenderId=b.TenderId AND existing.ContractorId=b.ContractorId
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                    AND latest.Decision=N'Approved'
                  ORDER BY c.Name,b.TechnicalBidId;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                adapter.Fill(data);
                FinancialEntryGrid.DataSource = data;
                FinancialEntryGrid.DataBind();
            }
        }

        private void BindFinancialReview(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT fb.FinancialBidId,fb.ContractorId,c.Name AS ContractorName,
                         c.RegistrationNo,d.QuotedAmount,d.Remarks
                  FROM dbo.TenderFinancialBidOpeningDetails d
                  INNER JOIN dbo.TenderFinancialBidOpening o ON o.FinancialBidOpeningId=d.FinancialBidOpeningId
                  INNER JOIN dbo.FinancialBid fb ON fb.FinancialBidId=d.FinancialBidId
                  INNER JOIN dbo.Contractors c ON c.ContractorId=fb.ContractorId
                  WHERE o.TenderId=@TenderId
                  ORDER BY d.QuotedAmount,fb.FinancialBidId;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                adapter.Fill(data);
                FinancialReviewGrid.DataSource = data;
                FinancialReviewGrid.DataBind();
                SelectedFinancialBidSelect.Items.Clear();
                SelectedFinancialBidSelect.Items.Add(new ListItem("Select winning bid", String.Empty));
                foreach (DataRow row in data.Rows)
                {
                    SelectedFinancialBidSelect.Items.Add(new ListItem(
                        Convert.ToString(row["ContractorName"]) + " — ₹" +
                        Convert.ToDecimal(row["QuotedAmount"], CultureInfo.InvariantCulture)
                            .ToString("N2", CultureInfo.CurrentCulture),
                        Convert.ToString(row["FinancialBidId"], CultureInfo.InvariantCulture)));
                }
            }
        }

        private void BindApprovedBidder(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT c.Name AS ContractorName,a.ApprovedAmount
                  FROM dbo.TenderFinancialApprovals a
                  INNER JOIN dbo.FinancialBid b ON b.FinancialBidId=a.SelectedFinancialBidId
                    AND b.TenderId=a.TenderId
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  WHERE a.TenderId=@TenderId AND a.Decision=N'Approved'
                    AND a.FinancialApprovalId=(SELECT TOP (1) f.FinancialApprovalId
                      FROM dbo.TenderFinancialApprovals f WHERE f.TenderId=@TenderId
                      ORDER BY f.ApprovedAtUtc DESC,f.FinancialApprovalId DESC);",
                connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The latest auditor-approved bidder could not be found.");
                    string bidder = ModuleDb.Html(reader["ContractorName"]);
                    string amount = ModuleDb.Html(
                        Convert.ToDecimal(reader["ApprovedAmount"], CultureInfo.InvariantCulture)
                            .ToString("N2", CultureInfo.CurrentCulture));
                    WorkOrderBidderName.Text = bidder;
                    WorkOrderBidAmount.Text = amount;
                    AgreementBidderName.Text = bidder;
                    AgreementBidAmount.Text = amount;
                }
            }
        }

        private void BindGeneratedDocuments(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT GeneratedDocumentId,DocumentKind,VersionNumber,FileName,GeneratedAtUtc
                  FROM dbo.TenderGeneratedDocuments
                  WHERE TenderId=@TenderId ORDER BY GeneratedAtUtc DESC,GeneratedDocumentId DESC;",
                connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                adapter.Fill(data);
                GeneratedDocumentsGrid.DataSource = data;
                GeneratedDocumentsGrid.DataBind();
            }
        }

        protected void SubmitDraftButton_Click(object sender, EventArgs e)
        {
            ProcessStage("Final_JE_SubmitTender", "tender.create.department",
                "Pending Tender Cell Verification",
                SubmitRemarksText.Text, null, null);
        }

        protected void ApproveInitialTenderDocumentsButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_Cell_InitialTenderDocumentVerification", "tender.cell.verify",
                "Submitted", CellDocumentVerificationRemarksText.Text, CurrentShortfallId,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.EnsureTenderRequirementDocumentsVerified(
                        connection, transaction, TenderId, "TENDER_CELL");
                    if (CurrentShortfallId.HasValue)
                        TenderFinalizedWorkflowService.ResolveTenderDocumentShortfall(
                            connection, transaction, TenderId, CurrentShortfallId.Value, CurrentUserId);
                    return new WorkflowTransitionMetadata
                    {
                        ActionName = "ApproveInitialTenderDocumentVerification"
                    };
                });
        }

        protected void RaiseTenderDocumentShortfallButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_Cell_InitialTenderDocumentVerification", "tender.cell.verify",
                "Pending JE Document Correction", CellDocumentVerificationRemarksText.Text, CurrentShortfallId,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.CreateTenderDocumentShortfall(connection, transaction,
                        TenderId, CurrentUserId, departmentId, CellDocumentVerificationRemarksText.Text,
                        RequiredTenderDocumentsText.Text));
        }

        protected void ResubmitTenderDocumentsButton_Click(object sender, EventArgs e)
        {
            if (!CurrentShortfallId.HasValue)
            {
                ShowMessage("There is no active document shortfall to resubmit.");
                return;
            }
            ProcessFinalizedStage("Final_JE_CorrectTenderDocuments", "tender.create.department",
                "Pending Tender Cell Verification", "JE resubmitted corrected tender documents.",
                CurrentShortfallId,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.ResubmitTenderDocuments(connection, transaction,
                        TenderId, CurrentShortfallId.Value, CurrentUserId));
        }

        protected void PublishButton_Click(object sender, EventArgs e)
        {
            ProcessStage("Final_JE_PublishTender", "tender.publication.manage", "Tender Published",
                PublishRemarksText.Text, null, null);
        }

        protected void OpenTechnicalBidsButton_Click(object sender, EventArgs e)
        {
            ProcessStage("Final_JE_OpenTechnicalBids", "tender.primary.open",
                "Primary Verification Pending", PrimaryOpenRemarksText.Text, null, null);
        }

        protected void CompletePrimaryVerificationButton_Click(object sender, EventArgs e)
        {
            ProcessStage("Final_JE_CompletePrimaryVerification", "tender.primary.verify",
                "Primary Verification Completed", PrimaryCompletionRemarksText.Text, null, null);
        }

        protected void ForwardToTenderCellButton_Click(object sender, EventArgs e)
        {
            ProcessStage("Final_JE_ForwardToTenderCell", "tender.primary.verify",
                "Tender Cell Verification Pending", PrimaryForwardRemarksText.Text, null, null);
        }

        protected void CompleteSecondaryVerificationButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_Cell_CompleteSecondaryVerification", "tender.cell.verify",
                "Financial Bid Opening Approval Pending", SecondaryRemarksText.Text,
                null, (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.EnsureSecondaryVerificationComplete(
                        connection, transaction, TenderId, newStatus);
                    return new WorkflowTransitionMetadata { ActionName = "CompleteSecondaryVerification" };
                });
        }

        protected void SendToShortfallCreationButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_Cell_CompleteSecondaryVerification", "tender.cell.verify",
                "Shortfall Generation Pending", SecondaryRemarksText.Text,
                null, (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.EnsureSecondaryVerificationComplete(
                        connection, transaction, TenderId, newStatus);
                    return new WorkflowTransitionMetadata { ActionName = "RequireStructuredShortfall" };
                });
        }

        protected void ReverifiedOpeningToChairmanButton_Click(object sender, EventArgs e)
        {
            CompleteRejectedOpeningReverification("Financial Bid Opening Approval Pending");
        }

        protected void ReverifiedOpeningToShortfallButton_Click(object sender, EventArgs e)
        {
            CompleteRejectedOpeningReverification("Shortfall Generation Pending");
        }

        private void CompleteRejectedOpeningReverification(string targetStatus)
        {
            ProcessFinalizedStage("Final_Cell_ReverifyRejectedOpening", "tender.cell.verify",
                targetStatus, SecondaryRemarksText.Text, null,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.EnsureSecondaryVerificationComplete(
                        connection, transaction, TenderId, newStatus);
                    return new WorkflowTransitionMetadata
                    {
                        ActionName = targetStatus == "Shortfall Generation Pending"
                            ? "RequireShortfallAfterFinancialOpeningReturn"
                            : "CompleteFinancialOpeningReverification"
                    };
                });
        }

        protected void ReturnShortfallButton_Click(object sender, EventArgs e)
        {
            long shortfallId = GetAssignedShortfallId("Final_JE_ApproveShortfall");
            ProcessFinalizedStage("Final_JE_ApproveShortfall", "shortfall.approve",
                "Shortfall Generation Pending", ShortfallDecisionRemarksText.Text, shortfallId,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.DecideStructuredShortfall(connection, transaction,
                        TenderId, shortfallId, CurrentUserId, departmentId,
                        false, ShortfallDecisionRemarksText.Text));
        }

        protected void ApproveShortfallButton_Click(object sender, EventArgs e)
        {
            long shortfallId = GetAssignedShortfallId("Final_JE_ApproveShortfall");
            ProcessFinalizedStage("Final_JE_ApproveShortfall", "shortfall.approve",
                "Awaiting Contractor Submission", ShortfallDecisionRemarksText.Text, shortfallId,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.DecideStructuredShortfall(connection, transaction,
                        TenderId, shortfallId, CurrentUserId, departmentId,
                        true, ShortfallDecisionRemarksText.Text));
        }

        protected void ApproveFinancialOpeningButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_Samiti_ApproveFinancialOpening", "financial.open.approve",
                "Financial Bid Opening Approved", FinancialOpeningRemarksText.Text, null,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.RecordFinancialOpeningDecision(connection, transaction,
                        TenderId, departmentId, true, FinancialOpeningRemarksText.Text));
        }

        protected void RejectFinancialOpeningButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_Samiti_ApproveFinancialOpening", "financial.open.approve",
                "Financial Bid Opening Rejected", FinancialOpeningRemarksText.Text, null,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.RecordFinancialOpeningDecision(connection, transaction,
                        TenderId, departmentId, false, FinancialOpeningRemarksText.Text));
        }

        protected void SaveFinancialBidsButton_Click(object sender, EventArgs e)
        {
            List<FinancialBidInput> bids = new List<FinancialBidInput>();
            foreach (GridViewRow row in FinancialEntryGrid.Rows)
            {
                if (row.RowType != DataControlRowType.DataRow) continue;
                int contractorId;
                decimal amount;
                TextBox amountInput = row.FindControl("FinancialBidAmountText") as TextBox;
                TextBox remarksInput = row.FindControl("FinancialBidRemarksText") as TextBox;
                if (!Int32.TryParse(Convert.ToString(FinancialEntryGrid.DataKeys[row.RowIndex].Value),
                        NumberStyles.None, CultureInfo.InvariantCulture, out contractorId) ||
                    amountInput == null ||
                    !Decimal.TryParse(amountInput.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
                {
                    ShowMessage("Enter a valid financial amount for every qualified bidder.");
                    return;
                }
                bids.Add(new FinancialBidInput
                {
                    ContractorId = contractorId,
                    Amount = amount,
                    Remarks = remarksInput == null ? String.Empty : remarksInput.Text
                });
            }

            ProcessFinalizedStage("Final_JE_OpenFinancialBids", "financial.open.execute",
                "Final Financial Approval Pending", FinancialEntryRemarksText.Text, null,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.EnterFinancialBids(connection, transaction,
                        TenderId, CurrentUserId, bids, FinancialEntryRemarksText.Text);
                    return new WorkflowTransitionMetadata { ActionName = "OpenFinancialBids" };
                });
        }

        protected void ApproveFinancialReviewButton_Click(object sender, EventArgs e)
        {
            int financialBidId;
            if (!Int32.TryParse(SelectedFinancialBidSelect.SelectedValue, NumberStyles.None,
                    CultureInfo.InvariantCulture, out financialBidId))
            {
                ShowMessage("Select a financial bid to approve.");
                return;
            }
            ProcessFinancialReview("Approved", financialBidId);
        }

        protected void ReturnFinancialReviewButton_Click(object sender, EventArgs e)
        {
            ProcessFinancialReview("Returned", null);
        }

        private void ProcessFinancialReview(string decision, int? selectedFinancialBidId)
        {
            ProcessFinalizedStage("Final_Auditor_ApproveFinancialBids", "financial.approve.final",
                decision == "Approved" ? "Final Financial Approval Completed" : "Final Financial Approval Rejected",
                FinancialDecisionRemarksText.Text, null,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.RecordAuditorFinancialDecision(connection, transaction,
                        TenderId, CurrentUserId, decision, selectedFinancialBidId,
                        FinancialDecisionRemarksText.Text);
                    return new WorkflowTransitionMetadata { ActionName = "AuditorFinancial" + decision };
                });
        }

        protected void ResubmitFinancialReviewButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_JE_ResubmitFinancialApproval", "financial.open.execute",
                "Final Financial Approval Pending", FinancialResubmissionRemarksText.Text, null,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.ResubmitFinancialReview(connection, transaction,
                        TenderId, CurrentUserId, FinancialResubmissionRemarksText.Text));
        }

        protected void CreateWorkOrderButton_Click(object sender, EventArgs e)
        {
            DateTime issueDateUtc;
            if (!TryParseIndiaDateTime(WorkOrderIssueDateText.Text, out issueDateUtc))
            {
                ShowMessage("Enter a valid work order issue date and time in India Standard Time.");
                return;
            }
            ProcessFinalizedStage("Final_JE_StartWorkOrder", "workorder.manage", "Work Order Pending",
                WorkOrderNumberText.Text, null, (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.CreateWorkOrder(connection, transaction, TenderId,
                        WorkOrderNumberText.Text, issueDateUtc, WorkDescriptionText.Text,
                        WorkOrderTermsText.Text, WorkOrderSignatoryText.Text);
                    return new WorkflowTransitionMetadata { ActionName = "CreateWorkOrder" };
                });
        }

        protected void GenerateWorkOrderButton_Click(object sender, EventArgs e)
        {
            ProcessGeneratedDocument("Final_JE_GenerateWorkOrder", "workorder.manage",
                "Work Order Generated", "WorkOrder");
        }

        protected void CreateAgreementButton_Click(object sender, EventArgs e)
        {
            DateTime agreementDateUtc;
            if (!TryParseIndiaDateTime(AgreementDateText.Text, out agreementDateUtc))
            {
                ShowMessage("Enter a valid agreement date and time in India Standard Time.");
                return;
            }
            ProcessFinalizedStage("Final_JE_StartAgreement", "agreement.manage", "Tender Agreement Pending",
                AgreementNumberText.Text, null, (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.CreateAgreement(connection, transaction, TenderId,
                        AgreementNumberText.Text, agreementDateUtc,
                        AgreementTermsText.Text, AgreementSignatoryText.Text);
                    return new WorkflowTransitionMetadata { ActionName = "CreateTenderAgreement" };
                });
        }

        protected void GenerateAgreementButton_Click(object sender, EventArgs e)
        {
            ProcessGeneratedDocument("Final_JE_GenerateAgreement", "agreement.manage",
                "Tender Agreement Generated", "Agreement");
        }

        protected void CompleteTenderButton_Click(object sender, EventArgs e)
        {
            ProcessFinalizedStage("Final_JE_CompleteTender", "agreement.manage",
                "Completed", "Tender documents completed.", null,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                {
                    TenderFinalizedWorkflowService.EnsureFinalDocumentsGenerated(
                        connection, transaction, TenderId);
                    return new WorkflowTransitionMetadata { ActionName = "CompleteTender" };
                });
        }

        protected void ShortfallClearanceGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "ReviewShortfallItem", StringComparison.Ordinal))
                return;
            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), NumberStyles.None,
                    CultureInfo.InvariantCulture, out rowIndex) ||
                rowIndex < 0 || rowIndex >= ShortfallClearanceGrid.Rows.Count ||
                rowIndex >= ShortfallClearanceGrid.DataKeys.Count)
            {
                ShowMessage("The selected shortfall item is invalid. Refresh and retry.");
                return;
            }

            long itemId;
            if (!Int64.TryParse(Convert.ToString(ShortfallClearanceGrid.DataKeys[rowIndex].Value),
                    NumberStyles.None, CultureInfo.InvariantCulture, out itemId))
            {
                ShowMessage("The selected shortfall item could not be read.");
                return;
            }
            DropDownList decisionInput = ShortfallClearanceGrid.Rows[rowIndex]
                .FindControl("ShortfallClearanceDecisionSelect") as DropDownList;
            TextBox remarksInput = ShortfallClearanceGrid.Rows[rowIndex]
                .FindControl("ShortfallClearanceRemarksText") as TextBox;
            if (decisionInput == null || remarksInput == null)
            {
                ShowMessage("The shortfall decision controls could not be read.");
                return;
            }

            long shortfallId = GetAssignedShortfallId("Final_Cell_ClearShortfall");
            string decision = decisionInput.SelectedValue;
            string remarks = remarksInput.Text;
            ProcessFinalizedStage("Final_Cell_ClearShortfall", "shortfall.clear",
                "Shortfall Documents Submitted", remarks, shortfallId,
                (connection, transaction, departmentId, previousStatus, newStatus) =>
                    TenderFinalizedWorkflowService.ReviewShortfallItem(connection, transaction,
                        TenderId, shortfallId, itemId, CurrentUserId, CurrentRoleId,
                        decision, remarks));
        }

        private void ProcessGeneratedDocument(string stageName, string permission,
            string newStatus, string kind)
        {
            List<string> storedPaths = new List<string>();
            try
            {
                TenderFinalizedWorkflowService.Transition(TenderId, CurrentUserId, CurrentRoleId,
                    CurrentRoleCode, CurrentDepartmentId, stageName, newStatus,
                    "Generate " + kind + " PDF.", null, permission, Request.UserHostAddress,
                    (connection, transaction, departmentId, previousStatus, targetStatus) =>
                    {
                        WorkflowTransitionMetadata metadata =
                            TenderFinalizedWorkflowService.GenerateFinalDocument(this, connection,
                                transaction, TenderId, CurrentUserId, kind);
                        if (!String.IsNullOrWhiteSpace(metadata.RelatedDocumentPath))
                            storedPaths.Add(metadata.RelatedDocumentPath);
                        if (String.Equals(kind, "Agreement", StringComparison.Ordinal))
                        {
                            WorkflowTransitionMetadata boqMetadata =
                                TenderFinalizedWorkflowService.GenerateFinalBoqDocument(this,
                                    connection, transaction, TenderId, CurrentUserId);
                            storedPaths.Add(boqMetadata.RelatedDocumentPath);
                            metadata.ActionName += ";" + boqMetadata.ActionName;
                        }
                        return metadata;
                    });
                Response.Redirect("~/Pages/TenderWorkflow.aspx?id=" +
                    TenderId.ToString(CultureInfo.InvariantCulture), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (SqlException ex)
            {
                foreach (string storedPath in storedPaths)
                    TenderLifecycleFiles.DeleteStoredGeneratedTenderDocument(this, storedPath);
                Trace.Warn("DepartmentModule", "Final tender document generation failed.", ex);
                ShowMessage("The PDF could not be generated and recorded. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                foreach (string storedPath in storedPaths)
                    TenderLifecycleFiles.DeleteStoredGeneratedTenderDocument(this, storedPath);
                ShowMessage(ModuleDb.Html(ex.Message));
            }
        }

        private long GetAssignedShortfallId(string stageName)
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                String.Empty, connection))
            {
                command.CommandText =
                    @"SELECT a.ShortfallId
                      FROM dbo.TenderWorkflowAssignments a
                      INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                        AND s.WorkflowVersion=2 AND s.IsActive=1
                      WHERE a.TenderId=@TenderId AND a.IsCurrent=1
                        AND a.AssignedRoleId=@RoleId AND s.StageName=@StageName
                        AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                        AND a.ShortfallId IS NOT NULL
                        AND (@IsJE=0 OR (@DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId));";
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                ModuleDb.AddParameter(command, "@StageName", SqlDbType.NVarChar, stageName, 80);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                ModuleDb.AddParameter(command, "@IsJE", SqlDbType.Bit,
                    TenderFinalizedWorkflowService.IsJuniorEngineer(CurrentRoleCode));
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("The assigned shortfall was not found. Refresh the workflow queue.");
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
        }

        private void ProcessFinalizedStage(string stageName, string permission, string newStatus,
            string remarks, long? shortfallId, TenderFinalizedWorkflowService.StageAction action)
        {
            try
            {
                TenderFinalizedWorkflowService.Transition(TenderId, CurrentUserId, CurrentRoleId,
                    CurrentRoleCode, CurrentDepartmentId, stageName, newStatus, remarks,
                    shortfallId, permission, Request.UserHostAddress, action);
                Response.Redirect("~/Pages/TenderWorkflow.aspx?id=" +
                    TenderId.ToString(CultureInfo.InvariantCulture), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Finalized tender workflow action failed.", ex);
                ShowMessage("The workflow action could not be saved. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ModuleDb.Html(ex.Message));
            }
        }

        private void BindPrimaryVerification(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT b.TechnicalBidId,b.ContractorId,c.Name AS ContractorName,c.RegistrationNo,
                         c.ContactNo,c.Email,b.SubmittedDate,
                         (SELECT COUNT(1) FROM dbo.TechnicalBidDocuments d
                          WHERE d.TechnicalBidId=b.TechnicalBidId) AS DocumentCount,
                         (SELECT COUNT(1) FROM dbo.TenderRequiredBidDocuments r
                          WHERE r.TenderId=b.TenderId) AS RequiredDocumentCount,
                         COALESCE(review.Decision,N'') AS PrimaryDecision,
                         COALESCE(review.Remarks,N'') AS PrimaryRemarks
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  OUTER APPLY
                  (
                      SELECT TOP (1) r.Decision,r.Remarks
                      FROM dbo.TenderVerificationReviews r
                      WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                        AND r.ReviewStage=N'Primary'
                      ORDER BY r.ReviewedAtUtc DESC,r.VerificationReviewId DESC
                  ) review
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                  ORDER BY b.SubmittedDate,b.TechnicalBidId;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                adapter.Fill(data);
                PrimaryBidderGrid.DataSource = data;
                PrimaryBidderGrid.DataBind();
            }

            using (SqlCommand command = new SqlCommand(
                @"SELECT d.TechnicalBidDocumentId,b.ContractorId,d.DocumentType,d.FileName,
                         d.UploadedDate,COALESCE(history.VerificationStatus,N'') AS VerificationStatus,
                         COALESCE(history.Remarks,N'') AS VerificationRemarks,CONVERT(bit,0) AS IsMissing,
                         c.Name AS ContractorName
                  FROM dbo.TechnicalBidDocuments d
                  INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus,h.Remarks
                      FROM dbo.TenderDocumentVerificationHistory h
                      WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                        AND h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                        AND h.ReviewStage=N'Primary'
                      ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                  ) history
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                  UNION ALL
                  SELECT CONVERT(int,0),b.ContractorId,r.DocumentType,N'(not submitted)',
                         CONVERT(datetime2,NULL),COALESCE(history.VerificationStatus,N''),
                         COALESCE(history.Remarks,N''),CONVERT(bit,1),c.Name
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  INNER JOIN dbo.TenderRequiredBidDocuments r ON r.TenderId=b.TenderId
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus,h.Remarks
                      FROM dbo.TenderDocumentVerificationHistory h
                      WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                        AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                        AND h.ReviewStage=N'Primary'
                      ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                  ) history
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                    AND NOT EXISTS
                    (
                        SELECT 1 FROM dbo.TechnicalBidDocuments d
                        WHERE d.TechnicalBidId=b.TechnicalBidId AND d.DocumentType=r.DocumentType
                    )
                  ORDER BY ContractorName,DocumentType,UploadedDate,TechnicalBidDocumentId;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                adapter.Fill(data);
                PrimaryDocumentGrid.DataSource = data;
                PrimaryDocumentGrid.DataBind();
            }
        }

        protected bool SupportsInlinePreview(object fileName)
        {
            string extension = Path.GetExtension(Convert.ToString(fileName));
            return String.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase);
        }

        protected void PrimaryBidderGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "RecordPrimaryBidderReview") return;
            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out rowIndex) ||
                rowIndex < 0 || rowIndex >= PrimaryBidderGrid.DataKeys.Count)
            {
                ShowMessage("The selected bidder could not be verified. Refresh the page and try again.");
                return;
            }

            int contractorId = Convert.ToInt32(PrimaryBidderGrid.DataKeys[rowIndex].Values["ContractorId"]);
            int technicalBidId = Convert.ToInt32(PrimaryBidderGrid.DataKeys[rowIndex].Values["TechnicalBidId"]);
            TextBox remarksInput = PrimaryBidderGrid.Rows[rowIndex].FindControl("PrimaryBidderRemarksText") as TextBox;
            string remarks = remarksInput == null ? String.Empty : remarksInput.Text.Trim();
            if (remarks.Length > 2000)
            {
                ShowMessage("Bidder review remarks cannot exceed 2,000 characters.");
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    EnsureAssignedStage(connection, transaction, "Final_JE_CompletePrimaryVerification",
                        "tender.primary.verify");
                    int documentCount;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT COUNT(1) FROM dbo.TechnicalBidDocuments d
                          INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                          WHERE b.TenderId=@TenderId AND b.ContractorId=@ContractorId
                            AND b.Status=N'Submitted';", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                        documentCount = Convert.ToInt32(command.ExecuteScalar());
                    }
                    if (documentCount == 0 && String.IsNullOrWhiteSpace(remarks))
                        throw new InvalidOperationException("Record remarks explaining the missing bidder documents.");

                    using (SqlCommand command = new SqlCommand(
                        @"IF EXISTS
                          (
                              SELECT 1 FROM dbo.TenderVerificationReviews WITH (UPDLOCK,HOLDLOCK)
                              WHERE TenderId=@TenderId AND ContractorId=@ContractorId AND ReviewStage=N'Primary'
                          )
                              THROW 51110,'A primary bidder review has already been recorded.',1;
                          IF NOT EXISTS
                          (
                              SELECT 1 FROM dbo.TechnicalBid
                              WHERE TenderId=@TenderId AND ContractorId=@ContractorId AND Status=N'Submitted'
                          )
                              THROW 51111,'The submitted bidder record is no longer available.',1;
                          INSERT INTO dbo.TenderVerificationReviews
                            (TenderId,ContractorId,ReviewStage,Decision,ReviewerId,ReviewerRoleId,Remarks)
                          VALUES(@TenderId,@ContractorId,N'Primary',N'Completed',@UserId,@RoleId,@Remarks);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        AddText(command, "@Remarks", SqlDbType.NVarChar, remarks, 2000);
                        command.ExecuteNonQuery();
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "PrimaryBidderReview",
                        "TechnicalBid", technicalBidId, remarks, Request.UserHostAddress);
                    transaction.Commit();
                }

                Message.Text = "<div class=\"message success\">Bidder review recorded.</div>";
                BindPrimaryVerificationAfterPostback();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Primary bidder review could not be recorded.", ex);
                ShowMessage(ex.Number == 51110
                    ? "A primary bidder review has already been recorded."
                    : "The bidder review could not be saved.");
            }
            catch (InvalidOperationException ex) { ShowMessage(ModuleDb.Html(ex.Message)); }
        }

        protected void PrimaryDocumentGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "RecordPrimaryDocument") return;
            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out rowIndex) ||
                rowIndex < 0 || rowIndex >= PrimaryDocumentGrid.DataKeys.Count)
            {
                ShowMessage("The selected document could not be verified. Refresh the page and try again.");
                return;
            }

            int documentId = Convert.ToInt32(PrimaryDocumentGrid.DataKeys[rowIndex].Value);
            int contractorId = Convert.ToInt32(PrimaryDocumentGrid.DataKeys[rowIndex].Values["ContractorId"]);
            string documentType = Convert.ToString(PrimaryDocumentGrid.DataKeys[rowIndex].Values["DocumentType"]);
            bool isMissing = Convert.ToBoolean(PrimaryDocumentGrid.DataKeys[rowIndex].Values["IsMissing"]);
            DropDownList decisionInput = PrimaryDocumentGrid.Rows[rowIndex].FindControl("PrimaryDocumentDecision") as DropDownList;
            TextBox remarksInput = PrimaryDocumentGrid.Rows[rowIndex].FindControl("PrimaryDocumentRemarksText") as TextBox;
            string decision = decisionInput == null ? String.Empty : decisionInput.SelectedValue;
            string remarks = remarksInput == null ? String.Empty : remarksInput.Text.Trim();
            if ((decision != "Verified" && decision != "Not Verified") ||
                ((decision == "Not Verified" || isMissing) && String.IsNullOrWhiteSpace(remarks)) ||
                (isMissing && decision != "Not Verified") || remarks.Length > 2000)
            {
                ShowMessage("Select a valid decision; a not-verified decision requires remarks of at most 2,000 characters.");
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    EnsureAssignedStage(connection, transaction, "Final_JE_CompletePrimaryVerification",
                        "tender.primary.verify");
                    using (SqlCommand command = new SqlCommand(
                        @"IF @IsMissing=1
                          BEGIN
                              IF EXISTS
                              (
                                  SELECT 1 FROM dbo.TenderDocumentVerificationHistory WITH (UPDLOCK,HOLDLOCK)
                                  WHERE TenderId=@TenderId AND ContractorId=@ContractorId
                                    AND TechnicalBidDocumentId IS NULL AND DocumentType=@DocumentType
                                    AND ReviewStage=N'Primary'
                              )
                                  THROW 51112,'A primary decision has already been recorded for this required document.',1;
                              IF EXISTS
                              (
                                  SELECT 1 FROM dbo.TechnicalBidDocuments d
                                  INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                                  WHERE b.TenderId=@TenderId AND b.ContractorId=@ContractorId
                                    AND b.Status=N'Submitted' AND d.DocumentType=@DocumentType
                              )
                                  THROW 51113,'The required document was uploaded after the page was loaded.',1;
                              INSERT INTO dbo.TenderDocumentVerificationHistory
                                (TenderId,ContractorId,TechnicalBidDocumentId,FileName,FilePath,DocumentType,
                                 ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks)
                              SELECT @TenderId,b.ContractorId,NULL,N'(not submitted)',N'',r.DocumentType,
                                     N'Primary',N'Not Verified',@UserId,@RoleId,@Remarks
                              FROM dbo.TechnicalBid b
                              INNER JOIN dbo.TenderRequiredBidDocuments r ON r.TenderId=b.TenderId
                              WHERE b.TenderId=@TenderId AND b.ContractorId=@ContractorId
                                AND b.Status=N'Submitted' AND r.DocumentType=@DocumentType
                                AND NOT EXISTS
                                (
                                    SELECT 1 FROM dbo.TechnicalBidDocuments d
                                    WHERE d.TechnicalBidId=b.TechnicalBidId AND d.DocumentType=r.DocumentType
                                );
                          END
                          ELSE
                          BEGIN
                              IF EXISTS
                              (
                                  SELECT 1 FROM dbo.TenderDocumentVerificationHistory WITH (UPDLOCK,HOLDLOCK)
                                  WHERE TenderId=@TenderId AND TechnicalBidDocumentId=@DocumentId
                                    AND ReviewStage=N'Primary'
                              )
                                  THROW 51112,'A primary document decision has already been recorded.',1;
                              INSERT INTO dbo.TenderDocumentVerificationHistory
                                (TenderId,ContractorId,TechnicalBidDocumentId,FileName,FilePath,DocumentType,
                                 ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks)
                              SELECT @TenderId,b.ContractorId,d.TechnicalBidDocumentId,d.FileName,d.FilePath,d.DocumentType,
                                     N'Primary',@Decision,@UserId,@RoleId,@Remarks
                              FROM dbo.TechnicalBidDocuments d
                              INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                              WHERE d.TechnicalBidDocumentId=@DocumentId AND b.TenderId=@TenderId
                                AND b.ContractorId=@ContractorId AND b.Status=N'Submitted';
                          END;",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                        AddText(command, "@DocumentType", SqlDbType.NVarChar, documentType, 160);
                        ModuleDb.AddParameter(command, "@IsMissing", SqlDbType.Bit, isMissing);
                        AddText(command, "@Decision", SqlDbType.NVarChar, decision, 20);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        AddText(command, "@Remarks", SqlDbType.NVarChar, remarks, 2000);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The selected document is no longer part of this tender.");
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "PrimaryDocumentVerification",
                        "TechnicalBidDocument", isMissing ? (int?)null : documentId,
                        documentType + " — " + decision + ": " + remarks, Request.UserHostAddress);
                    transaction.Commit();
                }

                Message.Text = "<div class=\"message success\">Document decision recorded.</div>";
                BindPrimaryVerificationAfterPostback();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Primary document decision could not be recorded.", ex);
                ShowMessage(ex.Number == 51112
                    ? "A primary decision has already been recorded for this document."
                    : ex.Number == 51113
                        ? "The required document was uploaded after the page was loaded. Refresh and verify the uploaded file."
                    : "The document decision could not be saved.");
            }
            catch (InvalidOperationException ex) { ShowMessage(ModuleDb.Html(ex.Message)); }
        }

        private void BindPrimaryVerificationAfterPostback()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                    BindPrimaryVerification(connection);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Primary verification data could not be refreshed.", ex);
                ShowMessage("The verification was saved, but the page could not refresh. Reload it to view the saved result.");
            }
        }

        private void ProcessStage(string expectedStageName, string requiredPermission, string newStatus,
            string remarks, DateTime? publicationStartUtc, DateTime? publicationCloseUtc)
        {
            if (!HasPermission(requiredPermission) || remarks.Length > 2000)
            {
                ShowMessage("Your role is not authorized for this action, or the remarks are too long.");
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    int stageId, assignedRoleId;
                    int? assignedUserId;
                    int departmentId;
                    string previousStatus;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT t.CurrentStatus,s.StageId,s.StatusOnPending,s.StatusOnApprove,
                                 a.AssignedRoleId,a.AssignedUserId,a.DepartmentId
                          FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
                          INNER JOIN dbo.TenderWorkflowAssignments a WITH (UPDLOCK,HOLDLOCK)
                            ON a.TenderId=t.TenderId AND a.IsCurrent=1
                          INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId AND s.WorkflowVersion=2 AND s.IsActive=1
                          WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                            AND s.StageName=@StageName;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        AddText(command, "@StageName", SqlDbType.NVarChar, expectedStageName, 100);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("The tender is no longer at this workflow stage.");
                            previousStatus = Convert.ToString(reader["CurrentStatus"]);
                            if (!String.Equals(previousStatus, Convert.ToString(reader["StatusOnPending"]), StringComparison.Ordinal))
                                throw new InvalidOperationException("Tender status and workflow stage are inconsistent. Contact support.");
                            if (!String.Equals(Convert.ToString(reader["StatusOnApprove"]), newStatus, StringComparison.Ordinal))
                                throw new InvalidOperationException("The requested status transition is not configured.");
                            assignedRoleId = Convert.ToInt32(reader["AssignedRoleId"]);
                            assignedUserId = reader["AssignedUserId"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["AssignedUserId"]);
                            departmentId = Convert.ToInt32(reader["DepartmentId"]);
                            stageId = Convert.ToInt32(reader["StageId"]);
                        }
                    }
                    if (assignedRoleId != CurrentRoleId ||
                        (assignedUserId.HasValue && assignedUserId.Value != CurrentUserId) ||
                        (HasAnyRole("Junior Engineer (JE)") &&
                         (!CurrentDepartmentId.HasValue || CurrentDepartmentId.Value != departmentId)))
                        throw new InvalidOperationException("The tender is not assigned to your role, user, or department.");

                    if (expectedStageName == "Final_JE_SubmitTender")
                    {
                        EnsureTenderPreparationComplete(connection, transaction);
                        TenderFinalizedWorkflowService.EnsureTenderRequirementDocumentsVerified(
                            connection, transaction, TenderId, "JE");
                        SaveRequiredBidDocumentTypes(connection, transaction);
                    }
                    if (expectedStageName == "Final_JE_PublishTender")
                    {
                        DateTime configuredStartUtc, configuredCloseUtc;
                        LoadPublicationSchedule(connection, transaction,
                            out configuredStartUtc, out configuredCloseUtc);
                        publicationStartUtc = configuredStartUtc;
                        publicationCloseUtc = configuredCloseUtc;
                    }
                    if (expectedStageName == "Final_JE_OpenTechnicalBids")
                        SaveTechnicalBidOpening(connection, transaction, remarks);
                    if (expectedStageName == "Final_JE_CompletePrimaryVerification")
                        EnsurePrimaryVerificationComplete(connection, transaction);

                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.Tender SET CurrentStatus=@NewStatus,ModifiedBy=@UserId,ModifiedDate=SYSUTCDATETIME(),
                              SubmissionStartDate=CASE WHEN @StartUtc IS NULL THEN SubmissionStartDate ELSE @StartUtc END,
                              SubmissionDeadline=CASE WHEN @CloseUtc IS NULL THEN SubmissionDeadline ELSE @CloseUtc END
                          WHERE TenderId=@TenderId AND CurrentStatus=@PreviousStatus AND WorkflowVersion=2 AND IsDeleted=0;",
                        connection, transaction))
                    {
                        AddText(command, "@NewStatus", SqlDbType.NVarChar, newStatus, 100);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@StartUtc", SqlDbType.DateTime2,
                            publicationStartUtc.HasValue ? (object)publicationStartUtc.Value : null);
                        ModuleDb.AddParameter(command, "@CloseUtc", SqlDbType.DateTime2,
                            publicationCloseUtc.HasValue ? (object)publicationCloseUtc.Value : null);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        AddText(command, "@PreviousStatus", SqlDbType.NVarChar, previousStatus, 100);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Tender status changed; refresh and try again.");
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.TenderWorkflowAssignments
                          SET IsCurrent=0,CompletedAtUtc=SYSUTCDATETIME()
                          WHERE TenderId=@TenderId AND IsCurrent=1 AND StageId=@StageId;",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, stageId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The workflow assignment changed; refresh and try again.");
                    }

                    int? nextStageId = null;
                    int? nextRoleId = null;
                    string nextStageName = null;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT TOP (1) StageId,ApproverRoleId,StageName FROM dbo.WorkflowStages
                          WHERE WorkflowVersion=2 AND IsActive=1 AND StatusOnPending=@NewStatus
                            AND StageOrder>@CurrentOrder ORDER BY StageOrder;",
                        connection, transaction))
                    {
                        AddText(command, "@NewStatus", SqlDbType.NVarChar, newStatus, 100);
                        ModuleDb.AddParameter(command, "@CurrentOrder", SqlDbType.Int, GetStageOrder(connection, transaction, stageId));
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                nextStageId = Convert.ToInt32(reader["StageId"]);
                                nextRoleId = Convert.ToInt32(reader["ApproverRoleId"]);
                                nextStageName = Convert.ToString(reader["StageName"]);
                            }
                        }
                    }
                    if (!nextStageId.HasValue || !nextRoleId.HasValue)
                        throw new InvalidOperationException("The next workflow stage is not configured.");

                    int? nextAssignedUserId = nextRoleId.Value == CurrentRoleId &&
                        HasAnyRole("Junior Engineer (JE)") ? (int?)CurrentUserId : null;
                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TenderWorkflowAssignments
                            (TenderId,StageId,AssignedRoleId,AssignedUserId,DepartmentId,IsCurrent)
                          VALUES(@TenderId,@StageId,@RoleId,@AssignedUserId,@DepartmentId,1);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, nextStageId.Value);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, nextRoleId.Value);
                        ModuleDb.AddParameter(command, "@AssignedUserId", SqlDbType.Int,
                            nextAssignedUserId.HasValue ? (object)nextAssignedUserId.Value : null);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,NotificationType,Message)
                          SELECT u.UserId,@TenderId,N'WorkflowAssignment',
                                 N'A tender is awaiting your action: ' + @StageName + N'.'
                          FROM dbo.Users u
                          WHERE u.IsActive=1 AND u.AccountType=N'Internal' AND u.RoleId=@RoleId
                            AND (u.DepartmentId IS NULL OR u.DepartmentId=@DepartmentId);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, nextRoleId.Value);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        AddText(command, "@StageName", SqlDbType.NVarChar, nextStageName, 80);
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TenderWorkflowHistory
                            (TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,
                             UserId,RoleId,DepartmentId,Remarks)
                          VALUES(@TenderId,2,@StageId,@PreviousStatus,@NewStatus,@ActionName,
                                 @UserId,@RoleId,@DepartmentId,@Remarks);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, stageId);
                        AddText(command, "@PreviousStatus", SqlDbType.NVarChar, previousStatus, 100);
                        AddText(command, "@NewStatus", SqlDbType.NVarChar, newStatus, 100);
                        AddText(command, "@ActionName", SqlDbType.NVarChar, expectedStageName, 100);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        AddText(command, "@Remarks", SqlDbType.NVarChar, remarks, 2000);
                        command.ExecuteNonQuery();
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "WorkflowTransition", "Tender",
                        TenderId, previousStatus + " -> " + newStatus, Request.UserHostAddress);
                    transaction.Commit();
                }

                Response.Redirect("~/Pages/TenderWorkflow.aspx?id=" + TenderId.ToString(CultureInfo.InvariantCulture), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Finalized workflow transition failed.", ex);
                ShowMessage("The workflow action could not be saved. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private static int GetStageOrder(SqlConnection connection, SqlTransaction transaction, int stageId)
        {
            using (SqlCommand command = new SqlCommand(
                "SELECT StageOrder FROM dbo.WorkflowStages WHERE StageId=@StageId;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, stageId);
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("The current workflow stage is not configured.");
                return Convert.ToInt32(value);
            }
        }

            private void EnsureAssignedStage(SqlConnection connection, SqlTransaction transaction,
                string stageName, string requiredPermission)
            {
                if (!HasPermission(requiredPermission))
                    throw new InvalidOperationException("Your role is not authorized to perform this workflow action.");

                using (SqlCommand command = new SqlCommand(
                    @"SELECT COUNT(1)
                      FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
                      INNER JOIN dbo.TenderWorkflowAssignments a WITH (UPDLOCK,HOLDLOCK)
                        ON a.TenderId=t.TenderId AND a.IsCurrent=1
                      INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                        AND s.WorkflowVersion=2 AND s.IsActive=1
                      WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                        AND s.StageName=@StageName AND t.CurrentStatus=s.StatusOnPending
                        AND a.AssignedRoleId=@RoleId
                        AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                        AND (@Role<>N'Junior Engineer (JE)'
                             OR (@DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId
                                 AND t.DepartmentId=@DepartmentId));",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    AddText(command, "@StageName", SqlDbType.NVarChar, stageName, 100);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    AddText(command, "@Role", SqlDbType.NVarChar, CurrentRole, 50);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                        CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                    if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                        throw new InvalidOperationException("The tender is no longer assigned to you at this workflow stage.");
                }
            }

            private void SaveTechnicalBidOpening(SqlConnection connection, SqlTransaction transaction, string remarks)
            {
                if (remarks.Length > 1000)
                    throw new InvalidOperationException("Opening remarks cannot exceed 1,000 characters.");

                DateTime originalClosingUtc;
                DateTime currentClosingUtc;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT s.OriginalClosingUtc,s.CurrentClosingUtc
                      FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
                      INNER JOIN dbo.TenderPublicationSchedule s WITH (UPDLOCK,HOLDLOCK)
                        ON s.TenderId=t.TenderId
                      WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                        AND t.CurrentStatus=N'Tender Published'
                        AND s.PublicationStartUtc<=SYSUTCDATETIME()
                        AND s.CurrentClosingUtc<=SYSUTCDATETIME()
                        AND NOT EXISTS(SELECT 1 FROM dbo.TenderBidOpening o WITH (UPDLOCK,HOLDLOCK)
                                       WHERE o.TenderId=t.TenderId);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("The approved publication period must be closed, and no technical opening may already exist.");
                        originalClosingUtc = Convert.ToDateTime(reader["OriginalClosingUtc"], CultureInfo.InvariantCulture);
                        currentClosingUtc = Convert.ToDateTime(reader["CurrentClosingUtc"], CultureInfo.InvariantCulture);
                    }
                }

                using (SqlCommand command = new SqlCommand(
                    @"SELECT COUNT(1) FROM dbo.TenderRequiredBidDocuments
                      WHERE TenderId=@TenderId;", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                        throw new InvalidOperationException("Configure the tender's required bidder-document checklist before opening technical bids.");
                }

                using (SqlCommand command = new SqlCommand(
                    @"SELECT COUNT(1) FROM dbo.TechnicalBid
                      WHERE TenderId=@TenderId AND Status=N'Submitted';", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                        throw new InvalidOperationException("No technical bids were received. The tender cannot proceed without an approved no-bid or cancellation path.");
                }

                string operatorName = Convert.ToString(Session["FullName"]);
                if (String.IsNullOrWhiteSpace(operatorName))
                {
                    using (SqlCommand command = new SqlCommand(
                        "SELECT FullName FROM dbo.Users WHERE UserId=@UserId AND IsActive=1;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        operatorName = Convert.ToString(command.ExecuteScalar());
                    }
                }
                if (String.IsNullOrWhiteSpace(operatorName) || operatorName.Length > 150)
                    throw new InvalidOperationException("The opening operator identity is unavailable or invalid.");

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderBidOpening
                        (TenderId,OriginalClosingUtc,CurrentClosingUtc,ActualOpeningUtc,OpenedBy,
                         OperatorName,Designation,OpeningRemarks,OpeningStatus)
                      VALUES(@TenderId,@OriginalClosingUtc,@CurrentClosingUtc,SYSUTCDATETIME(),@UserId,
                             @OperatorName,N'Junior Engineer (JE)',@Remarks,N'Opened');",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    ModuleDb.AddParameter(command, "@OriginalClosingUtc", SqlDbType.DateTime2, originalClosingUtc);
                    ModuleDb.AddParameter(command, "@CurrentClosingUtc", SqlDbType.DateTime2, currentClosingUtc);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    AddText(command, "@OperatorName", SqlDbType.NVarChar, operatorName, 150);
                    AddText(command, "@Remarks", SqlDbType.NVarChar, remarks, 1000);
                    command.ExecuteNonQuery();
                }
            }

            private void EnsurePrimaryVerificationComplete(SqlConnection connection, SqlTransaction transaction)
            {
                int bidderCount;
                using (SqlCommand command = new SqlCommand(
                    "SELECT COUNT(1) FROM dbo.TechnicalBid WHERE TenderId=@TenderId AND Status=N'Submitted';",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    bidderCount = Convert.ToInt32(command.ExecuteScalar());
                }
                if (bidderCount == 0)
                    throw new InvalidOperationException("No submitted technical bids are available for primary verification.");

                using (SqlCommand command = new SqlCommand(
                    "SELECT COUNT(1) FROM dbo.TenderRequiredBidDocuments WHERE TenderId=@TenderId;",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                        throw new InvalidOperationException("The tender has no configured required bidder-document checklist.");
                }

                using (SqlCommand command = new SqlCommand(
                    @"SELECT COUNT(1)
                      FROM dbo.TechnicalBid b
                      WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                        AND
                        (
                            NOT EXISTS
                            (
                                SELECT 1 FROM dbo.TenderVerificationReviews r
                                WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                                  AND r.ReviewStage=N'Primary' AND r.Decision=N'Completed'
                            )
                            OR
                            (
                                NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                           WHERE d.TechnicalBidId=b.TechnicalBidId)
                                AND NOT EXISTS
                                (
                                    SELECT 1 FROM dbo.TenderVerificationReviews r
                                    WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                                      AND r.ReviewStage=N'Primary' AND r.Decision=N'Completed'
                                      AND NULLIF(LTRIM(RTRIM(r.Remarks)),N'') IS NOT NULL
                                )
                            )
                            OR EXISTS
                            (
                                SELECT 1 FROM dbo.TechnicalBidDocuments d
                                WHERE d.TechnicalBidId=b.TechnicalBidId
                                  AND NOT EXISTS
                                  (
                                      SELECT 1 FROM dbo.TenderDocumentVerificationHistory h
                                      WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                                        AND h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                                        AND h.ReviewStage=N'Primary'
                                  )
                            )
                            OR EXISTS
                            (
                                    SELECT 1 FROM dbo.TenderRequiredBidDocuments requiredDocument
                                    WHERE requiredDocument.TenderId=b.TenderId
                                      AND
                                      (
                                          (
                                              NOT EXISTS
                                              (
                                                  SELECT 1 FROM dbo.TechnicalBidDocuments d
                                                  WHERE d.TechnicalBidId=b.TechnicalBidId
                                                    AND d.DocumentType=requiredDocument.DocumentType
                                              )
                                              AND NOT EXISTS
                                              (
                                                  SELECT 1 FROM dbo.TenderDocumentVerificationHistory h
                                                  WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                                                    AND h.TechnicalBidDocumentId IS NULL
                                                    AND h.DocumentType=requiredDocument.DocumentType
                                                    AND h.ReviewStage=N'Primary'
                                              )
                                          )
                                          OR EXISTS
                                          (
                                              SELECT 1 FROM dbo.TechnicalBidDocuments d
                                              WHERE d.TechnicalBidId=b.TechnicalBidId
                                                AND d.DocumentType=requiredDocument.DocumentType
                                                AND NOT EXISTS
                                                (
                                                    SELECT 1 FROM dbo.TenderDocumentVerificationHistory h
                                                    WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                                                      AND h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                                                      AND h.ReviewStage=N'Primary'
                                                )
                                          )
                                      )
                            )
                        );",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                        throw new InvalidOperationException("Record a bidder review for every bidder, explain any bidder with no uploaded files, and decide every uploaded technical document before completing verification.");
                }
            }

        private void EnsureTenderPreparationComplete(SqlConnection connection, SqlTransaction transaction)
        {
            List<TenderSubmissionReadinessItem> items =
                GetTenderSubmissionReadiness(connection, transaction);
            List<string> pending = new List<string>();
            foreach (TenderSubmissionReadinessItem item in items)
                if (!item.IsComplete)
                    pending.Add(item.Name + " (Step: " + item.Step + ")");

            if (pending.Count > 0)
                throw new InvalidOperationException(
                    "This tender remains with JE. Complete the following before forwarding to Tender Cell: " +
                    String.Join("; ", pending.ToArray()) + ".");
        }

        private void SaveRequiredBidDocumentTypes(SqlConnection connection, SqlTransaction transaction)
        {
            if (String.IsNullOrWhiteSpace(RequiredBidDocumentsText.Text) ||
                RequiredBidDocumentsText.Text.Length > 8000)
                throw new InvalidOperationException("Enter at least one required bidder document type, one per line.");

            List<string> documentTypes = new List<string>();
            HashSet<string> uniqueDocumentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = RequiredBidDocumentsText.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string documentType = line.Trim();
                if (documentType.Length == 0) continue;
                if (documentType.Length > 160)
                    throw new InvalidOperationException("Each required document type must be 160 characters or fewer.");
                if (!uniqueDocumentTypes.Add(documentType))
                    throw new InvalidOperationException("Required document types cannot be duplicated.");
                documentTypes.Add(documentType);
            }
            if (documentTypes.Count == 0 || documentTypes.Count > 30)
                throw new InvalidOperationException("Enter between 1 and 30 required bidder document types, one per line.");

            List<string> documentTypeParameters = new List<string>();
            using (SqlCommand command = new SqlCommand())
            {
                command.Connection = connection;
                command.Transaction = transaction;
                for (int i = 0; i < documentTypes.Count; i++)
                {
                    string parameterName = "@DocumentType" + i.ToString(CultureInfo.InvariantCulture);
                    documentTypeParameters.Add(parameterName);
                    AddText(command, parameterName, SqlDbType.NVarChar, documentTypes[i], 160);
                }
                command.CommandText =
                    "DELETE FROM dbo.TenderRequiredBidDocuments WHERE TenderId=@TenderId AND DocumentType NOT IN (" +
                    String.Join(",", documentTypeParameters.ToArray()) + ");";
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                command.ExecuteNonQuery();
            }
            foreach (string documentType in documentTypes)
            {
                using (SqlCommand command = new SqlCommand(
                    @"IF NOT EXISTS
                      (
                          SELECT 1 FROM dbo.TenderRequiredBidDocuments WITH (UPDLOCK,HOLDLOCK)
                          WHERE TenderId=@TenderId AND DocumentType=@DocumentType
                      )
                          INSERT INTO dbo.TenderRequiredBidDocuments(TenderId,DocumentType,CreatedBy)
                          VALUES(@TenderId,@DocumentType,@UserId);", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, documentType, 160);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    command.ExecuteNonQuery();
                }
            }
        }

        private void LoadPublicationSchedule(SqlConnection connection, SqlTransaction transaction,
            out DateTime startUtc, out DateTime closeUtc)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT PublicationStartUtc,CurrentClosingUtc
                  FROM dbo.TenderPublicationSchedule WITH (UPDLOCK,HOLDLOCK)
                  WHERE TenderId=@TenderId;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Tender Cell must save a publication schedule before JE can publish this tender.");
                    startUtc = DateTime.SpecifyKind(
                        Convert.ToDateTime(reader["PublicationStartUtc"], CultureInfo.InvariantCulture),
                        DateTimeKind.Utc);
                    closeUtc = DateTime.SpecifyKind(
                        Convert.ToDateTime(reader["CurrentClosingUtc"], CultureInfo.InvariantCulture),
                        DateTimeKind.Utc);
                }
            }
            if (startUtc <= DateTime.UtcNow || closeUtc <= startUtc)
                throw new InvalidOperationException("The saved publication schedule is invalid or has already started. Tender Cell must update it before publication.");
        }

        private void LoadPublicationScheduleSummary(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT PublicationStartUtc,CurrentClosingUtc
                  FROM dbo.TenderPublicationSchedule WHERE TenderId=@TenderId;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        PublicationScheduleSummary.Text = "No schedule has been saved. Contact Tender Cell before publishing.";
                        return;
                    }
                    DateTime startUtc = DateTime.SpecifyKind(
                        Convert.ToDateTime(reader["PublicationStartUtc"], CultureInfo.InvariantCulture),
                        DateTimeKind.Utc);
                    DateTime closeUtc = DateTime.SpecifyKind(
                        Convert.ToDateTime(reader["CurrentClosingUtc"], CultureInfo.InvariantCulture),
                        DateTimeKind.Utc);
                    TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                    PublicationScheduleSummary.Text = "Starts: " +
                        HttpUtility.HtmlEncode(TimeZoneInfo.ConvertTimeFromUtc(startUtc, indiaTimeZone)
                            .ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)) +
                        " IST · Closes: " +
                        HttpUtility.HtmlEncode(TimeZoneInfo.ConvertTimeFromUtc(closeUtc, indiaTimeZone)
                            .ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)) + " IST";
                }
            }
        }

        private static bool TryParseIndiaDateTime(string value, out DateTime utc)
        {
            utc = DateTime.MinValue;
            DateTime localTime;
            if (!DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out localTime)) return false;
            TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), indiaTimeZone);
            return true;
        }

        private void DenyAccess()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            Response.Redirect("~/Pages/Error.aspx?code=403", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void ShowMessage(string text)
        {
            Message.Text = "<div class=\"message\">" + text + "</div>";
        }
    }
}
