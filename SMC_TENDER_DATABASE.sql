/*
  SMC Tender Management - authoritative SQL Server setup
  Target: SQL Server 2016 SP1+; run in SQLCMD mode.

  INSTALLATION SECTION MAP
    1. Database preflight and environment validation
    2. Migration/version tracking
    3. Core schemas and application tables
    4. Lookup and master tables
    5. Roles and permissions
    6. Users and authentication
    7. Departments and workflow assignments
    8. Contractors and vendor registration
    9. Tender creation and publication
   10. Tender documents and BOQ
   11. Initial tender opening
   12. Primary verification
   13. Secondary verification
   14. Shortfall management
   15. Financial bid opening
   16. Financial bid details
   17. Financial approvals
   18. Work Orders
   19. Tender Agreements
   20. Workflow status and history
   21. Audit logs
   22. Indexes and constraints
   23. Views and existing database objects (no views were present)
   24. Post-installation validation

  MODES (required SQLCMD variables):
    -v DatabaseName=<database> -v InstallMode=Fresh -v BackupVerified=NO
      Creates the database if absent; refuses any non-empty target.
    -v DatabaseName=<database> -v InstallMode=Upgrade -v BackupVerified=YES
      Requires the compatible legacy schema, preserves business row counts,
      applies additive objects, and refuses ambiguous/incompatible schemas.

  No business, user, role, permission, workflow-stage, evaluation-setting, or
  lookup catalog rows are seeded. The installer writes migration-ledger metadata.
  Upgrade mode also performs value-preserving backfills: designations are copied
  only from existing opening records, and TenderCategory is mapped only from an
  exact existing TenderTypes match. Unmatched tender values remain unchanged.
  This database contains no stored procedures: workflow rules run in the
  ASP.NET Web Forms code-behind. Upgrade mode drops legacy procedures only.

  Fresh example:
    sqlcmd -S "localhost\SQLEXPRESS02" -E -b -v DatabaseName="SMC_Tender_Fresh" -v InstallMode=Fresh -v BackupVerified=NO -i "SMC_TENDER_DATABASE.sql"

  Existing database example (only after a verified backup and isolated rehearsal):
    sqlcmd -S "localhost\SQLEXPRESS02" -E -b -v DatabaseName="TenderManagementDB" -v InstallMode=Upgrade -v BackupVerified=YES -i "SMC_TENDER_DATABASE.sql"

  Upgrade mode does not recreate or drop existing tables. If the required
  legacy columns/tables differ, the script stops; prepare a separately reviewed
  mapping/migration instead of guessing. Rehearse on a restored clone and run
  application tests before using it on a live database.
*/
-- Required SQLCMD variables: DatabaseName and InstallMode (Fresh or Upgrade).
:on error exit

IF LEN(N'$(DatabaseName)')=0 OR LEN(N'$(DatabaseName)')>128
   OR CHARINDEX(N']',N'$(DatabaseName)')>0 OR CHARINDEX(N';',N'$(DatabaseName)')>0
    THROW 51000, 'Supply a valid DatabaseName SQLCMD variable (1-128 characters).', 1;
IF N'$(InstallMode)' NOT IN (N'Fresh',N'Upgrade')
    THROW 51001, 'InstallMode must be Fresh or Upgrade.', 1;
IF N'$(InstallMode)'=N'Upgrade' AND UPPER(N'$(BackupVerified)')<>N'YES'
    THROW 51002, 'Upgrade mode requires BackupVerified=YES after a full backup restore has been verified.', 1;
IF N'$(InstallMode)'=N'Fresh' AND DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    DECLARE @CreateDatabaseSql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(N'$(DatabaseName)') + N';';
    EXEC sys.sp_executesql @CreateDatabaseSql;
END;
IF N'$(InstallMode)'=N'Upgrade' AND DB_ID(N'$(DatabaseName)') IS NULL
    THROW 51003, 'Upgrade refused: the target database does not exist.', 1;
GO
USE [$(DatabaseName)];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(InstallMode)'=N'Fresh' AND
   (EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
   OR EXISTS (SELECT 1 FROM sys.views WHERE is_ms_shipped=0)
   OR EXISTS (SELECT 1 FROM sys.procedures WHERE is_ms_shipped=0)
   OR EXISTS (SELECT 1 FROM sys.objects WHERE is_ms_shipped=0 AND type IN ('FN','IF','TF','TR'))
   OR EXISTS (SELECT 1 FROM sys.synonyms WHERE is_ms_shipped=0)
   OR EXISTS (SELECT 1 FROM sys.sequences WHERE is_ms_shipped=0)
   OR EXISTS (SELECT 1 FROM sys.triggers WHERE parent_class=0 AND is_ms_shipped=0))
    THROW 51000, 'Fresh installer refused: target database contains user objects. Use the reviewed upgrade procedure instead.', 1;
IF N'$(InstallMode)'=N'Fresh' AND OBJECT_ID(N'dbo.TenderSchemaMigrations',N'U') IS NOT NULL
    THROW 51001, 'Fresh installer refused: migration ledger already exists.', 1;
IF N'$(InstallMode)'=N'Upgrade' AND
   (
       OBJECT_ID(N'dbo.Roles',N'U') IS NULL OR OBJECT_ID(N'dbo.Departments',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Users',N'U') IS NULL OR OBJECT_ID(N'dbo.Contractors',N'U') IS NULL
       OR OBJECT_ID(N'dbo.Tender',N'U') IS NULL OR OBJECT_ID(N'dbo.ApprovalHistory',N'U') IS NULL
       OR OBJECT_ID(N'dbo.TenderDocuments',N'U') IS NULL OR OBJECT_ID(N'dbo.TechnicalBid',N'U') IS NULL
       OR OBJECT_ID(N'dbo.FinancialBid',N'U') IS NULL OR OBJECT_ID(N'dbo.AuditLog',N'U') IS NULL
       OR COL_LENGTH(N'dbo.Tender',N'TenderType') IS NULL
       OR COL_LENGTH(N'dbo.Tender',N'DepartmentId') IS NULL
       OR COL_LENGTH(N'dbo.Users',N'RoleId') IS NULL
       OR COL_LENGTH(N'dbo.Users',N'DepartmentId') IS NULL
   )
    THROW 51004, 'Upgrade refused: required legacy tables or columns are missing. Review and map the schema before retrying.', 1;
IF N'$(InstallMode)'=N'Upgrade' AND OBJECT_ID(N'dbo.TenderSchemaMigrations',N'U') IS NOT NULL
    THROW 51005, 'Upgrade refused: this installer ledger already exists. Review the prior run before retrying.', 1;

BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.TenderSchemaMigrations',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderSchemaMigrations
    (
        MigrationId NVARCHAR(50) NOT NULL CONSTRAINT PK_TenderSchemaMigrations PRIMARY KEY,
        Description NVARCHAR(300) NOT NULL,
        AppliedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_TenderSchemaMigrations_AppliedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;

CREATE TABLE #PreservedBusinessCounts
(
    TableName SYSNAME NOT NULL PRIMARY KEY,
    BusinessRowCount BIGINT NOT NULL
);
IF N'$(InstallMode)'=N'Upgrade'
BEGIN
    INSERT INTO #PreservedBusinessCounts(TableName,BusinessRowCount)
    SELECT t.name,SUM(CONVERT(BIGINT,p.row_count))
    FROM sys.tables t
    INNER JOIN sys.dm_db_partition_stats p ON p.object_id=t.object_id AND p.index_id IN (0,1)
    WHERE t.is_ms_shipped=0
      AND t.name NOT IN
      (
          N'TenderSchemaMigrations',N'Roles',N'RolePermissions',N'Permissions',
          N'WorkflowStages',N'Departments',N'DocumentTypes',N'TenderTypes',N'TenderMethods',
          N'Units',N'Designations',N'TenderEvaluationSettings'
      )
    GROUP BY t.name;
END;
GO
/* =====================================================================
   1. ROLES
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Roles (
    RoleId          INT IDENTITY(1,1) PRIMARY KEY,
    RoleName        NVARCHAR(50)  NOT NULL UNIQUE,
    RoleCode        NVARCHAR(50)  NULL,
    Description     NVARCHAR(200) NULL,
    CreatedDate     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    IsActive        BIT           NOT NULL DEFAULT 1
);
END;
GO

/* =====================================================================
   2. DEPARTMENTS
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Departments (
    DepartmentId      INT IDENTITY(1,1) PRIMARY KEY,
    DepartmentName    NVARCHAR(150) NOT NULL UNIQUE,
    DepartmentCode    NVARCHAR(20)  NULL,
    HeadOfficerName   NVARCHAR(150) NULL,
    IsActive          BIT           NOT NULL DEFAULT 1,
    CreatedDate       DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
END;
GO

/* =====================================================================
   3. USERS
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150)  NOT NULL,
    Email           NVARCHAR(150)  NOT NULL UNIQUE,
    Mobile          NVARCHAR(20)   NULL,
    PasswordHash    NVARCHAR(500)  NOT NULL,
    RoleId          INT            NOT NULL,
    DepartmentId    INT            NULL,
    IsActive        BIT            NOT NULL DEFAULT 1,
    CreatedDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy       INT            NULL,
    ModifiedDate    DATETIME2      NULL,
    ModifiedBy      INT            NULL,
    FailedLoginCount INT NOT NULL CONSTRAINT DF_Users_FailedLoginCount DEFAULT(0),
    LockoutUntil DATETIME2 NULL,
    LastLoginDate DATETIME2 NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId),
    CONSTRAINT FK_Users_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(DepartmentId)
);
END;
GO
IF N'$(InstallMode)'=N'Upgrade'
BEGIN
    IF COL_LENGTH(N'dbo.Users',N'FailedLoginCount') IS NULL
        ALTER TABLE dbo.Users ADD FailedLoginCount INT NOT NULL
            CONSTRAINT DF_Users_FailedLoginCount DEFAULT(0) WITH VALUES;
    IF COL_LENGTH(N'dbo.Users',N'LockoutUntil') IS NULL
        ALTER TABLE dbo.Users ADD LockoutUntil DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Users',N'LastLoginDate') IS NULL
        ALTER TABLE dbo.Users ADD LastLoginDate DATETIME2 NULL;
END;
GO
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE INDEX IX_Users_RoleId ON dbo.Users(RoleId);
END;
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE INDEX IX_Users_DepartmentId ON dbo.Users(DepartmentId);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'IX_Users_Mobile')
    CREATE INDEX IX_Users_Mobile ON dbo.Users(Mobile);
GO

/* =====================================================================
   4. CONTRACTORS
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Contractors (
    ContractorId    INT IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150) NOT NULL,
    RegistrationNo  NVARCHAR(100) NOT NULL UNIQUE,
    Address         NVARCHAR(300) NULL,
    ContactNo       NVARCHAR(20)  NULL,
    Email           NVARCHAR(150) NULL,
    UserId          INT           NULL,
    IsActive        BIT           NOT NULL DEFAULT 1,
    CreatedDate     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Contractors_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
END;
GO

/* =====================================================================
   5. TENDER
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Tender (
    TenderId           INT IDENTITY(1,1) PRIMARY KEY,
    TenderNumber       NVARCHAR(50)   NOT NULL UNIQUE,
    TrackingId         NVARCHAR(100)  NOT NULL UNIQUE DEFAULT (CONVERT(NVARCHAR(100), NEWID())),
    Title              NVARCHAR(250)  NOT NULL,
    Description        NVARCHAR(MAX)  NULL,
    DepartmentId       INT            NOT NULL,
    EstimatedAmount    DECIMAL(18,2)  NOT NULL DEFAULT 0,
    TenderType         NVARCHAR(50)   NOT NULL,
    CurrentStatus      NVARCHAR(50)   NOT NULL DEFAULT 'Draft',
    SubmissionStartDate DATETIME2     NULL,
    SubmissionDeadline DATETIME2      NULL,
    PublicationDate DATETIME2 NULL,
    RowVer ROWVERSION NOT NULL,
    CreationToken UNIQUEIDENTIFIER NULL,
    CreatedBy          INT            NOT NULL,
    CreatedDate        DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedBy         INT            NULL,
    ModifiedDate       DATETIME2      NULL,
    IsDeleted          BIT            NOT NULL DEFAULT 0,
    CONSTRAINT FK_Tender_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(DepartmentId),
    CONSTRAINT FK_Tender_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId)
);
END;
GO
IF N'$(InstallMode)'=N'Upgrade'
BEGIN
    IF COL_LENGTH(N'dbo.Tender',N'PublicationDate') IS NULL
        ALTER TABLE dbo.Tender ADD PublicationDate DATETIME2 NULL;
    IF COL_LENGTH(N'dbo.Tender',N'RowVer') IS NULL
        ALTER TABLE dbo.Tender ADD RowVer ROWVERSION NOT NULL;
    IF COL_LENGTH(N'dbo.Tender',N'CreationToken') IS NULL
        ALTER TABLE dbo.Tender ADD CreationToken UNIQUEIDENTIFIER NULL;
END;
GO
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE INDEX IX_Tender_DepartmentId ON dbo.Tender(DepartmentId);
END;
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE INDEX IX_Tender_CurrentStatus ON dbo.Tender(CurrentStatus);
END;
GO

/* =====================================================================
   6. ESTIMATE
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Estimate (
    EstimateId      INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    Amount          DECIMAL(18,2)  NOT NULL,
    PreparedBy      INT            NOT NULL,
    DocumentPath    NVARCHAR(400)  NULL,
    Status          NVARCHAR(50)   NOT NULL DEFAULT 'Prepared',
    CreatedDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDate    DATETIME2      NULL,
    CONSTRAINT FK_Estimate_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId) ON DELETE CASCADE,
    CONSTRAINT FK_Estimate_Users FOREIGN KEY (PreparedBy) REFERENCES dbo.Users(UserId)
);
END;
GO

/* =====================================================================
   7. APPROVAL HISTORY
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.ApprovalHistory (
    ApprovalId       INT IDENTITY(1,1) PRIMARY KEY,
    TenderId         INT            NOT NULL,
    ApprovalStage    NVARCHAR(80)   NOT NULL,
    PreviousStatus   NVARCHAR(50)   NULL,
    NewStatus        NVARCHAR(50)   NOT NULL,
    Status           NVARCHAR(20)   NOT NULL,
    ApprovedBy       INT            NOT NULL,
    RoleAtAction     NVARCHAR(50)   NULL,
    Remarks          NVARCHAR(1000) NULL,
    ApprovalDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ApprovalHistory_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId) ON DELETE CASCADE,
    CONSTRAINT FK_ApprovalHistory_Users FOREIGN KEY (ApprovedBy) REFERENCES dbo.Users(UserId)
);
END;
GO
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE INDEX IX_ApprovalHistory_TenderId ON dbo.ApprovalHistory(TenderId);
END;
GO

/* =====================================================================
   8. TENDER DOCUMENTS
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.TenderDocuments (
    DocumentId      INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    DocumentType    NVARCHAR(80)   NOT NULL,
    FileName        NVARCHAR(260)  NOT NULL,
    FilePath        NVARCHAR(400)  NOT NULL,
    UploadedBy      INT            NOT NULL,
    UploadedDate    DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_TenderDocuments_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId) ON DELETE CASCADE,
    CONSTRAINT FK_TenderDocuments_Users FOREIGN KEY (UploadedBy) REFERENCES dbo.Users(UserId)
);
END;
GO

/* =====================================================================
   9. BOQ
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.BOQ (
    BOQId           INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    ItemName        NVARCHAR(200)  NOT NULL,
    Description     NVARCHAR(500)  NULL,
    Quantity        DECIMAL(18,3)  NOT NULL,
    Unit            NVARCHAR(20)   NOT NULL,
    Rate            DECIMAL(18,2)  NOT NULL,
    Amount          AS (Quantity * Rate) PERSISTED,
    CreatedDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_BOQ_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId) ON DELETE CASCADE
);
END;
GO

/* =====================================================================
   10. TECHNICAL BID
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.TechnicalBid (
    TechnicalBidId  INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    ContractorId    INT            NOT NULL,
    DocumentPath    NVARCHAR(400)  NULL,
    Status          NVARCHAR(30)   NOT NULL DEFAULT 'Submitted',
    Remarks         NVARCHAR(1000) NULL,
    EvaluatedBy     INT            NULL,
    SubmittedDate   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    EvaluatedDate   DATETIME2      NULL,
    TermsAcceptedAt DATETIME2      NULL,
    TermsVersion    NVARCHAR(50)   NULL,
    CONSTRAINT FK_TechnicalBid_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId) ON DELETE CASCADE,
    CONSTRAINT FK_TechnicalBid_Contractor FOREIGN KEY (ContractorId) REFERENCES dbo.Contractors(ContractorId),
    CONSTRAINT UQ_TechnicalBid_Tender_Contractor UNIQUE (TenderId, ContractorId)
);
END;
GO

IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.TechnicalBidDocuments (
    TechnicalBidDocumentId INT IDENTITY(1,1) PRIMARY KEY,
    TechnicalBidId         INT            NOT NULL,
    FileName               NVARCHAR(260)  NOT NULL,
    FilePath               NVARCHAR(400)  NOT NULL,
    UploadedDate           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_TechnicalBidDocuments_TechnicalBid
        FOREIGN KEY (TechnicalBidId) REFERENCES dbo.TechnicalBid(TechnicalBidId) ON DELETE CASCADE
);
END;
GO

/* =====================================================================
   11. FINANCIAL BID
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.FinancialBid (
    FinancialBidId  INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    ContractorId    INT            NOT NULL,
    BidAmount       DECIMAL(18,2)  NOT NULL,
    [Rank]          INT            NULL,
    Status          NVARCHAR(30)   NOT NULL DEFAULT 'Submitted',
    SubmittedDate   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_FinancialBid_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId) ON DELETE CASCADE,
    CONSTRAINT FK_FinancialBid_Contractor FOREIGN KEY (ContractorId) REFERENCES dbo.Contractors(ContractorId),
    CONSTRAINT UQ_FinancialBid_Tender_Contractor UNIQUE (TenderId, ContractorId)
);
END;
GO

/* =====================================================================
   12. AGREEMENT
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Agreement (
    AgreementId     INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    ContractorId    INT            NOT NULL,
    AgreementNo     NVARCHAR(50)   NOT NULL UNIQUE,
    AgreementDate   DATETIME2      NOT NULL,
    DocumentPath    NVARCHAR(400)  NULL,
    CreatedDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Agreement_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId),
    CONSTRAINT FK_Agreement_Contractor FOREIGN KEY (ContractorId) REFERENCES dbo.Contractors(ContractorId)
);
END;
GO

/* =====================================================================
   13. WORK ORDER
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.WorkOrder (
    WorkOrderId     INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    ContractorId    INT            NOT NULL,
    OrderNo         NVARCHAR(50)   NOT NULL UNIQUE,
    IssueDate       DATETIME2      NOT NULL,
    Amount          DECIMAL(18,2)  NOT NULL,
    CompletionDate  DATETIME2      NULL,
    CreatedDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_WorkOrder_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId),
    CONSTRAINT FK_WorkOrder_Contractor FOREIGN KEY (ContractorId) REFERENCES dbo.Contractors(ContractorId)
);
END;
GO

/* =====================================================================
   14. WORK EXECUTION
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.WorkExecution (
    ExecutionId        INT IDENTITY(1,1) PRIMARY KEY,
    TenderId           INT            NOT NULL,
    ProgressPercentage DECIMAL(5,2)   NOT NULL DEFAULT 0,
    Remarks            NVARCHAR(1000) NULL,
    UpdatedBy          INT            NULL,
    UpdatedDate        DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_WorkExecution_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId),
    CONSTRAINT FK_WorkExecution_Users FOREIGN KEY (UpdatedBy) REFERENCES dbo.Users(UserId)
);
END;
GO

/* =====================================================================
   15. MEASUREMENT BOOK
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.MeasurementBook (
    MBId             INT IDENTITY(1,1) PRIMARY KEY,
    TenderId         INT            NOT NULL,
    Description      NVARCHAR(500)  NOT NULL,
    Quantity         DECIMAL(18,3)  NOT NULL,
    MeasurementDate  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    RecordedBy       INT            NULL,
    ApprovedBy       INT            NULL,
    ApprovalStatus   NVARCHAR(30)   NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_MeasurementBook_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId),
    CONSTRAINT FK_MeasurementBook_RecordedBy FOREIGN KEY (RecordedBy) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_MeasurementBook_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.Users(UserId)
);
END;
GO

/* =====================================================================
   16. BILLS
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Bills (
    BillId          INT IDENTITY(1,1) PRIMARY KEY,
    TenderId        INT            NOT NULL,
    ContractorId    INT            NOT NULL,
    BillNo          NVARCHAR(50)   NOT NULL UNIQUE,
    Amount          DECIMAL(18,2)  NOT NULL,
    Status          NVARCHAR(30)   NOT NULL DEFAULT 'Submitted',
    SubmittedDate   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    ApprovedBy      INT            NULL,
    ApprovedDate    DATETIME2      NULL,
    CONSTRAINT FK_Bills_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId),
    CONSTRAINT FK_Bills_Contractor FOREIGN KEY (ContractorId) REFERENCES dbo.Contractors(ContractorId),
    CONSTRAINT FK_Bills_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.Users(UserId)
);
END;
GO

/* =====================================================================
   17. PAYMENTS
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    BillId          INT            NOT NULL,
    Amount          DECIMAL(18,2)  NOT NULL,
    PaymentDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    TransactionNo   NVARCHAR(100)  NOT NULL UNIQUE,
    ProcessedBy     INT            NULL,
    CONSTRAINT FK_Payments_Bills FOREIGN KEY (BillId) REFERENCES dbo.Bills(BillId),
    CONSTRAINT FK_Payments_Users FOREIGN KEY (ProcessedBy) REFERENCES dbo.Users(UserId)
);
END;
GO

/* =====================================================================
   18. AUDIT LOG
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.AuditLog (
    LogId           BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT            NULL,
    Action          NVARCHAR(80)   NOT NULL,
    EntityName      NVARCHAR(100)  NOT NULL,
    EntityId        INT            NULL,
    Details         NVARCHAR(MAX)  NULL,
    IPAddress       NVARCHAR(50)   NULL,
    CreatedDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AuditLog_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
END;
GO
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE INDEX IX_AuditLog_EntityName_EntityId ON dbo.AuditLog(EntityName, EntityId);
END;
GO

/* =====================================================================
   19. WORKFLOW STAGES
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.WorkflowStages (
    StageId         INT IDENTITY(1,1) PRIMARY KEY,
    StageOrder      INT            NOT NULL,
    StageName       NVARCHAR(80)   NOT NULL UNIQUE,
    StatusOnPending NVARCHAR(50)   NOT NULL,
    StatusOnApprove NVARCHAR(50)   NOT NULL,
    StatusOnReject  NVARCHAR(50)   NOT NULL DEFAULT 'Rejected',
    ApproverRoleId  INT            NOT NULL,
    IsActive        BIT            NOT NULL DEFAULT 1,
    CONSTRAINT FK_WorkflowStages_Roles FOREIGN KEY (ApproverRoleId) REFERENCES dbo.Roles(RoleId)
);
END;
GO

/* =====================================================================
   19a. ANNOUNCEMENTS -- public homepage notices, added for the portal
   homepage. Backs the "Important Announcements" section so it isn't
   hardcoded frontend content.
   ===================================================================== */
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE TABLE dbo.Announcements (
    AnnouncementId  INT IDENTITY(1,1) PRIMARY KEY,
    Title           NVARCHAR(250)  NOT NULL,
    Body            NVARCHAR(MAX)  NULL,
    IsPublished     BIT            NOT NULL DEFAULT 0,
    PublishDate     DATETIME2      NULL,
    ExpiryDate      DATETIME2      NULL,
    CreatedBy       INT            NOT NULL,
    CreatedDate     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedBy      INT            NULL,
    ModifiedDate    DATETIME2      NULL,
    IsDeleted       BIT            NOT NULL DEFAULT 0,
    CONSTRAINT FK_Announcements_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId)
);
END;
GO
IF N'$(InstallMode)'=N'Fresh'
BEGIN
CREATE INDEX IX_Announcements_IsPublished ON dbo.Announcements(IsPublished, PublishDate);
END;
GO


IF COL_LENGTH(N'dbo.Tender',N'CreationToken') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'UX_Tender_CreationToken')
    EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_Tender_CreationToken ON dbo.Tender(CreationToken) WHERE CreationToken IS NOT NULL;';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TechnicalBidDocuments') AND name=N'IX_TechnicalBidDocuments_TechnicalBidId')
    CREATE INDEX IX_TechnicalBidDocuments_TechnicalBidId ON dbo.TechnicalBidDocuments(TechnicalBidId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuditLog') AND name=N'IX_AuditLog_Created')
    CREATE INDEX IX_AuditLog_Created ON dbo.AuditLog(CreatedDate DESC) INCLUDE(UserId,Action,EntityName);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'IX_Tender_Status_Dept')
    CREATE INDEX IX_Tender_Status_Dept ON dbo.Tender(CurrentStatus,DepartmentId)
        INCLUDE(TenderNumber,Title,EstimatedAmount,SubmissionDeadline,CreatedDate,IsDeleted);
GO
/* Live auxiliary OTP/auth schema retained; ownership/use is unverified. */
IF OBJECT_ID(N'dbo.AuthenticationAttempts',N'U') IS NULL
BEGIN
CREATE TABLE dbo.AuthenticationAttempts
(
    AttemptKey VARBINARY(32) NOT NULL CONSTRAINT PK_AuthenticationAttempts PRIMARY KEY,
    FailureCount INT NOT NULL CONSTRAINT CK_AuthenticationAttempts_FailureCount CHECK(FailureCount BETWEEN 1 AND 5),
    WindowStartedUtc DATETIME2 NOT NULL,
    LockedUntilUtc DATETIME2 NULL,
    LastAttemptUtc DATETIME2 NOT NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AuthenticationAttempts') AND name=N'IX_AuthenticationAttempts_LockedUntilUtc')
BEGIN
CREATE INDEX IX_AuthenticationAttempts_LockedUntilUtc
    ON dbo.AuthenticationAttempts(LockedUntilUtc) WHERE LockedUntilUtc IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.LoginOtpChallenges',N'U') IS NULL
BEGIN
CREATE TABLE dbo.LoginOtpChallenges
(
    ChallengeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LoginOtpChallenges PRIMARY KEY,
    UserId INT NOT NULL,
    OtpHash NVARCHAR(128) NOT NULL,
    Salt VARBINARY(32) NOT NULL,
    MobileRateKey NVARCHAR(128) NOT NULL,
    IpRateKey NVARCHAR(128) NOT NULL,
    ExpiresAtUtc DATETIME2 NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_LoginOtpChallenges_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    FailedAttempts TINYINT NOT NULL CONSTRAINT DF_LoginOtpChallenges_FailedAttempts DEFAULT(0),
    ConsumedAtUtc DATETIME2 NULL,
    CONSTRAINT CK_LoginOtpChallenges_FailedAttempts CHECK(FailedAttempts<=5),
    CONSTRAINT FK_LoginOtpChallenges_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(UserId)
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LoginOtpChallenges') AND name=N'IX_LoginOtpChallenges_UserCreated')
BEGIN
CREATE INDEX IX_LoginOtpChallenges_UserCreated
    ON dbo.LoginOtpChallenges(UserId,CreatedAtUtc DESC) INCLUDE(ExpiresAtUtc,ConsumedAtUtc) WHERE UserId IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.LoginOtpRateLimits',N'U') IS NULL
BEGIN
CREATE TABLE dbo.LoginOtpRateLimits
(
    RateKey NVARCHAR(128) NOT NULL CONSTRAINT PK_LoginOtpRateLimits PRIMARY KEY,
    WindowStartedUtc DATETIME2 NOT NULL,
    LastRequestUtc DATETIME2 NOT NULL,
    RequestCount SMALLINT NOT NULL CONSTRAINT DF_LoginOtpRateLimits_RequestCount DEFAULT(0),
    FailureCount SMALLINT NOT NULL CONSTRAINT DF_LoginOtpRateLimits_FailureCount DEFAULT(0),
    LockedUntilUtc DATETIME2 NULL
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.LoginOtpRateLimits') AND name=N'IX_LoginOtpRateLimits_LastRequest')
BEGIN
CREATE INDEX IX_LoginOtpRateLimits_LastRequest
    ON dbo.LoginOtpRateLimits(LastRequestUtc) WHERE LastRequestUtc IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.MobileOtpChallenges',N'U') IS NULL
BEGIN
CREATE TABLE dbo.MobileOtpChallenges
(
    ChallengeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MobileOtpChallenges PRIMARY KEY,
    UserId INT NULL,
    MobileHash VARBINARY(32) NOT NULL,
    IpHash VARBINARY(32) NOT NULL,
    ProtectedChallengeSecret VARBINARY(256) NOT NULL,
    OtpHash VARBINARY(32) NOT NULL,
    IssuedUtc DATETIME2 NOT NULL,
    ExpiresUtc DATETIME2 NOT NULL,
    ConsumedUtc DATETIME2 NULL,
    FailedAttempts INT NOT NULL CONSTRAINT DF_MobileOtpChallenges_FailedAttempts DEFAULT(0),
    CONSTRAINT CK_MobileOtpChallenges_FailedAttempts CHECK(FailedAttempts BETWEEN 0 AND 2),
    CONSTRAINT CK_MobileOtpChallenges_Expiry CHECK(ExpiresUtc>IssuedUtc)
);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MobileOtpChallenges') AND name=N'IX_MobileOtpChallenges_MobileHash_IssuedUtc')
BEGIN
CREATE INDEX IX_MobileOtpChallenges_MobileHash_IssuedUtc ON dbo.MobileOtpChallenges(MobileHash,IssuedUtc);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MobileOtpChallenges') AND name=N'IX_MobileOtpChallenges_IpHash_IssuedUtc')
BEGIN
CREATE INDEX IX_MobileOtpChallenges_IpHash_IssuedUtc ON dbo.MobileOtpChallenges(IpHash,IssuedUtc);
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MobileOtpChallenges') AND name=N'IX_MobileOtpChallenges_ExpiresUtc')
BEGIN
CREATE INDEX IX_MobileOtpChallenges_ExpiresUtc ON dbo.MobileOtpChallenges(ExpiresUtc);
END;

IF OBJECT_ID(N'dbo.MobileOtpVerificationLockouts',N'U') IS NULL
BEGIN
CREATE TABLE dbo.MobileOtpVerificationLockouts
(
    MobileHash VARBINARY(32) NOT NULL CONSTRAINT PK_MobileOtpVerificationLockouts PRIMARY KEY,
    FailedAttempts INT NOT NULL,
    LockedUntilUtc DATETIME2 NULL,
    LastFailedUtc DATETIME2 NOT NULL,
    CONSTRAINT CK_MobileOtpVerificationLockouts_FailedAttempts
        CHECK((FailedAttempts=1 AND LockedUntilUtc IS NULL) OR (FailedAttempts=2 AND LockedUntilUtc IS NOT NULL))
);
END;

/* Lookup definitions and values match the inspected live SQL Server database. */
IF OBJECT_ID(N'dbo.DocumentTypes',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentTypes
    (
        TypeName NVARCHAR(80) NOT NULL CONSTRAINT PK_DocumentTypes PRIMARY KEY,
        SortOrder INT NOT NULL CONSTRAINT DF_DocumentTypes_SortOrder DEFAULT(0),
        IsActive BIT NOT NULL CONSTRAINT DF_DocumentTypes_IsActive DEFAULT(1)
    );
END;
IF OBJECT_ID(N'dbo.TenderTypes',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderTypes
    (
        TypeName NVARCHAR(50) NOT NULL CONSTRAINT PK_TenderTypes PRIMARY KEY,
        SortOrder INT NOT NULL CONSTRAINT DF_TenderTypes_SortOrder DEFAULT(0),
        IsActive BIT NOT NULL CONSTRAINT DF_TenderTypes_IsActive DEFAULT(1)
    );
END;
IF OBJECT_ID(N'dbo.Units',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Units
    (
        UnitName NVARCHAR(20) NOT NULL CONSTRAINT PK_Units PRIMARY KEY,
        SortOrder INT NOT NULL CONSTRAINT DF_Units_SortOrder DEFAULT(0),
        IsActive BIT NOT NULL CONSTRAINT DF_Units_IsActive DEFAULT(1)
    );
END;
IF OBJECT_ID(N'dbo.TenderMethods',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderMethods
    (
        MethodName NVARCHAR(50) NOT NULL CONSTRAINT PK_TenderMethods PRIMARY KEY,
        SortOrder INT NOT NULL CONSTRAINT DF_TenderMethods_SortOrder DEFAULT(0),
        IsActive BIT NOT NULL CONSTRAINT DF_TenderMethods_IsActive DEFAULT(1)
    );
END;
IF OBJECT_ID(N'dbo.Designations',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Designations
    (
        DesignationName NVARCHAR(150) NOT NULL CONSTRAINT PK_Designations PRIMARY KEY,
        SortOrder INT NOT NULL CONSTRAINT DF_Designations_SortOrder DEFAULT(0),
        IsActive BIT NOT NULL CONSTRAINT DF_Designations_IsActive DEFAULT(1),
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Designations_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;
GO
/* =====================================================================
   20. NO STORED PROCEDURES
   Business rules run in the ASP.NET Web Forms code-behind with parameterized
   SQL inside transactions. Legacy procedures from earlier installs are dropped.
   ===================================================================== */
DROP PROCEDURE IF EXISTS dbo.sp_UpdateTenderStatus;
DROP PROCEDURE IF EXISTS dbo.sp_GetDashboardStats;
DROP PROCEDURE IF EXISTS dbo.sp_GetPendingApprovalsForRole;
DROP PROCEDURE IF EXISTS dbo.sp_GetFinancialBidRanking;
DROP PROCEDURE IF EXISTS dbo.sp_GetTenderFullHistory;
DROP PROCEDURE IF EXISTS dbo.sp_SaveTenderPublicationSchedule;
DROP PROCEDURE IF EXISTS dbo.sp_CreateTenderExtensionRequest;
DROP PROCEDURE IF EXISTS dbo.sp_ReviewTenderExtensionRequest;
DROP PROCEDURE IF EXISTS dbo.sp_OpenTenderBids;
DROP PROCEDURE IF EXISTS dbo.sp_StartTenderEvaluation;
GO
/* Preserve only designations already present in an upgraded database. */
IF OBJECT_ID(N'dbo.TenderBidOpening',N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TenderBidOpening',N'Designation') IS NOT NULL
BEGIN
    INSERT INTO dbo.Designations(DesignationName)
    SELECT DISTINCT LTRIM(RTRIM(o.Designation))
    FROM dbo.TenderBidOpening o
    WHERE NULLIF(LTRIM(RTRIM(o.Designation)),N'') IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.Designations d
          WHERE d.DesignationName=LTRIM(RTRIM(o.Designation))
      );
END;

IF COL_LENGTH(N'dbo.Tender',N'TenderCategory') IS NULL
    ALTER TABLE dbo.Tender ADD TenderCategory NVARCHAR(50) NULL;
GO
UPDATE t
SET TenderCategory=t.TenderType
FROM dbo.Tender t
INNER JOIN dbo.TenderTypes category ON category.TypeName=t.TenderType
WHERE t.TenderCategory IS NULL;
IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Tender_TenderTypes'
      AND parent_object_id=OBJECT_ID(N'dbo.Tender')
)
    ALTER TABLE dbo.Tender WITH CHECK
        ADD CONSTRAINT FK_Tender_TenderTypes FOREIGN KEY(TenderCategory) REFERENCES dbo.TenderTypes(TypeName);
GO

GO
/* Migrations 04, 05, 06, and 08 are incorporated as schema changes; no reference rows are seeded. */

/* Migration 09: publication lifecycle, opening, evaluation, history, and immutability triggers. */
/*
  Add publication scheduling, extension review, bid opening and evaluation
  records to the existing Web Forms schema. Existing tender, user, approval,
  document and audit tables are reused. All lifecycle timestamps are UTC;
  Web Forms converts publication inputs and outputs to India Standard Time.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.TenderPublicationSchedule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderPublicationSchedule
    (
        PublicationScheduleId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderPublicationSchedule PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT UQ_TenderPublicationSchedule_Tender UNIQUE
            CONSTRAINT FK_TenderPublicationSchedule_Tender
                REFERENCES dbo.Tender(TenderId),
        PublicationStartUtc DATETIME2(0) NOT NULL,
        OriginalClosingUtc DATETIME2(0) NOT NULL,
        CurrentClosingUtc DATETIME2(0) NOT NULL,
        PublicationPeriodDays DECIMAL(10,2) NOT NULL,
        Remarks NVARCHAR(1000) NULL,
        ApprovedBy INT NOT NULL
            CONSTRAINT FK_TenderPublicationSchedule_ApprovedBy REFERENCES dbo.Users(UserId),
        ApprovedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderPublicationSchedule_ApprovedAt DEFAULT SYSUTCDATETIME(),
        ModifiedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderPublicationSchedule_ModifiedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TenderPublicationSchedule_Range
            CHECK (PublicationStartUtc < OriginalClosingUtc AND CurrentClosingUtc >= OriginalClosingUtc),
        CONSTRAINT CK_TenderPublicationSchedule_Days CHECK (PublicationPeriodDays > 0)
    );
    CREATE INDEX IX_TenderPublicationSchedule_CurrentClosing
        ON dbo.TenderPublicationSchedule(CurrentClosingUtc, TenderId);
END;
GO

IF OBJECT_ID(N'dbo.TenderExtensionRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderExtensionRequests
    (
        ExtensionRequestId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderExtensionRequests PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderExtensionRequests_Tender REFERENCES dbo.Tender(TenderId),
        ExtensionNumber TINYINT NOT NULL,
        OriginalClosingUtc DATETIME2(0) NOT NULL,
        ProposedClosingUtc DATETIME2(0) NOT NULL,
        ApplicationFileName NVARCHAR(260) NOT NULL,
        ApplicationFilePath NVARCHAR(400) NOT NULL,
        SupportingFileName NVARCHAR(260) NOT NULL,
        SupportingFilePath NVARCHAR(400) NOT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        SubmittedBy INT NOT NULL
            CONSTRAINT FK_TenderExtensionRequests_SubmittedBy REFERENCES dbo.Users(UserId),
        SubmittedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderExtensionRequests_SubmittedAt DEFAULT SYSUTCDATETIME(),
        ReviewStatus NVARCHAR(20) NOT NULL
            CONSTRAINT DF_TenderExtensionRequests_Status DEFAULT N'Pending',
        ReviewedBy INT NULL
            CONSTRAINT FK_TenderExtensionRequests_ReviewedBy REFERENCES dbo.Users(UserId),
        ReviewedAtUtc DATETIME2(0) NULL,
        ReviewRemarks NVARCHAR(1000) NULL,
        CONSTRAINT CK_TenderExtensionRequests_Number CHECK (ExtensionNumber BETWEEN 1 AND 3),
        CONSTRAINT CK_TenderExtensionRequests_Range CHECK (ProposedClosingUtc > OriginalClosingUtc),
        CONSTRAINT CK_TenderExtensionRequests_Status CHECK (ReviewStatus IN (N'Pending',N'Approved',N'Rejected'))
    );
    CREATE INDEX IX_TenderExtensionRequests_TenderStatus
        ON dbo.TenderExtensionRequests(TenderId, ReviewStatus, ExtensionNumber);
    CREATE UNIQUE INDEX UX_TenderExtensionRequests_OnePending
        ON dbo.TenderExtensionRequests(TenderId) WHERE ReviewStatus=N'Pending';
END;
GO

IF OBJECT_ID(N'dbo.TenderBidOpening', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderBidOpening
    (
        BidOpeningId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderBidOpening PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT UQ_TenderBidOpening_Tender UNIQUE
            CONSTRAINT FK_TenderBidOpening_Tender REFERENCES dbo.Tender(TenderId),
        OriginalClosingUtc DATETIME2(0) NOT NULL,
        CurrentClosingUtc DATETIME2(0) NOT NULL,
        ActualOpeningUtc DATETIME2(0) NOT NULL,
        OpenedBy INT NOT NULL
            CONSTRAINT FK_TenderBidOpening_OpenedBy REFERENCES dbo.Users(UserId),
        OperatorName NVARCHAR(150) NOT NULL,
        Designation NVARCHAR(150) NOT NULL,
        OpeningRemarks NVARCHAR(1000) NULL,
        OpeningStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_TenderBidOpening_Status DEFAULT N'Opened',
        CONSTRAINT CK_TenderBidOpening_Status CHECK (OpeningStatus=N'Opened')
    );
END;
GO

IF OBJECT_ID(N'dbo.TenderEvaluationSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderEvaluationSettings
    (
        SettingsId TINYINT NOT NULL
            CONSTRAINT PK_TenderEvaluationSettings PRIMARY KEY
            CONSTRAINT CK_TenderEvaluationSettings_Singleton CHECK (SettingsId=1),
        StartDelayDays INT NOT NULL
            CONSTRAINT CK_TenderEvaluationSettings_Delay CHECK (StartDelayDays BETWEEN 0 AND 365),
        ModifiedBy INT NULL
            CONSTRAINT FK_TenderEvaluationSettings_ModifiedBy REFERENCES dbo.Users(UserId),
        ModifiedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderEvaluationSettings_ModifiedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF OBJECT_ID(N'dbo.TenderEvaluation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderEvaluation
    (
        EvaluationId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderEvaluation PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT UQ_TenderEvaluation_Tender UNIQUE
            CONSTRAINT FK_TenderEvaluation_Tender REFERENCES dbo.Tender(TenderId),
        EvaluationEligibleAtUtc DATETIME2(0) NOT NULL,
        EvaluationStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_TenderEvaluation_Status DEFAULT N'Awaiting Evaluation',
        AssignedTeam NVARCHAR(50) NOT NULL
            CONSTRAINT DF_TenderEvaluation_Team DEFAULT N'TenderSection',
        StartedAtUtc DATETIME2(0) NULL,
        CompletedAtUtc DATETIME2(0) NULL,
        TechnicalOutcome NVARCHAR(1000) NULL,
        FinancialOutcome NVARCHAR(1000) NULL,
        Observations NVARCHAR(4000) NULL,
        Recommendation NVARCHAR(2000) NULL,
        ReportFileName NVARCHAR(260) NULL,
        ReportFilePath NVARCHAR(400) NULL,
        UpdatedBy INT NULL
            CONSTRAINT FK_TenderEvaluation_UpdatedBy REFERENCES dbo.Users(UserId),
        UpdatedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderEvaluation_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TenderEvaluation_Status CHECK
            (EvaluationStatus IN (N'Awaiting Evaluation',N'Under Evaluation',N'Evaluation Completed'))
    );
    CREATE INDEX IX_TenderEvaluation_Eligibility
        ON dbo.TenderEvaluation(EvaluationStatus, EvaluationEligibleAtUtc);
END;
GO

IF OBJECT_ID(N'dbo.TenderEvaluationDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderEvaluationDocuments
    (
        EvaluationDocumentId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderEvaluationDocuments PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderEvaluationDocuments_Tender REFERENCES dbo.Tender(TenderId),
        FileName NVARCHAR(260) NOT NULL,
        FilePath NVARCHAR(400) NOT NULL,
        UploadedBy INT NOT NULL
            CONSTRAINT FK_TenderEvaluationDocuments_User REFERENCES dbo.Users(UserId),
        UploadedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderEvaluationDocuments_UploadedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_TenderEvaluationDocuments_Tender
        ON dbo.TenderEvaluationDocuments(TenderId, UploadedAtUtc);
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_TenderEvaluationDocuments_Immutable
ON dbo.TenderEvaluationDocuments
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51065, 'Tender evaluation document history is immutable.', 1;
END;
GO

IF OBJECT_ID(N'dbo.TenderLifecycleHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderLifecycleHistory
    (
        LifecycleHistoryId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderLifecycleHistory PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderLifecycleHistory_Tender REFERENCES dbo.Tender(TenderId),
        PreviousStatus NVARCHAR(50) NULL,
        NewStatus NVARCHAR(50) NULL,
        ActionName NVARCHAR(80) NOT NULL,
        ActionBy INT NOT NULL
            CONSTRAINT FK_TenderLifecycleHistory_User REFERENCES dbo.Users(UserId),
        RoleAtAction NVARCHAR(50) NOT NULL,
        DepartmentId INT NULL
            CONSTRAINT FK_TenderLifecycleHistory_Department REFERENCES dbo.Departments(DepartmentId),
        ActionAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderLifecycleHistory_ActionAt DEFAULT SYSUTCDATETIME(),
        Remarks NVARCHAR(2000) NULL,
        RelatedDocumentPath NVARCHAR(400) NULL
    );
    CREATE INDEX IX_TenderLifecycleHistory_TenderDate
        ON dbo.TenderLifecycleHistory(TenderId, ActionAtUtc);
END;
GO

IF OBJECT_ID(N'dbo.TenderBudgetOpinions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderBudgetOpinions
    (
        BudgetOpinionId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderBudgetOpinions PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderBudgetOpinions_Tender REFERENCES dbo.Tender(TenderId),
        Opinion NVARCHAR(10) NOT NULL,
        ApprovedAmount DECIMAL(18,2) NULL,
        Remarks NVARCHAR(1000) NULL,
        ApprovedBy INT NOT NULL
            CONSTRAINT FK_TenderBudgetOpinions_User REFERENCES dbo.Users(UserId),
        OpinionAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderBudgetOpinions_OpinionAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TenderBudgetOpinions_Opinion CHECK (Opinion IN (N'Positive',N'Negative')),
        CONSTRAINT CK_TenderBudgetOpinions_Amount CHECK
            ((Opinion=N'Positive' AND ApprovedAmount>0) OR (Opinion=N'Negative' AND ApprovedAmount IS NULL))
    );
    CREATE INDEX IX_TenderBudgetOpinions_TenderDate
        ON dbo.TenderBudgetOpinions(TenderId, OpinionAtUtc);
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_TenderBidOpening_Immutable
ON dbo.TenderBidOpening
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51020, 'Bid-opening audit records are immutable.', 1;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_TenderLifecycleHistory_Immutable
ON dbo.TenderLifecycleHistory
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51021, 'Tender lifecycle history records are immutable.', 1;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_TenderBudgetOpinions_Immutable
ON dbo.TenderBudgetOpinions
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51073, 'Tender budget-opinion history records are immutable.', 1;
END;
GO

PRINT 'Tender lifecycle tables are ready.';
GO

/* Migration 10: RBAC and version-2 workflow structures. */
/*
  Additive RBAC and finalized-workflow schema.
  Existing role assignments, tender statuses, and legacy workflow rows remain
  unchanged. New tenders opt into WorkflowVersion=2 in the Web Forms intake.
  No users, tender records, or mobile-number-based privileged assignments are
  created by this migration.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions
    (
        PermissionId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Permissions PRIMARY KEY,
        PermissionKey NVARCHAR(120) NOT NULL
            CONSTRAINT UQ_Permissions_PermissionKey UNIQUE,
        Description NVARCHAR(400) NOT NULL,
        IsActive BIT NOT NULL
            CONSTRAINT DF_Permissions_IsActive DEFAULT(1),
        CreatedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions
    (
        RoleId INT NOT NULL
            CONSTRAINT FK_RolePermissions_Role REFERENCES dbo.Roles(RoleId),
        PermissionId INT NOT NULL
            CONSTRAINT FK_RolePermissions_Permission REFERENCES dbo.Permissions(PermissionId),
        AssignedBy INT NULL
            CONSTRAINT FK_RolePermissions_AssignedBy REFERENCES dbo.Users(UserId),
        AssignedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_RolePermissions_AssignedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RolePermissions PRIMARY KEY(RoleId, PermissionId)
    );
END;

IF OBJECT_ID(N'dbo.SuperAdminProvisioningAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SuperAdminProvisioningAudit
    (
        ProvisioningAuditId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SuperAdminProvisioningAudit PRIMARY KEY,
        UserId INT NOT NULL
            CONSTRAINT FK_SuperAdminProvisioningAudit_User REFERENCES dbo.Users(UserId),
        PreviousRoleId INT NOT NULL
            CONSTRAINT FK_SuperAdminProvisioningAudit_PreviousRole REFERENCES dbo.Roles(RoleId),
        AssignedRoleId INT NOT NULL
            CONSTRAINT FK_SuperAdminProvisioningAudit_AssignedRole REFERENCES dbo.Roles(RoleId),
        AssignedBy INT NOT NULL
            CONSTRAINT FK_SuperAdminProvisioningAudit_AssignedBy REFERENCES dbo.Users(UserId),
        VerificationReference NVARCHAR(200) NOT NULL,
        AssignedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_SuperAdminProvisioningAudit_AssignedAtUtc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_SuperAdminProvisioningAudit_User
        ON dbo.SuperAdminProvisioningAudit(UserId, AssignedAtUtc DESC);
END;

IF COL_LENGTH(N'dbo.Roles', N'IsSystemRole') IS NULL
BEGIN
    ALTER TABLE dbo.Roles
        ADD IsSystemRole BIT NOT NULL
            CONSTRAINT DF_Roles_IsSystemRole DEFAULT(0) WITH VALUES;
END;
GO

IF COL_LENGTH(N'dbo.Tender', N'WorkflowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.Tender
        ADD WorkflowVersion SMALLINT NOT NULL
            CONSTRAINT DF_Tender_WorkflowVersion DEFAULT(1) WITH VALUES;
END;

IF COL_LENGTH(N'dbo.Tender', N'BudgetInformation') IS NULL
    ALTER TABLE dbo.Tender ADD BudgetInformation NVARCHAR(2000) NULL;
IF COL_LENGTH(N'dbo.Tender', N'TechnicalSanctionNumber') IS NULL
    ALTER TABLE dbo.Tender ADD TechnicalSanctionNumber NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Tender', N'TechnicalSanctionAuthority') IS NULL
    ALTER TABLE dbo.Tender ADD TechnicalSanctionAuthority NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Tender', N'TechnicalSanctionDate') IS NULL
    ALTER TABLE dbo.Tender ADD TechnicalSanctionDate DATE NULL;
IF COL_LENGTH(N'dbo.Tender', N'PreBidMeetingUtc') IS NULL
    ALTER TABLE dbo.Tender ADD PreBidMeetingUtc DATETIME2(0) NULL;
IF COL_LENGTH(N'dbo.Tender', N'PreBidLocation') IS NULL
    ALTER TABLE dbo.Tender ADD PreBidLocation NVARCHAR(300) NULL;
IF COL_LENGTH(N'dbo.Tender', N'PreBidDetails') IS NULL
    ALTER TABLE dbo.Tender ADD PreBidDetails NVARCHAR(2000) NULL;
IF COL_LENGTH(N'dbo.Tender', N'TechnicalBidOpeningScheduledUtc') IS NULL
    ALTER TABLE dbo.Tender ADD TechnicalBidOpeningScheduledUtc DATETIME2(0) NULL;

IF COL_LENGTH(N'dbo.WorkflowStages', N'WorkflowVersion') IS NULL
BEGIN
    ALTER TABLE dbo.WorkflowStages
        ADD WorkflowVersion SMALLINT NOT NULL
            CONSTRAINT DF_WorkflowStages_WorkflowVersion DEFAULT(1) WITH VALUES;
END;
GO

IF OBJECT_ID(N'dbo.TenderWorkflowAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderWorkflowAssignments
    (
        AssignmentId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderWorkflowAssignments PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowAssignments_Tender REFERENCES dbo.Tender(TenderId),
        ShortfallId BIGINT NULL,
        StageId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowAssignments_Stage REFERENCES dbo.WorkflowStages(StageId),
        AssignedRoleId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowAssignments_Role REFERENCES dbo.Roles(RoleId),
        AssignedUserId INT NULL
            CONSTRAINT FK_TenderWorkflowAssignments_User REFERENCES dbo.Users(UserId),
        DepartmentId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowAssignments_Department REFERENCES dbo.Departments(DepartmentId),
        AssignedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderWorkflowAssignments_AssignedAtUtc DEFAULT SYSUTCDATETIME(),
        CompletedAtUtc DATETIME2(0) NULL,
        IsCurrent BIT NOT NULL
            CONSTRAINT DF_TenderWorkflowAssignments_IsCurrent DEFAULT(1)
    );
    CREATE UNIQUE INDEX UX_TenderWorkflowAssignments_Current
        ON dbo.TenderWorkflowAssignments(TenderId,ShortfallId) WHERE IsCurrent=1;
    CREATE INDEX IX_TenderWorkflowAssignments_Queue
        ON dbo.TenderWorkflowAssignments(AssignedRoleId,DepartmentId,IsCurrent,AssignedAtUtc);
END;

IF OBJECT_ID(N'dbo.TenderWorkflowHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderWorkflowHistory
    (
        WorkflowHistoryId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderWorkflowHistory PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowHistory_Tender REFERENCES dbo.Tender(TenderId),
        WorkflowVersion SMALLINT NOT NULL,
        StageId INT NULL
            CONSTRAINT FK_TenderWorkflowHistory_Stage REFERENCES dbo.WorkflowStages(StageId),
        PreviousStatus NVARCHAR(100) NULL,
        NewStatus NVARCHAR(100) NOT NULL,
        ActionName NVARCHAR(100) NOT NULL,
        UserId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowHistory_User REFERENCES dbo.Users(UserId),
        RoleId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowHistory_Role REFERENCES dbo.Roles(RoleId),
        ActorRoleCode NVARCHAR(50) NULL,
        DepartmentId INT NOT NULL
            CONSTRAINT FK_TenderWorkflowHistory_Department REFERENCES dbo.Departments(DepartmentId),
        ActionAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderWorkflowHistory_ActionAtUtc DEFAULT SYSUTCDATETIME(),
        Remarks NVARCHAR(2000) NULL,
        RelatedDocumentPath NVARCHAR(800) NULL
    );
    CREATE INDEX IX_TenderWorkflowHistory_Tender
        ON dbo.TenderWorkflowHistory(TenderId,WorkflowHistoryId DESC);
END;

IF OBJECT_ID(N'dbo.TenderVerificationReviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderVerificationReviews
    (
        VerificationReviewId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderVerificationReviews PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderVerificationReviews_Tender REFERENCES dbo.Tender(TenderId),
        ContractorId INT NOT NULL
            CONSTRAINT FK_TenderVerificationReviews_Contractor REFERENCES dbo.Contractors(ContractorId),
        ReviewStage NVARCHAR(20) NOT NULL,
        Decision NVARCHAR(30) NOT NULL,
        ReviewerId INT NOT NULL
            CONSTRAINT FK_TenderVerificationReviews_Reviewer REFERENCES dbo.Users(UserId),
        ReviewerRoleId INT NOT NULL
            CONSTRAINT FK_TenderVerificationReviews_Role REFERENCES dbo.Roles(RoleId),
        Remarks NVARCHAR(2000) NULL,
        ReviewedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderVerificationReviews_ReviewedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TenderVerificationReviews_Stage CHECK (ReviewStage IN (N'Primary',N'Secondary')),
        CONSTRAINT CK_TenderVerificationReviews_Decision CHECK (Decision IN (N'Completed',N'Approved',N'Shortfall'))
    );
    CREATE INDEX IX_TenderVerificationReviews_Current
        ON dbo.TenderVerificationReviews(TenderId,ContractorId,ReviewStage,ReviewedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.TenderDocumentVerificationHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderDocumentVerificationHistory
    (
        DocumentVerificationHistoryId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderDocumentVerificationHistory PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderDocumentVerificationHistory_Tender REFERENCES dbo.Tender(TenderId),
        ContractorId INT NOT NULL
            CONSTRAINT FK_TenderDocumentVerificationHistory_Contractor REFERENCES dbo.Contractors(ContractorId),
        TechnicalBidDocumentId INT NULL
            CONSTRAINT FK_TenderDocumentVerificationHistory_Document REFERENCES dbo.TechnicalBidDocuments(TechnicalBidDocumentId),
        FileName NVARCHAR(520) NOT NULL,
        FilePath NVARCHAR(800) NOT NULL,
        DocumentType NVARCHAR(160) NOT NULL,
        ReviewStage NVARCHAR(20) NOT NULL,
        VerificationStatus NVARCHAR(20) NOT NULL,
        ReviewerId INT NOT NULL
            CONSTRAINT FK_TenderDocumentVerificationHistory_Reviewer REFERENCES dbo.Users(UserId),
        ReviewerRoleId INT NOT NULL
            CONSTRAINT FK_TenderDocumentVerificationHistory_Role REFERENCES dbo.Roles(RoleId),
        Remarks NVARCHAR(2000) NULL,
        ReviewedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderDocumentVerificationHistory_ReviewedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TenderDocumentVerificationHistory_Stage CHECK (ReviewStage IN (N'Primary',N'Secondary')),
        CONSTRAINT CK_TenderDocumentVerificationHistory_Status CHECK (VerificationStatus IN (N'Verified',N'Not Verified'))
    );
    CREATE INDEX IX_TenderDocumentVerificationHistory_Bid
        ON dbo.TenderDocumentVerificationHistory(TenderId,ContractorId,TechnicalBidDocumentId,ReviewStage,ReviewedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.TenderShortfalls', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderShortfalls
    (
        ShortfallId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderShortfalls PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderShortfalls_Tender REFERENCES dbo.Tender(TenderId),
        ContractorId INT NOT NULL
            CONSTRAINT FK_TenderShortfalls_Contractor REFERENCES dbo.Contractors(ContractorId),
        SubmissionDurationDays DECIMAL(8,2) NOT NULL,
        SubmissionDeadlineUtc DATETIME2(0) NOT NULL,
        Status NVARCHAR(40) NOT NULL
            CONSTRAINT DF_TenderShortfalls_Status DEFAULT(N'Pending JE Approval'),
        CreatedBy INT NOT NULL
            CONSTRAINT FK_TenderShortfalls_CreatedBy REFERENCES dbo.Users(UserId),
        CreatedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderShortfalls_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        JeApprovedBy INT NULL
            CONSTRAINT FK_TenderShortfalls_JeApprovedBy REFERENCES dbo.Users(UserId),
        JeApprovedAtUtc DATETIME2(0) NULL,
        ContractorSubmittedAtUtc DATETIME2(0) NULL,
        ClearanceStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_TenderShortfalls_ClearanceStatus DEFAULT(N'Pending'),
        ClearedBy INT NULL
            CONSTRAINT FK_TenderShortfalls_ClearedBy REFERENCES dbo.Users(UserId),
        ClearedAtUtc DATETIME2(0) NULL,
        Remarks NVARCHAR(2000) NULL,
        CONSTRAINT CK_TenderShortfalls_Duration CHECK (SubmissionDurationDays>0),
        CONSTRAINT CK_TenderShortfalls_Status CHECK
            (Status IN (N'Pending JE Approval',N'Awaiting Contractor',N'Documents Submitted',N'Cleared',N'Rejected')),
        CONSTRAINT CK_TenderShortfalls_Clearance CHECK
            (ClearanceStatus IN (N'Pending',N'Submitted',N'Cleared',N'Continued Deficiency'))
    );
    CREATE INDEX IX_TenderShortfalls_Contractor
        ON dbo.TenderShortfalls(ContractorId,Status,SubmissionDeadlineUtc);
    CREATE INDEX IX_TenderShortfalls_Tender
        ON dbo.TenderShortfalls(TenderId,ContractorId,CreatedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.TenderShortfallItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderShortfallItems
    (
        ShortfallItemId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderShortfallItems PRIMARY KEY,
        ShortfallId BIGINT NOT NULL
            CONSTRAINT FK_TenderShortfallItems_Shortfall REFERENCES dbo.TenderShortfalls(ShortfallId),
        TechnicalBidDocumentId INT NULL
            CONSTRAINT FK_TenderShortfallItems_Document REFERENCES dbo.TechnicalBidDocuments(TechnicalBidDocumentId),
        RequiredDocumentType NVARCHAR(160) NOT NULL,
        ShortfallDescription NVARCHAR(2000) NOT NULL,
        CorrectiveAction NVARCHAR(2000) NOT NULL,
        ClearanceStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_TenderShortfallItems_ClearanceStatus DEFAULT(N'Pending'),
        Remarks NVARCHAR(2000) NULL,
        CONSTRAINT CK_TenderShortfallItems_Clearance CHECK
            (ClearanceStatus IN (N'Pending',N'Submitted',N'Cleared',N'Continued Deficiency'))
    );
    CREATE INDEX IX_TenderShortfallItems_Shortfall
        ON dbo.TenderShortfallItems(ShortfallId,ClearanceStatus);
END;

IF OBJECT_ID(N'dbo.TenderShortfallSubmissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderShortfallSubmissions
    (
        ShortfallSubmissionId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderShortfallSubmissions PRIMARY KEY,
        ShortfallItemId BIGINT NOT NULL
            CONSTRAINT FK_TenderShortfallSubmissions_Item REFERENCES dbo.TenderShortfallItems(ShortfallItemId),
        FileName NVARCHAR(520) NOT NULL,
        FilePath NVARCHAR(800) NOT NULL,
        SubmittedBy INT NOT NULL
            CONSTRAINT FK_TenderShortfallSubmissions_User REFERENCES dbo.Users(UserId),
        SubmittedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderShortfallSubmissions_SubmittedAtUtc DEFAULT SYSUTCDATETIME(),
        Remarks NVARCHAR(1000) NULL
    );
    CREATE INDEX IX_TenderShortfallSubmissions_Item
        ON dbo.TenderShortfallSubmissions(ShortfallItemId,SubmittedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.TenderFinancialBidOpening', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderFinancialBidOpening
    (
        FinancialBidOpeningId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderFinancialBidOpening PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT UQ_TenderFinancialBidOpening_Tender UNIQUE
            CONSTRAINT FK_TenderFinancialBidOpening_Tender REFERENCES dbo.Tender(TenderId),
        ActualOpeningUtc DATETIME2(0) NOT NULL,
        OpenedBy INT NOT NULL
            CONSTRAINT FK_TenderFinancialBidOpening_User REFERENCES dbo.Users(UserId),
        OpeningRemarks NVARCHAR(2000) NULL,
        OpeningStatus NVARCHAR(30) NOT NULL
            CONSTRAINT DF_TenderFinancialBidOpening_Status DEFAULT(N'Opened'),
        CONSTRAINT CK_TenderFinancialBidOpening_Status CHECK (OpeningStatus=N'Opened')
    );
END;

IF OBJECT_ID(N'dbo.TenderFinancialBidOpeningDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderFinancialBidOpeningDetails
    (
        FinancialBidOpeningDetailId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderFinancialBidOpeningDetails PRIMARY KEY,
        FinancialBidOpeningId BIGINT NOT NULL
            CONSTRAINT FK_TenderFinancialBidOpeningDetails_Opening REFERENCES dbo.TenderFinancialBidOpening(FinancialBidOpeningId),
        FinancialBidId INT NOT NULL
            CONSTRAINT FK_TenderFinancialBidOpeningDetails_Bid REFERENCES dbo.FinancialBid(FinancialBidId),
        QuotedAmount DECIMAL(18,2) NOT NULL,
        Remarks NVARCHAR(1000) NULL,
        CONSTRAINT UQ_TenderFinancialBidOpeningDetails_Bid UNIQUE(FinancialBidId)
    );
END;

IF OBJECT_ID(N'dbo.TenderFinancialBidDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderFinancialBidDocuments
    (
        FinancialBidDocumentId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderFinancialBidDocuments PRIMARY KEY,
        FinancialBidId INT NOT NULL
            CONSTRAINT FK_TenderFinancialBidDocuments_Bid REFERENCES dbo.FinancialBid(FinancialBidId),
        FileName NVARCHAR(520) NOT NULL,
        FilePath NVARCHAR(800) NOT NULL,
        UploadedBy INT NOT NULL
            CONSTRAINT FK_TenderFinancialBidDocuments_User REFERENCES dbo.Users(UserId),
        UploadedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderFinancialBidDocuments_UploadedAtUtc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_TenderFinancialBidDocuments_Bid
        ON dbo.TenderFinancialBidDocuments(FinancialBidId,UploadedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.TenderFinancialApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderFinancialApprovals
    (
        FinancialApprovalId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderFinancialApprovals PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderFinancialApprovals_Tender REFERENCES dbo.Tender(TenderId),
        Decision NVARCHAR(20) NOT NULL,
        ApprovedAmount DECIMAL(18,2) NULL,
        ApprovedBy INT NOT NULL
            CONSTRAINT FK_TenderFinancialApprovals_User REFERENCES dbo.Users(UserId),
        ApprovedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderFinancialApprovals_ApprovedAtUtc DEFAULT SYSUTCDATETIME(),
        Remarks NVARCHAR(2000) NULL,
        SupportingDocumentPath NVARCHAR(800) NULL,
        CONSTRAINT CK_TenderFinancialApprovals_Decision CHECK (Decision IN (N'Approved',N'Returned'))
    );
    CREATE INDEX IX_TenderFinancialApprovals_Tender
        ON dbo.TenderFinancialApprovals(TenderId,ApprovedAtUtc DESC);
END;

IF OBJECT_ID(N'dbo.TenderNotifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderNotifications
    (
        NotificationId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderNotifications PRIMARY KEY,
        UserId INT NOT NULL
            CONSTRAINT FK_TenderNotifications_User REFERENCES dbo.Users(UserId),
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderNotifications_Tender REFERENCES dbo.Tender(TenderId),
        ShortfallId BIGINT NULL
            CONSTRAINT FK_TenderNotifications_Shortfall REFERENCES dbo.TenderShortfalls(ShortfallId),
        NotificationType NVARCHAR(60) NOT NULL,
        Message NVARCHAR(1000) NOT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderNotifications_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ReadAtUtc DATETIME2(0) NULL
    );
    CREATE INDEX IX_TenderNotifications_User
        ON dbo.TenderNotifications(UserId,ReadAtUtc,CreatedAtUtc DESC);
END;

IF COL_LENGTH(N'dbo.WorkOrder', N'WorkDescription') IS NULL
    ALTER TABLE dbo.WorkOrder ADD WorkDescription NVARCHAR(2000) NULL;
IF COL_LENGTH(N'dbo.WorkOrder', N'DocumentPath') IS NULL
    ALTER TABLE dbo.WorkOrder ADD DocumentPath NVARCHAR(800) NULL;
IF COL_LENGTH(N'dbo.WorkOrder', N'TermsAndConditions') IS NULL
    ALTER TABLE dbo.WorkOrder ADD TermsAndConditions NVARCHAR(4000) NULL;
IF COL_LENGTH(N'dbo.WorkOrder', N'AuthorizedSignatory') IS NULL
    ALTER TABLE dbo.WorkOrder ADD AuthorizedSignatory NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.Agreement', N'ApprovedAmount') IS NULL
    ALTER TABLE dbo.Agreement ADD ApprovedAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Agreement', N'TermsAndConditions') IS NULL
    ALTER TABLE dbo.Agreement ADD TermsAndConditions NVARCHAR(4000) NULL;
IF COL_LENGTH(N'dbo.Agreement', N'AuthorizedSignatory') IS NULL
    ALTER TABLE dbo.Agreement ADD AuthorizedSignatory NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.TenderFinancialApprovals', N'SelectedFinancialBidId') IS NULL
    ALTER TABLE dbo.TenderFinancialApprovals ADD SelectedFinancialBidId INT NULL;
IF COL_LENGTH(N'dbo.TenderWorkflowHistory', N'ActorRoleCode') IS NULL
    ALTER TABLE dbo.TenderWorkflowHistory ADD ActorRoleCode NVARCHAR(50) NULL;
IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id=OBJECT_ID(N'dbo.TenderFinancialApprovals')
      AND name=N'FK_TenderFinancialApprovals_SelectedFinancialBid'
)
    ALTER TABLE dbo.TenderFinancialApprovals
        ADD CONSTRAINT FK_TenderFinancialApprovals_SelectedFinancialBid
            FOREIGN KEY (SelectedFinancialBidId) REFERENCES dbo.FinancialBid(FinancialBidId);

IF OBJECT_ID(N'dbo.TenderGeneratedDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderGeneratedDocuments
    (
        GeneratedDocumentId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TenderGeneratedDocuments PRIMARY KEY,
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderGeneratedDocuments_Tender REFERENCES dbo.Tender(TenderId),
        WorkOrderId INT NULL
            CONSTRAINT FK_TenderGeneratedDocuments_WorkOrder REFERENCES dbo.WorkOrder(WorkOrderId),
        AgreementId INT NULL
            CONSTRAINT FK_TenderGeneratedDocuments_Agreement REFERENCES dbo.Agreement(AgreementId),
        DocumentKind NVARCHAR(20) NOT NULL,
        VersionNumber INT NOT NULL,
        FileName NVARCHAR(520) NOT NULL,
        FilePath NVARCHAR(800) NOT NULL,
        GeneratedBy INT NOT NULL
            CONSTRAINT FK_TenderGeneratedDocuments_User REFERENCES dbo.Users(UserId),
        GeneratedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderGeneratedDocuments_GeneratedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_TenderGeneratedDocuments_Kind CHECK (DocumentKind IN (N'WorkOrder',N'Agreement')),
        CONSTRAINT CK_TenderGeneratedDocuments_Version CHECK (VersionNumber>0),
        CONSTRAINT CK_TenderGeneratedDocuments_Target CHECK
            ((DocumentKind=N'WorkOrder' AND WorkOrderId IS NOT NULL AND AgreementId IS NULL)
             OR (DocumentKind=N'Agreement' AND AgreementId IS NOT NULL AND WorkOrderId IS NULL))
    );
    CREATE UNIQUE INDEX UX_TenderGeneratedDocuments_WorkOrderVersion
        ON dbo.TenderGeneratedDocuments(WorkOrderId,VersionNumber) WHERE WorkOrderId IS NOT NULL;
    CREATE UNIQUE INDEX UX_TenderGeneratedDocuments_AgreementVersion
        ON dbo.TenderGeneratedDocuments(AgreementId,VersionNumber) WHERE AgreementId IS NOT NULL;
END;

COMMIT TRANSACTION;
PRINT 'RBAC and finalized tender workflow schema installed. No role, permission, or workflow-stage rows were seeded.';

/* Migration 11: bidder-document metadata. */
/*
  Add metadata required for secure, auditable bidder-document uploads.
  Existing document rows are retained and labeled with a neutral type.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.TechnicalBidDocuments', N'U') IS NULL
    THROW 51100, 'Apply migration 08 before migration 11.', 1;

IF COL_LENGTH(N'dbo.TechnicalBidDocuments', N'DocumentType') IS NULL
BEGIN
    ALTER TABLE dbo.TechnicalBidDocuments
        ADD DocumentType NVARCHAR(160) NOT NULL
            CONSTRAINT DF_TechnicalBidDocuments_DocumentType DEFAULT (N'Bid document') WITH VALUES;
END;

IF COL_LENGTH(N'dbo.TechnicalBidDocuments', N'UploadedBy') IS NULL
    ALTER TABLE dbo.TechnicalBidDocuments ADD UploadedBy INT NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.TechnicalBidDocuments')
      AND name = N'FK_TechnicalBidDocuments_UploadedBy'
)
BEGIN
    ALTER TABLE dbo.TechnicalBidDocuments
        ADD CONSTRAINT FK_TechnicalBidDocuments_UploadedBy
            FOREIGN KEY (UploadedBy) REFERENCES dbo.Users(UserId);
END;
GO

/* Migration 12: tender-specific required bidder documents. */
/*
  Store the required technical-bid document types configured for each tender
  before publication. This does not seed or modify tender records.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 51200, 'Apply the base schema before migration 12.', 1;

IF OBJECT_ID(N'dbo.TenderRequiredBidDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderRequiredBidDocuments
    (
        TenderId INT NOT NULL
            CONSTRAINT FK_TenderRequiredBidDocuments_Tender REFERENCES dbo.Tender(TenderId),
        DocumentType NVARCHAR(160) NOT NULL,
        CreatedBy INT NOT NULL
            CONSTRAINT FK_TenderRequiredBidDocuments_CreatedBy REFERENCES dbo.Users(UserId),
        CreatedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderRequiredBidDocuments_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TenderRequiredBidDocuments PRIMARY KEY (TenderId,DocumentType)
    );
END;
GO

/* Migration 15: tender eligibility and application document requirements. */
IF COL_LENGTH(N'dbo.Tender', N'EligibilityCriteria') IS NULL
    ALTER TABLE dbo.Tender ADD EligibilityCriteria NVARCHAR(4000) NULL;

IF COL_LENGTH(N'dbo.TenderRequiredBidDocuments', N'Description') IS NULL
    ALTER TABLE dbo.TenderRequiredBidDocuments ADD Description NVARCHAR(1000) NULL;

IF COL_LENGTH(N'dbo.TenderRequiredBidDocuments', N'IsMandatory') IS NULL
BEGIN
    ALTER TABLE dbo.TenderRequiredBidDocuments
        ADD IsMandatory BIT NOT NULL
            CONSTRAINT DF_TenderRequiredBidDocuments_IsMandatory DEFAULT (1) WITH VALUES;
END;

IF COL_LENGTH(N'dbo.TenderRequiredBidDocuments', N'AllowedExtensions') IS NULL
    ALTER TABLE dbo.TenderRequiredBidDocuments ADD AllowedExtensions NVARCHAR(500) NULL;

IF COL_LENGTH(N'dbo.TenderRequiredBidDocuments', N'MaxFileSizeBytes') IS NULL
BEGIN
    ALTER TABLE dbo.TenderRequiredBidDocuments
        ADD MaxFileSizeBytes INT NOT NULL
            CONSTRAINT DF_TenderRequiredBidDocuments_MaxFileSizeBytes DEFAULT (10000000) WITH VALUES;
END;
GO

/* Migration 16: immutable contractor application history and remarks. */
IF OBJECT_ID(N'dbo.ContractorApplicationHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContractorApplicationHistory
    (
        HistoryId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ContractorApplicationHistory PRIMARY KEY,
        TechnicalBidId INT NOT NULL
            CONSTRAINT FK_ContractorApplicationHistory_TechnicalBid
                REFERENCES dbo.TechnicalBid(TechnicalBidId) ON DELETE CASCADE,
        EventType NVARCHAR(40) NOT NULL,
        PreviousStatus NVARCHAR(30) NULL,
        NewStatus NVARCHAR(30) NULL,
        Remarks NVARCHAR(2000) NULL,
        CreatedBy INT NOT NULL
            CONSTRAINT FK_ContractorApplicationHistory_User REFERENCES dbo.Users(UserId),
        RoleId INT NULL
            CONSTRAINT FK_ContractorApplicationHistory_Role REFERENCES dbo.Roles(RoleId),
        VisibleToContractor BIT NOT NULL
            CONSTRAINT DF_ContractorApplicationHistory_VisibleToContractor DEFAULT (0),
        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_ContractorApplicationHistory_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ContractorApplicationHistory_TechnicalBidId_CreatedAtUtc
        ON dbo.ContractorApplicationHistory(TechnicalBidId,CreatedAtUtc);
END;

IF OBJECT_ID(N'dbo.ContractorApplicationRemarks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContractorApplicationRemarks
    (
        RemarkId BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ContractorApplicationRemarks PRIMARY KEY,
        TechnicalBidId INT NOT NULL
            CONSTRAINT FK_ContractorApplicationRemarks_TechnicalBid
                REFERENCES dbo.TechnicalBid(TechnicalBidId) ON DELETE CASCADE,
        RemarkType NVARCHAR(30) NOT NULL,
        Message NVARCHAR(2000) NOT NULL,
        CreatedBy INT NOT NULL
            CONSTRAINT FK_ContractorApplicationRemarks_User REFERENCES dbo.Users(UserId),
        RoleId INT NULL
            CONSTRAINT FK_ContractorApplicationRemarks_Role REFERENCES dbo.Roles(RoleId),
        VisibleToContractor BIT NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_ContractorApplicationRemarks_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ContractorApplicationRemarks_TechnicalBidId_CreatedAtUtc
        ON dbo.ContractorApplicationRemarks(TechnicalBidId,CreatedAtUtc);
END;
GO

/* Migration 17: role codes used by role-aware Web Forms authorization. */
IF COL_LENGTH(N'dbo.Roles', N'RoleCode') IS NULL
    ALTER TABLE dbo.Roles ADD RoleCode NVARCHAR(50) NULL;
GO

/* Migration 18: configure the complete four-role version-2 transition map. */
IF EXISTS
(
    SELECT 1 FROM dbo.Roles
    WHERE RoleCode IN (N'JE',N'TENDER_CELL',N'CHAIRMAN',N'DIRECTOR_AUDITOR')
      AND IsActive=1
)
BEGIN
    IF
    (
        SELECT COUNT(DISTINCT RoleCode) FROM dbo.Roles
        WHERE RoleCode IN (N'JE',N'TENDER_CELL',N'CHAIRMAN',N'DIRECTOR_AUDITOR')
          AND IsActive=1
    ) <> 4
        THROW 51009, 'Migration 18 requires all four active canonical workflow roles.', 1;

    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1 FROM dbo.Tender t
        WHERE t.WorkflowVersion=2 AND
        (
            t.IsDeleted=1
            OR t.CurrentStatus<>N'Draft'
            OR (SELECT COUNT(*) FROM dbo.TenderWorkflowAssignments a
                WHERE a.TenderId=t.TenderId AND a.IsCurrent=1)<>1
            OR NOT EXISTS
            (
                SELECT 1
                FROM dbo.TenderWorkflowAssignments a
                INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
                INNER JOIN dbo.Roles r ON r.RoleId=a.AssignedRoleId
                WHERE a.TenderId=t.TenderId AND a.IsCurrent=1 AND a.ShortfallId IS NULL
                  AND a.DepartmentId=t.DepartmentId AND s.WorkflowVersion=2
                  AND s.StageOrder=101 AND s.StageName=N'Final_JE_SubmitTender'
                  AND r.RoleCode=N'JE' AND r.IsActive=1
            )
        )
    )
    OR EXISTS
    (
        SELECT 1 FROM dbo.TenderWorkflowHistory
        WHERE WorkflowVersion=2
    )
    OR EXISTS
    (
        SELECT 1 FROM dbo.TenderWorkflowAssignments a
        INNER JOIN dbo.Tender t ON t.TenderId=a.TenderId AND t.WorkflowVersion=2
        INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
        WHERE a.IsCurrent=0 OR s.StageOrder<>101
    )
    OR EXISTS
    (
        SELECT 1 FROM dbo.TechnicalBid b
        INNER JOIN dbo.Tender t ON t.TenderId=b.TenderId AND t.WorkflowVersion=2
    )
        THROW 51010, 'Migration 18 can only remap untouched version-2 drafts still assigned to the initial JE stage. Rehearse any active workflow transition separately.', 1;

    IF EXISTS
    (
        SELECT StageOrder FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 GROUP BY StageOrder HAVING COUNT(*)>1
    )
        THROW 51011, 'Migration 18 found duplicate version-2 stage orders.', 1;

    CREATE TABLE #ExpectedWorkflowStages
    (
        StageOrder INT NOT NULL PRIMARY KEY,
        StageName NVARCHAR(80) NOT NULL,
        StatusOnPending NVARCHAR(50) NOT NULL,
        StatusOnApprove NVARCHAR(50) NOT NULL,
        StatusOnReject NVARCHAR(50) NOT NULL,
        RoleCode NVARCHAR(50) NOT NULL
    );
    INSERT INTO #ExpectedWorkflowStages
        (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,RoleCode)
    VALUES
        (101,N'Final_JE_SubmitTender',N'Draft',N'Submitted',N'Draft',N'JE'),
        (102,N'Final_JE_PublishTender',N'Submitted',N'Tender Published',N'Submitted',N'JE'),
        (103,N'Final_JE_OpenTechnicalBids',N'Tender Published',N'Primary Verification Pending',N'Tender Published',N'JE'),
        (104,N'Final_JE_CompletePrimaryVerification',N'Primary Verification Pending',N'Primary Verification Completed',N'Primary Verification Pending',N'JE'),
        (105,N'Final_JE_ForwardToTenderCell',N'Primary Verification Completed',N'Tender Cell Verification Pending',N'Primary Verification Completed',N'JE'),
        (106,N'Final_Cell_CompleteSecondaryVerification',N'Tender Cell Verification Pending',N'Financial Bid Opening Approval Pending',N'Shortfall Generation Pending',N'TENDER_CELL'),
        (107,N'Final_Cell_CreateShortfall',N'Shortfall Generation Pending',N'Shortfall Pending JE Approval',N'Tender Cell Verification Pending',N'TENDER_CELL'),
        (108,N'Final_JE_ApproveShortfall',N'Shortfall Pending JE Approval',N'Awaiting Contractor Submission',N'Shortfall Generation Pending',N'JE'),
        (109,N'Final_Contractor_SubmitShortfall',N'Awaiting Contractor Submission',N'Shortfall Documents Submitted',N'Awaiting Contractor Submission',N'TENDER_CELL'),
        (110,N'Final_Cell_ClearShortfall',N'Shortfall Documents Submitted',N'Shortfall Cleared',N'Awaiting Contractor Submission',N'TENDER_CELL'),
        (111,N'Final_Cell_ResumeVerification',N'Shortfall Cleared',N'Tender Cell Verification Pending',N'Shortfall Cleared',N'TENDER_CELL'),
        (112,N'Final_Samiti_ApproveFinancialOpening',N'Financial Bid Opening Approval Pending',N'Financial Bid Opening Approved',N'Financial Bid Opening Rejected',N'CHAIRMAN'),
        (113,N'Final_Cell_ReverifyRejectedOpening',N'Financial Bid Opening Rejected',N'Financial Bid Opening Approval Pending',N'Tender Cell Verification Pending',N'TENDER_CELL'),
        (114,N'Final_JE_OpenFinancialBids',N'Financial Bid Opening Approved',N'Final Financial Approval Pending',N'Financial Bid Opening Approved',N'JE'),
        (115,N'Final_Auditor_ApproveFinancialBids',N'Final Financial Approval Pending',N'Final Financial Approval Completed',N'Final Financial Approval Rejected',N'DIRECTOR_AUDITOR'),
        (116,N'Final_JE_ResubmitFinancialApproval',N'Final Financial Approval Rejected',N'Final Financial Approval Pending',N'Final Financial Approval Rejected',N'JE'),
        (117,N'Final_JE_StartWorkOrder',N'Final Financial Approval Completed',N'Work Order Pending',N'Final Financial Approval Completed',N'JE'),
        (118,N'Final_JE_GenerateWorkOrder',N'Work Order Pending',N'Work Order Generated',N'Work Order Pending',N'JE'),
        (119,N'Final_JE_StartAgreement',N'Work Order Generated',N'Tender Agreement Pending',N'Work Order Generated',N'JE'),
        (120,N'Final_JE_GenerateAgreement',N'Tender Agreement Pending',N'Tender Agreement Generated',N'Tender Agreement Pending',N'JE'),
        (121,N'Final_JE_CompleteTender',N'Tender Agreement Generated',N'Completed',N'Tender Agreement Generated',N'JE');

    UPDATE dbo.WorkflowStages
    SET StageName=N'__M18_' + CONVERT(NVARCHAR(20),StageId),IsActive=0
    WHERE WorkflowVersion=2;

    UPDATE stage
    SET StageOrder=expected.StageOrder,StageName=expected.StageName,
        StatusOnPending=expected.StatusOnPending,StatusOnApprove=expected.StatusOnApprove,
        StatusOnReject=expected.StatusOnReject,ApproverRoleId=role.RoleId,IsActive=1
    FROM dbo.WorkflowStages stage
    INNER JOIN #ExpectedWorkflowStages expected ON expected.StageOrder=stage.StageOrder
    INNER JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
    WHERE stage.WorkflowVersion=2;

    INSERT INTO dbo.WorkflowStages
        (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,ApproverRoleId,IsActive,WorkflowVersion)
    SELECT expected.StageOrder,expected.StageName,expected.StatusOnPending,expected.StatusOnApprove,
           expected.StatusOnReject,role.RoleId,1,2
    FROM #ExpectedWorkflowStages expected
    INNER JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages stage
        WHERE stage.WorkflowVersion=2 AND stage.StageOrder=expected.StageOrder
    );

    DECLARE @JeRoleId INT, @CellRoleId INT, @PrimaryVerifyPermissionId INT,
            @ShortfallApprovePermissionId INT;
    SELECT @JeRoleId=RoleId FROM dbo.Roles WHERE RoleCode=N'JE' AND IsActive=1;
    SELECT @CellRoleId=RoleId FROM dbo.Roles WHERE RoleCode=N'TENDER_CELL' AND IsActive=1;
    SELECT @PrimaryVerifyPermissionId=PermissionId FROM dbo.Permissions
        WHERE PermissionKey=N'tender.primary.verify' AND IsActive=1;
    SELECT @ShortfallApprovePermissionId=PermissionId FROM dbo.Permissions
        WHERE PermissionKey=N'shortfall.approve' AND IsActive=1;
    IF @JeRoleId IS NULL OR @CellRoleId IS NULL OR
       @PrimaryVerifyPermissionId IS NULL OR @ShortfallApprovePermissionId IS NULL
        THROW 51012, 'Migration 18 requires the JE/Tender Cell roles and primary-verification/shortfall-approval permissions.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.RolePermissions
        WHERE RoleId=@JeRoleId AND PermissionId=@PrimaryVerifyPermissionId
    )
        INSERT INTO dbo.RolePermissions(RoleId,PermissionId)
        VALUES(@JeRoleId,@PrimaryVerifyPermissionId);

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.RolePermissions
        WHERE RoleId=@JeRoleId AND PermissionId=@ShortfallApprovePermissionId
    )
        INSERT INTO dbo.RolePermissions(RoleId,PermissionId)
        VALUES(@JeRoleId,@ShortfallApprovePermissionId);

    DELETE FROM dbo.RolePermissions
    WHERE RoleId=@CellRoleId AND PermissionId=@ShortfallApprovePermissionId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

/* Complete the setup ledger only after every schema/data stage succeeded. */
INSERT INTO dbo.TenderSchemaMigrations(MigrationId,Description)
SELECT v.MigrationId,v.Description
FROM (VALUES
    (N'00-CORE',N'Core schema and compatibility objects'),
    (N'04',N'Announcements and department head-officer column'),
    (N'05',N'Technical evaluation role/stage alignment'),
    (N'06',N'Tender submission start date'),
    (N'08',N'Technical bid terms and multi-document structures'),
    (N'09',N'Tender lifecycle tables and immutability triggers'),
    (N'10',N'RBAC, version-2 workflow, verification, shortfall, financial, and generated-document schema'),
    (N'11',N'Technical bid document metadata'),
    (N'12',N'Per-tender required bidder document types'),
    (N'15',N'Tender eligibility and application document requirements'),
    (N'16',N'Immutable contractor application history and remarks'),
    (N'17',N'Role codes used by role-aware authorization'),
    (N'18',N'Complete four-role version-2 workflow transitions')
) v(MigrationId,Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.TenderSchemaMigrations m WHERE m.MigrationId=v.MigrationId
);
GO
IF N'$(InstallMode)'=N'Fresh' AND EXISTS
(
    SELECT 1
    FROM sys.tables t
    CROSS APPLY
    (
        SELECT SUM(CONVERT(BIGINT,p.row_count)) AS RecordCount
        FROM sys.dm_db_partition_stats p
        WHERE p.object_id=t.object_id AND p.index_id IN (0,1)
    ) rowsInTable
    WHERE t.is_ms_shipped=0
      AND t.name<>N'TenderSchemaMigrations'
      AND ISNULL(rowsInTable.RecordCount,0)>0
)
    THROW 51002, 'Post-install validation failed: Fresh mode must not seed business or reference data.', 1;
IF N'$(InstallMode)'=N'Fresh' AND
   (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>59
    THROW 51003, 'Post-install validation failed: expected 58 application tables plus the migration ledger.', 1;
IF EXISTS
(
    SELECT required.MigrationId
    FROM (VALUES
        (N'00-CORE'),(N'04'),(N'05'),(N'06'),(N'08'),(N'09'),(N'10'),
        (N'11'),(N'12'),(N'15'),(N'16'),(N'17'),(N'18')
    ) required(MigrationId)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.TenderSchemaMigrations m
        WHERE m.MigrationId=required.MigrationId
    )
)
    THROW 51005, 'Post-install validation failed: one or more required migrations are missing from the ledger.', 1;
IF EXISTS (SELECT 1 FROM sys.procedures WHERE is_ms_shipped=0)
    THROW 51006, 'Post-install validation failed: stored procedures are not permitted in this application database.', 1;
IF EXISTS
(
    SELECT RequiredName FROM (VALUES
        (N'TR_TenderEvaluationDocuments_Immutable'),(N'TR_TenderBidOpening_Immutable'),
        (N'TR_TenderLifecycleHistory_Immutable'),(N'TR_TenderBudgetOpinions_Immutable')
    ) required(RequiredName)
    WHERE OBJECT_ID(N'dbo.'+RequiredName,N'TR') IS NULL
)
    THROW 51007, 'Post-install validation failed: one or more required immutability triggers are missing.', 1;
IF N'$(InstallMode)'=N'Upgrade' AND EXISTS
(
    SELECT 1
    FROM #PreservedBusinessCounts b
    LEFT JOIN sys.tables t ON t.name=b.TableName AND SCHEMA_NAME(t.schema_id)=N'dbo'
    OUTER APPLY
    (
        SELECT SUM(CONVERT(BIGINT,p.row_count)) AS BusinessRowCount
        FROM sys.dm_db_partition_stats p
        WHERE p.object_id=t.object_id AND p.index_id IN (0,1)
    ) currentRows
    WHERE t.object_id IS NULL OR ISNULL(currentRows.BusinessRowCount,0)<>b.BusinessRowCount
)
    THROW 51008, 'Upgrade validation failed: a pre-existing business table row count changed. Roll back and investigate.', 1;
DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;
SELECT N'Application tables' AS Validation,COUNT(*) AS Result FROM sys.tables WHERE is_ms_shipped=0;
SELECT N'Users (preserved; zero on Fresh)' AS Validation,COUNT(*) AS Result FROM dbo.Users;
SELECT N'Contractors (preserved; zero on Fresh)' AS Validation,COUNT(*) AS Result FROM dbo.Contractors;
SELECT N'Tenders (preserved; zero on Fresh)' AS Validation,COUNT(*) AS Result FROM dbo.Tender;
SELECT N'Roles' AS Validation,COUNT(*) AS Result FROM dbo.Roles;
SELECT N'Departments' AS Validation,COUNT(*) AS Result FROM dbo.Departments;
SELECT N'Tender categories' AS Validation,COUNT(*) AS Result FROM dbo.TenderTypes WHERE IsActive=1;
SELECT N'Tender methods' AS Validation,COUNT(*) AS Result FROM dbo.TenderMethods WHERE IsActive=1;
SELECT N'Foreign keys' AS Validation,COUNT(*) AS Result FROM sys.foreign_keys WHERE is_ms_shipped=0;
SELECT N'Indexes' AS Validation,COUNT(*) AS Result FROM sys.indexes WHERE object_id IN (SELECT object_id FROM sys.tables WHERE is_ms_shipped=0) AND index_id>0;
SELECT N'Stored procedures' AS Validation,COUNT(*) AS Result FROM sys.procedures WHERE is_ms_shipped=0;
SELECT N'Triggers' AS Validation,COUNT(*) AS Result FROM sys.triggers WHERE parent_class=1 AND is_ms_shipped=0;
SELECT MigrationId,Description,AppliedAtUtc FROM dbo.TenderSchemaMigrations ORDER BY MigrationId;
COMMIT TRANSACTION;
IF N'$(InstallMode)'=N'Fresh'
    PRINT 'Fresh schema installed with empty application and reference tables. No business or configuration rows were seeded.';
ELSE
    PRINT 'Additive upgrade completed. Existing business row counts were verified.';
GO
