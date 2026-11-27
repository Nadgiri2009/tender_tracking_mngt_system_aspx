using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class Tenders : ModulePage
    {
        protected HyperLink NewTenderLink;
        protected Literal Message;
        protected TextBox SearchText;
        protected DropDownList StatusFilter, CategoryFilter;
        protected TextBox DateFrom, DateTo;
        protected DropDownList DepartmentFilter;
        protected Button FilterButton;
        protected GridView TenderGrid;
        protected Literal PageSummary;

        private string SortBy
        {
            get { return ViewState["SortBy"] as string ?? "CreatedDate"; }
            set { ViewState["SortBy"] = value; }
        }

        private bool SortDescending
        {
            get { return ViewState["SortDescending"] == null || (bool)ViewState["SortDescending"]; }
            set { ViewState["SortDescending"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (HasAnyRole("Contractor"))
            {
                Response.Redirect(ResolveUrl("~/Pages/Home.aspx#published-tenders"), false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!HasPermission("system.admin") && !HasPermission("tenders.monitor") &&
                !HasPermission("tenders.manage.all") && !HasPermission("tender.create.department") &&
                !HasPermission("tender.read.department") &&
                !HasPermission("tender.publication.manage") && !HasPermission("tender.primary.open") &&
                !HasPermission("tender.primary.verify") && !HasPermission("tender.cell.verify") &&
                !HasPermission("shortfall.create") && !HasPermission("shortfall.approve") &&
                !HasPermission("shortfall.respond.own") && !HasPermission("shortfall.clear") &&
                !HasPermission("financial.open.approve") && !HasPermission("financial.open.execute") &&
                !HasPermission("financial.approve.final") && !HasPermission("workorder.manage") &&
                !HasPermission("agreement.manage") && WorkflowRules.PendingStatusesForRole(CurrentRole).Length == 0)
            {
                RequirePermission("tenders.monitor");
                return;
            }

            NewTenderLink.Visible = HasPermission("tender.create.department") ||
                HasPermission("tenders.manage.all") || HasAnyRole("DepartmentOfficer");
            if (!IsPostBack)
            {
                LoadFilters();
                BindTenders();
            }
        }

        private void LoadFilters()
        {
            StatusFilter.Items.Clear();
            StatusFilter.Items.Add(new ListItem("All statuses", ""));
            StatusFilter.Items.Add(new ListItem("Draft", "Draft"));
            StatusFilter.Items.Add(new ListItem("Published", "Published"));
            StatusFilter.Items.Add(new ListItem("Active", "Active"));
            StatusFilter.Items.Add(new ListItem("Closed", "Closed"));
            StatusFilter.Items.Add(new ListItem("Cancelled", "Cancelled"));

            using (SqlConnection connection = ModuleDb.OpenConnection())
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT DISTINCT COALESCE(TenderCategory,N'Uncategorized') AS TenderCategory
                      FROM dbo.Tender WHERE IsDeleted=0 ORDER BY TenderCategory;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    CategoryFilter.Items.Clear();
                    CategoryFilter.Items.Add(new ListItem("All categories", ""));
                    while (reader.Read())
                    {
                        string category = reader.GetString(0);
                        CategoryFilter.Items.Add(new ListItem(category, category));
                    }
                }

                using (SqlCommand command = new SqlCommand("SELECT DepartmentId, DepartmentName FROM dbo.Departments WHERE IsActive=1 ORDER BY DepartmentName;", connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        DepartmentFilter.Items.Clear();
                        DepartmentFilter.Items.Add(new ListItem("All departments", ""));
                        while (reader.Read())
                            DepartmentFilter.Items.Add(new ListItem(reader.GetString(1), reader.GetInt32(0).ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }

            string requestedBucket = Request.QueryString["bucket"];
            string requestedStatus = Request.QueryString["status"];
            int requestedDepartmentId;
            string requestedDepartment = Request.QueryString["departmentId"];
            if (Int32.TryParse(requestedDepartment, NumberStyles.None, CultureInfo.InvariantCulture,
                    out requestedDepartmentId) &&
                DepartmentFilter.Items.FindByValue(requestedDepartmentId.ToString(CultureInfo.InvariantCulture)) != null)
                DepartmentFilter.SelectedValue = requestedDepartmentId.ToString(CultureInfo.InvariantCulture);
            if (!String.IsNullOrWhiteSpace(requestedBucket) && StatusFilter.Items.FindByValue(requestedBucket) != null)
                StatusFilter.SelectedValue = requestedBucket;
            else if (!String.IsNullOrWhiteSpace(requestedStatus) && StatusFilter.Items.FindByValue(requestedStatus) != null)
                StatusFilter.SelectedValue = requestedStatus;
        }

        private void BindTenders()
        {
            string orderColumn = SortBy == "Title" ? "t.Title" : SortBy == "Amount" ? "t.EstimatedAmount" : SortBy == "TenderNumber" ? "t.TenderNumber" : "t.CreatedDate";
            string orderDirection = SortDescending ? "DESC" : "ASC";
            int page = TenderGrid.PageIndex + 1;
            bool isDepartmentOfficer = !HasPermission("system.admin") &&
                (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                 String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase));
            string ownerFilter = isDepartmentOfficer ? " AND t.CreatedBy=@CurrentUserId" : String.Empty;
            string workflowScope = String.Empty;
            if (!HasPermission("system.admin") && HasPermission("tender.read.department"))
                workflowScope = " AND @AuthorizedDepartmentId IS NOT NULL AND t.DepartmentId=@AuthorizedDepartmentId";
            else if (!HasPermission("system.admin") &&
                HasAnyRole("Nivida Kaksh (Tender Cell)", "Nivida Samiti Adhyaksha", "Chief Auditor"))
                workflowScope = @" AND t.WorkflowVersion=2 AND
                                   (EXISTS
                                    (SELECT 1 FROM dbo.TenderWorkflowAssignments scopeAssignment
                                     WHERE scopeAssignment.TenderId=t.TenderId AND scopeAssignment.IsCurrent=1
                                       AND scopeAssignment.AssignedRoleId=@WorkflowRoleId
                                       AND (scopeAssignment.AssignedUserId IS NULL OR scopeAssignment.AssignedUserId=@WorkflowUserId))
                                    OR EXISTS
                                    (SELECT 1 FROM dbo.TenderWorkflowHistory priorAction
                                     WHERE priorAction.TenderId=t.TenderId
                                       AND priorAction.WorkflowVersion=2
                                       AND priorAction.RoleId=@WorkflowRoleId
                                       AND priorAction.UserId=@WorkflowUserId))";
            string statusBucket = TenderStatusBuckets.Predicate(StatusFilter.SelectedValue);
            object dateFrom;
            object dateTo;
            if (!TryGetDateFilters(out dateFrom, out dateTo))
            {
                TenderGrid.DataSource = null;
                TenderGrid.DataBind();
                PageSummary.Text = "Date filters are invalid.";
                return;
            }
            string filter = @" FROM dbo.Tender t INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                               LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                               LEFT JOIN dbo.Users u ON u.UserId=t.CreatedBy
                               WHERE t.IsDeleted=0 AND (@Search=N'' OR t.Title LIKE @SearchPattern OR t.TenderNumber LIKE @SearchPattern)
                               AND (@Status=N'' OR t.CurrentStatus=@Status" +
                               (statusBucket == null ? String.Empty : " OR (" + statusBucket + ")") +
                               @")
                               AND (@Category=N'' OR (@Category=N'Uncategorized' AND t.TenderCategory IS NULL)
                                    OR t.TenderCategory=@Category)
                               AND (@DateFrom IS NULL OR COALESCE(s.CurrentClosingUtc,t.SubmissionDeadline)>=@DateFrom)
                               AND (@DateTo IS NULL OR COALESCE(s.CurrentClosingUtc,t.SubmissionDeadline)<DATEADD(DAY,1,@DateTo))
                               AND (@DepartmentId=0 OR t.DepartmentId=@DepartmentId)" + ownerFilter + workflowScope;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    int count;
                    using (SqlCommand countCommand = new SqlCommand("SELECT COUNT(*) " + filter, connection))
                    {
                        AddFilterParameters(countCommand, dateFrom, dateTo);
                        AddOwnerParameter(countCommand);
                        AddWorkflowScopeParameters(countCommand);
                        count = Convert.ToInt32(countCommand.ExecuteScalar());
                    }
                    int pages = Math.Max(1, (int)Math.Ceiling(count / 10.0));
                    if (page > pages) { TenderGrid.PageIndex = pages - 1; page = pages; }
                    TenderGrid.VirtualItemCount = count;
                    using (SqlCommand listCommand = new SqlCommand(
                        @"SELECT t.TenderId,t.TenderNumber,t.Title,t.TenderType,
                                 COALESCE(t.TenderCategory,N'Uncategorized') AS TenderCategory,d.DepartmentName,
                                 COALESCE(u.FullName,N'') AS AssignedOfficer,t.EstimatedAmount,
                                 COALESCE((SELECT MIN(ah.ApprovalDate) FROM dbo.ApprovalHistory ah
                                           WHERE ah.TenderId=t.TenderId AND ah.NewStatus=N'Tender Published'),
                                          s.PublicationStartUtc) AS PublicationDate,
                                 COALESCE(s.PublicationStartUtc,t.SubmissionStartDate) AS SubmissionStartDate,
                                 COALESCE(s.CurrentClosingUtc,t.SubmissionDeadline) AS SubmissionDeadline,
                                 t.CurrentStatus,t.CreatedDate " + filter +
                        " ORDER BY " + orderColumn + " " + orderDirection + " OFFSET @Offset ROWS FETCH NEXT 10 ROWS ONLY;", connection))
                    {
                        AddFilterParameters(listCommand, dateFrom, dateTo);
                        AddOwnerParameter(listCommand);
                        AddWorkflowScopeParameters(listCommand);
                        ModuleDb.AddParameter(listCommand, "@Offset", SqlDbType.Int, (page - 1) * 10);
                        using (SqlDataAdapter adapter = new SqlDataAdapter(listCommand))
                        {
                            DataTable table = new DataTable();
                            adapter.Fill(table);
                            TenderGrid.DataSource = table;
                            TenderGrid.DataBind();
                        }
                    }
                    PageSummary.Text = String.Format(CultureInfo.CurrentCulture, "Page {0} of {1} · {2} tenders total", page, pages, count);
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Could not retrieve tenders.", ex);
                Message.Text = "<div class=\"message\">Tender records could not be loaded. Please retry or contact support.</div>";
            }
        }

        private void AddFilterParameters(SqlCommand command, object dateFrom, object dateTo)
        {
            string search = SearchText.Text == null ? String.Empty : SearchText.Text.Trim();
            AddFilterText(command, "@Search", search, 100);
            AddText(command, "@SearchPattern", SqlDbType.NVarChar, "%" + search + "%", 102);
            AddFilterText(command, "@Status", StatusFilter.SelectedValue, 50);
            AddFilterText(command, "@Category", CategoryFilter.SelectedValue, 50);
            ModuleDb.AddParameter(command, "@DateFrom", SqlDbType.Date, dateFrom);
            ModuleDb.AddParameter(command, "@DateTo", SqlDbType.Date, dateTo);
            int departmentId;
            ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                Int32.TryParse(DepartmentFilter.SelectedValue, out departmentId) ? departmentId : 0);
        }

        private static void AddFilterText(SqlCommand command, string name, string value, int size)
        {
            ModuleDb.AddParameter(command, name, SqlDbType.NVarChar,
                String.IsNullOrWhiteSpace(value) ? String.Empty : value.Trim(), size);
        }

        private bool TryGetDateFilters(out object dateFrom, out object dateTo)
        {
            dateFrom = null;
            dateTo = null;
            DateTime parsed;
            if (!String.IsNullOrWhiteSpace(DateFrom.Text))
            {
                if (!DateTime.TryParseExact(DateFrom.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                {
                    Message.Text = "<div class=\"message\">Enter a valid deadline start date.</div>";
                    return false;
                }
                dateFrom = parsed.Date;
            }
            if (!String.IsNullOrWhiteSpace(DateTo.Text))
            {
                if (!DateTime.TryParseExact(DateTo.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                {
                    Message.Text = "<div class=\"message\">Enter a valid deadline end date.</div>";
                    return false;
                }
                dateTo = parsed.Date;
            }
            if (dateFrom != null && dateTo != null && (DateTime)dateFrom > (DateTime)dateTo)
            {
                Message.Text = "<div class=\"message\">The deadline start date must be on or before the end date.</div>";
                return false;
            }
            Message.Text = String.Empty;
            return true;
        }

        private void AddOwnerParameter(SqlCommand command)
        {
            if (!HasPermission("system.admin") &&
                (String.Equals(CurrentRole, "DepartmentOfficer", StringComparison.OrdinalIgnoreCase) ||
                 String.Equals(CurrentRoleCode, "DEPARTMENT_OFFICER", StringComparison.OrdinalIgnoreCase)))
                ModuleDb.AddParameter(command, "@CurrentUserId", SqlDbType.Int, CurrentUserId);
        }

        private void AddWorkflowScopeParameters(SqlCommand command)
        {
            if (!HasPermission("system.admin") && HasPermission("tender.read.department"))
                ModuleDb.AddParameter(command, "@AuthorizedDepartmentId", SqlDbType.Int,
                    CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
            else if (!HasPermission("system.admin") &&
                HasAnyRole("Nivida Kaksh (Tender Cell)", "Nivida Samiti Adhyaksha", "Chief Auditor"))
            {
                ModuleDb.AddParameter(command, "@WorkflowRoleId", SqlDbType.Int, CurrentRoleId);
                ModuleDb.AddParameter(command, "@WorkflowUserId", SqlDbType.Int, CurrentUserId);
            }
        }

        protected void FilterButton_Click(object sender, EventArgs e)
        {
            TenderGrid.PageIndex = 0;
            BindTenders();
        }

        protected void TenderGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            TenderGrid.PageIndex = e.NewPageIndex;
            BindTenders();
        }

        protected void TenderGrid_Sorting(object sender, GridViewSortEventArgs e)
        {
            string key = e.SortExpression;
            if (key != "TenderNumber" && key != "Title" && key != "Amount" && key != "CreatedDate") key = "CreatedDate";
            SortDescending = SortBy == key ? !SortDescending : false;
            SortBy = key;
            TenderGrid.PageIndex = 0;
            BindTenders();
        }

        protected string TenderStatusCssClass(object status)
        {
            return TenderStatusBuckets.CssClass(Convert.ToString(status, CultureInfo.CurrentCulture));
        }

        protected int TenderSerialNumber(int rowIndex)
        {
            return TenderGrid.PageIndex * TenderGrid.PageSize + rowIndex + 1;
        }
    }
}
