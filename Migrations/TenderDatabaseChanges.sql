/*
  Consolidated, dependency-ordered tender database changes.
  Run after SMC_TENDER_DATABASE.sql and after provisioning the active workflow roles.
  Each dated section preserves its original transaction and SQL Server batch scope.
  Dates and source names below are retained from the original migration files.
  The 2026-11-27 typed-geometry section follows the 2026-11-28 boundary section
  because it depends on BoundaryLineGeometryJson created by that later-dated script.
  Back up and rehearse database changes before applying to production.
*/
SET NOCOUNT ON;
GO

-- ============================================================================
-- Change date: 2026-11-20
-- Change: Extends tender and contractor data; adds financial-year and subcategory masters, requirement/revision history, and indexes.
-- Original script: 20261120_TenderFormExtensions.sql
-- ============================================================================
/*
  Additive SMC tender-form schema extension.
  Safe to run repeatedly: existing rows and tables are preserved.
  Run against the configured tender database after reviewing the target.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Contractors', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TenderTypes', N'U') IS NULL
    THROW 51100, 'Tender schema extension requires the existing tender, contractor, user, and tender type tables.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Contractors', N'ContactPersonName') IS NULL
    ALTER TABLE dbo.Contractors ADD ContactPersonName NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.Contractors', N'City') IS NULL
    ALTER TABLE dbo.Contractors ADD City NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Contractors', N'State') IS NULL
    ALTER TABLE dbo.Contractors ADD State NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Contractors', N'PinCode') IS NULL
    ALTER TABLE dbo.Contractors ADD PinCode NVARCHAR(6) NULL;
IF COL_LENGTH(N'dbo.Contractors', N'GSTNumber') IS NULL
    ALTER TABLE dbo.Contractors ADD GSTNumber NVARCHAR(15) NULL;
IF COL_LENGTH(N'dbo.Contractors', N'PANNumber') IS NULL
    ALTER TABLE dbo.Contractors ADD PANNumber NVARCHAR(10) NULL;
IF COL_LENGTH(N'dbo.Contractors', N'CompanyType') IS NULL
    ALTER TABLE dbo.Contractors ADD CompanyType NVARCHAR(30) NULL;

IF COL_LENGTH(N'dbo.Tender', N'FinancialYear') IS NULL
    ALTER TABLE dbo.Tender ADD FinancialYear NVARCHAR(9) NULL;
IF COL_LENGTH(N'dbo.Tender', N'TenderSubcategoryId') IS NULL
    ALTER TABLE dbo.Tender ADD TenderSubcategoryId INT NULL;
IF COL_LENGTH(N'dbo.Tender', N'TenderSubcategoryCategory') IS NULL
    ALTER TABLE dbo.Tender ADD TenderSubcategoryCategory NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Tender', N'EMDAmount') IS NULL
    ALTER TABLE dbo.Tender ADD EMDAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Tender', N'TenderDocumentFee') IS NULL
    ALTER TABLE dbo.Tender ADD TenderDocumentFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Tender', N'ProcessingFee') IS NULL
    ALTER TABLE dbo.Tender ADD ProcessingFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Tender', N'PublicationScheduledUtc') IS NULL
    ALTER TABLE dbo.Tender ADD PublicationScheduledUtc DATETIME2(0) NULL;
IF COL_LENGTH(N'dbo.Tender', N'FinancialBidOpeningScheduledUtc') IS NULL
    ALTER TABLE dbo.Tender ADD FinancialBidOpeningScheduledUtc DATETIME2(0) NULL;

IF OBJECT_ID(N'dbo.FinancialYears', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FinancialYears
    (
        FinancialYear NVARCHAR(9) NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_FinancialYears_SortOrder DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_FinancialYears_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_FinancialYears_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FinancialYears PRIMARY KEY (FinancialYear),
        CONSTRAINT CK_FinancialYears_DateRange CHECK (EndDate > StartDate)
    );
END;

IF OBJECT_ID(N'dbo.TenderSubcategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderSubcategories
    (
        SubcategoryId INT IDENTITY(1,1) NOT NULL,
        TenderCategory NVARCHAR(50) NOT NULL,
        SubcategoryName NVARCHAR(100) NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_TenderSubcategories_SortOrder DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_TenderSubcategories_IsActive DEFAULT (1),
        CONSTRAINT PK_TenderSubcategories PRIMARY KEY (SubcategoryId),
        CONSTRAINT UQ_TenderSubcategories_CategoryName UNIQUE (TenderCategory, SubcategoryName),
        CONSTRAINT UQ_TenderSubcategories_IdCategory UNIQUE (SubcategoryId, TenderCategory),
        CONSTRAINT FK_TenderSubcategories_TenderTypes FOREIGN KEY (TenderCategory)
            REFERENCES dbo.TenderTypes(TypeName)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.Tender') AND name=N'FK_Tender_FinancialYears')
    ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT FK_Tender_FinancialYears
        FOREIGN KEY (FinancialYear) REFERENCES dbo.FinancialYears(FinancialYear);
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.Tender') AND name=N'FK_Tender_TenderSubcategories')
    ALTER TABLE dbo.Tender DROP CONSTRAINT FK_Tender_TenderSubcategories;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.Tender') AND name=N'FK_Tender_TenderSubcategoryCategory')
    ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT FK_Tender_TenderSubcategoryCategory
        FOREIGN KEY (TenderSubcategoryId, TenderSubcategoryCategory)
        REFERENCES dbo.TenderSubcategories(SubcategoryId, TenderCategory);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Tender') AND name=N'CK_Tender_Fees_NonNegative')
    EXEC sys.sp_executesql N'ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT CK_Tender_Fees_NonNegative CHECK
    (
        (EMDAmount IS NULL OR EMDAmount >= 0) AND
        (TenderDocumentFee IS NULL OR TenderDocumentFee >= 0) AND
        (ProcessingFee IS NULL OR ProcessingFee >= 0)
    );';

IF OBJECT_ID(N'dbo.TenderRequirements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderRequirements
    (
        TenderId INT NOT NULL,
        ScopeOfWork NVARCHAR(MAX) NULL,
        TechnicalRequirements NVARCHAR(4000) NULL,
        FinancialRequirements NVARCHAR(4000) NULL,
        AdditionalConditions NVARCHAR(4000) NULL,
        TermsAndConditions NVARCHAR(MAX) NULL,
        CreatedBy INT NOT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_TenderRequirements_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ModifiedBy INT NOT NULL,
        ModifiedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_TenderRequirements_ModifiedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TenderRequirements PRIMARY KEY (TenderId),
        CONSTRAINT FK_TenderRequirements_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId),
        CONSTRAINT FK_TenderRequirements_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_TenderRequirements_ModifiedBy FOREIGN KEY (ModifiedBy) REFERENCES dbo.Users(UserId)
    );
END;

IF OBJECT_ID(N'dbo.TenderRevisions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderRevisions
    (
        RevisionId BIGINT IDENTITY(1,1) NOT NULL,
        TenderId INT NOT NULL,
        RevisionNumber INT NOT NULL,
        ActionName NVARCHAR(50) NOT NULL,
        RevisionReason NVARCHAR(1000) NULL,
        TenderNumber NVARCHAR(100) NOT NULL,
        Title NVARCHAR(MAX) NOT NULL,
        Description NVARCHAR(MAX) NULL,
        EligibilityCriteria NVARCHAR(MAX) NULL,
        DepartmentId INT NOT NULL,
        EstimatedAmount DECIMAL(18,2) NOT NULL,
        TenderType NVARCHAR(100) NULL,
        TenderCategory NVARCHAR(100) NULL,
        TenderSubcategoryId INT NULL,
        FinancialYear NVARCHAR(9) NULL,
        EMDAmount DECIMAL(18,2) NULL,
        TenderDocumentFee DECIMAL(18,2) NULL,
        ProcessingFee DECIMAL(18,2) NULL,
        PublicationScheduledUtc DATETIME2(0) NULL,
        SubmissionStartDate DATETIME2 NULL,
        SubmissionDeadline DATETIME2 NULL,
        TechnicalBidOpeningScheduledUtc DATETIME2 NULL,
        FinancialBidOpeningScheduledUtc DATETIME2(0) NULL,
        CurrentStatus NVARCHAR(50) NOT NULL,
        ScopeOfWork NVARCHAR(MAX) NULL,
        TechnicalRequirements NVARCHAR(4000) NULL,
        FinancialRequirements NVARCHAR(4000) NULL,
        AdditionalConditions NVARCHAR(4000) NULL,
        TermsAndConditions NVARCHAR(MAX) NULL,
        RevisedBy INT NOT NULL,
        RevisedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_TenderRevisions_RevisedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TenderRevisions PRIMARY KEY (RevisionId),
        CONSTRAINT UQ_TenderRevisions_TenderRevision UNIQUE (TenderId, RevisionNumber),
        CONSTRAINT FK_TenderRevisions_Tender FOREIGN KEY (TenderId) REFERENCES dbo.Tender(TenderId),
        CONSTRAINT FK_TenderRevisions_RevisedBy FOREIGN KEY (RevisedBy) REFERENCES dbo.Users(UserId)
    );
END;

EXEC sys.sp_executesql N'
INSERT INTO dbo.TenderRevisions
    (TenderId,RevisionNumber,ActionName,RevisionReason,TenderNumber,Title,Description,
     EligibilityCriteria,DepartmentId,EstimatedAmount,TenderType,TenderCategory,
     TenderSubcategoryId,FinancialYear,EMDAmount,TenderDocumentFee,ProcessingFee,
     PublicationScheduledUtc,SubmissionStartDate,SubmissionDeadline,
     TechnicalBidOpeningScheduledUtc,FinancialBidOpeningScheduledUtc,CurrentStatus,
     ScopeOfWork,TechnicalRequirements,FinancialRequirements,AdditionalConditions,
     TermsAndConditions,RevisedBy)
SELECT t.TenderId,1,N''Schema baseline'',N''Initial snapshot recorded during tender form schema upgrade.'',
       t.TenderNumber,t.Title,t.Description,t.EligibilityCriteria,t.DepartmentId,t.EstimatedAmount,
       t.TenderType,t.TenderCategory,t.TenderSubcategoryId,t.FinancialYear,t.EMDAmount,
       t.TenderDocumentFee,t.ProcessingFee,t.PublicationScheduledUtc,
       t.SubmissionStartDate,t.SubmissionDeadline,t.TechnicalBidOpeningScheduledUtc,
       t.FinancialBidOpeningScheduledUtc,t.CurrentStatus,req.ScopeOfWork,req.TechnicalRequirements,
       req.FinancialRequirements,req.AdditionalConditions,req.TermsAndConditions,t.CreatedBy
FROM dbo.Tender t
LEFT JOIN dbo.TenderRequirements req ON req.TenderId=t.TenderId
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.TenderRevisions existingRevision WHERE existingRevision.TenderId=t.TenderId
);';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TenderDocuments') AND name=N'IX_TenderDocuments_TenderId')
    CREATE INDEX IX_TenderDocuments_TenderId ON dbo.TenderDocuments(TenderId, UploadedDate);

DECLARE @IndiaToday DATE = CONVERT(DATE, SYSUTCDATETIME() AT TIME ZONE 'India Standard Time');
DECLARE @FinancialYearStart INT = YEAR(@IndiaToday) - CASE WHEN MONTH(@IndiaToday) < 4 THEN 1 ELSE 0 END;
DECLARE @Offset INT = 0;
WHILE @Offset < 3
BEGIN
    DECLARE @StartYear INT = @FinancialYearStart + @Offset;
    DECLARE @FinancialYearLabel NVARCHAR(9) =
        CONVERT(NVARCHAR(4), @StartYear) + N'-' + RIGHT(CONVERT(NVARCHAR(4), @StartYear + 1), 2);
    IF NOT EXISTS (SELECT 1 FROM dbo.FinancialYears WHERE FinancialYear=@FinancialYearLabel)
        INSERT INTO dbo.FinancialYears(FinancialYear,StartDate,EndDate,SortOrder,IsActive)
        VALUES(@FinancialYearLabel, DATEFROMPARTS(@StartYear,4,1), DATEFROMPARTS(@StartYear+1,3,31), @Offset, 1);
    SET @Offset += 1;
END;

COMMIT TRANSACTION;
GO

-- ============================================================================
-- Change date: 2026-11-25
-- Change: Adds wizard state, tender category and method masters, GIS location, administrative approvals, and offline submissions.
-- Original script: 20261125_TenderCreationWizard.sql
-- ============================================================================
/*
  Additive schema and master-data update for the tender creation wizard.
  Existing tender IDs and historical values are preserved.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Departments', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TenderTypes', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TenderMethods', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 51120, 'Tender creation wizard requires the existing tender, master, and user tables.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.TenderTypes', N'TenderCategoryId') IS NULL
    ALTER TABLE dbo.TenderTypes ADD TenderCategoryId INT IDENTITY(1,1) NOT NULL;
IF NOT EXISTS
(
    SELECT 1 FROM sys.key_constraints
    WHERE parent_object_id=OBJECT_ID(N'dbo.TenderTypes')
      AND name=N'UQ_TenderTypes_TenderCategoryId'
)
    ALTER TABLE dbo.TenderTypes
        ADD CONSTRAINT UQ_TenderTypes_TenderCategoryId UNIQUE(TenderCategoryId);

IF COL_LENGTH(N'dbo.Tender', N'TenderCategoryId') IS NULL
    ALTER TABLE dbo.Tender ADD TenderCategoryId INT NULL;
IF COL_LENGTH(N'dbo.Tender', N'TenderMethod') IS NULL
    ALTER TABLE dbo.Tender ADD TenderMethod NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Tender', N'WizardCurrentStep') IS NULL
    ALTER TABLE dbo.Tender ADD WizardCurrentStep TINYINT NOT NULL
        CONSTRAINT DF_Tender_WizardCurrentStep DEFAULT(1) WITH VALUES;
IF COL_LENGTH(N'dbo.Tender', N'WizardCompletedStepsMask') IS NULL
    ALTER TABLE dbo.Tender ADD WizardCompletedStepsMask TINYINT NOT NULL
        CONSTRAINT DF_Tender_WizardCompletedStepsMask DEFAULT(0) WITH VALUES;

IF COL_LENGTH(N'dbo.TenderRevisions', N'TenderCategoryId') IS NULL
    ALTER TABLE dbo.TenderRevisions ADD TenderCategoryId INT NULL;
IF COL_LENGTH(N'dbo.TenderRevisions', N'TenderMethod') IS NULL
    ALTER TABLE dbo.TenderRevisions ADD TenderMethod NVARCHAR(50) NULL;

INSERT INTO dbo.Departments(DepartmentName, DepartmentCode, IsActive)
SELECT requested.DepartmentName, requested.DepartmentCode, 1
FROM (VALUES
    (N'City Engineer',N'CITY-ENG'),
    (N'PHE',N'PHE'),
    (N'Computer',N'COMPUTER'),
    (N'Zone 1',N'ZONE-1'),
    (N'Zone 2',N'ZONE-2'),
    (N'Zone 3',N'ZONE-3'),
    (N'Zone 4',N'ZONE-4'),
    (N'Zone 5',N'ZONE-5'),
    (N'Zone 6',N'ZONE-6'),
    (N'Zone 7',N'ZONE-7'),
    (N'Zone 8',N'ZONE-8'),
    (N'Zoo',N'ZOO'),
    (N'Garden',N'GARDEN')
) requested(DepartmentName,DepartmentCode)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Departments existing
    WHERE existing.DepartmentName=requested.DepartmentName
);

UPDATE d
SET IsActive=CASE WHEN requested.DepartmentName IS NULL THEN 0 ELSE 1 END,
    DepartmentCode=COALESCE(d.DepartmentCode,requested.DepartmentCode)
FROM dbo.Departments d
LEFT JOIN (VALUES
    (N'City Engineer',N'CITY-ENG'),
    (N'PHE',N'PHE'),
    (N'Computer',N'COMPUTER'),
    (N'Zone 1',N'ZONE-1'),
    (N'Zone 2',N'ZONE-2'),
    (N'Zone 3',N'ZONE-3'),
    (N'Zone 4',N'ZONE-4'),
    (N'Zone 5',N'ZONE-5'),
    (N'Zone 6',N'ZONE-6'),
    (N'Zone 7',N'ZONE-7'),
    (N'Zone 8',N'ZONE-8'),
    (N'Zoo',N'ZOO'),
    (N'Garden',N'GARDEN')
) requested(DepartmentName,DepartmentCode)
    ON requested.DepartmentName=d.DepartmentName;

INSERT INTO dbo.TenderTypes(TypeName,SortOrder,IsActive)
SELECT requested.TypeName,requested.SortOrder,1
FROM (VALUES (N'7 Days',1),(N'15 Days',2),(N'30 Days',3))
    requested(TypeName,SortOrder)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.TenderTypes existing WHERE existing.TypeName=requested.TypeName
);

UPDATE category
SET SortOrder=requested.SortOrder,IsActive=1
FROM dbo.TenderTypes category
INNER JOIN (VALUES (N'7 Days',1),(N'15 Days',2),(N'30 Days',3))
    requested(TypeName,SortOrder) ON requested.TypeName=category.TypeName;
UPDATE dbo.TenderTypes
SET IsActive=0
WHERE TypeName NOT IN (N'7 Days',N'15 Days',N'30 Days');

INSERT INTO dbo.TenderMethods(MethodName,SortOrder,IsActive)
SELECT requested.MethodName,requested.SortOrder,1
FROM (VALUES (N'Online',1),(N'Offline',2))
    requested(MethodName,SortOrder)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.TenderMethods existing WHERE existing.MethodName=requested.MethodName
);

UPDATE method
SET SortOrder=requested.SortOrder,IsActive=1
FROM dbo.TenderMethods method
INNER JOIN (VALUES (N'Online',1),(N'Offline',2))
    requested(MethodName,SortOrder) ON requested.MethodName=method.MethodName;
UPDATE dbo.TenderMethods
SET IsActive=0
WHERE MethodName NOT IN (N'Online',N'Offline');

EXEC sys.sp_executesql N'
    UPDATE t
    SET TenderCategoryId=category.TenderCategoryId
    FROM dbo.Tender t
    INNER JOIN dbo.TenderTypes category ON category.TypeName=t.TenderCategory
    WHERE t.TenderCategoryId IS NULL;';

EXEC sys.sp_executesql N'
    UPDATE t
    SET TenderMethod=t.TenderType
    FROM dbo.Tender t
    INNER JOIN dbo.TenderMethods method ON method.MethodName=t.TenderType
    WHERE t.TenderMethod IS NULL;';

EXEC sys.sp_executesql N'
    UPDATE revision
    SET TenderCategoryId=category.TenderCategoryId,
        TenderMethod=revision.TenderType
    FROM dbo.TenderRevisions revision
    LEFT JOIN dbo.TenderTypes category ON category.TypeName=revision.TenderCategory
    WHERE revision.TenderCategoryId IS NULL OR revision.TenderMethod IS NULL;';

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id=OBJECT_ID(N'dbo.Tender')
      AND name=N'FK_Tender_TenderCategoryId'
)
    ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT FK_Tender_TenderCategoryId
        FOREIGN KEY(TenderCategoryId) REFERENCES dbo.TenderTypes(TenderCategoryId);

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id=OBJECT_ID(N'dbo.Tender')
      AND name=N'FK_Tender_TenderMethod'
)
    ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT FK_Tender_TenderMethod
        FOREIGN KEY(TenderMethod) REFERENCES dbo.TenderMethods(MethodName);

IF OBJECT_ID(N'dbo.TenderGisLocations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderGisLocations
    (
        TenderId INT NOT NULL,
        Latitude DECIMAL(9,6) NULL,
        Longitude DECIMAL(9,6) NULL,
        LocationName NVARCHAR(300) NULL,
        BoundaryCoordinatesJson NVARCHAR(MAX) NULL,
        SelectedAreaSquareMeters DECIMAL(18,2) NULL,
        ImageOriginalFileName NVARCHAR(260) NULL,
        ImageStoredFileName NVARCHAR(100) NULL,
        ImagePath NVARCHAR(400) NULL,
        ImageExtension NVARCHAR(10) NULL,
        ImageSizeBytes BIGINT NULL,
        ImageUploadedBy INT NULL,
        ImageUploadedAtUtc DATETIME2(0) NULL,
        ModifiedBy INT NOT NULL,
        ModifiedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderGisLocations_ModifiedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TenderGisLocations PRIMARY KEY(TenderId),
        CONSTRAINT FK_TenderGisLocations_Tender FOREIGN KEY(TenderId)
            REFERENCES dbo.Tender(TenderId),
        CONSTRAINT FK_TenderGisLocations_ModifiedBy FOREIGN KEY(ModifiedBy)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_TenderGisLocations_ImageUploadedBy FOREIGN KEY(ImageUploadedBy)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_TenderGisLocations_Latitude CHECK(Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
        CONSTRAINT CK_TenderGisLocations_Longitude CHECK(Longitude IS NULL OR Longitude BETWEEN -180 AND 180),
        CONSTRAINT CK_TenderGisLocations_ImageMetadata CHECK
        (
            (ImagePath IS NULL AND ImageStoredFileName IS NULL AND ImageOriginalFileName IS NULL
             AND ImageExtension IS NULL AND ImageSizeBytes IS NULL AND ImageUploadedBy IS NULL
             AND ImageUploadedAtUtc IS NULL)
            OR
            (ImagePath IS NOT NULL AND ImageStoredFileName IS NOT NULL
             AND ImageOriginalFileName IS NOT NULL AND ImageExtension IN (N'.jpg',N'.jpeg',N'.png')
             AND ImageSizeBytes > 0 AND ImageUploadedBy IS NOT NULL AND ImageUploadedAtUtc IS NOT NULL)
        )
    );
END;

IF OBJECT_ID(N'dbo.TenderAdministrativeApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderAdministrativeApprovals
    (
        TenderId INT NOT NULL,
        ApprovalType NVARCHAR(40) NOT NULL,
        IsApproved BIT NOT NULL CONSTRAINT DF_TenderAdministrativeApprovals_IsApproved DEFAULT(0),
        ApprovalReferenceNumber NVARCHAR(100) NULL,
        ApprovalDate DATE NULL,
        DocumentOriginalFileName NVARCHAR(260) NULL,
        DocumentStoredFileName NVARCHAR(100) NULL,
        DocumentPath NVARCHAR(400) NULL,
        DocumentExtension NVARCHAR(10) NULL,
        DocumentSizeBytes BIGINT NULL,
        DocumentUploadedBy INT NULL,
        DocumentUploadedAtUtc DATETIME2(0) NULL,
        ModifiedBy INT NOT NULL,
        ModifiedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderAdministrativeApprovals_ModifiedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TenderAdministrativeApprovals PRIMARY KEY(TenderId,ApprovalType),
        CONSTRAINT FK_TenderAdministrativeApprovals_Tender FOREIGN KEY(TenderId)
            REFERENCES dbo.Tender(TenderId),
        CONSTRAINT FK_TenderAdministrativeApprovals_ModifiedBy FOREIGN KEY(ModifiedBy)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_TenderAdministrativeApprovals_DocumentUploadedBy FOREIGN KEY(DocumentUploadedBy)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_TenderAdministrativeApprovals_Type CHECK
            (ApprovalType IN (N'Administrative',N'TechnicalSanction',N'GeneralBody',N'DPDC')),
        CONSTRAINT CK_TenderAdministrativeApprovals_DocumentMetadata CHECK
        (
            (DocumentPath IS NULL AND DocumentStoredFileName IS NULL AND DocumentOriginalFileName IS NULL
             AND DocumentExtension IS NULL AND DocumentSizeBytes IS NULL AND DocumentUploadedBy IS NULL
             AND DocumentUploadedAtUtc IS NULL)
            OR
            (DocumentPath IS NOT NULL AND DocumentStoredFileName IS NOT NULL
             AND DocumentOriginalFileName IS NOT NULL AND DocumentExtension IS NOT NULL
             AND DocumentSizeBytes > 0 AND DocumentUploadedBy IS NOT NULL AND DocumentUploadedAtUtc IS NOT NULL)
        )
    );
END;

IF OBJECT_ID(N'dbo.TenderAdministrativeApprovalHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderAdministrativeApprovalHistory
    (
        ApprovalHistoryId BIGINT IDENTITY(1,1) NOT NULL,
        TenderId INT NOT NULL,
        ApprovalType NVARCHAR(40) NOT NULL,
        PreviousIsApproved BIT NULL,
        NewIsApproved BIT NOT NULL,
        ApprovalReferenceNumber NVARCHAR(100) NULL,
        ApprovalDate DATE NULL,
        ActionName NVARCHAR(20) NOT NULL,
        ChangedBy INT NOT NULL,
        ChangedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderAdministrativeApprovalHistory_ChangedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TenderAdministrativeApprovalHistory PRIMARY KEY(ApprovalHistoryId),
        CONSTRAINT FK_TenderAdministrativeApprovalHistory_Tender FOREIGN KEY(TenderId)
            REFERENCES dbo.Tender(TenderId),
        CONSTRAINT FK_TenderAdministrativeApprovalHistory_ChangedBy FOREIGN KEY(ChangedBy)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_TenderAdministrativeApprovalHistory_Type CHECK
            (ApprovalType IN (N'Administrative',N'TechnicalSanction',N'GeneralBody',N'DPDC'))
    );
END;

IF OBJECT_ID(N'dbo.TenderOfflineSubmissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenderOfflineSubmissions
    (
        OfflineSubmissionId BIGINT IDENTITY(1,1) NOT NULL,
        TenderId INT NOT NULL,
        CompanyName NVARCHAR(200) NOT NULL,
        RegistrationNumber NVARCHAR(100) NULL,
        RepresentativeName NVARCHAR(150) NOT NULL,
        ContactNumber NVARCHAR(20) NULL,
        ReceiptReference NVARCHAR(100) NOT NULL,
        ReceivedAtUtc DATETIME2(0) NOT NULL,
        ReviewStatus NVARCHAR(20) NOT NULL
            CONSTRAINT DF_TenderOfflineSubmissions_ReviewStatus DEFAULT(N'Received'),
        ProcessingNotes NVARCHAR(1000) NULL,
        CreatedBy INT NOT NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_TenderOfflineSubmissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ModifiedBy INT NULL,
        ModifiedAtUtc DATETIME2(0) NULL,
        CONSTRAINT PK_TenderOfflineSubmissions PRIMARY KEY(OfflineSubmissionId),
        CONSTRAINT FK_TenderOfflineSubmissions_Tender FOREIGN KEY(TenderId)
            REFERENCES dbo.Tender(TenderId),
        CONSTRAINT FK_TenderOfflineSubmissions_CreatedBy FOREIGN KEY(CreatedBy)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_TenderOfflineSubmissions_ModifiedBy FOREIGN KEY(ModifiedBy)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT UQ_TenderOfflineSubmissions_Receipt UNIQUE(TenderId,ReceiptReference),
        CONSTRAINT CK_TenderOfflineSubmissions_ReviewStatus CHECK
            (ReviewStatus IN (N'Received',N'Under Review',N'Accepted',N'Rejected'))
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TenderOfflineSubmissions')
      AND name=N'IX_TenderOfflineSubmissions_TenderId'
)
    CREATE INDEX IX_TenderOfflineSubmissions_TenderId
        ON dbo.TenderOfflineSubmissions(TenderId,ReceivedAtUtc DESC);

COMMIT TRANSACTION;
GO

-- ============================================================================
-- Change date: 2026-11-25
-- Change: Adds/validates polygon boundary coordinates and selected GIS area.
-- Original script: 20261125_TenderGisArea.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderGisLocations', N'U') IS NULL
        THROW 51000, 'The creation-wizard section must run before the GIS-area section in this script.', 1;

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryCoordinatesJson') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryCoordinatesJson NVARCHAR(MAX) NULL;

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'SelectedAreaSquareMeters') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD SelectedAreaSquareMeters DECIMAL(18,2) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_SelectedArea'
    )
        ALTER TABLE dbo.TenderGisLocations WITH CHECK
            ADD CONSTRAINT CK_TenderGisLocations_SelectedArea
            CHECK(SelectedAreaSquareMeters IS NULL OR SelectedAreaSquareMeters > 0);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_BoundaryJson'
    )
        ALTER TABLE dbo.TenderGisLocations WITH CHECK
            ADD CONSTRAINT CK_TenderGisLocations_BoundaryJson
            CHECK(BoundaryCoordinatesJson IS NULL OR ISJSON(BoundaryCoordinatesJson)=1);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Retire the obsolete fee without disturbing the remaining tender amounts. */
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.Tender')
          AND name=N'CK_Tender_Fees_NonNegative'
    )
        ALTER TABLE dbo.Tender DROP CONSTRAINT CK_Tender_Fees_NonNegative;

    DECLARE @TenderObjectId INT=OBJECT_ID(N'dbo.Tender');
    DECLARE @TenderColumnId INT;
    SELECT @TenderColumnId=column_id
    FROM sys.columns
    WHERE object_id=@TenderObjectId AND name=N'SecurityDepositAmount';

    IF @TenderColumnId IS NOT NULL
    BEGIN
        IF EXISTS
        (
            SELECT 1 FROM sys.sql_expression_dependencies
            WHERE referenced_id=@TenderObjectId AND referenced_minor_id=@TenderColumnId
        )
        OR EXISTS
        (
            SELECT 1 FROM sys.index_columns
            WHERE object_id=@TenderObjectId AND column_id=@TenderColumnId
        )
        OR EXISTS
        (
            SELECT 1 FROM sys.foreign_key_columns
            WHERE (parent_object_id=@TenderObjectId AND parent_column_id=@TenderColumnId)
               OR (referenced_object_id=@TenderObjectId AND referenced_column_id=@TenderColumnId)
        )
        OR EXISTS
        (
            SELECT 1 FROM sys.default_constraints
            WHERE parent_object_id=@TenderObjectId AND parent_column_id=@TenderColumnId
        )
            THROW 51090, 'Tender.SecurityDepositAmount has dependent database objects; resolve them before migration.', 1;

        ALTER TABLE dbo.Tender DROP COLUMN SecurityDepositAmount;
    END;

    DECLARE @RevisionObjectId INT=OBJECT_ID(N'dbo.TenderRevisions');
    DECLARE @RevisionColumnId INT;
    SELECT @RevisionColumnId=column_id
    FROM sys.columns
    WHERE object_id=@RevisionObjectId AND name=N'SecurityDepositAmount';

    IF @RevisionColumnId IS NOT NULL
    BEGIN
        IF EXISTS
        (
            SELECT 1 FROM sys.sql_expression_dependencies
            WHERE referenced_id=@RevisionObjectId AND referenced_minor_id=@RevisionColumnId
        )
        OR EXISTS
        (
            SELECT 1 FROM sys.index_columns
            WHERE object_id=@RevisionObjectId AND column_id=@RevisionColumnId
        )
        OR EXISTS
        (
            SELECT 1 FROM sys.foreign_key_columns
            WHERE (parent_object_id=@RevisionObjectId AND parent_column_id=@RevisionColumnId)
               OR (referenced_object_id=@RevisionObjectId AND referenced_column_id=@RevisionColumnId)
        )
        OR EXISTS
        (
            SELECT 1 FROM sys.default_constraints
            WHERE parent_object_id=@RevisionObjectId AND parent_column_id=@RevisionColumnId
        )
            THROW 51091, 'TenderRevisions.SecurityDepositAmount has dependent database objects; resolve them before migration.', 1;

        ALTER TABLE dbo.TenderRevisions DROP COLUMN SecurityDepositAmount;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.Tender')
          AND name=N'CK_Tender_Fees_NonNegative'
    )
        ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT CK_Tender_Fees_NonNegative CHECK
        (
            (EMDAmount IS NULL OR EMDAmount >= 0) AND
            (TenderDocumentFee IS NULL OR TenderDocumentFee >= 0) AND
            (ProcessingFee IS NULL OR ProcessingFee >= 0)
        );

    IF COL_LENGTH(N'dbo.TenderWorkflowHistory',N'PreviousStageName') IS NULL
        ALTER TABLE dbo.TenderWorkflowHistory ADD PreviousStageName NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.TenderWorkflowHistory',N'NewStageName') IS NULL
        ALTER TABLE dbo.TenderWorkflowHistory ADD NewStageName NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AuditLog',N'RoleAtAction') IS NULL
        ALTER TABLE dbo.AuditLog ADD RoleAtAction NVARCHAR(100) NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-25
-- Change: Adds typed tender-document metadata, document type relationships, and supporting indexes.
-- Original script: 20261125_TenderDocumentManagement.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.DocumentTypes', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL
        THROW 51000, 'DocumentTypes and TenderDocuments must exist before this migration is applied.', 1;

    IF COL_LENGTH(N'dbo.DocumentTypes', N'DocumentTypeId') IS NULL
        ALTER TABLE dbo.DocumentTypes ADD DocumentTypeId INT IDENTITY(1,1) NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.DocumentTypes')
          AND name=N'UQ_DocumentTypes_DocumentTypeId'
    )
        ALTER TABLE dbo.DocumentTypes
            ADD CONSTRAINT UQ_DocumentTypes_DocumentTypeId UNIQUE(DocumentTypeId);

    IF COL_LENGTH(N'dbo.TenderDocuments', N'DocumentTypeId') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD DocumentTypeId INT NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'DocumentName') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD DocumentName NVARCHAR(250) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'Remarks') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD Remarks NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'FileExtension') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD FileExtension NVARCHAR(10) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'FileSizeBytes') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD FileSizeBytes BIGINT NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.TenderDocuments
        SET DocumentName=LEFT(CASE WHEN NULLIF(LTRIM(RTRIM(FileName)), N'''') IS NULL
                                   THEN N''Untitled document'' ELSE FileName END, 250)
        WHERE DocumentName IS NULL OR LTRIM(RTRIM(DocumentName))=N'''';

        UPDATE dbo.TenderDocuments
        SET FileExtension=CASE
            WHEN CHARINDEX(N''.'', REVERSE(FileName)) > 0
            THEN LOWER(RIGHT(FileName, CHARINDEX(N''.'', REVERSE(FileName))))
            ELSE N''''
        END
        WHERE FileExtension IS NULL;

        UPDATE td
        SET DocumentTypeId=dt.DocumentTypeId
        FROM dbo.TenderDocuments td
        INNER JOIN dbo.DocumentTypes dt ON dt.TypeName=td.DocumentType
        WHERE td.DocumentTypeId IS NULL;

        ALTER TABLE dbo.TenderDocuments ALTER COLUMN DocumentName NVARCHAR(250) NOT NULL;
        ALTER TABLE dbo.TenderDocuments ALTER COLUMN FileExtension NVARCHAR(10) NOT NULL;

        IF NOT EXISTS
        (
            SELECT 1 FROM sys.foreign_keys
            WHERE parent_object_id=OBJECT_ID(N''dbo.TenderDocuments'')
              AND name=N''FK_TenderDocuments_DocumentTypes''
        )
            ALTER TABLE dbo.TenderDocuments WITH NOCHECK
                ADD CONSTRAINT FK_TenderDocuments_DocumentTypes
                FOREIGN KEY(DocumentTypeId) REFERENCES dbo.DocumentTypes(DocumentTypeId);

        IF NOT EXISTS
        (
            SELECT 1 FROM sys.check_constraints
            WHERE parent_object_id=OBJECT_ID(N''dbo.TenderDocuments'')
              AND name=N''CK_TenderDocuments_DocumentTypeId_Required''
        )
            ALTER TABLE dbo.TenderDocuments WITH NOCHECK
                ADD CONSTRAINT CK_TenderDocuments_DocumentTypeId_Required
                CHECK(DocumentTypeId IS NOT NULL);

        IF NOT EXISTS
        (
            SELECT 1 FROM sys.indexes
            WHERE object_id=OBJECT_ID(N''dbo.TenderDocuments'')
              AND name=N''IX_TenderDocuments_DocumentTypeId''
        )
            CREATE INDEX IX_TenderDocuments_DocumentTypeId
                ON dbo.TenderDocuments(DocumentTypeId);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-26
-- Change: Seeds requirement document types and converts saved wizard progress to the six-step flow.
-- Original script: 20261126_TenderRequirementDocuments.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.DocumentTypes', N'U') IS NULL OR
       COL_LENGTH(N'dbo.DocumentTypes', N'DocumentTypeId') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'DocumentTypeId') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'DocumentName') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'FileExtension') IS NULL OR
       COL_LENGTH(N'dbo.TenderDocuments', N'FileSizeBytes') IS NULL
        THROW 51000, 'The document-management section must run before the requirement-documents section in this script.', 1;

    DECLARE @RequirementTypes TABLE
    (
        TypeName NVARCHAR(80) NOT NULL PRIMARY KEY,
        SortOrder INT NOT NULL
    );

    INSERT INTO @RequirementTypes(TypeName, SortOrder)
    VALUES (N'Scope of Work', 20),
           (N'Technical Requirements', 21),
           (N'Financial Requirements', 22);

    UPDATE existingType
    SET IsActive=1,SortOrder=requirementType.SortOrder
    FROM dbo.DocumentTypes existingType
    INNER JOIN @RequirementTypes requirementType
        ON requirementType.TypeName=existingType.TypeName;

    INSERT INTO dbo.DocumentTypes(TypeName,SortOrder,IsActive)
    SELECT requirementType.TypeName,requirementType.SortOrder,1
    FROM @RequirementTypes requirementType
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.DocumentTypes existingType WITH (UPDLOCK,HOLDLOCK)
        WHERE existingType.TypeName=requirementType.TypeName
    );

    UPDATE dbo.Tender
    SET WizardCurrentStep=CASE
            WHEN WizardCurrentStep>5 THEN WizardCurrentStep-1
            ELSE WizardCurrentStep
        END,
        WizardCompletedStepsMask=
            (WizardCompletedStepsMask & 15) |
            CASE WHEN (WizardCompletedStepsMask & 32)<>0 THEN 16 ELSE 0 END |
            CASE WHEN (WizardCompletedStepsMask & 64)<>0 THEN 32 ELSE 0 END
    WHERE WizardCurrentStep>6 OR (WizardCompletedStepsMask & 64)<>0;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-27
-- Change: Expands tender and revision titles to NVARCHAR(MAX).
-- Original script: 20261127_TenderTitleCapacity.sql
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TenderTitleMaxLength SMALLINT;
    SELECT @TenderTitleMaxLength = max_length
    FROM sys.columns
    WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'Title';

    IF @TenderTitleMaxLength IS NOT NULL AND @TenderTitleMaxLength >= 0
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'IX_Tender_Status_Dept')
            DROP INDEX IX_Tender_Status_Dept ON dbo.Tender;

        ALTER TABLE dbo.Tender ALTER COLUMN Title NVARCHAR(MAX) NOT NULL;
    END;

    DECLARE @RevisionTitleMaxLength SMALLINT;
    SELECT @RevisionTitleMaxLength = max_length
    FROM sys.columns
    WHERE object_id=OBJECT_ID(N'dbo.TenderRevisions') AND name=N'Title';
    IF @RevisionTitleMaxLength IS NOT NULL AND @RevisionTitleMaxLength >= 0
        ALTER TABLE dbo.TenderRevisions ALTER COLUMN Title NVARCHAR(MAX) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.Tender') AND name=N'IX_Tender_Status_Dept')
        CREATE INDEX IX_Tender_Status_Dept ON dbo.Tender(CurrentStatus,DepartmentId)
            INCLUDE(TenderNumber,Title,EstimatedAmount,SubmissionDeadline,CreatedDate,IsDeleted);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-27
-- Change: Remaps saved wizard progress from six steps to five.
-- Original script: 20261127_TenderWizardFiveSteps.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL OR
       COL_LENGTH(N'dbo.Tender', N'WizardCurrentStep') IS NULL OR
       COL_LENGTH(N'dbo.Tender', N'WizardCompletedStepsMask') IS NULL
        THROW 51000, 'Apply the tender wizard migrations before this migration.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.extended_properties
        WHERE major_id = OBJECT_ID(N'dbo.Tender')
          AND minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Tender'), N'WizardCurrentStep', 'ColumnId')
          AND name = N'SMC_TenderWizardFiveStepVersion'
    )
    BEGIN
        UPDATE dbo.Tender
        SET WizardCurrentStep = CASE
                WHEN WizardCurrentStep <= 2 THEN 1
                ELSE WizardCurrentStep - 1
            END,
            WizardCompletedStepsMask =
                (WizardCompletedStepsMask & 1) |
                ((WizardCompletedStepsMask & 60) / 2);

        EXEC sys.sp_addextendedproperty
            @name = N'SMC_TenderWizardFiveStepVersion',
            @value = N'1',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE', @level1name = N'Tender',
            @level2type = N'COLUMN', @level2name = N'WizardCurrentStep';
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-28
-- Change: Adds validated two-point boundary coordinates and LineString JSON.
-- Original script: 20261128_TenderGisLinearBoundary.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderGisLocations', N'U') IS NULL
        THROW 51000, 'The creation-wizard section must run before the linear-boundary section in this script.', 1;

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryStartLatitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryStartLatitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryStartLongitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryStartLongitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryEndLatitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryEndLatitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryEndLongitude') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryEndLongitude DECIMAL(9,6) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryLineGeometryJson') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryLineGeometryJson NVARCHAR(MAX) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_LinearBoundaryCoordinates'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.TenderGisLocations WITH CHECK
                ADD CONSTRAINT CK_TenderGisLocations_LinearBoundaryCoordinates
                CHECK
                (
                    (BoundaryStartLatitude IS NULL AND BoundaryStartLongitude IS NULL
                     AND BoundaryEndLatitude IS NULL AND BoundaryEndLongitude IS NULL)
                    OR
                    (BoundaryStartLatitude BETWEEN -90 AND 90
                     AND BoundaryStartLongitude BETWEEN -180 AND 180
                     AND BoundaryEndLatitude BETWEEN -90 AND 90
                     AND BoundaryEndLongitude BETWEEN -180 AND 180)
                );';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGisLocations')
          AND name=N'CK_TenderGisLocations_BoundaryLineJson'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.TenderGisLocations WITH CHECK
                ADD CONSTRAINT CK_TenderGisLocations_BoundaryLineJson
                CHECK(BoundaryLineGeometryJson IS NULL OR ISJSON(BoundaryLineGeometryJson)=1);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-27
-- Change: Adds MIME types, replacement/deletion history, and typed LineString/Polygon geometry. Kept after the 2026-11-28 section because it reads the LineString column added there.
-- Original script: 20261127_TenderDocumentAndGeometryEnhancements.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderGisLocations', N'U') IS NULL
        THROW 51000, 'TenderDocuments and TenderGisLocations must exist before this migration is applied.', 1;

    IF COL_LENGTH(N'dbo.TenderDocuments', N'MimeType') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD MimeType NVARCHAR(100) NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.TenderDocuments
        SET MimeType=CASE LOWER(COALESCE(NULLIF(FileExtension,N''''),N''''))
            WHEN N''.pdf'' THEN N''application/pdf''
            WHEN N''.doc'' THEN N''application/msword''
            WHEN N''.docx'' THEN N''application/vnd.openxmlformats-officedocument.wordprocessingml.document''
            WHEN N''.xls'' THEN N''application/vnd.ms-excel''
            WHEN N''.xlsx'' THEN N''application/vnd.openxmlformats-officedocument.spreadsheetml.sheet''
            WHEN N''.jpg'' THEN N''image/jpeg''
            WHEN N''.jpeg'' THEN N''image/jpeg''
            WHEN N''.png'' THEN N''image/png''
            ELSE N''application/octet-stream''
        END
        WHERE MimeType IS NULL OR LTRIM(RTRIM(MimeType))=N'''';

        ALTER TABLE dbo.TenderDocuments ALTER COLUMN MimeType NVARCHAR(100) NOT NULL;

        IF NOT EXISTS
        (
            SELECT 1 FROM sys.default_constraints
            WHERE parent_object_id=OBJECT_ID(N''dbo.TenderDocuments'')
              AND name=N''DF_TenderDocuments_MimeType''
        )
            ALTER TABLE dbo.TenderDocuments
                ADD CONSTRAINT DF_TenderDocuments_MimeType
                DEFAULT(N''application/octet-stream'') FOR MimeType;';

    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryGeometryType') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryGeometryType NVARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.TenderGisLocations', N'BoundaryGeometryJson') IS NULL
        ALTER TABLE dbo.TenderGisLocations ADD BoundaryGeometryJson NVARCHAR(MAX) NULL;

    EXEC sys.sp_executesql N'
        UPDATE dbo.TenderGisLocations
        SET BoundaryGeometryType=CASE
                WHEN BoundaryCoordinatesJson IS NOT NULL THEN N''Polygon''
                WHEN JSON_VALUE(BoundaryLineGeometryJson,N''$.type'')=N''LineString'' THEN N''LineString''
                ELSE NULL
            END,
            BoundaryGeometryJson=CASE
                WHEN BoundaryCoordinatesJson IS NOT NULL
                    THEN N''{"type":"Polygon","coordinates":['' + BoundaryCoordinatesJson + N'']}''
                WHEN JSON_VALUE(BoundaryLineGeometryJson,N''$.type'')=N''LineString''
                    THEN BoundaryLineGeometryJson
                ELSE NULL
            END
        WHERE BoundaryGeometryType IS NULL OR BoundaryGeometryJson IS NULL;

        IF NOT EXISTS
        (
            SELECT 1 FROM sys.check_constraints
            WHERE parent_object_id=OBJECT_ID(N''dbo.TenderGisLocations'')
              AND name=N''CK_TenderGisLocations_BoundaryGeometry''
        )
            ALTER TABLE dbo.TenderGisLocations WITH CHECK
                ADD CONSTRAINT CK_TenderGisLocations_BoundaryGeometry
                CHECK
                (
                    (BoundaryGeometryType IS NULL AND BoundaryGeometryJson IS NULL)
                    OR
                    (BoundaryGeometryType IN (N''LineString'',N''Polygon'')
                     AND ISJSON(BoundaryGeometryJson)=1
                     AND JSON_VALUE(BoundaryGeometryJson,N''$.type'')=BoundaryGeometryType)
                );';

    IF OBJECT_ID(N'dbo.TenderDocumentFileHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderDocumentFileHistory
        (
            FileHistoryId BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderDocumentFileHistory PRIMARY KEY,
            TenderId INT NOT NULL,
            DocumentId INT NOT NULL,
            DocumentType NVARCHAR(80) NOT NULL,
            DocumentName NVARCHAR(250) NOT NULL,
            FileName NVARCHAR(260) NOT NULL,
            FilePath NVARCHAR(400) NOT NULL,
            FileExtension NVARCHAR(10) NOT NULL,
            FileSizeBytes BIGINT NULL,
            MimeType NVARCHAR(100) NOT NULL,
            UploadedBy INT NOT NULL,
            UploadedDate DATETIME2 NOT NULL,
            ActionName NVARCHAR(20) NOT NULL,
            ActionBy INT NOT NULL,
            ActionAtUtc DATETIME2 NOT NULL
                CONSTRAINT DF_TenderDocumentFileHistory_ActionAtUtc DEFAULT(SYSUTCDATETIME()),
            CONSTRAINT CK_TenderDocumentFileHistory_ActionName
                CHECK(ActionName IN (N'Replaced',N'Deleted')),
            CONSTRAINT FK_TenderDocumentFileHistory_Tender
                FOREIGN KEY(TenderId) REFERENCES dbo.Tender(TenderId),
            CONSTRAINT FK_TenderDocumentFileHistory_UploadedBy
                FOREIGN KEY(UploadedBy) REFERENCES dbo.Users(UserId),
            CONSTRAINT FK_TenderDocumentFileHistory_ActionBy
                FOREIGN KEY(ActionBy) REFERENCES dbo.Users(UserId)
        );

        CREATE INDEX IX_TenderDocumentFileHistory_Document_ActionAtUtc
            ON dbo.TenderDocumentFileHistory(DocumentId,ActionAtUtc DESC);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-29
-- Change: Adds tender schedule-date change history.
-- Original script: 20261129_TenderImportantDateHistory.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL OR OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 51000, 'Tender and Users tables must exist before this migration is applied.', 1;

    IF OBJECT_ID(N'dbo.TenderImportantDateHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderImportantDateHistory
        (
            HistoryId INT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderImportantDateHistory PRIMARY KEY,
            TenderId INT NOT NULL,
            FieldName NVARCHAR(64) NOT NULL,
            PreviousValue NVARCHAR(2000) NULL,
            NewValue NVARCHAR(2000) NULL,
            ModifiedBy INT NOT NULL,
            ModifiedAtUtc DATETIME2(0) NOT NULL
                CONSTRAINT DF_TenderImportantDateHistory_ModifiedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_TenderImportantDateHistory_Tender
                FOREIGN KEY(TenderId) REFERENCES dbo.Tender(TenderId),
            CONSTRAINT FK_TenderImportantDateHistory_User
                FOREIGN KEY(ModifiedBy) REFERENCES dbo.Users(UserId)
        );
        CREATE INDEX IX_TenderImportantDateHistory_TenderModified
            ON dbo.TenderImportantDateHistory(TenderId,ModifiedAtUtc DESC,HistoryId DESC);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-11-30
-- Change: Adds tender-document verification status, reviewer, timestamp, and remarks.
-- Original script: 20261130_TenderDocumentVerification.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 51000, 'TenderDocuments and Users must exist before this migration is applied.', 1;

    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerificationStatus') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerificationStatus NVARCHAR(30) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerificationRemarks') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerificationRemarks NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerifiedBy') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerifiedBy INT NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'VerifiedAtUtc') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD VerifiedAtUtc DATETIME2(0) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderDocuments')
          AND name=N'FK_TenderDocuments_VerifiedBy'
    )
        ALTER TABLE dbo.TenderDocuments WITH CHECK
            ADD CONSTRAINT FK_TenderDocuments_VerifiedBy
            FOREIGN KEY(VerifiedBy) REFERENCES dbo.Users(UserId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderDocuments')
          AND name=N'CK_TenderDocuments_VerificationStatus'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.TenderDocuments WITH CHECK
                ADD CONSTRAINT CK_TenderDocuments_VerificationStatus
                CHECK(VerificationStatus IS NULL OR VerificationStatus IN (N''Verified'',N''Not Verified''));';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-12-01
-- Change: Adds JE/Tender Cell verification history, shortfalls, and initial verification workflow stages.
-- Original script: 20261201_TenderInitialDocumentVerification.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Tender', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.WorkflowStages', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowAssignments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowHistory', N'U') IS NULL
        THROW 51020, 'Apply the tender workflow and document migrations before this migration.', 1;

    IF OBJECT_ID(N'dbo.TenderRequirementVerificationHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderRequirementVerificationHistory
        (
            VerificationHistoryId BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderRequirementVerificationHistory PRIMARY KEY,
            TenderId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Tender REFERENCES dbo.Tender(TenderId),
            DocumentId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Document REFERENCES dbo.TenderDocuments(DocumentId),
            ReviewStage NVARCHAR(20) NOT NULL,
            VerificationStatus NVARCHAR(20) NOT NULL,
            ReviewerId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Reviewer REFERENCES dbo.Users(UserId),
            ReviewerRoleId INT NOT NULL
                CONSTRAINT FK_TenderRequirementVerificationHistory_Role REFERENCES dbo.Roles(RoleId),
            Remarks NVARCHAR(1000) NULL,
            ReviewedAtUtc DATETIME2(0) NOT NULL
                CONSTRAINT DF_TenderRequirementVerificationHistory_ReviewedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT CK_TenderRequirementVerificationHistory_Stage
                CHECK(ReviewStage IN (N'JE',N'TENDER_CELL')),
            CONSTRAINT CK_TenderRequirementVerificationHistory_Status
                CHECK(VerificationStatus IN (N'Verified',N'Not Verified'))
        );
        CREATE INDEX IX_TenderRequirementVerificationHistory_Current
            ON dbo.TenderRequirementVerificationHistory
                (TenderId,DocumentId,ReviewStage,ReviewedAtUtc DESC,VerificationHistoryId DESC);
    END;

    IF OBJECT_ID(N'dbo.TenderDocumentShortfalls', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TenderDocumentShortfalls
        (
            TenderDocumentShortfallId BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_TenderDocumentShortfalls PRIMARY KEY,
            TenderId INT NOT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_Tender REFERENCES dbo.Tender(TenderId),
            RaisedBy INT NOT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_RaisedBy REFERENCES dbo.Users(UserId),
            RaisedAtUtc DATETIME2(0) NOT NULL
                CONSTRAINT DF_TenderDocumentShortfalls_RaisedAtUtc DEFAULT SYSUTCDATETIME(),
            DeadlineAtUtc DATETIME2(0) NOT NULL,
            Remarks NVARCHAR(2000) NOT NULL,
            RequiredDocuments NVARCHAR(2000) NOT NULL,
            ResolutionStatus NVARCHAR(20) NOT NULL
                CONSTRAINT DF_TenderDocumentShortfalls_ResolutionStatus DEFAULT(N'Open'),
            ResubmittedBy INT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_ResubmittedBy REFERENCES dbo.Users(UserId),
            ResubmittedAtUtc DATETIME2(0) NULL,
            ResolvedBy INT NULL
                CONSTRAINT FK_TenderDocumentShortfalls_ResolvedBy REFERENCES dbo.Users(UserId),
            ResolvedAtUtc DATETIME2(0) NULL,
            CONSTRAINT CK_TenderDocumentShortfalls_ResolutionStatus
                CHECK(ResolutionStatus IN (N'Open',N'Resubmitted',N'Resolved'))
        );
        CREATE INDEX IX_TenderDocumentShortfalls_Tender
            ON dbo.TenderDocumentShortfalls(TenderId,RaisedAtUtc DESC);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_Cell_InitialTenderDocumentVerification'
    )
    BEGIN
        IF EXISTS
        (
            SELECT StageOrder FROM dbo.WorkflowStages
            WHERE WorkflowVersion=2 GROUP BY StageOrder HAVING COUNT(*)>1
        )
            THROW 51021, 'Version-2 workflow stage orders are not unique; resolve the conflict before migration.', 1;

        UPDATE dbo.WorkflowStages
        SET StageOrder=StageOrder+1000
        WHERE WorkflowVersion=2 AND StageOrder>=102;

        UPDATE dbo.WorkflowStages
        SET StageOrder=StageOrder-999
        WHERE WorkflowVersion=2 AND StageOrder>=1102;

        DECLARE @CellRoleId INT;
        SELECT @CellRoleId=RoleId FROM dbo.Roles WHERE RoleCode=N'TENDER_CELL' AND IsActive=1;
        IF @CellRoleId IS NULL
            THROW 51022, 'The active Tender Cell role is required.', 1;

        UPDATE dbo.WorkflowStages
        SET StatusOnApprove=N'Pending Tender Cell Verification'
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_SubmitTender';

        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (102,N'Final_Cell_InitialTenderDocumentVerification',
             N'Pending Tender Cell Verification',N'Submitted',N'Pending JE Document Correction',
             @CellRoleId,1,2);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_CorrectTenderDocuments'
    )
    BEGIN
        DECLARE @JeRoleId INT;
        SELECT @JeRoleId=RoleId FROM dbo.Roles WHERE RoleCode=N'JE' AND IsActive=1;
        IF @JeRoleId IS NULL
            THROW 51023, 'The active Junior Engineer role is required.', 1;

        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (123,N'Final_JE_CorrectTenderDocuments',
             N'Pending JE Document Correction',N'Pending Tender Cell Verification',
             N'Pending JE Document Correction',@JeRoleId,1,2);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-12-02
-- Change: Adds and aligns JE work-order, agreement, and completion stages.
-- Original script: 20261202_TenderWorkOrderCompletionStages.sql
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WorkflowStages', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowAssignments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Roles', N'U') IS NULL
        THROW 51030, 'Apply the version-2 tender workflow migrations before this migration.', 1;

    DECLARE @JeRoleId INT;
    SELECT @JeRoleId=RoleId
    FROM dbo.Roles
    WHERE RoleCode=N'JE' AND IsActive=1;

    IF @JeRoleId IS NULL
        THROW 51031, 'The active Junior Engineer role is required.', 1;

    IF NOT EXISTS
       (SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateWorkOrder') OR
       NOT EXISTS
       (SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateAgreement')
        THROW 51034, 'The existing work-order and agreement generation stages are required.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.TenderWorkflowAssignments a
        INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
        WHERE a.IsCurrent=1 AND s.WorkflowVersion=2
          AND s.StageName IN
              (N'Final_JE_GenerateWorkOrder',N'Final_JE_GenerateAgreement',
               N'Final_JE_StartWorkOrder',N'Final_JE_StartAgreement',
               N'Final_JE_CompleteTender')
    )
        THROW 51032, 'An active work-order or agreement stage exists; review it before changing stage routing.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageOrder BETWEEN 111 AND 115
          AND StageName NOT IN
              (N'Final_JE_GenerateWorkOrder',N'Final_JE_GenerateAgreement',
               N'Final_JE_StartWorkOrder',N'Final_JE_StartAgreement',
               N'Final_JE_CompleteTender')
    )
        THROW 51033, 'Workflow stage orders 111-115 are occupied by unrelated stages.', 1;

    UPDATE dbo.WorkflowStages
    SET StageOrder=10000+StageId
    WHERE WorkflowVersion=2
      AND StageName IN (N'Final_JE_GenerateWorkOrder',N'Final_JE_GenerateAgreement');

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_StartWorkOrder'
    )
        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (111,N'Final_JE_StartWorkOrder',N'Final Financial Approval Completed',
             N'Work Order Pending',N'Final Financial Approval Completed',@JeRoleId,1,2);

    UPDATE dbo.WorkflowStages
    SET StageOrder=112,StatusOnPending=N'Work Order Pending',
        StatusOnApprove=N'Work Order Generated',StatusOnReject=N'Work Order Pending',
        ApproverRoleId=@JeRoleId,IsActive=1
    WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateWorkOrder';

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_StartAgreement'
    )
        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (113,N'Final_JE_StartAgreement',N'Work Order Generated',
             N'Tender Agreement Pending',N'Work Order Generated',@JeRoleId,1,2);

    UPDATE dbo.WorkflowStages
    SET StageOrder=114,StatusOnPending=N'Tender Agreement Pending',
        StatusOnApprove=N'Tender Agreement Generated',StatusOnReject=N'Tender Agreement Pending',
        ApproverRoleId=@JeRoleId,IsActive=1
    WHERE WorkflowVersion=2 AND StageName=N'Final_JE_GenerateAgreement';

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStages
        WHERE WorkflowVersion=2 AND StageName=N'Final_JE_CompleteTender'
    )
        INSERT INTO dbo.WorkflowStages
            (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
             ApproverRoleId,IsActive,WorkflowVersion)
        VALUES
            (115,N'Final_JE_CompleteTender',N'Tender Agreement Generated',
             N'Completed',N'Tender Agreement Generated',@JeRoleId,1,2);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-12-02
-- Change: Adds soft-delete, file revision tracking, and active-document indexing.
-- Original script: 20261202_TenderDocumentLifecycle.sql
-- ============================================================================
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderDocuments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderRequirementVerificationHistory', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 51030, 'Tender documents, verification history, and users must exist before this migration is applied.', 1;

    IF COL_LENGTH(N'dbo.TenderDocuments', N'IsDeleted') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD IsDeleted BIT NOT NULL
            CONSTRAINT DF_TenderDocuments_IsDeleted DEFAULT(0) WITH VALUES;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'DeletedAtUtc') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD DeletedAtUtc DATETIME2(0) NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'DeletedBy') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD DeletedBy INT NULL;
    IF COL_LENGTH(N'dbo.TenderDocuments', N'DocumentRevision') IS NULL
        ALTER TABLE dbo.TenderDocuments ADD DocumentRevision INT NOT NULL
            CONSTRAINT DF_TenderDocuments_DocumentRevision DEFAULT(1) WITH VALUES;
    IF COL_LENGTH(N'dbo.TenderRequirementVerificationHistory', N'DocumentRevision') IS NULL
        ALTER TABLE dbo.TenderRequirementVerificationHistory ADD DocumentRevision INT NOT NULL
            CONSTRAINT DF_TenderRequirementVerificationHistory_DocumentRevision DEFAULT(1) WITH VALUES;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderDocuments')
          AND name=N'FK_TenderDocuments_DeletedBy'
    )
        ALTER TABLE dbo.TenderDocuments WITH CHECK
            ADD CONSTRAINT FK_TenderDocuments_DeletedBy
            FOREIGN KEY(DeletedBy) REFERENCES dbo.Users(UserId);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderDocuments')
          AND name=N'CK_TenderDocuments_DocumentRevision'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.TenderDocuments WITH CHECK
                ADD CONSTRAINT CK_TenderDocuments_DocumentRevision CHECK(DocumentRevision>0);';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderRequirementVerificationHistory')
          AND name=N'CK_TenderRequirementVerificationHistory_DocumentRevision'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.TenderRequirementVerificationHistory WITH CHECK
                ADD CONSTRAINT CK_TenderRequirementVerificationHistory_DocumentRevision CHECK(DocumentRevision>0);';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id=OBJECT_ID(N'dbo.TenderDocuments')
          AND name=N'IX_TenderDocuments_ActiveTender'
    )
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_TenderDocuments_ActiveTender
                ON dbo.TenderDocuments(TenderId,IsDeleted,UploadedDate,DocumentId);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-12-03
-- Change: Adds workflow history and financial decision compatibility fields; updates generated-document constraints.
-- Original script: 20261203_TenderWorkflowSchemaCompatibility.sql
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TenderFinancialApprovals', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowHistory', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderGeneratedDocuments', N'U') IS NULL
        THROW 51040, 'Apply the version-2 tender workflow schema before this migration.', 1;

    IF COL_LENGTH(N'dbo.TenderFinancialApprovals', N'SelectedFinancialBidId') IS NULL
        ALTER TABLE dbo.TenderFinancialApprovals ADD SelectedFinancialBidId INT NULL;

    IF COL_LENGTH(N'dbo.TenderWorkflowHistory', N'ActorRoleCode') IS NULL
        ALTER TABLE dbo.TenderWorkflowHistory ADD ActorRoleCode NVARCHAR(50) NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderFinancialApprovals')
          AND name=N'FK_TenderFinancialApprovals_SelectedFinancialBid'
    )
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.TenderFinancialApprovals
            ADD CONSTRAINT FK_TenderFinancialApprovals_SelectedFinancialBid
                FOREIGN KEY (SelectedFinancialBidId)
                REFERENCES dbo.FinancialBid(FinancialBidId);';

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
          AND name=N'CK_TenderGeneratedDocuments_Kind'
    )
        ALTER TABLE dbo.TenderGeneratedDocuments
            DROP CONSTRAINT CK_TenderGeneratedDocuments_Kind;

    ALTER TABLE dbo.TenderGeneratedDocuments
        ADD CONSTRAINT CK_TenderGeneratedDocuments_Kind
            CHECK (DocumentKind IN (N'WorkOrder',N'Agreement',N'BOQ'));

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
          AND name=N'CK_TenderGeneratedDocuments_Target'
    )
        ALTER TABLE dbo.TenderGeneratedDocuments
            DROP CONSTRAINT CK_TenderGeneratedDocuments_Target;

    ALTER TABLE dbo.TenderGeneratedDocuments
        ADD CONSTRAINT CK_TenderGeneratedDocuments_Target CHECK
        (
            (DocumentKind=N'WorkOrder' AND WorkOrderId IS NOT NULL AND AgreementId IS NULL)
            OR (DocumentKind=N'Agreement' AND AgreementId IS NOT NULL AND WorkOrderId IS NULL)
            OR (DocumentKind=N'BOQ' AND WorkOrderId IS NULL AND AgreementId IS NULL)
        );

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id=OBJECT_ID(N'dbo.TenderGeneratedDocuments')
          AND name=N'UX_TenderGeneratedDocuments_BoqVersion'
    )
        CREATE UNIQUE INDEX UX_TenderGeneratedDocuments_BoqVersion
            ON dbo.TenderGeneratedDocuments(TenderId,VersionNumber)
            WHERE DocumentKind=N'BOQ';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- ============================================================================
-- Change date: 2026-12-04
-- Change: Aligns version-2 workflow stages, statuses, role assignments, and order with the current workflow.
-- Original script: 20261204_TenderWorkflowStageAlignment.sql
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.WorkflowStages', N'U') IS NULL OR
       OBJECT_ID(N'dbo.TenderWorkflowAssignments', N'U') IS NULL OR
       OBJECT_ID(N'dbo.Roles', N'U') IS NULL
        THROW 51050, 'Apply the version-2 tender workflow migrations before this migration.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.TenderWorkflowAssignments a
        INNER JOIN dbo.WorkflowStages s ON s.StageId=a.StageId
        WHERE a.IsCurrent=1 AND s.WorkflowVersion=2
          AND s.StageName<>N'Final_JE_SubmitTender'
    )
        THROW 51051, 'A non-initial version-2 workflow is active; review it before aligning workflow stages.', 1;

    DECLARE @ExpectedStages TABLE
    (
        StageOrder INT NOT NULL PRIMARY KEY,
        StageName NVARCHAR(80) NOT NULL UNIQUE,
        StatusOnPending NVARCHAR(50) NOT NULL,
        StatusOnApprove NVARCHAR(50) NOT NULL,
        StatusOnReject NVARCHAR(50) NOT NULL,
        RoleCode NVARCHAR(50) NOT NULL
    );

    INSERT INTO @ExpectedStages
        (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,RoleCode)
    VALUES
        (101,N'Final_JE_SubmitTender',N'Draft',N'Pending Tender Cell Verification',N'Draft',N'JE'),
        (102,N'Final_Cell_InitialTenderDocumentVerification',N'Pending Tender Cell Verification',N'Submitted',N'Pending JE Document Correction',N'TENDER_CELL'),
        (103,N'Final_JE_PublishTender',N'Submitted',N'Tender Published',N'Submitted',N'JE'),
        (104,N'Final_JE_OpenTechnicalBids',N'Tender Published',N'Primary Verification Pending',N'Tender Published',N'JE'),
        (105,N'Final_JE_CompletePrimaryVerification',N'Primary Verification Pending',N'Primary Verification Completed',N'Primary Verification Pending',N'JE'),
        (106,N'Final_JE_ForwardToTenderCell',N'Primary Verification Completed',N'Tender Cell Verification Pending',N'Primary Verification Completed',N'JE'),
        (107,N'Final_Cell_CompleteSecondaryVerification',N'Tender Cell Verification Pending',N'Financial Bid Opening Approval Pending',N'Shortfall Generation Pending',N'TENDER_CELL'),
        (108,N'Final_Cell_CreateShortfall',N'Shortfall Generation Pending',N'Shortfall Pending JE Approval',N'Tender Cell Verification Pending',N'TENDER_CELL'),
        (109,N'Final_JE_ApproveShortfall',N'Shortfall Pending JE Approval',N'Awaiting Contractor Submission',N'Shortfall Generation Pending',N'JE'),
        (110,N'Final_Contractor_SubmitShortfall',N'Awaiting Contractor Submission',N'Shortfall Documents Submitted',N'Awaiting Contractor Submission',N'TENDER_CELL'),
        (111,N'Final_Cell_ClearShortfall',N'Shortfall Documents Submitted',N'Shortfall Cleared',N'Awaiting Contractor Submission',N'TENDER_CELL'),
        (112,N'Final_Cell_ResumeVerification',N'Shortfall Cleared',N'Tender Cell Verification Pending',N'Shortfall Cleared',N'TENDER_CELL'),
        (113,N'Final_Samiti_ApproveFinancialOpening',N'Financial Bid Opening Approval Pending',N'Financial Bid Opening Approved',N'Financial Bid Opening Rejected',N'CHAIRMAN'),
        (114,N'Final_Cell_ReverifyRejectedOpening',N'Financial Bid Opening Rejected',N'Financial Bid Opening Approval Pending',N'Tender Cell Verification Pending',N'TENDER_CELL'),
        (115,N'Final_JE_OpenFinancialBids',N'Financial Bid Opening Approved',N'Final Financial Approval Pending',N'Financial Bid Opening Approved',N'JE'),
        (116,N'Final_Auditor_ApproveFinancialBids',N'Final Financial Approval Pending',N'Final Financial Approval Completed',N'Final Financial Approval Rejected',N'DIRECTOR_AUDITOR'),
        (117,N'Final_JE_ResubmitFinancialApproval',N'Final Financial Approval Rejected',N'Final Financial Approval Pending',N'Final Financial Approval Rejected',N'JE'),
        (118,N'Final_JE_StartWorkOrder',N'Final Financial Approval Completed',N'Work Order Pending',N'Final Financial Approval Completed',N'JE'),
        (119,N'Final_JE_GenerateWorkOrder',N'Work Order Pending',N'Work Order Generated',N'Work Order Pending',N'JE'),
        (120,N'Final_JE_StartAgreement',N'Work Order Generated',N'Tender Agreement Pending',N'Work Order Generated',N'JE'),
        (121,N'Final_JE_GenerateAgreement',N'Tender Agreement Pending',N'Tender Agreement Generated',N'Tender Agreement Pending',N'JE'),
        (122,N'Final_JE_CompleteTender',N'Tender Agreement Generated',N'Completed',N'Tender Agreement Generated',N'JE'),
        (123,N'Final_JE_CorrectTenderDocuments',N'Pending JE Document Correction',N'Pending Tender Cell Verification',N'Pending JE Document Correction',N'JE');

    IF EXISTS
    (
        SELECT 1
        FROM @ExpectedStages expected
        LEFT JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
        WHERE role.RoleId IS NULL
    )
        THROW 51052, 'One or more active workflow roles are missing.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowStages stage
        WHERE stage.WorkflowVersion=2
          AND NOT EXISTS (SELECT 1 FROM @ExpectedStages expected WHERE expected.StageName=stage.StageName)
    )
        THROW 51053, 'An unexpected version-2 stage exists; review it before aligning the workflow.', 1;

    UPDATE dbo.WorkflowStages
    SET StageOrder=20000+StageId
    WHERE WorkflowVersion=2;

    UPDATE stage
    SET StageOrder=expected.StageOrder,
        StatusOnPending=expected.StatusOnPending,
        StatusOnApprove=expected.StatusOnApprove,
        StatusOnReject=expected.StatusOnReject,
        ApproverRoleId=role.RoleId,
        IsActive=1
    FROM dbo.WorkflowStages stage
    INNER JOIN @ExpectedStages expected ON expected.StageName=stage.StageName
    INNER JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
    WHERE stage.WorkflowVersion=2;

    INSERT INTO dbo.WorkflowStages
        (StageOrder,StageName,StatusOnPending,StatusOnApprove,StatusOnReject,
         ApproverRoleId,IsActive,WorkflowVersion)
    SELECT expected.StageOrder,expected.StageName,expected.StatusOnPending,
           expected.StatusOnApprove,expected.StatusOnReject,role.RoleId,1,2
    FROM @ExpectedStages expected
    INNER JOIN dbo.Roles role ON role.RoleCode=expected.RoleCode AND role.IsActive=1
    WHERE NOT EXISTS
        (SELECT 1 FROM dbo.WorkflowStages existing
         WHERE existing.WorkflowVersion=2 AND existing.StageName=expected.StageName);

    IF EXISTS
    (
        SELECT 1
        FROM @ExpectedStages expected
        LEFT JOIN dbo.WorkflowStages stage
          ON stage.WorkflowVersion=2 AND stage.StageName=expected.StageName
        WHERE stage.StageId IS NULL OR stage.StageOrder<>expected.StageOrder OR stage.IsActive<>1
    )
        THROW 51054, 'Version-2 workflow stage alignment did not complete as expected.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
