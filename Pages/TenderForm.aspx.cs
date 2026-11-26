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
        protected Button CreateButton, SubmitTenderButton;
        protected Panel DepartmentPanel, FinancialYearMissingPanel,
            ApprovalsWizardPanel, ExistingGisImageActions, DepartmentReadOnlyPanel;
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
        protected Label ScopeOfWorkCurrentFileName, TechnicalRequirementsCurrentFileName,
            FinancialRequirementsCurrentFileName;
        protected HiddenField ScopeOfWorkDocumentRemoveFlag, TechnicalRequirementsDocumentRemoveFlag,
            FinancialRequirementsDocumentRemoveFlag;
        protected Image CurrentGisImagePreview;
        protected HiddenField WizardCurrentStep, WizardCompletedStepsMask, RemoveGisImageFlag,
            JeAdministratorFlag, GisBoundaryCoordinates;
        protected Panel ScheduleValidationPanel;
        protected Literal ScheduleValidationMessage;

        private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
        private static readonly string[] ApprovalTypes =
            { "Administrative", "TechnicalSanction", "GeneralBody", "DPDC" };
        private int EditingTenderId { get { return QueryId("id"); } }

        protected void Page_Load(object sender, EventArgs e)
        {
            bool isEditing = EditingTenderId > 0;
            bool isJe = IsJeAdministrator;
            if (!RequireAnyPermission("system.admin", "tender.create.department", "tenders.manage.all")) return;
            JeAdministratorFlag.Value = isJe ? "true" : "false";

            FormTitle.Text = isEditing ? "Edit Tender" : "New Tender";
            FormHeading.Text = isEditing ? "Edit Tender Requirement" : "New Tender Requirement";
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
            ApprovalsWizardPanel.Visible = isJe;
            SubmitTenderButton.Visible = isJe;

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
                }
                catch (SqlException ex)
                {
                    Trace.Warn("DepartmentModule", "Tender form data could not be loaded.", ex);
                    ShowError("Tender form data could not be loaded. Please retry or contact support.");
                    CreateButton.Enabled = false;
                }
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
                         r.AdditionalConditions,r.TermsAndConditions
                  FROM dbo.Tender t
                  INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                  LEFT JOIN dbo.TenderRequirements r ON r.TenderId=t.TenderId
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
                         AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                    INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                    WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                      AND s.StageName=N'Final_JE_SubmitTender'))
                    );", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit,
                    IsJeAdministrator);
                ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit,
                    !HasPermission("system.admin") &&
                    (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase)));
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
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
                    if (!String.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase) &&
                        !String.Equals(status, "Budget Revision Required", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowError("This tender is not an editable draft or returned budget proposal.");
                        CreateButton.Enabled = false;
                        return;
                    }
                    FormDescription.Text = String.Equals(status, "Budget Revision Required", StringComparison.OrdinalIgnoreCase)
                        ? "Revise the returned estimate and proposal. Submit the updated proposal from tender details when ready."
                        : "This draft is editable before it advances beyond its initial workflow stage.";

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
                    PreBidLocationText.Text = Convert.ToString(reader["PreBidLocation"] == DBNull.Value ? String.Empty : reader["PreBidLocation"]);
                    PreBidDetailsText.Text = Convert.ToString(reader["PreBidDetails"] == DBNull.Value ? String.Empty : reader["PreBidDetails"]);
                    AdditionalConditionsText.Text = Convert.ToString(reader["AdditionalConditions"] == DBNull.Value ? String.Empty : reader["AdditionalConditions"]);
                    TermsAndConditionsText.Text = Convert.ToString(reader["TermsAndConditions"] == DBNull.Value ? String.Empty : reader["TermsAndConditions"]);
                    FormDescription.Text += " Department: " + HttpUtility.HtmlEncode(Convert.ToString(reader["DepartmentName"]));
                    DepartmentReadOnlyText.Text = HttpUtility.HtmlEncode(Convert.ToString(reader["DepartmentName"]));
                }
                LoadRequirementDocuments(connection);
                LoadGisAndAdministrativeApprovals(connection);
            }
        }

        private void LoadRequirementDocuments(SqlConnection connection)
        {
            foreach (RequirementDocumentInput input in GetRequirementDocumentInputs())
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT TOP (1) d.DocumentId,d.FileName
                      FROM dbo.TenderDocuments d
                      LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                      WHERE d.TenderId=@TenderId
                        AND COALESCE(dt.TypeName,d.DocumentType)=@DocumentType
                      ORDER BY d.UploadedDate DESC,d.DocumentId DESC;", connection))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, input.TypeName, 80);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) continue;
                        int documentId = Convert.ToInt32(reader["DocumentId"], CultureInfo.InvariantCulture);
                        string fileName = Path.GetFileName(Convert.ToString(reader["FileName"]));
                        input.CurrentPanel.Visible = true;
                        input.CurrentFileName.Text = HttpUtility.HtmlEncode(fileName);
                        string documentUrl = ResolveUrl("~/Pages/DocumentDownload.aspx?id=" +
                            documentId.ToString(CultureInfo.InvariantCulture));
                        input.ViewLink.NavigateUrl = documentUrl + "&preview=true";
                        input.DownloadLink.NavigateUrl = documentUrl;
                        input.ViewLink.Visible = true;
                        input.DownloadLink.Visible = true;
                    }
                }
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
                         BoundaryCoordinatesJson
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
            foreach (RequirementDocumentInput input in GetRequirementDocumentInputs())
            {
                if (input.Document.HasFile &&
                    !IsValidTenderAssetUpload(input.Document, AllowedExtensions, 20 * 1024 * 1024))
                {
                    error = input.TypeName + " must be a permitted document no larger than 20 MB.";
                    return false;
                }
            }
            if (!IsJeAdministrator) return true;

            foreach (ApprovalInput input in GetApprovalInputs())
            {
                if (input.Document.HasFile &&
                    !IsValidTenderAssetUpload(input.Document, new[] { ".pdf", ".jpg", ".jpeg", ".png" },
                        20 * 1024 * 1024))
                {
                    error = "An approval document must be a valid PDF, JPEG, or PNG no larger than 20 MB.";
                    return false;
                }
            }
            return true;
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
                      WHERE d.TenderId=@TenderId
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
                        @"DELETE d
                          FROM dbo.TenderDocuments d
                          LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=d.DocumentTypeId
                          WHERE d.TenderId=@TenderId
                            AND COALESCE(dt.TypeName,d.DocumentType)=@DocumentType;",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
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
                         FileExtension,FileSizeBytes,UploadedBy)
                      VALUES(@TenderId,@DocumentTypeId,@DocumentType,@DocumentName,NULL,@FileName,@FilePath,
                             @FileExtension,@FileSizeBytes,@UploadedBy);", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@DocumentTypeId", SqlDbType.Int, documentTypeId);
                    AddText(command, "@DocumentType", SqlDbType.NVarChar, input.TypeName, 80);
                    AddText(command, "@DocumentName", SqlDbType.NVarChar, documentName, 250);
                    AddText(command, "@FileName", SqlDbType.NVarChar, document.OriginalName, 260);
                    AddText(command, "@FilePath", SqlDbType.NVarChar, document.RelativePath, 400);
                    AddText(command, "@FileExtension", SqlDbType.NVarChar, document.Extension, 10);
                    ModuleDb.AddParameter(command, "@FileSizeBytes", SqlDbType.BigInt, document.SizeBytes);
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
            decimal? selectedAreaSquareMeters, List<string> savedPaths, List<string> obsoletePaths)
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
                boundaryCoordinatesJson == null && image == null && !removeImage)
                return;

            string query = exists
                ? @"UPDATE dbo.TenderGisLocations
                    SET Latitude=@Latitude,Longitude=@Longitude,LocationName=@LocationName,
                        BoundaryCoordinatesJson=@BoundaryCoordinatesJson,
                        SelectedAreaSquareMeters=@SelectedAreaSquareMeters,
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
                     ImageOriginalFileName,ImageStoredFileName,
                     ImagePath,ImageExtension,ImageSizeBytes,ImageUploadedBy,ImageUploadedAtUtc,ModifiedBy)
                    VALUES(@TenderId,@Latitude,@Longitude,@LocationName,@BoundaryCoordinatesJson,@SelectedAreaSquareMeters,
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
            string originalName = Path.GetFileName(upload.FileName);
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
                RelativePath = "Uploads/" + relativeFolder + "/" + storedName,
                FullPath = fullPath,
                SizeBytes = upload.PostedFile.ContentLength
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

        protected void TitleLength_ServerValidate(object source, ServerValidateEventArgs args)
        {
            args.IsValid = args.Value != null && args.Value.Trim().Length >= 5 && args.Value.Trim().Length <= 250;
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
            DateTime parsedDate;
            DateTime? publicationScheduledUtc = null, submissionStart = null, submissionDeadline = null;
            DateTime? technicalOpeningUtc = null, financialOpeningUtc = null, preBidMeetingUtc = null;
            if (!Decimal.TryParse(AmountText.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) ||
                amount <= 0 || amount > 9999999999999999.99m ||
                (EditingTenderId == 0 && !TryGetAuthorizedDepartmentId(out departmentId)))
            {
                ShowError("Enter a positive estimated value and a valid department.");
                return;
            }
            if ((DescriptionText.Text != null && DescriptionText.Text.Length > 4000) ||
                (AdditionalConditionsText.Text != null && AdditionalConditionsText.Text.Length > 4000) ||
                (TermsAndConditionsText.Text != null && TermsAndConditionsText.Text.Length > 4000))
            {
                ShowError("Description, requirements, and conditions cannot exceed 4,000 characters each.");
                return;
            }
            if ((BudgetInformationText.Text != null && BudgetInformationText.Text.Length > 2000) ||
                (TechnicalSanctionNumberText.Text != null && TechnicalSanctionNumberText.Text.Length > 100) ||
                (TechnicalSanctionAuthorityText.Text != null && TechnicalSanctionAuthorityText.Text.Length > 200) ||
                (PreBidLocationText.Text != null && PreBidLocationText.Text.Length > 300) ||
                (PreBidDetailsText.Text != null && PreBidDetailsText.Text.Length > 2000))
            {
                ShowError("Budget, sanction, and pre-bid details exceed the allowed field lengths.");
                return;
            }
            string boundaryCoordinatesJson;
            decimal? selectedAreaSquareMeters, boundaryLatitude, boundaryLongitude;
            if (LocationNameText.Text.Length > 300 ||
                !TryParseGisBoundary(GisBoundaryCoordinates.Value,
                    out boundaryCoordinatesJson, out selectedAreaSquareMeters,
                    out boundaryLatitude, out boundaryLongitude))
            {
                ShowError("Draw a valid site boundary with at least three non-overlapping points.");
                return;
            }
            if (boundaryCoordinatesJson != null)
            {
                latitude = boundaryLatitude;
                longitude = boundaryLongitude;
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
            bool hasPreBidMeeting = !String.IsNullOrWhiteSpace(PreBidMeetingText.Text);
            if ((hasSanctionDate && !TryParseDate(TechnicalSanctionDateText.Text, out sanctionDate)) ||
                (hasPreBidMeeting && !TryParseIndiaLocalDate(PreBidMeetingText.Text, out parsedDate)))
            {
                ShowError("Enter valid technical-sanction and pre-bid dates.");
                return;
            }
            if (hasPreBidMeeting)
            {
                TryParseIndiaLocalDate(PreBidMeetingText.Text, out parsedDate);
                preBidMeetingUtc = parsedDate;
            }
            bool hasPublicationDate = !String.IsNullOrWhiteSpace(PublicationDateText.Text);
            bool hasSubmissionStart = !String.IsNullOrWhiteSpace(StartText.Text);
            bool hasSubmissionDeadline = !String.IsNullOrWhiteSpace(DeadlineText.Text);
            bool hasTechnicalBidOpening = !String.IsNullOrWhiteSpace(TechnicalBidOpeningText.Text);
            bool hasFinancialBidOpening = !String.IsNullOrWhiteSpace(FinancialBidOpeningText.Text);
            if ((hasPublicationDate && !TryParseIndiaLocalDate(PublicationDateText.Text, out parsedDate)) ||
                (hasSubmissionStart && !TryParseIndiaLocalDate(StartText.Text, out parsedDate)) ||
                (hasSubmissionDeadline && !TryParseIndiaLocalDate(DeadlineText.Text, out parsedDate, true)) ||
                (hasTechnicalBidOpening && !TryParseIndiaLocalDate(TechnicalBidOpeningText.Text, out parsedDate)) ||
                (hasFinancialBidOpening && !TryParseIndiaLocalDate(FinancialBidOpeningText.Text, out parsedDate)))
            {
                ShowError("Enter valid publication, submission, and bid-opening dates.");
                return;
            }
            if (hasPublicationDate)
            {
                TryParseIndiaLocalDate(PublicationDateText.Text, out parsedDate);
                publicationScheduledUtc = parsedDate;
            }
            if (hasSubmissionStart)
            {
                TryParseIndiaLocalDate(StartText.Text, out parsedDate);
                submissionStart = parsedDate;
            }
            if (hasSubmissionDeadline)
            {
                TryParseIndiaLocalDate(DeadlineText.Text, out parsedDate, true);
                submissionDeadline = parsedDate;
            }
            if (hasTechnicalBidOpening)
            {
                TryParseIndiaLocalDate(TechnicalBidOpeningText.Text, out parsedDate);
                technicalOpeningUtc = parsedDate;
            }
            if (hasFinancialBidOpening)
            {
                TryParseIndiaLocalDate(FinancialBidOpeningText.Text, out parsedDate);
                financialOpeningUtc = parsedDate;
            }
            if (hasSubmissionStart != hasSubmissionDeadline)
            {
                ShowError("Enter both the submission start date and submission closing date.");
                return;
            }
            ScheduleValidationPanel.Visible = false;
            ScheduleValidationMessage.Text = String.Empty;
            string scheduleError = ValidateTenderSchedule(publicationScheduledUtc, submissionStart,
                submissionDeadline, technicalOpeningUtc, financialOpeningUtc, preBidMeetingUtc,
                DateTime.UtcNow, EditingTenderId == 0);
            if (!String.IsNullOrEmpty(scheduleError))
            {
                ScheduleValidationPanel.Visible = true;
                ScheduleValidationMessage.Text = HttpUtility.HtmlEncode(scheduleError);
                WizardCurrentStep.Value = "2";
                ShowError(scheduleError);
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

            if (EditingTenderId > 0)
            {
                UpdateDraftTender(amount,
                    publicationScheduledUtc, submissionStart, submissionDeadline,
                    technicalOpeningUtc, financialOpeningUtc,
                    hasSanctionDate ? (DateTime?)sanctionDate.Date : null, preBidMeetingUtc,
                    latitude, longitude, boundaryCoordinatesJson, selectedAreaSquareMeters, submitToWorkflow);
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
                             PublicationScheduledUtc,FinancialBidOpeningScheduledUtc,
                             CurrentStatus,SubmissionStartDate,SubmissionDeadline,CreatedBy,WorkflowVersion,
                             BudgetInformation,TechnicalSanctionNumber,TechnicalSanctionAuthority,TechnicalSanctionDate,
                             PreBidMeetingUtc,PreBidLocation,PreBidDetails,TechnicalBidOpeningScheduledUtc,
                             WizardCurrentStep,WizardCompletedStepsMask)
                          VALUES
                            (@TenderNumber,@TrackingId,@Title,@Description,@DepartmentId,@Amount,@TenderMethod,@TenderMethod,
                             @TenderCategory,@TenderCategoryId,@FinancialYear,
                             @PublicationScheduledUtc,@FinancialBidOpeningUtc,
                             N'Draft',@StartDate,@Deadline,@CreatedBy,2,@BudgetInformation,@SanctionNumber,
                             @SanctionAuthority,@SanctionDate,@PreBidMeetingUtc,@PreBidLocation,@PreBidDetails,
                             @TechnicalBidOpeningUtc,5,@WizardCompletedStepsMask);
                          SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                    {
                        AddText(command, "@TenderNumber", SqlDbType.NVarChar, tenderNumber, 50);
                        AddText(command, "@TrackingId", SqlDbType.NVarChar, Guid.NewGuid().ToString(), 100);
                        AddText(command, "@Title", SqlDbType.NVarChar, TitleText.Text.Trim(), 250);
                        AddText(command, "@Description", SqlDbType.NVarChar, DescriptionText.Text, -1);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, amount);
                        AddText(command, "@TenderMethod", SqlDbType.NVarChar, TenderTypeSelect.SelectedValue, 50);
                        AddText(command, "@TenderCategory", SqlDbType.NVarChar, TenderCategorySelect.SelectedValue, 50);
                        ModuleDb.AddParameter(command, "@TenderCategoryId", SqlDbType.Int, categoryId);
                        AddText(command, "@FinancialYear", SqlDbType.NVarChar, FinancialYearSelect.SelectedValue, 9);
                        ModuleDb.AddParameter(command, "@PublicationScheduledUtc", SqlDbType.DateTime2,
                            publicationScheduledUtc.HasValue ? (object)publicationScheduledUtc.Value : null);
                        ModuleDb.AddParameter(command, "@FinancialBidOpeningUtc", SqlDbType.DateTime2,
                            financialOpeningUtc.HasValue ? (object)financialOpeningUtc.Value : null);
                        ModuleDb.AddParameter(command, "@StartDate", SqlDbType.DateTime2,
                            submissionStart.HasValue ? (object)submissionStart.Value : null);
                        ModuleDb.AddParameter(command, "@Deadline", SqlDbType.DateTime2, submissionDeadline.HasValue ? (object)submissionDeadline.Value : null);
                        ModuleDb.AddParameter(command, "@CreatedBy", SqlDbType.Int, CurrentUserId);
                        AddText(command, "@BudgetInformation", SqlDbType.NVarChar, BudgetInformationText.Text, 2000);
                        AddText(command, "@SanctionNumber", SqlDbType.NVarChar, TechnicalSanctionNumberText.Text, 100);
                        AddText(command, "@SanctionAuthority", SqlDbType.NVarChar, TechnicalSanctionAuthorityText.Text, 200);
                        ModuleDb.AddParameter(command, "@SanctionDate", SqlDbType.Date,
                            hasSanctionDate ? (object)sanctionDate.Date : null);
                        ModuleDb.AddParameter(command, "@PreBidMeetingUtc", SqlDbType.DateTime2,
                            hasPreBidMeeting ? (object)preBidMeetingUtc : null);
                        AddText(command, "@PreBidLocation", SqlDbType.NVarChar, PreBidLocationText.Text, 300);
                        AddText(command, "@PreBidDetails", SqlDbType.NVarChar, PreBidDetailsText.Text, 2000);
                        ModuleDb.AddParameter(command, "@TechnicalBidOpeningUtc", SqlDbType.DateTime2,
                            technicalOpeningUtc.HasValue ? (object)technicalOpeningUtc.Value : null);
                        ModuleDb.AddParameter(command, "@WizardCompletedStepsMask", SqlDbType.TinyInt,
                            GetWizardCompletedStepsMask());
                        tenderId = Convert.ToInt32(command.ExecuteScalar());
                    }

                    SaveTenderFormConditions(connection, transaction, tenderId);
                    requirementDocumentsChanged = SaveRequirementDocuments(connection, transaction,
                        tenderId, savedPaths, obsoletePaths);
                    SaveGisLocation(connection, transaction, tenderId, latitude, longitude,
                        boundaryCoordinatesJson, selectedAreaSquareMeters, savedPaths, obsoletePaths);
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
                CultureInfo.InvariantCulture, out mask) ? (byte)(mask & 31) : (byte)0;
        }

        private static int NormalizeWizardCurrentStep(object value)
        {
            int step;
            if (value == null || value == DBNull.Value ||
                !Int32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out step))
                return 1;
            return Math.Max(1, Math.Min(5, step));
        }

        private static byte NormalizeWizardCompletedStepsMask(object value)
        {
            byte mask;
            if (value == null || value == DBNull.Value ||
                !Byte.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out mask))
                return 0;
            return (byte)(mask & 31);
        }

        private static bool TryParseDate(string value, out DateTime result)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result);
        }

        private static bool TryParseIndiaLocalDate(string value, out DateTime utc, bool useEndOfDay = false)
        {
            utc = DateTime.MinValue;
            DateTime localDate;
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out localDate))
                return false;
            TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime localDateTime = useEndOfDay ? localDate.Date.AddDays(1).AddTicks(-1) : localDate.Date;
            utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), indiaTimeZone);
            return true;
        }

        private static DateTime GetIndiaCalendarDate(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")).Date;
        }

        private static string ValidateTenderSchedule(DateTime? publication, DateTime? submissionStart,
            DateTime? submissionClosing, DateTime? technicalOpening, DateTime? financialOpening,
            DateTime? preBidMeeting, DateTime utcNow, bool isNewTender)
        {
            List<string> errors = new List<string>();
            if (isNewTender && publication.HasValue)
            {
                TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                DateTime indiaToday = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), indiaTimeZone).Date;
                DateTime indiaTodayUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(indiaToday, DateTimeKind.Unspecified), indiaTimeZone);
                if (publication.Value < indiaTodayUtc)
                    errors.Add("Set the publication date to today or a future date (IST).");
            }
            if (publication.HasValue && submissionStart.HasValue &&
                publication.Value >= submissionStart.Value)
                errors.Add("Set the publication date before the submission start date.");
            if (submissionStart.HasValue && submissionClosing.HasValue &&
                GetIndiaCalendarDate(submissionStart.Value) >= GetIndiaCalendarDate(submissionClosing.Value))
                errors.Add("Set the submission closing date later than the submission start date.");
            if (technicalOpening.HasValue &&
                (!submissionClosing.HasValue || technicalOpening.Value <= submissionClosing.Value))
                errors.Add("Set the technical bid opening date after the submission closing date.");
            if (financialOpening.HasValue &&
                (!technicalOpening.HasValue || financialOpening.Value <= technicalOpening.Value))
                errors.Add("Set the financial bid opening date after the technical bid opening date.");
            if (preBidMeeting.HasValue && submissionClosing.HasValue &&
                GetIndiaCalendarDate(preBidMeeting.Value) >= GetIndiaCalendarDate(submissionClosing.Value))
                errors.Add("Set the pre-bid meeting date before the submission closing date.");
            if (preBidMeeting.HasValue && publication.HasValue &&
                preBidMeeting.Value < publication.Value)
                errors.Add("Set the pre-bid meeting date on or after the publication date.");
            return errors.Count == 0 ? null : String.Join(" ", errors);
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
            DateTime? publicationScheduledUtc, DateTime? submissionStart, DateTime? submissionDeadline,
            DateTime? technicalOpeningUtc, DateTime? financialOpeningUtc,
            DateTime? sanctionDate, DateTime? preBidMeetingUtc,
            decimal? latitude, decimal? longitude, string boundaryCoordinatesJson,
            decimal? selectedAreaSquareMeters, bool submitToWorkflow)
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
                              PublicationScheduledUtc=@PublicationScheduledUtc,
                              TechnicalBidOpeningScheduledUtc=@TechnicalBidOpeningUtc,
                              FinancialBidOpeningScheduledUtc=@FinancialBidOpeningUtc,
                              BudgetInformation=@BudgetInformation,TechnicalSanctionNumber=@SanctionNumber,
                              TechnicalSanctionAuthority=@SanctionAuthority,TechnicalSanctionDate=@SanctionDate,
                              PreBidMeetingUtc=@PreBidMeetingUtc,PreBidLocation=@PreBidLocation,
                              PreBidDetails=@PreBidDetails,
                              SubmissionStartDate=@StartDate,SubmissionDeadline=@Deadline,
                              WizardCurrentStep=5,WizardCompletedStepsMask=@WizardCompletedStepsMask,
                              ModifiedBy=@ModifiedBy,ModifiedDate=SYSUTCDATETIME()
                          FROM dbo.Tender t
                          WHERE t.TenderId=@TenderId AND t.CurrentStatus=@ExpectedStatus AND t.IsDeleted=0
                            AND
                            (
                                (t.WorkflowVersion=1 AND (@IsAdministrator=1 OR
                                 (@IsDepartmentOfficer=1 AND t.CreatedBy=@UserId)))
                                OR
                                (t.WorkflowVersion=2 AND @IsAdministrator=1 AND t.CurrentStatus=N'Draft'
                                 AND EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                            INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                            WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                              AND s.StageName=N'Final_JE_SubmitTender'))
                            );", connection, transaction))
                    {
                        AddText(command, "@Title", SqlDbType.NVarChar, TitleText.Text.Trim(), 250);
                        AddText(command, "@Description", SqlDbType.NVarChar, DescriptionText.Text, -1);
                        ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, amount);
                        AddText(command, "@TenderMethod", SqlDbType.NVarChar, TenderTypeSelect.SelectedValue, 50);
                        AddText(command, "@TenderCategory", SqlDbType.NVarChar, TenderCategorySelect.SelectedValue, 50);
                        ModuleDb.AddParameter(command, "@TenderCategoryId", SqlDbType.Int, categoryId);
                        AddText(command, "@FinancialYear", SqlDbType.NVarChar, FinancialYearSelect.SelectedValue, 9);
                        ModuleDb.AddParameter(command, "@PublicationScheduledUtc", SqlDbType.DateTime2,
                            publicationScheduledUtc.HasValue ? (object)publicationScheduledUtc.Value : null);
                        ModuleDb.AddParameter(command, "@TechnicalBidOpeningUtc", SqlDbType.DateTime2,
                            technicalOpeningUtc.HasValue ? (object)technicalOpeningUtc.Value : null);
                        ModuleDb.AddParameter(command, "@FinancialBidOpeningUtc", SqlDbType.DateTime2,
                            financialOpeningUtc.HasValue ? (object)financialOpeningUtc.Value : null);
                        AddText(command, "@BudgetInformation", SqlDbType.NVarChar, BudgetInformationText.Text, 2000);
                        AddText(command, "@SanctionNumber", SqlDbType.NVarChar, TechnicalSanctionNumberText.Text, 100);
                        AddText(command, "@SanctionAuthority", SqlDbType.NVarChar, TechnicalSanctionAuthorityText.Text, 200);
                        ModuleDb.AddParameter(command, "@SanctionDate", SqlDbType.Date, sanctionDate.HasValue ? (object)sanctionDate.Value : null);
                        ModuleDb.AddParameter(command, "@PreBidMeetingUtc", SqlDbType.DateTime2,
                            preBidMeetingUtc.HasValue ? (object)preBidMeetingUtc.Value : null);
                        AddText(command, "@PreBidLocation", SqlDbType.NVarChar, PreBidLocationText.Text, 300);
                        AddText(command, "@PreBidDetails", SqlDbType.NVarChar, PreBidDetailsText.Text, 2000);
                        ModuleDb.AddParameter(command, "@StartDate", SqlDbType.DateTime2,
                            submissionStart.HasValue ? (object)submissionStart.Value : null);
                        ModuleDb.AddParameter(command, "@Deadline", SqlDbType.DateTime2,
                            submissionDeadline.HasValue ? (object)submissionDeadline.Value : null);
                        ModuleDb.AddParameter(command, "@WizardCompletedStepsMask", SqlDbType.TinyInt,
                            GetWizardCompletedStepsMask());
                        ModuleDb.AddParameter(command, "@ModifiedBy", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        AddText(command, "@ExpectedStatus", SqlDbType.NVarChar, currentStatus, 50);
                        ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit, isAdministrator);
                        ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit, isDepartmentOfficer);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Tender status changed; refresh the page before editing.");
                    }

                    SaveTenderFormConditions(connection, transaction, EditingTenderId);
                    requirementDocumentsChanged = SaveRequirementDocuments(connection, transaction,
                        EditingTenderId, savedPaths, obsoletePaths);
                    SaveGisLocation(connection, transaction, EditingTenderId, latitude, longitude,
                        boundaryCoordinatesJson, selectedAreaSquareMeters, savedPaths, obsoletePaths);
                    SaveAdministrativeApprovals(connection, transaction, EditingTenderId, savedPaths, obsoletePaths);
                    RecordTenderRevision(connection, transaction, EditingTenderId,
                        currentStatus == "Budget Revision Required" ? "Budget proposal revised" : "Draft updated",
                        DescriptionText.Text);

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
                            AddText(history, "@Remarks", SqlDbType.NVarChar, DescriptionText.Text, 1000);
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

        private void DeleteSavedFiles(List<string> paths)
        {
            if (paths == null) return;
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    File.Delete(path);
                }
                catch (IOException ex)
                {
                    Trace.Warn("DepartmentModule", "A tender form file could not be removed from storage.", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Trace.Warn("DepartmentModule", "Tender form file cleanup was denied.", ex);
                }
            }
        }

        private void ShowError(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
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

        private sealed class StoredTenderAsset
        {
            public string OriginalName { get; set; }
            public string StoredName { get; set; }
            public string Extension { get; set; }
            public string RelativePath { get; set; }
            public string FullPath { get; set; }
            public long SizeBytes { get; set; }
        }
    }
}
