using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class TenderForm : ModulePage
    {
        protected Literal Message;
        protected Literal ReviewPublicationDateText, ReviewSubmissionStartDateText,
            ReviewSubmissionClosingDateText, ReviewTechnicalOpeningDateText,
            ReviewFinancialOpeningDateText, ReviewPreBidMeetingDateText, ReviewPublicationWindowText;
        protected Literal FormTitle, FormHeading, FormDescription;
        protected Literal DepartmentReadOnlyText;
        protected TextBox TitleText;
        protected TextBox DescriptionText;
        protected TextBox BudgetInformationText, TechnicalSanctionNumberText, TechnicalSanctionAuthorityText,
            TechnicalSanctionDateText, PreBidMeetingText, PreBidLocationText, PreBidDetailsText,
            TechnicalBidOpeningText, PublicationDateText, FinancialBidOpeningText,
            AdditionalConditionsText, TermsAndConditionsText, LatitudeText, LongitudeText,
            LocationNameText, AdministrativeApprovalReferenceText, AdministrativeApprovalDateText,
            TechnicalSanctionApprovalReferenceText, TechnicalSanctionApprovalDateText,
            GeneralBodyApprovalReferenceText, GeneralBodyApprovalDateText,
            DpdcApprovalReferenceText, DpdcApprovalDateText;
        protected DropDownList DepartmentSelect;
        protected DropDownList TenderTypeSelect;
        protected DropDownList TenderCategorySelect, FinancialYearSelect;
        protected TextBox AmountText;
        protected TextBox StartText, DeadlineText;
        protected FileUpload GisImageUpload, AdministrativeApprovalDocumentUpload,
            TechnicalSanctionApprovalDocumentUpload, GeneralBodyApprovalDocumentUpload, DpdcApprovalDocumentUpload,
            ScopeOfWorkDocumentUpload, TechnicalRequirementsDocumentUpload, FinancialRequirementsDocumentUpload;
        protected FileUpload RequirementDocumentsUpload;
        protected Button CreateButton, SubmitTenderButton;
        protected Panel DepartmentPanel, FinancialYearMissingPanel,
            ApprovalsWizardPanel, TechnicalDocumentVerificationPanel,
            ExistingGisImageActions, DepartmentReadOnlyPanel, UploadedRequirementDocumentsPanel;
        protected RequiredFieldValidator DepartmentRequired, FinancialYearRequired;
        protected CheckBox AdministrativeApprovalCheck, TechnicalSanctionApprovalCheck,
            GeneralBodyApprovalCheck, DpdcApprovalCheck;
        protected HyperLink AdministrativeApprovalDocumentLink, TechnicalSanctionApprovalDocumentLink,
            GeneralBodyApprovalDocumentLink, DpdcApprovalDocumentLink, CurrentGisImageLink,
            ScopeOfWorkViewLink, ScopeOfWorkDownloadLink,
            TechnicalRequirementsViewLink, TechnicalRequirementsDownloadLink,
            FinancialRequirementsViewLink, FinancialRequirementsDownloadLink;
        protected Panel ScopeOfWorkCurrentDocumentPanel, TechnicalRequirementsCurrentDocumentPanel,
            FinancialRequirementsCurrentDocumentPanel;
        protected GridView RequirementDocumentsGrid;
        protected GridView TechnicalDocumentVerificationGrid;
        protected HiddenField RequirementDocumentCategories;
        protected Label ScopeOfWorkCurrentFileName, TechnicalRequirementsCurrentFileName,
            FinancialRequirementsCurrentFileName;
        protected HiddenField ScopeOfWorkDocumentRemoveFlag, TechnicalRequirementsDocumentRemoveFlag,
            FinancialRequirementsDocumentRemoveFlag;
        protected Image CurrentGisImagePreview;
        protected HiddenField WizardCurrentStep, WizardCompletedStepsMask, RemoveGisImageFlag,
            JeAdministratorFlag, GisBoundaryCoordinates, GisBoundaryLineCoordinates, GisBoundaryGeometryType,
            DocumentVerificationOnlyFlag;
        protected Panel ScheduleValidationPanel;
        protected Panel RequirementDocumentsEmptyState;
        protected Literal ScheduleValidationMessage;

        private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
        private static readonly string[] ApprovalTypes =
            { "Administrative", "TechnicalSanction", "GeneralBody", "DPDC" };
        private int EditingTenderId { get { return QueryId("id"); } }
        private bool VerificationOnly { get; set; }
        private string TenderDocumentReviewStage
        {
            get
            {
                if (IsJeAdministrator) return "JE";
                return TenderFinalizedWorkflowService.IsTenderCell(CurrentRoleCode)
                    ? "TENDER_CELL" : String.Empty;
            }
        }
        private bool CanVerifyTenderDocuments
        {
            get
            {
                return HasPermission("system.admin") || HasPermission("tender.primary.verify") ||
                    HasPermission("tender.cell.verify");
            }
        }
        protected bool CanManageRequirementDocuments
        {
            get
            {
                return !VerificationOnly &&
                    (HasPermission("system.admin") || HasPermission("tender.create.department") ||
                     HasPermission("tenders.manage.all"));
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            bool isEditing = EditingTenderId > 0;
            bool isJe = IsJeAdministrator;
            bool canManageTender = HasPermission("system.admin") ||
                HasPermission("tender.create.department") || HasPermission("tenders.manage.all");
            bool canVerifyDocuments = CanVerifyTenderDocuments;
            if ((!isEditing && !canManageTender) ||
                (isEditing && !canManageTender && !canVerifyDocuments))
            {
                RequireAnyPermission("system.admin", "tender.create.department", "tenders.manage.all");
                return;
            }
            VerificationOnly = isEditing && !canManageTender && canVerifyDocuments;
            DocumentVerificationOnlyFlag.Value = VerificationOnly ? "true" : "false";

            JeAdministratorFlag.Value = isJe ? "true" : "false";

            FormTitle.Text = isEditing ? "Edit Tender" : "New Tender";
            FormHeading.Text = isEditing ? "Edit Tender Requirement" : "New Tender Requirement";
            if (VerificationOnly)
            {
                FormTitle.Text = "Verify Tender Documents";
                FormHeading.Text = "Tender Document Verification";
            }
            FormDescription.Text = isEditing
                ? "Only eligible drafts and returned budget proposals can be edited. Later workflow stages remain protected."
                : "Complete the tender details and save a draft for department processing.";
            CreateButton.Text = isEditing ? "Save Changes" : "Save as Draft";
            DepartmentPanel.Visible = !isEditing &&
                (isJe || HasPermission("system.admin") ||
                 (!CurrentDepartmentId.HasValue && HasPermission("tenders.manage.all")));
            DepartmentReadOnlyPanel.Visible = isEditing ||
                (!DepartmentPanel.Visible && CurrentDepartmentId.HasValue);
            DepartmentRequired.Enabled = DepartmentPanel.Visible;
            ApprovalsWizardPanel.Visible = isJe && !VerificationOnly;
            TechnicalDocumentVerificationPanel.Visible = isEditing && canVerifyDocuments;
            CreateButton.Visible = !VerificationOnly;
            SubmitTenderButton.Visible = isJe && !VerificationOnly;

            if (!IsPostBack)
            {
                try
                {
                    using (SqlConnection connection = ModuleDb.OpenConnection())
                    {
                        if (!isEditing && DepartmentPanel.Visible)
                        {
                            using (SqlCommand command = new SqlCommand("SELECT DepartmentId, DepartmentName FROM dbo.Departments WHERE IsActive=1 ORDER BY DepartmentName;", connection))
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                DepartmentSelect.Items.Clear();
                                DepartmentSelect.Items.Add(new ListItem("Select department", ""));
                                while (reader.Read())
                                {
                                    ListItem item = new ListItem(reader.GetString(1), reader.GetInt32(0).ToString(CultureInfo.InvariantCulture));
                                    DepartmentSelect.Items.Add(item);
                                    if (CurrentDepartmentId.HasValue && CurrentDepartmentId.Value == reader.GetInt32(0)) item.Selected = true;
                                }
                            }
                        }
                        else if (!isEditing && CurrentDepartmentId.HasValue)
                        {
                            using (SqlCommand command = new SqlCommand(
                                "SELECT DepartmentName FROM dbo.Departments WHERE DepartmentId=@DepartmentId AND IsActive=1;",
                                connection))
                            {
                                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, CurrentDepartmentId.Value);
                                object departmentName = command.ExecuteScalar();
                                if (departmentName != null && departmentName != DBNull.Value)
                                    DepartmentReadOnlyText.Text = HttpUtility.HtmlEncode(Convert.ToString(departmentName));
                            }
                        }

                        BindTenderCatalog(connection, TenderTypeSelect, "SELECT MethodName FROM dbo.TenderMethods WHERE IsActive=1 ORDER BY SortOrder,MethodName;", "Select tender method");
                        BindTenderCatalog(connection, TenderCategorySelect, "SELECT TypeName FROM dbo.TenderTypes WHERE IsActive=1 ORDER BY SortOrder,TypeName;", "Select tender category");
                        BindTenderCatalog(connection, FinancialYearSelect, "SELECT FinancialYear FROM dbo.FinancialYears WHERE IsActive=1 ORDER BY StartDate DESC;", "Select Budgeting Financial Year");
                    }

                    if (isEditing) LoadTenderForEdit();
                    else LoadRequirementDocuments();
                }
                catch (SqlException ex)
                {
                    Trace.Warn("DepartmentModule", "Tender form data could not be loaded.", ex);
                    ShowError("Tender form data could not be loaded. Please retry or contact support.");
                    CreateButton.Enabled = false;
                }
            }
            else
            {
                LoadRequirementDocuments();
            }
            FinancialYearMissingPanel.Visible = FinancialYearSelect.Items.Count <= 1;
            FinancialYearRequired.Enabled = !FinancialYearMissingPanel.Visible;
            if (FinancialYearMissingPanel.Visible) CreateButton.Enabled = false;
            if (isEditing && !IsJeAdministrator) SubmitTenderButton.Visible = false;
        }

        private static int BindTenderCatalog(SqlConnection connection, DropDownList control, string query, string placeholder)
        {
            control.Items.Clear();
            control.Items.Add(new ListItem(placeholder, ""));
            int count = 0;
            using (SqlCommand command = new SqlCommand(query, connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string value = Convert.ToString(reader[0]);
                    control.Items.Add(new ListItem(value, value));
                    count++;
                }
            }
            return count;
        }

        private void LoadTenderForEdit()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.Title,t.Description,t.EstimatedAmount,t.TenderType,t.TenderMethod,t.TenderCategory,
                         t.SubmissionStartDate,t.SubmissionDeadline,t.CurrentStatus,t.FinancialYear,
                         t.PublicationScheduledUtc,t.TechnicalBidOpeningScheduledUtc,t.FinancialBidOpeningScheduledUtc,
                         t.WizardCurrentStep,t.WizardCompletedStepsMask,d.DepartmentName,
                         t.BudgetInformation,t.TechnicalSanctionNumber,t.TechnicalSanctionAuthority,t.TechnicalSanctionDate,
                         t.PreBidMeetingUtc,t.PreBidLocation,t.PreBidDetails,
                         schedule.PublicationStartUtc,schedule.CurrentClosingUtc,
                         r.AdditionalConditions,r.TermsAndConditions
                  FROM dbo.Tender t
                  INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                  LEFT JOIN dbo.TenderRequirements r ON r.TenderId=t.TenderId
                  LEFT JOIN dbo.TenderPublicationSchedule schedule ON schedule.TenderId=t.TenderId
                  WHERE t.TenderId=@TenderId AND t.IsDeleted=0
                    AND
                    (
                        (t.WorkflowVersion=1 AND
                         (@IsAdministrator=1 OR
                          (@IsDepartmentOfficer=1 AND t.CreatedBy=@UserId
                           AND t.CurrentStatus=N'Budget Revision Required'
                           AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                           AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                           WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId IS NOT NULL)
                                OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                          WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId=t.DepartmentId)))))
                        OR
                        (t.WorkflowVersion=2 AND @IsAdministrator=1 AND t.CurrentStatus=N'Draft'
                         AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId
                         AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                    INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                    WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                      AND s.StageName=N'Final_JE_SubmitTender'))
                        OR
                        (@IsVerificationOfficer=1 AND
                         (
                            (t.WorkflowVersion=2 AND @ReviewStage=N'TENDER_CELL'
                             AND t.CurrentStatus=N'Pending Tender Cell Verification'
                             AND EXISTS
                             (
                                 SELECT 1
                                 FROM dbo.TenderWorkflowAssignments a
                                 INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                 WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                   AND a.AssignedRoleId=@RoleId
                                   AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                                   AND a.DepartmentId=t.DepartmentId
                                   AND s.StageName=N'Final_Cell_InitialTenderDocumentVerification'
                             ))
                            OR
                            (t.WorkflowVersion=1 AND @DepartmentId IS NOT NULL
                             AND t.DepartmentId=@DepartmentId AND t.CurrentStatus=N'Draft')
                         ))
                    );", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit,
                    IsJeAdministrator);
                ModuleDb.AddParameter(command, "@IsVerificationOfficer", SqlDbType.Bit,
                    CanVerifyTenderDocuments);
                ModuleDb.AddParameter(command, "@ReviewStage", SqlDbType.NVarChar,
                    TenderDocumentReviewStage, 20);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit,
                    !HasPermission("system.admin") &&
                    (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase)));
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        Response.Redirect("~/Pages/Tenders.aspx", false);
                        Context.ApplicationInstance.CompleteRequest();
                        return;
                    }
                    string status = Convert.ToString(reader["CurrentStatus"]);
                    if (!VerificationOnly &&
                        !String.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals(status, "Budget Revision Required", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowError("This tender is not an editable draft or returned budget proposal.");
                        CreateButton.Enabled = false;
                        return;
                    }
                    FormDescription.Text = VerificationOnly
                        ? "Review each tender document and record its verification decision. Tender details and uploaded files are read-only."
                        : (String.Equals(status, "Budget Revision Required", StringComparison.OrdinalIgnoreCase)
                            ? "Revise the returned estimate and proposal. Submit the updated proposal from tender details when ready."
                            : "This draft is editable before it advances beyond its initial workflow stage.");

                    TitleText.Text = Convert.ToString(reader["Title"]);
                    DescriptionText.Text = Convert.ToString(reader["Description"] == DBNull.Value ? String.Empty : reader["Description"]);
                    AmountText.Text = Convert.ToDecimal(reader["EstimatedAmount"]).ToString(CultureInfo.InvariantCulture);
                    string tenderType = Convert.ToString(reader["TenderMethod"] == DBNull.Value ? reader["TenderType"] : reader["TenderMethod"]);
                    if (TenderTypeSelect.Items.FindByValue(tenderType) == null)
                        TenderTypeSelect.Items.Add(new ListItem(tenderType, tenderType));
                    TenderTypeSelect.SelectedValue = tenderType;
                    string tenderCategory = Convert.ToString(reader["TenderCategory"] == DBNull.Value ? String.Empty : reader["TenderCategory"]);
                    if (tenderCategory.Length > 0 && TenderCategorySelect.Items.FindByValue(tenderCategory) == null)
                        TenderCategorySelect.Items.Add(new ListItem(tenderCategory, tenderCategory));
                    if (tenderCategory.Length > 0) TenderCategorySelect.SelectedValue = tenderCategory;
                    if (reader["FinancialYear"] != DBNull.Value)
                    {
                        string financialYear = Convert.ToString(reader["FinancialYear"], CultureInfo.InvariantCulture);
                        if (FinancialYearSelect.Items.FindByValue(financialYear) == null)
                            FinancialYearSelect.Items.Add(new ListItem(financialYear, financialYear));
                        FinancialYearSelect.SelectedValue = financialYear;
                    }
                    WizardCurrentStep.Value = NormalizeWizardCurrentStep(reader["WizardCurrentStep"])
                        .ToString(CultureInfo.InvariantCulture);
                    WizardCompletedStepsMask.Value = NormalizeWizardCompletedStepsMask(reader["WizardCompletedStepsMask"])
                        .ToString(CultureInfo.InvariantCulture);
                    PublicationDateText.Text = FormatIndiaDate(reader["PublicationScheduledUtc"]);
                    StartText.Text = FormatIndiaDate(reader["SubmissionStartDate"]);
                    DeadlineText.Text = FormatIndiaDate(reader["SubmissionDeadline"]);
                    TechnicalBidOpeningText.Text = FormatIndiaDate(reader["TechnicalBidOpeningScheduledUtc"]);
                    FinancialBidOpeningText.Text = FormatIndiaDate(reader["FinancialBidOpeningScheduledUtc"]);
                    BudgetInformationText.Text = Convert.ToString(reader["BudgetInformation"] == DBNull.Value ? String.Empty : reader["BudgetInformation"]);
                    TechnicalSanctionNumberText.Text = Convert.ToString(reader["TechnicalSanctionNumber"] == DBNull.Value ? String.Empty : reader["TechnicalSanctionNumber"]);
                    TechnicalSanctionAuthorityText.Text = Convert.ToString(reader["TechnicalSanctionAuthority"] == DBNull.Value ? String.Empty : reader["TechnicalSanctionAuthority"]);
                    TechnicalSanctionDateText.Text = reader["TechnicalSanctionDate"] == DBNull.Value
                        ? String.Empty
                        : Convert.ToDateTime(reader["TechnicalSanctionDate"], CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    PreBidMeetingText.Text = FormatIndiaDate(reader["PreBidMeetingUtc"]);
                    ReviewPublicationDateText.Text = FormatIndiaDate(reader["PublicationScheduledUtc"]);
                    ReviewSubmissionStartDateText.Text = FormatIndiaDate(reader["SubmissionStartDate"]);
                    ReviewSubmissionClosingDateText.Text = FormatIndiaDate(reader["SubmissionDeadline"]);
                    ReviewTechnicalOpeningDateText.Text = FormatIndiaDate(reader["TechnicalBidOpeningScheduledUtc"]);
                    ReviewFinancialOpeningDateText.Text = FormatIndiaDate(reader["FinancialBidOpeningScheduledUtc"]);
                    ReviewPreBidMeetingDateText.Text = FormatIndiaDate(reader["PreBidMeetingUtc"]);
                    string publicationStart = FormatIndiaDateTime(reader["PublicationStartUtc"]);
                    string publicationClose = FormatIndiaDateTime(reader["CurrentClosingUtc"]);
                    ReviewPublicationWindowText.Text = String.IsNullOrEmpty(publicationStart)
                        ? String.Empty
                        : publicationStart + " to " + publicationClose + " IST";
                    PreBidLocationText.Text = Convert.ToString(reader["PreBidLocation"] == DBNull.Value ? String.Empty : reader["PreBidLocation"]);
                    PreBidDetailsText.Text = Convert.ToString(reader["PreBidDetails"] == DBNull.Value ? String.Empty : reader["PreBidDetails"]);
                    AdditionalConditionsText.Text = Convert.ToString(reader["AdditionalConditions"] == DBNull.Value ? String.Empty : reader["AdditionalConditions"]);
                    TermsAndConditionsText.Text = Convert.ToString(reader["TermsAndConditions"] == DBNull.Value ? String.Empty : reader["TermsAndConditions"]);
                    FormDescription.Text += " Department: " + HttpUtility.HtmlEncode(Convert.ToString(reader["DepartmentName"]));
                    DepartmentReadOnlyText.Text = HttpUtility.HtmlEncode(Convert.ToString(reader["DepartmentName"]));
                }
                LoadRequirementDocuments(connection);
                if (TechnicalDocumentVerificationPanel.Visible)
                    BindTechnicalDocumentVerificationGrid(connection);
                LoadGisAndAdministrativeApprovals(connection);
            }
        }

        private void LoadRequirementDocuments()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
                LoadRequirementDocuments(connection);
        }

        private void LoadRequirementDocuments(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.DocumentId,d.DocumentName,d.FileSizeBytes,d.UploadedDate,
                         COALESCE(dt.TypeName,d.DocumentType) AS RequirementCategory,
                         COALESCE(NULLIF(d.FileExtension,N''),RIGHT(d.FileName,CHARINDEX(N'.',REVERSE(d.FileName)))) AS FileExtension
                  FROM dbo.TenderDocuments d
                  LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                  WHERE d.TenderId=@TenderId AND d.IsDeleted=0
                    AND COALESCE(dt.TypeName,d.DocumentType) IN
                        (N'Scope of Work',N'Technical Requirements',N'Financial Requirements')
                  ORDER BY d.UploadedDate DESC,d.DocumentId DESC;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                DataTable documents = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    adapter.Fill(documents);
                UploadedRequirementDocumentsPanel.Visible = documents.Rows.Count > 0;
                RequirementDocumentsEmptyState.Visible = documents.Rows.Count == 0;
                documents.Columns.Add("FileTypeDisplay", typeof(string));
                documents.Columns.Add("FileSizeDisplay", typeof(string));
                documents.Columns.Add("UploadStatus", typeof(string));
                documents.Columns.Add("UploadDateDisplay", typeof(string));
                foreach (DataRow document in documents.Rows)
                {
                    string extension = Convert.ToString(document["FileExtension"]);
                    long sizeBytes = document["FileSizeBytes"] == DBNull.Value
                        ? 0 : Convert.ToInt64(document["FileSizeBytes"], CultureInfo.InvariantCulture);
                    DateTime uploadedAt = Convert.ToDateTime(document["UploadedDate"], CultureInfo.InvariantCulture);
                    document["FileTypeDisplay"] = FormatRequirementFileType(extension);
                    document["FileSizeDisplay"] = FormatRequirementFileSize(sizeBytes);
                    document["UploadStatus"] = "Uploaded";
                    document["UploadDateDisplay"] = uploadedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
                }
                RequirementDocumentsGrid.DataSource = documents;
                RequirementDocumentsGrid.DataBind();
            }
        }

        private static string FormatRequirementFileType(string extension)
        {
            switch ((extension ?? String.Empty).ToLowerInvariant())
            {
                case ".pdf": return "PDF";
                case ".doc":
                case ".docx": return "Word";
                case ".xls":
                case ".xlsx": return "Excel";
                case ".jpg":
                case ".jpeg": return "JPEG";
                case ".png": return "PNG";
                default: return (extension ?? String.Empty).TrimStart('.').ToUpperInvariant();
            }
        }

        private static string FormatRequirementFileSize(long sizeBytes)
        {
            if (sizeBytes < 1024L * 1024L)
                return Math.Max(1L, (long)Math.Ceiling(sizeBytes / 1024d)).ToString(CultureInfo.InvariantCulture) + " KB";
            return (sizeBytes / (1024d * 1024d)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        private void BindTechnicalDocumentVerificationGrid(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.DocumentId,d.TenderId,d.DocumentName,d.FileName,
                         COALESCE(dt.TypeName,d.DocumentType) AS RequirementCategory,
                         COALESCE(latest.VerificationStatus,
                             CASE WHEN @ReviewStage=N'JE' THEN d.VerificationStatus END,N'') AS VerificationStatus,
                         COALESCE(latest.VerificationStatus,
                             CASE WHEN @ReviewStage=N'JE' THEN d.VerificationStatus END,N'Pending') AS VerificationStatusText,
                         COALESCE(latest.Remarks,
                             CASE WHEN @ReviewStage=N'JE' THEN d.VerificationRemarks END) AS VerificationRemarks,
                         COALESCE(latest.ReviewedAtUtc,
                             CASE WHEN @ReviewStage=N'JE' THEN d.VerifiedAtUtc END) AS VerifiedAtUtc,
                         COALESCE(reviewer.FullName,
                             CASE WHEN @ReviewStage=N'JE' THEN legacyReviewer.FullName END) AS VerifiedByName
                  FROM dbo.TenderDocuments d
                  LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                  LEFT JOIN dbo.Users legacyReviewer ON legacyReviewer.UserId=d.VerifiedBy
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus,h.Remarks,h.ReviewedAtUtc,h.ReviewerId
                      FROM dbo.TenderRequirementVerificationHistory h
                      WHERE h.TenderId=d.TenderId AND h.DocumentId=d.DocumentId
                        AND h.ReviewStage=@ReviewStage
                        AND h.DocumentRevision=d.DocumentRevision
                      ORDER BY h.ReviewedAtUtc DESC,h.VerificationHistoryId DESC
                  ) latest
                  LEFT JOIN dbo.Users reviewer ON reviewer.UserId=latest.ReviewerId
                  WHERE d.TenderId=@TenderId AND d.IsDeleted=0
                  ORDER BY d.UploadedDate,d.DocumentId;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                ModuleDb.AddParameter(command, "@ReviewStage", SqlDbType.NVarChar,
                    TenderDocumentReviewStage, 20);
                DataTable documents = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    adapter.Fill(documents);
                TechnicalDocumentVerificationGrid.DataSource = documents;
                TechnicalDocumentVerificationGrid.DataBind();
            }
        }

        private RequirementDocumentInput[] GetRequirementDocumentInputs()
        {
            return new[]
            {
                new RequirementDocumentInput
                {
                    TypeName="Scope of Work", FolderName="ScopeOfWork",
                    Document=ScopeOfWorkDocumentUpload, CurrentPanel=ScopeOfWorkCurrentDocumentPanel,
                    CurrentFileName=ScopeOfWorkCurrentFileName, ViewLink=ScopeOfWorkViewLink,
                    DownloadLink=ScopeOfWorkDownloadLink, RemoveFlag=ScopeOfWorkDocumentRemoveFlag
                },
                new RequirementDocumentInput
                {
                    TypeName="Technical Requirements", FolderName="TechnicalRequirements",
                    Document=TechnicalRequirementsDocumentUpload, CurrentPanel=TechnicalRequirementsCurrentDocumentPanel,
                    CurrentFileName=TechnicalRequirementsCurrentFileName, ViewLink=TechnicalRequirementsViewLink,
                    DownloadLink=TechnicalRequirementsDownloadLink, RemoveFlag=TechnicalRequirementsDocumentRemoveFlag
                },
                new RequirementDocumentInput
                {
                    TypeName="Financial Requirements", FolderName="FinancialRequirements",
                    Document=FinancialRequirementsDocumentUpload, CurrentPanel=FinancialRequirementsCurrentDocumentPanel,
                    CurrentFileName=FinancialRequirementsCurrentFileName, ViewLink=FinancialRequirementsViewLink,
                    DownloadLink=FinancialRequirementsDownloadLink, RemoveFlag=FinancialRequirementsDocumentRemoveFlag
                }
            };
        }

        private void LoadGisAndAdministrativeApprovals(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT Latitude,Longitude,LocationName,ImagePath,ImageOriginalFileName,
                         BoundaryCoordinatesJson,BoundaryLineGeometryJson,BoundaryGeometryType
                  FROM dbo.TenderGisLocations WHERE TenderId=@TenderId;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        LatitudeText.Text = FormatCoordinate(reader["Latitude"]);
                        LongitudeText.Text = FormatCoordinate(reader["Longitude"]);
                        GisBoundaryCoordinates.Value = reader["BoundaryCoordinatesJson"] == DBNull.Value
                            ? String.Empty : Convert.ToString(reader["BoundaryCoordinatesJson"]);
                        GisBoundaryLineCoordinates.Value = reader["BoundaryLineGeometryJson"] == DBNull.Value
                            ? String.Empty : Convert.ToString(reader["BoundaryLineGeometryJson"]);
                        GisBoundaryGeometryType.Value = reader["BoundaryGeometryType"] == DBNull.Value
                            ? String.Empty : Convert.ToString(reader["BoundaryGeometryType"]);
                        LocationNameText.Text = reader["LocationName"] == DBNull.Value
                            ? String.Empty : Convert.ToString(reader["LocationName"]);
                        if (reader["ImagePath"] != DBNull.Value)
                        {
                            string imageUrl = ResolveUrl(
                                "~/Pages/DocumentDownload.aspx?gisId=" +
                                EditingTenderId.ToString(CultureInfo.InvariantCulture) + "&preview=true");
                            CurrentGisImageLink.NavigateUrl = imageUrl;
                            CurrentGisImageLink.Text = reader["ImageOriginalFileName"] == DBNull.Value
                                ? "View uploaded GIS image"
                                : HttpUtility.HtmlEncode(Path.GetFileName(
                                    Convert.ToString(reader["ImageOriginalFileName"])));
                            CurrentGisImageLink.Visible = true;
                            ExistingGisImageActions.Visible = true;
                            CurrentGisImagePreview.ImageUrl = imageUrl;
                            CurrentGisImagePreview.Visible = true;
                        }
                    }
                }
            }

            if (!IsJeAdministrator) return;
            using (SqlCommand command = new SqlCommand(
                @"SELECT ApprovalType,IsApproved,ApprovalReferenceNumber,ApprovalDate,
                         DocumentPath,DocumentOriginalFileName
                  FROM dbo.TenderAdministrativeApprovals WHERE TenderId=@TenderId;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ApprovalInput input = GetApprovalInput(Convert.ToString(reader["ApprovalType"]));
                        if (input == null) continue;
                        input.Check.Checked = Convert.ToBoolean(reader["IsApproved"], CultureInfo.InvariantCulture);
                        input.ReferenceText.Text = reader["ApprovalReferenceNumber"] == DBNull.Value
                            ? String.Empty : Convert.ToString(reader["ApprovalReferenceNumber"]);
                        input.DateText.Text = reader["ApprovalDate"] == DBNull.Value
                            ? String.Empty
                            : Convert.ToDateTime(reader["ApprovalDate"], CultureInfo.InvariantCulture)
                                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        if (reader["DocumentPath"] != DBNull.Value)
                        {
                            input.DocumentLink.NavigateUrl = ResolveUrl(
                                "~/Pages/DocumentDownload.aspx?approvalId=" +
                                EditingTenderId.ToString(CultureInfo.InvariantCulture) + "&approvalType=" +
                                HttpUtility.UrlEncode(input.TypeName) + "&preview=true");
                            input.DocumentLink.Text = reader["DocumentOriginalFileName"] == DBNull.Value
                                ? "View current approval document"
                                : HttpUtility.HtmlEncode(Path.GetFileName(
                                    Convert.ToString(reader["DocumentOriginalFileName"])));
                            input.DocumentLink.Visible = true;
                        }
                    }
                }
            }
        }

        private static string FormatCoordinate(object value)
        {
            return value == null || value == DBNull.Value
                ? String.Empty
                : Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                    .ToString("0.######", CultureInfo.InvariantCulture);
        }

        private bool ValidateApprovalInputs()
        {
            if (!IsJeAdministrator) return true;
            foreach (ApprovalInput input in GetApprovalInputs())
            {
                DateTime approvalDate;
                if (input.ReferenceText.Text.Length > 100 ||
                    (!String.IsNullOrWhiteSpace(input.DateText.Text) &&
                     !TryParseDate(input.DateText.Text, out approvalDate)))
                    return false;
            }
            return true;
        }

        private bool PrepareGisAndApprovalUploads(out string error)
        {
            error = null;
            if (GisImageUpload.HasFile &&
                !IsValidTenderAssetUpload(GisImageUpload, new[] { ".jpg", ".jpeg", ".png" }, 10 * 1024 * 1024))
            {
                error = "Choose a valid JPEG or PNG GIS image no larger than 10 MB.";
                return false;
            }
            List<UploadedRequirementDocument> requirementDocuments;
            if (!TryGetRequirementUploads(out requirementDocuments, out error))
                return false;
            foreach (UploadedRequirementDocument document in requirementDocuments)
            {
                if (!TryValidateRequirementDocumentUpload(document.File, out error))
                    return false;
            }
            if (!IsJeAdministrator) return true;

            foreach (ApprovalInput input in GetApprovalInputs())
            {
                if (input.Document.HasFile &&
                    !IsValidTenderAssetUpload(input.Document, new[] { ".pdf", ".jpg", ".jpeg", ".png" },
                        10 * 1024 * 1024))
                {
                    error = "Approval document " + SafeTenderDocumentFileName(input.Document.FileName) +
                        " must be a valid PDF, JPEG, or PNG no larger than 10 MB.";
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidateRequirementDocumentUpload(HttpPostedFile file, out string error)
        {
            error = null;
            string originalName = SafeTenderDocumentFileName(file == null ? null : file.FileName);
            if (file == null || String.IsNullOrWhiteSpace(originalName))
            {
                error = "Select a document with a valid file name.";
                return false;
            }
            if (file.ContentLength <= 0)
            {
                error = originalName + " is empty.";
                return false;
            }
            if (file.ContentLength > 10 * 1024 * 1024)
            {
                error = originalName + " exceeds the 10 MB per-document limit.";
                return false;
            }
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            if (originalName.Length > 260 || Array.IndexOf(AllowedExtensions, extension) < 0)
            {
                error = originalName + " has an unsupported file name or extension.";
                return false;
            }
            if (!IsAllowedRequirementMimeType(file.ContentType, extension) ||
                !TenderLifecycleFiles.HasAllowedContentSignature(file.InputStream, extension))
            {
                error = originalName + " has an unsupported MIME type or invalid file contents.";
                return false;
            }
            return true;
        }

        private static bool IsAllowedRequirementMimeType(string contentType, string extension)
        {
            if (String.IsNullOrWhiteSpace(contentType) ||
                String.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return true;

            string normalizedType = contentType.Split(';')[0].Trim();
            string expectedType = GetRequirementMimeType(extension);
            if (String.Equals(normalizedType, expectedType, StringComparison.OrdinalIgnoreCase))
                return true;
            return (extension == ".docx" || extension == ".xlsx") &&
                String.Equals(normalizedType, "application/zip", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRequirementMimeType(string extension)
        {
            switch ((extension ?? String.Empty).ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                default: return "application/octet-stream";
            }
        }

        private static string SafeTenderDocumentFileName(string fileName)
        {
            string normalized = (fileName ?? String.Empty).Replace('\\', '/');
            string safeName = Path.GetFileName(normalized);
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            System.Text.StringBuilder result = new System.Text.StringBuilder(safeName.Length);
            foreach (char character in safeName)
                if (!Char.IsControl(character) && Array.IndexOf(invalidCharacters, character) < 0)
                    result.Append(character);
            return result.ToString().Trim();
        }

        private bool TryGetRequirementUploads(out List<UploadedRequirementDocument> uploads, out string error)
        {
            uploads = new List<UploadedRequirementDocument>();
            error = null;
            IList<HttpPostedFile> postedFiles = RequirementDocumentsUpload.PostedFiles;
            List<HttpPostedFile> selectedFiles = new List<HttpPostedFile>();
            if (postedFiles != null)
            {
                foreach (HttpPostedFile postedFile in postedFiles)
                {
                    if (postedFile != null && !String.IsNullOrWhiteSpace(postedFile.FileName))
                        selectedFiles.Add(postedFile);
                }
            }
            if (selectedFiles.Count == 0) return true;

            string[] categories;
            try
            {
                categories = new JavaScriptSerializer().Deserialize<string[]>(RequirementDocumentCategories.Value);
            }
            catch (ArgumentException)
            {
                error = "Assign a category to each uploaded tender document.";
                return false;
            }
            catch (InvalidOperationException)
            {
                error = "Assign a category to each uploaded tender document.";
                return false;
            }
            if (categories == null || categories.Length != selectedFiles.Count)
            {
                error = "Assign a category to each uploaded tender document.";
                return false;
            }

            for (int index = 0; index < selectedFiles.Count; index++)
            {
                HttpPostedFile file = selectedFiles[index];
                string category = categories[index] == null ? String.Empty : categories[index].Trim();
                if (category != "Scope of Work" && category != "Technical Requirements" &&
                    category != "Financial Requirements")
                {
                    error = "Select Scope of Work, Technical Requirements, or Financial Requirements for every file.";
                    return false;
                }
                uploads.Add(new UploadedRequirementDocument { File = file, Category = category });
            }
            return true;
        }

        private bool HasRequiredRequirementDocuments(out string missingCategory)
        {
            HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<UploadedRequirementDocument> uploads;
            string error;
            if (!TryGetRequirementUploads(out uploads, out error))
            {
                missingCategory = error;
                return false;
            }
            foreach (UploadedRequirementDocument upload in uploads)
                categories.Add(upload.Category);

            if (EditingTenderId > 0)
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT DISTINCT COALESCE(dt.TypeName,d.DocumentType)
                      FROM dbo.TenderDocuments d
                      LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                      WHERE d.TenderId=@TenderId AND d.IsDeleted=0
                        AND COALESCE(dt.TypeName,d.DocumentType) IN
                            (N'Scope of Work',N'Technical Requirements',N'Financial Requirements');", connection))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                    using (SqlDataReader reader = command.ExecuteReader())
                        while (reader.Read()) categories.Add(Convert.ToString(reader[0]));
                }
            }

            string[] required = { "Scope of Work", "Technical Requirements", "Financial Requirements" };
            foreach (string category in required)
                if (!categories.Contains(category))
                {
                    missingCategory = category;
                    return false;
                }
            missingCategory = null;
            return true;
        }

        private bool AppendRequirementDocuments(SqlConnection connection, SqlTransaction transaction,
            int tenderId, List<string> savedPaths)
        {
            List<UploadedRequirementDocument> uploads;
            string error;
            if (!TryGetRequirementUploads(out uploads, out error))
                throw new InvalidOperationException(error);
            foreach (UploadedRequirementDocument upload in uploads)
            {
                string folderName = upload.Category.Replace(" ", String.Empty);
                StoredTenderAsset document = SaveTenderAsset(upload.File, tenderId,
                    "requirements/" + folderName, savedPaths);
                int documentTypeId;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT DocumentTypeId FROM dbo.DocumentTypes WITH (UPDLOCK,HOLDLOCK)
                      WHERE TypeName=@DocumentType AND IsActive=1;", connection, transaction))
                {
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, upload.Category, 80);
                    object value = command.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        throw new InvalidOperationException("The " + upload.Category +
                            " document type is not configured. Apply the tender requirement document migration and retry.");
                    documentTypeId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }

                string documentName = Path.GetFileNameWithoutExtension(document.OriginalName);
                if (String.IsNullOrWhiteSpace(documentName)) documentName = upload.Category;
                if (documentName.Length > 250) documentName = documentName.Substring(0, 250);
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderDocuments
                        (TenderId,DocumentTypeId,DocumentType,DocumentName,Remarks,FileName,FilePath,
                         FileExtension,FileSizeBytes,MimeType,UploadedBy)
                      VALUES(@TenderId,@DocumentTypeId,@DocumentType,@DocumentName,NULL,@FileName,@FilePath,
                             @FileExtension,@FileSizeBytes,@MimeType,@UploadedBy);", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@DocumentTypeId", SqlDbType.Int, documentTypeId);
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, upload.Category, 80);
                    AddText(command, "@DocumentName", SqlDbType.NVarChar, documentName, 250);
                    AddText(command, "@FileName", SqlDbType.NVarChar, document.OriginalName, 260);
                    AddText(command, "@FilePath", SqlDbType.NVarChar, document.RelativePath, 400);
                    AddText(command, "@FileExtension", SqlDbType.NVarChar, document.Extension, 10);
                    ModuleDb.AddParameter(command, "@FileSizeBytes", SqlDbType.BigInt, document.SizeBytes);
                    AddText(command, "@MimeType", SqlDbType.NVarChar, document.MimeType, 100);
                    ModuleDb.AddParameter(command, "@UploadedBy", SqlDbType.Int, CurrentUserId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("The " + upload.Category + " document could not be saved.");
                }
            }
            return uploads.Count > 0;
        }

        protected void RequirementDocumentsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (EditingTenderId <= 0) return;
            if (String.Equals(e.CommandName, "ReplaceRequirementDocument", StringComparison.Ordinal))
            {
                ReplaceRequirementDocument(e);
                return;
            }
            if (String.Equals(e.CommandName, "UpdateRequirementDocumentCategory", StringComparison.Ordinal))
            {
                UpdateRequirementDocumentCategory(e);
                return;
            }
            if (!String.Equals(e.CommandName, "RemoveRequirementDocument", StringComparison.Ordinal)) return;
            if (VerificationOnly)
            {
                ShowError("Document verification users cannot remove tender documents.");
                return;
            }

            int documentId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture),
                NumberStyles.None, CultureInfo.InvariantCulture, out documentId))
            {
                ShowError("The selected document could not be identified.");
                return;
            }

            List<string> obsoletePaths = new List<string>();
            try
            {
                bool isDepartmentOfficer = !HasPermission("system.admin") &&
                    (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase));
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    string filePath = null;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT d.FilePath
                          FROM dbo.TenderDocuments d
                          INNER JOIN dbo.Tender t ON t.TenderId=d.TenderId
                          WHERE d.DocumentId=@DocumentId AND d.TenderId=@TenderId AND d.IsDeleted=0 AND t.IsDeleted=0
                            AND
                            (
                                (t.WorkflowVersion=1 AND
                                 (@IsAdministrator=1 OR
                                  (@IsDepartmentOfficer=1 AND t.CreatedBy=@UserId
                                   AND t.CurrentStatus=N'Budget Revision Required'
                                   AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                                   AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                   WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId IS NOT NULL)
                                        OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                  WHERE scopeUser.UserId=@UserId
                                                    AND scopeUser.DepartmentId=t.DepartmentId)))))
                                OR
                                (t.WorkflowVersion=2 AND @IsAdministrator=1 AND t.CurrentStatus=N'Draft'
                                 AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId
                                 AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                            INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                            WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                              AND s.StageName=N'Final_JE_SubmitTender'))
                            );", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit, IsJeAdministrator);
                        ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit, isDepartmentOfficer);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        object path = command.ExecuteScalar();
                        if (path == null || path == DBNull.Value)
                            throw new InvalidOperationException("This tender document cannot be removed in your current role or workflow stage.");
                        filePath = Convert.ToString(path);
                    }

                    RecordRequirementDocumentFileHistory(connection, transaction, documentId, "Deleted");
                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.TenderDocuments
                          SET IsDeleted=1,DeletedAtUtc=SYSUTCDATETIME(),DeletedBy=@UserId
                          WHERE DocumentId=@DocumentId AND TenderId=@TenderId AND IsDeleted=0;",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The selected document has changed; refresh the page and retry.");
                    }
                    QueueObsoleteAsset(filePath, obsoletePaths);
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId,
                        "DeleteTenderDocument", "TenderDocument", documentId,
                        EditingTenderId.ToString(CultureInfo.InvariantCulture), Request.UserHostAddress);
                    transaction.Commit();
                }
                bool oldFileRemoved = DeleteSavedFiles(obsoletePaths);
                LoadRequirementDocuments();
                if (oldFileRemoved)
                    ShowSuccess("The selected document was deleted.");
                else
                    ShowError("The document record was deleted, but its stored file could not be removed. Contact support to clean up the unused file.");
            }
            catch (InvalidOperationException ex)
            {
                Trace.Warn("DepartmentModule", "Tender requirement document could not be removed.", ex);
                ShowError(ex.Message);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender requirement document could not be removed.", ex);
                ShowError("The tender document could not be removed. Please retry or contact support.");
            }
        }

        private void ReplaceRequirementDocument(GridViewCommandEventArgs e)
        {
            if (VerificationOnly || !CanManageRequirementDocuments)
            {
                ShowError("Your role cannot replace tender documents.");
                return;
            }

            int documentId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture),
                NumberStyles.None, CultureInfo.InvariantCulture, out documentId) || documentId <= 0)
            {
                ShowError("The selected document could not be identified.");
                return;
            }

            Control sourceControl = e.CommandSource as Control;
            GridViewRow row = sourceControl == null ? null : sourceControl.NamingContainer as GridViewRow;
            FileUpload replacementUpload = row == null
                ? null : row.FindControl("RequirementDocumentReplacementUpload") as FileUpload;
            HttpPostedFile replacement = replacementUpload == null ? null : replacementUpload.PostedFile;
            string validationError;
            if (!TryValidateRequirementDocumentUpload(replacement, out validationError))
            {
                ShowError(validationError);
                return;
            }

            bool isDepartmentOfficer = !HasPermission("system.admin") &&
                (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                 String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase));
            List<string> savedPaths = new List<string>();
            List<string> obsoletePaths = new List<string>();
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    string oldPath;
                    string documentType;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT d.FilePath,d.DocumentType
                          FROM dbo.TenderDocuments d WITH (UPDLOCK,HOLDLOCK)
                          INNER JOIN dbo.Tender t WITH (UPDLOCK,HOLDLOCK) ON t.TenderId=d.TenderId
                          WHERE d.DocumentId=@DocumentId AND d.TenderId=@TenderId AND d.IsDeleted=0 AND t.IsDeleted=0
                            AND
                            (
                                (t.WorkflowVersion=1 AND
                                 (@IsAdministrator=1 OR
                                  (@IsDepartmentOfficer=1 AND t.CreatedBy=@UserId
                                   AND t.CurrentStatus=N'Budget Revision Required'
                                   AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                                   AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                   WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId IS NOT NULL)
                                        OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                  WHERE scopeUser.UserId=@UserId
                                                    AND scopeUser.DepartmentId=t.DepartmentId)))))
                                OR
                                (t.WorkflowVersion=2 AND @IsAdministrator=1 AND t.CurrentStatus=N'Draft'
                                 AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId
                                 AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                            INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                            WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                              AND s.StageName=N'Final_JE_SubmitTender'))
                            );", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit, IsJeAdministrator);
                        ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit, isDepartmentOfficer);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("This tender document cannot be replaced in your current role or workflow stage.");
                            oldPath = reader["FilePath"] == DBNull.Value
                                ? null : Convert.ToString(reader["FilePath"]);
                            documentType = Convert.ToString(reader["DocumentType"]);
                        }
                    }

                    string folderName = String.IsNullOrWhiteSpace(documentType)
                        ? "requirements" : "requirements/" + documentType.Replace(" ", String.Empty);
                    StoredTenderAsset newAsset = SaveTenderAsset(replacement, EditingTenderId,
                        folderName, savedPaths);
                    RecordRequirementDocumentFileHistory(connection, transaction, documentId, "Replaced");
                    string documentName = Path.GetFileNameWithoutExtension(newAsset.OriginalName);
                    if (String.IsNullOrWhiteSpace(documentName)) documentName = documentType;
                    if (documentName.Length > 250) documentName = documentName.Substring(0, 250);
                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.TenderDocuments
                          SET DocumentName=@DocumentName,FileName=@FileName,FilePath=@FilePath,
                              FileExtension=@FileExtension,FileSizeBytes=@FileSizeBytes,MimeType=@MimeType,
                              UploadedBy=@UploadedBy,UploadedDate=SYSUTCDATETIME(),
                              VerificationStatus=NULL,VerificationRemarks=NULL,VerifiedBy=NULL,VerifiedAtUtc=NULL,
                              DocumentRevision=DocumentRevision+1
                          WHERE DocumentId=@DocumentId AND TenderId=@TenderId AND IsDeleted=0;", connection, transaction))
                    {
                        AddText(command, "@DocumentName", SqlDbType.NVarChar, documentName, 250);
                        AddText(command, "@FileName", SqlDbType.NVarChar, newAsset.OriginalName, 260);
                        AddText(command, "@FilePath", SqlDbType.NVarChar, newAsset.RelativePath, 400);
                        AddText(command, "@FileExtension", SqlDbType.NVarChar, newAsset.Extension, 10);
                        ModuleDb.AddParameter(command, "@FileSizeBytes", SqlDbType.BigInt, newAsset.SizeBytes);
                        AddText(command, "@MimeType", SqlDbType.NVarChar, newAsset.MimeType, 100);
                        ModuleDb.AddParameter(command, "@UploadedBy", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The selected document changed; refresh the page and retry.");
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId,
                        "ReplaceTenderDocument", "TenderDocument", documentId,
                        EditingTenderId.ToString(CultureInfo.InvariantCulture), Request.UserHostAddress);
                    QueueObsoleteAsset(oldPath, obsoletePaths);
                    transaction.Commit();
                }

                bool oldFileRemoved = DeleteSavedFiles(obsoletePaths);
                LoadRequirementDocuments();
                if (oldFileRemoved)
                    ShowSuccess("The selected document was replaced.");
                else
                    ShowError("The document was replaced, but its previous file could not be removed. Contact support to clean up the unused file.");
            }
            catch (InvalidOperationException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender requirement document replacement was rejected.", ex);
                ShowError(ex.Message);
            }
            catch (SqlException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender requirement document could not be replaced.", ex);
                ShowError("The tender document could not be replaced. Please retry or contact support.");
            }
            catch (IOException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Replacement tender document could not be stored.", ex);
                ShowError("The replacement document could not be stored. No changes were committed.");
            }
            catch (UnauthorizedAccessException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Replacement tender document storage access was denied.", ex);
                ShowError("The replacement document could not be stored because file storage is unavailable.");
            }
        }

        private void RecordRequirementDocumentFileHistory(SqlConnection connection,
            SqlTransaction transaction, int documentId, string actionName)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderDocumentFileHistory
                    (TenderId,DocumentId,DocumentType,DocumentName,FileName,FilePath,
                     FileExtension,FileSizeBytes,MimeType,UploadedBy,UploadedDate,ActionName,ActionBy)
                  SELECT d.TenderId,d.DocumentId,d.DocumentType,d.DocumentName,d.FileName,d.FilePath,
                         d.FileExtension,d.FileSizeBytes,d.MimeType,d.UploadedBy,d.UploadedDate,
                         @ActionName,@ActionBy
                  FROM dbo.TenderDocuments d
                  WHERE d.DocumentId=@DocumentId AND d.TenderId=@TenderId AND d.IsDeleted=0;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                AddText(command, "@ActionName", SqlDbType.NVarChar, actionName, 20);
                ModuleDb.AddParameter(command, "@ActionBy", SqlDbType.Int, CurrentUserId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The document history could not be recorded.");
            }
        }

        private void UpdateRequirementDocumentCategory(GridViewCommandEventArgs e)
        {
            if (VerificationOnly)
            {
                ShowError("Document verification users cannot change tender document categories.");
                return;
            }

            int documentId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out documentId) || documentId <= 0)
            {
                ShowError("The selected document could not be identified.");
                return;
            }

            Control sourceControl = e.CommandSource as Control;
            GridViewRow row = sourceControl == null ? null : sourceControl.NamingContainer as GridViewRow;
            DropDownList categorySelect = row == null
                ? null : row.FindControl("RequirementDocumentCategorySelect") as DropDownList;
            string category = categorySelect == null ? String.Empty : categorySelect.SelectedValue;
            if (categorySelect != null && Request.Form[categorySelect.UniqueID] != null)
                category = Request.Form[categorySelect.UniqueID];
            if (category != "Scope of Work" &&
                category != "Technical Requirements" &&
                category != "Financial Requirements")
            {
                ShowError("Select a valid tender document category.");
                return;
            }

            bool isDepartmentOfficer = !HasPermission("system.admin") &&
                (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                 String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase));
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT d.DocumentId
                          FROM dbo.TenderDocuments d WITH (UPDLOCK,HOLDLOCK)
                          INNER JOIN dbo.Tender t WITH (UPDLOCK,HOLDLOCK) ON t.TenderId=d.TenderId
                          WHERE d.DocumentId=@DocumentId AND d.TenderId=@TenderId AND d.IsDeleted=0 AND t.IsDeleted=0
                            AND
                            (
                                (t.WorkflowVersion=1 AND
                                 (@IsAdministrator=1 OR
                                  (@IsDepartmentOfficer=1 AND t.CreatedBy=@UserId
                                   AND t.CurrentStatus=N'Budget Revision Required'
                                   AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                                   AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                   WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId IS NOT NULL)
                                        OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                  WHERE scopeUser.UserId=@UserId
                                                    AND scopeUser.DepartmentId=t.DepartmentId)))))
                                OR
                                (t.WorkflowVersion=2 AND @IsAdministrator=1 AND t.CurrentStatus=N'Draft'
                                 AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId
                                 AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                            INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                            WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                              AND s.StageName=N'Final_JE_SubmitTender'))
                            );", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit, IsJeAdministrator);
                        ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit, isDepartmentOfficer);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        if (command.ExecuteScalar() == null)
                            throw new InvalidOperationException("This tender document cannot be changed in your current role or workflow stage.");
                    }

                    int documentTypeId;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT DocumentTypeId FROM dbo.DocumentTypes WITH (UPDLOCK,HOLDLOCK)
                          WHERE TypeName=@Category AND IsActive=1;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@Category", SqlDbType.NVarChar, category, 80);
                        object value = command.ExecuteScalar();
                        if (value == null || value == DBNull.Value)
                            throw new InvalidOperationException("The selected document category is not active.");
                        documentTypeId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.TenderDocuments
                          SET DocumentTypeId=@DocumentTypeId,DocumentType=@Category
                          WHERE DocumentId=@DocumentId AND TenderId=@TenderId AND IsDeleted=0;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentTypeId", SqlDbType.Int, documentTypeId);
                        ModuleDb.AddParameter(command, "@Category", SqlDbType.NVarChar, category, 80);
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The selected document has changed; refresh the page and retry.");
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Update",
                        "TenderDocument", documentId, "Requirement category changed to " + category + ".",
                        Request.UserHostAddress);
                    transaction.Commit();
                }

                LoadRequirementDocuments();
            }
            catch (InvalidOperationException ex)
            {
                Trace.Warn("DepartmentModule", "Tender requirement document category could not be updated.", ex);
                ShowError(ex.Message);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender requirement document category could not be updated.", ex);
                ShowError("The tender document category could not be updated. Please retry or contact support.");
            }
        }

        protected void TechnicalDocumentVerificationGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "SaveTechnicalDocumentVerification", StringComparison.Ordinal))
                return;
            string reviewStage = TenderDocumentReviewStage;
            if (!CanVerifyTenderDocuments || String.IsNullOrEmpty(reviewStage) || EditingTenderId <= 0)
            {
                ShowError("Your role is not authorized to verify tender documents.");
                return;
            }

            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out rowIndex) ||
                rowIndex < 0 || rowIndex >= TechnicalDocumentVerificationGrid.Rows.Count)
            {
                ShowError("The selected document could not be identified.");
                return;
            }

            DataKey key = TechnicalDocumentVerificationGrid.DataKeys[rowIndex];
            int documentId = Convert.ToInt32(key.Values["DocumentId"], CultureInfo.InvariantCulture);
            int tenderId = Convert.ToInt32(key.Values["TenderId"], CultureInfo.InvariantCulture);
            if (tenderId != EditingTenderId)
            {
                ShowError("The selected document does not belong to this tender.");
                return;
            }

            GridViewRow row = TechnicalDocumentVerificationGrid.Rows[rowIndex];
            DropDownList decision = row.FindControl("TechnicalDocumentDecision") as DropDownList;
            TextBox remarks = row.FindControl("TechnicalDocumentVerificationRemarks") as TextBox;
            if (decision == null || remarks == null ||
                (decision.SelectedValue != String.Empty &&
                 decision.SelectedValue != "Verified" &&
                 decision.SelectedValue != "Not Verified") ||
                remarks.Text.Length > 1000)
            {
                ShowError("Select a valid verification status and keep remarks within 1,000 characters.");
                return;
            }
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (decision.SelectedValue == "Verified")
                    {
                        using (SqlCommand command = new SqlCommand(
                            @"SELECT DocumentName,FileName,FilePath,FileExtension,FileSizeBytes
                              FROM dbo.TenderDocuments WITH (UPDLOCK,HOLDLOCK)
                              WHERE DocumentId=@DocumentId AND TenderId=@TenderId AND IsDeleted=0;",
                            connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("The selected document no longer exists on this tender.");
                                string fullPath;
                                string fileError = TenderLifecycleFiles.ValidateStoredTenderDocument(this,
                                    Convert.ToString(reader["FilePath"]),
                                    Convert.ToString(reader["FileName"]),
                                    Convert.ToString(reader["FileExtension"]),
                                    reader["FileSizeBytes"] == DBNull.Value
                                        ? (long?)null
                                        : Convert.ToInt64(reader["FileSizeBytes"], CultureInfo.InvariantCulture),
                                    out fullPath);
                                if (!String.IsNullOrEmpty(fileError))
                                    throw new InvalidOperationException("Cannot verify \"" +
                                        Convert.ToString(reader["DocumentName"]) + "\": " + fileError);
                            }
                        }
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE d
                          SET VerificationStatus=@Status,VerificationRemarks=@Remarks,
                              VerifiedBy=CASE WHEN @Status IS NULL THEN NULL ELSE @UserId END,
                              VerifiedAtUtc=CASE WHEN @Status IS NULL THEN NULL ELSE SYSUTCDATETIME() END
                          FROM dbo.TenderDocuments d
                          INNER JOIN dbo.Tender t ON t.TenderId=d.TenderId
                          WHERE d.DocumentId=@DocumentId AND d.TenderId=@TenderId
                            AND d.IsDeleted=0 AND t.IsDeleted=0 AND
                            (
                                (t.WorkflowVersion=2 AND @ReviewStage=N'JE'
                                 AND @IsJE=1 AND t.CurrentStatus=N'Draft'
                                 AND EXISTS
                                 (
                                     SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                     INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                     WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                       AND a.AssignedRoleId=@RoleId
                                       AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                                       AND a.DepartmentId=t.DepartmentId
                                       AND s.StageName=N'Final_JE_SubmitTender'
                                 ))
                                OR
                                (t.WorkflowVersion=2 AND @ReviewStage=N'TENDER_CELL'
                                 AND @IsTenderCell=1
                                 AND t.CurrentStatus=N'Pending Tender Cell Verification'
                                 AND EXISTS
                                 (
                                     SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                     INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                     WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                       AND a.AssignedRoleId=@RoleId
                                       AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                                       AND a.DepartmentId=t.DepartmentId
                                       AND s.StageName=N'Final_Cell_InitialTenderDocumentVerification'
                                 ))
                                OR
                                (t.WorkflowVersion=1 AND t.CurrentStatus=N'Draft'
                                 AND (@IsSystemAdministrator=1 OR
                                      (@CanVerify=1 AND @DepartmentId IS NOT NULL
                                       AND t.DepartmentId=@DepartmentId)))
                            );",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@Status", SqlDbType.NVarChar,
                            String.IsNullOrEmpty(decision.SelectedValue)
                                ? (object)null : decision.SelectedValue, 30);
                        ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                            String.IsNullOrWhiteSpace(remarks.Text)
                                ? (object)null : remarks.Text.Trim(), 1000);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@IsSystemAdministrator", SqlDbType.Bit,
                            HasPermission("system.admin"));
                        ModuleDb.AddParameter(command, "@CanVerify", SqlDbType.Bit,
                            CanVerifyTenderDocuments);
                        ModuleDb.AddParameter(command, "@ReviewStage", SqlDbType.NVarChar, reviewStage, 20);
                        ModuleDb.AddParameter(command, "@IsJE", SqlDbType.Bit, IsJeAdministrator);
                        ModuleDb.AddParameter(command, "@IsTenderCell", SqlDbType.Bit,
                            TenderFinalizedWorkflowService.IsTenderCell(CurrentRoleCode));
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException(
                                "The document is not at your assigned review stage, or it was removed.");
                    }

                    if (decision.SelectedValue.Length > 0)
                    {
                        using (SqlCommand command = new SqlCommand(
                            @"INSERT INTO dbo.TenderRequirementVerificationHistory
                                (TenderId,DocumentId,ReviewStage,VerificationStatus,ReviewerId,
                                 ReviewerRoleId,Remarks,DocumentRevision)
                              SELECT @TenderId,@DocumentId,@ReviewStage,@Status,@ReviewerId,
                                     @RoleId,@Remarks,d.DocumentRevision
                              FROM dbo.TenderDocuments d
                              WHERE d.TenderId=@TenderId AND d.DocumentId=@DocumentId AND d.IsDeleted=0;",
                            connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                            ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                            ModuleDb.AddParameter(command, "@ReviewStage", SqlDbType.NVarChar, reviewStage, 20);
                            ModuleDb.AddParameter(command, "@Status", SqlDbType.NVarChar,
                                decision.SelectedValue, 20);
                            ModuleDb.AddParameter(command, "@ReviewerId", SqlDbType.Int, CurrentUserId);
                            ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                            ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                                String.IsNullOrWhiteSpace(remarks.Text) ? (object)null : remarks.Text.Trim(), 1000);
                            command.ExecuteNonQuery();
                        }
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId,
                        "VerifyTenderDocument", "TenderDocument", documentId,
                        reviewStage + ": " + (decision.SelectedValue.Length == 0 ? "Pending" : decision.SelectedValue),
                        Request.UserHostAddress);
                    transaction.Commit();
                    BindTechnicalDocumentVerificationGrid(connection);
                }
                ShowError("Document verification saved.");
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender document verification could not be saved.", ex);
                ShowError("Document verification could not be saved. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
        }

        private bool SaveRequirementDocuments(SqlConnection connection, SqlTransaction transaction,
            int tenderId, List<string> savedPaths, List<string> obsoletePaths)
        {
            bool documentsChanged = false;
            foreach (RequirementDocumentInput input in GetRequirementDocumentInputs())
            {
                bool removeExisting = String.Equals(input.RemoveFlag.Value, "true",
                    StringComparison.OrdinalIgnoreCase);
                if (!input.Document.HasFile && !removeExisting) continue;
                documentsChanged = true;

                List<string> existingPaths = new List<string>();
                using (SqlCommand command = new SqlCommand(
                    @"SELECT d.FilePath
                      FROM dbo.TenderDocuments d WITH (UPDLOCK,HOLDLOCK)
                      LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                      WHERE d.TenderId=@TenderId AND d.IsDeleted=0
                        AND COALESCE(dt.TypeName,d.DocumentType)=@DocumentType;", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, input.TypeName, 80);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            if (reader["FilePath"] != DBNull.Value)
                                existingPaths.Add(Convert.ToString(reader["FilePath"]));
                    }
                }

                StoredTenderAsset document = input.Document.HasFile
                    ? SaveTenderAsset(input.Document, tenderId,
                        "requirements/" + input.FolderName, savedPaths)
                    : null;
                foreach (string oldPath in existingPaths)
                    QueueObsoleteAsset(oldPath, obsoletePaths);

                if (existingPaths.Count > 0)
                {
                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE d
                          SET IsDeleted=1,DeletedAtUtc=SYSUTCDATETIME(),DeletedBy=@UserId
                          FROM dbo.TenderDocuments d
                          LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                          WHERE d.TenderId=@TenderId AND d.IsDeleted=0
                            AND COALESCE(dt.TypeName,d.DocumentType)=@DocumentType;",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        AddText(command, "@DocumentType", SqlDbType.NVarChar, input.TypeName, 80);
                        command.ExecuteNonQuery();
                    }
                }

                if (document == null) continue;

                int documentTypeId;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT DocumentTypeId
                      FROM dbo.DocumentTypes WITH (UPDLOCK,HOLDLOCK)
                      WHERE TypeName=@DocumentType AND IsActive=1;", connection, transaction))
                {
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, input.TypeName, 80);
                    object value = command.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        throw new InvalidOperationException("The " + input.TypeName +
                            " document type is not configured. Apply the tender requirement document migration and retry.");
                    documentTypeId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }

                string documentName = Path.GetFileNameWithoutExtension(document.OriginalName);
                if (String.IsNullOrWhiteSpace(documentName)) documentName = input.TypeName;
                if (documentName.Length > 250) documentName = documentName.Substring(0, 250);
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderDocuments
                        (TenderId,DocumentTypeId,DocumentType,DocumentName,Remarks,FileName,FilePath,
                         FileExtension,FileSizeBytes,MimeType,UploadedBy)
                      VALUES(@TenderId,@DocumentTypeId,@DocumentType,@DocumentName,NULL,@FileName,@FilePath,
                             @FileExtension,@FileSizeBytes,@MimeType,@UploadedBy);", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@DocumentTypeId", SqlDbType.Int, documentTypeId);
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, input.TypeName, 80);
                    AddText(command, "@DocumentName", SqlDbType.NVarChar, documentName, 250);
                    AddText(command, "@FileName", SqlDbType.NVarChar, document.OriginalName, 260);
                    AddText(command, "@FilePath", SqlDbType.NVarChar, document.RelativePath, 400);
                    AddText(command, "@FileExtension", SqlDbType.NVarChar, document.Extension, 10);
                    ModuleDb.AddParameter(command, "@FileSizeBytes", SqlDbType.BigInt, document.SizeBytes);
                    AddText(command, "@MimeType", SqlDbType.NVarChar, document.MimeType, 100);
                    ModuleDb.AddParameter(command, "@UploadedBy", SqlDbType.Int, CurrentUserId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("The " + input.TypeName + " document could not be saved.");
                }
            }
            return documentsChanged;
        }

        private static bool IsValidTenderAssetUpload(FileUpload upload, string[] allowedExtensions, int maxBytes)
        {
            if (upload == null || !upload.HasFile) return true;
            string originalName = Path.GetFileName(upload.FileName);
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            return originalName.Length > 0 && originalName.Length <= 260 &&
                upload.PostedFile.ContentLength > 0 && upload.PostedFile.ContentLength <= maxBytes &&
                Array.IndexOf(allowedExtensions, extension) >= 0 &&
                TenderLifecycleFiles.HasAllowedContentSignature(upload.PostedFile.InputStream, extension);
        }

        private static bool IsValidTenderAssetUpload(HttpPostedFile upload, string[] allowedExtensions, int maxBytes)
        {
            if (upload == null || String.IsNullOrWhiteSpace(upload.FileName)) return true;
            string originalName = Path.GetFileName(upload.FileName);
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            return originalName.Length > 0 && originalName.Length <= 260 &&
                upload.ContentLength > 0 && upload.ContentLength <= maxBytes &&
                Array.IndexOf(allowedExtensions, extension) >= 0 &&
                TenderLifecycleFiles.HasAllowedContentSignature(upload.InputStream, extension);
        }

        private ApprovalInput[] GetApprovalInputs()
        {
            return new[]
            {
                new ApprovalInput
                {
                    TypeName=ApprovalTypes[0], Check=AdministrativeApprovalCheck,
                    ReferenceText=AdministrativeApprovalReferenceText, DateText=AdministrativeApprovalDateText,
                    Document=AdministrativeApprovalDocumentUpload, DocumentLink=AdministrativeApprovalDocumentLink
                },
                new ApprovalInput
                {
                    TypeName=ApprovalTypes[1], Check=TechnicalSanctionApprovalCheck,
                    ReferenceText=TechnicalSanctionApprovalReferenceText, DateText=TechnicalSanctionApprovalDateText,
                    Document=TechnicalSanctionApprovalDocumentUpload, DocumentLink=TechnicalSanctionApprovalDocumentLink
                },
                new ApprovalInput
                {
                    TypeName=ApprovalTypes[2], Check=GeneralBodyApprovalCheck,
                    ReferenceText=GeneralBodyApprovalReferenceText, DateText=GeneralBodyApprovalDateText,
                    Document=GeneralBodyApprovalDocumentUpload, DocumentLink=GeneralBodyApprovalDocumentLink
                },
                new ApprovalInput
                {
                    TypeName=ApprovalTypes[3], Check=DpdcApprovalCheck,
                    ReferenceText=DpdcApprovalReferenceText, DateText=DpdcApprovalDateText,
                    Document=DpdcApprovalDocumentUpload, DocumentLink=DpdcApprovalDocumentLink
                }
            };
        }

        private ApprovalInput GetApprovalInput(string typeName)
        {
            foreach (ApprovalInput input in GetApprovalInputs())
                if (String.Equals(input.TypeName, typeName, StringComparison.Ordinal))
                    return input;
            return null;
        }

        private void SaveGisLocation(SqlConnection connection, SqlTransaction transaction, int tenderId,
            decimal? latitude, decimal? longitude, string boundaryCoordinatesJson,
            decimal? selectedAreaSquareMeters, string boundaryLineJson,
            string boundaryGeometryType, string boundaryGeometryJson,
            decimal? boundaryStartLatitude, decimal? boundaryStartLongitude,
            decimal? boundaryEndLatitude, decimal? boundaryEndLongitude,
            List<string> savedPaths, List<string> obsoletePaths)
        {
            bool exists;
            string oldPath = null;
            using (SqlCommand command = new SqlCommand(
                @"SELECT ImagePath FROM dbo.TenderGisLocations WITH (UPDLOCK,HOLDLOCK)
                  WHERE TenderId=@TenderId;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    exists = reader.Read();
                    if (exists && reader["ImagePath"] != DBNull.Value)
                        oldPath = Convert.ToString(reader["ImagePath"]);
                }
            }

            StoredTenderAsset image = GisImageUpload.HasFile
                ? SaveTenderAsset(GisImageUpload, tenderId, "gis", savedPaths)
                : null;
            bool removeImage = String.Equals(RemoveGisImageFlag.Value, "true", StringComparison.OrdinalIgnoreCase);
            if (image != null || removeImage)
                QueueObsoleteAsset(oldPath, obsoletePaths);

            string locationName = String.IsNullOrWhiteSpace(LocationNameText.Text)
                ? null : LocationNameText.Text.Trim();
            if (!exists && !latitude.HasValue && !longitude.HasValue && locationName == null &&
                boundaryCoordinatesJson == null && boundaryLineJson == null &&
                boundaryGeometryType == null && boundaryGeometryJson == null && image == null && !removeImage)
                return;

            string query = exists
                ? @"UPDATE dbo.TenderGisLocations
                    SET Latitude=@Latitude,Longitude=@Longitude,LocationName=@LocationName,
                        BoundaryCoordinatesJson=@BoundaryCoordinatesJson,
                        SelectedAreaSquareMeters=@SelectedAreaSquareMeters,
                        BoundaryGeometryType=@BoundaryGeometryType,
                        BoundaryGeometryJson=@BoundaryGeometryJson,
                        BoundaryStartLatitude=@BoundaryStartLatitude,
                        BoundaryStartLongitude=@BoundaryStartLongitude,
                        BoundaryEndLatitude=@BoundaryEndLatitude,
                        BoundaryEndLongitude=@BoundaryEndLongitude,
                        BoundaryLineGeometryJson=@BoundaryLineGeometryJson,
                        ImageOriginalFileName=CASE WHEN @HasImage=1 THEN @OriginalFileName WHEN @RemoveImage=1 THEN NULL ELSE ImageOriginalFileName END,
                        ImageStoredFileName=CASE WHEN @HasImage=1 THEN @StoredFileName WHEN @RemoveImage=1 THEN NULL ELSE ImageStoredFileName END,
                        ImagePath=CASE WHEN @HasImage=1 THEN @ImagePath WHEN @RemoveImage=1 THEN NULL ELSE ImagePath END,
                        ImageExtension=CASE WHEN @HasImage=1 THEN @Extension WHEN @RemoveImage=1 THEN NULL ELSE ImageExtension END,
                        ImageSizeBytes=CASE WHEN @HasImage=1 THEN @SizeBytes WHEN @RemoveImage=1 THEN NULL ELSE ImageSizeBytes END,
                        ImageUploadedBy=CASE WHEN @HasImage=1 THEN @UserId WHEN @RemoveImage=1 THEN NULL ELSE ImageUploadedBy END,
                        ImageUploadedAtUtc=CASE WHEN @HasImage=1 THEN SYSUTCDATETIME() WHEN @RemoveImage=1 THEN NULL ELSE ImageUploadedAtUtc END,
                        ModifiedBy=@UserId,ModifiedAtUtc=SYSUTCDATETIME()
                    WHERE TenderId=@TenderId;"
                : @"INSERT INTO dbo.TenderGisLocations
                    (TenderId,Latitude,Longitude,LocationName,BoundaryCoordinatesJson,SelectedAreaSquareMeters,
                     BoundaryGeometryType,BoundaryGeometryJson,
                     BoundaryStartLatitude,BoundaryStartLongitude,BoundaryEndLatitude,BoundaryEndLongitude,
                     BoundaryLineGeometryJson,
                     ImageOriginalFileName,ImageStoredFileName,
                     ImagePath,ImageExtension,ImageSizeBytes,ImageUploadedBy,ImageUploadedAtUtc,ModifiedBy)
                    VALUES(@TenderId,@Latitude,@Longitude,@LocationName,@BoundaryCoordinatesJson,@SelectedAreaSquareMeters,
                           @BoundaryGeometryType,@BoundaryGeometryJson,
                           @BoundaryStartLatitude,@BoundaryStartLongitude,@BoundaryEndLatitude,@BoundaryEndLongitude,
                           @BoundaryLineGeometryJson,
                           @OriginalFileName,@StoredFileName,
                           @ImagePath,@Extension,@SizeBytes,@ImageUploadedBy,
                           CASE WHEN @HasImage=1 THEN SYSUTCDATETIME() ELSE NULL END,@UserId);";
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@Latitude", SqlDbType.Decimal, latitude);
                ModuleDb.AddParameter(command, "@Longitude", SqlDbType.Decimal, longitude);
                AddText(command, "@BoundaryCoordinatesJson", SqlDbType.NVarChar, boundaryCoordinatesJson, -1);
                ModuleDb.AddParameter(command, "@SelectedAreaSquareMeters", SqlDbType.Decimal,
                    selectedAreaSquareMeters);
                AddText(command, "@BoundaryGeometryType", SqlDbType.NVarChar, boundaryGeometryType, 20);
                AddText(command, "@BoundaryGeometryJson", SqlDbType.NVarChar, boundaryGeometryJson, -1);
                ModuleDb.AddParameter(command, "@BoundaryStartLatitude", SqlDbType.Decimal, boundaryStartLatitude);
                ModuleDb.AddParameter(command, "@BoundaryStartLongitude", SqlDbType.Decimal, boundaryStartLongitude);
                ModuleDb.AddParameter(command, "@BoundaryEndLatitude", SqlDbType.Decimal, boundaryEndLatitude);
                ModuleDb.AddParameter(command, "@BoundaryEndLongitude", SqlDbType.Decimal, boundaryEndLongitude);
                AddText(command, "@BoundaryLineGeometryJson", SqlDbType.NVarChar, boundaryLineJson, -1);
                AddText(command, "@LocationName", SqlDbType.NVarChar, locationName, 300);
                AddText(command, "@OriginalFileName", SqlDbType.NVarChar, image == null ? null : image.OriginalName, 260);
                AddText(command, "@StoredFileName", SqlDbType.NVarChar, image == null ? null : image.StoredName, 100);
                AddText(command, "@ImagePath", SqlDbType.NVarChar, image == null ? null : image.RelativePath, 400);
                AddText(command, "@Extension", SqlDbType.NVarChar, image == null ? null : image.Extension, 10);
                ModuleDb.AddParameter(command, "@SizeBytes", SqlDbType.BigInt,
                    image == null ? (object)null : image.SizeBytes);
                ModuleDb.AddParameter(command, "@ImageUploadedBy", SqlDbType.Int,
                    image == null ? (object)null : CurrentUserId);
                ModuleDb.AddParameter(command, "@HasImage", SqlDbType.Bit, image != null);
                ModuleDb.AddParameter(command, "@RemoveImage", SqlDbType.Bit, removeImage);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The tender GIS location could not be saved.");
            }
        }

        private void SaveAdministrativeApprovals(SqlConnection connection, SqlTransaction transaction,
            int tenderId, List<string> savedPaths, List<string> obsoletePaths)
        {
            if (!IsJeAdministrator)
            {
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderAdministrativeApprovals(TenderId,ApprovalType,IsApproved,ModifiedBy)
                      SELECT @TenderId,requested.ApprovalType,0,@UserId
                      FROM (VALUES(N'Administrative'),(N'TechnicalSanction'),(N'GeneralBody'),(N'DPDC')) requested(ApprovalType)
                      WHERE NOT EXISTS
                        (SELECT 1 FROM dbo.TenderAdministrativeApprovals existing WITH (UPDLOCK,HOLDLOCK)
                         WHERE existing.TenderId=@TenderId AND existing.ApprovalType=requested.ApprovalType);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    command.ExecuteNonQuery();
                }
                return;
            }

            foreach (ApprovalInput input in GetApprovalInputs())
            {
                bool exists;
                bool oldApproved = false;
                string oldReference = null;
                DateTime? oldDate = null;
                string oldPath = null;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT IsApproved,ApprovalReferenceNumber,ApprovalDate,DocumentPath
                      FROM dbo.TenderAdministrativeApprovals WITH (UPDLOCK,HOLDLOCK)
                      WHERE TenderId=@TenderId AND ApprovalType=@ApprovalType;", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    AddText(command, "@ApprovalType", SqlDbType.NVarChar, input.TypeName, 40);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        exists = reader.Read();
                        if (exists)
                        {
                            oldApproved = Convert.ToBoolean(reader["IsApproved"], CultureInfo.InvariantCulture);
                            oldReference = reader["ApprovalReferenceNumber"] == DBNull.Value
                                ? null : Convert.ToString(reader["ApprovalReferenceNumber"]);
                            oldDate = reader["ApprovalDate"] == DBNull.Value
                                ? (DateTime?)null : Convert.ToDateTime(reader["ApprovalDate"], CultureInfo.InvariantCulture);
                            oldPath = reader["DocumentPath"] == DBNull.Value
                                ? null : Convert.ToString(reader["DocumentPath"]);
                        }
                    }
                }

                StoredTenderAsset document = input.Document.HasFile
                    ? SaveTenderAsset(input.Document, tenderId, "approvals/" + input.TypeName, savedPaths)
                    : null;
                if (document != null) QueueObsoleteAsset(oldPath, obsoletePaths);
                string reference = String.IsNullOrWhiteSpace(input.ReferenceText.Text)
                    ? null : input.ReferenceText.Text.Trim();
                DateTime parsedDate;
                DateTime? approvalDate = TryParseDate(input.DateText.Text, out parsedDate)
                    ? (DateTime?)parsedDate.Date : null;
                bool approved = input.Check.Checked;
                bool changed = !exists || oldApproved != approved ||
                    !String.Equals(oldReference, reference, StringComparison.Ordinal) ||
                    oldDate != approvalDate || document != null;

                using (SqlCommand command = new SqlCommand(
                    exists
                        ? @"UPDATE dbo.TenderAdministrativeApprovals
                            SET IsApproved=@IsApproved,ApprovalReferenceNumber=@ReferenceNumber,ApprovalDate=@ApprovalDate,
                                DocumentOriginalFileName=CASE WHEN @HasDocument=1 THEN @OriginalFileName ELSE DocumentOriginalFileName END,
                                DocumentStoredFileName=CASE WHEN @HasDocument=1 THEN @StoredFileName ELSE DocumentStoredFileName END,
                                DocumentPath=CASE WHEN @HasDocument=1 THEN @DocumentPath ELSE DocumentPath END,
                                DocumentExtension=CASE WHEN @HasDocument=1 THEN @Extension ELSE DocumentExtension END,
                                DocumentSizeBytes=CASE WHEN @HasDocument=1 THEN @SizeBytes ELSE DocumentSizeBytes END,
                                DocumentUploadedBy=CASE WHEN @HasDocument=1 THEN @UserId ELSE DocumentUploadedBy END,
                                DocumentUploadedAtUtc=CASE WHEN @HasDocument=1 THEN SYSUTCDATETIME() ELSE DocumentUploadedAtUtc END,
                                ModifiedBy=@UserId,ModifiedAtUtc=SYSUTCDATETIME()
                            WHERE TenderId=@TenderId AND ApprovalType=@ApprovalType;"
                        : @"INSERT INTO dbo.TenderAdministrativeApprovals
                            (TenderId,ApprovalType,IsApproved,ApprovalReferenceNumber,ApprovalDate,
                             DocumentOriginalFileName,DocumentStoredFileName,DocumentPath,DocumentExtension,
                             DocumentSizeBytes,DocumentUploadedBy,DocumentUploadedAtUtc,ModifiedBy)
                            VALUES(@TenderId,@ApprovalType,@IsApproved,@ReferenceNumber,@ApprovalDate,
                             @OriginalFileName,@StoredFileName,@DocumentPath,@Extension,@SizeBytes,
                             @DocumentUploadedBy,CASE WHEN @HasDocument=1 THEN SYSUTCDATETIME() ELSE NULL END,@UserId);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    AddText(command, "@ApprovalType", SqlDbType.NVarChar, input.TypeName, 40);
                    ModuleDb.AddParameter(command, "@IsApproved", SqlDbType.Bit, approved);
                    AddText(command, "@ReferenceNumber", SqlDbType.NVarChar, reference, 100);
                    ModuleDb.AddParameter(command, "@ApprovalDate", SqlDbType.Date,
                        approvalDate.HasValue ? (object)approvalDate.Value : null);
                    AddText(command, "@OriginalFileName", SqlDbType.NVarChar,
                        document == null ? null : document.OriginalName, 260);
                    AddText(command, "@StoredFileName", SqlDbType.NVarChar,
                        document == null ? null : document.StoredName, 100);
                    AddText(command, "@DocumentPath", SqlDbType.NVarChar,
                        document == null ? null : document.RelativePath, 400);
                    AddText(command, "@Extension", SqlDbType.NVarChar,
                        document == null ? null : document.Extension, 10);
                    ModuleDb.AddParameter(command, "@SizeBytes", SqlDbType.BigInt,
                        document == null ? (object)null : document.SizeBytes);
                    ModuleDb.AddParameter(command, "@DocumentUploadedBy", SqlDbType.Int,
                        document == null ? (object)null : CurrentUserId);
                    ModuleDb.AddParameter(command, "@HasDocument", SqlDbType.Bit, document != null);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("An administrative approval could not be saved.");
                }

                if (changed)
                {
                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TenderAdministrativeApprovalHistory
                            (TenderId,ApprovalType,PreviousIsApproved,NewIsApproved,ApprovalReferenceNumber,
                             ApprovalDate,ActionName,ChangedBy)
                          VALUES(@TenderId,@ApprovalType,@PreviousIsApproved,@NewIsApproved,@ReferenceNumber,
                                 @ApprovalDate,@ActionName,@UserId);", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        AddText(command, "@ApprovalType", SqlDbType.NVarChar, input.TypeName, 40);
                        ModuleDb.AddParameter(command, "@PreviousIsApproved", SqlDbType.Bit,
                            exists ? (object)oldApproved : null);
                        ModuleDb.AddParameter(command, "@NewIsApproved", SqlDbType.Bit, approved);
                        AddText(command, "@ReferenceNumber", SqlDbType.NVarChar, reference, 100);
                        ModuleDb.AddParameter(command, "@ApprovalDate", SqlDbType.Date,
                            approvalDate.HasValue ? (object)approvalDate.Value : null);
                        AddText(command, "@ActionName", SqlDbType.NVarChar, exists ? "Updated" : "Created", 20);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        command.ExecuteNonQuery();
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId,
                        exists ? "UpdateAdministrativeApproval" : "CreateAdministrativeApproval",
                        "Tender", tenderId, input.TypeName + ": " + (approved ? "Approved" : "Pending"),
                        Request.UserHostAddress);
                }
            }
        }

        private StoredTenderAsset SaveTenderAsset(FileUpload upload, int tenderId, string folderName,
            List<string> savedPaths)
        {
            string originalName = SafeTenderDocumentFileName(upload.FileName);
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            string storedName = Guid.NewGuid().ToString("N") + extension;
            string relativeFolder = "tenders/" + tenderId.ToString(CultureInfo.InvariantCulture) + "/" + folderName;
            string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
            string folder = Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/" + relativeFolder);
            Directory.CreateDirectory(folder);
            string fullPath = Path.Combine(folder, storedName);
            upload.SaveAs(fullPath);
            savedPaths.Add(fullPath);
            return new StoredTenderAsset
            {
                OriginalName = originalName,
                StoredName = storedName,
                Extension = extension,
                MimeType = GetRequirementMimeType(extension),
                RelativePath = "Uploads/" + relativeFolder + "/" + storedName,
                FullPath = fullPath,
                SizeBytes = upload.PostedFile.ContentLength
            };
        }

        private StoredTenderAsset SaveTenderAsset(HttpPostedFile upload, int tenderId, string folderName,
            List<string> savedPaths)
        {
            string originalName = SafeTenderDocumentFileName(upload.FileName);
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            string storedName = Guid.NewGuid().ToString("N") + extension;
            string relativeFolder = "tenders/" + tenderId.ToString(CultureInfo.InvariantCulture) + "/" + folderName;
            string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
            string folder = Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/" + relativeFolder);
            Directory.CreateDirectory(folder);
            string fullPath = Path.Combine(folder, storedName);
            upload.SaveAs(fullPath);
            savedPaths.Add(fullPath);
            return new StoredTenderAsset
            {
                OriginalName = originalName,
                StoredName = storedName,
                Extension = extension,
                MimeType = GetRequirementMimeType(extension),
                RelativePath = "Uploads/" + relativeFolder + "/" + storedName,
                FullPath = fullPath,
                SizeBytes = upload.ContentLength
            };
        }

        private void QueueObsoleteAsset(string relativePath, List<string> obsoletePaths)
        {
            if (String.IsNullOrWhiteSpace(relativePath)) return;
            string normalized = relativePath.Replace('\\', '/').TrimStart('/');
            string fullPath;
            if (normalized.StartsWith("Uploads/tenders/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.Contains("..") &&
                TenderLifecycleFiles.TryResolveStoredFilePath(this, normalized, out fullPath))
                obsoletePaths.Add(fullPath);
        }

        private static string FormatIndiaDate(object value)
        {
            if (value == null || value == DBNull.Value) return String.Empty;
            DateTime utc = DateTime.SpecifyKind(Convert.ToDateTime(value, CultureInfo.InvariantCulture), DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FormatIndiaDateTime(object value)
        {
            if (value == null || value == DBNull.Value) return String.Empty;
            DateTime utc = DateTime.SpecifyKind(Convert.ToDateTime(value, CultureInfo.InvariantCulture), DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"))
                .ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        protected void CreateButton_Click(object sender, EventArgs e)
        {
            if (!HasPermission("system.admin") && !HasPermission("tender.create.department") &&
                !HasPermission("tenders.manage.all"))
            {
                RequireAnyPermission("system.admin", "tender.create.department", "tenders.manage.all");
                return;
            }
            Page.Validate();
            if (!Page.IsValid) return;

            bool submitToWorkflow = Object.ReferenceEquals(sender, SubmitTenderButton);
            if (submitToWorkflow && !IsJeAdministrator)
            {
                ShowError("Only JE can submit a new tender into the approval workflow.");
                return;
            }
            decimal amount;
            int departmentId = 0;
            decimal? latitude, longitude;
            string fileError;
            if (!Decimal.TryParse(AmountText.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) ||
                amount <= 0 || amount > 9999999999999999.99m ||
                (EditingTenderId == 0 && !TryGetAuthorizedDepartmentId(out departmentId)))
            {
                ShowError("Enter a positive estimated value and a valid department.");
                return;
            }
            if ((AdditionalConditionsText.Text != null && AdditionalConditionsText.Text.Length > 4000) ||
                (TermsAndConditionsText.Text != null && TermsAndConditionsText.Text.Length > 4000))
            {
                ShowError("Additional conditions and terms cannot exceed 4,000 characters each.");
                return;
            }
            if (String.IsNullOrWhiteSpace(TitleText.Text))
            {
                ShowError("Project title is required.");
                return;
            }
            if ((BudgetInformationText.Text != null && BudgetInformationText.Text.Length > 2000) ||
                (TechnicalSanctionNumberText.Text != null && TechnicalSanctionNumberText.Text.Length > 100) ||
                (TechnicalSanctionAuthorityText.Text != null && TechnicalSanctionAuthorityText.Text.Length > 200))
            {
                ShowError("Budget and technical-sanction details exceed the allowed field lengths.");
                return;
            }
            string boundaryCoordinatesJson;
            decimal? selectedAreaSquareMeters, boundaryLatitude, boundaryLongitude;
            string boundaryLineJson, boundaryGeometryType, boundaryGeometryJson;
            decimal? boundaryStartLatitude, boundaryStartLongitude, boundaryEndLatitude, boundaryEndLongitude;
            if (LocationNameText.Text.Length > 300 ||
                !TryParseGisBoundary(GisBoundaryCoordinates.Value,
                    out boundaryCoordinatesJson, out selectedAreaSquareMeters,
                    out boundaryLatitude, out boundaryLongitude) ||
                !TryParseGisBoundaryLine(GisBoundaryLineCoordinates.Value, out boundaryLineJson,
                    out boundaryStartLatitude, out boundaryStartLongitude,
                    out boundaryEndLatitude, out boundaryEndLongitude))
            {
                ShowError("GIS geometry is incomplete or invalid. Select exactly two distinct endpoints for a line or at least three points for a valid polygon.");
                return;
            }
            boundaryGeometryType = (GisBoundaryGeometryType.Value ?? String.Empty).Trim();
            if (boundaryGeometryType.Length == 0)
                boundaryGeometryType = boundaryCoordinatesJson != null
                    ? "Polygon" : boundaryLineJson != null ? "LineString" : String.Empty;
            if (boundaryGeometryType == "Polygon" && boundaryCoordinatesJson != null)
                boundaryGeometryJson = "{\"type\":\"Polygon\",\"coordinates\":[" +
                    boundaryCoordinatesJson + "]}";
            else if (boundaryGeometryType == "LineString" && boundaryLineJson != null)
                boundaryGeometryJson = boundaryLineJson;
            else if (boundaryGeometryType.Length == 0 &&
                boundaryCoordinatesJson == null && boundaryLineJson == null)
                boundaryGeometryJson = null;
            else
            {
                ShowError("GIS selection is incomplete or invalid. Finish a two-point line or a closed polygon with at least three distinct points.");
                return;
            }
            if (boundaryCoordinatesJson != null)
            {
                latitude = boundaryLatitude;
                longitude = boundaryLongitude;
            }
            else if (boundaryGeometryType == "LineString" &&
                boundaryStartLatitude.HasValue && boundaryEndLatitude.HasValue &&
                boundaryStartLongitude.HasValue && boundaryEndLongitude.HasValue)
            {
                latitude = Decimal.Round((boundaryStartLatitude.Value + boundaryEndLatitude.Value) / 2m, 6);
                longitude = Decimal.Round((boundaryStartLongitude.Value + boundaryEndLongitude.Value) / 2m, 6);
            }
            else if (!TryParseCoordinate(LatitudeText.Text, -90m, 90m, out latitude) ||
                !TryParseCoordinate(LongitudeText.Text, -180m, 180m, out longitude) ||
                (latitude.HasValue != longitude.HasValue))
            {
                ShowError("Enter both coordinates when providing a location. Latitude must be -90 to 90 and longitude -180 to 180.");
                return;
            }
            if (!ValidateApprovalInputs())
            {
                ShowError("Approval reference numbers, dates, and uploaded files must be valid.");
                return;
            }
            DateTime sanctionDate = DateTime.MinValue;
            bool hasSanctionDate = !String.IsNullOrWhiteSpace(TechnicalSanctionDateText.Text);
            if (hasSanctionDate && !TryParseDate(TechnicalSanctionDateText.Text, out sanctionDate))
            {
                ShowError("Enter a valid technical-sanction date.");
                return;
            }
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    if (!IsActiveCatalogValue(connection, "dbo.TenderMethods", "MethodName", TenderTypeSelect.SelectedValue) ||
                        !IsActiveCatalogValue(connection, "dbo.TenderTypes", "TypeName", TenderCategorySelect.SelectedValue) ||
                        !IsActiveFinancialYear(connection, FinancialYearSelect.SelectedValue))
                    {
                        ShowError("Select active tender method, category, and financial-year values from the master data.");
                        return;
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender reference data could not be validated.", ex);
                ShowError("Tender reference data could not be validated. Please retry or contact support.");
                return;
            }
            if (!PrepareGisAndApprovalUploads(out fileError))
            {
                ShowError(fileError);
                return;
            }
            string missingRequirementCategory;
            if (!HasRequiredRequirementDocuments(out missingRequirementCategory))
            {
                ShowError("Upload at least one " + missingRequirementCategory + " document before continuing.");
                return;
            }

            if (EditingTenderId > 0)
            {
                UpdateDraftTender(amount,
                    hasSanctionDate ? (DateTime?)sanctionDate.Date : null,
                    latitude, longitude, boundaryCoordinatesJson, selectedAreaSquareMeters,
                    boundaryLineJson, boundaryGeometryType, boundaryGeometryJson,
                    boundaryStartLatitude, boundaryStartLongitude,
                    boundaryEndLatitude, boundaryEndLongitude, submitToWorkflow);
                return;
            }

            List<string> savedPaths = new List<string>();
            List<string> obsoletePaths = new List<string>();
            bool requirementDocumentsChanged = false;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    if (!IsActiveCatalogValue(connection, "dbo.TenderMethods", "MethodName", TenderTypeSelect.SelectedValue, transaction) ||
                        !IsActiveCatalogValue(connection, "dbo.TenderTypes", "TypeName", TenderCategorySelect.SelectedValue, transaction) ||
                        !IsActiveFinancialYear(connection, FinancialYearSelect.SelectedValue, transaction))
                        throw new InvalidOperationException("A selected tender master value is no longer active. Refresh the form and retry.");

                    int tenderId;
                    int categoryId = GetActiveTenderCategoryId(connection, transaction, TenderCategorySelect.SelectedValue);
                    if (categoryId <= 0)
                        throw new InvalidOperationException("The selected tender category is no longer active. Refresh the form and retry.");
                    TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                    int year = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone).Year;
                    string tenderNumber;
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.Departments WHERE DepartmentId=@DepartmentId AND IsActive=1;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                            throw new InvalidOperationException("The selected department is no longer active. Refresh the form and select an active department.");
                    }
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT COALESCE(MAX(TRY_CONVERT(int,SUBSTRING(TenderNumber,LEN(@Prefix)+1,50))),0)+1
                          FROM dbo.Tender WITH (UPDLOCK, HOLDLOCK)
                          WHERE TenderNumber LIKE @PrefixPattern;", connection, transaction))
                    {
                        string prefix = "TND-" + year.ToString(CultureInfo.InvariantCulture) + "-";
                        AddText(command, "@Prefix", SqlDbType.NVarChar, prefix, 20);
                        AddText(command, "@PrefixPattern", SqlDbType.NVarChar, prefix + "%", 21);
                        int nextSequence = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        tenderNumber = "TND-" + year.ToString(CultureInfo.InvariantCulture) + "-" +
                                       nextSequence.ToString("D3", CultureInfo.InvariantCulture);
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.Tender
                            (TenderNumber,TrackingId,Title,Description,DepartmentId,EstimatedAmount,TenderType,TenderMethod,
                             TenderCategory,TenderCategoryId,FinancialYear,
                             CurrentStatus,CreatedBy,WorkflowVersion,
                             BudgetInformation,TechnicalSanctionNumber,TechnicalSanctionAuthority,TechnicalSanctionDate,
                             WizardCurrentStep,WizardCompletedStepsMask)
                          VALUES
                            (@TenderNumber,@TrackingId,@Title,@Description,@DepartmentId,@Amount,@TenderMethod,@TenderMethod,
                             @TenderCategory,@TenderCategoryId,@FinancialYear,
                             N'Draft',@CreatedBy,2,@BudgetInformation,@SanctionNumber,
                             @SanctionAuthority,@SanctionDate,4,@WizardCompletedStepsMask);
                          SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                    {
                        AddText(command, "@TenderNumber", SqlDbType.NVarChar, tenderNumber, 50);
                        AddText(command, "@TrackingId", SqlDbType.NVarChar, Guid.NewGuid().ToString(), 100);
                        AddText(command, "@Title", SqlDbType.NVarChar, TitleText.Text.Trim(), -1);
                        AddText(command, "@Description", SqlDbType.NVarChar, DescriptionText.Text, -1);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, amount);
                        AddText(command, "@TenderMethod", SqlDbType.NVarChar, TenderTypeSelect.SelectedValue, 50);
                        AddText(command, "@TenderCategory", SqlDbType.NVarChar, TenderCategorySelect.SelectedValue, 50);
                        ModuleDb.AddParameter(command, "@TenderCategoryId", SqlDbType.Int, categoryId);
                        AddText(command, "@FinancialYear", SqlDbType.NVarChar, FinancialYearSelect.SelectedValue, 9);
                        ModuleDb.AddParameter(command, "@CreatedBy", SqlDbType.Int, CurrentUserId);
                        AddText(command, "@BudgetInformation", SqlDbType.NVarChar, BudgetInformationText.Text, 2000);
                        AddText(command, "@SanctionNumber", SqlDbType.NVarChar, TechnicalSanctionNumberText.Text, 100);
                        AddText(command, "@SanctionAuthority", SqlDbType.NVarChar, TechnicalSanctionAuthorityText.Text, 200);
                        ModuleDb.AddParameter(command, "@SanctionDate", SqlDbType.Date,
                            hasSanctionDate ? (object)sanctionDate.Date : null);
                        ModuleDb.AddParameter(command, "@WizardCompletedStepsMask", SqlDbType.TinyInt,
                            GetWizardCompletedStepsMask());
                        tenderId = Convert.ToInt32(command.ExecuteScalar());
                    }

                    SaveTenderFormConditions(connection, transaction, tenderId);
                    requirementDocumentsChanged = AppendRequirementDocuments(connection, transaction,
                        tenderId, savedPaths);
                    SaveGisLocation(connection, transaction, tenderId, latitude, longitude,
                        boundaryCoordinatesJson, selectedAreaSquareMeters, boundaryLineJson,
                        boundaryGeometryType, boundaryGeometryJson,
                        boundaryStartLatitude, boundaryStartLongitude, boundaryEndLatitude, boundaryEndLongitude,
                        savedPaths, obsoletePaths);
                    SaveAdministrativeApprovals(connection, transaction, tenderId, savedPaths, obsoletePaths);
                    RecordTenderRevision(connection, transaction, tenderId, "Draft created", "Initial tender draft.");

                    int jeRoleId;
                    int initialStageId;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT r.RoleId,s.StageId
                          FROM dbo.Roles r
                          CROSS JOIN dbo.WorkflowStages s
                          WHERE r.RoleName=N'Junior Engineer (JE)' AND r.IsActive=1
                            AND s.WorkflowVersion=2 AND s.StageName=N'Final_JE_SubmitTender';", connection, transaction))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("The initial finalized workflow stage is not configured.");
                        jeRoleId = Convert.ToInt32(reader["RoleId"]);
                        initialStageId = Convert.ToInt32(reader["StageId"]);
                    }
                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TenderWorkflowAssignments
                            (TenderId,StageId,AssignedRoleId,AssignedUserId,DepartmentId,IsCurrent)
                          VALUES(@TenderId,@StageId,@RoleId,@AssignedUserId,@DepartmentId,1);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, initialStageId);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, jeRoleId);
                        ModuleDb.AddParameter(command, "@AssignedUserId", SqlDbType.Int,
                            HasAnyRole("Junior Engineer (JE)") ? (object)CurrentUserId : null);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        command.ExecuteNonQuery();
                    }
                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TenderWorkflowHistory
                            (TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,
                             UserId,RoleId,DepartmentId,Remarks)
                          VALUES(@TenderId,2,@StageId,NULL,N'Draft',N'Created',@UserId,@RoleId,@DepartmentId,@Remarks);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, initialStageId);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        AddText(command, "@Remarks", SqlDbType.NVarChar, "Draft tender created.", 2000);
                        command.ExecuteNonQuery();
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Create", "Tender", tenderId, tenderNumber, Request.UserHostAddress);
                    transaction.Commit();
                    DeleteSavedFiles(obsoletePaths);
                    string destination = submitToWorkflow
                        ? "~/Pages/TenderWorkflow.aspx?id=" + tenderId.ToString(CultureInfo.InvariantCulture)
                        : "~/Pages/TenderDetails.aspx?id=" + tenderId.ToString(CultureInfo.InvariantCulture) +
                          "&saved=1" + (requirementDocumentsChanged ? "&documentsSaved=1" : String.Empty);
                    Response.Redirect(destination, false);
                    Context.ApplicationInstance.CompleteRequest();
                }
            }
            catch (SqlException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender creation failed.", ex);
                ShowError(ex.Number == 2601 || ex.Number == 2627
                    ? "The tender number already exists. Retry saving to generate the next number."
                    : "The tender could not be saved because of a database error. Please retry or contact support.");
            }
            catch (IOException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender requirement document could not be stored.", ex);
                ShowError("A requirement document could not be saved. The tender was not submitted.");
            }
            catch (UnauthorizedAccessException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender requirement document storage access was denied.", ex);
                ShowError("A requirement document could not be saved because file storage is unavailable.");
            }
            catch (InvalidOperationException ex)
            {
                DeleteSavedFiles(savedPaths);
                ShowError(ex.Message);
            }
        }

        private bool TryGetAuthorizedDepartmentId(out int departmentId)
        {
            departmentId = 0;
            if (DepartmentPanel.Visible)
                return Int32.TryParse(DepartmentSelect.SelectedValue, NumberStyles.None,
                    CultureInfo.InvariantCulture, out departmentId) && departmentId > 0;

            if (!CurrentDepartmentId.HasValue)
                return false;
            departmentId = CurrentDepartmentId.Value;
            return true;
        }

        private static bool TryParseCoordinate(string value, decimal minimum, decimal maximum, out decimal? coordinate)
        {
            coordinate = null;
            if (String.IsNullOrWhiteSpace(value)) return true;
            decimal parsed;
            if (!Decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out parsed) ||
                parsed < minimum || parsed > maximum || Decimal.Round(parsed, 6) != parsed)
                return false;
            coordinate = parsed;
            return true;
        }

        private static bool TryParseGisBoundaryLine(string json, out string normalizedJson,
            out decimal? startLatitude, out decimal? startLongitude,
            out decimal? endLatitude, out decimal? endLongitude)
        {
            normalizedJson = null;
            startLatitude = startLongitude = endLatitude = endLongitude = null;
            if (String.IsNullOrWhiteSpace(json)) return true;

            GisLineGeometry geometry;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 16384 };
                geometry = serializer.Deserialize<GisLineGeometry>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (geometry == null || !String.Equals(geometry.type, "LineString", StringComparison.Ordinal) ||
                geometry.coordinates == null || geometry.coordinates.Count != 2)
                return false;

            List<decimal> start = geometry.coordinates[0];
            List<decimal> end = geometry.coordinates[1];
            if (start == null || end == null || start.Count != 2 || end.Count != 2 ||
                start[0] < -180m || start[0] > 180m || end[0] < -180m || end[0] > 180m ||
                start[1] < -90m || start[1] > 90m || end[1] < -90m || end[1] > 90m ||
                Decimal.Round(start[0], 6) != start[0] || Decimal.Round(start[1], 6) != start[1] ||
                Decimal.Round(end[0], 6) != end[0] || Decimal.Round(end[1], 6) != end[1] ||
                (start[0] == end[0] && start[1] == end[1]))
                return false;

            startLongitude = start[0];
            startLatitude = start[1];
            endLongitude = end[0];
            endLatitude = end[1];
            normalizedJson = new JavaScriptSerializer().Serialize(new GisLineGeometry
            {
                type = "LineString",
                coordinates = new List<List<decimal>>
                {
                    new List<decimal> { start[0], start[1] },
                    new List<decimal> { end[0], end[1] }
                }
            });
            return true;
        }

        private static bool TryParseGisBoundary(string json, out string normalizedJson,
            out decimal? areaSquareMeters, out decimal? centroidLatitude, out decimal? centroidLongitude)
        {
            normalizedJson = null;
            areaSquareMeters = null;
            centroidLatitude = null;
            centroidLongitude = null;
            if (String.IsNullOrWhiteSpace(json)) return true;

            List<List<decimal>> coordinates;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = 131072 };
                coordinates = serializer.Deserialize<List<List<decimal>>>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (coordinates == null || coordinates.Count < 4 || coordinates.Count > 501)
                return false;
            List<decimal> first = coordinates[0];
            List<decimal> last = coordinates[coordinates.Count - 1];
            if (first == null || last == null || first.Count != 2 || last.Count != 2 ||
                first[0] != last[0] || first[1] != last[1])
                return false;

            const double earthRadius = 6378137d;
            double referenceLatitude = 0d;
            for (int i = 0; i < coordinates.Count - 1; i++)
            {
                List<decimal> coordinate = coordinates[i];
                if (coordinate == null || coordinate.Count != 2 ||
                    coordinate[0] < -180m || coordinate[0] > 180m ||
                    coordinate[1] < -90m || coordinate[1] > 90m ||
                    Decimal.Round(coordinate[0], 6) != coordinate[0] ||
                    Decimal.Round(coordinate[1], 6) != coordinate[1])
                    return false;

                List<decimal> next = coordinates[i + 1];
                if (next == null || next.Count != 2 ||
                    Math.Abs((double)(next[0] - coordinate[0])) > 180d)
                    return false;

                if (coordinate[0] == next[0] && coordinate[1] == next[1])
                    return false;
                referenceLatitude += (double)coordinate[1];
            }
            referenceLatitude /= coordinates.Count - 1;

            double longitudeScale = Math.Cos(referenceLatitude * Math.PI / 180d);
            double sphericalAreaSum = 0d;
            double centroidCrossSum = 0d;
            double centroidX = 0d;
            double centroidY = 0d;
            for (int i = 0; i < coordinates.Count - 1; i++)
            {
                double longitude1 = (double)coordinates[i][0] * Math.PI / 180d;
                double latitude1 = (double)coordinates[i][1] * Math.PI / 180d;
                double longitude2 = (double)coordinates[i + 1][0] * Math.PI / 180d;
                double latitude2 = (double)coordinates[i + 1][1] * Math.PI / 180d;
                sphericalAreaSum += (longitude2 - longitude1) *
                    (2d + Math.Sin(latitude1) + Math.Sin(latitude2));

                double x1 = earthRadius * longitude1 * longitudeScale;
                double y1 = earthRadius * latitude1;
                double x2 = earthRadius * longitude2 * longitudeScale;
                double y2 = earthRadius * latitude2;
                double cross = x1 * y2 - x2 * y1;
                centroidCrossSum += cross;
                centroidX += (x1 + x2) * cross;
                centroidY += (y1 + y2) * cross;
            }

            double area = Math.Abs(sphericalAreaSum) * earthRadius * earthRadius / 2d;
            if (Double.IsNaN(area) || Double.IsInfinity(area) || area <= 0d ||
                area >= 10000000000000000d || Double.IsNaN(centroidCrossSum) ||
                Double.IsInfinity(centroidCrossSum) || Math.Abs(centroidCrossSum) < 0.000001d ||
                longitudeScale <= 0d)
                return false;
            if (HasSelfIntersectingBoundary(coordinates))
                return false;

            double longitude = centroidX / (3d * centroidCrossSum) /
                (earthRadius * longitudeScale) * 180d / Math.PI;
            double latitude = centroidY / (3d * centroidCrossSum) /
                earthRadius * 180d / Math.PI;
            if (Double.IsNaN(latitude) || Double.IsInfinity(latitude) ||
                Double.IsNaN(longitude) || Double.IsInfinity(longitude) ||
                latitude < -90d || latitude > 90d || longitude < -180d || longitude > 180d)
                return false;

            areaSquareMeters = Decimal.Round((decimal)area, 2);
            centroidLatitude = Decimal.Round((decimal)latitude, 6);
            centroidLongitude = Decimal.Round((decimal)longitude, 6);
            normalizedJson = new JavaScriptSerializer { MaxJsonLength = 131072 }
                .Serialize(coordinates);
            return true;
        }

        private static bool HasSelfIntersectingBoundary(List<List<decimal>> coordinates)
        {
            int edgeCount = coordinates.Count - 1;
            for (int firstEdge = 0; firstEdge < edgeCount; firstEdge++)
            {
                List<decimal> a = coordinates[firstEdge];
                List<decimal> b = coordinates[firstEdge + 1];
                for (int secondEdge = firstEdge + 1; secondEdge < edgeCount; secondEdge++)
                {
                    if (secondEdge == firstEdge + 1 ||
                        (firstEdge == 0 && secondEdge == edgeCount - 1))
                        continue;
                    List<decimal> c = coordinates[secondEdge];
                    List<decimal> d = coordinates[secondEdge + 1];
                    if (SegmentsIntersect(a[0], a[1], b[0], b[1],
                        c[0], c[1], d[0], d[1]))
                        return true;
                }
            }
            return false;
        }

        private static bool SegmentsIntersect(decimal ax, decimal ay, decimal bx, decimal by,
            decimal cx, decimal cy, decimal dx, decimal dy)
        {
            decimal first = CrossProduct(ax, ay, bx, by, cx, cy);
            decimal second = CrossProduct(ax, ay, bx, by, dx, dy);
            decimal third = CrossProduct(cx, cy, dx, dy, ax, ay);
            decimal fourth = CrossProduct(cx, cy, dx, dy, bx, by);
            if ((first == 0m && IsOnSegment(ax, ay, bx, by, cx, cy)) ||
                (second == 0m && IsOnSegment(ax, ay, bx, by, dx, dy)) ||
                (third == 0m && IsOnSegment(cx, cy, dx, dy, ax, ay)) ||
                (fourth == 0m && IsOnSegment(cx, cy, dx, dy, bx, by)))
                return true;
            return ((first > 0m && second < 0m) || (first < 0m && second > 0m)) &&
                ((third > 0m && fourth < 0m) || (third < 0m && fourth > 0m));
        }

        private static bool IsOnSegment(decimal ax, decimal ay, decimal bx, decimal by,
            decimal px, decimal py)
        {
            return px >= Math.Min(ax, bx) && px <= Math.Max(ax, bx) &&
                py >= Math.Min(ay, by) && py <= Math.Max(ay, by);
        }

        private static decimal CrossProduct(decimal ax, decimal ay, decimal bx, decimal by,
            decimal cx, decimal cy)
        {
            return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        }

        private byte GetWizardCompletedStepsMask()
        {
            byte mask;
            return Byte.TryParse(WizardCompletedStepsMask.Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out mask) ? (byte)(mask & 15) : (byte)0;
        }

        private static int NormalizeWizardCurrentStep(object value)
        {
            int step;
            if (value == null || value == DBNull.Value ||
                !Int32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out step))
                return 1;
            return Math.Max(1, Math.Min(4, step));
        }

        private static byte NormalizeWizardCompletedStepsMask(object value)
        {
            byte mask;
            if (value == null || value == DBNull.Value ||
                !Byte.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out mask))
                return 0;
            return (byte)(mask & 15);
        }

        private static bool TryParseDate(string value, out DateTime result)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result);
        }

        private static bool TryParseIndiaLocalDate(string value, out DateTime utc, bool useEndOfDay = false)
        {
            return TenderDateRules.TryParseIndiaLocalDate(value, out utc, useEndOfDay);
        }

        private static DateTime GetIndiaCalendarDate(DateTime utc)
        {
            return TenderDateRules.GetIndiaCalendarDate(utc);
        }

        private static string ValidateTenderSchedule(DateTime? publication, DateTime? submissionStart,
            DateTime? submissionClosing, DateTime? technicalOpening, DateTime? financialOpening,
            DateTime? preBidMeeting, DateTime utcNow, bool isNewTender)
        {
            return TenderDateRules.ValidateTenderSchedule(publication, submissionStart,
                submissionClosing, technicalOpening, financialOpening, preBidMeeting, utcNow, isNewTender);
        }

        private static bool IsActiveCatalogValue(SqlConnection connection, string tableName, string columnName,
            string value, SqlTransaction transaction = null)
        {
            string query = "SELECT COUNT(*) FROM " + tableName + " WHERE " + columnName + "=@Value AND IsActive=1;";
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                AddText(command, "@Value", SqlDbType.NVarChar, value, 80);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        private static bool IsActiveFinancialYear(SqlConnection connection, string financialYear, SqlTransaction transaction = null)
        {
            using (SqlCommand command = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.FinancialYears WHERE FinancialYear=@FinancialYear AND IsActive=1;",
                connection, transaction))
            {
                AddText(command, "@FinancialYear", SqlDbType.NVarChar, financialYear, 9);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        private static int GetActiveTenderCategoryId(SqlConnection connection, SqlTransaction transaction,
            string category)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT TenderCategoryId FROM dbo.TenderTypes WITH (UPDLOCK,HOLDLOCK)
                  WHERE TypeName=@Category AND IsActive=1;", connection, transaction))
            {
                AddText(command, "@Category", SqlDbType.NVarChar, category, 50);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value
                    ? 0
                    : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }

        private void SaveTenderFormConditions(SqlConnection connection, SqlTransaction transaction, int tenderId)
        {
            using (SqlCommand command = new SqlCommand(
                @"IF EXISTS (SELECT 1 FROM dbo.TenderRequirements WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId)
                  BEGIN
                      UPDATE dbo.TenderRequirements
                      SET AdditionalConditions=@AdditionalConditions,TermsAndConditions=@TermsAndConditions,
                          ModifiedBy=@UserId,ModifiedAtUtc=SYSUTCDATETIME()
                      WHERE TenderId=@TenderId;
                  END
                  ELSE
                  BEGIN
                      INSERT INTO dbo.TenderRequirements
                          (TenderId,AdditionalConditions,TermsAndConditions,CreatedBy,ModifiedBy)
                      VALUES(@TenderId,@AdditionalConditions,@TermsAndConditions,@UserId,@UserId);
                  END;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                AddText(command, "@AdditionalConditions", SqlDbType.NVarChar, AdditionalConditionsText.Text, 4000);
                AddText(command, "@TermsAndConditions", SqlDbType.NVarChar, TermsAndConditionsText.Text, 4000);
                command.ExecuteNonQuery();
            }
        }

        private void RecordTenderRevision(SqlConnection connection, SqlTransaction transaction,
            int tenderId, string action, string reason)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderRevisions
                    (TenderId,RevisionNumber,ActionName,RevisionReason,TenderNumber,Title,Description,
                     EligibilityCriteria,DepartmentId,EstimatedAmount,TenderType,TenderMethod,TenderCategory,
                     TenderCategoryId,TenderSubcategoryId,FinancialYear,EMDAmount,TenderDocumentFee,ProcessingFee,
                     SecurityDepositAmount,PublicationScheduledUtc,SubmissionStartDate,SubmissionDeadline,
                     TechnicalBidOpeningScheduledUtc,FinancialBidOpeningScheduledUtc,CurrentStatus,
                     ScopeOfWork,TechnicalRequirements,FinancialRequirements,AdditionalConditions,
                     TermsAndConditions,RevisedBy)
                  SELECT t.TenderId,
                         COALESCE((SELECT MAX(r.RevisionNumber) FROM dbo.TenderRevisions r WITH (UPDLOCK,HOLDLOCK)
                                   WHERE r.TenderId=t.TenderId),0)+1,
                         @Action,@Reason,t.TenderNumber,t.Title,t.Description,t.EligibilityCriteria,
                         t.DepartmentId,t.EstimatedAmount,t.TenderType,t.TenderMethod,t.TenderCategory,
                         t.TenderCategoryId,t.TenderSubcategoryId,t.FinancialYear,t.EMDAmount,t.TenderDocumentFee,t.ProcessingFee,
                         t.SecurityDepositAmount,t.PublicationScheduledUtc,t.SubmissionStartDate,t.SubmissionDeadline,
                         t.TechnicalBidOpeningScheduledUtc,t.FinancialBidOpeningScheduledUtc,t.CurrentStatus,
                         req.ScopeOfWork,req.TechnicalRequirements,req.FinancialRequirements,
                         req.AdditionalConditions,req.TermsAndConditions,@UserId
                  FROM dbo.Tender t
                  LEFT JOIN dbo.TenderRequirements req ON req.TenderId=t.TenderId
                  WHERE t.TenderId=@TenderId;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                AddText(command, "@Action", SqlDbType.NVarChar, action, 50);
                AddText(command, "@Reason", SqlDbType.NVarChar, reason, 1000);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The tender revision could not be recorded.");
            }
        }

        private void UpdateDraftTender(decimal amount,
            DateTime? sanctionDate,
            decimal? latitude, decimal? longitude, string boundaryCoordinatesJson,
            decimal? selectedAreaSquareMeters, string boundaryLineJson,
            string boundaryGeometryType, string boundaryGeometryJson,
            decimal? boundaryStartLatitude, decimal? boundaryStartLongitude,
            decimal? boundaryEndLatitude, decimal? boundaryEndLongitude, bool submitToWorkflow)
        {
            bool isAdministrator = IsJeAdministrator;
            bool isDepartmentOfficer = !isAdministrator &&
                (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                 String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase));
            List<string> savedPaths = new List<string>();
            List<string> obsoletePaths = new List<string>();
            bool requirementDocumentsChanged = false;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    if (!IsActiveCatalogValue(connection, "dbo.TenderMethods", "MethodName", TenderTypeSelect.SelectedValue, transaction) ||
                        !IsActiveCatalogValue(connection, "dbo.TenderTypes", "TypeName", TenderCategorySelect.SelectedValue, transaction) ||
                        !IsActiveFinancialYear(connection, FinancialYearSelect.SelectedValue, transaction))
                        throw new InvalidOperationException("A selected tender master value is no longer active. Refresh the form and retry.");
                    int categoryId = GetActiveTenderCategoryId(connection, transaction, TenderCategorySelect.SelectedValue);
                    if (categoryId <= 0)
                        throw new InvalidOperationException("The selected tender category is no longer active. Refresh the form and retry.");
                    string currentStatus;
                    using (SqlCommand read = new SqlCommand(
                        @"SELECT t.CurrentStatus FROM dbo.Tender t WITH (UPDLOCK,ROWLOCK)
                          WHERE t.TenderId=@TenderId AND t.IsDeleted=0
                            AND
                            (
                                (t.WorkflowVersion=1 AND
                                 (@IsAdministrator=1 OR
                                  (@IsDepartmentOfficer=1 AND t.CreatedBy=@UserId
                                   AND t.CurrentStatus=N'Budget Revision Required'
                                   AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                                   AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                   WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId IS NOT NULL)
                                        OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                                  WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId=t.DepartmentId))))
                                OR
                                (t.WorkflowVersion=2 AND @IsAdministrator=1 AND t.CurrentStatus=N'Draft'
                                 AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId
                                 AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                            INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                            WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                              AND s.StageName=N'Final_JE_SubmitTender'))
                            );",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(read, "@TenderId", SqlDbType.Int, EditingTenderId);
                        ModuleDb.AddParameter(read, "@IsAdministrator", SqlDbType.Bit, isAdministrator);
                        ModuleDb.AddParameter(read, "@IsDepartmentOfficer", SqlDbType.Bit, isDepartmentOfficer);
                        ModuleDb.AddParameter(read, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(read, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        object value = read.ExecuteScalar();
                        if (value == null || value == DBNull.Value)
                            throw new InvalidOperationException("This tender cannot be edited in your role or department.");
                        currentStatus = Convert.ToString(value);
                    }
                    if (currentStatus != "Draft" && currentStatus != "Budget Revision Required")
                        throw new InvalidOperationException("Only a draft or returned budget proposal can be edited.");

                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE t
                          SET Title=@Title,Description=@Description,
                              EstimatedAmount=@Amount,TenderType=@TenderMethod,TenderMethod=@TenderMethod,
                              TenderCategory=@TenderCategory,TenderCategoryId=@TenderCategoryId,
                              FinancialYear=@FinancialYear,
                              BudgetInformation=CASE WHEN @IsAdministrator=1 THEN @BudgetInformation ELSE BudgetInformation END,
                              TechnicalSanctionNumber=CASE WHEN @IsAdministrator=1 THEN @SanctionNumber ELSE TechnicalSanctionNumber END,
                              TechnicalSanctionAuthority=CASE WHEN @IsAdministrator=1 THEN @SanctionAuthority ELSE TechnicalSanctionAuthority END,
                              TechnicalSanctionDate=CASE WHEN @IsAdministrator=1 THEN @SanctionDate ELSE TechnicalSanctionDate END,
                              WizardCurrentStep=4,WizardCompletedStepsMask=@WizardCompletedStepsMask,
                              ModifiedBy=@ModifiedBy,ModifiedDate=SYSUTCDATETIME()
                          FROM dbo.Tender t
                          WHERE t.TenderId=@TenderId AND t.CurrentStatus=@ExpectedStatus AND t.IsDeleted=0
                            AND
                            (
                                (t.WorkflowVersion=1 AND (@IsAdministrator=1 OR
                                 (@IsDepartmentOfficer=1 AND t.CreatedBy=@UserId)))
                                OR
                                (t.WorkflowVersion=2 AND @IsAdministrator=1 AND t.CurrentStatus=N'Draft'
                                 AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId
                                 AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                            INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                            WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                              AND s.StageName=N'Final_JE_SubmitTender'))
                            );", connection, transaction))
                    {
                        AddText(command, "@Title", SqlDbType.NVarChar, TitleText.Text.Trim(), -1);
                        AddText(command, "@Description", SqlDbType.NVarChar, DescriptionText.Text, -1);
                        ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, amount);
                        AddText(command, "@TenderMethod", SqlDbType.NVarChar, TenderTypeSelect.SelectedValue, 50);
                        AddText(command, "@TenderCategory", SqlDbType.NVarChar, TenderCategorySelect.SelectedValue, 50);
                        ModuleDb.AddParameter(command, "@TenderCategoryId", SqlDbType.Int, categoryId);
                        AddText(command, "@FinancialYear", SqlDbType.NVarChar, FinancialYearSelect.SelectedValue, 9);
                        AddText(command, "@BudgetInformation", SqlDbType.NVarChar, BudgetInformationText.Text, 2000);
                        AddText(command, "@SanctionNumber", SqlDbType.NVarChar, TechnicalSanctionNumberText.Text, 100);
                        AddText(command, "@SanctionAuthority", SqlDbType.NVarChar, TechnicalSanctionAuthorityText.Text, 200);
                        ModuleDb.AddParameter(command, "@SanctionDate", SqlDbType.Date, sanctionDate.HasValue ? (object)sanctionDate.Value : null);
                        ModuleDb.AddParameter(command, "@WizardCompletedStepsMask", SqlDbType.TinyInt,
                            GetWizardCompletedStepsMask());
                        ModuleDb.AddParameter(command, "@ModifiedBy", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        AddText(command, "@ExpectedStatus", SqlDbType.NVarChar, currentStatus, 50);
                        ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit, isAdministrator);
                        ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit, isDepartmentOfficer);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Tender status changed; refresh the page before editing.");
                    }

                    SaveTenderFormConditions(connection, transaction, EditingTenderId);
                    requirementDocumentsChanged = AppendRequirementDocuments(connection, transaction,
                        EditingTenderId, savedPaths);
                    SaveGisLocation(connection, transaction, EditingTenderId, latitude, longitude,
                        boundaryCoordinatesJson, selectedAreaSquareMeters, boundaryLineJson,
                        boundaryGeometryType, boundaryGeometryJson,
                        boundaryStartLatitude, boundaryStartLongitude, boundaryEndLatitude, boundaryEndLongitude,
                        savedPaths, obsoletePaths);
                    SaveAdministrativeApprovals(connection, transaction, EditingTenderId, savedPaths, obsoletePaths);
                    RecordTenderRevision(connection, transaction, EditingTenderId,
                        currentStatus == "Budget Revision Required" ? "Budget proposal revised" : "Draft updated",
                        currentStatus == "Budget Revision Required"
                            ? "Budget proposal details updated; see the revision snapshot."
                            : "Tender details updated.");

                    if (currentStatus == "Budget Revision Required")
                    {
                        using (SqlCommand history = new SqlCommand(
                            @"INSERT INTO dbo.TenderLifecycleHistory
                                (TenderId,PreviousStatus,NewStatus,ActionName,ActionBy,RoleAtAction,DepartmentId,Remarks)
                              VALUES(@TenderId,@Status,@Status,N'Budget proposal revised',@UserId,@Role,@DepartmentId,@Remarks);",
                            connection, transaction))
                        {
                            ModuleDb.AddParameter(history, "@TenderId", SqlDbType.Int, EditingTenderId);
                            AddText(history, "@Status", SqlDbType.NVarChar, currentStatus, 50);
                            ModuleDb.AddParameter(history, "@UserId", SqlDbType.Int, CurrentUserId);
                            AddText(history, "@Role", SqlDbType.NVarChar, CurrentRole, 50);
                            ModuleDb.AddParameter(history, "@DepartmentId", SqlDbType.Int,
                                CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                            AddText(history, "@Remarks", SqlDbType.NVarChar,
                                "Budget proposal revised; see the tender revision snapshot.", 1000);
                            history.ExecuteNonQuery();
                        }
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId,
                        currentStatus == "Budget Revision Required" ? "ReviseBudgetProposal" : "Update",
                        "Tender", EditingTenderId, null, Request.UserHostAddress);
                    transaction.Commit();
                }

                DeleteSavedFiles(obsoletePaths);
                string destination = submitToWorkflow
                    ? "~/Pages/TenderWorkflow.aspx?id=" + EditingTenderId.ToString(CultureInfo.InvariantCulture)
                    : "~/Pages/TenderDetails.aspx?id=" + EditingTenderId.ToString(CultureInfo.InvariantCulture) +
                      "&saved=1" + (requirementDocumentsChanged ? "&documentsSaved=1" : String.Empty);
                Response.Redirect(destination, false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (SqlException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender update failed.", ex);
                ShowError("The tender could not be updated. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                DeleteSavedFiles(savedPaths);
                ShowError(ex.Message);
            }
            catch (IOException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender document, GIS image, or approval file could not be stored.", ex);
                ShowError("A document, GIS image, or approval file could not be stored. No changes were committed.");
            }
            catch (UnauthorizedAccessException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender file storage access was denied.", ex);
                ShowError("A document, GIS image, or approval file could not be stored because file storage is unavailable.");
            }
        }

        private bool DeleteSavedFiles(List<string> paths)
        {
            if (paths == null) return true;
            bool allDeleted = true;
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    File.Delete(path);
                }
                catch (IOException ex)
                {
                    allDeleted = false;
                    Trace.Warn("DepartmentModule", "A tender form file could not be removed from storage.", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    allDeleted = false;
                    Trace.Warn("DepartmentModule", "Tender form file cleanup was denied.", ex);
                }
            }
            return allDeleted;
        }

        private void ShowError(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }

        private void ShowSuccess(string text)
        {
            Message.Text = "<div class=\"message success\" role=\"status\">" +
                HttpUtility.HtmlEncode(text) + "</div>";
        }

        private sealed class ApprovalInput
        {
            public string TypeName { get; set; }
            public CheckBox Check { get; set; }
            public TextBox ReferenceText { get; set; }
            public TextBox DateText { get; set; }
            public FileUpload Document { get; set; }
            public HyperLink DocumentLink { get; set; }
        }

        private sealed class RequirementDocumentInput
        {
            public string TypeName { get; set; }
            public string FolderName { get; set; }
            public FileUpload Document { get; set; }
            public Panel CurrentPanel { get; set; }
            public Label CurrentFileName { get; set; }
            public HyperLink ViewLink { get; set; }
            public HyperLink DownloadLink { get; set; }
            public HiddenField RemoveFlag { get; set; }
        }

        private sealed class UploadedRequirementDocument
        {
            public HttpPostedFile File { get; set; }
            public string Category { get; set; }
        }

        private sealed class GisLineGeometry
        {
            public string type { get; set; }
            public List<List<decimal>> coordinates { get; set; }
        }

        private sealed class StoredTenderAsset
        {
            public string OriginalName { get; set; }
            public string StoredName { get; set; }
            public string Extension { get; set; }
            public string MimeType { get; set; }
            public string RelativePath { get; set; }
            public string FullPath { get; set; }
            public long SizeBytes { get; set; }
        }
    }
}
