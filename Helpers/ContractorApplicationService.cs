using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;

namespace DepartmentModule.Helpers
{
    public static class ContractorApplicationService
    {
        private sealed class ApplicationRecord
        {
            public int ApplicationId;
            public int TenderId;
            public int ContractorId;
            public string BidStatus;
            public DateTime SubmittedDate;
        }

        public static int SaveDraft(Page page, int tenderId, int userId, int roleId,
            string documentType, IList<HttpPostedFile> files, string ipAddress)
        {
            if (page == null) throw new ArgumentNullException("page");
            if (files == null) files = new List<HttpPostedFile>();
            ValidateRequestSize(files);
            if (files.Count > 0 && String.IsNullOrWhiteSpace(documentType))
                throw new InvalidOperationException("Select the document requirement for the uploaded files.");

            List<string> storedPaths = new List<string>();
            bool committed = false;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    int contractorId = GetActiveContractorId(connection, transaction, userId);
                    int? existingId = ContractorApplicationRules.GetApplicationId(
                        connection, transaction, tenderId, contractorId);
                    int applicationId;
                    string previousStatus;
                    if (existingId.HasValue)
                    {
                        applicationId = existingId.Value;
                        ApplicationRecord record = GetApplication(connection, transaction, applicationId, null, true);
                        previousStatus = ContractorApplicationRules.GetStatus(connection, transaction, applicationId);
                        if (previousStatus == "Draft") EnsureWindowOpen(connection, transaction, tenderId);
                        if (previousStatus != "Draft" && previousStatus != "Shortfall")
                            throw new InvalidOperationException("Only a draft or application with an open clarification can be updated.");
                        if (record.ContractorId != contractorId)
                            throw new InvalidOperationException("This application does not belong to the signed-in contractor.");
                    }
                    else
                    {
                        EnsureWindowOpen(connection, transaction, tenderId);
                        using (SqlCommand command = new SqlCommand(
                            @"INSERT INTO dbo.TechnicalBid(TenderId,ContractorId,Status,SubmittedDate)
                              OUTPUT INSERTED.TechnicalBidId
                              VALUES(@TenderId,@ContractorId,N'Draft',SYSUTCDATETIME());",
                            connection, transaction))
                        {
                            ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                            ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                            applicationId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
                        }
                        previousStatus = String.Empty;
                        ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                            "DraftCreated", null, "Draft", null, userId, roleId, false);
                    }

                    DataTable requirements = ContractorApplicationRules.GetRequirements(connection, transaction, tenderId);
                    DataRow requirement = files.Count == 0 ? null : FindRequirement(requirements, documentType);
                    int maxFileSizeBytes = requirement == null ? 0 :
                        Convert.ToInt32(requirement["MaxFileSizeBytes"], CultureInfo.InvariantCulture);
                    string allowedExtensions = requirement == null ? null :
                        Convert.ToString(requirement["AllowedExtensions"]);

                    if (files.Count > 0)
                    {
                        foreach (HttpPostedFile file in files)
                        {
                            string relativePath = TenderLifecycleFiles.SaveTenderApplicationUpload(
                                page, file, tenderId, contractorId, maxFileSizeBytes, allowedExtensions);
                            storedPaths.Add(relativePath);
                            InsertDocument(connection, transaction, applicationId, documentType,
                                TenderLifecycleFiles.SafeFileName(file.FileName), relativePath, userId);
                        }
                        ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                            "DocumentsUploaded", previousStatus, previousStatus.Length == 0 ? "Draft" : previousStatus,
                            null, userId, roleId, previousStatus == "Shortfall");
                    }

                    ModuleDb.AddAudit(connection, transaction, userId,
                        existingId.HasValue ? "UpdateApplicationDraft" : "CreateApplicationDraft",
                        "TechnicalBid", applicationId, tenderId.ToString(CultureInfo.InvariantCulture), ipAddress);
                    transaction.Commit();
                    committed = true;
                    return applicationId;
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627)
                    throw new InvalidOperationException("An application already exists for this contractor and tender.", ex);
                throw;
            }
            finally
            {
                if (!committed) DeleteStoredFiles(page, storedPaths);
            }
        }

        public static void AddDocuments(Page page, int applicationId, int userId, int roleId,
            string documentType, IList<HttpPostedFile> files, string ipAddress)
        {
            if (page == null) throw new ArgumentNullException("page");
            if (files == null || files.Count == 0)
                throw new InvalidOperationException("Select at least one document to upload.");
            ValidateRequestSize(files);
            if (String.IsNullOrWhiteSpace(documentType))
                throw new InvalidOperationException("Select the document requirement for the uploaded files.");

            List<string> storedPaths = new List<string>();
            bool committed = false;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    int contractorId = GetActiveContractorId(connection, transaction, userId);
                    ApplicationRecord record = GetApplication(connection, transaction, applicationId, userId, true);
                    string previousStatus = ContractorApplicationRules.GetStatus(connection, transaction, applicationId);
                    if (previousStatus == "Draft") EnsureWindowOpen(connection, transaction, record.TenderId);
                    if (previousStatus != "Draft" && previousStatus != "Shortfall")
                        throw new InvalidOperationException("Documents may only be added to a draft or an application with an open clarification.");
                    if (record.ContractorId != contractorId)
                        throw new InvalidOperationException("This application does not belong to the signed-in contractor.");

                    DataTable requirements = ContractorApplicationRules.GetRequirements(connection, transaction, record.TenderId);
                    DataRow requirement = FindRequirement(requirements, documentType);
                    int maxFileSizeBytes = Convert.ToInt32(requirement["MaxFileSizeBytes"], CultureInfo.InvariantCulture);
                    string allowedExtensions = Convert.ToString(requirement["AllowedExtensions"]);
                    foreach (HttpPostedFile file in files)
                    {
                        string relativePath = TenderLifecycleFiles.SaveTenderApplicationUpload(
                            page, file, record.TenderId, contractorId, maxFileSizeBytes, allowedExtensions);
                        storedPaths.Add(relativePath);
                        InsertDocument(connection, transaction, applicationId, documentType,
                            TenderLifecycleFiles.SafeFileName(file.FileName), relativePath, userId);
                    }
                    ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                        "DocumentsUploaded", previousStatus, previousStatus, null, userId, roleId,
                        previousStatus == "Shortfall");
                    ModuleDb.AddAudit(connection, transaction, userId, "UploadApplicationDocuments",
                        "TechnicalBid", applicationId, null, ipAddress);
                    transaction.Commit();
                    committed = true;
                }
            }
            finally
            {
                if (!committed) DeleteStoredFiles(page, storedPaths);
            }
        }

        public static void Submit(int applicationId, int userId, int roleId,
            bool termsAccepted, string termsVersion, string ipAddress)
        {
            if (!termsAccepted)
                throw new InvalidOperationException("Accept the Terms & Conditions before submitting.");
            if (!String.Equals(termsVersion, ContractorApplicationRules.TermsVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("The Terms & Conditions have changed. Review them and submit again.");

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                GetActiveContractorId(connection, transaction, userId);
                ApplicationRecord record = GetApplication(connection, transaction, applicationId, userId, true);
                string previousStatus = ContractorApplicationRules.GetStatus(connection, transaction, applicationId);
                if (previousStatus == "Draft") EnsureWindowOpen(connection, transaction, record.TenderId);
                if (previousStatus != "Draft" && previousStatus != "Shortfall")
                    throw new InvalidOperationException("Only a draft or application with an open clarification can be submitted.");

                using (SqlCommand command = new SqlCommand(
                    @"IF EXISTS
                      (
                          SELECT 1 FROM dbo.TenderRequiredBidDocuments r
                          WHERE r.TenderId=@TenderId AND r.IsMandatory=1
                            AND NOT EXISTS
                            (
                                SELECT 1 FROM dbo.TechnicalBidDocuments d
                                WHERE d.TechnicalBidId=@ApplicationId AND d.DocumentType=r.DocumentType
                            )
                      )
                          THROW 51302,'One or more mandatory application documents are missing.',1;
                      UPDATE dbo.TechnicalBid
                      SET Status=N'Submitted',SubmittedDate=SYSUTCDATETIME(),
                          TermsAcceptedAt=SYSUTCDATETIME(),TermsVersion=@TermsVersion
                      WHERE TechnicalBidId=@ApplicationId;",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, record.TenderId);
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                    ModuleDb.AddParameter(command, "@TermsVersion", SqlDbType.NVarChar,
                        ContractorApplicationRules.TermsVersion, 50);
                    command.ExecuteNonQuery();
                }

                ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                    "Submitted", previousStatus, "Submitted", null, userId, roleId, true);
                NotifyAssignedJuniorEngineers(connection, transaction, record.TenderId, applicationId);
                ModuleDb.AddAudit(connection, transaction, userId, "SubmitTenderApplication",
                    "TechnicalBid", applicationId, null, ipAddress);
                transaction.Commit();
            }
        }

        public static void AddRemark(int applicationId, int userId, int roleId, string roleCode,
            int? departmentId, bool contractor, string remarkType, string message, string ipAddress)
        {
            string text = (message ?? String.Empty).Trim();
            if (text.Length == 0 || text.Length > 2000)
                throw new InvalidOperationException("Remark must contain 1 to 2,000 characters.");

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                ApplicationRecord record = GetApplication(connection, transaction, applicationId,
                    contractor ? (int?)userId : null, true);
                string previousStatus = ContractorApplicationRules.GetStatus(connection, transaction, applicationId);
                bool visibleToContractor;
                string storedType;
                string newStatus = previousStatus;
                if (contractor)
                {
                    GetActiveContractorId(connection, transaction, userId);
                    if (record.ContractorId <= 0 ||
                        !IsApplicationOwnedBy(connection, transaction, applicationId, userId))
                        throw new InvalidOperationException("This application is not available to the signed-in contractor.");
                    if (!String.Equals(remarkType, "Response", StringComparison.OrdinalIgnoreCase) ||
                        previousStatus != "Shortfall")
                        throw new InvalidOperationException("Contractors may respond only to an open clarification.");
                    storedType = "Response";
                    visibleToContractor = true;
                }
                else
                {
                    if (previousStatus == "Qualified" || previousStatus == "Disqualified")
                        throw new InvalidOperationException("The application has a final decision and can no longer be changed.");
                    bool isJe = IsJuniorEngineer(roleCode);
                    bool isTenderCell = IsTenderCell(roleCode);
                    if ((!isJe && !isTenderCell) ||
                        !HasCurrentAssignment(connection, transaction, record.TenderId, userId, roleId,
                            departmentId, isJe))
                        throw new InvalidOperationException("The application is not assigned to your role, user, or department.");
                    if (remarkType != "Internal" && remarkType != "Clarification")
                        throw new InvalidOperationException("Remark type must be Internal or Clarification.");
                    storedType = remarkType;
                    visibleToContractor = storedType == "Clarification";
                    if (visibleToContractor) newStatus = "Shortfall";
                }

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.ContractorApplicationRemarks
                        (TechnicalBidId,RemarkType,Message,CreatedBy,RoleId,VisibleToContractor)
                      VALUES(@ApplicationId,@RemarkType,@Message,@UserId,@RoleId,@Visible);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                    ModuleDb.AddParameter(command, "@RemarkType", SqlDbType.NVarChar, storedType, 30);
                    ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar, text, 2000);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId > 0 ? (object)roleId : null);
                    ModuleDb.AddParameter(command, "@Visible", SqlDbType.Bit, visibleToContractor);
                    command.ExecuteNonQuery();
                }
                ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                    storedType, previousStatus, newStatus, text, userId, roleId, visibleToContractor);

                if (contractor)
                {
                    NotifyAssignedReviewers(connection, transaction, record.TenderId, applicationId,
                        "ApplicationResponse", "A contractor responded to a clarification.");
                }
                else if (visibleToContractor)
                {
                    NotifyContractor(connection, transaction, record.TenderId, record.ContractorId,
                        "ApplicationClarification", "A clarification is required for application " +
                        ContractorApplicationRules.GetReference(applicationId, record.SubmittedDate) + ".");
                }

                ModuleDb.AddAudit(connection, transaction, userId, "AddApplicationRemark",
                    "ContractorApplicationRemarks", applicationId, storedType, ipAddress);
                transaction.Commit();
            }
        }

        public static void ReviewDocument(int applicationId, int documentId, int userId, int roleId,
            string roleCode, int? departmentId, string decision, string remarks, string ipAddress)
        {
            if (!IsTenderCell(roleCode))
                throw new InvalidOperationException("Only the assigned Tender Cell can verify application documents.");
            string normalizedDecision = (decision ?? String.Empty).Trim();
            if (normalizedDecision != "Verified" && normalizedDecision != "Shortfall" &&
                normalizedDecision != "Rejected")
                throw new InvalidOperationException("Document status must be Verified, Shortfall, or Rejected.");
            string text = (remarks ?? String.Empty).Trim();
            if (text.Length > 2000 || (normalizedDecision != "Verified" && text.Length == 0))
                throw new InvalidOperationException("Provide remarks of at most 2,000 characters for a shortfall or rejection.");

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                ApplicationRecord record = GetApplication(connection, transaction, applicationId, null, true);
                if (!HasCurrentAssignment(connection, transaction, record.TenderId, userId, roleId,
                    departmentId, false))
                    throw new InvalidOperationException("The application is not assigned to your Tender Cell account.");

                string previousStatus = ContractorApplicationRules.GetStatus(connection, transaction, applicationId);
                if (previousStatus == "Draft" || previousStatus == "Qualified" || previousStatus == "Disqualified")
                    throw new InvalidOperationException("This application is not awaiting document verification.");

                string fileName, filePath, documentType;
                using (SqlCommand command = new SqlCommand(
                    @"SELECT FileName,FilePath,DocumentType
                      FROM dbo.TechnicalBidDocuments
                      WHERE TechnicalBidDocumentId=@DocumentId AND TechnicalBidId=@ApplicationId;",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                    ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("The selected document does not belong to this application.");
                        fileName = Convert.ToString(reader["FileName"]);
                        filePath = Convert.ToString(reader["FilePath"]);
                        documentType = Convert.ToString(reader["DocumentType"]);
                    }
                }

                using (SqlCommand command = new SqlCommand(
                    @"INSERT INTO dbo.TenderDocumentVerificationHistory
                        (TenderId,ContractorId,TechnicalBidDocumentId,FileName,FilePath,DocumentType,
                         ReviewStage,VerificationStatus,ReviewerId,ReviewerRoleId,Remarks)
                      VALUES(@TenderId,@ContractorId,@DocumentId,@FileName,@FilePath,@DocumentType,
                             N'Secondary',@VerificationStatus,@UserId,@RoleId,@Remarks);",
                    connection, transaction))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, record.TenderId);
                    ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, record.ContractorId);
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                    ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 520);
                    ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, filePath, 800);
                    ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar, documentType, 160);
                    ModuleDb.AddParameter(command, "@VerificationStatus", SqlDbType.NVarChar,
                        normalizedDecision == "Verified" ? "Verified" : "Not Verified", 20);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                    ModuleDb.AddParameter(command, "@Remarks", SqlDbType.NVarChar,
                        String.IsNullOrWhiteSpace(text) ? null : text, 2000);
                    command.ExecuteNonQuery();
                }

                string newStatus = normalizedDecision == "Shortfall" ? "Shortfall" :
                    normalizedDecision == "Rejected" ? "Disqualified" :
                    previousStatus == "Shortfall" &&
                    !HasUnverifiedMandatoryDocuments(connection, transaction, applicationId, record.TenderId)
                        ? "Submitted" : "UnderVerification";
                ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                    "Document" + normalizedDecision, previousStatus, newStatus,
                    String.IsNullOrWhiteSpace(text) ? null : text, userId, roleId, true);
                if (normalizedDecision == "Shortfall" || normalizedDecision == "Rejected")
                {
                    AddRemarkRow(connection, transaction, applicationId,
                        normalizedDecision == "Shortfall" ? "Clarification" : "Decision",
                        text, userId, roleId, true);
                    NotifyContractor(connection, transaction, record.TenderId, record.ContractorId,
                        normalizedDecision == "Shortfall" ? "ApplicationClarification" : "ApplicationDecision",
                        normalizedDecision == "Shortfall"
                            ? "A document clarification is required for application " +
                              ContractorApplicationRules.GetReference(applicationId, record.SubmittedDate) + "."
                            : "A document was rejected for application " +
                              ContractorApplicationRules.GetReference(applicationId, record.SubmittedDate) + ".");
                }
                ModuleDb.AddAudit(connection, transaction, userId, "VerifyApplicationDocument",
                    "TechnicalBidDocuments", documentId, normalizedDecision, ipAddress);
                transaction.Commit();
            }
        }

        public static void ReviewApplication(int applicationId, int userId, int roleId,
            string roleCode, int? departmentId, string decision, string remarks, string ipAddress)
        {
            bool isJe = IsJuniorEngineer(roleCode);
            bool isTenderCell = IsTenderCell(roleCode);
            if (!isJe && !isTenderCell)
                throw new InvalidOperationException("Only an assigned Junior Engineer or Tender Cell reviewer can decide an application.");
            string normalizedDecision = (decision ?? String.Empty).Trim();
            if (normalizedDecision != "Qualified" && normalizedDecision != "Disqualified")
                throw new InvalidOperationException("Decision must be Qualified or Disqualified.");
            string text = (remarks ?? String.Empty).Trim();
            if (text.Length > 2000 || (normalizedDecision == "Disqualified" && text.Length == 0))
                throw new InvalidOperationException("Provide a disqualification reason of at most 2,000 characters.");

            using (SqlConnection connection = ModuleDb.OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                ApplicationRecord record = GetApplication(connection, transaction, applicationId, null, true);
                if (!HasCurrentAssignment(connection, transaction, record.TenderId, userId, roleId,
                    departmentId, isJe))
                    throw new InvalidOperationException("The application is not assigned to your role, user, or department.");
                string previousStatus = ContractorApplicationRules.GetStatus(connection, transaction, applicationId);
                if (previousStatus != "Submitted" && previousStatus != "UnderVerification" &&
                    previousStatus != "Shortfall")
                    throw new InvalidOperationException("This application is not awaiting a technical decision.");

                if (normalizedDecision == "Qualified" && HasUnverifiedMandatoryDocuments(connection, transaction, applicationId, record.TenderId))
                    throw new InvalidOperationException("Verify every mandatory document before qualifying this application.");

                ContractorApplicationRules.AddHistory(connection, transaction, applicationId,
                    "TechnicalReview", previousStatus, normalizedDecision,
                    String.IsNullOrWhiteSpace(text) ? null : text, userId, roleId, true);
                if (text.Length > 0)
                    AddRemarkRow(connection, transaction, applicationId, "Decision", text, userId, roleId, true);
                NotifyContractor(connection, transaction, record.TenderId, record.ContractorId,
                    "ApplicationDecision", "Application " +
                    ContractorApplicationRules.GetReference(applicationId, record.SubmittedDate) +
                    " was " + normalizedDecision.ToLowerInvariant() + ".");
                ModuleDb.AddAudit(connection, transaction, userId, "ReviewContractorApplication",
                    "TechnicalBid", applicationId, normalizedDecision, ipAddress);
                transaction.Commit();
            }
        }

        public static bool CanInternalUserView(SqlConnection connection, int applicationId,
            int userId, int roleId, string roleCode, int? departmentId)
        {
            bool isJe = IsJuniorEngineer(roleCode);
            bool isTenderCell = IsTenderCell(roleCode);
            if (!isJe && !isTenderCell) return false;
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.IsDeleted=0
                  WHERE b.TechnicalBidId=@ApplicationId
                    AND ((@IsJE=1 AND @DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId)
                         OR (@IsCell=1 AND EXISTS
                         (
                             SELECT 1 FROM dbo.TenderWorkflowAssignments a
                             WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                               AND a.AssignedRoleId=@RoleId
                               AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                               AND a.DepartmentId=t.DepartmentId
                         )));", connection))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                ModuleDb.AddParameter(command, "@IsJE", SqlDbType.Bit, isJe);
                ModuleDb.AddParameter(command, "@IsCell", SqlDbType.Bit, isTenderCell);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    departmentId.HasValue ? (object)departmentId.Value : null);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        public static bool HasCurrentAssignment(SqlConnection connection, int applicationId,
            int userId, int roleId, string roleCode, int? departmentId)
        {
            ApplicationRecord record = GetApplication(connection, null, applicationId, null, false);
            return HasCurrentAssignment(connection, null, record.TenderId, userId, roleId, departmentId,
                IsJuniorEngineer(roleCode));
        }

        private static bool HasUnverifiedMandatoryDocuments(SqlConnection connection,
            SqlTransaction transaction, int applicationId, int tenderId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TenderRequiredBidDocuments r
                  WHERE r.TenderId=@TenderId AND r.IsMandatory=1
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM
                        (
                            SELECT TOP (1) d.TechnicalBidDocumentId
                            FROM dbo.TechnicalBidDocuments d
                            WHERE d.TechnicalBidId=@ApplicationId AND d.DocumentType=r.DocumentType
                            ORDER BY d.UploadedDate DESC,d.TechnicalBidDocumentId DESC
                        ) latest
                        INNER JOIN
                        (
                            SELECT h.TechnicalBidDocumentId,h.VerificationStatus,
                                   ROW_NUMBER() OVER
                                   (PARTITION BY h.TechnicalBidDocumentId ORDER BY h.ReviewedAtUtc DESC,
                                    h.DocumentVerificationHistoryId DESC) AS RowNumber
                            FROM dbo.TenderDocumentVerificationHistory h
                            WHERE h.ReviewStage=N'Secondary'
                        ) review ON review.TechnicalBidDocumentId=latest.TechnicalBidDocumentId
                            AND review.RowNumber=1 AND review.VerificationStatus=N'Verified'
                    );", connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
            }
        }

        private static void EnsureWindowOpen(SqlConnection connection, SqlTransaction transaction, int tenderId)
        {
            if (!ContractorApplicationRules.IsSubmissionWindowOpen(connection, transaction, tenderId))
                throw new InvalidOperationException("Applications are available only during the approved publication period.");
        }

        private static int GetActiveContractorId(SqlConnection connection, SqlTransaction transaction, int userId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT c.ContractorId
                  FROM dbo.Contractors c
                  INNER JOIN dbo.Users u ON u.UserId=c.UserId
                  WHERE c.UserId=@UserId AND c.IsActive=1 AND u.IsActive=1
                    AND u.AccountType=N'ExternalBidder' AND u.RoleId IS NULL;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                    throw new InvalidOperationException("An active contractor profile linked to this account is required.");
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
        }

        private static ApplicationRecord GetApplication(SqlConnection connection, SqlTransaction transaction,
            int applicationId, int? ownerUserId, bool lockRows)
        {
            string lockHint = lockRows ? " WITH (UPDLOCK,HOLDLOCK)" : String.Empty;
            using (SqlCommand command = new SqlCommand(
                @"SELECT b.TechnicalBidId,b.TenderId,b.ContractorId,b.Status,b.SubmittedDate
                  FROM dbo.TechnicalBid b" + lockHint + @"
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.IsDeleted=0
                  WHERE b.TechnicalBidId=@ApplicationId
                    AND (@OwnerUserId IS NULL OR c.UserId=@OwnerUserId);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                ModuleDb.AddParameter(command, "@OwnerUserId", SqlDbType.Int,
                    ownerUserId.HasValue ? (object)ownerUserId.Value : null);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("The application was not found or is not available to this account.");
                    return new ApplicationRecord
                    {
                        ApplicationId = Convert.ToInt32(reader["TechnicalBidId"], CultureInfo.InvariantCulture),
                        TenderId = Convert.ToInt32(reader["TenderId"], CultureInfo.InvariantCulture),
                        ContractorId = Convert.ToInt32(reader["ContractorId"], CultureInfo.InvariantCulture),
                        BidStatus = Convert.ToString(reader["Status"]),
                        SubmittedDate = Convert.ToDateTime(reader["SubmittedDate"], CultureInfo.InvariantCulture)
                    };
                }
            }
        }

        private static bool IsApplicationOwnedBy(SqlConnection connection, SqlTransaction transaction,
            int applicationId, int userId)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TechnicalBid b
                  INNER JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                  WHERE b.TechnicalBidId=@ApplicationId AND c.UserId=@UserId AND c.IsActive=1;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        private static bool HasCurrentAssignment(SqlConnection connection, SqlTransaction transaction,
            int tenderId, int userId, int roleId, int? departmentId, bool isJe)
        {
            using (SqlCommand command = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.TenderWorkflowAssignments a
                  INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.IsDeleted=0
                  WHERE a.TenderId=@TenderId AND a.IsCurrent=1 AND a.AssignedRoleId=@RoleId
                    AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                    AND a.DepartmentId=t.DepartmentId
                    AND (@IsJE=0 OR (@DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId));",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                    departmentId.HasValue ? (object)departmentId.Value : null);
                ModuleDb.AddParameter(command, "@IsJE", SqlDbType.Bit, isJe);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
            }
        }

        private static bool IsJuniorEngineer(string roleCode)
        {
            return String.Equals(roleCode, "Junior Engineer (JE)", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(roleCode, "JE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTenderCell(string roleCode)
        {
            return String.Equals(roleCode, "TENDER_CELL", StringComparison.OrdinalIgnoreCase);
        }

        private static DataRow FindRequirement(DataTable requirements, string documentType)
        {
            foreach (DataRow row in requirements.Rows)
            {
                if (String.Equals(Convert.ToString(row["DocumentType"]), documentType.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                    return row;
            }
            throw new InvalidOperationException("Select a document requirement configured for this tender.");
        }

        private static void InsertDocument(SqlConnection connection, SqlTransaction transaction,
            int applicationId, string documentType, string fileName, string filePath, int userId)
        {
            if (String.IsNullOrWhiteSpace(fileName) || fileName.Length > 260)
                throw new InvalidOperationException("The uploaded file name is invalid or too long.");
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TechnicalBidDocuments
                    (TechnicalBidId,FileName,FilePath,DocumentType,UploadedBy)
                  VALUES(@ApplicationId,@FileName,@FilePath,@DocumentType,@UserId);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                ModuleDb.AddParameter(command, "@FileName", SqlDbType.NVarChar, fileName, 260);
                ModuleDb.AddParameter(command, "@FilePath", SqlDbType.NVarChar, filePath, 400);
                ModuleDb.AddParameter(command, "@DocumentType", SqlDbType.NVarChar, documentType.Trim(), 160);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                command.ExecuteNonQuery();
            }
        }

        private static void AddRemarkRow(SqlConnection connection, SqlTransaction transaction,
            int applicationId, string remarkType, string message, int userId, int roleId, bool visible)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.ContractorApplicationRemarks
                    (TechnicalBidId,RemarkType,Message,CreatedBy,RoleId,VisibleToContractor)
                  VALUES(@ApplicationId,@RemarkType,@Message,@UserId,@RoleId,@Visible);",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@ApplicationId", SqlDbType.Int, applicationId);
                ModuleDb.AddParameter(command, "@RemarkType", SqlDbType.NVarChar, remarkType, 30);
                ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar, message, 2000);
                ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, userId);
                ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, roleId > 0 ? (object)roleId : null);
                ModuleDb.AddParameter(command, "@Visible", SqlDbType.Bit, visible);
                command.ExecuteNonQuery();
            }
        }

        private static void NotifyAssignedJuniorEngineers(SqlConnection connection,
            SqlTransaction transaction, int tenderId, int applicationId)
        {
            string reference = ContractorApplicationRules.GetReference(applicationId, DateTime.UtcNow);
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,NotificationType,Message)
                  SELECT DISTINCT u.UserId,@TenderId,N'ApplicationSubmitted',@Message
                  FROM dbo.TenderWorkflowAssignments a
                  INNER JOIN dbo.Roles r ON r.RoleId=a.AssignedRoleId
                  INNER JOIN dbo.Users u ON u.RoleId=r.RoleId AND u.IsActive=1 AND u.AccountType=N'Internal'
                  WHERE a.TenderId=@TenderId AND a.IsCurrent=1
                    AND (r.RoleCode IN (N'Junior Engineer (JE)',N'JE') OR r.RoleName=N'Junior Engineer (JE)')
                    AND (a.AssignedUserId IS NULL OR a.AssignedUserId=u.UserId)
                    AND u.DepartmentId=a.DepartmentId;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar,
                    "A contractor submitted application " + reference + ".", 1000);
                command.ExecuteNonQuery();
            }
        }

        private static void NotifyAssignedReviewers(SqlConnection connection, SqlTransaction transaction,
            int tenderId, int applicationId, string type, string message)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,NotificationType,Message)
                  SELECT DISTINCT u.UserId,@TenderId,@Type,@Message
                  FROM dbo.TenderWorkflowAssignments a
                  INNER JOIN dbo.Users u ON u.RoleId=a.AssignedRoleId AND u.IsActive=1
                    AND (a.AssignedUserId IS NULL OR a.AssignedUserId=u.UserId)
                  WHERE a.TenderId=@TenderId AND a.IsCurrent=1
                    AND u.DepartmentId=a.DepartmentId
                    AND a.AssignedRoleId IN
                    (
                        SELECT RoleId FROM dbo.Roles
                        WHERE RoleCode IN (N'Junior Engineer (JE)',N'JE',N'TENDER_CELL')
                           OR RoleName IN (N'Junior Engineer (JE)',N'Tender Cell')
                    );",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@Type", SqlDbType.NVarChar, type, 60);
                ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar, message, 1000);
                command.ExecuteNonQuery();
            }
        }

        private static void NotifyContractor(SqlConnection connection, SqlTransaction transaction,
            int tenderId, int contractorId, string type, string message)
        {
            using (SqlCommand command = new SqlCommand(
                @"INSERT INTO dbo.TenderNotifications(UserId,TenderId,NotificationType,Message)
                  SELECT c.UserId,@TenderId,@Type,@Message
                  FROM dbo.Contractors c
                  WHERE c.ContractorId=@ContractorId AND c.IsActive=1 AND c.UserId IS NOT NULL;",
                connection, transaction))
            {
                ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                ModuleDb.AddParameter(command, "@ContractorId", SqlDbType.Int, contractorId);
                ModuleDb.AddParameter(command, "@Type", SqlDbType.NVarChar, type, 60);
                ModuleDb.AddParameter(command, "@Message", SqlDbType.NVarChar, message, 1000);
                command.ExecuteNonQuery();
            }
        }

        private static void ValidateRequestSize(IList<HttpPostedFile> files)
        {
            if (files.Count > ContractorApplicationRules.MaximumFilesPerRequest)
                throw new InvalidOperationException("Upload no more than 10 documents per request.");
            long totalBytes = 0;
            foreach (HttpPostedFile file in files)
            {
                if (file == null || file.ContentLength <= 0)
                    throw new InvalidOperationException("Each uploaded document must be non-empty.");
                totalBytes += file.ContentLength;
                if (file.ContentLength > ContractorApplicationRules.MaximumFileSizeBytes)
                    throw new InvalidOperationException("Each document must be no larger than 10 MB.");
            }
            if (totalBytes > ContractorApplicationRules.MaximumRequestBytes)
                throw new InvalidOperationException("The total upload must not exceed 50 MB.");
        }

        private static void DeleteStoredFiles(Page page, IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    TenderLifecycleFiles.DeleteStoredApplicationUpload(page, path);
                }
                catch (IOException ex)
                {
                    page.Trace.Warn("DepartmentModule", "A temporary application upload could not be removed.", ex);
                }
            }
        }
    }
}
