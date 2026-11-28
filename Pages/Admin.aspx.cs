using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class AdminDashboard : ModulePage
    {
        private const int RecentTendersPerPage = 10;

        protected Literal Message, TotalContractors, ActiveContractors,
            InactiveContractors, TotalDepartmentUsers, ActiveDepartmentUsers, TotalRoles,
            PendingAdminActions, RecentTendersPageSummary;
        protected GridView RecentTendersGrid, DepartmentTenderDetailsGrid;
        protected Panel DepartmentTenderDetailsPanel;
        protected Literal SelectedDepartmentName, SelectedDepartmentStatus;
        protected Panel DepartmentTendersEmptyPanel;
        protected Repeater DepartmentTendersCards;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RequireJeAdministrator()) return;
            Response.Redirect(ResolveUrl("~/Pages/Tenders.aspx"), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void LoadDashboard()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT
                            (SELECT COUNT(*) FROM dbo.Contractors) AS TotalContractors,
                            (SELECT COUNT(*) FROM dbo.Contractors c
                             WHERE c.IsActive=1) AS ActiveContractors,
                            (SELECT COUNT(*) FROM dbo.Contractors c
                             WHERE c.IsActive=0) AS InactiveContractors,
                            (SELECT COUNT(*) FROM dbo.Users WHERE AccountType=N'Internal') AS TotalDepartmentUsers,
                            (SELECT COUNT(*) FROM dbo.Users WHERE AccountType=N'Internal' AND IsActive=1) AS ActiveDepartmentUsers,
                            (SELECT COUNT(*) FROM dbo.Roles) AS TotalRoles,
                            (SELECT COUNT(*) FROM dbo.TenderWorkflowAssignments a
                             INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                             INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId
                             INNER JOIN dbo.Roles r ON r.RoleId=a.AssignedRoleId AND r.RoleCode=N'JE'
                             WHERE a.IsCurrent=1 AND t.IsDeleted=0) AS PendingAdminActions;", connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                TotalContractors.Text = Convert.ToString(reader["TotalContractors"]);
                                ActiveContractors.Text = Convert.ToString(reader["ActiveContractors"]);
                                InactiveContractors.Text = Convert.ToString(reader["InactiveContractors"]);
                                TotalDepartmentUsers.Text = Convert.ToString(reader["TotalDepartmentUsers"]);
                                ActiveDepartmentUsers.Text = Convert.ToString(reader["ActiveDepartmentUsers"]);
                                TotalRoles.Text = Convert.ToString(reader["TotalRoles"]);
                                PendingAdminActions.Text = Convert.ToString(reader["PendingAdminActions"]);
                            }
                        }
                    }

                    BindOverviewTables(connection);
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Admin dashboard data could not be loaded.", ex);
                Message.Text = "<div class=\"message\">Dashboard data could not be loaded. Please retry or contact support.</div>";
            }
        }

        private void BindOverviewTables(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT d.DepartmentId,d.DepartmentName,COUNT(t.TenderId) AS TenderCount,
                         SUM(CASE WHEN t.CurrentStatus=N'Draft' THEN 1 ELSE 0 END) AS DraftCount,
                         SUM(CASE WHEN t.CurrentStatus NOT LIKE N'%cancel%'
                                   AND published.TenderId IS NOT NULL THEN 1 ELSE 0 END) AS PublishedCount,
                         SUM(CASE WHEN t.CurrentStatus=N'Tender Published'
                                   AND s.PublicationStartUtc<=SYSUTCDATETIME()
                                   AND s.CurrentClosingUtc>SYSUTCDATETIME()
                                   AND published.TenderId IS NOT NULL THEN 1 ELSE 0 END) AS ActiveCount,
                         SUM(CASE WHEN t.CurrentStatus NOT LIKE N'%cancel%'
                                   AND (t.CurrentStatus LIKE N'%closed%'
                                        OR (s.CurrentClosingUtc<=SYSUTCDATETIME()
                                            AND published.TenderId IS NOT NULL))
                                   THEN 1 ELSE 0 END) AS ClosedCount
                  FROM dbo.Departments d
                  INNER JOIN dbo.Tender t ON t.DepartmentId=d.DepartmentId AND t.IsDeleted=0
                  LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                  LEFT JOIN
                  (
                      SELECT ah.TenderId
                      FROM dbo.ApprovalHistory ah
                      WHERE ah.NewStatus=N'Tender Published'
                      UNION
                      SELECT wh.TenderId
                      FROM dbo.TenderWorkflowHistory wh
                      WHERE wh.WorkflowVersion=2 AND wh.NewStatus=N'Tender Published'
                  ) published ON published.TenderId=t.TenderId
                  GROUP BY d.DepartmentId,d.DepartmentName
                  ORDER BY d.DepartmentName;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                DataTable data = new DataTable();
                adapter.Fill(data);
                DepartmentTendersCards.DataSource = data;
                DepartmentTendersCards.DataBind();
                DepartmentTendersEmptyPanel.Visible = data.Rows.Count == 0;
            }

            int tenderCount;
            using (SqlCommand countCommand = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.Tender WHERE IsDeleted=0;", connection))
            {
                tenderCount = Convert.ToInt32(countCommand.ExecuteScalar());
            }
            int pageCount = Math.Max(1, (int)Math.Ceiling(tenderCount / (double)RecentTendersPerPage));
            RecentTendersGrid.PageIndex = Math.Max(0, Math.Min(RecentTendersGrid.PageIndex, pageCount - 1));
            RecentTendersGrid.VirtualItemCount = tenderCount;
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.TenderId,t.TenderNumber,t.Title,t.TenderType,
                         COALESCE(t.TenderCategory,N'Uncategorized') AS TenderCategory,d.DepartmentName,
                         COALESCE(u.FullName,N'') AS AssignedOfficer,
                         (SELECT MIN(ah.ApprovalDate) FROM dbo.ApprovalHistory ah
                          WHERE ah.TenderId=t.TenderId AND ah.NewStatus=N'Tender Published') AS PublicationDate,
                         t.SubmissionStartDate,t.SubmissionDeadline,t.CurrentStatus,
                         CAST(CASE WHEN t.CurrentStatus=N'Draft' AND
                             (t.WorkflowVersion=1 OR
                              (t.WorkflowVersion=2 AND EXISTS
                                  (SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                   INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                   WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                     AND s.StageName=N'Final_JE_SubmitTender')))
                             THEN 1 ELSE 0 END AS bit) AS CanEditDraft
                  FROM dbo.Tender t INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                  LEFT JOIN dbo.Users u ON u.UserId=t.CreatedBy
                  WHERE t.IsDeleted=0
                  ORDER BY t.CreatedDate DESC,t.TenderId DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", connection))
            {
                ModuleDb.AddParameter(command, "@Offset", SqlDbType.Int,
                    RecentTendersGrid.PageIndex * RecentTendersPerPage);
                ModuleDb.AddParameter(command, "@PageSize", SqlDbType.Int, RecentTendersPerPage);
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable data = new DataTable();
                    adapter.Fill(data);
                    RecentTendersGrid.DataSource = data;
                    RecentTendersGrid.DataBind();
                }
            }
            RecentTendersPageSummary.Text = String.Format(
                "Page {0} of {1} · {2} tenders",
                RecentTendersGrid.PageIndex + 1, pageCount, tenderCount);
        }

        protected void DepartmentTendersCards_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            bool filterByStatus = String.Equals(e.CommandName, "ViewDepartmentStatus", StringComparison.Ordinal);
            if (!filterByStatus &&
                !String.Equals(e.CommandName, "ViewDepartmentTenders", StringComparison.Ordinal))
                return;

            int departmentId;
            string[] arguments = Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture).Split('|');
            string departmentValue = arguments.Length > 0 ? arguments[0] : String.Empty;
            string selectedStatus = filterByStatus && arguments.Length == 2 ? arguments[1] : String.Empty;
            if (!Int32.TryParse(departmentValue,
                    NumberStyles.None, CultureInfo.InvariantCulture, out departmentId) ||
                departmentId <= 0 ||
                (filterByStatus && TenderStatusBuckets.Predicate(selectedStatus) == null))
            {
                Message.Text = "<div class=\"message\">Select a valid department and tender status, then try again.</div>";
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    string departmentName;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT DepartmentName FROM dbo.Departments
                          WHERE DepartmentId=@DepartmentId;", connection))
                    {
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        object value = command.ExecuteScalar();
                        if (value == null || value == DBNull.Value)
                            throw new InvalidOperationException("The selected department is no longer available.");
                        departmentName = Convert.ToString(value);
                    }

                    string statusPredicate = filterByStatus
                        ? " AND (" + TenderStatusBuckets.Predicate(selectedStatus) + ")"
                        : String.Empty;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT t.TenderId,t.TenderNumber,t.Title,t.CurrentStatus,t.CreatedDate
                          FROM dbo.Tender t
                          LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                          WHERE t.DepartmentId=@DepartmentId AND t.IsDeleted=0" + statusPredicate +
                        " ORDER BY t.CreatedDate DESC,t.TenderId DESC;", connection))
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                        DataTable tenders = new DataTable();
                        adapter.Fill(tenders);
                        DepartmentTenderDetailsGrid.DataSource = tenders;
                        DepartmentTenderDetailsGrid.DataBind();
                    }

                    SelectedDepartmentName.Text = HttpUtility.HtmlEncode(departmentName);
                    SelectedDepartmentStatus.Text = filterByStatus
                        ? HttpUtility.HtmlEncode(selectedStatus)
                        : "All";
                    DepartmentTenderDetailsPanel.Visible = true;
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Department tenders could not be loaded.", ex);
                Message.Text = "<div class=\"message\">Tenders for this department could not be loaded. Please retry.</div>";
            }
            catch (InvalidOperationException ex)
            {
                Message.Text = "<div class=\"message\">" + ModuleDb.Html(ex.Message) + "</div>";
            }
        }

        protected void ClearDepartmentSelectionButton_Click(object sender, EventArgs e)
        {
            DepartmentTenderDetailsPanel.Visible = false;
            SelectedDepartmentName.Text = String.Empty;
            SelectedDepartmentStatus.Text = String.Empty;
        }

        protected int RecentTenderSerialNumber(int rowIndex)
        {
            return RecentTendersGrid.PageIndex * RecentTendersPerPage + rowIndex + 1;
        }

        protected bool CanEditDraft(object value)
        {
            return HasPermission("system.admin") &&
                value != null && value != DBNull.Value && Convert.ToBoolean(value);
        }

        protected void RecentTendersGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            RecentTendersGrid.PageIndex = e.NewPageIndex;
            LoadDashboard();
        }

        protected void RecentTendersGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "DeleteDraft", StringComparison.Ordinal)) return;
            if (!HasPermission("system.admin"))
            {
                Message.Text = "<div class=\"message\">You are not authorized to delete tenders.</div>";
                return;
            }

            int tenderId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out tenderId) || tenderId <= 0)
            {
                Message.Text = "<div class=\"message\">The selected tender could not be verified. Refresh the page and retry.</div>";
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction())
                using (SqlCommand command = new SqlCommand(
                    @"UPDATE t
                      SET IsDeleted=1,ModifiedBy=@UserId,ModifiedDate=SYSUTCDATETIME()
                      FROM dbo.Tender t
                      WHERE t.TenderId=@TenderId AND t.IsDeleted=0 AND t.CurrentStatus=N'Draft'
                        AND (t.WorkflowVersion=1 OR
                             (t.WorkflowVersion=2 AND EXISTS
                                 (SELECT 1 FROM dbo.TenderWorkflowAssignments a
                                  INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                  WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                    AND s.StageName=N'Final_JE_SubmitTender')));", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Only a draft at its initial workflow stage can be deleted.");
                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Delete", "Tender",
                        tenderId, "Draft soft-deleted; workflow and audit history retained.", Request.UserHostAddress);
                    transaction.Commit();
                }
                LoadDashboard();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Draft tender could not be deleted.", ex);
                Message.Text = "<div class=\"message\">The draft could not be deleted. Please retry.</div>";
            }
            catch (InvalidOperationException ex)
            {
                Message.Text = "<div class=\"message\">" + ModuleDb.Html(ex.Message) + "</div>";
            }
        }

    }
}
