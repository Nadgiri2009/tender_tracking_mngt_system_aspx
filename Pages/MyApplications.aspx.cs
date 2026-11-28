using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class MyApplications : ModulePage
    {
        protected Literal Message, AvailableTendersCount, AppliedTendersCount, PendingApplicationsCount,
            UnderEvaluationCount, ShortfallReceivedCount, AwardedTendersCount,
            RejectedApplicationsCount, PendingActionsCount;
        protected GridView ApplicationsGrid, NotificationsGrid, PendingActionsGrid, UnderEvaluationGrid,
            RejectedApplicationsGrid, DocumentsGrid, AwardedTendersGrid, EmdRequirementsGrid, PaymentsGrid;
        protected TextBox ApplicationSearchText;
        protected DropDownList ApplicationStatusFilter;
        protected Button FilterApplicationsButton;

        protected void Page_Init(object sender, EventArgs e)
        {
            if (!RequireRoles("Contractor")) return;
            BindDashboard();
        }

        private DataTable FilterApplications(DataTable applications)
        {
            string search = (ApplicationSearchText.Text ?? String.Empty).Trim();
            string statusFilter = ApplicationStatusFilter.SelectedValue;
            DataTable filtered = applications.Clone();
            foreach (DataRow application in applications.Rows)
            {
                if (!String.IsNullOrEmpty(statusFilter) &&
                    !String.Equals(Convert.ToString(application["ApplicationStatus"]), statusFilter,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (search.Length > 0)
                {
                    string[] searchableFields =
                    {
                        "ApplicationReference", "TenderNumber", "Title", "DepartmentName", "CurrentStage"
                    };
                    bool found = false;
                    foreach (string field in searchableFields)
                    {
                        string value = application[field] == DBNull.Value
                            ? String.Empty
                            : Convert.ToString(application[field]);
                        if (value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) continue;
                }
                filtered.ImportRow(application);
            }
            return filtered;
        }

        protected void FilterApplicationsButton_Click(object sender, EventArgs e)
        {
            ApplicationsGrid.PageIndex = 0;
            BindDashboard();
        }

        private void BindDashboard()
        {
            Message.Text = String.Empty;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    DataTable applications = GetApplications(connection);
                    DataTable awardedTenders = GetAwardedTenders(connection);
                    BindApplicationGrids(applications);
                    BindGrid(AwardedTendersGrid, awardedTenders);
                    BindGrid(DocumentsGrid, GetDocuments(connection));
                    BindGrid(EmdRequirementsGrid, applications);
                    BindGrid(PaymentsGrid, GetPayments(connection));
                    BindGrid(NotificationsGrid,
                        ContractorApplicationRules.GetApplicationNotifications(connection, CurrentUserId));
                    BindStatistics(connection, applications, awardedTenders);

                    if (!ContractorApplicationRules.HasScheduledNotificationAvailability(connection))
                    {
                        Message.Text = "<div class=\"message\">Scheduled shortfall reminders will appear after the database upgrade is applied.</div>";
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Contractor dashboard data could not be loaded.", ex);
                ShowDashboardError();
            }
            catch (ConfigurationErrorsException ex)
            {
                Trace.Warn("DepartmentModule", "Contractor dashboard database configuration is invalid.", ex);
                ShowDashboardError();
            }
        }

        private DataTable GetApplications(SqlConnection connection)
        {
            const string query = @"
                SELECT t.TenderId,t.WorkflowVersion,t.TenderNumber,t.Title,d.DepartmentName,
                       COALESCE(currentStage.StageName,t.CurrentStatus) AS CurrentStage,
                       t.CurrentStatus AS TenderStage,t.EMDAmount,t.TenderDocumentFee,t.ProcessingFee,
                       tb.TechnicalBidId,tb.SubmittedDate,
                       COALESCE(latestStatus.NewStatus,tb.Status) AS ApplicationStatus,
                       fb.Status AS FinancialStatus,
                       latestRemark.Remarks AS LatestVisibleRemark,
                       shortfall.PendingDeadlineUtc,
                       CONVERT(bit,CASE WHEN shortfall.PendingDeadlineUtc IS NULL THEN 0 ELSE 1 END) AS HasOpenShortfall,
                       CASE
                           WHEN COALESCE(latestStatus.NewStatus,tb.Status)=N'Draft'
                               THEN N'Complete and submit your draft'
                           WHEN COALESCE(latestStatus.NewStatus,tb.Status)=N'Shortfall'
                                OR shortfall.PendingDeadlineUtc IS NOT NULL
                               THEN N'Submit the requested clarification documents'
                           ELSE N''
                       END AS PendingAction
                FROM dbo.TechnicalBid tb
                INNER JOIN dbo.Contractors c
                    ON c.ContractorId=tb.ContractorId AND c.UserId=@UserId AND c.IsActive=1
                INNER JOIN dbo.Tender t ON t.TenderId=tb.TenderId AND t.IsDeleted=0
                INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                LEFT JOIN dbo.FinancialBid fb
                    ON fb.TenderId=tb.TenderId AND fb.ContractorId=tb.ContractorId
                OUTER APPLY
                (
                    SELECT TOP (1) REPLACE(REPLACE(s.StageName,N'Final_',N''),N'_',N' ') AS StageName
                    FROM dbo.TenderWorkflowAssignments a
                    INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                      AND s.WorkflowVersion=2 AND s.IsActive=1
                    WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                    ORDER BY a.AssignmentId DESC
                ) currentStage
                OUTER APPLY
                (
                    SELECT TOP (1) h.NewStatus
                    FROM dbo.ContractorApplicationHistory h
                    WHERE h.TechnicalBidId=tb.TechnicalBidId AND h.NewStatus IS NOT NULL
                    ORDER BY h.HistoryId DESC
                ) latestStatus
                OUTER APPLY
                (
                    SELECT TOP (1) visibleRemark.Remarks
                    FROM
                    (
                        SELECT h.Remarks,h.CreatedAtUtc,h.HistoryId AS RecordId
                        FROM dbo.ContractorApplicationHistory h
                        WHERE h.TechnicalBidId=tb.TechnicalBidId
                          AND h.VisibleToContractor=1 AND h.Remarks IS NOT NULL
                        UNION ALL
                        SELECT r.Message,r.CreatedAtUtc,r.RemarkId
                        FROM dbo.ContractorApplicationRemarks r
                        WHERE r.TechnicalBidId=tb.TechnicalBidId
                          AND r.VisibleToContractor=1
                    ) visibleRemark
                    ORDER BY visibleRemark.CreatedAtUtc DESC,visibleRemark.RecordId DESC
                ) latestRemark
                OUTER APPLY
                (
                    SELECT MIN(s.SubmissionDeadlineUtc) AS PendingDeadlineUtc
                    FROM dbo.TenderShortfalls s
                    WHERE s.TenderId=tb.TenderId AND s.ContractorId=tb.ContractorId
                      AND s.Status=N'Awaiting Contractor'
                      AND EXISTS
                      (
                          SELECT 1 FROM dbo.TenderShortfallItems i
                          WHERE i.ShortfallId=s.ShortfallId
                            AND i.ClearanceStatus IN (N'Pending',N'Continued Deficiency')
                      )
                ) shortfall
                ORDER BY tb.SubmittedDate DESC,tb.TechnicalBidId DESC;";

            DataTable applications = QueryTable(connection, query, CurrentUserId);
            applications.Columns.Add("ApplicationReference", typeof(string));
            foreach (DataRow application in applications.Rows)
            {
                application["ApplicationReference"] = ContractorApplicationRules.GetReference(
                    Convert.ToInt32(application["TechnicalBidId"], CultureInfo.InvariantCulture),
                    Convert.ToDateTime(application["SubmittedDate"], CultureInfo.InvariantCulture));
            }
            return applications;
        }

        private DataTable GetAwardedTenders(SqlConnection connection)
        {
            const string query = @"
                SELECT t.TenderId,t.TenderNumber,t.Title,award.AwardType,award.AwardReference,
                       award.AwardDate,award.AwardAmount
                FROM dbo.Tender t
                OUTER APPLY
                (
                    SELECT TOP (1) awardRecord.AwardType,awardRecord.AwardReference,
                           awardRecord.AwardDate,awardRecord.AwardAmount
                    FROM
                    (
                        SELECT N'Work order' AS AwardType,w.OrderNo AS AwardReference,
                               w.IssueDate AS AwardDate,w.Amount AS AwardAmount,w.CreatedDate AS RecordedDate
                        FROM dbo.WorkOrder w
                        INNER JOIN dbo.Contractors c
                            ON c.ContractorId=w.ContractorId AND c.UserId=@UserId AND c.IsActive=1
                        WHERE w.TenderId=t.TenderId
                        UNION ALL
                        SELECT N'Tender agreement',a.AgreementNo,a.AgreementDate,
                               CONVERT(decimal(18,2),NULL),a.CreatedDate
                        FROM dbo.Agreement a
                        INNER JOIN dbo.Contractors c
                            ON c.ContractorId=a.ContractorId AND c.UserId=@UserId AND c.IsActive=1
                        WHERE a.TenderId=t.TenderId
                    ) awardRecord
                    ORDER BY awardRecord.RecordedDate DESC,awardRecord.AwardDate DESC
                ) award
                WHERE t.IsDeleted=0 AND award.AwardReference IS NOT NULL
                ORDER BY award.AwardDate DESC,t.TenderId DESC;";

            return QueryTable(connection, query, CurrentUserId);
        }

        private DataTable GetDocuments(SqlConnection connection)
        {
            const string query = @"
                SELECT N'Application' AS DocumentKind,
                       CONVERT(bigint,d.TechnicalBidDocumentId) AS DocumentId,
                       t.TenderNumber,d.DocumentType,d.FileName,d.UploadedDate AS UploadedAt,
                       uploader.FullName AS UploadedBy,
                       review.VerificationStatus,review.Remarks
                FROM dbo.TechnicalBidDocuments d
                INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                LEFT JOIN dbo.Users uploader ON uploader.UserId=d.UploadedBy
                INNER JOIN dbo.Contractors c
                    ON c.ContractorId=b.ContractorId AND c.UserId=@UserId AND c.IsActive=1
                INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.IsDeleted=0
                OUTER APPLY
                (
                    SELECT TOP (1) h.VerificationStatus,h.Remarks
                    FROM dbo.TenderDocumentVerificationHistory h
                    WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                      AND h.ContractorId=b.ContractorId AND h.ReviewStage=N'Secondary'
                    ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                ) review
                UNION ALL
                SELECT N'Shortfall response',s.ShortfallSubmissionId,t.TenderNumber,
                       i.RequiredDocumentType,s.FileName,s.SubmittedAtUtc,uploader.FullName,
                       i.ClearanceStatus,s.Remarks
                FROM dbo.TenderShortfallSubmissions s
                INNER JOIN dbo.TenderShortfallItems i ON i.ShortfallItemId=s.ShortfallItemId
                INNER JOIN dbo.TenderShortfalls sf ON sf.ShortfallId=i.ShortfallId
                LEFT JOIN dbo.Users uploader ON uploader.UserId=s.SubmittedBy
                INNER JOIN dbo.Contractors c
                    ON c.ContractorId=sf.ContractorId AND c.UserId=@UserId AND c.IsActive=1
                INNER JOIN dbo.Tender t ON t.TenderId=sf.TenderId AND t.IsDeleted=0
                ORDER BY UploadedAt DESC;";

            return QueryTable(connection, query, CurrentUserId);
        }

        private DataTable GetPayments(SqlConnection connection)
        {
            const string query = @"
                SELECT t.TenderNumber,b.BillNo,p.Amount,p.TransactionNo,p.PaymentDate
                FROM dbo.Payments p
                INNER JOIN dbo.Bills b ON b.BillId=p.BillId
                INNER JOIN dbo.Contractors c
                    ON c.ContractorId=b.ContractorId AND c.UserId=@UserId AND c.IsActive=1
                INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.IsDeleted=0
                ORDER BY p.PaymentDate DESC,p.PaymentId DESC;";

            return QueryTable(connection, query, CurrentUserId);
        }

        private void BindApplicationGrids(DataTable applications)
        {
            BindGrid(ApplicationsGrid, FilterApplications(applications));

            DataView pendingActions = new DataView(applications,
                "ApplicationStatus = 'Draft' OR ApplicationStatus = 'Shortfall' OR HasOpenShortfall = true",
                "SubmittedDate DESC", DataViewRowState.CurrentRows);
            BindGrid(PendingActionsGrid, pendingActions);

            DataView underEvaluation = new DataView(applications,
                "ApplicationStatus IN ('UnderVerification','Qualified','UnderEvaluation','FinancialEvaluation')",
                "SubmittedDate DESC", DataViewRowState.CurrentRows);
            BindGrid(UnderEvaluationGrid, underEvaluation);

            DataView rejected = new DataView(applications,
                "ApplicationStatus IN ('Disqualified','Rejected')",
                "SubmittedDate DESC", DataViewRowState.CurrentRows);
            BindGrid(RejectedApplicationsGrid, rejected);
        }

        private void BindStatistics(SqlConnection connection, DataTable applications, DataTable awardedTenders)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(DISTINCT t.TenderId)
                  FROM dbo.Tender t
                  INNER JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                  WHERE " + TenderStatusBuckets.PublicTenderVisibility + @"
                    AND t.CurrentStatus=N'Tender Published'
                    AND s.CurrentClosingUtc>SYSUTCDATETIME()
                    AND (t.SubmissionStartDate IS NULL OR t.SubmissionStartDate<=SYSUTCDATETIME())
                    AND (t.SubmissionDeadline IS NULL OR t.SubmissionDeadline>=SYSUTCDATETIME());",
                connection))
            {
                AvailableTendersCount.Text = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            HashSet<int> applied = new HashSet<int>();
            HashSet<int> underEvaluation = new HashSet<int>();
            HashSet<int> pendingApplications = new HashSet<int>();
            HashSet<int> shortfallReceived = new HashSet<int>();
            HashSet<int> rejected = new HashSet<int>();
            HashSet<int> pending = new HashSet<int>();
            foreach (DataRow row in applications.Rows)
            {
                int tenderId = Convert.ToInt32(row["TenderId"], CultureInfo.InvariantCulture);
                string status = Convert.ToString(row["ApplicationStatus"]);
                if (!String.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase)) applied.Add(tenderId);
                if (String.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase))
                    pendingApplications.Add(tenderId);
                if (String.Equals(status, "UnderVerification", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(status, "Qualified", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(status, "UnderEvaluation", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(status, "FinancialEvaluation", StringComparison.OrdinalIgnoreCase))
                    underEvaluation.Add(tenderId);
                if (String.Equals(status, "Disqualified", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
                    rejected.Add(tenderId);
                if (String.Equals(status, "Shortfall", StringComparison.OrdinalIgnoreCase) ||
                    Convert.ToBoolean(row["HasOpenShortfall"], CultureInfo.InvariantCulture))
                    shortfallReceived.Add(tenderId);
                if (String.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(status, "Shortfall", StringComparison.OrdinalIgnoreCase) ||
                    Convert.ToBoolean(row["HasOpenShortfall"], CultureInfo.InvariantCulture))
                    pending.Add(tenderId);
            }

            AppliedTendersCount.Text = applied.Count.ToString(CultureInfo.InvariantCulture);
            PendingApplicationsCount.Text = pendingApplications.Count.ToString(CultureInfo.InvariantCulture);
            UnderEvaluationCount.Text = underEvaluation.Count.ToString(CultureInfo.InvariantCulture);
            ShortfallReceivedCount.Text = shortfallReceived.Count.ToString(CultureInfo.InvariantCulture);
            AwardedTendersCount.Text = awardedTenders.Rows.Count.ToString(CultureInfo.InvariantCulture);
            RejectedApplicationsCount.Text = rejected.Count.ToString(CultureInfo.InvariantCulture);
            PendingActionsCount.Text = pending.Count.ToString(CultureInfo.InvariantCulture);
        }

        private static DataTable QueryTable(SqlConnection connection, string query, int userId)
        {
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                return data;
            }
        }

        private static void BindGrid(GridView grid, object dataSource)
        {
            grid.DataSource = dataSource;
            grid.DataBind();
        }

        private void ShowDashboardError()
        {
            AvailableTendersCount.Text = AppliedTendersCount.Text = UnderEvaluationCount.Text =
                PendingApplicationsCount.Text = ShortfallReceivedCount.Text =
                AwardedTendersCount.Text = RejectedApplicationsCount.Text = PendingActionsCount.Text = "—";
            Message.Text = "<div class=\"message\">Your dashboard could not be loaded. Please retry or contact support.</div>";
            BindEmptyGrid(ApplicationsGrid);
            BindEmptyGrid(NotificationsGrid);
            BindEmptyGrid(PendingActionsGrid);
            BindEmptyGrid(UnderEvaluationGrid);
            BindEmptyGrid(RejectedApplicationsGrid);
            BindEmptyGrid(DocumentsGrid);
            BindEmptyGrid(AwardedTendersGrid);
            BindEmptyGrid(EmdRequirementsGrid);
            BindEmptyGrid(PaymentsGrid);
        }

        private static void BindEmptyGrid(GridView grid)
        {
            grid.DataSource = new DataTable();
            grid.DataBind();
        }

        protected string ContractorDocumentDownloadUrl(object kindValue, object idValue)
        {
            long documentId;
            if (!Int64.TryParse(Convert.ToString(idValue, CultureInfo.InvariantCulture),
                    NumberStyles.None, CultureInfo.InvariantCulture, out documentId) || documentId <= 0)
                return String.Empty;

            string kind = Convert.ToString(kindValue, CultureInfo.InvariantCulture);
            string parameter;
            if (String.Equals(kind, "Shortfall response", StringComparison.Ordinal))
                parameter = "shortfallSubmissionId";
            else if (String.Equals(kind, "Application", StringComparison.Ordinal))
                parameter = "technicalBidDocumentId";
            else
                return String.Empty;
            return ResolveUrl("~/Pages/DocumentDownload.aspx?" + parameter + "=" +
                documentId.ToString(CultureInfo.InvariantCulture));
        }

        protected void ApplicationsGrid_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            ApplicationsGrid.PageIndex = e.NewPageIndex;
            BindDashboard();
        }

        protected int ApplicationSerialNumber(int rowIndex)
        {
            return ApplicationsGrid.PageIndex * ApplicationsGrid.PageSize + rowIndex + 1;
        }

        protected void NotificationsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!String.Equals(e.CommandName, "MarkNotificationRead", StringComparison.Ordinal)) return;
            int rowIndex;
            long notificationId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument), out rowIndex) ||
                rowIndex < 0 || rowIndex >= NotificationsGrid.Rows.Count ||
                !Int64.TryParse(Convert.ToString(NotificationsGrid.DataKeys[rowIndex].Value), out notificationId))
            {
                Message.Text = "<div class=\"message\">The selected notification could not be verified. Refresh the page and try again.</div>";
                return;
            }

            try
            {
                bool changed;
                using (SqlConnection connection = ModuleDb.OpenConnection())
                {
                    changed = ContractorApplicationRules.MarkApplicationNotificationRead(
                        connection, notificationId, CurrentUserId);
                }
                BindDashboard();
                if (!changed)
                    Message.Text = "<div class=\"message\">The notification is no longer unread.</div>";
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Application notification could not be marked read.", ex);
                Message.Text = "<div class=\"message\">The notification could not be updated. Please retry.</div>";
            }
        }
    }
}
