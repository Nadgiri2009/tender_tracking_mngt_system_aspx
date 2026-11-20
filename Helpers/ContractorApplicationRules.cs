using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;

namespace DepartmentModule.Helpers
{
    public static class ContractorApplicationRules
    {
        public const string TermsVersion = "portal-terms-v1";
        public const int MaximumFilesPerRequest = 10;
        public const int MaximumFileSizeBytes = 10 * 1024 * 1024;
        public const int MaximumRequestBytes = 50 * 1000 * 1000;

        public static DataTable GetRequirements(SqlConnection connection, SqlTransaction transaction, int tenderId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT DocumentType,Description,IsMandatory,AllowedExtensions,MaxFileSizeBytes
                  FROM dbo.TenderRequiredBidDocuments
                  WHERE TenderId=@TenderId ORDER BY DocumentType;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                return data;
            }
        }

        public static int? GetApplicationId(SqlConnection connection, SqlTransaction transaction,
            int tenderId, int contractorId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT TechnicalBidId FROM dbo.TechnicalBid
                  WHERE TenderId=@TenderId AND ContractorId=@ContractorId;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }

        public static string GetStatus(SqlConnection connection, SqlTransaction transaction, int applicationId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COALESCE(
                      (SELECT TOP (1) h.NewStatus
                       FROM dbo.ContractorApplicationHistory h
                       WHERE h.TechnicalBidId=b.TechnicalBidId AND h.NewStatus IS NOT NULL
                       ORDER BY h.HistoryId DESC), b.Status)
                  FROM dbo.TechnicalBid b WHERE b.TechnicalBidId=@ApplicationId;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? String.Empty : Convert.ToString(value);
            }
        }

        public static bool IsSubmissionWindowOpen(SqlConnection connection, SqlTransaction transaction, int tenderId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.Tender t
                  INNER JOIN dbo.TenderPublicationSchedule s ON s.TenderId=t.TenderId
                  WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                    AND t.CurrentStatus=N'Tender Published'
                    AND s.PublicationStartUtc<=SYSUTCDATETIME()
                    AND s.CurrentClosingUtc>=SYSUTCDATETIME()
                    AND (t.SubmissionStartDate IS NULL OR t.SubmissionStartDate<=SYSUTCDATETIME())
                    AND (t.SubmissionDeadline IS NULL OR t.SubmissionDeadline>=SYSUTCDATETIME());",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        public static string GetReference(int applicationId, DateTime createdAtUtc)
        {
            return "TAPP-" + createdAtUtc.ToUniversalTime().Year.ToString(CultureInfo.InvariantCulture) +
                "-" + applicationId.ToString("D6", CultureInfo.InvariantCulture);
        }

        public static void AddHistory(SqlConnection connection, SqlTransaction transaction, int applicationId,
            string eventType, string previousStatus, string newStatus, string remarks, int userId, int roleId,
            bool visibleToContractor)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.ContractorApplicationHistory
                    (TechnicalBidId,EventType,PreviousStatus,NewStatus,Remarks,CreatedBy,RoleId,VisibleToContractor)
                  VALUES(@ApplicationId,@EventType,@PreviousStatus,@NewStatus,@Remarks,@UserId,@RoleId,@Visible);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                ModuleDb.AddParameter(command, "@EventType", SqlDbType.NVarChar, eventType, 40);
                ModuleDb.AddParameter(command, "@PreviousStatus", SqlDbType.NVarChar, EmptyToNull(previousStatus), 30);
                ModuleDb.AddParameter(command, "@NewStatus", SqlDbType.NVarChar, EmptyToNull(newStatus), 30);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar, EmptyToNull(remarks), 2000);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId > 0 ? (object)roleId : null);
                ModuleDb.AddParameter(command, "@Visible", SqlDbType.Bit, visibleToContractor);
                command.ExecuteNonQuery();
            }
        }

        public static void AddNotification(SqlConnection connection, SqlTransaction transaction, int userId,
            int tenderId, string notificationType, string message)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,NotificationType,Message)
                  VALUES(@UserId,@TenderId,@Type,@Message);", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@Type", SqlDbType.NVarChar, notificationType, 60);
                ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar, message, 1000);
                command.ExecuteNonQuery();
            }
        }

        public static DataTable GetApplicationNotifications(SqlConnection connection, int userId)
        {
            bool supportsScheduledNotifications = HasScheduledNotificationAvailability(connection);
            using (SqlCommand command = new SqlCommand(
                supportsScheduledNotifications
                     ? @"SELECT TOP (20) n.NotificationId,n.TenderId,t.TenderNumber,n.NotificationType,
                                n.Message,n.CreatedAtUtc,n.ReadAtUtc
                         FROM dbo.TenderNotifications n
                         INNER JOIN dbo.Tender t ON t.TenderId=n.TenderId
                         WHERE n.UserId=@UserId
                           AND (n.NotificationType LIKE N'Application%' OR n.NotificationType LIKE N'Shortfall%')
                           AND n.AvailableAtUtc<=SYSUTCDATETIME()
                         ORDER BY n.CreatedAtUtc DESC,n.NotificationId DESC;"
                     : @"SELECT TOP (20) n.NotificationId,n.TenderId,t.TenderNumber,n.NotificationType,
                                n.Message,n.CreatedAtUtc,n.ReadAtUtc
                         FROM dbo.TenderNotifications n
                         INNER JOIN dbo.Tender t ON t.TenderId=n.TenderId
                         WHERE n.UserId=@UserId
                           AND (n.NotificationType LIKE N'Application%' OR n.NotificationType LIKE N'Shortfall%')
                         ORDER BY n.CreatedAtUtc DESC,n.NotificationId DESC;",
                connection))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                DataTable data = new DataTable();
                using (SqlDataAdapter adapter = new SqlDataAdapter(command)) adapter.Fill(data);
                return data;
            }
        }

        public static bool HasScheduledNotificationAvailability(SqlConnection connection)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT CASE WHEN COL_LENGTH(N'dbo.TenderNotifications',N'AvailableAtUtc') IS NULL
                               THEN 0 ELSE 1 END;", connection))
            {
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        public static bool MarkApplicationNotificationRead(SqlConnection connection,
            long notificationId, int userId)
        {
            string query = HasScheduledNotificationAvailability(connection)
                ? @"UPDATE dbo.TenderNotifications
                     SET ReadAtUtc=SYSUTCDATETIME()
                     WHERE NotificationId=@NotificationId AND UserId=@UserId
                       AND (NotificationType LIKE N'Application%' OR NotificationType LIKE N'Shortfall%')
                       AND AvailableAtUtc<=SYSUTCDATETIME() AND ReadAtUtc IS NULL;"
                : @"UPDATE dbo.TenderNotifications
                     SET ReadAtUtc=SYSUTCDATETIME()
                     WHERE NotificationId=@NotificationId AND UserId=@UserId
                       AND (NotificationType LIKE N'Application%' OR NotificationType LIKE N'Shortfall%')
                       AND ReadAtUtc IS NULL;";
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                ModuleDb.AddParameter(command, "@NotificationId", SqlDbType.BigInt, notificationId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                return command.ExecuteNonQuery() == 1;
            }
        }

        private static object EmptyToNull(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
