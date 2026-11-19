using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class ApplicationRequirements : ModulePage
    {
        protected Literal Message, TenderNumber, TenderTitle;
        protected HyperLink WorkflowLink;
        protected TextBox DocumentTypeText, DescriptionText, AllowedExtensionsText, MaxFileSizeText;
        protected CheckBox MandatoryCheck;
        protected Button AddRequirementButton;
        protected GridView RequirementsGrid;

        private int TenderId { get { return QueryId("tenderId"); } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RequireRoles("Junior Engineer (JE)") ||
                !RequirePermission("tender.create.department")) return;
            if (TenderId <= 0)
            {
                DenyAccess();
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    if (!CanManageRequirements(connection, null, false))
                    {
                        DenyAccess();
                        return;
                    }
                    WorkflowLink.NavigateUrl = "~/Pages/TenderWorkflow.aspx?id=" +
                        TenderId.ToString(CultureInfo.InvariantCulture);
                    if (!IsPostBack) BindRequirements(connection);
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application requirements could not be loaded.", ex);
                ShowError("Application requirements could not be loaded. Please retry or contact support.");
                AddRequirementButton.Enabled = false;
            }
        }

        protected void AddRequirementButton_Click(object sender, EventArgs e)
        {
            string documentType = (DocumentTypeText.Text ?? String.Empty).Trim();
            string description = (DescriptionText.Text ?? String.Empty).Trim();
            int maxFileSizeBytes;
            string allowedExtensions;
            if (documentType.Length == 0 || documentType.Length > 160 ||
                description.Length > 1000 ||
                !Int32.TryParse(MaxFileSizeText.Text, NumberStyles.None, CultureInfo.InvariantCulture, out maxFileSizeBytes) ||
                maxFileSizeBytes < 1 || maxFileSizeBytes > 10 * 1024 * 1024)
            {
                ShowError("Enter a document name, description of at most 1,000 characters, and file limit from 1 byte to 10 MB.");
                return;
            }

            try
            {
                allowedExtensions = TenderLifecycleFiles.NormalizeAllowedApplicationExtensions(AllowedExtensionsText.Text);
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    EnsureEditable(connection, transaction);
                    using (SqlCommand command = new SqlCommand(
                        @"IF EXISTS
                          (
                              SELECT 1 FROM dbo.TenderRequiredBidDocuments WITH (UPDLOCK,HOLDLOCK)
                              WHERE TenderId=@TenderId AND DocumentType=@DocumentType
                          )
                              THROW 51301,'A document requirement with this name already exists.',1;
                          INSERT INTO dbo.TenderRequiredBidDocuments
                              (TenderId,DocumentType,CreatedBy,Description,IsMandatory,AllowedExtensions,MaxFileSizeBytes)
                          VALUES(@TenderId,@DocumentType,@UserId,@Description,@IsMandatory,@AllowedExtensions,@MaxFileSizeBytes);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                        AddText(command, "@DocumentType", documentType, 160);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                        AddText(command, "@Description", description, 1000);
                        ModuleDb.AddParameter(command, "@IsMandatory", SqlDbType.Bit, MandatoryCheck.Checked);
                        AddText(command, "@AllowedExtensions", allowedExtensions, 500);
                        ModuleDb.AddParameter(command, "@MaxFileSizeBytes", SqlDbType.Int, maxFileSizeBytes);
                        command.ExecuteNonQuery();
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "AddApplicationRequirement",
                        "TenderRequiredBidDocuments", TenderId, documentType, Request.UserHostAddress);
                    transaction.Commit();
                    DocumentTypeText.Text = String.Empty;
                    DescriptionText.Text = String.Empty;
                    AllowedExtensionsText.Text = String.Empty;
                    MaxFileSizeText.Text = "10000000";
                    MandatoryCheck.Checked = true;
                    ShowSuccess("Application document requirement added.");
                    BindRequirements(connection);
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application requirement could not be added.", ex);
                ShowError(ex.Number == 51301 || ex.Number == 2601 || ex.Number == 2627
                    ? "A requirement with that document name already exists."
                    : "The application document requirement could not be saved.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
        }

        protected void RequirementsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName != "UpdateRequirement" && e.CommandName != "DeleteRequirement") return;
            int rowIndex;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), out rowIndex) ||
                rowIndex < 0 || rowIndex >= RequirementsGrid.DataKeys.Count)
            {
                ShowError("The selected requirement could not be verified. Refresh the page and try again.");
                return;
            }

            string documentType = Convert.ToString(RequirementsGrid.DataKeys[rowIndex].Value);
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    EnsureEditable(connection, transaction);
                    if (e.CommandName == "DeleteRequirement")
                    {
                        using (SqlCommand command = new SqlCommand(
                            @"DELETE FROM dbo.TenderRequiredBidDocuments
                              WHERE TenderId=@TenderId AND DocumentType=@DocumentType;",
                            connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                            AddText(command, "@DocumentType", documentType, 160);
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("The selected requirement no longer exists.");
                        }
                        ModuleDb.AddAudit(connection, transaction, CurrentUserId, "RemoveApplicationRequirement",
                            "TenderRequiredBidDocuments", TenderId, documentType, Request.UserHostAddress);
                    }
                    else
                    {
                        GridViewRow row = RequirementsGrid.Rows[rowIndex];
                        TextBox descriptionInput = row.FindControl("EditDescriptionText") as TextBox;
                        TextBox extensionsInput = row.FindControl("EditExtensionsText") as TextBox;
                        TextBox sizeInput = row.FindControl("EditMaxFileSizeText") as TextBox;
                        CheckBox mandatoryInput = row.FindControl("EditMandatoryCheck") as CheckBox;
                        int maxFileSizeBytes;
                        if (descriptionInput == null || extensionsInput == null || sizeInput == null ||
                            mandatoryInput == null || descriptionInput.Text.Trim().Length > 1000 ||
                            !Int32.TryParse(sizeInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out maxFileSizeBytes) ||
                            maxFileSizeBytes < 1 || maxFileSizeBytes > 10 * 1024 * 1024)
                            throw new InvalidOperationException("Descriptions may not exceed 1,000 characters and file limits must be from 1 byte to 10 MB.");

                        string extensions = TenderLifecycleFiles.NormalizeAllowedApplicationExtensions(extensionsInput.Text);
                        using (SqlCommand command = new SqlCommand(
                            @"UPDATE dbo.TenderRequiredBidDocuments
                              SET Description=@Description,IsMandatory=@IsMandatory,
                                  AllowedExtensions=@AllowedExtensions,MaxFileSizeBytes=@MaxFileSizeBytes
                              WHERE TenderId=@TenderId AND DocumentType=@DocumentType;",
                            connection, transaction))
                        {
                            AddText(command, "@Description", descriptionInput.Text, 1000);
                            ModuleDb.AddParameter(command, "@IsMandatory", SqlDbType.Bit, mandatoryInput.Checked);
                            AddText(command, "@AllowedExtensions", extensions, 500);
                            ModuleDb.AddParameter(command, "@MaxFileSizeBytes", SqlDbType.Int, maxFileSizeBytes);
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                            AddText(command, "@DocumentType", documentType, 160);
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("The selected requirement no longer exists.");
                        }
                        ModuleDb.AddAudit(connection, transaction, CurrentUserId, "UpdateApplicationRequirement",
                            "TenderRequiredBidDocuments", TenderId, documentType, Request.UserHostAddress);
                    }

                    transaction.Commit();
                    ShowSuccess(e.CommandName == "DeleteRequirement"
                        ? "Application document requirement removed."
                        : "Application document requirement updated.");
                    BindRequirements(connection);
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application requirement could not be changed.", ex);
                ShowError("The application document requirement could not be changed.");
            }
            catch (InvalidOperationException ex)
            {
                ShowError(ex.Message);
            }
        }

        private bool CanManageRequirements(SqlConnection connection, SqlTransaction transaction, bool lockTender)
        {
            string lockHint = lockTender ? " WITH (UPDLOCK,HOLDLOCK)" : String.Empty;
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.Tender t" + lockHint + @"
                  WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.CurrentStatus=N'Draft'
                    AND t.IsDeleted=0 AND t.DepartmentId=@DepartmentId
                    AND EXISTS
                    (
                        SELECT 1 FROM dbo.TenderWorkflowAssignments a
                        INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                        WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                          AND a.AssignedRoleId=@RoleId
                          AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                          AND a.DepartmentId=t.DepartmentId
                          AND s.WorkflowVersion=2 AND s.StageName=N'Final_JE_SubmitTender'
                    )
                    AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBid b WHERE b.TenderId=t.TenderId);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : -1);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        private void EnsureEditable(SqlConnection connection, SqlTransaction transaction)
        {
            if (!CanManageRequirements(connection, transaction, true))
                throw new InvalidOperationException("Requirements can only be changed on an assigned department draft before an application is created.");
        }

        private void BindRequirements(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT DocumentType,Description,IsMandatory,AllowedExtensions,MaxFileSizeBytes
                  FROM dbo.TenderRequiredBidDocuments WHERE TenderId=@TenderId ORDER BY DocumentType;",
                connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                RequirementsGrid.DataSource = data;
                RequirementsGrid.DataBind();
            }
        }

        private static void AddText(SqlCommand command, string name, string value, int size)
        {
            ModuleDb.AddParameter(command, name, SqlDbType.NVarChar,
                String.IsNullOrWhiteSpace(value) ? null : value.Trim(), size);
        }

        private void ShowError(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }

        private void ShowSuccess(string text)
        {
            Message.Text = "<div class=\"message success\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }

        private void DenyAccess()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            Response.Redirect("~/Pages/Error.aspx?code=403", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
