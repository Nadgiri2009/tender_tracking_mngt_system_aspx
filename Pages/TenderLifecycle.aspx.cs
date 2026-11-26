using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class TenderLifecycle : ModulePage
    {
        protected Literal Message, SelectedTender, PublicationPeriod;
        protected HyperLink EvaluationLink;
        protected Panel PublishedTendersPanel, SchedulePanel, ExtensionRequestPanel,
            PendingRequestsPanel, ExtensionHistoryPanel;
        protected GridView PublishedTendersGrid, PendingRequestsGrid, ExtensionHistoryGrid;
        protected TextBox StartDate, StartTime, ClosingDate, ClosingTime, ScheduleRemarks,
            ProposedClosingDate, ProposedClosingTime, ExtensionReason, ReviewRemarks;
        protected FileUpload ApplicationFile, SupportingFile;
        protected Button SaveScheduleButton, SubmitExtensionButton;

        private int TenderId { get { return QueryId("tenderId"); } }
        private bool IsAdministrator { get { return IsJeAdministrator; } }
        private bool CanReview { get { return IsAdministrator || HasAnyRole("CAFOOfficer"); } }
        private bool CanRequest { get { return IsAdministrator || HasAnyRole("DepartmentOfficer"); } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsAdministrator && !HasAnyRole("DepartmentOfficer", "CAFOOfficer"))
            {
                RequireRoles("DepartmentOfficer", "CAFOOfficer");
                return;
            }
            EvaluationLink.Visible = IsAdministrator ||
                HasAnyRole("CAFOOfficer", "TenderCommittee", "TenderSection");
            SchedulePanel.Visible = CanReview && TenderId > 0;
            ExtensionRequestPanel.Visible = CanRequest && TenderId > 0;
            PendingRequestsPanel.Visible = CanReview;
            if (!IsPostBack)
            {
                BindTenders();
                BindPendingRequests();
                BindHistory();
                if (TenderId > 0) BindSelectedTender();
            }
        }

        private string TenderScope(string alias)
        {
            if (IsAdministrator) return String.Empty;
            return " AND (@DepartmentId IS NULL OR " + alias + @".DepartmentId=@DepartmentId)
                    AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                    WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId IS NOT NULL)
                         OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                   WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId=" + alias + ".DepartmentId))";
        }

        private void AddDepartmentScope(SqlCommand command)
        {
            if (!IsAdministrator)
            {
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                ModuleDb.AddParameter(command, "@ScopeUserId", SqlDbType.Int, CurrentUserId);
            }
        }

        private void BindTenders()
        {
            string ownerScope = HasAnyRole("DepartmentOfficer") && !IsAdministrator
                ? " AND t.CreatedBy=@UserId" : String.Empty;
            string sql = @"SELECT t.TenderId,t.TenderNumber,t.Title,d.DepartmentName,
                                  s.PublicationStartUtc,s.OriginalClosingUtc,s.CurrentClosingUtc,
                                  (SELECT COUNT(*) FROM dbo.TenderExtensionRequests e
                                   WHERE e.TenderId=t.TenderId AND e.ReviewStatus=N'Approved') AS ApprovedExtensions,
                                  (SELECT COUNT(*) FROM dbo.TenderExtensionRequests e
                                   WHERE e.TenderId=t.TenderId AND e.ReviewStatus=N'Pending') AS PendingExtensions
                           FROM dbo.Tender t
                           INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                           LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                           WHERE t.IsDeleted=0 AND t.CurrentStatus=N'Tender Published'" + TenderScope("t") + ownerScope +
                           " ORDER BY s.CurrentClosingUtc,t.TenderNumber;";
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddDepartmentScope(command);
                if (ownerScope.Length > 0)
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                TenderLifecycleFiles.ConvertDatesToIndia(data, "PublicationStartUtc", "OriginalClosingUtc", "CurrentClosingUtc");
                PublishedTendersGrid.DataSource = data;
                PublishedTendersGrid.DataBind();
            }
        }

        private void BindSelectedTender()
        {
            string ownership = HasAnyRole("DepartmentOfficer") && !IsAdministrator
                ? " AND t.CreatedBy=@UserId" : String.Empty;
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.TenderNumber,t.Title,t.CurrentStatus,d.DepartmentName,
                         s.PublicationStartUtc,s.OriginalClosingUtc,s.CurrentClosingUtc,s.PublicationPeriodDays,
                         s.Remarks,s.ApprovedAtUtc
                  FROM dbo.Tender t
                  INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                  LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                  WHERE t.TenderId=@TenderId AND t.IsDeleted=0 AND t.CurrentStatus=N'Tender Published'" +
                  TenderScope("t") + ownership + ";", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                AddDepartmentScope(command);
                if (ownership.Length > 0) ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        SchedulePanel.Visible = false;
                        ExtensionRequestPanel.Visible = false;
                        ShowError("This tender is not available for lifecycle actions in your role or department.");
                        return;
                    }

                    SelectedTender.Text = ModuleDb.Html(reader["TenderNumber"]) + " · " +
                        ModuleDb.Html(reader["Title"]) + " · " + ModuleDb.Html(reader["DepartmentName"]);
                    if (reader["CurrentClosingUtc"] != DBNull.Value)
                    {
                        PublicationPeriod.Text = "<p class=\"message success\">Current approved period: " +
                            HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["PublicationStartUtc"])) +
                            " to " + HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["CurrentClosingUtc"])) +
                            " IST (" + HttpUtility.HtmlEncode(Convert.ToString(reader["PublicationPeriodDays"], CultureInfo.InvariantCulture)) +
                            " days). Approved " + HttpUtility.HtmlEncode(TenderLifecycleFiles.FormatIndiaDateTime(reader["ApprovedAtUtc"])) +
                            " IST.</p>";
                        if (reader["Remarks"] != DBNull.Value)
                            PublicationPeriod.Text += "<p>" + HttpUtility.HtmlEncode(Convert.ToString(reader["Remarks"])) + "</p>";
                    }
                    else
                    {
                        PublicationPeriod.Text = "<p class=\"message\">No publication schedule has been approved.</p>";
                    }
                }
            }
        }

        private void BindPendingRequests()
        {
            if (!CanReview) return;
            string sql = @"SELECT e.ExtensionRequestId,e.ExtensionNumber,e.OriginalClosingUtc,e.ProposedClosingUtc,
                                  e.ApplicationFileName,e.SupportingFileName,e.Reason,u.FullName AS SubmittedByName,
                                  t.TenderNumber,t.Title,d.DepartmentName
                           FROM dbo.TenderExtensionRequests e
                           INNER JOIN dbo.Tender t ON t.TenderId=e.TenderId
                           INNER JOIN dbo.Departments d ON d.DepartmentId=t.DepartmentId
                           INNER JOIN dbo.Users u ON u.UserId=e.SubmittedBy
                           WHERE e.ReviewStatus=N'Pending' AND t.IsDeleted=0" + TenderScope("t") +
                           " ORDER BY e.SubmittedAtUtc;";
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddDepartmentScope(command);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                TenderLifecycleFiles.ConvertDatesToIndia(data, "OriginalClosingUtc", "ProposedClosingUtc");
                PendingRequestsGrid.DataSource = data;
                PendingRequestsGrid.DataBind();
            }
        }

        private void BindHistory()
        {
            string ownerScope = HasAnyRole("DepartmentOfficer") && !IsAdministrator
                ? " AND t.CreatedBy=@UserId" : String.Empty;
            string sql = @"SELECT e.ExtensionRequestId,e.ExtensionNumber,e.OriginalClosingUtc,e.ProposedClosingUtc,e.Reason,
                                  e.ApplicationFileName,e.SupportingFileName,
                                  e.SubmittedAtUtc,e.ReviewStatus,e.ReviewedAtUtc,e.ReviewRemarks,
                                  su.FullName AS SubmittedByName,ru.FullName AS ReviewedByName,t.TenderNumber
                           FROM dbo.TenderExtensionRequests e
                           INNER JOIN dbo.Tender t ON t.TenderId=e.TenderId
                           INNER JOIN dbo.Users su ON su.UserId=e.SubmittedBy
                           LEFT JOIN dbo.Users ru ON ru.UserId=e.ReviewedBy
                           WHERE t.IsDeleted=0" + TenderScope("t") + ownerScope +
                           (TenderId > 0 ? " AND t.TenderId=@TenderId" : String.Empty) +
                           " ORDER BY e.SubmittedAtUtc DESC;";
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(sql, connection))
            {
                AddDepartmentScope(command);
                if (ownerScope.Length > 0)
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                if (TenderId > 0) ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                TenderLifecycleFiles.ConvertDatesToIndia(data, "OriginalClosingUtc", "ProposedClosingUtc", "SubmittedAtUtc", "ReviewedAtUtc");
                ExtensionHistoryGrid.DataSource = data;
                ExtensionHistoryGrid.DataBind();
            }
        }

        protected void SaveScheduleButton_Click(object sender, EventArgs e)
        {
            if (!CanReview || TenderId <= 0) { ShowError("Your role cannot approve publication schedules."); return; }
            if (ScheduleRemarks.Text.Length > 1000) { ShowError("Remarks must not exceed 1,000 characters."); return; }
            try
            {
                DateTime startUtc = TenderLifecycleFiles.ParseIndiaDateTime(StartDate.Text, StartTime.Text, "publication start");
                DateTime closingUtc = TenderLifecycleFiles.ParseIndiaDateTime(ClosingDate.Text, ClosingTime.Text, "publication closing");
                if (closingUtc <= startUtc) throw new InvalidOperationException("The closing time must be after the publication start.");

                SaveSchedule(startUtc, closingUtc, ScheduleRemarks.Text);

                Message.Text = "<div class=\"message success\">Publication schedule approved and recorded.</div>";
                BindSelectedTender();
                BindTenders();
            }
            catch (InvalidOperationException ex) { ShowError(ex.Message); }
            catch (ArgumentException ex) { ShowError("The server's India time zone configuration is unavailable: " + ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Publication schedule approval failed.", ex);
                ShowError(ex.Number >= 51000 && ex.Number <= 51099 ? ex.Message : "The publication schedule could not be saved.");
            }
        }

        protected void SubmitExtensionButton_Click(object sender, EventArgs e)
        {
            if (!CanRequest || TenderId <= 0) { ShowError("Your role cannot request a tender extension."); return; }
            if (ExtensionReason.Text.Trim().Length == 0 || ExtensionReason.Text.Length > 1000)
            {
                ShowError("Enter an extension reason (maximum 1,000 characters).");
                return;
            }

            string applicationPath = null, supportingPath = null;
            try
            {
                DateTime proposedUtc = TenderLifecycleFiles.ParseIndiaDateTime(
                    ProposedClosingDate.Text, ProposedClosingTime.Text, "proposed closing");
                EnsureExtensionRequestAllowed(proposedUtc);
                applicationPath = TenderLifecycleFiles.SaveUpload(this, ApplicationFile, TenderId, "extension application");
                supportingPath = TenderLifecycleFiles.SaveUpload(this, SupportingFile, TenderId, "supporting");

                SubmitExtension(proposedUtc, applicationPath, supportingPath);

                Message.Text = "<div class=\"message success\">Extension request submitted for CAFO review.</div>";
                BindTenders();
                BindHistory();
            }
            catch (InvalidOperationException ex)
            {
                TenderLifecycleFiles.DeleteStoredUpload(this, applicationPath);
                TenderLifecycleFiles.DeleteStoredUpload(this, supportingPath);
                ShowError(ex.Message);
            }
            catch (SqlException ex)
            {
                TenderLifecycleFiles.DeleteStoredUpload(this, applicationPath);
                TenderLifecycleFiles.DeleteStoredUpload(this, supportingPath);
                Trace.Warn("DepartmentModule", "Tender extension request failed.", ex);
                ShowError(ex.Number >= 51000 && ex.Number <= 51099 ? ex.Message : "The extension request could not be submitted.");
            }
            catch (System.IO.IOException ex)
            {
                TenderLifecycleFiles.DeleteStoredUpload(this, applicationPath);
                TenderLifecycleFiles.DeleteStoredUpload(this, supportingPath);
                Trace.Warn("DepartmentModule", "Extension documents could not be stored.", ex);
                ShowError("The extension documents could not be saved.");
            }
        }

        private void EnsureExtensionRequestAllowed(DateTime proposedClosingUtc)
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.CurrentStatus,t.CreatedBy,t.DepartmentId,s.CurrentClosingUtc,
                         (SELECT COUNT(*) FROM dbo.TenderExtensionRequests e
                          WHERE e.TenderId=t.TenderId AND e.ReviewStatus=N'Approved') AS ApprovedExtensions,
                         (SELECT COUNT(*) FROM dbo.TenderExtensionRequests e
                          WHERE e.TenderId=t.TenderId AND e.ReviewStatus=N'Pending') AS PendingExtensions,
                         u.DepartmentId AS UserDepartmentId,
                         CASE WHEN EXISTS(SELECT 1 FROM dbo.Users activeUser
                                          INNER JOIN dbo.Roles r ON r.RoleId=activeUser.RoleId
                                          WHERE activeUser.UserId=@UserId AND activeUser.IsActive=1
                                            AND r.RoleName=@Role) THEN 1 ELSE 0 END AS IsAuthorized
                  FROM dbo.Tender t
                  LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                  INNER JOIN dbo.Users u ON u.UserId=@UserId
                  WHERE t.TenderId=@TenderId AND t.IsDeleted=0;", connection))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                AddText(command, "@Role", SqlDbType.NVarChar, CurrentRole, 50);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read() || Convert.ToInt32(reader["IsAuthorized"], CultureInfo.InvariantCulture) != 1)
                        throw new InvalidOperationException("The tender or your active role could not be verified.");
                    if (Convert.ToString(reader["CurrentStatus"]) != "Tender Published")
                        throw new InvalidOperationException("Extensions are only available for published tenders.");
                    if (!IsAdministrator && Convert.ToInt32(reader["CreatedBy"], CultureInfo.InvariantCulture) != CurrentUserId)
                        throw new InvalidOperationException("You may only request extensions for tenders you own.");

                    int tenderDepartment = Convert.ToInt32(reader["DepartmentId"], CultureInfo.InvariantCulture);
                    if (!IsAdministrator &&
                        ((CurrentDepartmentId.HasValue && CurrentDepartmentId.Value != tenderDepartment) ||
                         (reader["UserDepartmentId"] != DBNull.Value &&
                          Convert.ToInt32(reader["UserDepartmentId"], CultureInfo.InvariantCulture) != tenderDepartment)))
                        throw new InvalidOperationException("The tender is outside your department.");

                    if (Convert.ToInt32(reader["ApprovedExtensions"], CultureInfo.InvariantCulture) >= 3)
                        throw new InvalidOperationException("The maximum of three approved extensions has been reached.");
                    if (Convert.ToInt32(reader["PendingExtensions"], CultureInfo.InvariantCulture) > 0)
                        throw new InvalidOperationException("An extension request is already awaiting review.");
                    if (reader["CurrentClosingUtc"] == DBNull.Value)
                        throw new InvalidOperationException("The tender has no approved publication schedule.");

                    DateTime currentClosingUtc = DateTime.SpecifyKind(
                        Convert.ToDateTime(reader["CurrentClosingUtc"], CultureInfo.InvariantCulture), DateTimeKind.Utc);
                    if (currentClosingUtc <= DateTime.UtcNow)
                        throw new InvalidOperationException("An extension must be requested before publication closes.");
                    if (proposedClosingUtc <= currentClosingUtc)
                        throw new InvalidOperationException("The proposed closing time must be after the current closing time.");
                }
            }
        }

        protected void PendingRequestsGrid_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (!CanReview || (e.CommandName != "ApproveExtension" && e.CommandName != "RejectExtension"))
            {
                ShowError("Your role cannot review this extension request.");
                return;
            }
            int requestId;
            if (!Int32.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), out requestId) || requestId <= 0)
            {
                ShowError("The selected extension request is invalid.");
                return;
            }

            try
            {
                ReviewExtension(requestId, e.CommandName == "ApproveExtension");

                Message.Text = "<div class=\"message success\">Extension request " +
                    (e.CommandName == "ApproveExtension" ? "approved; closing time updated." : "rejected; closing time unchanged.") +
                    "</div>";
                BindTenders();
                BindPendingRequests();
                BindHistory();
                if (TenderId > 0) BindSelectedTender();
            }
            catch (InvalidOperationException ex) { ShowError(ex.Message); }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender extension review failed.", ex);
                ShowError(ex.Number >= 51000 && ex.Number <= 51099 ? ex.Message : "The extension decision could not be saved.");
            }
        }

        // ---- Workflow rules implemented directly in C# with parameterized SQL inside one transaction each ----

        private void SaveSchedule(DateTime startUtc, DateTime closingUtc, string remarks)
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction tx = connection.BeginTransaction())
            {
                string status; int tenderDepartmentId, createdBy;
                if (!ModuleDb.LockTender(connection, tx, TenderId, out status, out tenderDepartmentId, out createdBy))
                    throw new InvalidOperationException("Tender not found.");
                if ((CurrentRole != "CAFOOfficer" && CurrentRole != "Admin") ||
                    !ModuleDb.UserHasRole(connection, tx, CurrentUserId, CurrentRole))
                    throw new InvalidOperationException("Only the authorized CAFO or administrator can approve a publication schedule.");
                if (CurrentDepartmentId.HasValue && CurrentDepartmentId.Value != tenderDepartmentId)
                    throw new InvalidOperationException("The tender is outside your department.");
                if (CurrentRole != "Admin" && ModuleDb.UserOutsideTenderDepartment(connection, tx, CurrentUserId, tenderDepartmentId))
                    throw new InvalidOperationException("The tender is outside your assigned department.");
                if (status != "Tender Published")
                    throw new InvalidOperationException("A publication schedule can only be configured for a tender approved for publication.");

                DateTime now = ModuleDb.UtcNow(connection, tx);
                if (startUtc <= now || closingUtc <= startUtc)
                    throw new InvalidOperationException("Publication dates must be valid and start in the future.");

                SqlParameter tenderParam = ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId);
                if (ModuleDb.Exists(connection, tx, "SELECT COUNT(1) FROM dbo.TenderBidOpening WHERE TenderId=@TenderId;", tenderParam))
                    throw new InvalidOperationException("A schedule cannot be changed after bid opening.");
                if (ModuleDb.Exists(connection, tx, "SELECT COUNT(1) FROM dbo.TechnicalBid WHERE TenderId=@TenderId;", ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId)) ||
                    ModuleDb.Exists(connection, tx, "SELECT COUNT(1) FROM dbo.FinancialBid WHERE TenderId=@TenderId;", ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId)))
                    throw new InvalidOperationException("A schedule cannot be changed after bids have been received.");

                object currentEnd = ModuleDb.Scalar(connection, tx,
                    "SELECT CurrentClosingUtc FROM dbo.TenderPublicationSchedule WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId;",
                    ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId));
                if (currentEnd != null && Convert.ToDateTime(currentEnd) <= now)
                    throw new InvalidOperationException("An active publication schedule cannot be changed after it starts.");

                decimal days = Math.Round(
                    (decimal)Math.Floor((closingUtc - startUtc).TotalSeconds) / 86400m, 2, MidpointRounding.AwayFromZero);
                if (days <= 0)
                    throw new InvalidOperationException("The publication period must be at least one hundredth of a day.");

                SqlParameter[] values = new SqlParameter[]
                {
                    ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId),
                    ModuleDb.Param("@Start", SqlDbType.DateTime2, startUtc),
                    ModuleDb.Param("@Closing", SqlDbType.DateTime2, closingUtc),
                    ModuleDb.Param("@Days", SqlDbType.Decimal, days),
                    ModuleDb.Param("@Remarks", SqlDbType.NVarChar, ModuleDb.Text(remarks), 1000),
                    ModuleDb.Param("@UserId", SqlDbType.Int, CurrentUserId)
                };
                values[3].Precision = 10; values[3].Scale = 2;

                if (currentEnd == null)
                {
                    ModuleDb.NonQuery(connection, tx,
                        @"INSERT INTO dbo.TenderPublicationSchedule
                            (TenderId,PublicationStartUtc,OriginalClosingUtc,CurrentClosingUtc,PublicationPeriodDays,Remarks,ApprovedBy)
                          VALUES(@TenderId,@Start,@Closing,@Closing,@Days,@Remarks,@UserId);", values);
                }
                else
                {
                    ModuleDb.NonQuery(connection, tx,
                        @"UPDATE dbo.TenderPublicationSchedule
                          SET PublicationStartUtc=@Start,OriginalClosingUtc=@Closing,CurrentClosingUtc=@Closing,
                              PublicationPeriodDays=@Days,Remarks=@Remarks,ApprovedBy=@UserId,
                              ApprovedAtUtc=SYSUTCDATETIME(),ModifiedAtUtc=SYSUTCDATETIME()
                          WHERE TenderId=@TenderId;", values);
                }

                ModuleDb.AddLifecycleHistory(connection, tx, TenderId, status, status, "Publication schedule approved",
                    CurrentUserId, CurrentRole, CurrentDepartmentId, remarks, null);
                ModuleDb.AddAudit(connection, tx, CurrentUserId, "ApprovePublicationSchedule", "TenderPublicationSchedule", TenderId,
                    "Start UTC: " + startUtc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) +
                    "; closing UTC: " + closingUtc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) +
                    "; days: " + days.ToString("0.00", CultureInfo.InvariantCulture), null);
                tx.Commit();
            }
        }

        private void SubmitExtension(DateTime proposedUtc, string applicationPath, string supportingPath)
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction tx = connection.BeginTransaction())
            {
                string status; int tenderDepartmentId, createdBy;
                if (!ModuleDb.LockTender(connection, tx, TenderId, out status, out tenderDepartmentId, out createdBy))
                    throw new InvalidOperationException("Tender not found.");
                if ((CurrentRole != "DepartmentOfficer" && CurrentRole != "Admin") ||
                    !ModuleDb.UserHasRole(connection, tx, CurrentUserId, CurrentRole))
                    throw new InvalidOperationException("Only the authorized department officer can request an extension.");
                if (CurrentDepartmentId.HasValue && CurrentDepartmentId.Value != tenderDepartmentId)
                    throw new InvalidOperationException("The tender is outside your department.");
                if (CurrentRole != "Admin" && ModuleDb.UserOutsideTenderDepartment(connection, tx, CurrentUserId, tenderDepartmentId))
                    throw new InvalidOperationException("The tender is outside your assigned department.");
                if (CurrentRole != "Admin" && createdBy != CurrentUserId)
                    throw new InvalidOperationException("You may only request extensions for tenders you own.");
                if (status != "Tender Published" ||
                    ModuleDb.Exists(connection, tx, "SELECT COUNT(1) FROM dbo.TenderBidOpening WHERE TenderId=@TenderId;",
                        ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId)))
                    throw new InvalidOperationException("Extensions are unavailable after bid opening or evaluation begins.");

                object closing = ModuleDb.Scalar(connection, tx,
                    "SELECT CurrentClosingUtc FROM dbo.TenderPublicationSchedule WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId;",
                    ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId));
                if (closing == null) throw new InvalidOperationException("The tender has no approved publication schedule.");
                DateTime closingUtc = Convert.ToDateTime(closing);
                if (closingUtc <= ModuleDb.UtcNow(connection, tx))
                    throw new InvalidOperationException("An extension must be requested before publication closes.");
                if (proposedUtc <= closingUtc)
                    throw new InvalidOperationException("The proposed closing time must be after the current closing time.");
                if (ModuleDb.Exists(connection, tx,
                    "SELECT COUNT(1) FROM dbo.TenderExtensionRequests WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId AND ReviewStatus=N'Pending';",
                    ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId)))
                    throw new InvalidOperationException("An extension request is already awaiting review.");

                int extensionNo = Convert.ToInt32(ModuleDb.Scalar(connection, tx,
                    "SELECT COUNT(*)+1 FROM dbo.TenderExtensionRequests WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId AND ReviewStatus=N'Approved';",
                    ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId)));
                if (extensionNo > 3)
                    throw new InvalidOperationException("The maximum of three approved extensions has been reached.");

                int requestId = Convert.ToInt32(ModuleDb.Scalar(connection, tx,
                    @"INSERT INTO dbo.TenderExtensionRequests
                        (TenderId,ExtensionNumber,OriginalClosingUtc,ProposedClosingUtc,
                         ApplicationFileName,ApplicationFilePath,SupportingFileName,SupportingFilePath,Reason,SubmittedBy)
                      VALUES(@TenderId,@No,@Original,@Proposed,@AppName,@AppPath,@SupName,@SupPath,@Reason,@UserId);
                      SELECT CONVERT(INT,SCOPE_IDENTITY());",
                    ModuleDb.Param("@TenderId", SqlDbType.Int, TenderId),
                    ModuleDb.Param("@No", SqlDbType.Int, extensionNo),
                    ModuleDb.Param("@Original", SqlDbType.DateTime2, closingUtc),
                    ModuleDb.Param("@Proposed", SqlDbType.DateTime2, proposedUtc),
                    ModuleDb.Param("@AppName", SqlDbType.NVarChar, ModuleDb.Text(TenderLifecycleFiles.SafeFileName(ApplicationFile.FileName)), 260),
                    ModuleDb.Param("@AppPath", SqlDbType.NVarChar, ModuleDb.Text(applicationPath), 400),
                    ModuleDb.Param("@SupName", SqlDbType.NVarChar, ModuleDb.Text(TenderLifecycleFiles.SafeFileName(SupportingFile.FileName)), 260),
                    ModuleDb.Param("@SupPath", SqlDbType.NVarChar, ModuleDb.Text(supportingPath), 400),
                    ModuleDb.Param("@Reason", SqlDbType.NVarChar, ModuleDb.Text(ExtensionReason.Text), 1000),
                    ModuleDb.Param("@UserId", SqlDbType.Int, CurrentUserId)));

                ModuleDb.AddLifecycleHistory(connection, tx, TenderId, status, status, "Extension requested",
                    CurrentUserId, CurrentRole, CurrentDepartmentId, ExtensionReason.Text, applicationPath);
                ModuleDb.AddAudit(connection, tx, CurrentUserId, "Submit", "TenderExtensionRequest", requestId, ExtensionReason.Text, null);
                tx.Commit();
            }
        }

        private void ReviewExtension(int requestId, bool approve)
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction tx = connection.BeginTransaction())
            {
                if ((CurrentRole != "CAFOOfficer" && CurrentRole != "Admin") ||
                    !ModuleDb.UserHasRole(connection, tx, CurrentUserId, CurrentRole))
                    throw new InvalidOperationException("Only the authorized CAFO or administrator can review extensions.");

                int tenderId = 0, submittedBy = 0, extensionNo = 0;
                DateTime originalClosing = DateTime.MinValue, proposedClosing = DateTime.MinValue;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT e.TenderId,e.OriginalClosingUtc,e.ProposedClosingUtc,e.SubmittedBy,e.ExtensionNumber
                      FROM dbo.TenderExtensionRequests e WITH (UPDLOCK,HOLDLOCK)
                      WHERE e.ExtensionRequestId=@Id AND e.ReviewStatus=N'Pending';", connection, tx))
                {
                    command.Parameters.Add(ModuleDb.Param("@Id", SqlDbType.Int, requestId));
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            tenderId = Convert.ToInt32(reader["TenderId"]);
                            originalClosing = Convert.ToDateTime(reader["OriginalClosingUtc"]);
                            proposedClosing = Convert.ToDateTime(reader["ProposedClosingUtc"]);
                            submittedBy = Convert.ToInt32(reader["SubmittedBy"]);
                            extensionNo = Convert.ToInt32(reader["ExtensionNumber"]);
                        }
                    }
                }
                if (tenderId == 0) throw new InvalidOperationException("The extension request is no longer pending.");

                string status; int tenderDepartmentId, createdBy;
                ModuleDb.LockTender(connection, tx, tenderId, out status, out tenderDepartmentId, out createdBy);
                if (CurrentDepartmentId.HasValue && CurrentDepartmentId.Value != tenderDepartmentId)
                    throw new InvalidOperationException("The tender is outside your department.");
                if (CurrentRole != "Admin" && ModuleDb.UserOutsideTenderDepartment(connection, tx, CurrentUserId, tenderDepartmentId))
                    throw new InvalidOperationException("The tender is outside your assigned department.");
                if (status != "Tender Published" ||
                    ModuleDb.Exists(connection, tx, "SELECT COUNT(1) FROM dbo.TenderBidOpening WHERE TenderId=@TenderId;",
                        ModuleDb.Param("@TenderId", SqlDbType.Int, tenderId)))
                    throw new InvalidOperationException("The tender has entered bid opening or evaluation.");

                object scheduleClosing = ModuleDb.Scalar(connection, tx,
                    "SELECT CurrentClosingUtc FROM dbo.TenderPublicationSchedule WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId;",
                    ModuleDb.Param("@TenderId", SqlDbType.Int, tenderId));
                if (scheduleClosing == null || Convert.ToDateTime(scheduleClosing) != originalClosing)
                    throw new InvalidOperationException("The publication schedule changed; the request must be resubmitted.");

                if (approve)
                {
                    if (Convert.ToInt32(ModuleDb.Scalar(connection, tx,
                        "SELECT COUNT(*) FROM dbo.TenderExtensionRequests WITH (UPDLOCK,HOLDLOCK) WHERE TenderId=@TenderId AND ReviewStatus=N'Approved';",
                        ModuleDb.Param("@TenderId", SqlDbType.Int, tenderId))) >= 3)
                        throw new InvalidOperationException("The maximum of three approved extensions has been reached.");
                    ModuleDb.NonQuery(connection, tx,
                        "UPDATE dbo.TenderPublicationSchedule SET CurrentClosingUtc=@Proposed,ModifiedAtUtc=SYSUTCDATETIME() WHERE TenderId=@TenderId;",
                        ModuleDb.Param("@Proposed", SqlDbType.DateTime2, proposedClosing),
                        ModuleDb.Param("@TenderId", SqlDbType.Int, tenderId));
                }

                string decisionStatus = approve ? "Approved" : "Rejected";
                string remarks = ReviewRemarks.Text;
                ModuleDb.NonQuery(connection, tx,
                    @"UPDATE dbo.TenderExtensionRequests
                      SET ReviewStatus=@Status,ReviewedBy=@UserId,ReviewedAtUtc=SYSUTCDATETIME(),ReviewRemarks=@Remarks
                      WHERE ExtensionRequestId=@Id AND ReviewStatus=N'Pending';",
                    ModuleDb.Param("@Status", SqlDbType.NVarChar, decisionStatus, 20),
                    ModuleDb.Param("@UserId", SqlDbType.Int, CurrentUserId),
                    ModuleDb.Param("@Remarks", SqlDbType.NVarChar, ModuleDb.Text(remarks), 1000),
                    ModuleDb.Param("@Id", SqlDbType.Int, requestId));
                ModuleDb.NonQuery(connection, tx,
                    @"INSERT INTO dbo.ApprovalHistory
                        (TenderId,ApprovalStage,PreviousStatus,NewStatus,Status,ApprovedBy,RoleAtAction,Remarks)
                      VALUES(@TenderId,N'PublicationExtension',@Status0,@Status0,@Decision,@UserId,@Role,@Remarks);",
                    ModuleDb.Param("@TenderId", SqlDbType.Int, tenderId),
                    ModuleDb.Param("@Status0", SqlDbType.NVarChar, status, 50),
                    ModuleDb.Param("@Decision", SqlDbType.NVarChar, decisionStatus, 20),
                    ModuleDb.Param("@UserId", SqlDbType.Int, CurrentUserId),
                    ModuleDb.Param("@Role", SqlDbType.NVarChar, CurrentRole, 50),
                    ModuleDb.Param("@Remarks", SqlDbType.NVarChar, ModuleDb.Text(remarks), 1000));
                ModuleDb.AddLifecycleHistory(connection, tx, tenderId, status, status,
                    approve ? "Extension approved" : "Extension rejected",
                    CurrentUserId, CurrentRole, CurrentDepartmentId, remarks, null);
                ModuleDb.AddAudit(connection, tx, CurrentUserId, approve ? "Approve" : "Reject", "TenderExtensionRequest", requestId,
                    "Extension " + extensionNo.ToString(CultureInfo.InvariantCulture) + "; submitted by user " +
                    submittedBy.ToString(CultureInfo.InvariantCulture) + "; " + (remarks ?? String.Empty), null);
                tx.Commit();
            }
        }

        private void ShowError(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
