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
    public partial class ApplicationDetails : ModulePage
    {
        protected Literal Message, TenderTitle, ApplicationReference, TenderNumber, ContractorName,
            RepresentativeName, ContractorMobile, ContractorEmail, DepartmentName, EstimatedAmount,
            ApplicationStatus, SubmittedDate, ContractorAddress;
        protected HyperLink BackLink;
        protected Panel ContractorPanel, ContractorUploadPanel, ContractorSubmitPanel,
            ContractorResponsePanel, InternalPanel, ReviewDocumentsPanel, InternalRemarkPanel,
            ApplicationDecisionPanel, SecondaryApplicationDecisionPanel, ShortfallRequestPanel,
            ContractorShortfallPanel, FinalDocumentsPanel;
        protected DropDownList UploadDocumentTypeSelect, InternalRemarkTypeSelect;
        protected DropDownList SecondaryApplicationDecisionSelect;
        protected FileUpload ApplicationDocumentUpload;
        protected Button UploadDocumentsButton, SubmitApplicationButton, SendResponseButton,
            AddInternalRemarkButton, QualifyButton, DisqualifyButton,
            RecordSecondaryApplicationDecisionButton, CreateShortfallButton;
        protected CheckBox TermsAcceptedCheck;
        protected GridView RequirementsGrid, DocumentsGrid, ReviewDocumentsGrid, RemarksGrid, HistoryGrid,
            ShortfallRequestItemsGrid, ContractorShortfallGrid, FinalDocumentsGrid;
        protected TextBox ContractorResponseText, InternalRemarkText, DecisionRemarksText;
        protected TextBox SecondaryApplicationDecisionRemarksText, ShortfallRemarksText;

        private int ApplicationId { get { return QueryId("id"); } }
        private int ApplicationTenderId { get; set; }
        private bool IsContractor { get { return HasAnyRole("ExternalBidder"); } }
        private bool IsJe { get { return HasAnyRole("Junior Engineer (JE)"); } }
        private bool IsTenderCell { get { return TenderFinalizedWorkflowService.IsTenderCell(CurrentRoleCode); } }

        protected void Page_Init(object sender, EventArgs e)
        {
            if (!RequireRoles("ExternalBidder", "Junior Engineer (JE)", "Tender Cell")) return;
            if (!IsContractor && !RequirePermission(IsJe ? "tender.primary.verify" : "tender.cell.verify"))
            {
                return;
            }

            if (ApplicationId <= 0)
            {
                NotFound();
                return;
            }

            try
            {
                LoadApplication();
            }
            catch (InvalidOperationException)
            {
                NotFound();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Contractor application details could not be loaded.", ex);
                ShowMessage("The application could not be loaded. Please retry or contact support.");
            }
        }

        private void LoadApplication()
        {
            DataRow application;
            DataTable requirements;
            bool windowOpen;
            bool assigned;
            string assignedStage = String.Empty;
            using (SqlConnection connection = ModuleDb.OpenConnection())
            {
                if (!CanViewApplication(connection)) throw new InvalidOperationException("Application is not available.");
                using (SqlCommand command = new SqlCommand(
                    @"SELECT b.TechnicalBidId,b.TenderId,b.ContractorId,b.SubmittedDate,
                             t.TenderNumber,t.Title,t.EstimatedAmount,d.DepartmentName,
                             c.Name AS ContractorName,c.Address,c.ContactNo,c.Email,
                             u.FullName AS RepresentativeName,
                             COALESCE(latest.NewStatus,b.Status) AS ApplicationStatus
                      FROM dbo.TechnicalBid b
                      INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                      INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                      INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                      LEFT JOIN dbo.Users u ON u.UserId=c.UserId
                      OUTER APPLY
                      (
                          SELECT TOP (1) h.NewStatus
                          FROM dbo.ContractorApplicationHistory h
                          WHERE h.TechnicalBidId=b.TechnicalBidId AND h.NewStatus IS NOT NULL
                          ORDER BY h.HistoryId DESC
                      ) latest
                      WHERE b.TechnicalBidId=@ApplicationId;", connection))
                {
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                    DataTable data = new DataTable();
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                    if (data.Rows.Count == 0) throw new InvalidOperationException("Application is not available.");
                    application = data.Rows[0];
                }

                int tenderId = Convert.ToInt32(application["TenderId"], CultureInfo.InvariantCulture);
                ApplicationTenderId = tenderId;
                requirements = ContractorApplicationRules.GetRequirements(connection, null, tenderId);
                windowOpen = ContractorApplicationRules.IsSubmissionWindowOpen(connection, null, tenderId);
                assigned = !IsContractor && ContractorApplicationService.HasCurrentAssignment(
                    connection, ApplicationId, CurrentUserId, CurrentRoleId, CurrentRoleCode, CurrentDepartmentId);
                if (assigned)
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT s.StageName
                          FROM dbo.TenderWorkflowAssignments a
                          INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                            AND s.WorkflowVersion=2 AND s.IsActive=1
                          WHERE a.TenderId=@TenderId AND a.IsCurrent=1 AND a.AssignedRoleId=@RoleId
                            AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                            AND a.DepartmentId=(SELECT DepartmentId FROM dbo.Tender WHERE TenderId=@TenderId)
                            AND (@IsJE=0 OR (@DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId));",
                        connection))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@IsJE", SqlDbType.Bit, IsJe);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        assignedStage = Convert.ToString(command.ExecuteScalar());
                    }
                }
            }

            string status = Convert.ToString(application["ApplicationStatus"]);
            string submittedReference = ContractorApplicationRules.GetReference(
                ApplicationId, Convert.ToDateTime(application["SubmittedDate"], CultureInfo.InvariantCulture));
            TenderTitle.Text = Encode(application["Title"]);
            TenderNumber.Text = Encode(application["TenderNumber"]);
            ApplicationReference.Text = Encode(submittedReference);
            ContractorName.Text = Encode(application["ContractorName"]);
            RepresentativeName.Text = Encode(application["RepresentativeName"]);
            ContractorMobile.Text = Encode(application["ContactNo"]);
            ContractorEmail.Text = Encode(application["Email"]);
            DepartmentName.Text = Encode(application["DepartmentName"]);
            EstimatedAmount.Text = Encode(Convert.ToDecimal(application["EstimatedAmount"], CultureInfo.InvariantCulture)
                .ToString("N2", CultureInfo.CurrentCulture));
            ContractorAddress.Text = Encode(application["Address"]);
            ApplicationStatus.Text = Encode(status);
            SubmittedDate.Text = String.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)
                ? "Not submitted"
                : Encode(Convert.ToDateTime(application["SubmittedDate"], CultureInfo.InvariantCulture)
                    .ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture));
            BackLink.NavigateUrl = IsContractor
                ? ResolveUrl("~/Pages/MyApplications.aspx")
                : ResolveUrl("~/Pages/Applications.aspx");

            BindRequirements(requirements, ApplicationId);
            BindDocuments();
            BindRemarks();
            BindHistory();
            FinalDocumentsPanel.Visible = IsContractor;
            if (IsContractor) BindFinalDocuments();

            ContractorPanel.Visible = IsContractor;
            InternalPanel.Visible = !IsContractor;
            bool canUpdate = IsContractor && windowOpen && status == "Draft";
            ContractorUploadPanel.Visible = canUpdate;
            ContractorSubmitPanel.Visible = canUpdate;
            ContractorResponsePanel.Visible = false;
            SubmitApplicationButton.Visible = canUpdate;
            ReviewDocumentsPanel.Visible = IsTenderCell && assigned &&
                (assignedStage == "Final_Cell_CompleteSecondaryVerification" ||
                 assignedStage == "Final_Cell_ReverifyRejectedOpening") && status != "Draft";
            ReviewDocumentsGrid.DataSource = ReviewDocumentsPanel.Visible ? GetDocuments() : null;
            ReviewDocumentsGrid.DataBind();
            SecondaryApplicationDecisionPanel.Visible = ReviewDocumentsPanel.Visible;
            ShortfallRequestPanel.Visible = IsTenderCell && assigned &&
                assignedStage == "Final_Cell_CreateShortfall" && status != "Draft";
            if (ShortfallRequestPanel.Visible)
            {
                ShortfallRequestItemsGrid.DataSource = GetShortfallCandidateDocuments();
                ShortfallRequestItemsGrid.DataBind();
            }
            InternalRemarkPanel.Visible = false;
            ApplicationDecisionPanel.Visible = false;
            ContractorShortfallPanel.Visible = IsContractor && BindContractorShortfallItems();
        }

        private void BindFinalDocuments()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.GeneratedDocumentId,d.DocumentKind,d.VersionNumber,d.FileName,d.GeneratedAtUtc
                  FROM dbo.TenderGeneratedDocuments d
                  WHERE d.TenderId=@TenderId
                    AND EXISTS
                    (
                        SELECT 1 FROM dbo.TechnicalBid b
                        INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                          AND c.UserId=@UserId AND c.IsActive=1
                        WHERE b.TechnicalBidId=@ApplicationId AND b.TenderId=d.TenderId
                    )
                    AND
                    (
                        d.DocumentKind=N'BOQ'
                        OR (d.WorkOrderId IS NOT NULL AND EXISTS
                            (SELECT 1 FROM dbo.WorkOrder w
                             INNER JOIN dbo.Contractors c ON c.ContractorId=w.ContractorId
                             WHERE w.WorkOrderId=d.WorkOrderId AND c.UserId=@UserId))
                        OR (d.AgreementId IS NOT NULL AND EXISTS
                            (SELECT 1 FROM dbo.Agreement a
                             INNER JOIN dbo.Contractors c ON c.ContractorId=a.ContractorId
                             WHERE a.AgreementId=d.AgreementId AND c.UserId=@UserId))
                    )
                  ORDER BY d.GeneratedAtUtc DESC,d.GeneratedDocumentId DESC;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, ApplicationTenderId);
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                DataTable data = new DataTable();
                adapter.Fill(data);
                FinalDocumentsGrid.DataSource = data;
                FinalDocumentsGrid.DataBind();
            }
        }

        private bool CanViewApplication(SqlConnection connection)
        {
            if (IsContractor)
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT COUNT(1)
                      FROM dbo.TechnicalBid b
                      INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                      INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                      WHERE b.TechnicalBidId=@ApplicationId AND c.UserId=@UserId AND c.IsActive=1;",
                    connection))
                {
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
                }
            }
            return ContractorApplicationService.CanInternalUserView(connection, ApplicationId,
                CurrentUserId, CurrentRoleId, CurrentRoleCode);
        }

        private void BindRequirements(DataTable requirements, int applicationId)
        {
            DataTable uploaded = new DataTable();
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT DISTINCT DocumentType FROM dbo.TechnicalBidDocuments WHERE TechnicalBidId=@ApplicationId;",
                connection))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(uploaded);
            }

            requirements.Columns.Add("IsUploaded", typeof(bool));
            foreach (DataRow requirement in requirements.Rows)
            {
                bool exists = false;
                foreach (DataRow document in uploaded.Rows)
                {
                    if (String.Equals(Convert.ToString(requirement["DocumentType"]),
                        Convert.ToString(document["DocumentType"]), StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                requirement["IsUploaded"] = exists;
            }
            RequirementsGrid.DataSource = requirements;
            RequirementsGrid.DataBind();

            UploadDocumentTypeSelect.Items.Clear();
            UploadDocumentTypeSelect.Items.Add(new ListItem("Select a requirement", String.Empty));
            foreach (DataRow requirement in requirements.Rows)
            {
                UploadDocumentTypeSelect.Items.Add(new ListItem(
                    Convert.ToString(requirement["DocumentType"]), Convert.ToString(requirement["DocumentType"])));
            }
        }

        private void BindDocuments()
        {
            DataTable documents = GetDocuments();
            DataView uploadedDocuments = new DataView(documents);
            uploadedDocuments.RowFilter = "IsMissing = false";
            DocumentsGrid.DataSource = uploadedDocuments;
            DocumentsGrid.DataBind();
        }

        private DataTable GetDocuments()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.TechnicalBidDocumentId,d.DocumentType,d.FileName,d.UploadedDate AS UploadedAt,
                         u.FullName AS UploadedBy,review.VerificationStatus,review.Remarks AS VerificationRemarks,
                         CONVERT(bit,0) AS IsMissing
                  FROM dbo.TechnicalBidDocuments d
                  LEFT JOIN dbo.Users u ON u.UserId=d.UploadedBy
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus,h.Remarks
                      FROM dbo.TenderDocumentVerificationHistory h
                      WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId AND h.ReviewStage=N'Secondary'
                      ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                  ) review
                  WHERE d.TechnicalBidId=@ApplicationId
                  UNION ALL
                  SELECT CONVERT(int,NULL),r.DocumentType,N'(not submitted)',CONVERT(datetime2,NULL),
                         N'',review.VerificationStatus,review.Remarks,CONVERT(bit,1)
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.TenderRequiredBidDocuments r ON r.TenderId=b.TenderId AND r.IsMandatory=1
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus,h.Remarks
                      FROM dbo.TenderDocumentVerificationHistory h
                      WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                        AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                        AND h.ReviewStage=N'Secondary'
                      ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                  ) review
                  WHERE b.TechnicalBidId=@ApplicationId
                    AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                   WHERE d.TechnicalBidId=b.TechnicalBidId
                                     AND d.DocumentType=r.DocumentType)
                  ORDER BY UploadedAt DESC,TechnicalBidDocumentId DESC;", connection))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                return data;
            }
        }

        private DataTable GetShortfallCandidateDocuments()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.TechnicalBidDocumentId,d.DocumentType,d.FileName,
                         CONVERT(bit,0) AS IsMissing,latest.VerificationRemarks
                  FROM dbo.TechnicalBidDocuments d
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus,h.Remarks AS VerificationRemarks
                      FROM dbo.TenderDocumentVerificationHistory h
                      WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                        AND h.ReviewStage=N'Secondary'
                      ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                  ) latest
                  WHERE d.TechnicalBidId=@ApplicationId AND latest.VerificationStatus=N'Not Verified'
                  UNION ALL
                  SELECT CONVERT(int,NULL),r.DocumentType,N'(not submitted)',
                         CONVERT(bit,1),latest.VerificationRemarks
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.TenderRequiredBidDocuments r ON r.TenderId=b.TenderId AND r.IsMandatory=1
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus,h.Remarks AS VerificationRemarks
                      FROM dbo.TenderDocumentVerificationHistory h
                      WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                        AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                        AND h.ReviewStage=N'Secondary'
                      ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                  ) latest
                  WHERE b.TechnicalBidId=@ApplicationId AND latest.VerificationStatus=N'Not Verified'
                    AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                   WHERE d.TechnicalBidId=b.TechnicalBidId AND d.DocumentType=r.DocumentType)
                  ORDER BY DocumentType,TechnicalBidDocumentId;", connection))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                return data;
            }
        }

        private bool BindContractorShortfallItems()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT s.ShortfallId,i.ShortfallItemId,i.RequiredDocumentType,
                         i.ShortfallDescription,i.CorrectiveAction,i.ClearanceStatus,
                         s.SubmissionDeadlineUtc,
                         CONVERT(bit,CASE WHEN s.SubmissionDeadlineUtc>SYSUTCDATETIME()
                             THEN 1 ELSE 0 END) AS CanUpload
                  FROM dbo.TenderShortfalls s
                  INNER JOIN dbo.TenderShortfallItems i ON i.ShortfallId=s.ShortfallId
                  INNER JOIN dbo.TechnicalBid b ON b.TenderId=s.TenderId AND b.ContractorId=s.ContractorId
                  INNER JOIN dbo.Contractors c ON c.ContractorId=s.ContractorId AND c.UserId=@UserId
                  INNER JOIN dbo.TenderWorkflowAssignments a
                    ON a.TenderId=s.TenderId AND a.ShortfallId=s.ShortfallId AND a.IsCurrent=1
                  INNER JOIN dbo.WorkflowStages ws ON ws.StageId=a.StageId
                    AND ws.WorkflowVersion=2 AND ws.IsActive=1
                    AND ws.StageName=N'Final_Contractor_SubmitShortfall'
                  WHERE b.TechnicalBidId=@ApplicationId AND s.Status=N'Awaiting Contractor'
                    AND i.ClearanceStatus IN (N'Pending',N'Continued Deficiency')
                  ORDER BY i.ShortfallItemId;", connection))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                ContractorShortfallGrid.DataSource = data;
                ContractorShortfallGrid.DataBind();
                return data.Rows.Count > 0;
            }
        }

        private void BindRemarks()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT r.RemarkType,r.Message,u.FullName AS Author,r.CreatedAtUtc
                  FROM dbo.ContractorApplicationRemarks r
                  INNER JOIN dbo.Users u ON u.UserId=r.CreatedBy
                  WHERE r.TechnicalBidId=@ApplicationId
                    AND (@IsInternal=1 OR r.VisibleToContractor=1)
                  ORDER BY r.CreatedAtUtc DESC,r.RemarkId DESC;", connection))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                ModuleDb.AddParameter(command, "@IsInternal", SqlDbType.Bit, !IsContractor);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                RemarksGrid.DataSource = data;
                RemarksGrid.DataBind();
            }
        }

        private void BindHistory()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT h.EventType,h.PreviousStatus,h.NewStatus,h.Remarks,u.FullName AS Author,h.CreatedAtUtc
                  FROM dbo.ContractorApplicationHistory h
                  INNER JOIN dbo.Users u ON u.UserId=h.CreatedBy
                  WHERE h.TechnicalBidId=@ApplicationId
                    AND (@IsInternal=1 OR h.VisibleToContractor=1)
                  ORDER BY h.CreatedAtUtc DESC,h.HistoryId DESC;", connection))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, ApplicationId);
                ModuleDb.AddParameter(command, "@IsInternal", SqlDbType.Bit, !IsContractor);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                HistoryGrid.DataSource = data;
                HistoryGrid.DataBind();
            }
        }

        protected void UploadDocumentsButton_Click(object sender, EventArgs e)
        {
            try
            {
                ContractorApplicationService.AddDocuments(this, ApplicationId, CurrentUserId, CurrentRoleId,
                    UploadDocumentTypeSelect.SelectedValue, ApplicationDocumentUpload.PostedFiles,
                    Request.UserHostAddress);
                ShowMessage("Documents uploaded successfully.");
                LoadApplication();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application documents could not be saved.", ex);
                ShowMessage("The documents could not be saved. Please retry or contact support.");
            }
            catch (IOException ex)
            {
                Trace.Warn("DepartmentModule", "Application document storage failed.", ex);
                ShowMessage("The document upload could not be completed. Please retry or contact support.");
            }
        }

        protected void SubmitApplicationButton_Click(object sender, EventArgs e)
        {
            try
            {
                ContractorApplicationService.Submit(ApplicationId, CurrentUserId, CurrentRoleId,
                    TermsAcceptedCheck.Checked, ContractorApplicationRules.TermsVersion, Request.UserHostAddress);
                ShowMessage("Application submitted successfully.");
                LoadApplication();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Contractor application could not be submitted.", ex);
                ShowMessage("The application could not be submitted. Please retry or contact support.");
            }
        }

        protected void SendResponseButton_Click(object sender, EventArgs e)
        {
            AddRemark(true, "Response", ContractorResponseText.Text);
        }

        protected void AddInternalRemarkButton_Click(object sender, EventArgs e)
        {
            AddRemark(false, InternalRemarkTypeSelect.SelectedValue, InternalRemarkText.Text);
        }

        private void AddRemark(bool contractor, string type, string text)
        {
            try
            {
                ContractorApplicationService.AddRemark(ApplicationId, CurrentUserId, CurrentRoleId,
                    CurrentRoleCode, CurrentDepartmentId, contractor, type, text, Request.UserHostAddress);
                ShowMessage(contractor ? "Your clarification response was sent." : "Review remark added.");
                LoadApplication();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application remark could not be saved.", ex);
                ShowMessage("The remark could not be saved. Please retry or contact support.");
            }
        }

        protected void ReviewDocumentsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "ReviewDocument", StringComparison.Ordinal)) return;
            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out rowIndex) ||
                rowIndex < 0 || rowIndex >= ReviewDocumentsGrid.Rows.Count)
            {
                ShowMessage("Select a valid application document.");
                return;
            }
            GridViewRow row = ReviewDocumentsGrid.Rows[rowIndex];
            DropDownList decision = row.FindControl("DocumentDecisionSelect") as DropDownList;
            TextBox remarks = row.FindControl("DocumentReviewRemarksText") as TextBox;
            int? documentId = null;
            bool isMissing = Convert.ToBoolean(ReviewDocumentsGrid.DataKeys[rowIndex].Values["IsMissing"]);
            if (!isMissing)
            {
                int parsedDocumentId;
                if (!Int32.TryParse(Convert.ToString(ReviewDocumentsGrid.DataKeys[rowIndex].Values["TechnicalBidDocumentId"]),
                        NumberStyles.None, CultureInfo.InvariantCulture, out parsedDocumentId))
                {
                    ShowMessage("The selected document could not be read.");
                    return;
                }
                documentId = parsedDocumentId;
            }
            string documentType = Convert.ToString(
                ReviewDocumentsGrid.DataKeys[rowIndex].Values["DocumentType"]);
            if (decision == null || remarks == null)
            {
                ShowMessage("The selected document could not be read.");
                return;
            }

            try
            {
                TenderFinalizedWorkflowService.RecordSecondaryDocumentDecision(ApplicationId,
                    documentId, documentType, isMissing, CurrentUserId, CurrentRoleId,
                    CurrentRoleCode, CurrentDepartmentId, decision.SelectedValue,
                    remarks.Text, Request.UserHostAddress);
                ShowMessage("Document decision recorded.");
                LoadApplication();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application document review could not be saved.", ex);
                ShowMessage("The document decision could not be saved. Please retry or contact support.");
            }
        }

        protected void RecordSecondaryApplicationDecisionButton_Click(object sender, EventArgs e)
        {
            try
            {
                TenderFinalizedWorkflowService.RecordSecondaryApplicationDecision(ApplicationId,
                    CurrentUserId, CurrentRoleId, CurrentRoleCode, CurrentDepartmentId,
                    SecondaryApplicationDecisionSelect.SelectedValue,
                    SecondaryApplicationDecisionRemarksText.Text, Request.UserHostAddress);
                ShowMessage("Secondary bidder decision recorded.");
                LoadApplication();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Secondary bidder decision could not be recorded.", ex);
                ShowMessage("The bidder decision could not be saved. Please retry or contact support.");
            }
        }

        protected void CreateShortfallButton_Click(object sender, EventArgs e)
        {
            List<ShortfallItemInput> items = new List<ShortfallItemInput>();
            foreach (GridViewRow row in ShortfallRequestItemsGrid.Rows)
            {
                CheckBox selected = row.FindControl("CreateShortfallItemCheck") as CheckBox;
                if (selected == null || !selected.Checked) continue;
                TextBox description = row.FindControl("ShortfallDescriptionText") as TextBox;
                TextBox correctiveAction = row.FindControl("ShortfallCorrectiveActionText") as TextBox;
                if (description == null || correctiveAction == null)
                {
                    ShowMessage("The selected shortfall item controls could not be read.");
                    return;
                }
                int? documentId = null;
                object key = ShortfallRequestItemsGrid.DataKeys[row.RowIndex].Values["TechnicalBidDocumentId"];
                int parsedId;
                if (Int32.TryParse(Convert.ToString(key), NumberStyles.None,
                        CultureInfo.InvariantCulture, out parsedId))
                    documentId = parsedId;
                items.Add(new ShortfallItemInput
                {
                    TechnicalBidDocumentId = documentId,
                    DocumentType = Convert.ToString(
                        ShortfallRequestItemsGrid.DataKeys[row.RowIndex].Values["DocumentType"]),
                    Description = description.Text,
                    CorrectiveAction = correctiveAction.Text
                });
            }

            ProcessShortfallCreation(items);
        }

        private void ProcessShortfallCreation(IList<ShortfallItemInput> items)
        {
            try
            {
                TenderFinalizedWorkflowService.Transition(
                    Convert.ToInt32(ApplicationTenderId, CultureInfo.InvariantCulture),
                    CurrentUserId, CurrentRoleId, CurrentRoleCode, CurrentDepartmentId,
                    "Final_Cell_CreateShortfall", "Shortfall Pending JE Approval",
                    ShortfallRemarksText.Text, null, "shortfall.create", Request.UserHostAddress,
                    (connection, transaction, departmentId, previousStatus, newStatus) =>
                        TenderFinalizedWorkflowService.CreateStructuredShortfall(connection,
                            transaction, Convert.ToInt32(ApplicationTenderId, CultureInfo.InvariantCulture),
                            ApplicationId, CurrentUserId, CurrentRoleId, departmentId,
                            ShortfallRemarksText.Text, items, Request.UserHostAddress));
                Response.Redirect("~/Pages/TenderWorkflow.aspx?id=" +
                    ApplicationTenderId.ToString(CultureInfo.InvariantCulture), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "The structured shortfall could not be created.", ex);
                ShowMessage("The shortfall could not be saved. Please retry or contact support.");
            }
        }

        protected void ContractorShortfallGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "SubmitShortfallItem", StringComparison.Ordinal))
                return;
            int rowIndex;
            if (!IsContractor || !Int32.TryParse(Convert.ToString(e.CommandArgument),
                    NumberStyles.None, CultureInfo.InvariantCulture, out rowIndex) ||
                rowIndex < 0 || rowIndex >= ContractorShortfallGrid.Rows.Count)
            {
                ShowMessage("Select a valid shortfall item.");
                return;
            }
            long shortfallId, itemId;
            if (!Int64.TryParse(Convert.ToString(ContractorShortfallGrid.DataKeys[rowIndex].Values["ShortfallId"]),
                    NumberStyles.None, CultureInfo.InvariantCulture, out shortfallId) ||
                !Int64.TryParse(Convert.ToString(ContractorShortfallGrid.DataKeys[rowIndex].Values["ShortfallItemId"]),
                    NumberStyles.None, CultureInfo.InvariantCulture, out itemId))
            {
                ShowMessage("The shortfall assignment could not be read.");
                return;
            }
            FileUpload file = ContractorShortfallGrid.Rows[rowIndex]
                .FindControl("ShortfallItemFileUpload") as FileUpload;
            TextBox remarks = ContractorShortfallGrid.Rows[rowIndex]
                .FindControl("ShortfallSubmissionRemarksText") as TextBox;
            try
            {
                TenderFinalizedWorkflowService.SubmitShortfallItem(this, shortfallId, itemId,
                    CurrentUserId, file == null ? null : file.PostedFile,
                    remarks == null ? String.Empty : remarks.Text, Request.UserHostAddress);
                ShowMessage("The corrected document was submitted.");
                LoadApplication();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Shortfall document could not be submitted.", ex);
                ShowMessage("The corrected document could not be saved. Please retry or contact support.");
            }
            catch (IOException ex)
            {
                Trace.Warn("DepartmentModule", "Shortfall document storage failed.", ex);
                ShowMessage("The corrected document upload could not be completed. Please retry or contact support.");
            }
        }

        protected void QualifyButton_Click(object sender, EventArgs e)
        {
            DecideApplication("Qualified");
        }

        protected void DisqualifyButton_Click(object sender, EventArgs e)
        {
            DecideApplication("Disqualified");
        }

        private void DecideApplication(string decision)
        {
            try
            {
                ContractorApplicationService.ReviewApplication(ApplicationId, CurrentUserId, CurrentRoleId,
                    CurrentRoleCode, CurrentDepartmentId, decision, DecisionRemarksText.Text, Request.UserHostAddress);
                ShowMessage("Application decision recorded.");
                LoadApplication();
            }
            catch (InvalidOperationException ex) { ShowMessage(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application decision could not be saved.", ex);
                ShowMessage("The application decision could not be saved. Please retry or contact support.");
            }
        }

        private void ShowMessage(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }

        private static string Encode(object value)
        {
            return value == null || value == DBNull.Value
                ? "—"
                : HttpUtility.HtmlEncode(Convert.ToString(value, CultureInfo.CurrentCulture));
        }

        private void NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            Response.Redirect(ResolveUrl("~/Pages/Error.aspx?code=404"), false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
