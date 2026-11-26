using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web.UI;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class Home : ModulePage
    {
        private const int TendersPerPage = 10;
        private const string PageIndexKey = "HomeTenderPageIndex";
        private const string PublicTenderVisibility = TenderStatusBuckets.PublicTenderVisibility;
        private const string PublicTenderSource =
            " FROM dbo.Tender t INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId " +
            "INNER JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId WHERE " + PublicTenderVisibility;
        private const string PublicTenderFilter =
            PublicTenderSource +
            @" AND (@Search=N'' OR t.TenderNumber LIKE @SearchPattern OR t.Title LIKE @SearchPattern)
               AND (@DepartmentId=0 OR t.DepartmentId=@DepartmentId)
               AND (@Category=N'' OR COALESCE(NULLIF(t.TenderCategory,N''),N'Uncategorized')=@Category)
               AND (@TenderType=N'' OR t.TenderType=@TenderType)
               AND (@TenderStatus=N'' OR
                    (@TenderStatus=N'open' AND t.CurrentStatus=N'Tender Published'
                     AND s.CurrentClosingUtc>SYSUTCDATETIME()
                     AND (t.SubmissionStartDate IS NULL OR t.SubmissionStartDate<=SYSUTCDATETIME())
                     AND (t.SubmissionDeadline IS NULL OR t.SubmissionDeadline>=SYSUTCDATETIME())) OR
                    (@TenderStatus=N'completed' AND " + TenderStatusBuckets.Awarded + @"))";

        protected Literal Message, TotalTenders, PublishedTenders, ActiveTenders, ClosedTenders,
            TotalContractors, CompletedAwardedTenders, PageSummary;
        protected TextBox TenderSearchText;
        protected DropDownList DepartmentFilter, CategoryFilter, TenderTypeFilter, TenderStatusFilter, SortBySelect;
        protected GridView TendersGrid, AnnouncementsGrid;
        protected LinkButton PreviousPageButton, NextPageButton;
        protected Repeater PageNumbers;
        protected Panel InternalTendersPanel;
        protected GridView InternalTendersGrid;
        protected Literal InternalTendersSummary;

        protected override bool RequiresAuthentication
        {
            get { return false; }
        }

        private int CurrentPage
        {
            get { return ViewState[PageIndexKey] is int ? (int)ViewState[PageIndexKey] : 1; }
            set { ViewState[PageIndexKey] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                LoadPortalData();
                string requestedStatus = Request.QueryString["tenderStatus"];
                if (requestedStatus == "open" || requestedStatus == "completed")
                    TenderStatusFilter.SelectedValue = requestedStatus;
                if (!String.IsNullOrEmpty(requestedStatus))
                {
                    CurrentPage = 1;
                    BindCurrentPage();
                }
            }
            BindInternalTenders();
        }

        private void LoadPortalData()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    BindTenderFilterOptions(connection);
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT
                            (SELECT COUNT(*) FROM dbo.Tender t WHERE t.IsDeleted=0) AS TotalTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Published + @") AS PublishedTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             INNER JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Active + @") AS ActiveTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Closed + @") AS ClosedTenders,
                            (SELECT COUNT(*) FROM dbo.Contractors) AS TotalContractors,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Awarded + @") AS CompletedAwardedTenders;",
                        connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            TotalTenders.Text = Convert.ToString(reader["TotalTenders"]);
                            PublishedTenders.Text = Convert.ToString(reader["PublishedTenders"]);
                            ActiveTenders.Text = Convert.ToString(reader["ActiveTenders"]);
                            ClosedTenders.Text = Convert.ToString(reader["ClosedTenders"]);
                            TotalContractors.Text = Convert.ToString(reader["TotalContractors"]);
                            CompletedAwardedTenders.Text = Convert.ToString(reader["CompletedAwardedTenders"]);
                        }
                    }

                    BindTenders(connection);
                    Bind(connection, AnnouncementsGrid,
                        @"SELECT TOP (8) Title,Body,PublishDate,ExpiryDate FROM dbo.Announcements
                          WHERE IsDeleted=0 AND IsPublished=1
                            AND (PublishDate IS NULL OR PublishDate<=SYSUTCDATETIME())
                            AND (ExpiryDate IS NULL OR ExpiryDate>=SYSUTCDATETIME())
                          ORDER BY COALESCE(PublishDate,CreatedDate) DESC;");
                }
            }
            catch (SqlException ex)
            {
                ShowLoadError("Public portal content could not be loaded.", ex);
            }
            catch (ConfigurationErrorsException ex)
            {
                ShowLoadError("Public portal database configuration is invalid.", ex);
            }
        }

        private void BindInternalTenders()
        {
            InternalTendersPanel.Visible = String.Equals(
                CurrentAccountType, "Internal", StringComparison.OrdinalIgnoreCase);
            if (!InternalTendersPanel.Visible) return;

            bool isDepartmentOfficer = String.Equals(CurrentRole, "DepartmentOfficer",
                StringComparison.OrdinalIgnoreCase) ||
                String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase);
            bool canReadDepartment = HasPermission("tender.read.department");
            string accessScope;
            if (IsJeAdministrator)
            {
                accessScope = String.Empty;
            }
            else if (isDepartmentOfficer)
            {
                accessScope = " AND t.CreatedBy=@InternalUserId";
            }
            else if (canReadDepartment)
            {
                accessScope = " AND @InternalDepartmentId IS NOT NULL AND t.DepartmentId=@InternalDepartmentId";
            }
            else
            {
                accessScope = @" AND EXISTS
                    (
                        SELECT 1 FROM dbo.TenderWorkflowAssignments a
                        WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                          AND a.AssignedRoleId=@InternalRoleId
                          AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@InternalUserId)
                    )";
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT t.TenderId,t.TenderNumber,t.Title,t.CurrentStatus,t.CreatedDate,d.DepartmentName
                      FROM dbo.Tender t
                      INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                      WHERE t.IsDeleted=0
                        AND NOT EXISTS
                        (
                            SELECT 1 FROM dbo.ApprovalHistory ah
                            WHERE ah.TenderId=t.TenderId AND ah.NewStatus=N'Tender Published'
                        )
                        AND NOT EXISTS
                        (
                            SELECT 1 FROM dbo.TenderWorkflowHistory wh
                            WHERE wh.TenderId=t.TenderId AND wh.WorkflowVersion=2
                              AND wh.NewStatus=N'Tender Published'
                        )" + accessScope + @"
                      ORDER BY t.CreatedDate DESC,t.TenderId DESC;", connection))
                {
                    if (!IsJeAdministrator)
                    {
                        if (isDepartmentOfficer)
                        {
                            ModuleDb.AddParameter(command, "@InternalUserId", SqlDbType.Int, CurrentUserId);
                        }
                        else if (canReadDepartment)
                        {
                            ModuleDb.AddParameter(command, "@InternalDepartmentId", SqlDbType.Int,
                                CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        }
                        else
                        {
                            ModuleDb.AddParameter(command, "@InternalUserId", SqlDbType.Int, CurrentUserId);
                            ModuleDb.AddParameter(command, "@InternalRoleId", SqlDbType.Int, CurrentRoleId);
                        }
                    }

                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        DataTable data = new DataTable();
                        adapter.Fill(data);
                        InternalTendersGrid.DataSource = data;
                        InternalTendersGrid.DataBind();
                        InternalTendersSummary.Text = String.Format(
                            CultureInfo.CurrentCulture, "<p class=\"muted\">{0} unpublished tender(s)</p>", data.Rows.Count);
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Internal unpublished tenders could not be loaded.", ex);
                Message.Text = "<div class=\"message\">Internal tender records could not be loaded. Please retry or contact support.</div>";
            }
        }

        private void BindTenders(SqlConnection connection)
        {
            if (TenderSearchText.Text.Trim().Length > 100)
            {
                Message.Text = "<div class=\"message\">Search text must be 100 characters or fewer.</div>";
                return;
            }

            int count;
            using (SqlCommand countCommand = new SqlCommand("SELECT COUNT(*)" + PublicTenderFilter + ";", connection))
            {
                AddTenderFilterParameters(countCommand);
                count = Convert.ToInt32(countCommand.ExecuteScalar());
            }

            int pageCount = Math.Max(1, (int)Math.Ceiling(count / (double)TendersPerPage));
            CurrentPage = Math.Max(1, Math.Min(CurrentPage, pageCount));
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.TenderId,t.TenderNumber,t.Title,t.TenderType,t.EstimatedAmount,
                         COALESCE(t.TenderCategory,N'Uncategorized') AS TenderCategory,d.DepartmentName,
                         COALESCE((SELECT MIN(ah.ApprovalDate)
                                   FROM dbo.ApprovalHistory ah
                                   WHERE ah.TenderId=t.TenderId AND ah.NewStatus=N'Tender Published'),
                                  t.PublicationDate,s.PublicationStartUtc) AS PublicationDate,
                         s.CurrentClosingUtc,t.CurrentStatus,
                         CASE WHEN t.CurrentStatus=N'Tender Published'
                                   AND s.CurrentClosingUtc>SYSUTCDATETIME()
                                   AND (t.SubmissionStartDate IS NULL OR t.SubmissionStartDate<=SYSUTCDATETIME())
                                   AND (t.SubmissionDeadline IS NULL OR t.SubmissionDeadline>=SYSUTCDATETIME())
                              THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsApplicationOpen" + PublicTenderFilter + @"
                  ORDER BY " + GetTenderSortExpression() + @"
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", connection))
            {
                AddTenderFilterParameters(command);
                ModuleDb.AddParameter(command, "@Offset", SqlDbType.Int, (CurrentPage - 1) * TendersPerPage);
                ModuleDb.AddParameter(command, "@PageSize", SqlDbType.Int, TendersPerPage);
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable tenders = new DataTable();
                    adapter.Fill(tenders);
                    TenderLifecycleFiles.ConvertDatesToIndia(
                        tenders, "CurrentClosingUtc");
                    TendersGrid.DataSource = tenders;
                    TendersGrid.DataBind();
                }
            }

            BindPageNumbers(pageCount);
            PreviousPageButton.Enabled = CurrentPage > 1;
            NextPageButton.Enabled = CurrentPage < pageCount;
            PageSummary.Text = String.Format(
                "Page {0} of {1} · {2} tenders total · Showing {3}-{4}",
                CurrentPage,
                pageCount,
                count,
                count == 0 ? 0 : (CurrentPage - 1) * TendersPerPage + 1,
                Math.Min(CurrentPage * TendersPerPage, count));
        }

        private void BindTenderFilterOptions(SqlConnection connection)
        {
            DepartmentFilter.Items.Clear();
            DepartmentFilter.Items.Add(new ListItem("All departments", "0"));
            using (SqlCommand command = new SqlCommand(
                "SELECT DISTINCT d.DepartmentId,d.DepartmentName" + PublicTenderSource +
                " ORDER BY d.DepartmentName;", connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                    DepartmentFilter.Items.Add(new ListItem(
                        Convert.ToString(reader["DepartmentName"]),
                        Convert.ToString(reader["DepartmentId"], CultureInfo.InvariantCulture)));
            }

            CategoryFilter.Items.Clear();
            CategoryFilter.Items.Add(new ListItem("All categories", String.Empty));
            using (SqlCommand command = new SqlCommand(
                "SELECT DISTINCT COALESCE(NULLIF(t.TenderCategory,N''),N'Uncategorized') AS Category" +
                PublicTenderSource + " ORDER BY Category;", connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string category = Convert.ToString(reader["Category"]);
                    CategoryFilter.Items.Add(new ListItem(category, category));
                }
            }

            TenderTypeFilter.Items.Clear();
            TenderTypeFilter.Items.Add(new ListItem("All tender types", String.Empty));
            using (SqlCommand command = new SqlCommand(
                "SELECT DISTINCT t.TenderType" + PublicTenderSource + " ORDER BY t.TenderType;", connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string tenderType = Convert.ToString(reader["TenderType"]);
                    TenderTypeFilter.Items.Add(new ListItem(tenderType, tenderType));
                }
            }
        }

        private void AddTenderFilterParameters(SqlCommand command)
        {
            string search = TenderSearchText.Text.Trim();
            int departmentId;
            if (!Int32.TryParse(DepartmentFilter.SelectedValue, NumberStyles.None, CultureInfo.InvariantCulture, out departmentId))
                departmentId = 0;
            ModuleDb.AddParameter(command, "@Search", SqlDbType.NVarChar, search, 100);
            ModuleDb.AddParameter(command, "@SearchPattern", SqlDbType.NVarChar, "%" + search + "%", 202);
            ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
            ModuleDb.AddParameter(command, "@Category", SqlDbType.NVarChar, CategoryFilter.SelectedValue, 100);
            ModuleDb.AddParameter(command, "@TenderType", SqlDbType.NVarChar, TenderTypeFilter.SelectedValue, 100);
            ModuleDb.AddParameter(command, "@TenderStatus", SqlDbType.NVarChar, TenderStatusFilter.SelectedValue, 20);
        }

        private string GetTenderSortExpression()
        {
            switch (SortBySelect.SelectedValue)
            {
                case "closing":
                    return "s.CurrentClosingUtc ASC,t.TenderId DESC";
                case "amount":
                    return "t.EstimatedAmount DESC,t.TenderId DESC";
                case "title":
                    return "t.Title ASC,t.TenderId DESC";
                default:
                    return "COALESCE(t.PublicationDate,(SELECT MIN(ah.ApprovalDate) " +
                        "FROM dbo.ApprovalHistory ah WHERE ah.TenderId=t.TenderId " +
                        "AND ah.NewStatus=N'Tender Published'),s.PublicationStartUtc) DESC,t.TenderId DESC";
            }
        }

        private void BindPageNumbers(int pageCount)
        {
            DataTable pages = new DataTable();
            pages.Columns.Add("PageNumber", typeof(int));
            pages.Columns.Add("IsCurrent", typeof(bool));
            int firstPage = Math.Max(1, Math.Min(CurrentPage - 3, pageCount - 6));
            int lastPage = Math.Min(pageCount, firstPage + 6);
            for (int page = firstPage; page <= lastPage; page++)
            {
                pages.Rows.Add(page, page == CurrentPage);
            }
            PageNumbers.DataSource = pages;
            PageNumbers.DataBind();
        }

        protected int TenderSerialNumber(int rowIndex)
        {
            return (CurrentPage - 1) * TendersPerPage + rowIndex + 1;
        }

        protected void PreviousPageButton_Click(object sender, EventArgs e)
        {
            CurrentPage--;
            BindCurrentPage();
        }

        protected void NextPageButton_Click(object sender, EventArgs e)
        {
            CurrentPage++;
            BindCurrentPage();
        }

        protected void PageNumbers_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int requestedPage;
            if (Int32.TryParse(Convert.ToString(e.CommandArgument), out requestedPage) && requestedPage > 0)
            {
                CurrentPage = requestedPage;
                BindCurrentPage();
            }
        }

        protected void ApplyTenderFiltersButton_Click(object sender, EventArgs e)
        {
            CurrentPage = 1;
            BindCurrentPage();
        }

        protected void ClearTenderFiltersButton_Click(object sender, EventArgs e)
        {
            TenderSearchText.Text = String.Empty;
            DepartmentFilter.SelectedIndex = 0;
            CategoryFilter.SelectedIndex = 0;
            TenderTypeFilter.SelectedIndex = 0;
            TenderStatusFilter.SelectedIndex = 0;
            SortBySelect.SelectedIndex = 0;
            CurrentPage = 1;
            BindCurrentPage();
        }

        private void BindCurrentPage()
        {
            Message.Text = String.Empty;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    BindTenders(connection);
                }
            }
            catch (SqlException ex)
            {
                ShowLoadError("Tender records could not be loaded.", ex);
            }
            catch (ConfigurationErrorsException ex)
            {
                ShowLoadError("Tender database configuration is invalid.", ex);
            }
        }

        private void ShowLoadError(string message, Exception exception)
        {
            Trace.Warn("DepartmentModule", message, exception);
            Message.Text = "<div class=\"message\">" + message + " Please retry or contact support.</div>";
            TendersGrid.DataSource = null;
            TendersGrid.DataBind();
            PageNumbers.DataSource = null;
            PageNumbers.DataBind();
            PreviousPageButton.Enabled = false;
            NextPageButton.Enabled = false;
            PageSummary.Text = "Tender list unavailable.";
        }

        private static void Bind(SqlConnection connection, GridView grid, string sql)
        {
            using (SqlCommand command = new SqlCommand(sql, connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                DataTable data = new DataTable();
                adapter.Fill(data);
                grid.DataSource = data;
                grid.DataBind();
            }
        }
    }
}
