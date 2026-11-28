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
        protected TextBox TenderNumberText, TenderTitleText, DateFrom, DateTo, DeadlineFrom, DeadlineTo;
        protected DropDownList StatusFilter, CategoryFilter, DepartmentFilter, WorkflowStageFilter,
            AssignedOfficerFilter, PendingTaskFilter, PublicationStatusFilter;
        protected Button FilterButton, ClearFiltersButton;
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
            StatusFilter.Items.Add(new ListItem("Pending", "Pending"));
            StatusFilter.Items.Add(new ListItem("Approved", "Approved"));
            StatusFilter.Items.Add(new ListItem("Under review", "Under Review"));
            StatusFilter.Items.Add(new ListItem("Draft / submitted", "Draft / Submitted"));
            StatusFilter.Items.Add(new ListItem("Published", "Published"));
            StatusFilter.Items.Add(new ListItem("Active", "Active"));
            StatusFilter.Items.Add(new ListItem("Closed", "Closed"));
            StatusFilter.Items.Add(new ListItem("Cancelled", "Cancelled"));
            StatusFilter.Items.Add(new ListItem("Awarded", "Awarded"));

            using (SqlConnection connection = ModuleDb.OpenConnection())
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT DISTINCT CurrentStatus FROM dbo.Tender
                      WHERE IsDeleted=0 AND CurrentStatus IS NOT NULL ORDER BY CurrentStatus;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string status = reader.GetString(0);
                        if (StatusFilter.Items.FindByValue(status) == null)
                            StatusFilter.Items.Add(new ListItem(status, status));
                    }
                }

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

                using (SqlCommand command = new SqlCommand(
                    @"SELECT DISTINCT StageName FROM dbo.WorkflowStages
                      WHERE WorkflowVersion=2 AND IsActive=1 ORDER BY StageName;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    WorkflowStageFilter.Items.Clear();
                    WorkflowStageFilter.Items.Add(new ListItem("All workflow stages", ""));
                    while (reader.Read())
                    {
                        string stageName = reader.GetString(0);
                        WorkflowStageFilter.Items.Add(new ListItem(stageName, stageName));
                    }
                }

                using (SqlCommand command = new SqlCommand(
                    @"SELECT DISTINCT u.UserId,u.FullName
                      FROM dbo.TenderWorkflowAssignments a
                      INNER JOIN dbo.Users u ON u.UserId=a.AssignedUserId AND u.IsActive=1
                      WHERE a.IsCurrent=1 ORDER BY u.FullName;", connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    AssignedOfficerFilter.Items.Clear();
                    AssignedOfficerFilter.Items.Add(new ListItem("All officers", ""));
                    while (reader.Read())
                        AssignedOfficerFilter.Items.Add(new ListItem(reader.GetString(1),
                            reader.GetInt32(0).ToString(CultureInfo.InvariantCulture)));
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
            string orderColumn = SortBy == "Title" ? "t.Title" :
                SortBy == "TenderNumber" ? "t.TenderNumber" :
                SortBy == "ModifiedDate" ? "t.ModifiedDate" :
                SortBy == "AssignedOfficer" ? "AssignedOfficer" :
                SortBy == "CurrentStage" ? "CurrentStage" :
                SortBy == "CurrentStatus" ? "t.CurrentStatus" : "t.CreatedDate";
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
            object createdFrom;
            object createdTo;
            object deadlineFrom;
            object deadlineTo;
            if (!TryGetDateFilters(out createdFrom, out createdTo, out deadlineFrom, out deadlineTo))
            {
                TenderGrid.DataSource = null;
                TenderGrid.DataBind();
                PageSummary.Text = "Date filters are invalid.";
                return;
            }
            string filter = @" FROM dbo.Tender t INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                               LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                               LEFT JOIN dbo.Users creator ON creator.UserId=t.CreatedBy
                               OUTER APPLY
                               (
                                   SELECT TOP (1) a.StageId,a.AssignedRoleId,a.AssignedUserId,a.DepartmentId,
                                          ws.StageName,ws.StageOrder
                                   FROM dbo.TenderWorkflowAssignments a
                                   INNER JOIN dbo.WorkflowStages ws ON ws.StageId=a.StageId
                                   WHERE a.TenderId=t.TenderId AND a.IsCurrent=1 AND ws.WorkflowVersion=2
                                   ORDER BY a.AssignedAtUtc DESC,a.AssignmentId DESC
                               ) currentWork
                               LEFT JOIN dbo.Roles assignedRole ON assignedRole.RoleId=currentWork.AssignedRoleId
                               LEFT JOIN dbo.Users assignedUser ON assignedUser.UserId=currentWork.AssignedUserId
                               OUTER APPLY
                               (
                                   SELECT CASE
                                       WHEN EXISTS
                                           (SELECT 1 FROM dbo.ApprovalHistory ph
                                            WHERE ph.TenderId=t.TenderId AND ph.NewStatus=N'Tender Published')
                                         OR EXISTS
                                           (SELECT 1 FROM dbo.TenderWorkflowHistory pwh
                                            WHERE pwh.TenderId=t.TenderId AND pwh.WorkflowVersion=2
                                              AND pwh.NewStatus=N'Tender Published')
                                         OR t.CurrentStatus=N'Tender Published'
                                       THEN CASE
                                           WHEN s.PublicationStartUtc>SYSUTCDATETIME() THEN N'Scheduled'
                                           WHEN COALESCE(s.CurrentClosingUtc,t.SubmissionDeadline)<SYSUTCDATETIME() THEN N'Closed'
                                           WHEN s.PublicationStartUtc IS NULL THEN N'Published'
                                           ELSE N'Active'
                                       END
                                       WHEN t.CurrentStatus IN (N'Active',N'Closed') THEN t.CurrentStatus
                                       ELSE N'Not published'
                                   END AS PublicationStatus
                               ) publication
                               WHERE t.IsDeleted=0
                               AND (@TenderNumber=N'' OR t.TenderNumber LIKE @TenderNumberPattern)
                               AND (@TenderTitle=N'' OR t.Title LIKE @TenderTitlePattern)
                               AND (@Status=N'' OR t.CurrentStatus=@Status" +
                               (statusBucket == null ? String.Empty : " OR (" + statusBucket + ")") +
                               @")
                               AND (@Category=N'' OR (@Category=N'Uncategorized' AND t.TenderCategory IS NULL)
                                    OR t.TenderCategory=@Category)
                               AND (@CreatedFrom IS NULL OR t.CreatedDate>=@CreatedFrom)
                               AND (@CreatedTo IS NULL OR t.CreatedDate<DATEADD(DAY,1,@CreatedTo))
                               AND (@DeadlineFrom IS NULL OR COALESCE(s.CurrentClosingUtc,t.SubmissionDeadline)>=@DeadlineFrom)
                               AND (@DeadlineTo IS NULL OR COALESCE(s.CurrentClosingUtc,t.SubmissionDeadline)<DATEADD(DAY,1,@DeadlineTo))
                               AND (@DepartmentId=0 OR t.DepartmentId=@DepartmentId)
                               AND (@WorkflowStage=N'' OR currentWork.StageName=@WorkflowStage)
                               AND (@AssignedOfficerId=0 OR currentWork.AssignedUserId=@AssignedOfficerId)
                               AND (@PendingTask=N'' OR
                                    (@PendingTask=N'pending' AND currentWork.StageName IS NOT NULL AND t.WorkflowVersion=2)
                                    OR (@PendingTask=N'none' AND
                                        (currentWork.StageName IS NULL OR t.WorkflowVersion<>2)))
                               AND (@PublicationStatus=N'' OR publication.PublicationStatus=@PublicationStatus)" +
                               ownerFilter + workflowScope;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    int count;
                    using (SqlCommand countCommand = new SqlCommand("SELECT COUNT(*) " + filter, connection))
                    {
                        AddFilterParameters(countCommand, createdFrom, createdTo, deadlineFrom, deadlineTo);
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
                                 COALESCE(assignedUser.FullName,assignedRole.RoleName,creator.FullName,N'—') AS AssignedOfficer,
                                 COALESCE(currentWork.StageName,N'—') AS CurrentStage,
                                 publication.PublicationStatus,
                                 t.CurrentStatus,t.CreatedDate,t.ModifiedDate,
                                 t.WorkflowVersion,t.DepartmentId,
                                 currentWork.StageName AS PendingStage,
                                 currentWork.AssignedRoleId,currentWork.AssignedUserId,
                                 currentWork.DepartmentId AS AssignedDepartmentId,
                                 (SELECT COUNT(DISTINCT COALESCE(dt.TypeName,doc.DocumentType))
                                  FROM dbo.TenderDocuments doc
                                  LEFT JOIN dbo.DocumentTypes dt ON dt.DocumentTypeId=doc.DocumentTypeId
                                  WHERE doc.TenderId=t.TenderId AND doc.IsDeleted=0
                                    AND COALESCE(dt.TypeName,doc.DocumentType) IN
                                        (N'Scope of Work',N'Technical Requirements',N'Financial Requirements')) AS RequiredDocumentCount,
                                 (SELECT COUNT(*)
                                  FROM dbo.TenderDocuments doc
                                  OUTER APPLY
                                  (
                                      SELECT TOP (1) h.VerificationStatus
                                      FROM dbo.TenderRequirementVerificationHistory h
                                      WHERE h.TenderId=doc.TenderId AND h.DocumentId=doc.DocumentId
                                        AND h.ReviewStage=N'JE' AND h.DocumentRevision=doc.DocumentRevision
                                      ORDER BY h.ReviewedAtUtc DESC,h.VerificationHistoryId DESC
                                  ) latest
                                  WHERE doc.TenderId=t.TenderId AND doc.IsDeleted=0
                                    AND COALESCE(latest.VerificationStatus,
                                        CASE WHEN doc.VerifiedAtUtc IS NOT NULL THEN doc.VerificationStatus END,
                                        N'Pending')<>N'Verified') AS UnverifiedDocumentCount,
                                 (SELECT COUNT(*) FROM dbo.BOQ boq WHERE boq.TenderId=t.TenderId) AS BoqCount,
                                 CAST(CASE WHEN EXISTS
                                     (SELECT 1 FROM dbo.TenderGisLocations gis WHERE gis.TenderId=t.TenderId
                                       AND ((gis.BoundaryGeometryJson IS NOT NULL AND ISJSON(gis.BoundaryGeometryJson)=1)
                                         OR (gis.BoundaryLineGeometryJson IS NOT NULL AND
                                             ISJSON(gis.BoundaryLineGeometryJson)=1)))
                                     THEN 1 ELSE 0 END AS bit) AS HasGisLocation " + filter +
                        " ORDER BY " + orderColumn + " " + orderDirection + " OFFSET @Offset ROWS FETCH NEXT 10 ROWS ONLY;", connection))
                    {
                        AddFilterParameters(listCommand, createdFrom, createdTo, deadlineFrom, deadlineTo);
                        AddOwnerParameter(listCommand);
                        AddWorkflowScopeParameters(listCommand);
                        ModuleDb.AddParameter(listCommand, "@Offset", SqlDbType.Int, (page - 1) * 10);
                        using (SqlDataAdapter adapter = new SqlDataAdapter(listCommand))
                        {
                            DataTable table = new DataTable();
                            adapter.Fill(table);
                            AddPendingTaskColumns(table);
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

        private void AddFilterParameters(SqlCommand command, object createdFrom, object createdTo,
            object deadlineFrom, object deadlineTo)
        {
            string number = TenderNumberText.Text == null ? String.Empty : TenderNumberText.Text.Trim();
            string title = TenderTitleText.Text == null ? String.Empty : TenderTitleText.Text.Trim();
            AddFilterText(command, "@TenderNumber", number, 50);
            AddFilterText(command, "@TenderNumberPattern", "%" + number + "%", 52);
            AddFilterText(command, "@TenderTitle", title, 150);
            AddFilterText(command, "@TenderTitlePattern", "%" + title + "%", 152);
            AddFilterText(command, "@Status", StatusFilter.SelectedValue, 50);
            AddFilterText(command, "@Category", CategoryFilter.SelectedValue, 50);
            ModuleDb.AddParameter(command, "@CreatedFrom", SqlDbType.Date, createdFrom);
            ModuleDb.AddParameter(command, "@CreatedTo", SqlDbType.Date, createdTo);
            ModuleDb.AddParameter(command, "@DeadlineFrom", SqlDbType.Date, deadlineFrom);
            ModuleDb.AddParameter(command, "@DeadlineTo", SqlDbType.Date, deadlineTo);
            int departmentId;
            ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                Int32.TryParse(DepartmentFilter.SelectedValue, out departmentId) ? departmentId : 0);
            AddFilterText(command, "@WorkflowStage", WorkflowStageFilter.SelectedValue, 100);
            int assignedOfficerId;
            ModuleDb.AddParameter(command, "@AssignedOfficerId", SqlDbType.Int,
                Int32.TryParse(AssignedOfficerFilter.SelectedValue, out assignedOfficerId) ? assignedOfficerId : 0);
            AddFilterText(command, "@PendingTask", PendingTaskFilter.SelectedValue, 10);
            AddFilterText(command, "@PublicationStatus", PublicationStatusFilter.SelectedValue, 30);
        }

        private static void AddFilterText(SqlCommand command, string name, string value, int size)
        {
            ModuleDb.AddParameter(command, name, SqlDbType.NVarChar,
                String.IsNullOrWhiteSpace(value) ? String.Empty : value.Trim(), size);
        }

        private bool TryGetDateFilters(out object createdFrom, out object createdTo,
            out object deadlineFrom, out object deadlineTo)
        {
            deadlineFrom = null;
            deadlineTo = null;
            if (!TryParseDateRange(DateFrom.Text, DateTo.Text, "creation", out createdFrom, out createdTo) ||
                !TryParseDateRange(DeadlineFrom.Text, DeadlineTo.Text, "closing", out deadlineFrom, out deadlineTo))
                return false;
            Message.Text = String.Empty;
            return true;
        }

        private bool TryParseDateRange(string fromText, string toText, string rangeName,
            out object dateFrom, out object dateTo)
        {
            dateFrom = null;
            dateTo = null;
            DateTime parsed;
            if (!String.IsNullOrWhiteSpace(fromText))
            {
                if (!DateTime.TryParseExact(fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed))
                {
                    Message.Text = "<div class=\"message\">Enter a valid " + rangeName + " start date.</div>";
                    return false;
                }
                dateFrom = parsed.Date;
            }
            if (!String.IsNullOrWhiteSpace(toText))
            {
                if (!DateTime.TryParseExact(toText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed))
                {
                    Message.Text = "<div class=\"message\">Enter a valid " + rangeName + " end date.</div>";
                    return false;
                }
                dateTo = parsed.Date;
            }
            if (dateFrom != null && dateTo != null && (DateTime)dateFrom > (DateTime)dateTo)
            {
                Message.Text = "<div class=\"message\">The " + rangeName + " start date must be on or before the end date.</div>";
                return false;
            }
            return true;
        }

        private void AddPendingTaskColumns(DataTable table)
        {
            table.Columns.Add("PendingTask", typeof(string));
            table.Columns.Add("PendingTaskUrl", typeof(string));
            foreach (DataRow row in table.Rows)
            {
                row["PendingTask"] = "—";
                row["PendingTaskUrl"] = String.Empty;
                if (Convert.ToInt32(row["WorkflowVersion"], CultureInfo.InvariantCulture) != 2 ||
                    row["PendingStage"] == DBNull.Value)
                    continue;

                TenderPendingTaskInfo task = TenderPendingTaskCatalog.Find(
                    Convert.ToString(row["PendingStage"], CultureInfo.InvariantCulture));
                if (task == null || !HasPermission(task.PermissionKey) ||
                    Convert.ToInt32(row["AssignedRoleId"], CultureInfo.InvariantCulture) != CurrentRoleId)
                    continue;

                object assignedUser = row["AssignedUserId"];
                if (assignedUser != DBNull.Value &&
                    Convert.ToInt32(assignedUser, CultureInfo.InvariantCulture) != CurrentUserId)
                    continue;

                if (task.PermissionKey == "tender.create.department" &&
                    (row["AssignedDepartmentId"] == DBNull.Value ||
                     !CurrentDepartmentId.HasValue ||
                     Convert.ToInt32(row["AssignedDepartmentId"], CultureInfo.InvariantCulture) != CurrentDepartmentId.Value))
                    continue;

                string label = task.Label;
                string url = task.GetUrl(Convert.ToInt32(row["TenderId"], CultureInfo.InvariantCulture));
                if (String.Equals(task.StageName, "Final_JE_SubmitTender", StringComparison.Ordinal))
                {
                    if (Convert.ToInt32(row["RequiredDocumentCount"], CultureInfo.InvariantCulture) < 3 ||
                        Convert.ToInt32(row["UnverifiedDocumentCount"], CultureInfo.InvariantCulture) > 0)
                    {
                        label = "Documents";
                        url = "~/Pages/TenderDetails.aspx?id=" + row["TenderId"] + "#tender-documents-section";
                    }
                    else if (Convert.ToInt32(row["BoqCount"], CultureInfo.InvariantCulture) == 0)
                    {
                        label = "BOQ";
                        url = "~/Pages/TenderDetails.aspx?id=" + row["TenderId"] + "#tender-boq-section";
                    }
                    else if (!Convert.ToBoolean(row["HasGisLocation"], CultureInfo.InvariantCulture))
                    {
                        label = "GIS Map";
                        url = "~/Pages/TenderForm.aspx?id=" + row["TenderId"] + "&step=3";
                    }
                }

                row["PendingTask"] = label;
                row["PendingTaskUrl"] = ResolveUrl(url);
            }
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

        protected void ClearFiltersButton_Click(object sender, EventArgs e)
        {
            TenderNumberText.Text = String.Empty;
            TenderTitleText.Text = String.Empty;
            StatusFilter.SelectedIndex = 0;
            CategoryFilter.SelectedIndex = 0;
            DepartmentFilter.SelectedIndex = 0;
            WorkflowStageFilter.SelectedIndex = 0;
            AssignedOfficerFilter.SelectedIndex = 0;
            PendingTaskFilter.SelectedIndex = 0;
            PublicationStatusFilter.SelectedIndex = 0;
            DateFrom.Text = String.Empty;
            DateTo.Text = String.Empty;
            DeadlineFrom.Text = String.Empty;
            DeadlineTo.Text = String.Empty;
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
            if (key != "TenderNumber" && key != "Title" && key != "CreatedDate" &&
                key != "ModifiedDate" && key != "AssignedOfficer" && key != "CurrentStage" &&
                key != "CurrentStatus")
                key = "CreatedDate";
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
