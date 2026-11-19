using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace DepartmentModule.Helpers
{
    public static class ModuleDb
    {
        public static SqlConnection OpenConnection()
        {
            ConnectionStringSettings setting = ConfigurationManager.ConnectionStrings["TenderManagement"];
            if (setting == null || String.IsNullOrWhiteSpace(setting.ConnectionString) ||
                setting.ConnectionString.IndexOf("YOUR_SQL_SERVER", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ConfigurationErrorsException("Set the TenderManagement connection string in Web.config before using the module.");
            }

            SqlConnection connection = new SqlConnection(setting.ConnectionString);
            connection.Open();
            return connection;
        }

        public static void AddParameter(SqlCommand command, string name, SqlDbType type, object value, int size = 0)
        {
            SqlParameter parameter = size == -1
                ? command.Parameters.Add(name, type, -1)
                : size > 0 ? command.Parameters.Add(name, type, size) : command.Parameters.Add(name, type);
            parameter.Value = value ?? DBNull.Value;
            if (type == SqlDbType.Decimal && value is decimal)
            {
                int[] bits = Decimal.GetBits((decimal)value);
                parameter.Precision = 18;
                parameter.Scale = (byte)Math.Min(18, (bits[3] >> 16) & 0x7F);
            }
        }

        public static void AddAudit(SqlConnection connection, SqlTransaction transaction, int? userId,
            string action, string entityName, int? entityId, string details, string ipAddress)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.AuditLog (UserId, Action, EntityName, EntityId, Details, IPAddress)
                  VALUES (@UserId, @Action, @EntityName, @EntityId, @Details, @IPAddress);",
                connection, transaction))
            {
                AddParameter(command, "@UserId", SqlDbType.Int, userId.HasValue ? (object)userId.Value : null);
                AddParameter(command, "@Action", SqlDbType.NVarChar, action, 80);
                AddParameter(command, "@EntityName", SqlDbType.NVarChar, entityName, 100);
                AddParameter(command, "@EntityId", SqlDbType.Int, entityId.HasValue ? (object)entityId.Value : null);
                AddParameter(command, "@Details", SqlDbType.NVarChar, details);
                AddParameter(command, "@IPAddress", SqlDbType.NVarChar, ipAddress, 50);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Builds a typed parameter (null becomes DBNull).</summary>
        public static SqlParameter Param(string name, SqlDbType type, object value, int size = 0)
        {
            SqlParameter parameter = size > 0 ? new SqlParameter(name, type, size) : new SqlParameter(name, type);
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }

        /// <summary>Runs a parameterized query inside the given transaction and returns the first column of the first row.</summary>
        public static object Scalar(SqlConnection connection, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                object value = command.ExecuteScalar();
                return value == DBNull.Value ? null : value;
            }
        }

        /// <summary>Runs a parameterized INSERT/UPDATE inside the given transaction and returns the affected row count.</summary>
        public static int NonQuery(SqlConnection connection, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            using (SqlCommand command = new SqlCommand(sql, connection, transaction))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>True when the active user holds the named role (replaces the old per-procedure role checks).</summary>
        public static bool UserHasRole(SqlConnection connection, SqlTransaction transaction, int userId, string roleName)
        {
            return Convert.ToInt32(Scalar(connection, transaction,
                @"SELECT COUNT(1) FROM dbo.Users u JOIN dbo.Roles r ON r.RoleId=u.RoleId
                  WHERE u.UserId=@UserId AND u.IsActive=1 AND r.RoleName=@RoleName;",
                Param("@UserId", SqlDbType.Int, userId),
                Param("@RoleName", SqlDbType.NVarChar, roleName, 50))) > 0;
        }

        /// <summary>True when the user is assigned to a department other than the tender's department.</summary>
        public static bool UserOutsideTenderDepartment(SqlConnection connection, SqlTransaction transaction, int userId, int tenderDepartmentId)
        {
            return Convert.ToInt32(Scalar(connection, transaction,
                @"SELECT COUNT(1) FROM dbo.Users WHERE UserId=@UserId AND DepartmentId IS NOT NULL AND DepartmentId<>@DepartmentId;",
                Param("@UserId", SqlDbType.Int, userId),
                Param("@DepartmentId", SqlDbType.Int, tenderDepartmentId))) > 0;
        }

        public static bool Exists(SqlConnection connection, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            return Convert.ToInt32(Scalar(connection, transaction, sql, parameters)) > 0;
        }

        public static object Text(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static object NullableInt(int? value)
        {
            return value.HasValue ? (object)value.Value : null;
        }

        /// <summary>Current database UTC time truncated to whole seconds (matches the DATETIME2(0) columns).</summary>
        public static DateTime UtcNow(SqlConnection connection, SqlTransaction transaction)
        {
            return Convert.ToDateTime(Scalar(connection, transaction, "SELECT CONVERT(DATETIME2(0),SYSUTCDATETIME());"));
        }

        /// <summary>Reads and locks a tender row for the rest of the transaction. Returns false when the tender does not exist.</summary>
        public static bool LockTender(SqlConnection connection, SqlTransaction transaction, int tenderId,
            out string status, out int departmentId, out int createdBy)
        {
            status = null; departmentId = 0; createdBy = 0;
            using (SqlCommand command = new SqlCommand(
                @"SELECT CurrentStatus,DepartmentId,CreatedBy FROM dbo.Tender WITH (UPDLOCK,HOLDLOCK)
                  WHERE TenderId=@TenderId AND IsDeleted=0;", connection, transaction))
            {
                command.Parameters.Add(Param("@TenderId", SqlDbType.Int, tenderId));
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return false;
                    status = Convert.ToString(reader["CurrentStatus"]);
                    departmentId = Convert.ToInt32(reader["DepartmentId"]);
                    createdBy = reader["CreatedBy"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CreatedBy"]);
                    return true;
                }
            }
        }

        public static void AddLifecycleHistory(SqlConnection connection, SqlTransaction transaction, int tenderId,
            string previousStatus, string newStatus, string actionName, int userId, string role, int? departmentId,
            string remarks, string relatedDocumentPath)
        {
            NonQuery(connection, transaction,
                @"INSERT INTO dbo.TenderLifecycleHistory
                    (TenderId,PreviousStatus,NewStatus,ActionName,ActionBy,RoleAtAction,DepartmentId,Remarks,RelatedDocumentPath)
                  VALUES(@TenderId,@Prev,@New,@Action,@UserId,@Role,@DepartmentId,@Remarks,@Path);",
                Param("@TenderId", SqlDbType.Int, tenderId),
                Param("@Prev", SqlDbType.NVarChar, previousStatus, 50),
                Param("@New", SqlDbType.NVarChar, newStatus, 50),
                Param("@Action", SqlDbType.NVarChar, actionName, 80),
                Param("@UserId", SqlDbType.Int, userId),
                Param("@Role", SqlDbType.NVarChar, role, 50),
                Param("@DepartmentId", SqlDbType.Int, NullableInt(departmentId)),
                Param("@Remarks", SqlDbType.NVarChar, Text(remarks), 1000),
                Param("@Path", SqlDbType.NVarChar, relatedDocumentPath, 400));
        }

        public static string Html(object value)
        {
            return System.Web.HttpUtility.HtmlEncode(value == null || value == DBNull.Value ? String.Empty : Convert.ToString(value));
        }
    }
}
