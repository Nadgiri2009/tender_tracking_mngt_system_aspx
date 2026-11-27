using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;

namespace DepartmentModule.Helpers
{
    public sealed class WorkflowTransitionMetadata
    {
        public bool HoldStage { get; set; }
        public string NewStatusOverride { get; set; }
        public bool SetNextShortfallId { get; set; }
        public long? NextShortfallId { get; set; }
        public bool SetNextAssignedUserId { get; set; }
        public int? NextAssignedUserId { get; set; }
        public string ActionName { get; set; }
        public string RelatedDocumentPath { get; set; }
    }

    public sealed class ShortfallItemInput
    {
        public int? TechnicalBidDocumentId { get; set; }
        public string DocumentType { get; set; }
        public string Description { get; set; }
        public string CorrectiveAction { get; set; }
    }

    public sealed class FinancialBidInput
    {
        public int ContractorId { get; set; }
        public decimal Amount { get; set; }
        public string Remarks { get; set; }
    }

    public static class TenderFinalizedWorkflowService
    {
        public delegate WorkflowTransitionMetadata StageAction(
            SqlConnection connection, SqlTransaction transaction, int departmentId,
            string previousStatus, string newStatus);

        private static int GetShortfallResponseDurationDays()
        {
            string value = Environment.GetEnvironmentVariable("SMC_ShortfallResponseDurationDays");
            if (String.IsNullOrWhiteSpace(value))
                value = ConfigurationManager.AppSettings["ShortfallResponseDurationDays"];
            if (String.IsNullOrWhiteSpace(value)) return 3;

            int days;
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out days) ||
                days < 1 || days > 30)
                throw new ConfigurationErrorsException(
                    "ShortfallResponseDurationDays must be an integer from 1 through 30.");
            return days;
        }

        private sealed class StageAssignment
        {
            public long AssignmentId;
            public int StageId;
            public int StageOrder;
            public int DepartmentId;
            public int AssignedRoleId;
            public int? AssignedUserId;
            public string StageName;
            public string CurrentStatus;
            public string StatusOnPending;
            public string StatusOnApprove;
            public string StatusOnReject;
        }

        public static void Transition(int tenderId, int userId, int roleId, string roleCode,
            int? departmentId, string stageName, string newStatus, string remarks,
            long? shortfallId, string requiredPermission, string ipAddress, StageAction action)
        {
            string note = (remarks ?? String.Empty).Trim();
            if (tenderId <= 0 || userId <= 0 || roleId <= 0 ||
                String.IsNullOrWhiteSpace(stageName) || stageName.Length > 80 ||
                String.IsNullOrWhiteSpace(newStatus) || newStatus.Length > 50 || note.Length > 2000)
                throw new InvalidOperationException("The workflow action contains invalid values.");

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                StageAssignment assignment = LockAssignedStage(connection, transaction, tenderId,
                    userId, roleId, stageName, shortfallId, requiredPermission);
                if (!String.Equals(assignment.CurrentStatus, assignment.StatusOnPending, StringComparison.Ordinal))
                    throw new InvalidOperationException("Tender status and workflow stage are inconsistent. Refresh and retry.");

                WorkflowTransitionMetadata metadata = action == null
                    ? new WorkflowTransitionMetadata()
                    : action(connection, transaction, assignment.DepartmentId,
                        assignment.CurrentStatus, newStatus);
                if (metadata == null) metadata = new WorkflowTransitionMetadata();
                string targetStatus = String.IsNullOrWhiteSpace(metadata.NewStatusOverride)
                    ? newStatus : metadata.NewStatusOverride;
                bool approved = String.Equals(targetStatus, assignment.StatusOnApprove, StringComparison.Ordinal);
                bool rejected = String.Equals(targetStatus, assignment.StatusOnReject, StringComparison.Ordinal);
                bool expiredShortfall = assignment.StageName == "Final_Cell_ClearShortfall" &&
                    String.Equals(targetStatus, "Tender Cell Verification Pending", StringComparison.Ordinal);
                bool rejectedOpeningShortfall = assignment.StageName == "Final_Cell_ReverifyRejectedOpening" &&
                    String.Equals(targetStatus, "Shortfall Generation Pending", StringComparison.Ordinal);
                if (!approved && !rejected && !expiredShortfall && !rejectedOpeningShortfall &&
                    !String.Equals(targetStatus, assignment.CurrentStatus, StringComparison.Ordinal))
                    throw new InvalidOperationException("The requested status transition is not configured.");
                if (metadata.HoldStage && !String.Equals(targetStatus, assignment.CurrentStatus, StringComparison.Ordinal))
                    throw new InvalidOperationException("A workflow action cannot hold the stage while changing status.");

                if (!metadata.HoldStage)
                {
                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.Tender
                          SET CurrentStatus=@NewStatus,ModifiedBy=@UserId,ModifiedDate=SYSUTCDATETIME()
                          WHERE TenderId=@TenderId AND WorkflowVersion=2 AND IsDeleted=0
                            AND CurrentStatus=@PreviousStatus;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@NewStatus", SqlDbType.NVarChar, targetStatus, 50);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@PreviousStatus", SqlDbType.NVarChar,
                            assignment.CurrentStatus, 50);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Tender status changed; refresh and try again.");
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"UPDATE dbo.TenderWorkflowAssignments
                          SET IsCurrent=0,CompletedAtUtc=SYSUTCDATETIME()
                          WHERE AssignmentId=@AssignmentId AND IsCurrent=1;", connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@AssignmentId", SqlDbType.BigInt, assignment.AssignmentId);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The workflow assignment changed; refresh and retry.");
                    }

                    CreateNextAssignment(connection, transaction, tenderId, assignment,
                        targetStatus, roleCode, userId, metadata);
                }

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderWorkflowHistory
                        (TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,
                         UserId,RoleId,ActorRoleCode,DepartmentId,Remarks,RelatedDocumentPath)
                      VALUES(@TenderId,2,@StageId,@PreviousStatus,@NewStatus,@ActionName,
                             @UserId,@RoleId,@ActorRoleCode,@DepartmentId,@Remarks,@RelatedDocumentPath);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, assignment.StageId);
                    ModuleDb.AddParameter(command, "@PreviousStatus", SqlDbType.NVarChar,
                        assignment.CurrentStatus, 100);
                    ModuleDb.AddParameter(command, "@NewStatus", SqlDbType.NVarChar, targetStatus, 100);
                    ModuleDb.AddParameter(command, "@ActionName", SqlDbType.NVarChar,
                        String.IsNullOrWhiteSpace(metadata.ActionName) ? stageName : metadata.ActionName, 100);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                    ModuleDb.AddParameter(command, "@ActorRoleCode", SqlDbType.NVarChar, roleCode, 50);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, assignment.DepartmentId);
                    ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                        String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                    ModuleDb.AddParameter(command, "@RelatedDocumentPath", SqlDbType.NVarChar,
                        metadata.RelatedDocumentPath, 800);
                    command.ExecuteNonQuery();
                }

                ModuleDb.AddAudit(connection, transaction, userId, "WorkflowTransition", "Tender",
                    tenderId, assignment.CurrentStatus + " -> " + targetStatus + "; " + stageName,
                    ipAddress);
                transaction.Commit();
            }
        }

        public static void EnsureTenderRequirementDocumentsVerified(SqlConnection connection,
            SqlTransaction transaction, int tenderId, string reviewStage)
        {
            if (reviewStage != "JE" && reviewStage != "TENDER_CELL")
                throw new InvalidOperationException("The tender document review stage is invalid.");

            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TenderDocuments d
                  OUTER APPLY
                  (
                      SELECT TOP (1) h.VerificationStatus
                      FROM dbo.TenderRequirementVerificationHistory h
                      WHERE h.TenderId=d.TenderId AND h.DocumentId=d.DocumentId
                        AND h.ReviewStage=@ReviewStage
                        AND h.DocumentRevision=d.DocumentRevision
                      ORDER BY h.ReviewedAtUtc DESC,h.VerificationHistoryId DESC
                  ) latest
                  WHERE d.TenderId=@TenderId AND d.IsDeleted=0
                    AND ISNULL(COALESCE(latest.VerificationStatus,
                        CASE WHEN @ReviewStage=N'JE' AND d.VerifiedAtUtc IS NOT NULL
                             THEN d.VerificationStatus END),N'')<>N'Verified';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ReviewStage", SqlDbType.NVarChar, reviewStage, 20);
                int incomplete = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                if (incomplete > 0)
                    throw new InvalidOperationException(
                        "Record a Verified decision for every tender document at the " +
                        (reviewStage == "JE" ? "JE" : "Tender Cell") + " review stage before approval.");
            }

            using (SqlCommand command = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.TenderDocuments WHERE TenderId=@TenderId AND IsDeleted=0;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                    throw new InvalidOperationException("Upload the required tender documents before verification.");
            }
        }

        public static WorkflowTransitionMetadata CreateTenderDocumentShortfall(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int userId, int departmentId,
            string remarks, string requiredDocuments)
        {
            string note = (remarks ?? String.Empty).Trim();
            string documents = (requiredDocuments ?? String.Empty).Trim();
            if (note.Length == 0 || note.Length > 2000 ||
                documents.Length == 0 || documents.Length > 2000)
                throw new InvalidOperationException(
                    "Explain the document shortfall and list the required documents (up to 2,000 characters each).");

            long shortfallId;
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderDocumentShortfalls
                    (TenderId,RaisedBy,DeadlineAtUtc,Remarks,RequiredDocuments)
                  VALUES(@TenderId,@RaisedBy,DATEADD(DAY,3,SYSUTCDATETIME()),@Remarks,@RequiredDocuments);
                  SELECT CAST(SCOPE_IDENTITY() AS bigint);", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@RaisedBy", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar, note, 2000);
                ModuleDb.AddParameter(command, "@RequiredDocuments", SqlDbType.NVarChar, documents, 2000);
                shortfallId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            return new WorkflowTransitionMetadata
            {
                ActionName = "RaiseTenderDocumentShortfall",
                SetNextShortfallId = true,
                NextShortfallId = shortfallId
            };
        }

        public static WorkflowTransitionMetadata ResubmitTenderDocuments(SqlConnection connection,
            SqlTransaction transaction, int tenderId, long shortfallId, int userId)
        {
            DateTime raisedAtUtc;
            using (SqlCommand command = new SqlCommand(
                @"SELECT RaisedAtUtc
                  FROM dbo.TenderDocumentShortfalls WITH (UPDLOCK,HOLDLOCK)
                  WHERE TenderDocumentShortfallId=@ShortfallId AND TenderId=@TenderId
                    AND ResolutionStatus=N'Open' AND DeadlineAtUtc>=SYSUTCDATETIME();",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("The document shortfall is no longer open or its 3-day deadline has passed.");
                raisedAtUtc = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            }

            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1) FROM dbo.TenderDocuments
                  WHERE TenderId=@TenderId AND IsDeleted=0
                    AND UploadedBy=@UserId AND UploadedDate>=@RaisedAtUtc;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@RaisedAtUtc", SqlDbType.DateTime2, raisedAtUtc);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                    throw new InvalidOperationException("Upload at least one corrected tender document before resubmitting.");
            }

            using (SqlCommand command = new SqlCommand(
                @"UPDATE dbo.TenderDocumentShortfalls
                  SET ResolutionStatus=N'Resubmitted',ResubmittedBy=@UserId,
                      ResubmittedAtUtc=SYSUTCDATETIME()
                  WHERE TenderDocumentShortfallId=@ShortfallId AND TenderId=@TenderId
                    AND ResolutionStatus=N'Open' AND DeadlineAtUtc>=SYSUTCDATETIME();",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The shortfall changed while the documents were being resubmitted.");
            }

            return new WorkflowTransitionMetadata
            {
                ActionName = "ResubmitTenderDocuments",
                SetNextShortfallId = true,
                NextShortfallId = shortfallId
            };
        }

        public static void ResolveTenderDocumentShortfall(SqlConnection connection,
            SqlTransaction transaction, int tenderId, long shortfallId, int userId)
        {
            using (SqlCommand command = new SqlCommand(
                @"UPDATE dbo.TenderDocumentShortfalls
                  SET ResolutionStatus=N'Resolved',ResolvedBy=@UserId,ResolvedAtUtc=SYSUTCDATETIME()
                  WHERE TenderDocumentShortfallId=@ShortfallId AND TenderId=@TenderId
                    AND ResolutionStatus=N'Resubmitted';", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The resubmitted document shortfall could not be resolved.");
            }
        }

        public static WorkflowTransitionMetadata CreateStructuredShortfall(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int applicationId, int userId, int roleId,
            int departmentId, string remarks, IEnumerable<ShortfallItemInput> requestedItems,
            string ipAddress)
        {
            string note = (remarks ?? String.Empty).Trim();
            List<ShortfallItemInput> items = requestedItems == null
                ? new List<ShortfallItemInput>() : requestedItems.ToList();
            if (note.Length > 2000 || items.Count == 0 || items.Count > 20)
                throw new InvalidOperationException("Add one to twenty shortfall items and remarks up to 2,000 characters.");

            int contractorId, technicalBidId, contractorUserId;
            using (SqlCommand command = new SqlCommand(
                @"SELECT b.TechnicalBidId,b.ContractorId,c.UserId
                  FROM dbo.TechnicalBid b WITH (UPDLOCK,HOLDLOCK)
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId AND c.IsActive=1
                  INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                  WHERE b.TechnicalBidId=@ApplicationId AND b.TenderId=@TenderId AND b.Status=N'Submitted';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Select a submitted application for this tender.");
                    technicalBidId = Convert.ToInt32(reader["TechnicalBidId"], CultureInfo.InvariantCulture);
                    contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                    contractorUserId = Convert.ToInt32(reader["UserId"], CultureInfo.InvariantCulture);
                }
            }

            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TenderVerificationReviews r
                  WHERE r.TenderId=@TenderId AND r.ContractorId=@ContractorId
                    AND r.ReviewStage=N'Secondary' AND r.Decision=N'Shortfall'
                    AND r.VerificationReviewId=
                    (
                        SELECT TOP (1) latest.VerificationReviewId
                        FROM dbo.TenderVerificationReviews latest
                        WHERE latest.TenderId=r.TenderId AND latest.ContractorId=r.ContractorId
                          AND latest.ReviewStage=N'Secondary'
                        ORDER BY latest.ReviewedAtUtc DESC,latest.VerificationReviewId DESC
                    );", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                    throw new InvalidOperationException("A shortfall can be created only after the Tender Cell records a secondary review requiring correction.");
            }

            HashSet<string> allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand command = new SqlCommand(
                @"SELECT DocumentType FROM dbo.TenderRequiredBidDocuments WHERE TenderId=@TenderId;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                    while (reader.Read()) allowedTypes.Add(Convert.ToString(reader["DocumentType"]));
            }

            if (items.Any(item => item == null ||
                String.IsNullOrWhiteSpace(item.DocumentType) || item.DocumentType.Trim().Length > 160 ||
                !allowedTypes.Contains(item.DocumentType.Trim()) ||
                String.IsNullOrWhiteSpace(item.Description) || item.Description.Trim().Length > 2000 ||
                String.IsNullOrWhiteSpace(item.CorrectiveAction) || item.CorrectiveAction.Trim().Length > 2000))
                throw new InvalidOperationException("Each shortfall item must use a configured document requirement and include a description and corrective action.");

            long shortfallId;
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderShortfalls
                    (TenderId,ContractorId,SubmissionDurationDays,SubmissionDeadlineUtc,Status,CreatedBy,
                     ClearanceStatus,Remarks)
                  VALUES(@TenderId,@ContractorId,3,DATEADD(DAY,3,SYSUTCDATETIME()),
                         N'Pending JE Approval',@UserId,N'Pending',@Remarks);
                  SELECT CAST(SCOPE_IDENTITY() AS bigint);", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                shortfallId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            foreach (ShortfallItemInput item in items)
            {
                int? documentId = item.TechnicalBidDocumentId;
                if (documentId.HasValue)
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT COUNT(1)
                          FROM dbo.TechnicalBidDocuments d
                          INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                          OUTER APPLY
                          (
                              SELECT TOP (1) h.VerificationStatus
                              FROM dbo.TenderDocumentVerificationHistory h
                              WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                                AND h.ReviewStage=N'Secondary'
                              ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                          ) latest
                          WHERE d.TechnicalBidDocumentId=@DocumentId AND b.TechnicalBidId=@ApplicationId
                            AND d.DocumentType=@DocumentType
                            AND ISNULL(latest.VerificationStatus,N'Not Verified')<>N'Verified';",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId.Value);
                        ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, technicalBidId);
                        ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar,
                            item.DocumentType.Trim(), 160);
                        if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                            throw new InvalidOperationException("A selected document is not part of this application or is already verified.");
                    }
                }
                else
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT COUNT(1)
                          FROM dbo.TenderRequiredBidDocuments r
                          WHERE r.TenderId=@TenderId AND r.DocumentType=@DocumentType AND r.IsMandatory=1
                            AND NOT EXISTS
                              (SELECT 1 FROM dbo.TechnicalBidDocuments d
                               WHERE d.TechnicalBidId=@ApplicationId AND d.DocumentType=r.DocumentType);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar,
                            item.DocumentType.Trim(), 160);
                        ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, technicalBidId);
                        if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                            throw new InvalidOperationException("A missing-document item must be a mandatory requirement not uploaded by this application.");
                    }
                }

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderShortfallItems
                        (ShortfallId,TechnicalBidDocumentId,RequiredDocumentType,ShortfallDescription,CorrectiveAction)
                      VALUES(@ShortfallId,@DocumentId,@DocumentType,@Description,@CorrectiveAction);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int,
                        documentId.HasValue ? (object)documentId.Value : null);
                    ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar,
                        item.DocumentType.Trim(), 160);
                    ModuleDb.AddParameter(command, "@Description", SqlDbType.NVarChar,
                        item.Description.Trim(), 2000);
                    ModuleDb.AddParameter(command, "@CorrectiveAction", SqlDbType.NVarChar,
                        item.CorrectiveAction.Trim(), 2000);
                    command.ExecuteNonQuery();
                }
            }

            ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                "StructuredShortfallCreated", null, null,
                String.IsNullOrWhiteSpace(note) ? "A structured shortfall was submitted for approval." : note,
                userId, roleId, true);
            NotifyRole(connection, transaction, tenderId, shortfallId, departmentId, "JE",
                "A shortfall request is awaiting Junior Engineer approval.");
            ModuleDb.AddAudit(connection, transaction, userId, "CreateTenderShortfall",
                "TenderShortfall", tenderId, "Shortfall " + shortfallId.ToString(CultureInfo.InvariantCulture),
                ipAddress);

            return new WorkflowTransitionMetadata
            {
                SetNextShortfallId = true,
                NextShortfallId = shortfallId,
                ActionName = "CreateStructuredShortfall"
            };
        }

        public static WorkflowTransitionMetadata DecideStructuredShortfall(SqlConnection connection,
            SqlTransaction transaction, int tenderId, long shortfallId, int userId, int departmentId,
            bool approve, string remarks)
        {
            string note = (remarks ?? String.Empty).Trim();
            if (note.Length > 2000 || (!approve && note.Length == 0))
                throw new InvalidOperationException("Provide remarks of at most 2,000 characters; remarks are required when returning a shortfall.");
            if (approve)
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT CASE WHEN COL_LENGTH(N'dbo.TenderNotifications',N'AvailableAtUtc') IS NULL
                                  THEN 0 ELSE 1 END;", connection, transaction))
                {
                    if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                        throw new InvalidOperationException("Apply the scheduled-notification database upgrade before approving a shortfall.");
                }
            }

            int contractorUserId, applicationId, contractorId;
            using (SqlCommand command = new SqlCommand(
                @"SELECT c.UserId,c.ContractorId,b.TechnicalBidId
                  FROM dbo.TenderShortfalls s WITH (UPDLOCK,HOLDLOCK)
                  INNER JOIN dbo.Contractors c ON c.ContractorId=s.ContractorId
                  INNER JOIN dbo.TechnicalBid b ON b.TenderId=s.TenderId AND b.ContractorId=s.ContractorId
                  WHERE s.ShortfallId=@ShortfallId AND s.TenderId=@TenderId
                    AND s.Status=N'Pending JE Approval';", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The shortfall is no longer awaiting approval.");
                    contractorUserId = Convert.ToInt32(reader["UserId"], CultureInfo.InvariantCulture);
                    contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                    applicationId = Convert.ToInt32(reader["TechnicalBidId"], CultureInfo.InvariantCulture);
                }
            }

            using (SqlCommand command = new SqlCommand(
                @"DECLARE @DecisionAtUtc DATETIME2(0)=SYSUTCDATETIME();
                  UPDATE dbo.TenderShortfalls
                  SET Status=@Status,JeApprovedBy=@UserId,JeApprovedAtUtc=@DecisionAtUtc,
                      SubmissionDurationDays=CASE WHEN @Approve=1 THEN @DurationDays ELSE SubmissionDurationDays END,
                      SubmissionDeadlineUtc=CASE WHEN @Approve=1
                          THEN DATEADD(DAY,@DurationDays,@DecisionAtUtc) ELSE SubmissionDeadlineUtc END,
                      ClearanceStatus=CASE WHEN @Approve=1 THEN N'Pending' ELSE N'Continued Deficiency' END,
                      Remarks=CASE WHEN @Remarks IS NULL THEN Remarks ELSE @Remarks END
                  WHERE ShortfallId=@ShortfallId AND TenderId=@TenderId AND Status=N'Pending JE Approval';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@Status", SqlDbType.NVarChar,
                    approve ? "Awaiting Contractor" : "Rejected", 40);
                ModuleDb.AddParameter(command, "@Approve", SqlDbType.Bit, approve);
                ModuleDb.AddParameter(command, "@DurationDays", SqlDbType.Int, GetShortfallResponseDurationDays());
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The shortfall approval changed; refresh and retry.");
            }
            if (approve)
            {
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderNotifications
                        (UserId,TenderId,ShortfallId,NotificationType,Message,AvailableAtUtc)
                      SELECT @UserId,@TenderId,@ShortfallId,N'ShortfallReminder',
                             N'This is your reminder: the shortfall response deadline is 24 hours away.',
                             DATEADD(HOUR,-24,s.SubmissionDeadlineUtc)
                      FROM dbo.TenderShortfalls s
                      WHERE s.ShortfallId=@ShortfallId AND s.TenderId=@TenderId
                        AND s.Status=N'Awaiting Contractor'
                        AND NOT EXISTS
                          (SELECT 1 FROM dbo.TenderNotifications n
                           WHERE n.ShortfallId=s.ShortfallId
                             AND n.NotificationType=N'ShortfallReminder');", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, contractorUserId);
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("The contractor deadline reminder could not be scheduled.");
                }
            }

            ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                approve ? "StructuredShortfallApproved" : "StructuredShortfallReturned",
                null, null, String.IsNullOrWhiteSpace(note) ? null : note, userId, 0, true);
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,ShortfallId,NotificationType,Message)
                  VALUES(@UserId,@TenderId,@ShortfallId,N'ShortfallDecision',@Message);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, contractorUserId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar,
                    approve
                        ? "The shortfall is approved. Submit each corrected document within three calendar days."
                        : "The shortfall request was returned to the Tender Cell for revision.", 1000);
                command.ExecuteNonQuery();
            }
            ModuleDb.AddAudit(connection, transaction, userId, "DecideTenderShortfall",
                "TenderShortfall", tenderId, (approve ? "Approved " : "Returned ") +
                shortfallId.ToString(CultureInfo.InvariantCulture) + "; contractor " +
                contractorId.ToString(CultureInfo.InvariantCulture) + "; department " +
                departmentId.ToString(CultureInfo.InvariantCulture), null);

            return new WorkflowTransitionMetadata
            {
                SetNextShortfallId = true,
                NextShortfallId = approve ? shortfallId : (long?)null,
                SetNextAssignedUserId = true,
                NextAssignedUserId = approve ? contractorUserId : (int?)null,
                ActionName = approve ? "ApproveStructuredShortfall" : "ReturnStructuredShortfall"
            };
        }

        private static void NotifyRole(SqlConnection connection, SqlTransaction transaction,
            int tenderId, long? shortfallId, int departmentId, string roleCode, string message)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,ShortfallId,NotificationType,Message)
                  SELECT u.UserId,@TenderId,@ShortfallId,N'ShortfallWorkflow',@Message
                  FROM dbo.Users u
                  INNER JOIN dbo.Roles r ON r.RoleId=u.RoleId
                  WHERE u.IsActive=1 AND u.AccountType=N'Internal' AND r.RoleCode=@RoleCode
                    AND (u.DepartmentId IS NULL OR u.DepartmentId=@DepartmentId);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt,
                    shortfallId.HasValue ? (object)shortfallId.Value : null);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                ModuleDb.AddParameter(command, "@RoleCode", SqlDbType.NVarChar, roleCode, 50);
                ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar, message, 1000);
                command.ExecuteNonQuery();
            }
        }

        private static void NotifyWorkflowRole(SqlConnection connection, SqlTransaction transaction,
            int tenderId, int assignedRoleId, int departmentId, string stageName)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,NotificationType,Message)
                  SELECT u.UserId,@TenderId,N'WorkflowAssignment',
                         N'A tender is awaiting your action: ' + @StageName + N'.'
                  FROM dbo.Users u
                  WHERE u.IsActive=1 AND u.AccountType=N'Internal' AND u.RoleId=@RoleId
                    AND (u.DepartmentId IS NULL OR u.DepartmentId=@DepartmentId);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, assignedRoleId);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                ModuleDb.AddParameter(command, "@StageName", SqlDbType.NVarChar, stageName, 80);
                command.ExecuteNonQuery();
            }
        }

        public static void RecordSecondaryDocumentDecision(int applicationId, int? documentId,
            string documentType, bool isMissing, int userId, int roleId, string roleCode,
            int? departmentId, string decision, string remarks, string ipAddress)
        {
            string normalizedDecision = (decision ?? String.Empty).Trim();
            string note = (remarks ?? String.Empty).Trim();
            if (!IsTenderCell(roleCode) ||
                (normalizedDecision != "Verified" && normalizedDecision != "Not Verified") ||
                note.Length > 2000 || (normalizedDecision == "Not Verified" && note.Length == 0) ||
                isMissing != !documentId.HasValue)
                throw new InvalidOperationException("Choose a valid document decision and provide remarks for any item that is not verified.");

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                int tenderId, contractorId;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT b.TenderId,b.ContractorId
                      FROM dbo.TechnicalBid b WITH (UPDLOCK,HOLDLOCK)
                      WHERE b.TechnicalBidId=@ApplicationId AND b.Status=N'Submitted';",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("The selected submitted application is no longer available.");
                        tenderId = Convert.ToInt32(reader["TenderId"], CultureInfo.InvariantCulture);
                        contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                    }
                }

                LockAssignedStage(connection, transaction, tenderId, userId, roleId,
                    "Final_Cell_CompleteSecondaryVerification", null, "tender.cell.verify");

                string fileName = "(not submitted)";
                string filePath = String.Empty;
                if (documentId.HasValue)
                {
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT FileName,FilePath,DocumentType
                          FROM dbo.TechnicalBidDocuments
                          WHERE TechnicalBidDocumentId=@DocumentId AND TechnicalBidId=@ApplicationId;",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId.Value);
                        ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read() ||
                                !String.Equals(Convert.ToString(reader["DocumentType"]), documentType,
                                    StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("The document does not belong to this submitted application.");
                            fileName = Convert.ToString(reader["FileName"]);
                            filePath = Convert.ToString(reader["FilePath"]);
                        }
                    }
                }
                else
                {
                    if (normalizedDecision != "Not Verified")
                        throw new InvalidOperationException("A missing required document cannot be marked verified.");
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT COUNT(1) FROM dbo.TenderRequiredBidDocuments r
                          WHERE r.TenderId=@TenderId AND r.DocumentType=@DocumentType AND r.IsMandatory=1
                            AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                              WHERE d.TechnicalBidId=@ApplicationId AND d.DocumentType=r.DocumentType);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                        ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar, documentType, 160);
                        ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                        if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                            throw new InvalidOperationException("Only a missing mandatory requirement can be reviewed as not verified.");
                    }
                }

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderDocumentVerificationHistory
                        (TenderId,ContractorId,TechnicalBidDocumentId,FileName,FilePath,DocumentType,
                         ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks)
                      VALUES(@TenderId,@ContractorId,@DocumentId,@FileName,@FilePath,@DocumentType,
                             N'Secondary',@Decision,@UserId,@RoleId,@Remarks);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int,
                        documentId.HasValue ? (object)documentId.Value : null);
                    ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 520);
                    ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, filePath, 800);
                    ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar, documentType, 160);
                    ModuleDb.AddParameter(command, "@Decision", SqlDbType.NVarChar, normalizedDecision, 20);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                    ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                        String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                    command.ExecuteNonQuery();
                }

                ModuleDb.AddAudit(connection, transaction, userId, "SecondaryDocumentVerification",
                    "TechnicalBidDocument", documentId, documentType + ": " + normalizedDecision,
                    ipAddress);
                transaction.Commit();
            }
        }

        public static void RecordSecondaryApplicationDecision(int applicationId, int userId, int roleId,
            string roleCode, int? departmentId, string decision, string remarks, string ipAddress)
        {
            string normalizedDecision = (decision ?? String.Empty).Trim();
            string note = (remarks ?? String.Empty).Trim();
            if (!IsTenderCell(roleCode) || (normalizedDecision != "Approved" && normalizedDecision != "Shortfall") ||
                note.Length > 2000 || (normalizedDecision == "Shortfall" && note.Length == 0))
                throw new InvalidOperationException("Choose a valid secondary decision and include remarks when a shortfall is required.");

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                int tenderId, contractorId;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT TenderId,ContractorId FROM dbo.TechnicalBid WITH (UPDLOCK,HOLDLOCK)
                      WHERE TechnicalBidId=@ApplicationId AND Status=N'Submitted';", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("The selected submitted application is no longer available.");
                        tenderId = Convert.ToInt32(reader["TenderId"], CultureInfo.InvariantCulture);
                        contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                    }
                }

                LockAssignedStage(connection, transaction, tenderId, userId, roleId,
                    "Final_Cell_CompleteSecondaryVerification", null, "tender.cell.verify");

                if (HasUnreviewedSecondaryDocuments(connection, transaction, tenderId, applicationId))
                    throw new InvalidOperationException("Record a secondary decision for every submitted document and missing mandatory requirement first.");
                bool hasDeficiency = HasSecondaryDeficiency(connection, transaction, tenderId, applicationId);
                if ((normalizedDecision == "Approved" && hasDeficiency) ||
                    (normalizedDecision == "Shortfall" && !hasDeficiency))
                    throw new InvalidOperationException("The bidder decision must match the recorded secondary document decisions.");

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderVerificationReviews
                        (TenderId,ContractorId,ReviewStage,Decision,ReviewerId,ReviewerRoleId,Remarks)
                      VALUES(@TenderId,@ContractorId,N'Secondary',@Decision,@UserId,@RoleId,@Remarks);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                    ModuleDb.AddParameter(command, "@Decision", SqlDbType.NVarChar, normalizedDecision, 30);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                    ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                        String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                    command.ExecuteNonQuery();
                }
                ModuleDb.AddAudit(connection, transaction, userId, "SecondaryApplicationReview",
                    "TechnicalBid", applicationId, normalizedDecision, ipAddress);
                transaction.Commit();
            }
        }

        public static void EnsureSecondaryVerificationComplete(SqlConnection connection,
            SqlTransaction transaction, int tenderId, string targetStatus)
        {
            int bidderCount;
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1) FROM dbo.TechnicalBid
                  WHERE TenderId=@TenderId AND Status=N'Submitted';", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                bidderCount = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            if (bidderCount == 0)
                throw new InvalidOperationException("At least one submitted application is required before secondary verification can be completed.");

            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TechnicalBid b
                  OUTER APPLY
                  (
                      SELECT TOP (1) r.Decision
                      FROM dbo.TenderVerificationReviews r
                      WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                        AND r.ReviewStage=N'Secondary'
                      ORDER BY r.ReviewedAtUtc DESC,r.VerificationReviewId DESC
                  ) latest
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                    AND latest.Decision IS NULL;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
                    throw new InvalidOperationException("Record a secondary bidder decision for every submitted application.");
            }

            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TechnicalBid b
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                    AND
                    (
                        EXISTS
                        (
                            SELECT 1 FROM dbo.TechnicalBidDocuments d
                            OUTER APPLY
                            (
                                SELECT TOP (1) h.VerificationStatus
                                FROM dbo.TenderDocumentVerificationHistory h
                                WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                                  AND h.ReviewStage=N'Secondary'
                                ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                            ) latest
                            WHERE d.TechnicalBidId=b.TechnicalBidId AND latest.VerificationStatus IS NULL
                        )
                        OR EXISTS
                        (
                            SELECT 1 FROM dbo.TenderRequiredBidDocuments r
                            OUTER APPLY
                            (
                                SELECT TOP (1) h.VerificationStatus
                                FROM dbo.TenderDocumentVerificationHistory h
                                WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                                  AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                                  AND h.ReviewStage=N'Secondary'
                                ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                            ) latest
                            WHERE r.TenderId=b.TenderId AND r.IsMandatory=1
                              AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                             WHERE d.TechnicalBidId=b.TechnicalBidId
                                               AND d.DocumentType=r.DocumentType)
                              AND latest.VerificationStatus IS NULL
                        )
                    );", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
                    throw new InvalidOperationException("Record secondary decisions for every submitted document and missing mandatory requirement.");
            }

            bool hasDeficiency;
            using (SqlCommand command = new SqlCommand(
                @"SELECT CASE WHEN EXISTS
                  (
                      SELECT 1 FROM dbo.TechnicalBid b
                      WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                        AND
                        (
                            EXISTS
                            (
                                SELECT 1 FROM dbo.TechnicalBidDocuments d
                                OUTER APPLY
                                (
                                    SELECT TOP (1) h.VerificationStatus
                                    FROM dbo.TenderDocumentVerificationHistory h
                                    WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                                      AND h.ReviewStage=N'Secondary'
                                    ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                                ) latest
                                WHERE d.TechnicalBidId=b.TechnicalBidId
                                  AND latest.VerificationStatus=N'Not Verified'
                            )
                            OR EXISTS
                            (
                                SELECT 1 FROM dbo.TenderRequiredBidDocuments r
                                OUTER APPLY
                                (
                                    SELECT TOP (1) h.VerificationStatus
                                    FROM dbo.TenderDocumentVerificationHistory h
                                    WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                                      AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                                      AND h.ReviewStage=N'Secondary'
                                    ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                                ) latest
                                WHERE r.TenderId=b.TenderId AND r.IsMandatory=1
                                  AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                                 WHERE d.TechnicalBidId=b.TechnicalBidId
                                                   AND d.DocumentType=r.DocumentType)
                                  AND latest.VerificationStatus=N'Not Verified'
                            )
                        )
                  ) THEN 1 ELSE 0 END;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                hasDeficiency = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }

            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TechnicalBid b
                  OUTER APPLY
                  (
                      SELECT TOP (1) r.Decision
                      FROM dbo.TenderVerificationReviews r
                      WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                        AND r.ReviewStage=N'Secondary'
                      ORDER BY r.ReviewedAtUtc DESC,r.VerificationReviewId DESC
                  ) latest
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted'
                    AND
                    (
                        (latest.Decision=N'Shortfall' AND NOT
                            (EXISTS
                                (SELECT 1 FROM dbo.TechnicalBidDocuments d
                                 OUTER APPLY
                                 (
                                     SELECT TOP (1) h.VerificationStatus
                                     FROM dbo.TenderDocumentVerificationHistory h
                                     WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                                       AND h.ReviewStage=N'Secondary'
                                     ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                                 ) documentReview
                                 WHERE d.TechnicalBidId=b.TechnicalBidId
                                   AND documentReview.VerificationStatus=N'Not Verified')
                             OR EXISTS
                                (SELECT 1 FROM dbo.TenderRequiredBidDocuments r
                                 OUTER APPLY
                                 (
                                     SELECT TOP (1) h.VerificationStatus
                                     FROM dbo.TenderDocumentVerificationHistory h
                                     WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                                       AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                                       AND h.ReviewStage=N'Secondary'
                                     ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                                 ) documentReview
                                 WHERE r.TenderId=b.TenderId AND r.IsMandatory=1
                                   AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                                  WHERE d.TechnicalBidId=b.TechnicalBidId
                                                    AND d.DocumentType=r.DocumentType)
                                   AND documentReview.VerificationStatus=N'Not Verified'))
                        )
                        OR
                        (latest.Decision=N'Approved' AND
                            (EXISTS
                                (SELECT 1 FROM dbo.TechnicalBidDocuments d
                                 OUTER APPLY
                                 (
                                     SELECT TOP (1) h.VerificationStatus
                                     FROM dbo.TenderDocumentVerificationHistory h
                                     WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                                       AND h.ReviewStage=N'Secondary'
                                     ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                                 ) documentReview
                                 WHERE d.TechnicalBidId=b.TechnicalBidId
                                   AND documentReview.VerificationStatus=N'Not Verified')
                             OR EXISTS
                                (SELECT 1 FROM dbo.TenderRequiredBidDocuments r
                                 OUTER APPLY
                                 (
                                     SELECT TOP (1) h.VerificationStatus
                                     FROM dbo.TenderDocumentVerificationHistory h
                                     WHERE h.TenderId=b.TenderId AND h.ContractorId=b.ContractorId
                                       AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                                       AND h.ReviewStage=N'Secondary'
                                     ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                                 ) documentReview
                                 WHERE r.TenderId=b.TenderId AND r.IsMandatory=1
                                   AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                                  WHERE d.TechnicalBidId=b.TechnicalBidId
                                                    AND d.DocumentType=r.DocumentType)
                                   AND documentReview.VerificationStatus=N'Not Verified'))
                        )
                    );", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
                    throw new InvalidOperationException(
                        "Update the bidder decision after changing any secondary document decisions.");
            }

            string expectedStatus = hasDeficiency
                ? "Shortfall Generation Pending" : "Financial Bid Opening Approval Pending";
            if (!String.Equals(targetStatus, expectedStatus, StringComparison.Ordinal))
                throw new InvalidOperationException(hasDeficiency
                    ? "Create and obtain approval for the required shortfall before opening financial bids."
                    : "Financial opening can proceed only when all submitted bidder documents are verified.");
        }

        private static bool HasUnreviewedSecondaryDocuments(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int applicationId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT CASE WHEN EXISTS
                  (
                      SELECT 1 FROM dbo.TechnicalBidDocuments d
                      OUTER APPLY
                      (
                          SELECT TOP (1) h.VerificationStatus
                          FROM dbo.TenderDocumentVerificationHistory h
                          WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                            AND h.ReviewStage=N'Secondary'
                          ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                      ) latest
                      WHERE d.TechnicalBidId=@ApplicationId AND latest.VerificationStatus IS NULL
                  ) OR EXISTS
                  (
                      SELECT 1 FROM dbo.TenderRequiredBidDocuments r
                      OUTER APPLY
                      (
                          SELECT TOP (1) h.VerificationStatus
                          FROM dbo.TenderDocumentVerificationHistory h
                          WHERE h.TenderId=@TenderId AND h.ContractorId=
                              (SELECT ContractorId FROM dbo.TechnicalBid WHERE TechnicalBidId=@ApplicationId)
                            AND h.TechnicalBidDocumentId IS NULL
                            AND h.DocumentType=r.DocumentType AND h.ReviewStage=N'Secondary'
                          ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                      ) latest
                      WHERE r.TenderId=@TenderId AND r.IsMandatory=1
                        AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                       WHERE d.TechnicalBidId=@ApplicationId AND d.DocumentType=r.DocumentType)
                        AND latest.VerificationStatus IS NULL
                  ) THEN 1 ELSE 0 END;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        private static bool HasSecondaryDeficiency(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int applicationId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT CASE WHEN EXISTS
                  (
                      SELECT 1 FROM dbo.TechnicalBidDocuments d
                      OUTER APPLY
                      (
                          SELECT TOP (1) h.VerificationStatus
                          FROM dbo.TenderDocumentVerificationHistory h
                          WHERE h.TechnicalBidDocumentId=d.TechnicalBidDocumentId
                            AND h.ReviewStage=N'Secondary'
                          ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                      ) latest
                      WHERE d.TechnicalBidId=@ApplicationId AND latest.VerificationStatus=N'Not Verified'
                  ) OR EXISTS
                  (
                      SELECT 1 FROM dbo.TenderRequiredBidDocuments r
                      OUTER APPLY
                      (
                          SELECT TOP (1) h.VerificationStatus
                          FROM dbo.TenderDocumentVerificationHistory h
                          WHERE h.TenderId=@TenderId AND h.ContractorId=
                              (SELECT ContractorId FROM dbo.TechnicalBid WHERE TechnicalBidId=@ApplicationId)
                            AND h.TechnicalBidDocumentId IS NULL AND h.DocumentType=r.DocumentType
                            AND h.ReviewStage=N'Secondary'
                          ORDER BY h.ReviewedAtUtc DESC,h.DocumentVerificationHistoryId DESC
                      ) latest
                      WHERE r.TenderId=@TenderId AND r.IsMandatory=1
                        AND NOT EXISTS(SELECT 1 FROM dbo.TechnicalBidDocuments d
                                       WHERE d.TechnicalBidId=@ApplicationId AND d.DocumentType=r.DocumentType)
                        AND latest.VerificationStatus=N'Not Verified'
                  ) THEN 1 ELSE 0 END;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        public static void EnterFinancialBids(SqlConnection connection, SqlTransaction transaction,
            int tenderId, int userId, IEnumerable<FinancialBidInput> requestedBids, string remarks)
        {
            List<FinancialBidInput> bids = requestedBids == null
                ? new List<FinancialBidInput>() : requestedBids.ToList();
            string note = (remarks ?? String.Empty).Trim();
            if (bids.Count == 0 || bids.Count > 500 || note.Length > 2000 ||
                bids.Any(bid => bid == null || bid.ContractorId <= 0 ||
                    bid.Amount <= 0 || bid.Amount > 9999999999999999.99m ||
                    (bid.Remarks ?? String.Empty).Trim().Length > 1000) ||
                bids.Select(bid => bid.ContractorId).Distinct().Count() != bids.Count)
                throw new InvalidOperationException("Enter one positive financial amount per qualified bidder, with remarks up to 1,000 characters.");

            int qualifiedCount;
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TechnicalBid b
                  OUTER APPLY
                  (
                      SELECT TOP (1) r.Decision
                      FROM dbo.TenderVerificationReviews r
                      WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                        AND r.ReviewStage=N'Secondary'
                      ORDER BY r.ReviewedAtUtc DESC,r.VerificationReviewId DESC
                  ) latest
                  WHERE b.TenderId=@TenderId AND b.Status=N'Submitted' AND latest.Decision=N'Approved';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                qualifiedCount = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            if (qualifiedCount == 0 || qualifiedCount != bids.Count)
                throw new InvalidOperationException("Financial values must be entered for every and only every qualified bidder.");

            using (SqlCommand command = new SqlCommand(
                @"IF EXISTS(SELECT 1 FROM dbo.TenderFinancialBidOpening WITH (UPDLOCK,HOLDLOCK)
                            WHERE TenderId=@TenderId)
                      THROW 51130,'Financial bids have already been opened.',1;
                  IF EXISTS(SELECT 1 FROM dbo.FinancialBid WITH (UPDLOCK,HOLDLOCK)
                            WHERE TenderId=@TenderId)
                      THROW 51131,'Financial bid rows already exist for this tender.',1;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                command.ExecuteNonQuery();
            }

            long openingId;
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderFinancialBidOpening
                    (TenderId,ActualOpeningUtc,OpenedBy,OpeningRemarks)
                  VALUES(@TenderId,SYSUTCDATETIME(),@UserId,@Remarks);
                  SELECT CAST(SCOPE_IDENTITY() AS bigint);", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                openingId = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            foreach (FinancialBidInput bid in bids)
            {
                int financialBidId;
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.FinancialBid(TenderId,ContractorId,BidAmount,Status)
                      SELECT @TenderId,b.ContractorId,@Amount,N'Submitted'
                      FROM dbo.TechnicalBid b
                      OUTER APPLY
                      (
                          SELECT TOP (1) r.Decision
                          FROM dbo.TenderVerificationReviews r
                          WHERE r.TenderId=b.TenderId AND r.ContractorId=b.ContractorId
                            AND r.ReviewStage=N'Secondary'
                          ORDER BY r.ReviewedAtUtc DESC,r.VerificationReviewId DESC
                      ) latest
                      WHERE b.TenderId=@TenderId AND b.ContractorId=@ContractorId
                        AND b.Status=N'Submitted' AND latest.Decision=N'Approved';
                      IF @@ROWCOUNT<>1 THROW 51132,'Qualified bidder changed during financial opening.',1;
                      SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, bid.ContractorId);
                    ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, bid.Amount);
                    financialBidId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                }

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderFinancialBidOpeningDetails
                        (FinancialBidOpeningId,FinancialBidId,QuotedAmount,Remarks)
                      VALUES(@OpeningId,@FinancialBidId,@Amount,@Remarks);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@OpeningId", SqlDbType.BigInt, openingId);
                    ModuleDb.AddParameter(command, "@FinancialBidId", SqlDbType.Int, financialBidId);
                    ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, bid.Amount);
                    ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                        String.IsNullOrWhiteSpace(bid.Remarks) ? null : bid.Remarks.Trim(), 1000);
                    command.ExecuteNonQuery();
                }
            }
        }

        public static void RecordAuditorFinancialDecision(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int userId, string decision,
            int? selectedFinancialBidId, string remarks)
        {
            string normalized = (decision ?? String.Empty).Trim();
            string note = (remarks ?? String.Empty).Trim();
            if ((normalized != "Approved" && normalized != "Returned") ||
                note.Length > 2000 || (normalized == "Returned" && note.Length == 0) ||
                (normalized == "Approved" && !selectedFinancialBidId.HasValue))
                throw new InvalidOperationException("Select an approved bidder and amount, or provide a reason to return the financial review.");

            decimal? approvedAmount = null;
            if (normalized == "Approved")
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT d.QuotedAmount
                      FROM dbo.TenderFinancialBidOpeningDetails d
                      INNER JOIN dbo.TenderFinancialBidOpening o ON o.FinancialBidOpeningId=d.FinancialBidOpeningId
                      INNER JOIN dbo.FinancialBid b ON b.FinancialBidId=d.FinancialBidId
                      WHERE o.TenderId=@TenderId AND b.TenderId=@TenderId
                        AND b.FinancialBidId=@FinancialBidId;", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@FinancialBidId", SqlDbType.Int, selectedFinancialBidId.Value);
                    object value = command.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        throw new InvalidOperationException("The selected financial bid was not opened for this tender.");
                    approvedAmount = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                }
            }

            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderFinancialApprovals
                    (TenderId,Decision,ApprovedAmount,SelectedFinancialBidId,ApprovedBy,Remarks)
                  VALUES(@TenderId,@Decision,@Amount,@FinancialBidId,@UserId,@Remarks);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@Decision", SqlDbType.NVarChar, normalized, 20);
                ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal,
                    approvedAmount.HasValue ? (object)approvedAmount.Value : null);
                ModuleDb.AddParameter(command, "@FinancialBidId", SqlDbType.Int,
                    selectedFinancialBidId.HasValue ? (object)selectedFinancialBidId.Value : null);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                command.ExecuteNonQuery();
            }
        }

        public static WorkflowTransitionMetadata RecordFinancialOpeningDecision(
            SqlConnection connection, SqlTransaction transaction, int tenderId, int departmentId,
            bool approved, string remarks)
        {
            string note = (remarks ?? String.Empty).Trim();
            if (note.Length > 2000 || (!approved && note.Length == 0))
                throw new InvalidOperationException("Provide remarks of at most 2,000 characters; a rejection reason is required.");
            NotifyRole(connection, transaction, tenderId, null, departmentId, "JE",
                approved
                    ? "The Chairman approved financial bid opening. Enter bidder-level financial data."
                    : "The Chairman returned the financial opening for Tender Cell re-verification.");
            NotifyRole(connection, transaction, tenderId, null, departmentId, "TENDER_CELL",
                approved
                    ? "The Chairman approved financial bid opening."
                    : "The Chairman returned the financial opening for Tender Cell re-verification.");
            return new WorkflowTransitionMetadata
            {
                ActionName = approved ? "ApproveFinancialOpening" : "RejectFinancialOpening"
            };
        }

        public static WorkflowTransitionMetadata ResubmitFinancialReview(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int userId, string remarks)
        {
            string note = (remarks ?? String.Empty).Trim();
            if (note.Length > 2000 || note.Length == 0)
                throw new InvalidOperationException("Explain the Junior Engineer resubmission in 2,000 characters or fewer.");
            using (SqlCommand command = new SqlCommand(
                @"SELECT TOP (1) Decision
                  FROM dbo.TenderFinancialApprovals
                  WHERE TenderId=@TenderId ORDER BY ApprovedAtUtc DESC,FinancialApprovalId DESC;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                if (!String.Equals(Convert.ToString(command.ExecuteScalar()), "Returned",
                    StringComparison.Ordinal))
                    throw new InvalidOperationException("A returned Director Auditor review is required before resubmission.");
            }
            return new WorkflowTransitionMetadata
            {
                ActionName = "ResubmitFinancialReview",
                SetNextShortfallId = true,
                NextShortfallId = null
            };
        }

        public static void EnsureFinalDocumentsGenerated(SqlConnection connection,
            SqlTransaction transaction, int tenderId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT
                    CASE WHEN EXISTS
                    (
                        SELECT 1 FROM dbo.Agreement a
                        INNER JOIN dbo.TenderGeneratedDocuments d ON d.AgreementId=a.AgreementId
                          AND d.TenderId=a.TenderId AND d.DocumentKind=N'Agreement'
                        WHERE a.TenderId=@TenderId
                          AND a.AgreementId=(SELECT TOP (1) x.AgreementId FROM dbo.Agreement x
                            WHERE x.TenderId=@TenderId ORDER BY x.CreatedDate DESC,x.AgreementId DESC)
                    ) THEN 1 ELSE 0 END AS AgreementGenerated,
                    CASE WHEN EXISTS
                    (
                        SELECT 1 FROM dbo.TenderGeneratedDocuments
                        WHERE TenderId=@TenderId AND DocumentKind=N'BOQ'
                    ) THEN 1 ELSE 0 END AS BoqGenerated;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read() ||
                        Convert.ToInt32(reader["AgreementGenerated"], CultureInfo.InvariantCulture) != 1 ||
                        Convert.ToInt32(reader["BoqGenerated"], CultureInfo.InvariantCulture) != 1)
                        throw new InvalidOperationException("Generate and store the approved agreement and final BOQ PDFs before completing the tender.");
                }
            }
        }

        public static int CreateWorkOrder(SqlConnection connection, SqlTransaction transaction,
            int tenderId, string orderNumber, DateTime issueDateUtc, string description,
            string terms, string signatory)
        {
            string number = (orderNumber ?? String.Empty).Trim();
            string workDescription = (description ?? String.Empty).Trim();
            string conditions = (terms ?? String.Empty).Trim();
            string authorizedBy = (signatory ?? String.Empty).Trim();
            if (number.Length == 0 || number.Length > 50 || workDescription.Length == 0 ||
                workDescription.Length > 2000 || conditions.Length > 4000 ||
                authorizedBy.Length == 0 || authorizedBy.Length > 200)
                throw new InvalidOperationException("Provide a valid work order number, description, terms, and authorized signatory.");

            int contractorId;
            decimal approvedAmount;
            using (SqlCommand command = new SqlCommand(
                @"SELECT a.SelectedFinancialBidId,a.ApprovedAmount,b.ContractorId
                  FROM dbo.TenderFinancialApprovals a
                  INNER JOIN dbo.FinancialBid b ON b.FinancialBidId=a.SelectedFinancialBidId
                    AND b.TenderId=a.TenderId
                  WHERE a.TenderId=@TenderId AND a.Decision=N'Approved'
                    AND a.FinancialApprovalId=
                    (SELECT TOP (1) latest.FinancialApprovalId FROM dbo.TenderFinancialApprovals latest
                     WHERE latest.TenderId=a.TenderId
                     ORDER BY latest.ApprovedAtUtc DESC,latest.FinancialApprovalId DESC);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read() || reader["ApprovedAmount"] == DBNull.Value)
                        throw new InvalidOperationException("A Director Auditor-approved financial bid is required before creating a work order.");
                    contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                    approvedAmount = Convert.ToDecimal(reader["ApprovedAmount"], CultureInfo.InvariantCulture);
                }
            }

            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.WorkOrder
                    (TenderId,ContractorId,OrderNo,IssueDate,Amount,WorkDescription,TermsAndConditions,AuthorizedSignatory)
                  VALUES(@TenderId,@ContractorId,@Number,@IssueDate,@Amount,@Description,@Terms,@Signatory);
                  SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                ModuleDb.AddParameter(command, "@Number", SqlDbType.NVarChar, number, 50);
                ModuleDb.AddParameter(command, "@IssueDate", SqlDbType.DateTime2, issueDateUtc);
                ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, approvedAmount);
                ModuleDb.AddParameter(command, "@Description", SqlDbType.NVarChar, workDescription, 2000);
                ModuleDb.AddParameter(command, "@Terms", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(conditions) ? null : conditions, 4000);
                ModuleDb.AddParameter(command, "@Signatory", SqlDbType.NVarChar, authorizedBy, 200);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        public static int CreateAgreement(SqlConnection connection, SqlTransaction transaction,
            int tenderId, string agreementNumber, DateTime agreementDateUtc,
            string terms, string signatory)
        {
            string number = (agreementNumber ?? String.Empty).Trim();
            string conditions = (terms ?? String.Empty).Trim();
            string authorizedBy = (signatory ?? String.Empty).Trim();
            if (number.Length == 0 || number.Length > 50 || conditions.Length > 4000 ||
                authorizedBy.Length == 0 || authorizedBy.Length > 200)
                throw new InvalidOperationException("Provide a valid agreement number, terms, and authorized signatory.");

            int workOrderId, contractorId;
            decimal approvedAmount;
            using (SqlCommand command = new SqlCommand(
                @"SELECT TOP (1) w.WorkOrderId,w.ContractorId,w.Amount
                  FROM dbo.WorkOrder w
                  INNER JOIN dbo.TenderFinancialApprovals a ON a.TenderId=w.TenderId
                    AND a.SelectedFinancialBidId=(SELECT TOP (1) latest.SelectedFinancialBidId
                        FROM dbo.TenderFinancialApprovals latest
                        WHERE latest.TenderId=a.TenderId AND latest.Decision=N'Approved'
                        ORDER BY latest.ApprovedAtUtc DESC,latest.FinancialApprovalId DESC)
                  WHERE w.TenderId=@TenderId
                    AND a.Decision=N'Approved'
                    AND a.FinancialApprovalId=(SELECT TOP (1) latest.FinancialApprovalId
                        FROM dbo.TenderFinancialApprovals latest
                        WHERE latest.TenderId=a.TenderId
                        ORDER BY latest.ApprovedAtUtc DESC,latest.FinancialApprovalId DESC)
                  ORDER BY w.CreatedDate DESC,w.WorkOrderId DESC;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("A work order based on the approved financial bid is required before creating an agreement.");
                    workOrderId = Convert.ToInt32(reader["WorkOrderId"], CultureInfo.InvariantCulture);
                    contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                    approvedAmount = Convert.ToDecimal(reader["Amount"], CultureInfo.InvariantCulture);
                }
            }

            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.Agreement
                    (TenderId,ContractorId,AgreementNo,AgreementDate,ApprovedAmount,TermsAndConditions,AuthorizedSignatory)
                  VALUES(@TenderId,@ContractorId,@Number,@AgreementDate,@Amount,@Terms,@Signatory);
                  SELECT CAST(SCOPE_IDENTITY() AS int);", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                ModuleDb.AddParameter(command, "@Number", SqlDbType.NVarChar, number, 50);
                ModuleDb.AddParameter(command, "@AgreementDate", SqlDbType.DateTime2, agreementDateUtc);
                ModuleDb.AddParameter(command, "@Amount", SqlDbType.Decimal, approvedAmount);
                ModuleDb.AddParameter(command, "@Terms", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(conditions) ? null : conditions, 4000);
                ModuleDb.AddParameter(command, "@Signatory", SqlDbType.NVarChar, authorizedBy, 200);
                int agreementId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                using (SqlCommand verify = new SqlCommand(
                    @"IF NOT EXISTS(SELECT 1 FROM dbo.WorkOrder WHERE WorkOrderId=@WorkOrderId
                                    AND TenderId=@TenderId AND ContractorId=@ContractorId AND Amount=@Amount)
                      BEGIN
                          DELETE dbo.Agreement WHERE AgreementId=@AgreementId;
                          THROW 51133,'The work order no longer matches the approved bidder and amount.',1;
                      END;",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(verify, "@WorkOrderId", SqlDbType.Int, workOrderId);
                    ModuleDb.AddParameter(verify, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(verify, "@ContractorId", SqlDbType.Int, contractorId);
                    ModuleDb.AddParameter(verify, "@Amount", SqlDbType.Decimal, approvedAmount);
                    ModuleDb.AddParameter(verify, "@AgreementId", SqlDbType.Int, agreementId);
                    verify.ExecuteNonQuery();
                }
                return agreementId;
            }
        }

        public static WorkflowTransitionMetadata GenerateFinalDocument(Page page,
            SqlConnection connection, SqlTransaction transaction, int tenderId, int userId,
            string kind)
        {
            bool workOrder = String.Equals(kind, "WorkOrder", StringComparison.Ordinal);
            if (!workOrder && !String.Equals(kind, "Agreement", StringComparison.Ordinal))
                throw new InvalidOperationException("The generated document type is not supported.");

            Dictionary<string, string> fields = new Dictionary<string, string>();
            string tenderNumber, tenderTitle, fileStem;
            using (SqlCommand command = new SqlCommand(
                @"SELECT TenderNumber,Title FROM dbo.Tender
                  WHERE TenderId=@TenderId AND WorkflowVersion=2 AND IsDeleted=0;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The tender could not be found.");
                    tenderNumber = Convert.ToString(reader["TenderNumber"]);
                    tenderTitle = Convert.ToString(reader["Title"]);
                }
            }

            int recordId;
            using (SqlCommand command = new SqlCommand(workOrder
                ? @"SELECT TOP (1) w.WorkOrderId,w.OrderNo,w.IssueDate,w.Amount,w.WorkDescription,
                           w.TermsAndConditions,w.AuthorizedSignatory,c.Name AS ContractorName,
                           a.ApprovedAmount
                    FROM dbo.WorkOrder w
                    INNER JOIN dbo.Contractors c ON c.ContractorId=w.ContractorId
                    INNER JOIN dbo.TenderFinancialApprovals a ON a.TenderId=w.TenderId
                      AND a.Decision=N'Approved' AND a.SelectedFinancialBidId=
                          (SELECT TOP (1) f.SelectedFinancialBidId
                           FROM dbo.TenderFinancialApprovals f WHERE f.TenderId=w.TenderId
                           ORDER BY f.ApprovedAtUtc DESC,f.FinancialApprovalId DESC)
                    WHERE w.TenderId=@TenderId AND a.FinancialApprovalId=
                          (SELECT TOP (1) f.FinancialApprovalId
                           FROM dbo.TenderFinancialApprovals f WHERE f.TenderId=w.TenderId
                           ORDER BY f.ApprovedAtUtc DESC,f.FinancialApprovalId DESC)
                    ORDER BY w.CreatedDate DESC,w.WorkOrderId DESC;"
                : @"SELECT TOP (1) a.AgreementId,a.AgreementNo,a.AgreementDate,a.ApprovedAmount,
                           a.TermsAndConditions,a.AuthorizedSignatory,c.Name AS ContractorName,
                           w.OrderNo
                    FROM dbo.Agreement a
                    INNER JOIN dbo.Contractors c ON c.ContractorId=a.ContractorId
                    INNER JOIN dbo.WorkOrder w ON w.TenderId=a.TenderId AND w.ContractorId=a.ContractorId
                      AND w.Amount=a.ApprovedAmount
                    WHERE a.TenderId=@TenderId AND a.AgreementId=
                      (SELECT TOP (1) x.AgreementId FROM dbo.Agreement x
                       WHERE x.TenderId=@TenderId ORDER BY x.CreatedDate DESC,x.AgreementId DESC)
                    ORDER BY w.CreatedDate DESC,w.WorkOrderId DESC;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException(workOrder
                            ? "Create a work order based on the approved bidder before generating its PDF."
                            : "Create an agreement based on the approved bidder before generating its PDF.");
                    recordId = Convert.ToInt32(reader[workOrder ? "WorkOrderId" : "AgreementId"],
                        CultureInfo.InvariantCulture);
                    fields.Add(workOrder ? "Work order number" : "Agreement number",
                        Convert.ToString(reader[workOrder ? "OrderNo" : "AgreementNo"]));
                    fields.Add(workOrder ? "Issue date (UTC)" : "Agreement date (UTC)",
                        Convert.ToDateTime(reader[workOrder ? "IssueDate" : "AgreementDate"],
                            CultureInfo.InvariantCulture).ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
                    fields.Add("Contractor", Convert.ToString(reader["ContractorName"]));
                    if (!workOrder) fields.Add("Related work order", Convert.ToString(reader["OrderNo"]));
                    fields.Add("Approved amount", Convert.ToDecimal(
                        reader[workOrder ? "ApprovedAmount" : "ApprovedAmount"],
                        CultureInfo.InvariantCulture).ToString("N2", CultureInfo.InvariantCulture));
                    if (workOrder)
                        fields.Add("Work description", Convert.ToString(reader["WorkDescription"]));
                    fields.Add("Terms and conditions", Convert.ToString(reader["TermsAndConditions"]));
                    fields.Add("Authorized signatory", Convert.ToString(reader["AuthorizedSignatory"]));
                    fileStem = Convert.ToString(reader[workOrder ? "OrderNo" : "AgreementNo"]);
                }
            }

            int version;
            using (SqlCommand command = new SqlCommand(
                workOrder
                    ? "SELECT COALESCE(MAX(VersionNumber),0)+1 FROM dbo.TenderGeneratedDocuments WITH (UPDLOCK,HOLDLOCK) WHERE WorkOrderId=@RecordId;"
                    : "SELECT COALESCE(MAX(VersionNumber),0)+1 FROM dbo.TenderGeneratedDocuments WITH (UPDLOCK,HOLDLOCK) WHERE AgreementId=@RecordId;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@RecordId", SqlDbType.Int, recordId);
                version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            string fileName = TenderLifecycleFiles.SafeFileName(fileStem) +
                "_v" + version.ToString(CultureInfo.InvariantCulture) + ".pdf";
            byte[] content = TenderDocumentGenerator.Render(
                workOrder ? "Work Order" : "Tender Agreement", tenderNumber, tenderTitle, fields);
            string relativePath = TenderLifecycleFiles.SaveGeneratedTenderDocument(
                page, tenderId, fileName, content);

            try
            {
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderGeneratedDocuments
                        (TenderId,WorkOrderId,AgreementId,DocumentKind,VersionNumber,FileName,FilePath,GeneratedBy)
                      VALUES(@TenderId,@WorkOrderId,@AgreementId,@Kind,@Version,@FileName,@FilePath,@UserId);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@WorkOrderId", SqlDbType.Int,
                        workOrder ? (object)recordId : null);
                    ModuleDb.AddParameter(command, "@AgreementId", SqlDbType.Int,
                        workOrder ? null : (object)recordId);
                    ModuleDb.AddParameter(command, "@Kind", SqlDbType.NVarChar, kind, 20);
                    ModuleDb.AddParameter(command, "@Version", SqlDbType.Int, version);
                    ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 520);
                    ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, relativePath, 800);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    command.ExecuteNonQuery();
                }

                using (SqlCommand command = new SqlCommand(
                    workOrder
                        ? "UPDATE dbo.WorkOrder SET DocumentPath=@Path WHERE WorkOrderId=@RecordId;"
                        : "UPDATE dbo.Agreement SET DocumentPath=@Path WHERE AgreementId=@RecordId;",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@Path", SqlDbType.NVarChar, relativePath, 800);
                    ModuleDb.AddParameter(command, "@RecordId", SqlDbType.Int, recordId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("The generated document was saved but could not be linked to its record.");
                }
            }
            catch
            {
                TenderLifecycleFiles.DeleteStoredGeneratedTenderDocument(page, relativePath);
                throw;
            }

            return new WorkflowTransitionMetadata
            {
                RelatedDocumentPath = relativePath,
                ActionName = "Generate" + kind + "V" + version.ToString(CultureInfo.InvariantCulture)
            };
        }

        public static WorkflowTransitionMetadata GenerateFinalBoqDocument(Page page,
            SqlConnection connection, SqlTransaction transaction, int tenderId, int userId)
        {
            if (page == null || connection == null || transaction == null ||
                tenderId <= 0 || userId <= 0)
                throw new InvalidOperationException("A valid tender and authenticated user are required to generate the BOQ.");

            using (SqlCommand command = new SqlCommand(
                @"SELECT CASE WHEN EXISTS
                  (
                      SELECT 1 FROM sys.check_constraints
                      WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
                        AND name=N'CK_TenderGeneratedDocuments_Kind'
                        AND definition LIKE N'%BOQ%'
                  ) AND EXISTS
                  (
                      SELECT 1 FROM sys.check_constraints
                      WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
                        AND name=N'CK_TenderGeneratedDocuments_Target'
                        AND definition LIKE N'%BOQ%'
                  ) THEN 1 ELSE 0 END;", connection, transaction))
            {
                if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
                    throw new InvalidOperationException("Apply the BOQ document database upgrade before generating the final agreement.");
            }

            using (SqlCommand command = new SqlCommand(
                @"SELECT TOP (1) Decision,SelectedFinancialBidId
                  FROM dbo.TenderFinancialApprovals
                  WHERE TenderId=@TenderId
                  ORDER BY ApprovedAtUtc DESC,FinancialApprovalId DESC;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read() || Convert.ToString(reader["Decision"]) != "Approved" ||
                        reader["SelectedFinancialBidId"] == DBNull.Value)
                        throw new InvalidOperationException("The BOQ can be generated only after the Chief Auditor approves a selected bidder.");
                }
            }

            string tenderNumber, tenderTitle;
            DataTable items = new DataTable();
            using (SqlCommand command = new SqlCommand(
                @"SELECT t.TenderNumber,t.Title,b.BOQId,b.ItemName,b.Description,b.Quantity,b.Unit,b.Rate,b.Amount
                  FROM dbo.Tender t
                  INNER JOIN dbo.BOQ b ON b.TenderId=t.TenderId
                  WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                  ORDER BY b.BOQId;", connection, transaction))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                adapter.Fill(items);
            }
            if (items.Rows.Count == 0)
                throw new InvalidOperationException("Add at least one BOQ item before generating the final BOQ.");
            tenderNumber = Convert.ToString(items.Rows[0]["TenderNumber"]);
            tenderTitle = Convert.ToString(items.Rows[0]["Title"]);

            int version;
            using (SqlCommand command = new SqlCommand(
                @"SELECT COALESCE(MAX(VersionNumber),0)+1
                  FROM dbo.TenderGeneratedDocuments WITH (UPDLOCK,HOLDLOCK)
                  WHERE TenderId=@TenderId AND DocumentKind=N'BOQ';", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            string fileName = TenderLifecycleFiles.SafeFileName(tenderNumber + "_BOQ_v" +
                version.ToString(CultureInfo.InvariantCulture) + ".pdf");
            byte[] content = TenderDocumentGenerator.RenderBoq(tenderNumber, tenderTitle, items);
            string relativePath = TenderLifecycleFiles.SaveGeneratedTenderDocument(
                page, tenderId, fileName, content);

            try
            {
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderGeneratedDocuments
                        (TenderId,WorkOrderId,AgreementId,DocumentKind,VersionNumber,FileName,FilePath,GeneratedBy)
                      VALUES(@TenderId,NULL,NULL,N'BOQ',@Version,@FileName,@FilePath,@UserId);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@Version", SqlDbType.Int, version);
                    ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 520);
                    ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, relativePath, 800);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    if (command.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("The BOQ PDF could not be recorded.");
                }

                ModuleDb.AddAudit(connection, transaction, userId, "GenerateFinalBoq",
                    "Tender", tenderId, "Generated final BOQ PDF version " +
                    version.ToString(CultureInfo.InvariantCulture), null);
            }
            catch
            {
                TenderLifecycleFiles.DeleteStoredGeneratedTenderDocument(page, relativePath);
                throw;
            }

            return new WorkflowTransitionMetadata
            {
                RelatedDocumentPath = relativePath,
                ActionName = "GenerateBOQV" + version.ToString(CultureInfo.InvariantCulture)
            };
        }

        public static void SubmitShortfallItem(Page page, long shortfallId, long itemId,
            int userId, HttpPostedFile file, string remarks, string ipAddress)
        {
            string note = (remarks ?? String.Empty).Trim();
            if (page == null || shortfallId <= 0 || itemId <= 0 || userId <= 0 ||
                file == null || file.ContentLength <= 0 || note.Length > 1000)
                throw new InvalidOperationException("Choose a valid shortfall item, a non-empty document, and remarks up to 1,000 characters.");

            string filePath = null;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    int tenderId, contractorId, applicationId, cellRoleId, departmentId;
                    string documentType, clearanceStatus, shortfallStatus;
                    DateTime deadline;
                    int maxFileSize = 10 * 1024 * 1024;
                    string allowedExtensions = null;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT s.TenderId,s.ContractorId,b.TechnicalBidId,s.SubmissionDeadlineUtc,
                                 s.Status AS ShortfallStatus,i.RequiredDocumentType,i.ClearanceStatus,
                                 a.AssignedRoleId,a.DepartmentId,t.CurrentStatus,u.AccountType,u.RoleId,
                                 r.MaxFileSizeBytes,r.AllowedExtensions
                          FROM dbo.TenderShortfalls s WITH (UPDLOCK,HOLDLOCK)
                          INNER JOIN dbo.TenderShortfallItems i WITH (UPDLOCK,HOLDLOCK)
                            ON i.ShortfallId=s.ShortfallId AND i.ShortfallItemId=@ItemId
                          INNER JOIN dbo.TechnicalBid b ON b.TenderId=s.TenderId AND b.ContractorId=s.ContractorId
                          INNER JOIN dbo.Contractors c ON c.ContractorId=s.ContractorId AND c.UserId=@UserId
                          INNER JOIN dbo.Users u ON u.UserId=@UserId AND u.IsActive=1
                          INNER JOIN dbo.Tender t ON t.TenderId=s.TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                          INNER JOIN dbo.TenderWorkflowAssignments a WITH (UPDLOCK,HOLDLOCK)
                            ON a.TenderId=s.TenderId AND a.ShortfallId=s.ShortfallId AND a.IsCurrent=1
                          INNER JOIN dbo.WorkflowStages ws ON ws.StageId=a.StageId
                            AND ws.WorkflowVersion=2 AND ws.IsActive=1
                            AND ws.StageName=N'Final_Contractor_SubmitShortfall'
                          LEFT JOIN dbo.TenderRequiredBidDocuments r
                            ON r.TenderId=s.TenderId AND r.DocumentType=i.RequiredDocumentType
                          WHERE s.ShortfallId=@ShortfallId AND s.Status=N'Awaiting Contractor'
                            AND t.CurrentStatus=N'Awaiting Contractor Submission'
                            AND a.AssignedUserId=@UserId AND i.ClearanceStatus IN (N'Pending',N'Continued Deficiency');",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                        ModuleDb.AddParameter(command, "@ItemId", SqlDbType.BigInt, itemId);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read() || !String.Equals(Convert.ToString(reader["AccountType"]),
                                "ExternalBidder", StringComparison.Ordinal) || reader["RoleId"] != DBNull.Value)
                                throw new InvalidOperationException("This shortfall is not assigned to your contractor account.");
                            tenderId = Convert.ToInt32(reader["TenderId"], CultureInfo.InvariantCulture);
                            contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                            applicationId = Convert.ToInt32(reader["TechnicalBidId"], CultureInfo.InvariantCulture);
                            deadline = Convert.ToDateTime(reader["SubmissionDeadlineUtc"], CultureInfo.InvariantCulture);
                            documentType = Convert.ToString(reader["RequiredDocumentType"]);
                            clearanceStatus = Convert.ToString(reader["ClearanceStatus"]);
                            shortfallStatus = Convert.ToString(reader["ShortfallStatus"]);
                            cellRoleId = Convert.ToInt32(reader["AssignedRoleId"], CultureInfo.InvariantCulture);
                            departmentId = Convert.ToInt32(reader["DepartmentId"], CultureInfo.InvariantCulture);
                            if (reader["MaxFileSizeBytes"] != DBNull.Value)
                                maxFileSize = Convert.ToInt32(reader["MaxFileSizeBytes"], CultureInfo.InvariantCulture);
                            if (reader["AllowedExtensions"] != DBNull.Value)
                                allowedExtensions = Convert.ToString(reader["AllowedExtensions"]);
                        }
                    }

                    if (deadline <= DateTime.UtcNow)
                        throw new InvalidOperationException("The three-calendar-day shortfall deadline has passed.");
                    if (clearanceStatus != "Pending" && clearanceStatus != "Continued Deficiency")
                        throw new InvalidOperationException("This shortfall item has already been submitted for review.");
                    string fileName = TenderLifecycleFiles.SafeFileName(file.FileName);
                    filePath = TenderLifecycleFiles.SaveTenderApplicationUpload(page, file, tenderId,
                        contractorId, maxFileSize, allowedExtensions);

                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TechnicalBidDocuments
                            (TechnicalBidId,FileName,FilePath,DocumentType,UploadedBy)
                          VALUES(@ApplicationId,@FileName,@FilePath,@DocumentType,@UserId);",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                        ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 260);
                        ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, filePath, 400);
                        ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar, documentType, 160);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                        command.ExecuteNonQuery();
                    }

                    using (SqlCommand command = new SqlCommand(
                        @"INSERT INTO dbo.TenderShortfallSubmissions
                            (ShortfallItemId,FileName,FilePath,SubmittedBy,Remarks)
                          VALUES(@ItemId,@FileName,@FilePath,@UserId,@Remarks);
                          UPDATE dbo.TenderShortfallItems
                          SET ClearanceStatus=N'Submitted',Remarks=@Remarks
                          WHERE ShortfallItemId=@ItemId AND ClearanceStatus IN (N'Pending',N'Continued Deficiency');",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@ItemId", SqlDbType.BigInt, itemId);
                        ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 520);
                        ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, filePath, 800);
                        ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                        ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                            String.IsNullOrWhiteSpace(note) ? null : note, 1000);
                        if (command.ExecuteNonQuery() != 2)
                            throw new InvalidOperationException("The shortfall item changed; refresh and retry.");
                    }

                    ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                        "ShortfallItemSubmitted", null, null,
                        "Corrected " + documentType + " document submitted." +
                        (String.IsNullOrWhiteSpace(note) ? String.Empty : " " + note),
                        userId, 0, true);

                    int remaining;
                    using (SqlCommand command = new SqlCommand(
                        @"SELECT COUNT(1) FROM dbo.TenderShortfallItems
                          WHERE ShortfallId=@ShortfallId AND ClearanceStatus NOT IN (N'Submitted',N'Cleared');",
                        connection, transaction))
                    {
                        ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                        remaining = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                    }
                    if (remaining == 0)
                    {
                        using (SqlCommand command = new SqlCommand(
                            @"UPDATE dbo.TenderShortfalls
                              SET Status=N'Documents Submitted',ClearanceStatus=N'Submitted',
                                  ContractorSubmittedAtUtc=SYSUTCDATETIME()
                              WHERE ShortfallId=@ShortfallId AND Status=N'Awaiting Contractor';",
                            connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("The shortfall changed before submission could be completed.");
                        }

                        int currentStageId;
                        using (SqlCommand command = new SqlCommand(
                            @"SELECT a.StageId FROM dbo.TenderWorkflowAssignments a
                              WHERE a.TenderId=@TenderId AND a.ShortfallId=@ShortfallId
                                AND a.IsCurrent=1 AND a.AssignedUserId=@UserId
                                AND a.AssignedRoleId=@RoleId;", connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                            ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                            ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                            ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, cellRoleId);
                            object stage = command.ExecuteScalar();
                            if (stage == null || stage == DBNull.Value)
                                throw new InvalidOperationException("The contractor workflow assignment changed; refresh and retry.");
                            currentStageId = Convert.ToInt32(stage, CultureInfo.InvariantCulture);
                        }
                        int nextStageId, nextRoleId;
                        using (SqlCommand command = new SqlCommand(
                            @"SELECT s.StageId,s.ApproverRoleId
                              FROM dbo.WorkflowStages s
                              WHERE s.WorkflowVersion=2 AND s.IsActive=1
                                AND s.StageName=N'Final_Cell_ClearShortfall';", connection, transaction))
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("Tender Cell shortfall clearance is not configured.");
                            nextStageId = Convert.ToInt32(reader["StageId"], CultureInfo.InvariantCulture);
                            nextRoleId = Convert.ToInt32(reader["ApproverRoleId"], CultureInfo.InvariantCulture);
                        }

                        using (SqlCommand command = new SqlCommand(
                            @"UPDATE dbo.Tender
                              SET CurrentStatus=N'Shortfall Documents Submitted',ModifiedBy=@UserId,
                                  ModifiedDate=SYSUTCDATETIME()
                              WHERE TenderId=@TenderId AND CurrentStatus=N'Awaiting Contractor Submission'
                                AND WorkflowVersion=2 AND IsDeleted=0;
                              IF @@ROWCOUNT<>1 THROW 51120,'Tender status changed.',1;
                              UPDATE dbo.TenderWorkflowAssignments
                              SET IsCurrent=0,CompletedAtUtc=SYSUTCDATETIME()
                              WHERE TenderId=@TenderId AND ShortfallId=@ShortfallId
                                AND StageId=@CurrentStageId AND IsCurrent=1;
                              IF @@ROWCOUNT<>1 THROW 51121,'Workflow assignment changed.',1;
                              INSERT INTO dbo.TenderWorkflowAssignments
                                (TenderId,ShortfallId,StageId,AssignedRoleId,AssignedUserId,DepartmentId,IsCurrent)
                              VALUES(@TenderId,@ShortfallId,@NextStageId,@NextRoleId,NULL,@DepartmentId,1);
                              INSERT INTO dbo.TenderWorkflowHistory
                                (TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,
                                 UserId,RoleId,ActorRoleCode,DepartmentId,Remarks)
                              VALUES(@TenderId,2,@CurrentStageId,N'Awaiting Contractor Submission',
                                     N'Shortfall Documents Submitted',N'ContractorSubmitShortfallItems',
                                     @UserId,@RoleId,N'ExternalBidder',@DepartmentId,@Remarks);",
                            connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                            ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                            ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                            ModuleDb.AddParameter(command, "@CurrentStageId", SqlDbType.Int, currentStageId);
                            ModuleDb.AddParameter(command, "@NextStageId", SqlDbType.Int, nextStageId);
                            ModuleDb.AddParameter(command, "@NextRoleId", SqlDbType.Int, nextRoleId);
                            ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, cellRoleId);
                            ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId);
                            ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                                String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                            command.ExecuteNonQuery();
                        }
                        NotifyRole(connection, transaction, tenderId, shortfallId, departmentId,
                            "TENDER_CELL", "Corrected shortfall documents are ready for clearance.");
                    }

                    ModuleDb.AddAudit(connection, transaction, userId, "SubmitShortfallItem",
                        "TenderShortfall", tenderId,
                        "Shortfall " + shortfallId.ToString(CultureInfo.InvariantCulture) +
                        ", item " + itemId.ToString(CultureInfo.InvariantCulture), ipAddress);
                    transaction.Commit();
                }
            }
            catch
            {
                if (!String.IsNullOrWhiteSpace(filePath))
                    TenderLifecycleFiles.DeleteStoredApplicationUpload(page, filePath);
                throw;
            }
        }

        public static bool ExpireOverdueShortfall(int tenderId, int userId, int roleId,
            string roleCode, int? departmentId, string ipAddress)
        {
            if (!IsTenderCell(roleCode) || !departmentId.HasValue)
                return false;

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                long shortfallId;
                int assignmentId, stageId, contractorId, applicationId, contractorUserId, department;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT a.AssignmentId,a.StageId,a.DepartmentId,f.ShortfallId,
                             f.ContractorId,c.UserId,b.TechnicalBidId
                      FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
                      INNER JOIN dbo.TenderWorkflowAssignments a WITH (UPDLOCK,HOLDLOCK)
                        ON a.TenderId=t.TenderId AND a.IsCurrent=1
                      INNER JOIN dbo.WorkflowStages ws ON ws.StageId=a.StageId
                        AND ws.WorkflowVersion=2 AND ws.IsActive=1
                        AND ws.StageName=N'Final_Contractor_SubmitShortfall'
                      INNER JOIN dbo.TenderShortfalls f WITH (UPDLOCK,HOLDLOCK)
                        ON f.ShortfallId=a.ShortfallId AND f.TenderId=t.TenderId
                        AND f.Status=N'Awaiting Contractor'
                        AND f.SubmissionDeadlineUtc<=SYSUTCDATETIME()
                      INNER JOIN dbo.Contractors c ON c.ContractorId=f.ContractorId
                      INNER JOIN dbo.TechnicalBid b ON b.TenderId=t.TenderId
                        AND b.ContractorId=f.ContractorId AND b.Status=N'Submitted'
                      INNER JOIN dbo.Users u ON u.UserId=@UserId AND u.IsActive=1
                        AND u.AccountType=N'Internal' AND u.RoleId=@RoleId
                      WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                        AND t.CurrentStatus=N'Awaiting Contractor Submission'
                        AND a.AssignedRoleId=@RoleId AND a.DepartmentId=t.DepartmentId
                        AND t.DepartmentId=@DepartmentId
                        AND EXISTS
                        (
                            SELECT 1 FROM dbo.RolePermissions rp
                            INNER JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId AND p.IsActive=1
                            WHERE rp.RoleId=@RoleId AND p.PermissionKey=N'shortfall.clear'
                        );", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, departmentId.Value);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            transaction.Commit();
                            return false;
                        }
                        assignmentId = Convert.ToInt32(reader["AssignmentId"], CultureInfo.InvariantCulture);
                        stageId = Convert.ToInt32(reader["StageId"], CultureInfo.InvariantCulture);
                        department = Convert.ToInt32(reader["DepartmentId"], CultureInfo.InvariantCulture);
                        shortfallId = Convert.ToInt64(reader["ShortfallId"], CultureInfo.InvariantCulture);
                        contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                        contractorUserId = Convert.ToInt32(reader["UserId"], CultureInfo.InvariantCulture);
                        applicationId = Convert.ToInt32(reader["TechnicalBidId"], CultureInfo.InvariantCulture);
                    }
                }

                int nextStageId, nextRoleId;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT StageId,ApproverRoleId FROM dbo.WorkflowStages
                      WHERE WorkflowVersion=2 AND IsActive=1
                        AND StageName=N'Final_Cell_CompleteSecondaryVerification';",
                    connection, transaction))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("Tender Cell secondary verification is not configured.");
                    nextStageId = Convert.ToInt32(reader["StageId"], CultureInfo.InvariantCulture);
                    nextRoleId = Convert.ToInt32(reader["ApproverRoleId"], CultureInfo.InvariantCulture);
                }

                using (SqlCommand command = new SqlCommand(
                    @"UPDATE dbo.TenderShortfalls
                      SET Status=N'Rejected',ClearanceStatus=N'Continued Deficiency',
                          Remarks=COALESCE(Remarks+N' ',N'')+N'The three-calendar-day deadline expired.'
                      WHERE ShortfallId=@ShortfallId AND Status=N'Awaiting Contractor';
                      UPDATE dbo.TenderShortfallItems
                      SET ClearanceStatus=N'Continued Deficiency',
                          Remarks=COALESCE(Remarks+N' ',N'')+N'The three-calendar-day deadline expired.'
                      WHERE ShortfallId=@ShortfallId AND ClearanceStatus IN (N'Pending',N'Submitted',N'Continued Deficiency');
                      UPDATE dbo.TechnicalBid
                      SET Status=N'Disqualified',EvaluatedBy=@UserId,EvaluatedDate=SYSUTCDATETIME()
                      WHERE TechnicalBidId=@ApplicationId AND Status=N'Submitted';
                      UPDATE dbo.Tender
                      SET CurrentStatus=N'Tender Cell Verification Pending',ModifiedBy=@UserId,
                          ModifiedDate=SYSUTCDATETIME()
                      WHERE TenderId=@TenderId AND CurrentStatus=N'Awaiting Contractor Submission'
                        AND WorkflowVersion=2 AND IsDeleted=0;
                      IF @@ROWCOUNT<>1 THROW 51140,'Tender status changed.',1;
                      UPDATE dbo.TenderWorkflowAssignments
                      SET IsCurrent=0,CompletedAtUtc=SYSUTCDATETIME()
                      WHERE AssignmentId=@AssignmentId AND IsCurrent=1;
                      IF @@ROWCOUNT<>1 THROW 51141,'Workflow assignment changed.',1;
                      INSERT INTO dbo.TenderWorkflowAssignments
                        (TenderId,ShortfallId,StageId,AssignedRoleId,AssignedUserId,DepartmentId,IsCurrent)
                      VALUES(@TenderId,NULL,@NextStageId,@NextRoleId,@UserId,@DepartmentId,1);
                      INSERT INTO dbo.TenderWorkflowHistory
                        (TenderId,WorkflowVersion,StageId,PreviousStatus,NewStatus,ActionName,
                         UserId,RoleId,ActorRoleCode,DepartmentId,Remarks)
                      VALUES(@TenderId,2,@CurrentStageId,N'Awaiting Contractor Submission',
                             N'Tender Cell Verification Pending',N'ShortfallDeadlineExpired',
                             @UserId,@RoleId,@RoleCode,@DepartmentId,N'The three-calendar-day deadline expired.');",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@AssignmentId", SqlDbType.Int, assignmentId);
                    ModuleDb.AddParameter(command, "@CurrentStageId", SqlDbType.Int, stageId);
                    ModuleDb.AddParameter(command, "@NextStageId", SqlDbType.Int, nextStageId);
                    ModuleDb.AddParameter(command, "@NextRoleId", SqlDbType.Int, nextRoleId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                    ModuleDb.AddParameter(command, "@RoleCode", SqlDbType.NVarChar, roleCode, 50);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, department);
                    command.ExecuteNonQuery();
                }

                string previousStatus = ContractorApplicationRules.GetStatus(connection, transaction, applicationId);
                ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                    "ShortfallDeadlineExpired", previousStatus, "Disqualified",
                    "The required shortfall documents were not submitted within three calendar days.",
                    userId, roleId, true);
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,ShortfallId,NotificationType,Message)
                      VALUES(@UserId,@TenderId,@ShortfallId,N'ShortfallExpired',
                             N'The shortfall deadline passed without a response; this application is disqualified.');",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, contractorUserId);
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                    command.ExecuteNonQuery();
                }
                ModuleDb.AddAudit(connection, transaction, userId, "ExpireTenderShortfall",
                    "TenderShortfall", tenderId,
                    "Shortfall " + shortfallId.ToString(CultureInfo.InvariantCulture) +
                    ", contractor " + contractorId.ToString(CultureInfo.InvariantCulture), ipAddress);
                transaction.Commit();
                return true;
            }
        }

        public static WorkflowTransitionMetadata ReviewShortfallItem(SqlConnection connection,
            SqlTransaction transaction, int tenderId, long shortfallId, long itemId, int userId,
            int roleId, string decision, string remarks)
        {
            string normalizedDecision = (decision ?? String.Empty).Trim();
            string note = (remarks ?? String.Empty).Trim();
            if (normalizedDecision != "Cleared" && normalizedDecision != "Continued Deficiency" ||
                note.Length > 2000 || (normalizedDecision == "Continued Deficiency" && note.Length == 0))
                throw new InvalidOperationException("Choose a valid clearance decision and explain any continued deficiency.");

            int applicationId, contractorId, contractorUserId, departmentId;
            DateTime deadline;
            string itemType;
            using (SqlCommand command = new SqlCommand(
                @"SELECT b.TechnicalBidId,s.ContractorId,c.UserId,s.SubmissionDeadlineUtc,
                         i.RequiredDocumentType,a.DepartmentId
                  FROM dbo.TenderShortfalls s WITH (UPDLOCK,HOLDLOCK)
                  INNER JOIN dbo.TenderShortfallItems i WITH (UPDLOCK,HOLDLOCK)
                    ON i.ShortfallId=s.ShortfallId AND i.ShortfallItemId=@ItemId
                  INNER JOIN dbo.Contractors c ON c.ContractorId=s.ContractorId
                  INNER JOIN dbo.TechnicalBid b ON b.TenderId=s.TenderId AND b.ContractorId=s.ContractorId
                  INNER JOIN dbo.TenderWorkflowAssignments a WITH (UPDLOCK,HOLDLOCK)
                    ON a.TenderId=s.TenderId AND a.ShortfallId=s.ShortfallId AND a.IsCurrent=1
                  WHERE s.ShortfallId=@ShortfallId AND s.TenderId=@TenderId
                    AND s.Status=N'Documents Submitted' AND i.ClearanceStatus=N'Submitted';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ItemId", SqlDbType.BigInt, itemId);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The shortfall item is not awaiting clearance.");
                    applicationId = Convert.ToInt32(reader["TechnicalBidId"], CultureInfo.InvariantCulture);
                    contractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture);
                    contractorUserId = Convert.ToInt32(reader["UserId"], CultureInfo.InvariantCulture);
                    deadline = Convert.ToDateTime(reader["SubmissionDeadlineUtc"], CultureInfo.InvariantCulture);
                    itemType = Convert.ToString(reader["RequiredDocumentType"]);
                    departmentId = Convert.ToInt32(reader["DepartmentId"], CultureInfo.InvariantCulture);
                }
            }

            int submissionDocumentId;
            string fileName, filePath;
            using (SqlCommand command = new SqlCommand(
                @"SELECT TOP (1) d.TechnicalBidDocumentId,su.FileName,su.FilePath
                  FROM dbo.TenderShortfallSubmissions su
                  INNER JOIN dbo.TechnicalBidDocuments d ON d.FilePath=su.FilePath
                  INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                  WHERE su.ShortfallItemId=@ItemId AND b.TechnicalBidId=@ApplicationId
                  ORDER BY su.SubmittedAtUtc DESC,su.ShortfallSubmissionId DESC;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ItemId", SqlDbType.BigInt, itemId);
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The latest contractor submission could not be found.");
                    submissionDocumentId = Convert.ToInt32(reader["TechnicalBidDocumentId"], CultureInfo.InvariantCulture);
                    fileName = Convert.ToString(reader["FileName"]);
                    filePath = Convert.ToString(reader["FilePath"]);
                }
            }

            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderDocumentVerificationHistory
                    (TenderId,ContractorId,TechnicalBidDocumentId,FileName,FilePath,DocumentType,
                     ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks)
                  VALUES(@TenderId,@ContractorId,@DocumentId,@FileName,@FilePath,@DocumentType,
                         N'Secondary',@Status,@UserId,@RoleId,@Remarks);
                  UPDATE dbo.TenderShortfallItems
                  SET ClearanceStatus=@ItemStatus,Remarks=@Remarks
                  WHERE ShortfallItemId=@ItemId AND ClearanceStatus=N'Submitted';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, submissionDocumentId);
                ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 520);
                ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, filePath, 800);
                ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar, itemType, 160);
                ModuleDb.AddParameter(command, "@Status", SqlDbType.NVarChar,
                    normalizedDecision == "Cleared" ? "Verified" : "Not Verified", 20);
                ModuleDb.AddParameter(command, "@ItemStatus", SqlDbType.NVarChar, normalizedDecision, 30);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                ModuleDb.AddParameter(command, "@ItemId", SqlDbType.BigInt, itemId);
                if (command.ExecuteNonQuery() != 2)
                    throw new InvalidOperationException("The shortfall item changed; refresh and retry.");
            }

            int remaining;
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1) FROM dbo.TenderShortfallItems
                  WHERE ShortfallId=@ShortfallId AND ClearanceStatus=N'Submitted';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                remaining = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
            if (remaining > 0)
            {
                return new WorkflowTransitionMetadata { HoldStage = true, ActionName = "ReviewShortfallItem" };
            }

            bool continued;
            using (SqlCommand command = new SqlCommand(
                @"SELECT CASE WHEN EXISTS
                  (SELECT 1 FROM dbo.TenderShortfallItems
                   WHERE ShortfallId=@ShortfallId AND ClearanceStatus=N'Continued Deficiency')
                  THEN 1 ELSE 0 END;", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                continued = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }

            string newTenderStatus;
            string newShortfallStatus;
            string newClearanceStatus;
            if (continued && deadline > DateTime.UtcNow)
            {
                newTenderStatus = "Awaiting Contractor Submission";
                newShortfallStatus = "Awaiting Contractor";
                newClearanceStatus = "Continued Deficiency";
            }
            else if (continued)
            {
                newTenderStatus = "Tender Cell Verification Pending";
                newShortfallStatus = "Rejected";
                newClearanceStatus = "Continued Deficiency";
                using (SqlCommand command = new SqlCommand(
                    @"UPDATE dbo.TechnicalBid SET Status=N'Disqualified',EvaluatedBy=@UserId,
                        EvaluatedDate=SYSUTCDATETIME()
                      WHERE TechnicalBidId=@ApplicationId AND Status=N'Submitted';",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                    command.ExecuteNonQuery();
                }
                ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                    "ShortfallDeadlineExpired", "UnderVerification", "Disqualified",
                    "The required shortfall documents were not cleared before the three-calendar-day deadline.",
                    userId, roleId, true);
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,ShortfallId,NotificationType,Message)
                      VALUES(@UserId,@TenderId,@ShortfallId,N'ShortfallExpired',
                             N'The shortfall deadline passed without clearance; this application is disqualified.');",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, contractorUserId);
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                newTenderStatus = "Shortfall Cleared";
                newShortfallStatus = "Cleared";
                newClearanceStatus = "Cleared";
                using (SqlCommand command = new SqlCommand(
                    @"UPDATE dbo.TenderShortfalls
                      SET ClearedBy=@UserId,ClearedAtUtc=SYSUTCDATETIME()
                      WHERE ShortfallId=@ShortfallId;", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                    command.ExecuteNonQuery();
                }
                ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                    "StructuredShortfallCleared", null, null,
                    "All shortfall items were cleared by Tender Cell.", userId, roleId, true);
                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,ShortfallId,NotificationType,Message)
                      VALUES(@UserId,@TenderId,@ShortfallId,N'ShortfallCleared',
                             N'All corrected shortfall documents have been cleared.');",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, contractorUserId);
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                    command.ExecuteNonQuery();
                }
            }

            using (SqlCommand command = new SqlCommand(
                @"UPDATE dbo.TenderShortfalls
                  SET Status=@Status,ClearanceStatus=@ClearanceStatus,
                      Remarks=CASE WHEN @Remarks IS NULL THEN Remarks ELSE @Remarks END
                  WHERE ShortfallId=@ShortfallId AND Status=N'Documents Submitted';",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@Status", SqlDbType.NVarChar, newShortfallStatus, 40);
                ModuleDb.AddParameter(command, "@ClearanceStatus", SqlDbType.NVarChar, newClearanceStatus, 30);
                ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                    String.IsNullOrWhiteSpace(note) ? null : note, 2000);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt, shortfallId);
                command.ExecuteNonQuery();
            }

            ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                normalizedDecision == "Cleared" ? "ShortfallItemCleared" : "ShortfallItemContinued",
                null, null, String.IsNullOrWhiteSpace(note) ? null : note, userId, roleId, true);
            return new WorkflowTransitionMetadata
            {
                NewStatusOverride = newTenderStatus,
                SetNextShortfallId = true,
                NextShortfallId = newTenderStatus == "Tender Cell Verification Pending" ? (long?)null : shortfallId,
                SetNextAssignedUserId = newTenderStatus == "Awaiting Contractor Submission",
                NextAssignedUserId = newTenderStatus == "Awaiting Contractor Submission"
                    ? (int?)contractorUserId : null,
                ActionName = newTenderStatus == "Awaiting Contractor Submission"
                    ? "ReturnShortfallToContractor"
                    : newTenderStatus == "Shortfall Cleared" ? "ClearShortfall" : "ExpireShortfall"
            };
        }

        private static StageAssignment LockAssignedStage(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int userId, int roleId,
            string stageName, long? shortfallId, string requiredPermission)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT a.AssignmentId,a.StageId,s.StageOrder,a.DepartmentId,a.AssignedRoleId,a.AssignedUserId,
                         s.StageName,t.CurrentStatus,s.StatusOnPending,s.StatusOnApprove,s.StatusOnReject
                  FROM dbo.Tender t WITH (UPDLOCK,HOLDLOCK)
                  INNER JOIN dbo.TenderWorkflowAssignments a WITH (UPDLOCK,HOLDLOCK)
                    ON a.TenderId=t.TenderId AND a.IsCurrent=1 AND a.DepartmentId=t.DepartmentId
                  INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                    AND s.WorkflowVersion=2 AND s.IsActive=1
                  INNER JOIN dbo.Users u ON u.UserId=@UserId AND u.IsActive=1
                    AND u.AccountType=N'Internal' AND u.RoleId=@RoleId
                  INNER JOIN dbo.Roles r ON r.RoleId=@RoleId AND r.IsActive=1
                  WHERE t.TenderId=@TenderId AND t.WorkflowVersion=2 AND t.IsDeleted=0
                    AND a.AssignedRoleId=@RoleId
                    AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                    AND
                    (
                        s.StageName=@StageName
                        OR (@StageName=N'Final_Cell_CompleteSecondaryVerification'
                            AND s.StageName=N'Final_Cell_ReverifyRejectedOpening')
                    )
                    AND ((@ShortfallId IS NULL AND a.ShortfallId IS NULL) OR a.ShortfallId=@ShortfallId)
                    AND EXISTS
                    (
                        SELECT 1 FROM dbo.RolePermissions rp
                        INNER JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId AND p.IsActive=1
                        WHERE rp.RoleId=@RoleId AND p.PermissionKey=@Permission
                    );", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@StageName", SqlDbType.NVarChar, stageName, 80);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt,
                    shortfallId.HasValue ? (object)shortfallId.Value : null);
                ModuleDb.AddParameter(command, "@Permission", SqlDbType.NVarChar, requiredPermission, 100);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The tender is not assigned to your role, user, or department for this action.");
                    return new StageAssignment
                    {
                        AssignmentId = Convert.ToInt64(reader["AssignmentId"], CultureInfo.InvariantCulture),
                        StageId = Convert.ToInt32(reader["StageId"], CultureInfo.InvariantCulture),
                        StageOrder = Convert.ToInt32(reader["StageOrder"], CultureInfo.InvariantCulture),
                        DepartmentId = Convert.ToInt32(reader["DepartmentId"], CultureInfo.InvariantCulture),
                        AssignedRoleId = Convert.ToInt32(reader["AssignedRoleId"], CultureInfo.InvariantCulture),
                        AssignedUserId = reader["AssignedUserId"] == DBNull.Value
                            ? (int?)null : Convert.ToInt32(reader["AssignedUserId"], CultureInfo.InvariantCulture),
                        StageName = Convert.ToString(reader["StageName"]),
                        CurrentStatus = Convert.ToString(reader["CurrentStatus"]),
                        StatusOnPending = Convert.ToString(reader["StatusOnPending"]),
                        StatusOnApprove = Convert.ToString(reader["StatusOnApprove"]),
                        StatusOnReject = Convert.ToString(reader["StatusOnReject"])
                    };
                }
            }
        }

        private static void CreateNextAssignment(SqlConnection connection, SqlTransaction transaction,
            int tenderId, StageAssignment current, string newStatus, string roleCode, int userId,
            WorkflowTransitionMetadata metadata)
        {
            if (current.StageName == "Final_JE_CompleteTender") return;

            string nextStageName = null;
            if (current.StageName == "Final_Cell_InitialTenderDocumentVerification" &&
                String.Equals(newStatus, "Pending JE Document Correction", StringComparison.Ordinal))
            {
                nextStageName = "Final_JE_CorrectTenderDocuments";
            }
            else if (current.StageName == "Final_JE_CorrectTenderDocuments" &&
                String.Equals(newStatus, "Pending Tender Cell Verification", StringComparison.Ordinal))
            {
                nextStageName = "Final_Cell_InitialTenderDocumentVerification";
            }
            else if (String.Equals(newStatus, "Tender Cell Verification Pending", StringComparison.Ordinal) &&
                current.StageName == "Final_Cell_ResumeVerification")
            {
                nextStageName = "Final_Cell_CompleteSecondaryVerification";
            }
            else if (current.StageName == "Final_Cell_CompleteSecondaryVerification" &&
                String.Equals(newStatus, "Shortfall Generation Pending", StringComparison.Ordinal))
            {
                nextStageName = "Final_Cell_CreateShortfall";
            }
            else if ((current.StageName == "Final_Cell_CreateShortfall" ||
                      current.StageName == "Final_JE_ApproveShortfall") &&
                String.Equals(newStatus, "Shortfall Generation Pending", StringComparison.Ordinal))
            {
                nextStageName = "Final_Cell_CreateShortfall";
            }
            else if (current.StageName == "Final_Cell_CreateShortfall" &&
                String.Equals(newStatus, "Tender Cell Verification Pending", StringComparison.Ordinal))
            {
                nextStageName = "Final_Cell_CompleteSecondaryVerification";
            }
            else if (current.StageName == "Final_Cell_ClearShortfall" &&
                String.Equals(newStatus, "Tender Cell Verification Pending", StringComparison.Ordinal))
            {
                nextStageName = "Final_Cell_CompleteSecondaryVerification";
            }
            else if (current.StageName == "Final_Cell_ReverifyRejectedOpening")
            {
                if (String.Equals(newStatus, "Financial Bid Opening Approval Pending", StringComparison.Ordinal))
                    nextStageName = "Final_Samiti_ApproveFinancialOpening";
                else if (String.Equals(newStatus, "Shortfall Generation Pending", StringComparison.Ordinal))
                    nextStageName = "Final_Cell_CreateShortfall";
                else
                    nextStageName = "Final_Cell_CompleteSecondaryVerification";
            }
            else if (current.StageName == "Final_JE_ResubmitFinancialApproval")
            {
                nextStageName = "Final_Auditor_ApproveFinancialBids";
            }
            else if (String.Equals(newStatus, "Awaiting Contractor Submission", StringComparison.Ordinal) &&
                current.StageName == "Final_Cell_ClearShortfall")
            {
                nextStageName = "Final_Contractor_SubmitShortfall";
            }
            else
            {
                using (SqlCommand command = new SqlCommand(
                    @"SELECT TOP (1) StageName,StageId,ApproverRoleId,StageOrder
                      FROM dbo.WorkflowStages
                      WHERE WorkflowVersion=2 AND IsActive=1 AND StatusOnPending=@NewStatus
                        AND StageOrder>@CurrentOrder
                      ORDER BY StageOrder;", connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@NewStatus", SqlDbType.NVarChar, newStatus, 50);
                    ModuleDb.AddParameter(command, "@CurrentOrder", SqlDbType.Int, current.StageOrder);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read()) nextStageName = Convert.ToString(reader["StageName"]);
                    }
                }
            }

            if (String.IsNullOrWhiteSpace(nextStageName))
            {
                if (String.Equals(newStatus, "Completed", StringComparison.Ordinal)) return;
                throw new InvalidOperationException("The next workflow stage is not configured.");
            }

            int stageId, assignedRoleId;
            string nextRoleCode;
            using (SqlCommand command = new SqlCommand(
                @"SELECT s.StageId,s.ApproverRoleId,r.RoleCode
                  FROM dbo.WorkflowStages s
                  INNER JOIN dbo.Roles r ON r.RoleId=s.ApproverRoleId AND r.IsActive=1
                  WHERE s.WorkflowVersion=2 AND s.IsActive=1 AND s.StageName=@StageName;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@StageName", SqlDbType.NVarChar, nextStageName, 80);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The next workflow role is not configured.");
                    stageId = Convert.ToInt32(reader["StageId"], CultureInfo.InvariantCulture);
                    assignedRoleId = Convert.ToInt32(reader["ApproverRoleId"], CultureInfo.InvariantCulture);
                    nextRoleCode = Convert.ToString(reader["RoleCode"]);
                }
            }

            int? assignedUserId = metadata.SetNextAssignedUserId
                ? metadata.NextAssignedUserId
                : String.Equals(nextRoleCode, roleCode, StringComparison.OrdinalIgnoreCase)
                    ? (int?)userId : null;
            long? nextShortfallId = metadata.SetNextShortfallId
                ? metadata.NextShortfallId : current.StageName.IndexOf("Shortfall", StringComparison.OrdinalIgnoreCase) >= 0
                    ? GetCurrentShortfallId(connection, transaction, current.AssignmentId) : (long?)null;

            if (nextStageName == "Final_Cell_CompleteSecondaryVerification" ||
                nextStageName == "Final_Samiti_ApproveFinancialOpening" ||
                nextStageName == "Final_JE_OpenFinancialBids" ||
                nextStageName == "Final_Auditor_ApproveFinancialBids")
                nextShortfallId = null;

            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderWorkflowAssignments
                    (TenderId,ShortfallId,StageId,AssignedRoleId,AssignedUserId,DepartmentId,IsCurrent)
                  VALUES(@TenderId,@ShortfallId,@StageId,@RoleId,@AssignedUserId,@DepartmentId,1);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ShortfallId", SqlDbType.BigInt,
                    nextShortfallId.HasValue ? (object)nextShortfallId.Value : null);
                ModuleDb.AddParameter(command, "@StageId", SqlDbType.Int, stageId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, assignedRoleId);
                ModuleDb.AddParameter(command, "@AssignedUserId", SqlDbType.Int,
                    assignedUserId.HasValue ? (object)assignedUserId.Value : null);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int, current.DepartmentId);
                command.ExecuteNonQuery();
            }

            NotifyWorkflowRole(connection, transaction, tenderId, assignedRoleId,
                current.DepartmentId, nextStageName);
        }

        private static long? GetCurrentShortfallId(SqlConnection connection,
            SqlTransaction transaction, long assignmentId)
        {
            using (SqlCommand command = new SqlCommand(
                "SELECT ShortfallId FROM dbo.TenderWorkflowAssignments WHERE AssignmentId=@AssignmentId;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@AssignmentId", SqlDbType.BigInt, assignmentId);
                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value
                    ? (long?)null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
        }

        public static bool IsJuniorEngineer(string roleCode)
        {
            return String.Equals(roleCode, "JE", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(roleCode, "Junior Engineer (JE)", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTenderCell(string roleCode)
        {
            return String.Equals(roleCode, "TENDER_CELL", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(roleCode, "Tender Cell", StringComparison.OrdinalIgnoreCase);
        }
    }
}
