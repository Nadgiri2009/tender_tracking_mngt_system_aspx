using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using DepartmentModule.Helpers;

namespace DepartmentModule.Pages
{
    public partial class DocumentDownload : ModulePage
    {
        protected override bool RequiresAuthentication
        {
            get { return false; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            int extensionRequestId = QueryId("extensionRequestId");
            if (extensionRequestId > 0)
            {
                string kind = Convert.ToString(Request.QueryString["kind"]);
                if (kind != "application" && kind != "supporting") { NotFound(); return; }
                DownloadExtensionDocument(extensionRequestId, kind);
                return;
            }

            int evaluationDocumentId = QueryId("evaluationDocumentId");
            if (evaluationDocumentId > 0)
            {
                DownloadEvaluationDocument(evaluationDocumentId);
                return;
            }

            int gisTenderId = QueryId("gisId");
            if (gisTenderId > 0)
            {
                DownloadGisImage(gisTenderId);
                return;
            }

            long shortfallSubmissionId = QueryLongId("shortfallSubmissionId");
            if (shortfallSubmissionId > 0)
            {
                DownloadShortfallSubmission(shortfallSubmissionId);
                return;
            }

            long generatedDocumentId = QueryLongId("generatedDocumentId");
            if (generatedDocumentId > 0)
            {
                DownloadGeneratedDocument(generatedDocumentId);
                return;
            }

            int technicalBidDocumentId = QueryId("technicalBidDocumentId");
            if (technicalBidDocumentId > 0)
            {
                DownloadTechnicalBidChildDocument(technicalBidDocumentId);
                return;
            }

            int technicalBidId = QueryId("technicalBidId");
            if (technicalBidId > 0)
            {
                DownloadTechnicalBidDocument(technicalBidId);
                return;
            }

            int documentId = QueryId("id");
            if (documentId <= 0) { NotFound(); return; }
            string storedPath;
            string fileName;
            bool isContractor = String.Equals(CurrentAccountType, "ExternalBidder", StringComparison.OrdinalIgnoreCase);
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT td.FilePath,td.FileName
                      FROM dbo.TenderDocuments td
                      INNER JOIN dbo.Tender t ON t.TenderId=td.TenderId
                      WHERE td.DocumentId=@DocumentId AND t.IsDeleted=0
                        AND (
                            ((@CurrentUserId=0 OR @IsContractor=1) AND
                                (EXISTS(SELECT 1 FROM dbo.ApprovalHistory publicHistory
                                        WHERE publicHistory.TenderId=t.TenderId AND publicHistory.NewStatus=N'Tender Published')
                                 OR EXISTS(SELECT 1 FROM dbo.TenderWorkflowHistory publicWorkflowHistory
                                           WHERE publicWorkflowHistory.TenderId=t.TenderId
                                             AND publicWorkflowHistory.WorkflowVersion=2
                                             AND publicWorkflowHistory.NewStatus=N'Tender Published')))
                            OR (@CurrentUserId>0 AND @IsContractor=0
                                AND (@CurrentRole<>N'DepartmentOfficer' OR t.CreatedBy=@CurrentUserId))
                        )
                        AND (@IsAdministrator=1 OR @CurrentUserId=0 OR @IsContractor=1
                             OR NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                           WHERE scopeUser.UserId=@CurrentUserId
                                             AND scopeUser.DepartmentId IS NOT NULL
                                             AND scopeUser.DepartmentId<>t.DepartmentId));", connection))
                {
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                    ModuleDb.AddParameter(command, "@CurrentUserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@CurrentRole", SqlDbType.NVarChar, CurrentRole, 50);
                    ModuleDb.AddParameter(command, "@IsContractor", SqlDbType.Bit, isContractor);
                    ModuleDb.AddParameter(command, "@IsAdministrator", SqlDbType.Bit, IsJeAdministrator);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) { NotFound(); return; }
                        storedPath = Convert.ToString(reader["FilePath"]);
                        fileName = Path.GetFileName(Convert.ToString(reader["FileName"]));
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Document metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            SendStoredFile(storedPath, fileName);
        }

        private void DownloadGisImage(int tenderId)
        {
            string storedPath, fileName;
            bool isContractor = HasAnyRole("Contractor");
            bool isSystemAdministrator = HasPermission("system.admin");
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT gis.ImagePath,gis.ImageOriginalFileName
                      FROM dbo.TenderGisLocations gis
                      INNER JOIN dbo.Tender t ON t.TenderId=gis.TenderId
                      LEFT JOIN dbo.TenderPublicationSchedule schedule ON schedule.TenderId=t.TenderId
                      WHERE t.TenderId=@TenderId AND t.IsDeleted=0
                        AND gis.ImagePath IS NOT NULL
                        AND
                        (
                            (@CurrentUserId=0 AND
                             schedule.PublicationStartUtc<=SYSUTCDATETIME()
                             AND t.CurrentStatus NOT LIKE N'%cancel%'
                             AND
                             (
                                 EXISTS(SELECT 1 FROM dbo.ApprovalHistory publicHistory
                                        WHERE publicHistory.TenderId=t.TenderId
                                          AND publicHistory.NewStatus=N'Tender Published')
                                 OR EXISTS(SELECT 1 FROM dbo.TenderWorkflowHistory publicWorkflowHistory
                                           WHERE publicWorkflowHistory.TenderId=t.TenderId
                                             AND publicWorkflowHistory.WorkflowVersion=2
                                             AND publicWorkflowHistory.NewStatus=N'Tender Published')
                             ))
                            OR (@CurrentUserId>0 AND @IsContractor=1 AND
                                (
                                    EXISTS(SELECT 1 FROM dbo.ApprovalHistory publicHistory
                                           WHERE publicHistory.TenderId=t.TenderId
                                             AND publicHistory.NewStatus=N'Tender Published')
                                    OR EXISTS(SELECT 1 FROM dbo.TenderWorkflowHistory publicWorkflowHistory
                                              WHERE publicWorkflowHistory.TenderId=t.TenderId
                                                AND publicWorkflowHistory.WorkflowVersion=2
                                                AND publicWorkflowHistory.NewStatus=N'Tender Published')
                                    OR (t.WorkflowVersion=2 AND t.CurrentStatus IN (N'Tender Published',N'Completed'))
                                    OR EXISTS(SELECT 1 FROM dbo.TenderWorkflowAssignments ownAssignment
                                              WHERE ownAssignment.TenderId=t.TenderId
                                                AND ownAssignment.IsCurrent=1
                                                AND ownAssignment.AssignedRoleId=@RoleId
                                                AND ownAssignment.AssignedUserId=@CurrentUserId)
                                ))
                            OR (@CurrentUserId>0 AND @IsContractor=0 AND
                                (
                                    @IsSystemAdministrator=1
                                    OR (t.WorkflowVersion=2 AND @DepartmentId IS NOT NULL
                                        AND t.DepartmentId=@DepartmentId)
                                    OR (t.WorkflowVersion=1 AND
                                        (@CurrentRole<>N'DepartmentOfficer'
                                         OR t.CreatedBy=@CurrentUserId))
                                ))
                        );", connection))
                {
                    ModuleDb.AddParameter(command, "@TenderId", SqlDbType.Int, tenderId);
                    ModuleDb.AddParameter(command, "@CurrentUserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@IsContractor", SqlDbType.Bit, isContractor);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                    ModuleDb.AddParameter(command, "@IsSystemAdministrator", SqlDbType.Bit, isSystemAdministrator);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                        CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                    ModuleDb.AddParameter(command, "@CurrentRole", SqlDbType.NVarChar,
                        CurrentRole ?? String.Empty, 50);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            NotFound();
                            return;
                        }
                        storedPath = Convert.ToString(reader["ImagePath"]);
                        fileName = TenderLifecycleFiles.SafeFileName(
                            Convert.ToString(reader["ImageOriginalFileName"]));
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "GIS image metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            SendStoredFile(storedPath, fileName);
        }

        private void DownloadExtensionDocument(int requestId, string kind)
        {
            if (!IsJeAdministrator && !HasAnyRole("DepartmentOfficer", "CAFOOfficer")) { NotFound(); return; }
            string storedPath, fileName;
            bool isAdmin = IsJeAdministrator;
            string departmentScope = isAdmin ? String.Empty :
                @" AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                   AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                   WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId IS NOT NULL)
                        OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                  WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId=t.DepartmentId))";
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT CASE WHEN @Kind=N'application' THEN e.ApplicationFilePath ELSE e.SupportingFilePath END AS FilePath,
                             CASE WHEN @Kind=N'application' THEN e.ApplicationFileName ELSE e.SupportingFileName END AS FileName
                      FROM dbo.TenderExtensionRequests e
                      INNER JOIN dbo.Tender t ON t.TenderId=e.TenderId
                      WHERE e.ExtensionRequestId=@RequestId AND t.IsDeleted=0 AND
                        (@IsAdmin=1
                         OR (@Role=N'DepartmentOfficer' AND e.SubmittedBy=@UserId)
                         OR @Role=N'CAFOOfficer')" + departmentScope + ";", connection))
                {
                    ModuleDb.AddParameter(command, "@Kind", SqlDbType.NVarChar, kind, 12);
                    ModuleDb.AddParameter(command, "@RequestId", SqlDbType.Int, requestId);
                    ModuleDb.AddParameter(command, "@Role", SqlDbType.NVarChar, CurrentRole, 50);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@IsAdmin", SqlDbType.Bit, isAdmin);
                    if (!isAdmin)
                    {
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        ModuleDb.AddParameter(command, "@ScopeUserId", SqlDbType.Int, CurrentUserId);
                    }
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) { NotFound(); return; }
                        storedPath = Convert.ToString(reader["FilePath"]);
                        fileName = TenderLifecycleFiles.SafeFileName(Convert.ToString(reader["FileName"]));
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Extension document metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            SendStoredFile(storedPath, fileName);
        }

        private void DownloadEvaluationDocument(int documentId)
        {
            if (!IsJeAdministrator && !HasAnyRole("TenderSection", "TenderCommittee")) { NotFound(); return; }
            string storedPath, fileName;
            bool isAdmin = IsJeAdministrator;
            string departmentScope = isAdmin ? String.Empty :
                @" AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                   AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                   WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId IS NOT NULL)
                        OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                  WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId=t.DepartmentId))";
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT d.FilePath,d.FileName
                      FROM dbo.TenderEvaluationDocuments d
                      INNER JOIN dbo.Tender t ON t.TenderId=d.TenderId
                      WHERE d.EvaluationDocumentId=@DocumentId AND t.IsDeleted=0
                        AND EXISTS(SELECT 1 FROM dbo.TenderBidOpening o WHERE o.TenderId=t.TenderId)" +
                    departmentScope + ";", connection))
                {
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                    if (!isAdmin)
                    {
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        ModuleDb.AddParameter(command, "@ScopeUserId", SqlDbType.Int, CurrentUserId);
                    }
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) { NotFound(); return; }
                        storedPath = Convert.ToString(reader["FilePath"]);
                        fileName = TenderLifecycleFiles.SafeFileName(Convert.ToString(reader["FileName"]));
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Evaluation document metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            SendStoredFile(storedPath, fileName);
        }

        private void DownloadTechnicalBidDocument(int technicalBidId)
        {
            if (!IsJeAdministrator && !HasAnyRole("TenderSection", "TenderCommittee")) { NotFound(); return; }
            string storedPath, fileName;
            bool isAdmin = IsJeAdministrator;
            string departmentScope = isAdmin ? String.Empty :
                @" AND (@DepartmentId IS NULL OR t.DepartmentId=@DepartmentId)
                   AND (NOT EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                   WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId IS NOT NULL)
                        OR EXISTS(SELECT 1 FROM dbo.Users scopeUser
                                  WHERE scopeUser.UserId=@ScopeUserId AND scopeUser.DepartmentId=t.DepartmentId))";
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT b.DocumentPath,b.TenderId
                      FROM dbo.TechnicalBid b
                      INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId
                      WHERE b.TechnicalBidId=@BidId AND t.IsDeleted=0
                        AND EXISTS(SELECT 1 FROM dbo.TenderBidOpening o WHERE o.TenderId=t.TenderId)" +
                    departmentScope + ";", connection))
                {
                    ModuleDb.AddParameter(command, "@BidId", SqlDbType.Int, technicalBidId);
                    if (!isAdmin)
                    {
                        ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                            CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                        ModuleDb.AddParameter(command, "@ScopeUserId", SqlDbType.Int, CurrentUserId);
                    }
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read() || reader["DocumentPath"] == DBNull.Value) { NotFound(); return; }
                        storedPath = Convert.ToString(reader["DocumentPath"]);
                    }
                }
                fileName = Path.GetFileName(storedPath);
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Technical bid document metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            SendStoredFile(storedPath, fileName);
        }

        private void DownloadTechnicalBidChildDocument(int documentId)
        {
            bool isContractor = HasAnyRole("ExternalBidder", "Contractor");
            bool isSystemAdministrator = HasPermission("system.admin");
            bool isAssignedReviewer = HasPermission("document.read.assigned");
            bool isLegacyReviewer = HasAnyRole("TenderSection", "TenderCommittee", "CAFOOfficer");
            if (CurrentUserId <= 0 ||
                (!isContractor && !isSystemAdministrator && !isAssignedReviewer && !isLegacyReviewer))
            {
                NotFound();
                return;
            }

            string storedPath, fileName;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT d.FilePath,d.FileName
                      FROM dbo.TechnicalBidDocuments d
                      INNER JOIN dbo.TechnicalBid b ON b.TechnicalBidId=d.TechnicalBidId
                      INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId
                      LEFT JOIN dbo.Contractors c ON c.ContractorId=b.ContractorId
                      WHERE d.TechnicalBidDocumentId=@DocumentId AND t.IsDeleted=0
                        AND
                        (
                            @IsSystemAdministrator=1
                            OR (@IsContractor=1 AND c.UserId=@UserId)
                            OR (@IsAssignedReviewer=1 AND t.WorkflowVersion=2 AND EXISTS
                                (
                                    SELECT 1
                                    FROM dbo.TenderWorkflowAssignments a
                                    INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                                        AND s.WorkflowVersion=2 AND s.IsActive=1
                                    WHERE a.TenderId=t.TenderId AND a.IsCurrent=1
                                      AND a.AssignedRoleId=@RoleId
                                      AND (a.AssignedUserId IS NULL OR a.AssignedUserId=@UserId)
                                      AND (@Role<>N'Junior Engineer (JE)'
                                           OR (@DepartmentId IS NOT NULL AND a.DepartmentId=@DepartmentId
                                               AND t.DepartmentId=@DepartmentId))
                                ))
                            OR (@IsLegacyReviewer=1 AND t.WorkflowVersion=1
                                AND EXISTS(SELECT 1 FROM dbo.TenderBidOpening o WHERE o.TenderId=t.TenderId)
                                AND (@Role=N'Admin' OR NOT EXISTS
                                    (SELECT 1 FROM dbo.Users scopeUser
                                     WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId IS NOT NULL)
                                     OR EXISTS
                                    (SELECT 1 FROM dbo.Users scopeUser
                                     WHERE scopeUser.UserId=@UserId AND scopeUser.DepartmentId=t.DepartmentId)))
                        );", connection))
                {
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.Int, documentId);
                    ModuleDb.AddParameter(command, "@IsSystemAdministrator", SqlDbType.Bit, isSystemAdministrator);
                    ModuleDb.AddParameter(command, "@IsContractor", SqlDbType.Bit, isContractor);
                    ModuleDb.AddParameter(command, "@IsAssignedReviewer", SqlDbType.Bit, isAssignedReviewer);
                    ModuleDb.AddParameter(command, "@IsLegacyReviewer", SqlDbType.Bit, isLegacyReviewer);
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@RoleId", SqlDbType.Int, CurrentRoleId);
                    AddText(command, "@Role", SqlDbType.NVarChar, CurrentRole, 50);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                        CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) { NotFound(); return; }
                        storedPath = Convert.ToString(reader["FilePath"]);
                        fileName = TenderLifecycleFiles.SafeFileName(Convert.ToString(reader["FileName"]));
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Bid document metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            SendStoredFile(storedPath, fileName);
        }

        private void DownloadShortfallSubmission(long submissionId)
        {
            bool isExternalBidder = HasAnyRole("ExternalBidder");
            bool isSystemAdministrator = HasPermission("system.admin");
            bool hasDocumentPermission = HasPermission("document.read.assigned");
            if (CurrentUserId <= 0 ||
                (!isExternalBidder && !isSystemAdministrator && !hasDocumentPermission))
            {
                NotFound();
                return;
            }

            string storedPath, fileName;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT submission.FilePath,submission.FileName
                      FROM dbo.TenderShortfallSubmissions submission
                      INNER JOIN dbo.TenderShortfallItems item
                        ON item.ShortfallItemId=submission.ShortfallItemId
                      INNER JOIN dbo.TenderShortfalls shortfall
                        ON shortfall.ShortfallId=item.ShortfallId
                      INNER JOIN dbo.Tender t ON t.TenderId=shortfall.TenderId AND t.IsDeleted=0
                      INNER JOIN dbo.Contractors c ON c.ContractorId=shortfall.ContractorId
                      WHERE submission.ShortfallSubmissionId=@SubmissionId
                        AND
                        (
                            @IsSystemAdministrator=1
                            OR (@IsExternalBidder=1 AND c.UserId=@UserId)
                            OR (@HasDocumentPermission=1 AND
                                (@IsDepartmentScopedRole=0 OR
                                 (@DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId)))
                        );", connection))
                {
                    ModuleDb.AddParameter(command, "@SubmissionId", SqlDbType.BigInt, submissionId);
                    ModuleDb.AddParameter(command, "@IsSystemAdministrator", SqlDbType.Bit, isSystemAdministrator);
                    ModuleDb.AddParameter(command, "@IsExternalBidder", SqlDbType.Bit, isExternalBidder);
                    ModuleDb.AddParameter(command, "@HasDocumentPermission", SqlDbType.Bit, hasDocumentPermission);
                    ModuleDb.AddParameter(command, "@IsDepartmentScopedRole", SqlDbType.Bit,
                        TenderFinalizedWorkflowService.IsJuniorEngineer(CurrentRoleCode) ||
                        TenderFinalizedWorkflowService.IsTenderCell(CurrentRoleCode));
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                        CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) { NotFound(); return; }
                        storedPath = Convert.ToString(reader["FilePath"]);
                        fileName = TenderLifecycleFiles.SafeFileName(Convert.ToString(reader["FileName"]));
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Shortfall document metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            SendStoredFile(storedPath, fileName);
        }

        private void DownloadGeneratedDocument(long generatedDocumentId)
        {
            bool isExternalBidder = HasAnyRole("ExternalBidder");
            bool isSystemAdministrator = HasPermission("system.admin");
            bool hasDocumentPermission = HasPermission("document.read.assigned");
            if (CurrentUserId <= 0 ||
                (!isExternalBidder && !isSystemAdministrator && !hasDocumentPermission))
            {
                NotFound();
                return;
            }

            string storedPath, fileName;
            try
            {
                using (SqlConnection connection = ModuleDb.OpenConnection())
                using (SqlCommand command = new SqlCommand(
                    @"SELECT d.FilePath,d.FileName
                      FROM dbo.TenderGeneratedDocuments d
                      INNER JOIN dbo.Tender t ON t.TenderId=d.TenderId AND t.IsDeleted=0
                      WHERE d.GeneratedDocumentId=@DocumentId
                        AND
                        (
                            @IsSystemAdministrator=1
                            OR (@IsExternalBidder=1 AND EXISTS
                                (
                                    SELECT 1
                                    FROM dbo.Contractors c
                                    WHERE c.UserId=@UserId AND c.IsActive=1
                                      AND
                                      (
                                          (d.WorkOrderId IS NOT NULL AND EXISTS
                                            (SELECT 1 FROM dbo.WorkOrder w
                                             WHERE w.WorkOrderId=d.WorkOrderId AND w.ContractorId=c.ContractorId))
                                          OR (d.AgreementId IS NOT NULL AND EXISTS
                                            (SELECT 1 FROM dbo.Agreement a
                                             WHERE a.AgreementId=d.AgreementId AND a.ContractorId=c.ContractorId))
                                          OR (d.DocumentKind=N'BOQ' AND EXISTS
                                            (SELECT 1 FROM dbo.TechnicalBid b
                                             WHERE b.TenderId=d.TenderId
                                               AND b.ContractorId=c.ContractorId))
                                      )
                                ))
                            OR (@HasDocumentPermission=1 AND
                                (@IsDepartmentScopedRole=0 OR
                                 (@DepartmentId IS NOT NULL AND t.DepartmentId=@DepartmentId)))
                        );", connection))
                {
                    ModuleDb.AddParameter(command, "@DocumentId", SqlDbType.BigInt, generatedDocumentId);
                    ModuleDb.AddParameter(command, "@IsSystemAdministrator", SqlDbType.Bit, isSystemAdministrator);
                    ModuleDb.AddParameter(command, "@IsExternalBidder", SqlDbType.Bit, isExternalBidder);
                    ModuleDb.AddParameter(command, "@HasDocumentPermission", SqlDbType.Bit, hasDocumentPermission);
                    ModuleDb.AddParameter(command, "@IsDepartmentScopedRole", SqlDbType.Bit,
                        TenderFinalizedWorkflowService.IsJuniorEngineer(CurrentRoleCode) ||
                        TenderFinalizedWorkflowService.IsTenderCell(CurrentRoleCode));
                    ModuleDb.AddParameter(command, "@UserId", SqlDbType.Int, CurrentUserId);
                    ModuleDb.AddParameter(command, "@DepartmentId", SqlDbType.Int,
                        CurrentDepartmentId.HasValue ? (object)CurrentDepartmentId.Value : null);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) { NotFound(); return; }
                        storedPath = Convert.ToString(reader["FilePath"]);
                        fileName = TenderLifecycleFiles.SafeFileName(Convert.ToString(reader["FileName"]));
                    }
                }
            }
            catch (SqlException ex)
            {
                Trace.Warn("DepartmentModule", "Generated document metadata could not be read.", ex);
                Response.StatusCode = 500;
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            SendStoredFile(storedPath, fileName);
        }

        private void SendStoredFile(string storedPath, string fileName)
        {
            string path;
            if (!TenderLifecycleFiles.TryResolveStoredFilePath(this, storedPath, out path))
            {
                NotFound();
                return;
            }

            Response.Clear();
            Response.ContentType = MimeType(Path.GetExtension(fileName));
            string disposition = IsInlinePreviewRequested(fileName) ? "inline" : "attachment";
            Response.AddHeader("Content-Disposition", disposition + "; filename=\"" + fileName.Replace("\"", String.Empty) + "\"");
            Response.TransmitFile(path);
            Context.ApplicationInstance.CompleteRequest();
        }

        private bool IsInlinePreviewRequested(string fileName)
        {
            if (!String.Equals(Request.QueryString["preview"], "true", StringComparison.OrdinalIgnoreCase))
                return false;
            string extension = Path.GetExtension(fileName);
            return String.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase);
        }

        private static long QueryLongId(string key)
        {
            long value;
            return Int64.TryParse(HttpContext.Current.Request.QueryString[key],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out value) && value > 0
                ? value : 0;
        }

        private static string MimeType(string extension)
        {
            switch ((extension ?? String.Empty).ToLowerInvariant())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                default: return "application/octet-stream";
            }
        }

        private void NotFound()
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
