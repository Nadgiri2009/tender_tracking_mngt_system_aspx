using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class AdminDashboard : ModulePage
    {
        private const int FeedItemsPerPage = 10;
        private const string FeedPageIndexKey = "AdminDashboardFeedPageIndex";
        private const int RecentTendersPerPage = 10;

        protected Literal Message, TotalTenders, DraftTenders, PublishedTenders, ActiveTenderCount,
            ClosedTenders, CancelledTenders, AwardedTenders, TotalContractors, ActiveContractors,
            InactiveContractors, PendingDocumentVerification,
            ShortfallsGenerated, ShortfallResponsesPending, PendingFinancialOpeningApprovals,
            PendingAuditApprovals, PendingWorkOrders, PendingAgreements, TotalDepartmentUsers,
            ActiveDepartmentUsers, TotalRoles, PendingAdminActions, PageSummary, RecentTendersPageSummary;
        protected GridView DashboardFeedGrid, CategoryStatsGrid, UpcomingDeadlinesGrid, RecentTendersGrid;
        protected LinkButton PreviousPageButton, NextPageButton;
        protected Repeater PageNumbers;

        private int CurrentPage
        {
            get { return ViewState[FeedPageIndexKey] is int ? (int)ViewState[FeedPageIndexKey] : 1; }
            set { ViewState[FeedPageIndexKey] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RequireJeAdministrator()) return;
            if (!IsPostBack) LoadDashboard();
        }

        private void LoadDashboard()
        {
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT
                            (SELECT COUNT(*) FROM dbo.Tender WHERE IsDeleted=0) AS TotalTenders,
                            (SELECT COUNT(*) FROM dbo.Tender WHERE IsDeleted=0 AND CurrentStatus=N'Draft') AS DraftTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Published + @") AS PublishedTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             INNER JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Active + @") AS ActiveTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Closed + @") AS ClosedTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Cancelled + @") AS CancelledTenders,
                            (SELECT COUNT(*) FROM dbo.Tender t
                             WHERE t.IsDeleted=0 AND " + TenderStatusBuckets.Awarded + @") AS AwardedTenders,
                            (SELECT COUNT(*) FROM dbo.Contractors) AS TotalContractors,
                            (SELECT COUNT(*) FROM dbo.Contractors c
                             WHERE c.IsActive=1) AS ActiveContractors,
                            (SELECT COUNT(*) FROM dbo.Contractors c
                             WHERE c.IsActive=0) AS InactiveContractors,
                            (SELECT COUNT(DISTINCT a.TenderId)
                             FROM dbo.TenderWorkflowAssignments a
                             INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                             INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.IsDeleted=0
                             WHERE a.IsCurrent=1 AND s.StageName=N'Final_Cell_CompleteSecondaryVerification') AS PendingDocumentVerification,
                            (SELECT COUNT(*) FROM dbo.TenderShortfalls) AS ShortfallsGenerated,
                            (SELECT COUNT(*) FROM dbo.TenderShortfalls WHERE Status=N'Awaiting Contractor') AS ShortfallResponsesPending,
                            (SELECT COUNT(DISTINCT a.TenderId)
                             FROM dbo.TenderWorkflowAssignments a
                             INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                             INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.IsDeleted=0
                             WHERE a.IsCurrent=1 AND s.StageName=N'Final_Samiti_ApproveFinancialOpening') AS PendingFinancialOpeningApprovals,
                            (SELECT COUNT(DISTINCT a.TenderId)
                             FROM dbo.TenderWorkflowAssignments a
                             INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                             INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.IsDeleted=0
                             WHERE a.IsCurrent=1 AND s.StageName=N'Final_Auditor_ApproveFinancialBids') AS PendingAuditApprovals,
                            (SELECT COUNT(DISTINCT a.TenderId)
                             FROM dbo.TenderWorkflowAssignments a
                             INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                             INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.IsDeleted=0
                             WHERE a.IsCurrent=1 AND s.StageName=N'Final_JE_GenerateWorkOrder') AS PendingWorkOrders,
                            (SELECT COUNT(DISTINCT a.TenderId)
                             FROM dbo.TenderWorkflowAssignments a
                             INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                             INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.IsDeleted=0
                             WHERE a.IsCurrent=1 AND s.StageName=N'Final_JE_GenerateAgreement') AS PendingAgreements,
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
                                TotalTenders.Text = Convert.ToString(reader["TotalTenders"]);
                                DraftTenders.Text = Convert.ToString(reader["DraftTenders"]);
                                PublishedTenders.Text = Convert.ToString(reader["PublishedTenders"]);
                                ActiveTenderCount.Text = Convert.ToString(reader["ActiveTenders"]);
                                ClosedTenders.Text = Convert.ToString(reader["ClosedTenders"]);
                                CancelledTenders.Text = Convert.ToString(reader["CancelledTenders"]);
                                AwardedTenders.Text = Convert.ToString(reader["AwardedTenders"]);
                                TotalContractors.Text = Convert.ToString(reader["TotalContractors"]);
                                ActiveContractors.Text = Convert.ToString(reader["ActiveContractors"]);
                                InactiveContractors.Text = Convert.ToString(reader["InactiveContractors"]);
                                PendingDocumentVerification.Text = Convert.ToString(reader["PendingDocumentVerification"]);
                                ShortfallsGenerated.Text = Convert.ToString(reader["ShortfallsGenerated"]);
                                ShortfallResponsesPending.Text = Convert.ToString(reader["ShortfallResponsesPending"]);
                                PendingFinancialOpeningApprovals.Text = Convert.ToString(reader["PendingFinancialOpeningApprovals"]);
                                PendingAuditApprovals.Text = Convert.ToString(reader["PendingAuditApprovals"]);
                                PendingWorkOrders.Text = Convert.ToString(reader["PendingWorkOrders"]);
                                PendingAgreements.Text = Convert.ToString(reader["PendingAgreements"]);
                                TotalDepartmentUsers.Text = Convert.ToString(reader["TotalDepartmentUsers"]);
                                ActiveDepartmentUsers.Text = Convert.ToString(reader["ActiveDepartmentUsers"]);
                                TotalRoles.Text = Convert.ToString(reader["TotalRoles"]);
                                PendingAdminActions.Text = Convert.ToString(reader["PendingAdminActions"]);
                            }
                        }
                    }

                    BindOverviewTables(connection);
                    BindDashboardFeed(connection);
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
                @"SELECT COALESCE(t.TenderCategory,N'Uncategorized') AS Category,COUNT(*) AS TenderCount
                  FROM dbo.Tender t WHERE t.IsDeleted=0
                  GROUP BY COALESCE(t.TenderCategory,N'Uncategorized')
                  ORDER BY TenderCount DESC,Category;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                DataTable data = new DataTable();
                adapter.Fill(data);
                CategoryStatsGrid.DataSource = data;
                CategoryStatsGrid.DataBind();
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

            using (SqlCommand command = new SqlCommand(
                @"SELECT TOP (5) t.TenderId,t.TenderNumber,t.Title,t.TenderType,
                         COALESCE(t.TenderCategory,N'Uncategorized') AS TenderCategory,d.DepartmentName,
                         COALESCE(u.FullName,N'') AS AssignedOfficer,
                         t.SubmissionStartDate,t.SubmissionDeadline,t.CurrentStatus
                  FROM dbo.Tender t INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                  LEFT JOIN dbo.Users u ON u.UserId=t.CreatedBy
                  WHERE t.IsDeleted=0 AND t.SubmissionDeadline>=SYSUTCDATETIME()
                    AND t.CurrentStatus<>N'Draft'
                  ORDER BY t.SubmissionDeadline,t.TenderId;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                DataTable data = new DataTable();
                adapter.Fill(data);
                UpcomingDeadlinesGrid.DataSource = data;
                UpcomingDeadlinesGrid.DataBind();
            }
        }

        private void BindDashboardFeed(SqlConnection connection)
        {
            const string feedSource = @"
                SELECT N'Tender' AS FeedType,t.TenderId,
                       COALESCE(t.ModifiedDate,t.CreatedDate) AS ActivityDate,
                       CONCAT(t.TenderNumber,N' - ',t.Title) AS Area,
                       CASE WHEN t.ModifiedDate IS NULL THEN N'Tender created' ELSE N'Tender updated' END AS Details,
                       COALESCE(NULLIF(u.FullName,N''),N'System / unknown') AS Actor,
                       t.CurrentStatus,
                       CAST(t.TenderId AS BIGINT) AS SortId
                FROM dbo.Tender t
                LEFT JOIN dbo.Users u ON u.UserId=COALESCE(t.ModifiedBy,t.CreatedBy)
                WHERE t.IsDeleted=0
                UNION ALL
                SELECT N'Activity' AS FeedType,CAST(NULL AS INT) AS TenderId,a.CreatedDate AS ActivityDate,
                       a.EntityName AS Area,a.Action + CASE WHEN a.Details IS NULL THEN N''
                           ELSE N' - ' + a.Details END AS Details,
                       COALESCE(NULLIF(u.FullName,N''),N'System / unknown') AS Actor,
                       CAST(NULL AS NVARCHAR(50)) AS CurrentStatus,
                       a.LogId AS SortId
                FROM dbo.AuditLog a
                LEFT JOIN dbo.Users u ON u.UserId=a.UserId
                WHERE a.EntityName IN (N'Tender',N'TenderDocument',N'BOQItem',N'TechnicalBid',N'FinancialBid')
                  AND NOT (a.EntityName=N'Tender' AND a.Action IN (N'Create',N'Update'))";

            int totalItems;
            using (SqlCommand countCommand = new SqlCommand(
                "SELECT COUNT(*) FROM (" + feedSource + ") AS DashboardFeed;", connection))
            {
                totalItems = Convert.ToInt32(countCommand.ExecuteScalar());
            }

            int pageCount = Math.Max(1, (int)Math.Ceiling(totalItems / (double)FeedItemsPerPage));
            CurrentPage = Math.Max(1, Math.Min(CurrentPage, pageCount));

            using (SqlCommand command = new SqlCommand(
                "SELECT FeedType,TenderId,ActivityDate,Area,Details,Actor,CurrentStatus" +
                " FROM (" + feedSource + @") AS DashboardFeed
                  ORDER BY ActivityDate DESC,FeedType,SortId DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", connection))
            {
                ModuleDb.AddParameter(command, "@Offset", SqlDbType.Int, (CurrentPage - 1) * FeedItemsPerPage);
                ModuleDb.AddParameter(command, "@PageSize", SqlDbType.Int, FeedItemsPerPage);
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    DataTable data = new DataTable();
                    adapter.Fill(data);
                    DashboardFeedGrid.DataSource = data;
                    DashboardFeedGrid.DataBind();
                }
            }

            BindPageNumbers(pageCount);
            PreviousPageButton.Enabled = CurrentPage > 1;
            NextPageButton.Enabled = CurrentPage < pageCount;
            PageSummary.Text = String.Format(
                "Page {0} of {1} · {2} records · Showing {3}-{4}",
                CurrentPage,
                pageCount,
                totalItems,
                totalItems == 0 ? 0 : (CurrentPage - 1) * FeedItemsPerPage + 1,
                Math.Min(CurrentPage * FeedItemsPerPage, totalItems));
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

        protected int FeedSerialNumber(int rowIndex)
        {
            return (CurrentPage - 1) * FeedItemsPerPage + rowIndex + 1;
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

        protected void PreviousPageButton_Click(object sender, EventArgs e)
        {
            CurrentPage--;
            BindCurrentFeedPage();
        }

        protected void NextPageButton_Click(object sender, EventArgs e)
        {
            CurrentPage++;
            BindCurrentFeedPage();
        }

        protected void PageNumbers_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int requestedPage;
            if (Int32.TryParse(Convert.ToString(e.CommandArgument), out requestedPage) && requestedPage > 0)
            {
                CurrentPage = requestedPage;
                BindCurrentFeedPage();
            }
        }

        private void BindCurrentFeedPage()
        {
            Message.Text = String.Empty;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    BindDashboardFeed(connection);
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Admin dashboard feed could not be loaded.", ex);
                Message.Text = "<div class=\"message\">Dashboard records could not be loaded. Please retry or contact support.</div>";
            }
        }

    }
}
