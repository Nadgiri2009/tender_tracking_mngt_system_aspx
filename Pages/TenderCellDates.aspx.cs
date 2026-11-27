using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class TenderCellDates : ModulePage
    {
        protected Literal Message, TenderHeading, TenderStatus;
        protected Panel QueuePanel, EditPanel, ReadOnlyPanel;
        protected GridView TenderQueue, DateHistoryGrid;
        protected TextBox PublicationDateText, SubmissionStartText, SubmissionClosingText,
            TechnicalOpeningText, FinancialOpeningText, PreBidMeetingText, PublicationStartText,
            PublicationCloseText, PreBidLocationText, PreBidDetailsText, ScheduleRemarksText;
        protected Button SaveDatesButton;

        private int TenderId { get { return QueryId("id"); } }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!HasAnyRole("Tender Cell") || !CurrentDepartmentId.HasValue)
            {
                Response.StatusCode = 403;
                Response.TrySkipIisCustomErrors = true;
                Response.Redirect("~/Pages/Error.aspx?code=403", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (!IsPostBack)
            {
                BindTenderQueue();
                if (TenderId > 0) LoadTenderDates();
            }
        }

        private void BindTenderQueue()
        {
            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.TenderId,t.TenderNumber,t.Title,t.CurrentStatus,
                         s.PublicationStartUtc,s.CurrentClosingUtc,s.ModifiedAtUtc
                  FROM dbo.Tender t
                  LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                  WHERE t.IsDeleted=0 AND t.DepartmentId=@DepartmentId
                  ORDER BY CASE WHEN s.TenderId IS NULL THEN 0 ELSE 1 END,
                           t.CreatedDate DESC,t.TenderId DESC;", connection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, CurrentDepartmentId.Value);
                DataTable data = new DataTable();
                adapter.Fill(data);
                data.Columns.Add("PublicationStartIst", typeof(string));
                data.Columns.Add("CurrentClosingIst", typeof(string));
                data.Columns.Add("ModifiedAtIst", typeof(string));
                foreach (DataRow row in data.Rows)
                {
                    row["PublicationStartIst"] = FormatIndiaDateTime(row["PublicationStartUtc"]);
                    row["CurrentClosingIst"] = FormatIndiaDateTime(row["CurrentClosingUtc"]);
                    row["ModifiedAtIst"] = FormatIndiaDateTime(row["ModifiedAtUtc"]);
                }
                TenderQueue.DataSource = data;
                TenderQueue.DataBind();
            }
        }

        private void LoadTenderDates()
        {
            EditPanel.Visible = true;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT t.TenderId,t.TenderNumber,t.Title,t.CurrentStatus,
                             t.PublicationScheduledUtc,t.SubmissionStartDate,t.SubmissionDeadline,
                             t.TechnicalBidOpeningScheduledUtc,t.FinancialBidOpeningScheduledUtc,
                             t.PreBidMeetingUtc,t.PreBidLocation,t.PreBidDetails,
                             s.PublicationStartUtc,s.CurrentClosingUtc,s.Remarks,
                             CASE WHEN EXISTS
                                  (SELECT 1 FROM dbo.ApprovalHistory ah
                                   WHERE ah.TenderId=t.TenderId AND ah.NewStatus=N'Tender Published')
                                  OR EXISTS
                                  (SELECT 1 FROM dbo.TenderWorkflowHistory wh
                                   WHERE wh.TenderId=t.TenderId AND wh.NewStatus=N'Tender Published')
                                  THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasBeenPublished
                      FROM dbo.Tender t
                      LEFT JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                      WHERE t.TenderId=@TenderId AND t.DepartmentId=@DepartmentId AND t.IsDeleted=0;",
                    connection))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, CurrentDepartmentId.Value);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            DenyAccess();
                            return;
                        }
                        TenderHeading.Text = HttpUtility.HtmlEncode(
                            Convert.ToString(reader["TenderNumber"]) + " — " + Convert.ToString(reader["Title"]));
                        TenderStatus.Text = HttpUtility.HtmlEncode(Convert.ToString(reader["CurrentStatus"]));
                        PublicationDateText.Text = FormatDate(reader["PublicationScheduledUtc"]);
                        SubmissionStartText.Text = FormatDate(reader["SubmissionStartDate"]);
                        SubmissionClosingText.Text = FormatDate(reader["SubmissionDeadline"]);
                        TechnicalOpeningText.Text = FormatDate(reader["TechnicalBidOpeningScheduledUtc"]);
                        FinancialOpeningText.Text = FormatDate(reader["FinancialBidOpeningScheduledUtc"]);
                        PreBidMeetingText.Text = FormatDate(reader["PreBidMeetingUtc"]);
                        PreBidLocationText.Text = Convert.ToString(reader["PreBidLocation"] == DBNull.Value
                            ? String.Empty : reader["PreBidLocation"]);
                        PreBidDetailsText.Text = Convert.ToString(reader["PreBidDetails"] == DBNull.Value
                            ? String.Empty : reader["PreBidDetails"]);
                        PublicationStartText.Text = FormatDateTime(reader["PublicationStartUtc"]);
                        PublicationCloseText.Text = FormatDateTime(reader["CurrentClosingUtc"]);
                        ScheduleRemarksText.Text = Convert.ToString(reader["Remarks"] == DBNull.Value
                            ? String.Empty : reader["Remarks"]);
                        bool isPublished = Convert.ToBoolean(reader["HasBeenPublished"], CultureInfo.InvariantCulture);
                        ReadOnlyPanel.Visible = isPublished;
                        SaveDatesButton.Visible = !isPublished;
                        PublicationDateText.Enabled = !isPublished;
                        SubmissionStartText.Enabled = !isPublished;
                        SubmissionClosingText.Enabled = !isPublished;
                        TechnicalOpeningText.Enabled = !isPublished;
                        FinancialOpeningText.Enabled = !isPublished;
                        PreBidMeetingText.Enabled = !isPublished;
                        PublicationStartText.Enabled = !isPublished;
                        PublicationCloseText.Enabled = !isPublished;
                        PreBidLocationText.Enabled = !isPublished;
                        PreBidDetailsText.Enabled = !isPublished;
                        ScheduleRemarksText.Enabled = !isPublished;
                    }
                }
                BindDateHistory(connection: null);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender dates could not be loaded.", ex);
                ShowMessage("Tender dates could not be loaded. Please retry or contact support.");
            }
        }

        private void BindDateHistory(SqlConnection connection)
        {
            bool ownsConnection = connection == null;
            if (ownsConnection) connection = ModuleDb.OpenConnection();
            try
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT TOP (100) h.FieldName,h.PreviousValue,h.NewValue,
                             u.FullName AS ModifiedByName,h.ModifiedAtUtc
                      FROM dbo.TenderImportantDateHistory h
                      INNER JOIN dbo.Users u ON u.UserId=h.ModifiedBy
                      WHERE h.TenderId=@TenderId
                      ORDER BY h.ModifiedAtUtc DESC,h.HistoryId DESC;", connection))
                using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                    DataTable history = new DataTable();
                    adapter.Fill(history);
                    DateHistoryGrid.DataSource = history;
                    DateHistoryGrid.DataBind();
                }
            }
            finally
            {
                if (ownsConnection) connection.Dispose();
            }
        }

        protected void SaveDatesButton_Click(object sender, EventArgs e)
        {
            DateTime? publication, submissionStart, submissionClosing, technicalOpening,
                financialOpening, preBidMeeting;
            DateTime publicationStartUtc, publicationCloseUtc;
            if (!TryParseOptionalDate(PublicationDateText.Text, false, out publication) ||
                !TryParseOptionalDate(SubmissionStartText.Text, false, out submissionStart) ||
                !TryParseOptionalDate(SubmissionClosingText.Text, true, out submissionClosing) ||
                !TryParseOptionalDate(TechnicalOpeningText.Text, false, out technicalOpening) ||
                !TryParseOptionalDate(FinancialOpeningText.Text, false, out financialOpening) ||
                !TryParseOptionalDate(PreBidMeetingText.Text, false, out preBidMeeting) ||
                !TenderDateRules.TryParseIndiaLocalDateTime(PublicationStartText.Text, out publicationStartUtc) ||
                !TenderDateRules.TryParseIndiaLocalDateTime(PublicationCloseText.Text, out publicationCloseUtc))
            {
                ShowMessage("Enter a valid publication window and valid dates (IST). Both publication start and closing are required.");
                return;
            }
            if (publicationStartUtc <= DateTime.UtcNow || publicationCloseUtc <= publicationStartUtc)
            {
                ShowMessage("The publication start must be in the future and publication closing must be later than the start.");
                return;
            }
            string validationError = TenderDateRules.ValidateTenderSchedule(publication, submissionStart,
                submissionClosing, technicalOpening, financialOpening, preBidMeeting, DateTime.UtcNow, false);
            if (!String.IsNullOrEmpty(validationError))
            {
                ShowMessage(validationError);
                return;
            }
            if (PreBidLocationText.Text.Length > 300 || PreBidDetailsText.Text.Length > 2000 ||
                ScheduleRemarksText.Text.Length > 1000)
            {
                ShowMessage("Pre-bid details or Tender Cell remarks exceed the allowed length.");
                return;
            }

            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    TenderDateValues previous = ReadExistingValues(connection, transaction);
                    if (previous == null)
                        throw new InvalidOperationException("This tender is unavailable in your department.");
                    if (previous.HasBeenPublished)
                        throw new InvalidOperationException("Published tender dates cannot be changed here. Use the existing extension workflow.");

                    SaveTenderDateFields(connection, transaction, publication, submissionStart,
                        submissionClosing, technicalOpening, financialOpening, preBidMeeting);
                    SavePublicationWindow(connection, transaction, publicationStartUtc,
                        publicationCloseUtc, ScheduleRemarksText.Text.Trim());

                    RecordChange(connection, transaction, "PublicationScheduledUtc",
                        previous.PublicationScheduledUtc, publication);
                    RecordChange(connection, transaction, "SubmissionStartDate",
                        previous.SubmissionStartDate, submissionStart);
                    RecordChange(connection, transaction, "SubmissionDeadline",
                        previous.SubmissionDeadline, submissionClosing);
                    RecordChange(connection, transaction, "TechnicalBidOpeningScheduledUtc",
                        previous.TechnicalOpeningUtc, technicalOpening);
                    RecordChange(connection, transaction, "FinancialBidOpeningScheduledUtc",
                        previous.FinancialOpeningUtc, financialOpening);
                    RecordChange(connection, transaction, "PreBidMeetingUtc",
                        previous.PreBidMeetingUtc, preBidMeeting);
                    RecordChange(connection, transaction, "PublicationStartUtc",
                        previous.PublicationStartUtc, publicationStartUtc);
                    RecordChange(connection, transaction, "PublicationClosingUtc",
                        previous.PublicationCloseUtc, publicationCloseUtc);

                    ModuleDb.AddAudit(connection, transaction, CurrentUserId, "Update",
                        "TenderImportantDates", TenderId, previous.TenderNumber,
                        Request.UserHostAddress);
                    transaction.Commit();
                }
                ShowMessage("Important dates saved.");
                LoadTenderDates();
                BindTenderQueue();
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Tender date changes could not be saved.", ex);
                ShowMessage("Tender dates could not be saved. Please retry or contact support.");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private TenderDateValues ReadExistingValues(SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.TenderNumber,t.CurrentStatus,t.PublicationScheduledUtc,t.SubmissionStartDate,
                         t.SubmissionDeadline,t.TechnicalBidOpeningScheduledUtc,
                         t.FinancialBidOpeningScheduledUtc,t.PreBidMeetingUtc,
                         s.PublicationStartUtc,s.CurrentClosingUtc,
                         CASE WHEN EXISTS
                              (SELECT 1 FROM dbo.ApprovalHistory ah
                               WHERE ah.TenderId=t.TenderId AND ah.NewStatus=N'Tender Published')
                              OR EXISTS
                              (SELECT 1 FROM dbo.TenderWorkflowHistory wh
                               WHERE wh.TenderId=t.TenderId AND wh.NewStatus=N'Tender Published')
                              THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasBeenPublished
                  FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
                  LEFT JOIN dbo.TenderPublicationSchedule s WITH (UPDLOCK,HOLDLOCK)
                    ON s.TenderId=t.TenderId
                  WHERE t.TenderId=@TenderId AND t.DepartmentId=@DepartmentId AND t.IsDeleted=0;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, CurrentDepartmentId.Value);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new TenderDateValues
                    {
                        TenderNumber = Convert.ToString(reader["TenderNumber"]),
                        Status = Convert.ToString(reader["CurrentStatus"]),
                        HasBeenPublished = Convert.ToBoolean(reader["HasBeenPublished"], CultureInfo.InvariantCulture),
                        PublicationScheduledUtc = ReadNullableDate(reader["PublicationScheduledUtc"]),
                        SubmissionStartDate = ReadNullableDate(reader["SubmissionStartDate"]),
                        SubmissionDeadline = ReadNullableDate(reader["SubmissionDeadline"]),
                        TechnicalOpeningUtc = ReadNullableDate(reader["TechnicalBidOpeningScheduledUtc"]),
                        FinancialOpeningUtc = ReadNullableDate(reader["FinancialBidOpeningScheduledUtc"]),
                        PreBidMeetingUtc = ReadNullableDate(reader["PreBidMeetingUtc"]),
                        PublicationStartUtc = ReadNullableDate(reader["PublicationStartUtc"]),
                        PublicationCloseUtc = ReadNullableDate(reader["CurrentClosingUtc"])
                    };
                }
            }
        }

        private void SaveTenderDateFields(SqlConnection connection, SqlTransaction transaction,
            DateTime? publication, DateTime? submissionStart, DateTime? submissionClosing,
            DateTime? technicalOpening, DateTime? financialOpening, DateTime? preBidMeeting)
        {
            using (SqlCommand command = new SqlCommand(
                @"UPDATE dbo.Tender
                  SET PublicationScheduledUtc=@PublicationScheduledUtc,
                      SubmissionStartDate=@SubmissionStartDate,SubmissionDeadline=@SubmissionDeadline,
                      TechnicalBidOpeningScheduledUtc=@TechnicalOpeningUtc,
                      FinancialBidOpeningScheduledUtc=@FinancialOpeningUtc,
                      PreBidMeetingUtc=@PreBidMeetingUtc,
                      PreBidLocation=@PreBidLocation,PreBidDetails=@PreBidDetails,
                      ModifiedBy=@ModifiedBy,ModifiedDate=SYSUTCDATETIME()
                  WHERE TenderId=@TenderId AND DepartmentId=@DepartmentId AND IsDeleted=0
                    AND CurrentStatus<>N'Tender Published';", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@PublicationScheduledUtc", SqlDbType.DateTime2, publication);
                ModuleDb.AddParameter(command, "@SubmissionStartDate", SqlDbType.DateTime2, submissionStart);
                ModuleDb.AddParameter(command, "@SubmissionDeadline", SqlDbType.DateTime2, submissionClosing);
                ModuleDb.AddParameter(command, "@TechnicalOpeningUtc", SqlDbType.DateTime2, technicalOpening);
                ModuleDb.AddParameter(command, "@FinancialOpeningUtc", SqlDbType.DateTime2, financialOpening);
                ModuleDb.AddParameter(command, "@PreBidMeetingUtc", SqlDbType.DateTime2, preBidMeeting);
                ModuleDb.AddParameter(command, "@PreBidLocation", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(PreBidLocationText.Text) ? (object)null : PreBidLocationText.Text.Trim(), 300);
                ModuleDb.AddParameter(command, "@PreBidDetails", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(PreBidDetailsText.Text) ? (object)null : PreBidDetailsText.Text.Trim(), 2000);
                ModuleDb.AddParameter(command, "@ModifiedBy", SqlDbType.Int, CurrentUserId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, CurrentDepartmentId.Value);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The tender could not be updated. Refresh and retry.");
            }
        }

        private void SavePublicationWindow(SqlConnection connection, SqlTransaction transaction,
            DateTime startUtc, DateTime closeUtc, string remarks)
        {
            decimal duration = Decimal.Round((decimal)(closeUtc - startUtc).TotalDays, 2);
            using (SqlCommand command = new SqlCommand(
                @"IF EXISTS(SELECT 1 FROM dbo.TenderPublicationSchedule WITH (UPDLOCK,HOLDLOCK)
                            WHERE TenderId=@TenderId)
                    UPDATE dbo.TenderPublicationSchedule
                    SET PublicationStartUtc=@StartUtc,OriginalClosingUtc=@CloseUtc,
                        CurrentClosingUtc=@CloseUtc,PublicationPeriodDays=@Duration,
                        Remarks=@Remarks,ApprovedBy=@UserId,ApprovedAtUtc=SYSUTCDATETIME(),
                        ModifiedAtUtc=SYSUTCDATETIME()
                    WHERE TenderId=@TenderId;
                  ELSE
                    INSERT INTO dbo.TenderPublicationSchedule
                        (TenderId,PublicationStartUtc,OriginalClosingUtc,CurrentClosingUtc,
                         PublicationPeriodDays,Remarks,ApprovedBy,ApprovedAtUtc,ModifiedAtUtc)
                    VALUES(@TenderId,@StartUtc,@CloseUtc,@CloseUtc,@Duration,@Remarks,@UserId,
                           SYSUTCDATETIME(),SYSUTCDATETIME());", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@StartUtc", SqlDbType.DateTime2, startUtc);
                ModuleDb.AddParameter(command, "@CloseUtc", SqlDbType.DateTime2, closeUtc);
                ModuleDb.AddParameter(command, "@Duration", SqlDbType.Decimal, duration);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(remarks) ? (object)null : remarks, 1000);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                command.ExecuteNonQuery();
            }
        }

        private void RecordChange(SqlConnection connection, SqlTransaction transaction,
            string fieldName, DateTime? previous, DateTime? current)
        {
            if (previous == current) return;
            InsertHistory(connection, transaction, fieldName, FormatHistoryValue(previous),
                FormatHistoryValue(current));
        }

        private void RecordChange(SqlConnection connection, SqlTransaction transaction,
            string fieldName, DateTime? previous, DateTime current)
        {
            RecordChange(connection, transaction, fieldName, previous, (DateTime?)current);
        }

        private void InsertHistory(SqlConnection connection, SqlTransaction transaction,
            string fieldName, string previous, string current)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderImportantDateHistory
                    (TenderId,FieldName,PreviousValue,NewValue,ModifiedBy)
                  VALUES(@TenderId,@FieldName,@PreviousValue,@NewValue,@ModifiedBy);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, TenderId);
                ModuleDb.AddParameter(command, "@FieldName", SqlDbType.NVarChar, fieldName, 64);
                ModuleDb.AddParameter(command, "@PreviousValue", SqlDbType.NVarChar, previous, 2000);
                ModuleDb.AddParameter(command, "@NewValue", SqlDbType.NVarChar, current, 2000);
                ModuleDb.AddParameter(command, "@ModifiedBy", SqlDbType.Int, CurrentUserId);
                command.ExecuteNonQuery();
            }
        }

        private static bool TryParseOptionalDate(string value, bool endOfDay, out DateTime? utc)
        {
            utc = null;
            if (String.IsNullOrWhiteSpace(value)) return true;
            DateTime parsed;
            if (!TenderDateRules.TryParseIndiaLocalDate(value, out parsed, endOfDay)) return false;
            utc = parsed;
            return true;
        }

        private static DateTime? ReadNullableDate(object value)
        {
            return value == null || value == DBNull.Value
                ? (DateTime?)null
                : DateTime.SpecifyKind(Convert.ToDateTime(value, CultureInfo.InvariantCulture), DateTimeKind.Utc);
        }

        private static string FormatDate(object value)
        {
            DateTime? utc = ReadNullableDate(value);
            return utc.HasValue ? TenderDateRules.GetIndiaCalendarDate(utc.Value)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : String.Empty;
        }

        private static string FormatDateTime(object value)
        {
            DateTime? utc = ReadNullableDate(value);
            if (!utc.HasValue) return String.Empty;
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(utc.Value,
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
            return local.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        }

        private static string FormatIndiaDateTime(object value)
        {
            DateTime? utc = ReadNullableDate(value);
            return utc.HasValue
                ? TimeZoneInfo.ConvertTimeFromUtc(utc.Value,
                    TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"))
                    .ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)
                : String.Empty;
        }

        private static string FormatHistoryValue(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
                : null;
        }

        private void DenyAccess()
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
            Response.Redirect("~/Pages/Error.aspx?code=403", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void ShowMessage(string text)
        {
            Message.Text = "<div class=\"message\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }

        private sealed class TenderDateValues
        {
            public string TenderNumber;
            public string Status;
            public bool HasBeenPublished;
            public DateTime? PublicationScheduledUtc;
            public DateTime? SubmissionStartDate;
            public DateTime? SubmissionDeadline;
            public DateTime? TechnicalOpeningUtc;
            public DateTime? FinancialOpeningUtc;
            public DateTime? PreBidMeetingUtc;
            public DateTime? PublicationStartUtc;
            public DateTime? PublicationCloseUtc;
        }
    }
}
