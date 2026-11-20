using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class AdminMasterData : ModulePage
    {
        protected Literal Message;
        protected DropDownList CatalogSelect;
        protected DropDownList ParentCategorySelect;
        protected HiddenField OriginalValue;
        protected Label ValueLabel;
        protected TextBox ValueText, SortOrderText;
        protected TextBox FinancialYearStartText, FinancialYearEndText;
        protected CheckBox IsActiveCheck;
        protected Button SaveButton, CancelButton;
        protected GridView CatalogGrid;
        protected Panel ParentCategoryPanel, FinancialYearPanel;

        private sealed class CatalogDefinition
        {
            public string TableName;
            public string KeyColumn;
            public string DisplayColumn;
            public string ParentColumn;
            public int MaxLength;
            public string UsageQuery;
            public string DisplayName;
            public bool UsesIntegerKey;
            public bool IsFinancialYear;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RequirePermission("masterdata.manage")) return;
            if (!IsPostBack)
            {
                CatalogSelect.Items.Add(new ListItem("Tender categories", "categories"));
                CatalogSelect.Items.Add(new ListItem("Tender subcategories", "subcategories"));
                CatalogSelect.Items.Add(new ListItem("Tender methods", "methods"));
                CatalogSelect.Items.Add(new ListItem("Financial years", "financialyears"));
                CatalogSelect.Items.Add(new ListItem("Document types", "documents"));
                CatalogSelect.Items.Add(new ListItem("Units", "units"));
                CatalogSelect.Items.Add(new ListItem("Officer designations", "designations"));
                BindParentCategories();
                SetCatalogControls();
                BindCatalog();
            }
        }

        protected void CatalogSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetForm();
            SetCatalogControls();
            BindCatalog();
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            Page.Validate("Catalog");
            string value = ValueText.Text.Trim();
            int sortOrder;
            CatalogDefinition catalog = GetCatalog();
            if (!Page.IsValid || value.Length == 0 || value.Length > catalog.MaxLength ||
                !Int32.TryParse(SortOrderText.Text, NumberStyles.None, CultureInfo.InvariantCulture, out sortOrder) ||
                sortOrder < 0 || sortOrder > 32767)
            {
                ShowMessage("Enter a name within the displayed catalog limit and a display order from 0 to 32767.");
                return;
            }

            bool isEditing = !String.IsNullOrEmpty(OriginalValue.Value);
            int financialYearStart = 0;
            DateTime financialYearStartDate = DateTime.MinValue, financialYearEndDate = DateTime.MinValue;
            if (catalog.IsFinancialYear &&
                (!TryParseFinancialYear(value, out financialYearStart, out financialYearStartDate, out financialYearEndDate) ||
                 financialYearStartDate >= financialYearEndDate))
            {
                ShowMessage("Enter a financial year as YYYY-YY with a valid date range.");
                return;
            }
            if (catalog.IsFinancialYear && !isEditing &&
                (!DateTime.TryParseExact(FinancialYearStartText.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out financialYearStartDate) ||
                 !DateTime.TryParseExact(FinancialYearEndText.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out financialYearEndDate) ||
                 financialYearStartDate >= financialYearEndDate ||
                 financialYearStartDate.Year != financialYearStart ||
                 financialYearStartDate.Month != 4 || financialYearStartDate.Day != 1 ||
                 financialYearEndDate.Year != financialYearStart + 1 ||
                 financialYearEndDate.Month != 3 || financialYearEndDate.Day != 31))
            {
                ShowMessage("Financial-year dates must run from 1 April of the first year through 31 March of the next year.");
                return;
            }
            if (!catalog.UsesIntegerKey && isEditing && !catalog.IsFinancialYear &&
                !String.Equals(value, OriginalValue.Value, StringComparison.Ordinal))
            {
                ShowMessage("Catalog names cannot be renamed. Add a new value and retain the existing value for historical records.");
                return;
            }
            if (catalog.ParentColumn != null && !isEditing && String.IsNullOrWhiteSpace(ParentCategorySelect.SelectedValue))
            {
                ShowMessage("Select a tender category for this subcategory.");
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (catalog.ParentColumn != null && !IsActiveCategory(connection, transaction, ParentCategorySelect.SelectedValue))
                        throw new InvalidOperationException("The selected category is no longer active. Refresh and retry.");
                    using (SqlCommand command = new SqlCommand(BuildSaveQuery(catalog, isEditing), connection, transaction))
                    {
                        if (catalog.UsesIntegerKey && !isEditing)
                            ModuleDb.AddParameter(command, "@Name", SqlDbType.NVarChar, value, catalog.MaxLength);
                        else
                            AddCatalogKey(command, catalog, isEditing ? OriginalValue.Value : value);
                        ModuleDb.AddParameter(command, "@SortOrder", SqlDbType.Int, sortOrder);
                        ModuleDb.AddParameter(command, "@IsActive", SqlDbType.Bit, IsActiveCheck.Checked);
                        if (!isEditing && catalog.ParentColumn != null)
                            ModuleDb.AddParameter(command, "@ParentValue", SqlDbType.NVarChar, ParentCategorySelect.SelectedValue, 100);
                        if (!isEditing && catalog.IsFinancialYear)
                        {
                            ModuleDb.AddParameter(command, "@StartDate", SqlDbType.Date, financialYearStartDate.Date);
                            ModuleDb.AddParameter(command, "@EndDate", SqlDbType.Date, financialYearEndDate.Date);
                        }
                        int affected = command.ExecuteNonQuery();
                        if (affected != 1) throw new InvalidOperationException("The catalog entry was changed by another administrator. Refresh and retry.");
                    }

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId,
                        isEditing ? "Update" : "Create", "MasterData", null,
                        catalog.DisplayName + ": " + value, Request.UserHostAddress);
                    transaction.Commit();
                }

                ResetForm();
                SetCatalogControls();
                BindCatalog();
                ShowMessage("Catalog entry saved.", true);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Master data item could not be saved.", ex);
                ShowMessage(ex.Number == 2601 || ex.Number == 2627
                    ? "An entry with that name already exists."
                    : "The catalog entry could not be saved. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
        }

        protected void CatalogGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            CatalogDefinition catalog = GetCatalog();
            string value = Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture);
            if (String.IsNullOrWhiteSpace(value)) return;

            if (e.CommandName == "EditCatalogItem")
            {
                LoadCatalogItem(catalog, value);
                return;
            }
            if (e.CommandName != "DeleteCatalogItem") return;

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    using (SqlCommand command = new SqlCommand(catalog.UsageQuery, connection, transaction))
                    {
                        AddCatalogKey(command, catalog, value);
                        if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                        {
                            ShowMessage("This entry is used by existing records and cannot be deleted. Deactivate it instead.");
                            transaction.Rollback();
                            return;
                        }
                    }
                    using (SqlCommand command = new SqlCommand(
                        "DELETE FROM " + catalog.TableName + " WHERE " + catalog.KeyColumn + "=@Value;", connection, transaction))
                    {
                        AddCatalogKey(command, catalog, value);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The catalog entry was changed by another administrator. Refresh and retry.");
                    }
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Delete", "MasterData", null,
                        catalog.DisplayName + ": " + value, Request.UserHostAddress);
                    transaction.Commit();
                }
                BindCatalog();
                ShowMessage("Unused catalog entry deleted.", true);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Master data item could not be deleted.", ex);
                ShowMessage("The catalog entry could not be deleted. It may still be referenced; refresh and retry.");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
        }

        protected void CancelButton_Click(object sender, EventArgs e)
        {
            ResetForm();
            BindCatalog();
        }

        private void LoadCatalogItem(CatalogDefinition catalog, string value)
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    "SELECT " + catalog.KeyColumn + ", " + (catalog.DisplayColumn ?? catalog.KeyColumn) +
                    " AS CatalogDisplay,SortOrder,IsActive" +
                    (catalog.ParentColumn == null ? String.Empty : "," + catalog.ParentColumn + " AS ParentValue") +
                    (catalog.IsFinancialYear ? ",StartDate,EndDate" : String.Empty) + " FROM " + catalog.TableName +
                    " WHERE " + catalog.KeyColumn + "=@Value;", connection))
                {
                    AddCatalogKey(command, catalog, value);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            ShowMessage("The selected entry no longer exists.");
                            return;
                        }
                        OriginalValue.Value = Convert.ToString(reader[catalog.KeyColumn], CultureInfo.InvariantCulture);
                        ValueText.Text = Convert.ToString(reader["CatalogDisplay"], CultureInfo.InvariantCulture);
                        SortOrderText.Text = Convert.ToString(reader["SortOrder"], CultureInfo.InvariantCulture);
                        IsActiveCheck.Checked = Convert.ToBoolean(reader["IsActive"], CultureInfo.InvariantCulture);
                        if (catalog.ParentColumn != null)
                        {
                            ParentCategorySelect.SelectedValue = Convert.ToString(reader["ParentValue"], CultureInfo.InvariantCulture);
                            ParentCategorySelect.Enabled = false;
                        }
                        if (catalog.IsFinancialYear)
                        {
                            FinancialYearStartText.Text = Convert.ToDateTime(reader["StartDate"], CultureInfo.InvariantCulture)
                                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            FinancialYearEndText.Text = Convert.ToDateTime(reader["EndDate"], CultureInfo.InvariantCulture)
                                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            FinancialYearStartText.Enabled = false;
                            FinancialYearEndText.Enabled = false;
                        }
                    }
                }
                ValueText.ReadOnly = true;
                ValueLabel.Text = catalog.DisplayName;
                SaveButton.Text = "Save changes";
                CancelButton.Visible = true;
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Master data item could not be loaded.", ex);
                ShowMessage("The selected catalog entry could not be loaded. Please retry or contact support.");
            }
        }

        private void BindCatalog()
        {
            CatalogDefinition catalog = GetCatalog();
            try
            {
                DataTable data = new DataTable();
                string displayColumn = catalog.DisplayColumn ?? catalog.KeyColumn;
                string parentColumn = catalog.ParentColumn == null
                    ? "CAST(NULL AS NVARCHAR(100))"
                    : catalog.ParentColumn;
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    "SELECT " + catalog.KeyColumn + " AS CatalogKey," + displayColumn + " AS CatalogValue," +
                    parentColumn + " AS ParentValue," +
                    (catalog.IsFinancialYear ? "CONVERT(NVARCHAR(10),StartDate,23)+N' to '+CONVERT(NVARCHAR(10),EndDate,23)" : "CAST(NULL AS NVARCHAR(50))") +
                    " AS FinancialYearRange,SortOrder,IsActive FROM " + catalog.TableName +
                    " ORDER BY SortOrder," + displayColumn + ";", connection))
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    adapter.Fill(data);

                data.Columns.Add("UsageCount", typeof(int));
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    foreach (DataRow row in data.Rows)
                    {
                        using (SqlCommand command = new SqlCommand(catalog.UsageQuery, connection))
                        {
                            AddCatalogKey(command, catalog, Convert.ToString(row["CatalogKey"], CultureInfo.InvariantCulture));
                            row["UsageCount"] = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        }
                    }
                }
                CatalogGrid.DataSource = data;
                CatalogGrid.DataBind();
                CatalogGrid.Columns[1].Visible = catalog.ParentColumn != null;
                CatalogGrid.Columns[2].Visible = catalog.IsFinancialYear;
                ValueLabel.Text = catalog.DisplayName;
                ValueText.MaxLength = catalog.MaxLength;
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Master data catalog could not be loaded.", ex);
                ShowMessage("The catalog could not be loaded. Please retry or contact support.");
                SaveButton.Enabled = false;
            }
        }

        private CatalogDefinition GetCatalog()
        {
            switch (CatalogSelect.SelectedValue)
            {
                case "categories":
                    return new CatalogDefinition
                    {
                        TableName = "dbo.TenderTypes", KeyColumn = "TypeName", MaxLength = 50,
                        DisplayName = "Category name",
                        UsageQuery = @"SELECT
                            (SELECT COUNT(*) FROM dbo.Tender WHERE TenderCategory=@Value) +
                            (SELECT COUNT(*) FROM dbo.TenderSubcategories WHERE TenderCategory=@Value) +
                            (SELECT COUNT(*) FROM dbo.TenderRevisions WHERE TenderCategory=@Value);"
                    };
                case "subcategories":
                    return new CatalogDefinition
                    {
                        TableName = "dbo.TenderSubcategories", KeyColumn = "SubcategoryId",
                        DisplayColumn = "SubcategoryName", ParentColumn = "TenderCategory",
                        MaxLength = 100, UsesIntegerKey = true,
                        DisplayName = "Subcategory name",
                        UsageQuery = @"SELECT
                            (SELECT COUNT(*) FROM dbo.Tender WHERE TenderSubcategoryId=@Value) +
                            (SELECT COUNT(*) FROM dbo.TenderRevisions WHERE TenderSubcategoryId=@Value);"
                    };
                case "methods":
                    return new CatalogDefinition
                    {
                        TableName = "dbo.TenderMethods", KeyColumn = "MethodName", MaxLength = 50,
                        DisplayName = "Method name",
                        UsageQuery = @"SELECT
                            (SELECT COUNT(*) FROM dbo.Tender WHERE TenderType=@Value) +
                            (SELECT COUNT(*) FROM dbo.TenderRevisions WHERE TenderType=@Value);"
                    };
                case "financialyears":
                    return new CatalogDefinition
                    {
                        TableName = "dbo.FinancialYears", KeyColumn = "FinancialYear", MaxLength = 9,
                        IsFinancialYear = true, DisplayName = "Financial year (YYYY-YY)",
                        UsageQuery = @"SELECT
                            (SELECT COUNT(*) FROM dbo.Tender WHERE FinancialYear=@Value) +
                            (SELECT COUNT(*) FROM dbo.TenderRevisions WHERE FinancialYear=@Value);"
                    };
                case "documents":
                    return new CatalogDefinition
                    {
                        TableName = "dbo.DocumentTypes", KeyColumn = "TypeName", MaxLength = 80,
                        DisplayName = "Document type name",
                        UsageQuery = @"SELECT
                            (SELECT COUNT(*) FROM dbo.TenderDocuments WHERE DocumentType=@Value) +
                            (SELECT COUNT(*) FROM dbo.TechnicalBidDocuments WHERE DocumentType=@Value) +
                            (SELECT COUNT(*) FROM dbo.TenderRequiredBidDocuments WHERE DocumentType=@Value);"
                    };
                case "units":
                    return new CatalogDefinition
                    {
                        TableName = "dbo.Units", KeyColumn = "UnitName", MaxLength = 20,
                        DisplayName = "Unit name",
                        UsageQuery = "SELECT COUNT(*) FROM dbo.BOQ WHERE Unit=@Value;"
                    };
                case "designations":
                    return new CatalogDefinition
                    {
                        TableName = "dbo.Designations", KeyColumn = "DesignationName", MaxLength = 150,
                        DisplayName = "Designation name",
                        UsageQuery = "SELECT COUNT(*) FROM dbo.TenderBidOpening WHERE Designation=@Value;"
                    };
                default:
                    throw new InvalidOperationException("Select a valid master-data catalog.");
            }
        }

        private void ResetForm()
        {
            OriginalValue.Value = String.Empty;
            ValueText.Text = String.Empty;
            ValueText.ReadOnly = false;
            SortOrderText.Text = "0";
            IsActiveCheck.Checked = true;
            ParentCategorySelect.Enabled = true;
            if (ParentCategorySelect.Items.Count > 0) ParentCategorySelect.SelectedIndex = 0;
            FinancialYearStartText.Text = String.Empty;
            FinancialYearEndText.Text = String.Empty;
            FinancialYearStartText.Enabled = true;
            FinancialYearEndText.Enabled = true;
            SaveButton.Text = "Add item";
            SaveButton.Enabled = true;
            CancelButton.Visible = false;
        }

        private void SetCatalogControls()
        {
            CatalogDefinition catalog = GetCatalog();
            ParentCategoryPanel.Visible = catalog.ParentColumn != null;
            FinancialYearPanel.Visible = catalog.IsFinancialYear;
            if (catalog.ParentColumn != null && ParentCategorySelect.Items.Count == 0)
                BindParentCategories();
        }

        private void BindParentCategories()
        {
            ParentCategorySelect.Items.Clear();
            ParentCategorySelect.Items.Add(new ListItem("Select tender category", ""));
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                "SELECT TypeName FROM dbo.TenderTypes WHERE IsActive=1 ORDER BY SortOrder,TypeName;", connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string category = Convert.ToString(reader["TypeName"], CultureInfo.InvariantCulture);
                    ParentCategorySelect.Items.Add(new ListItem(category, category));
                }
            }
        }

        private static bool IsActiveCategory(SqlConnection connection, SqlTransaction transaction, string category)
        {
            using (SqlCommand command = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.TenderTypes WHERE TypeName=@Value AND IsActive=1;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@Value", SqlDbType.NVarChar, category, 100);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        private static string BuildSaveQuery(CatalogDefinition catalog, bool isEditing)
        {
            if (catalog.UsesIntegerKey)
                return isEditing
                    ? "UPDATE " + catalog.TableName + " SET SortOrder=@SortOrder,IsActive=@IsActive WHERE " + catalog.KeyColumn + "=@Value;"
                    : "INSERT INTO " + catalog.TableName + "(SubcategoryName,TenderCategory,SortOrder,IsActive) VALUES(@Name,@ParentValue,@SortOrder,@IsActive);";
            if (catalog.IsFinancialYear)
                return isEditing
                    ? "UPDATE " + catalog.TableName + " SET SortOrder=@SortOrder,IsActive=@IsActive WHERE " + catalog.KeyColumn + "=@Value;"
                    : "INSERT INTO " + catalog.TableName + "(FinancialYear,StartDate,EndDate,SortOrder,IsActive) VALUES(@Value,@StartDate,@EndDate,@SortOrder,@IsActive);";
            return isEditing
                ? "UPDATE " + catalog.TableName + " SET SortOrder=@SortOrder,IsActive=@IsActive WHERE " + catalog.KeyColumn + "=@Value;"
                : "INSERT INTO " + catalog.TableName + "(" + catalog.KeyColumn + ",SortOrder,IsActive) VALUES(@Value,@SortOrder,@IsActive);";
        }

        private static void AddCatalogKey(SqlCommand command, CatalogDefinition catalog, string value)
        {
            if (catalog.UsesIntegerKey)
            {
                int id;
                if (!Int32.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0)
                    throw new InvalidOperationException("The selected catalog entry is invalid.");
                ModuleDb.AddParameter(command, "@Value", SqlDbType.Int, id);
            }
            else
            {
                ModuleDb.AddParameter(command, "@Value", SqlDbType.NVarChar, value, catalog.MaxLength);
            }
        }

        private static bool TryParseFinancialYear(string value, out int firstYear, out DateTime start, out DateTime end)
        {
            firstYear = 0;
            start = DateTime.MinValue;
            end = DateTime.MinValue;
            if (value == null || value.Length != 7 || value[4] != '-' ||
                !Int32.TryParse(value.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out firstYear) ||
                !Int32.TryParse(value.Substring(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int shortEndYear) ||
                shortEndYear != (firstYear + 1) % 100)
                return false;
            start = new DateTime(firstYear, 4, 1);
            end = new DateTime(firstYear + 1, 3, 31);
            return true;
        }

        private void ShowMessage(string text, bool success = false)
        {
            Message.Text = "<div class=\"message" + (success ? " success" : String.Empty) + "\">" +
                System.Web.HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
