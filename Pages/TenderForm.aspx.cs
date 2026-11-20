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
    public partial class TenderForm : ModulePage
    {
        protected Literal Message;
        protected Literal FormTitle, FormHeading, FormDescription;
        protected TextBox TitleText;
        protected TextBox DescriptionText, EligibilityCriteriaText;
        protected TextBox BudgetInformationText, TechnicalSanctionNumberText, TechnicalSanctionAuthorityText,
            TechnicalSanctionDateText, PreBidMeetingText, PreBidLocationText, PreBidDetailsText,
            TechnicalBidOpeningText, PublicationDateText, FinancialBidOpeningText,
            EMDAmountText, DocumentFeeText, ProcessingFeeText, SecurityDepositText,
            ScopeOfWorkText, TechnicalRequirementsText, FinancialRequirementsText,
            AdditionalConditionsText, TermsAndConditionsText;
        protected DropDownList DepartmentSelect;
        protected DropDownList TenderTypeSelect;
        protected DropDownList TenderCategorySelect, TenderSubcategorySelect, FinancialYearSelect;
        protected DropDownList DocumentTypeSelect;
        protected TextBox AmountText;
        protected TextBox StartText, DeadlineText;
        protected FileUpload Attachments;
        protected Button CreateButton;
        protected Panel DepartmentPanel, AttachmentPanel, FinancialYearMissingPanel, DocumentTypesMissingPanel;
        protected RequiredFieldValidator DepartmentRequired, FinancialYearRequired;

        private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" };
        private int EditingTenderId { get { return QueryId("id"); } }

        protected void Page_Load(object sender, EventArgs e)
        {
            bool isEditing = EditingTenderId > 0;
            bool isJe = IsJeAdministrator ||
                String.Equals(CurrentRole, "Junior Engineer (JE)", StringComparison.OrdinalIgnoreCase);
            if (!RequireAnyPermission("system.admin", "tender.create.department", "tenders.manage.all")) return;

            FormTitle.Text = isEditing ? "Edit Tender" : "New Tender";
            FormHeading.Text = isEditing ? "Edit Tender Requirement" : "New Tender Requirement";
            FormDescription.Text = isEditing
                ? "Only eligible drafts and returned budget proposals can be edited. Later workflow stages remain protected."
                : "Complete the tender details and save a draft for department processing.";
            CreateButton.Text = isEditing ? "Save Changes" : "Save as Draft";
            DepartmentPanel.Visible = !isEditing && (!isJe || HasPermission("system.admin"));
            DepartmentRequired.Enabled = DepartmentPanel.Visible;
            AttachmentPanel.Visible = !isEditing;

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

                        BindTenderCatalog(connection, TenderTypeSelect, "SELECT MethodName FROM dbo.TenderMethods WHERE IsActive=1 ORDER BY SortOrder,MethodName;", "Select tender method");
                        BindTenderCatalog(connection, TenderCategorySelect, "SELECT TypeName FROM dbo.TenderTypes WHERE IsActive=1 ORDER BY SortOrder,TypeName;", "Select tender category");
                        BindTenderCatalog(connection, FinancialYearSelect, "SELECT FinancialYear FROM dbo.FinancialYears WHERE IsActive=1 ORDER BY StartDate DESC;", "Select financial year");
                        BindTenderSubcategories(connection, null);
                        int documentTypeCount = BindTenderCatalog(connection, DocumentTypeSelect,
                            "SELECT TypeName FROM dbo.DocumentTypes WHERE IsActive=1 ORDER BY SortOrder,TypeName;", "Select attachment type");
                        DocumentTypesMissingPanel.Visible = documentTypeCount == 0;
                        Attachments.Enabled = documentTypeCount > 0;
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
            DocumentTypesMissingPanel.Visible = DocumentTypeSelect.Items.Count <= 1;
            Attachments.Enabled = DocumentTypeSelect.Items.Count > 1;
            if (FinancialYearMissingPanel.Visible) CreateButton.Enabled = false;
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

        private void BindTenderSubcategories(SqlConnection connection, string category)
        {
            TenderSubcategorySelect.Items.Clear();
            TenderSubcategorySelect.Items.Add(new ListItem("No subcategory", ""));
            if (String.IsNullOrWhiteSpace(category)) return;
            using (SqlCommand command = new SqlCommand(
                @"SELECT SubcategoryId,SubcategoryName FROM dbo.TenderSubcategories
                  WHERE TenderCategory=@Category AND IsActive=1 ORDER BY SortOrder,SubcategoryName;", connection))
            {
                AddText(command, "@Category", SqlDbType.NVarChar, category, 100);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        TenderSubcategorySelect.Items.Add(new ListItem(
                            Convert.ToString(reader["SubcategoryName"], CultureInfo.InvariantCulture),
                            Convert.ToInt32(reader["SubcategoryId"], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        protected void TenderCategorySelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                    BindTenderSubcategories(connection, TenderCategorySelect.SelectedValue);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender subcategories could not be loaded.", ex);
                ShowError("Tender subcategories could not be loaded. Please retry or contact support.");
            }
        }

        private void LoadTenderForEdit()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.Title,t.Description,t.EligibilityCriteria,t.EstimatedAmount,t.TenderType,t.TenderCategory,
                         t.SubmissionStartDate,t.SubmissionDeadline,t.CurrentStatus,t.FinancialYear,t.TenderSubcategoryId,
                         t.EMDAmount,t.TenderDocumentFee,t.ProcessingFee,t.SecurityDepositAmount,
                         t.PublicationScheduledUtc,t.TechnicalBidOpeningScheduledUtc,t.FinancialBidOpeningScheduledUtc,
                         t.BudgetInformation,t.TechnicalSanctionNumber,t.TechnicalSanctionAuthority,t.TechnicalSanctionDate,
                         t.PreBidMeetingUtc,t.PreBidLocation,t.PreBidDetails,
                         r.ScopeOfWork,r.TechnicalRequirements,r.FinancialRequirements,r.AdditionalConditions,r.TermsAndConditions
                  FROM dbo.Tender t
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
                    HasPermission("system.admin") || HasAnyRole("Admin"));
                ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit,
                    !HasPermission("system.admin") &&
                    (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase)));
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                int? tenderSubcategoryId = null;
                string tenderCategory = String.Empty;
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
                    EligibilityCriteriaText.Text = Convert.ToString(reader["EligibilityCriteria"] == DBNull.Value ? String.Empty : reader["EligibilityCriteria"]);
                    AmountText.Text = Convert.ToDecimal(reader["EstimatedAmount"]).ToString(CultureInfo.InvariantCulture);
                    string tenderType = Convert.ToString(reader["TenderType"]);
                    if (TenderTypeSelect.Items.FindByValue(tenderType) == null)
                        TenderTypeSelect.Items.Add(new ListItem(tenderType, tenderType));
                    TenderTypeSelect.SelectedValue = tenderType;
                    tenderCategory = Convert.ToString(reader["TenderCategory"] == DBNull.Value ? String.Empty : reader["TenderCategory"]);
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
                    if (reader["TenderSubcategoryId"] != DBNull.Value)
                        tenderSubcategoryId = Convert.ToInt32(reader["TenderSubcategoryId"], CultureInfo.InvariantCulture);
                    EMDAmountText.Text = FormatOptionalAmount(reader["EMDAmount"]);
                    DocumentFeeText.Text = FormatOptionalAmount(reader["TenderDocumentFee"]);
                    ProcessingFeeText.Text = FormatOptionalAmount(reader["ProcessingFee"]);
                    SecurityDepositText.Text = FormatOptionalAmount(reader["SecurityDepositAmount"]);
                    PublicationDateText.Text = FormatDateTimeLocal(reader["PublicationScheduledUtc"]);
                    StartText.Text = FormatDateTimeLocal(reader["SubmissionStartDate"]);
                    DeadlineText.Text = FormatDateTimeLocal(reader["SubmissionDeadline"]);
                    TechnicalBidOpeningText.Text = FormatDateTimeLocal(reader["TechnicalBidOpeningScheduledUtc"]);
                    FinancialBidOpeningText.Text = FormatDateTimeLocal(reader["FinancialBidOpeningScheduledUtc"]);
                    BudgetInformationText.Text = Convert.ToString(reader["BudgetInformation"] == DBNull.Value ? String.Empty : reader["BudgetInformation"]);
                    TechnicalSanctionNumberText.Text = Convert.ToString(reader["TechnicalSanctionNumber"] == DBNull.Value ? String.Empty : reader["TechnicalSanctionNumber"]);
                    TechnicalSanctionAuthorityText.Text = Convert.ToString(reader["TechnicalSanctionAuthority"] == DBNull.Value ? String.Empty : reader["TechnicalSanctionAuthority"]);
                    TechnicalSanctionDateText.Text = reader["TechnicalSanctionDate"] == DBNull.Value
                        ? String.Empty
                        : Convert.ToDateTime(reader["TechnicalSanctionDate"], CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    PreBidMeetingText.Text = FormatDateTimeLocal(reader["PreBidMeetingUtc"]);
                    PreBidLocationText.Text = Convert.ToString(reader["PreBidLocation"] == DBNull.Value ? String.Empty : reader["PreBidLocation"]);
                    PreBidDetailsText.Text = Convert.ToString(reader["PreBidDetails"] == DBNull.Value ? String.Empty : reader["PreBidDetails"]);
                    ScopeOfWorkText.Text = Convert.ToString(reader["ScopeOfWork"] == DBNull.Value ? String.Empty : reader["ScopeOfWork"]);
                    TechnicalRequirementsText.Text = Convert.ToString(reader["TechnicalRequirements"] == DBNull.Value ? String.Empty : reader["TechnicalRequirements"]);
                    FinancialRequirementsText.Text = Convert.ToString(reader["FinancialRequirements"] == DBNull.Value ? String.Empty : reader["FinancialRequirements"]);
                    AdditionalConditionsText.Text = Convert.ToString(reader["AdditionalConditions"] == DBNull.Value ? String.Empty : reader["AdditionalConditions"]);
                    TermsAndConditionsText.Text = Convert.ToString(reader["TermsAndConditions"] == DBNull.Value ? String.Empty : reader["TermsAndConditions"]);
                }
                BindTenderSubcategories(connection, tenderCategory);
                if (tenderSubcategoryId.HasValue)
                {
                    if (TenderSubcategorySelect.Items.FindByValue(tenderSubcategoryId.Value.ToString(CultureInfo.InvariantCulture)) == null)
                    {
                        using (SqlCommand subcategory = new SqlCommand(
                            "SELECT SubcategoryName FROM dbo.TenderSubcategories WHERE SubcategoryId=@Id AND TenderCategory=@Category;", connection))
                        {
                            ModuleDb.AddParameter(subcategory, "@Id", SqlDbType.Int, tenderSubcategoryId.Value);
                            AddText(subcategory, "@Category", SqlDbType.NVarChar, tenderCategory, 100);
                            object name = subcategory.ExecuteScalar();
                            if (name != null && name != DBNull.Value)
                                TenderSubcategorySelect.Items.Add(new ListItem(Convert.ToString(name), tenderSubcategoryId.Value.ToString(CultureInfo.InvariantCulture)));
                        }
                    }
                    TenderSubcategorySelect.SelectedValue = tenderSubcategoryId.Value.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        private static string FormatOptionalAmount(object value)
        {
            return value == null || value == DBNull.Value
                ? String.Empty
                : Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDateTimeLocal(object value)
        {
            if (value == null || value == DBNull.Value) return String.Empty;
            DateTime utc = DateTime.SpecifyKind(Convert.ToDateTime(value, CultureInfo.InvariantCulture), DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"))
                .ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
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

            decimal amount;
            decimal? emdAmount, documentFee, processingFee, securityDeposit;
            int departmentId = 0;
            int? subcategoryId = null;
            DateTime parsedDate;
            DateTime? publicationScheduledUtc = null, submissionStart = null, submissionDeadline = null;
            DateTime? technicalOpeningUtc = null, financialOpeningUtc = null, preBidMeetingUtc = null;
            if (!Decimal.TryParse(AmountText.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) ||
                amount <= 0 || amount > 9999999999999999.99m ||
                !TryParseOptionalAmount(EMDAmountText.Text, out emdAmount) ||
                !TryParseOptionalAmount(DocumentFeeText.Text, out documentFee) ||
                !TryParseOptionalAmount(ProcessingFeeText.Text, out processingFee) ||
                !TryParseOptionalAmount(SecurityDepositText.Text, out securityDeposit) ||
                (EditingTenderId == 0 && !TryGetAuthorizedDepartmentId(out departmentId)))
            {
                ShowError("Enter a positive estimated value, non-negative fees, and a valid department.");
                return;
            }
            if ((DescriptionText.Text != null && DescriptionText.Text.Length > 4000) ||
                (EligibilityCriteriaText.Text != null && EligibilityCriteriaText.Text.Length > 4000) ||
                (ScopeOfWorkText.Text != null && ScopeOfWorkText.Text.Length > 4000) ||
                (TechnicalRequirementsText.Text != null && TechnicalRequirementsText.Text.Length > 4000) ||
                (FinancialRequirementsText.Text != null && FinancialRequirementsText.Text.Length > 4000) ||
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
            DateTime sanctionDate = DateTime.MinValue;
            bool hasSanctionDate = !String.IsNullOrWhiteSpace(TechnicalSanctionDateText.Text);
            bool hasPreBidMeeting = !String.IsNullOrWhiteSpace(PreBidMeetingText.Text);
            if ((hasSanctionDate && !TryParseDate(TechnicalSanctionDateText.Text, out sanctionDate)) ||
                (hasPreBidMeeting && !TryParseIndiaLocalDateTime(PreBidMeetingText.Text, out parsedDate)))
            {
                ShowError("Enter valid technical-sanction and pre-bid dates.");
                return;
            }
            if (hasPreBidMeeting)
            {
                TryParseIndiaLocalDateTime(PreBidMeetingText.Text, out parsedDate);
                preBidMeetingUtc = parsedDate;
            }
            bool hasPublicationDate = !String.IsNullOrWhiteSpace(PublicationDateText.Text);
            bool hasSubmissionStart = !String.IsNullOrWhiteSpace(StartText.Text);
            bool hasSubmissionDeadline = !String.IsNullOrWhiteSpace(DeadlineText.Text);
            bool hasTechnicalBidOpening = !String.IsNullOrWhiteSpace(TechnicalBidOpeningText.Text);
            bool hasFinancialBidOpening = !String.IsNullOrWhiteSpace(FinancialBidOpeningText.Text);
            if ((hasPublicationDate && !TryParseIndiaLocalDateTime(PublicationDateText.Text, out parsedDate)) ||
                (hasSubmissionStart && !TryParseIndiaLocalDateTime(StartText.Text, out parsedDate)) ||
                (hasSubmissionDeadline && !TryParseIndiaLocalDateTime(DeadlineText.Text, out parsedDate)) ||
                (hasTechnicalBidOpening && !TryParseIndiaLocalDateTime(TechnicalBidOpeningText.Text, out parsedDate)) ||
                (hasFinancialBidOpening && !TryParseIndiaLocalDateTime(FinancialBidOpeningText.Text, out parsedDate)))
            {
                ShowError("Enter valid publication, submission, and bid-opening dates and times.");
                return;
            }
            if (hasPublicationDate)
            {
                TryParseIndiaLocalDateTime(PublicationDateText.Text, out parsedDate);
                publicationScheduledUtc = parsedDate;
            }
            if (hasSubmissionStart)
            {
                TryParseIndiaLocalDateTime(StartText.Text, out parsedDate);
                submissionStart = parsedDate;
            }
            if (hasSubmissionDeadline)
            {
                TryParseIndiaLocalDateTime(DeadlineText.Text, out parsedDate);
                submissionDeadline = parsedDate;
            }
            if (hasTechnicalBidOpening)
            {
                TryParseIndiaLocalDateTime(TechnicalBidOpeningText.Text, out parsedDate);
                technicalOpeningUtc = parsedDate;
            }
            if (hasFinancialBidOpening)
            {
                TryParseIndiaLocalDateTime(FinancialBidOpeningText.Text, out parsedDate);
                financialOpeningUtc = parsedDate;
            }
            if (hasSubmissionStart != hasSubmissionDeadline ||
                (submissionStart.HasValue && submissionStart.Value >= submissionDeadline.Value))
            {
                ShowError("Enter both submission dates, and make the closing time later than the submission start.");
                return;
            }
            if ((publicationScheduledUtc.HasValue &&
                 (publicationScheduledUtc.Value <= DateTime.UtcNow ||
                  (submissionStart.HasValue && publicationScheduledUtc.Value > submissionStart.Value))) ||
                (technicalOpeningUtc.HasValue &&
                 (!submissionDeadline.HasValue || technicalOpeningUtc.Value <= submissionDeadline.Value)) ||
                (financialOpeningUtc.HasValue &&
                 (!technicalOpeningUtc.HasValue || financialOpeningUtc.Value <= technicalOpeningUtc.Value)) ||
                (preBidMeetingUtc.HasValue &&
                 (submissionDeadline.HasValue && preBidMeetingUtc.Value >= submissionDeadline.Value ||
                  publicationScheduledUtc.HasValue && preBidMeetingUtc.Value < publicationScheduledUtc.Value)))
            {
                ShowError("The schedule is inconsistent: publication must precede submission, technical opening must follow closing, and financial opening must follow technical opening.");
                return;
            }
            if (!String.IsNullOrWhiteSpace(TenderSubcategorySelect.SelectedValue))
            {
                int parsedSubcategoryId;
                if (!Int32.TryParse(TenderSubcategorySelect.SelectedValue, NumberStyles.None, CultureInfo.InvariantCulture, out parsedSubcategoryId) ||
                    parsedSubcategoryId <= 0)
                {
                    ShowError("Select a valid tender subcategory.");
                    return;
                }
                subcategoryId = parsedSubcategoryId;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    if (!IsActiveCatalogValue(connection, "dbo.TenderMethods", "MethodName", TenderTypeSelect.SelectedValue) ||
                        !IsActiveCatalogValue(connection, "dbo.TenderTypes", "TypeName", TenderCategorySelect.SelectedValue) ||
                        !IsActiveFinancialYear(connection, FinancialYearSelect.SelectedValue) ||
                        (subcategoryId.HasValue && !IsActiveSubcategory(connection, subcategoryId.Value, TenderCategorySelect.SelectedValue)))
                    {
                        ShowError("Select active tender method, category, subcategory, and financial-year values from the master data.");
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

            if (EditingTenderId > 0)
            {
                UpdateDraftTender(amount, emdAmount, documentFee, processingFee, securityDeposit,
                    subcategoryId, publicationScheduledUtc, submissionStart, submissionDeadline,
                    technicalOpeningUtc, financialOpeningUtc,
                    hasSanctionDate ? (DateTime?)sanctionDate.Date : null, preBidMeetingUtc);
                return;
            }

            List<PostedFileInfo> files;
            string fileError;
            if (!PrepareUploads(out files, out fileError))
            {
                ShowError(fileError);
                return;
            }
            try
            {
                if (files.Count > 0 && !TenderLifecycleFiles.IsAllowedTenderDocumentType(DocumentTypeSelect.SelectedValue))
                {
                    ShowError("Select an active attachment type.");
                    return;
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Document type could not be validated.", ex);
                ShowError("Attachment type could not be validated. Please retry or contact support.");
                return;
            }

            List<string> savedPaths = new List<string>();
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    if (!IsActiveCatalogValue(connection, "dbo.TenderMethods", "MethodName", TenderTypeSelect.SelectedValue, transaction) ||
                        !IsActiveCatalogValue(connection, "dbo.TenderTypes", "TypeName", TenderCategorySelect.SelectedValue, transaction) ||
                        !IsActiveFinancialYear(connection, FinancialYearSelect.SelectedValue, transaction) ||
                        (subcategoryId.HasValue && !IsActiveSubcategory(connection, subcategoryId.Value, TenderCategorySelect.SelectedValue, transaction)))
                        throw new InvalidOperationException("A selected tender master value is no longer active. Refresh the form and retry.");
                    if (files.Count > 0 &&
                        !IsActiveCatalogValue(connection, "dbo.DocumentTypes", "TypeName", DocumentTypeSelect.SelectedValue, transaction))
                        throw new InvalidOperationException("The selected attachment type is no longer active. Refresh the form and retry.");

                    int tenderId;
                    int year = DateTime.UtcNow.Year;
                    string tenderNumber;
                    using (SqlCommand command = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.Departments WHERE DepartmentId=@DepartmentId AND IsActive=1;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                            throw new InvalidOperationException("The selected department is no longer active. Refresh the form and select an active department.");
                    }
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT COUNT(*) FROM dbo.Tender WITH (UPDLOCK, HOLDLOCK)
                          WHERE TenderNumber LIKE @Prefix;", connection, transaction))
                    {
                        AddText(command, "@Prefix", SqlDbType.NVarChar, "TND-" + year.ToString(CultureInfo.InvariantCulture) + "-%", 20);
                        int countThisYear = Convert.ToInt32(command.ExecuteScalar());
                        tenderNumber = "TND-" + year.ToString(CultureInfo.InvariantCulture) + "-" +
                                       (countThisYear + 1).ToString("D5", CultureInfo.InvariantCulture);
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.Tender
                            (TenderNumber,TrackingId,Title,Description,EligibilityCriteria,DepartmentId,EstimatedAmount,TenderType,TenderCategory,
                             TenderSubcategoryId,TenderSubcategoryCategory,FinancialYear,EMDAmount,TenderDocumentFee,ProcessingFee,SecurityDepositAmount,
                             PublicationScheduledUtc,FinancialBidOpeningScheduledUtc,
                             CurrentStatus,SubmissionStartDate,SubmissionDeadline,CreatedBy,WorkflowVersion,
                             BudgetInformation,TechnicalSanctionNumber,TechnicalSanctionAuthority,TechnicalSanctionDate,
                             PreBidMeetingUtc,PreBidLocation,PreBidDetails,TechnicalBidOpeningScheduledUtc)
                          VALUES
                            (@TenderNumber,@TrackingId,@Title,@Description,@EligibilityCriteria,@DepartmentId,@Amount,@TenderType,@TenderCategory,
                             @TenderSubcategoryId,@TenderSubcategoryCategory,@FinancialYear,@EMDAmount,@DocumentFee,@ProcessingFee,@SecurityDeposit,
                             @PublicationScheduledUtc,@FinancialBidOpeningUtc,
                             N'Draft',@StartDate,@Deadline,@CreatedBy,2,@BudgetInformation,@SanctionNumber,
                             @SanctionAuthority,@SanctionDate,@PreBidMeetingUtc,@PreBidLocation,@PreBidDetails,
                             @TechnicalBidOpeningUtc);
                          SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                    {
                        AddText(command, "@TenderNumber", SqlDbType.NVarChar, tenderNumber, 50);
                        AddText(command, "@TrackingId", SqlDbType.NVarChar, Guid.NewGuid().ToString(), 100);
                        AddText(command, "@Title", SqlDbType.NVarChar, TitleText.Text.Trim(), 250);
                        AddText(command, "@Description", SqlDbType.NVarChar, DescriptionText.Text, -1);
                        AddText(command, "@EligibilityCriteria", SqlDbType.NVarChar, EligibilityCriteriaText.Text, 4000);
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, amount);
                        AddText(command, "@TenderType", SqlDbType.NVarChar, TenderTypeSelect.SelectedValue, 50);
                        AddText(command, "@TenderCategory", SqlDbType.NVarChar, TenderCategorySelect.SelectedValue, 50);
                        ModuleDb.AddParameter(command, "@TenderSubcategoryId", SqlDbType.Int,
                            subcategoryId.HasValue ? (object)subcategoryId.Value : null);
                        AddText(command, "@TenderSubcategoryCategory", SqlDbType.NVarChar,
                            subcategoryId.HasValue ? TenderCategorySelect.SelectedValue : null, 50);
                        AddText(command, "@FinancialYear", SqlDbType.NVarChar, FinancialYearSelect.SelectedValue, 9);
                        ModuleDb.AddParameter(command, "@EMDAmount", SqlDbType.Decimal, emdAmount);
                        ModuleDb.AddParameter(command, "@DocumentFee", SqlDbType.Decimal, documentFee);
                        ModuleDb.AddParameter(command, "@ProcessingFee", SqlDbType.Decimal, processingFee);
                        ModuleDb.AddParameter(command, "@SecurityDeposit", SqlDbType.Decimal, securityDeposit);
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
                        tenderId = Convert.ToInt32(command.ExecuteScalar());
                    }

                    SaveTenderRequirements(connection, transaction, tenderId);
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

                    foreach (PostedFileInfo file in files)
                    {
                        string uploadRoot = System.Configuration.ConfigurationManager.AppSettings["UploadRoot"] ?? "~/Uploads";
                        string folder = Server.MapPath(uploadRoot.TrimEnd('/', '\\') + "/tenders/" + tenderId.ToString(CultureInfo.InvariantCulture));
                        Directory.CreateDirectory(folder);
                        string diskName = Guid.NewGuid().ToString("N") + file.Extension;
                        string fullPath = Path.Combine(folder, diskName);
                        file.File.SaveAs(fullPath);
                        string relativePath = "Uploads/tenders/" +
                            tenderId.ToString(CultureInfo.InvariantCulture) + "/" + diskName;
                        savedPaths.Add(fullPath);
                        using (SqlCommand command = new SqlCommand(
                            @"INSERT INTO dbo.TenderDocuments (TenderId, DocumentType, FileName, FilePath, UploadedBy)
                              VALUES (@TenderId, @DocumentType, @FileName, @FilePath, @UploadedBy);", connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                            AddText(command, "@DocumentType", SqlDbType.NVarChar, DocumentTypeSelect.SelectedValue, 160);
                            AddText(command, "@FileName", SqlDbType.NVarChar, file.OriginalName, 260);
                            AddText(command, "@FilePath", SqlDbType.NVarChar, relativePath, 400);
                            ModuleDb.AddParameter(command, "@UploadedBy", SqlDbType.Int, CurrentUserId);
                            command.ExecuteNonQuery();
                        }

                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Create", "Tender", tenderId, tenderNumber, Request.UserHostAddress);
                    transaction.Commit();
                    Response.Redirect("~/Pages/TenderDetails.aspx?id=" + tenderId.ToString(CultureInfo.InvariantCulture), false);
                    Context.ApplicationInstance.CompleteRequest();
                }
            }
            catch (SqlException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender creation failed.", ex);
                ShowError("The tender could not be saved. Check that the tender number is unique and retry.");
            }
            catch (IOException ex)
            {
                DeleteSavedFiles(savedPaths);
                Trace.Warn("DepartmentModule", "Tender attachment could not be stored.", ex);
                ShowError("An attachment could not be saved. The tender was not submitted.");
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
            return Int32.TryParse(DepartmentSelect.SelectedValue, out departmentId) && departmentId > 0;
        }

        private static bool TryParseDate(string value, out DateTime result)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out result);
        }

        private static bool TryParseIndiaLocalDateTime(string value, out DateTime utc)
        {
            utc = DateTime.MinValue;
            DateTime localTime;
            if (!DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out localTime))
                return false;
            TimeZoneInfo indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified), indiaTimeZone);
            return true;
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

        private static bool IsActiveSubcategory(SqlConnection connection, int subcategoryId,
            string category, SqlTransaction transaction = null)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(*) FROM dbo.TenderSubcategories
                  WHERE SubcategoryId=@SubcategoryId AND TenderCategory=@Category AND IsActive=1;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@SubcategoryId", SqlDbType.Int, subcategoryId);
                AddText(command, "@Category", SqlDbType.NVarChar, category, 100);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        private static bool TryParseOptionalAmount(string value, out decimal? amount)
        {
            amount = null;
            if (String.IsNullOrWhiteSpace(value)) return true;
            decimal parsed;
            if (!Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed) ||
                parsed < 0 || parsed > 9999999999999999.99m)
                return false;
            amount = parsed;
            return true;
        }

        private void SaveTenderRequirements(SqlConnection connection, SqlTransaction transaction, int tenderId)
        {
            using (SqlCommand command = new SqlCommand(
                @"IF EXISTS (SELECT 1 FROM dbo.TenderRequirements WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId)
                  BEGIN
                      UPDATE dbo.TenderRequirements
                      SET ScopeOfWork=@ScopeOfWork,TechnicalRequirements=@TechnicalRequirements,
                          FinancialRequirements=@FinancialRequirements,AdditionalConditions=@AdditionalConditions,
                          TermsAndConditions=@TermsAndConditions,ModifiedBy=@UserId,ModifiedAtUtc=SYSUTCDATETIME()
                      WHERE TenderId=@TenderId;
                  END
                  ELSE
                  BEGIN
                      INSERT INTO dbo.TenderRequirements
                          (TenderId,ScopeOfWork,TechnicalRequirements,FinancialRequirements,AdditionalConditions,
                           TermsAndConditions,CreatedBy,ModifiedBy)
                      VALUES
                          (@TenderId,@ScopeOfWork,@TechnicalRequirements,@FinancialRequirements,@AdditionalConditions,
                           @TermsAndConditions,@UserId,@UserId);
                  END;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                AddText(command, "@ScopeOfWork", SqlDbType.NVarChar, ScopeOfWorkText.Text, 4000);
                AddText(command, "@TechnicalRequirements", SqlDbType.NVarChar, TechnicalRequirementsText.Text, 4000);
                AddText(command, "@FinancialRequirements", SqlDbType.NVarChar, FinancialRequirementsText.Text, 4000);
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
                     EligibilityCriteria,DepartmentId,EstimatedAmount,TenderType,TenderCategory,
                     TenderSubcategoryId,FinancialYear,EMDAmount,TenderDocumentFee,ProcessingFee,
                     SecurityDepositAmount,PublicationScheduledUtc,SubmissionStartDate,SubmissionDeadline,
                     TechnicalBidOpeningScheduledUtc,FinancialBidOpeningScheduledUtc,CurrentStatus,
                     ScopeOfWork,TechnicalRequirements,FinancialRequirements,AdditionalConditions,
                     TermsAndConditions,RevisedBy)
                  SELECT t.TenderId,
                         COALESCE((SELECT MAX(r.RevisionNumber) FROM dbo.TenderRevisions r WITH (UPDLOCK,HOLDLOCK)
                                   WHERE r.TenderId=t.TenderId),0)+1,
                         @Action,@Reason,t.TenderNumber,t.Title,t.Description,t.EligibilityCriteria,
                         t.DepartmentId,t.EstimatedAmount,t.TenderType,t.TenderCategory,
                         t.TenderSubcategoryId,t.FinancialYear,t.EMDAmount,t.TenderDocumentFee,t.ProcessingFee,
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

        private void UpdateDraftTender(decimal amount, decimal? emdAmount, decimal? documentFee,
            decimal? processingFee, decimal? securityDeposit, int? subcategoryId,
            DateTime? publicationScheduledUtc, DateTime? submissionStart, DateTime? submissionDeadline,
            DateTime? technicalOpeningUtc, DateTime? financialOpeningUtc,
            DateTime? sanctionDate, DateTime? preBidMeetingUtc)
        {
            bool isAdministrator = HasPermission("system.admin") || HasAnyRole("Admin");
            bool isDepartmentOfficer = !isAdministrator &&
                (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                 String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase));
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    if (!IsActiveCatalogValue(connection, "dbo.TenderMethods", "MethodName", TenderTypeSelect.SelectedValue, transaction) ||
                        !IsActiveCatalogValue(connection, "dbo.TenderTypes", "TypeName", TenderCategorySelect.SelectedValue, transaction) ||
                        !IsActiveFinancialYear(connection, FinancialYearSelect.SelectedValue, transaction) ||
                        (subcategoryId.HasValue && !IsActiveSubcategory(connection, subcategoryId.Value, TenderCategorySelect.SelectedValue, transaction)))
                        throw new InvalidOperationException("A selected tender master value is no longer active. Refresh the form and retry.");
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
                          SET Title=@Title,Description=@Description,EligibilityCriteria=@EligibilityCriteria,
                              EstimatedAmount=@Amount,TenderType=@TenderType,
                              TenderCategory=@TenderCategory,TenderSubcategoryId=@TenderSubcategoryId,
                              TenderSubcategoryCategory=@TenderSubcategoryCategory,
                              FinancialYear=@FinancialYear,EMDAmount=@EMDAmount,TenderDocumentFee=@DocumentFee,
                              ProcessingFee=@ProcessingFee,SecurityDepositAmount=@SecurityDeposit,
                              PublicationScheduledUtc=@PublicationScheduledUtc,
                              TechnicalBidOpeningScheduledUtc=@TechnicalBidOpeningUtc,
                              FinancialBidOpeningScheduledUtc=@FinancialBidOpeningUtc,
                              BudgetInformation=@BudgetInformation,TechnicalSanctionNumber=@SanctionNumber,
                              TechnicalSanctionAuthority=@SanctionAuthority,TechnicalSanctionDate=@SanctionDate,
                              PreBidMeetingUtc=@PreBidMeetingUtc,PreBidLocation=@PreBidLocation,
                              PreBidDetails=@PreBidDetails,
                              SubmissionStartDate=@StartDate,SubmissionDeadline=@Deadline,
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
                        AddText(command, "@EligibilityCriteria", SqlDbType.NVarChar, EligibilityCriteriaText.Text, 4000);
                        ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, amount);
                        AddText(command, "@TenderType", SqlDbType.NVarChar, TenderTypeSelect.SelectedValue, 50);
                        AddText(command, "@TenderCategory", SqlDbType.NVarChar, TenderCategorySelect.SelectedValue, 50);
                        ModuleDb.AddParameter(command, "@TenderSubcategoryId", SqlDbType.Int,
                            subcategoryId.HasValue ? (object)subcategoryId.Value : null);
                        AddText(command, "@TenderSubcategoryCategory", SqlDbType.NVarChar,
                            subcategoryId.HasValue ? TenderCategorySelect.SelectedValue : null, 50);
                        AddText(command, "@FinancialYear", SqlDbType.NVarChar, FinancialYearSelect.SelectedValue, 9);
                        ModuleDb.AddParameter(command, "@EMDAmount", SqlDbType.Decimal, emdAmount);
                        ModuleDb.AddParameter(command, "@DocumentFee", SqlDbType.Decimal, documentFee);
                        ModuleDb.AddParameter(command, "@ProcessingFee", SqlDbType.Decimal, processingFee);
                        ModuleDb.AddParameter(command, "@SecurityDeposit", SqlDbType.Decimal, securityDeposit);
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
                        ModuleDb.AddParameter(command, "@ModifiedBy", SqlDbType.Int, CurrentUserId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, EditingTenderId);
                        AddText(command, "@ExpectedStatus", SqlDbType.NVarChar, currentStatus, 50);
                        ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit, isAdministrator);
                        ModuleDb.AddParameter(command, "@IsDepartmentOfficer", SqlDbType.Bit, isDepartmentOfficer);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Tender status changed; refresh the page before editing.");
                    }

                    SaveTenderRequirements(connection, transaction, EditingTenderId);
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

                Response.Redirect("~/Pages/TenderDetails.aspx?id=" + EditingTenderId.ToString(CultureInfo.InvariantCulture), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender update failed.", ex);
                ShowError("The tender could not be updated. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
        }

        private bool PrepareUploads(out List<PostedFileInfo> files, out string error)
        {
            files = new List<PostedFileInfo>();
            error = null;
            if (!Attachments.HasFiles) return true;
            foreach (HttpPostedFile file in Attachments.PostedFiles)
            {
                string originalName = Path.GetFileName(file.FileName);
                string extension = Path.GetExtension(originalName);
                if (originalName.Length == 0 || originalName.Length > 260 ||
                    file.ContentLength <= 0 || file.ContentLength > 20 * 1024 * 1024)
                {
                    error = "Each attachment must have a valid file name and be no larger than 20 MB.";
                    return false;
                }
                if (Array.IndexOf(AllowedExtensions, extension.ToLowerInvariant()) < 0)
                {
                    error = "An attachment has a file type that is not permitted.";
                    return false;
                }
                if (!TenderLifecycleFiles.HasAllowedContentSignature(file.InputStream, extension.ToLowerInvariant()))
                {
                    error = "An attachment's file contents do not match its file type.";
                    return false;
                }
                files.Add(new PostedFileInfo { File = file, OriginalName = originalName, Extension = extension.ToLowerInvariant() });
            }
            return true;
        }

        private static void DeleteSavedFiles(List<string> paths)
        {
            foreach (string path in paths) if (File.Exists(path)) File.Delete(path);
        }

        private void ShowError(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }

        private sealed class PostedFileInfo
        {
            public HttpPostedFile File { get; set; }
            public string OriginalName { get; set; }
            public string Extension { get; set; }
        }
    }
}
