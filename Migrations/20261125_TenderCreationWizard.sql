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
