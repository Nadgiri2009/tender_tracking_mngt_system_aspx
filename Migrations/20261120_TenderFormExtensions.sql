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
IF COL_LENGTH(N'dbo.Tender', N'EMDAmount') IS NULL
    ALTER TABLE dbo.Tender ADD EMDAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Tender', N'TenderDocumentFee') IS NULL
    ALTER TABLE dbo.Tender ADD TenderDocumentFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Tender', N'ProcessingFee') IS NULL
    ALTER TABLE dbo.Tender ADD ProcessingFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'dbo.Tender', N'SecurityDepositAmount') IS NULL
    ALTER TABLE dbo.Tender ADD SecurityDepositAmount DECIMAL(18,2) NULL;
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
        TenderCategory NVARCHAR(100) NOT NULL,
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
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.Tender') AND name=N'FK_Tender_TenderSubcategories')
    ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT FK_Tender_TenderSubcategories
        FOREIGN KEY (TenderSubcategoryId, TenderCategory)
        REFERENCES dbo.TenderSubcategories(SubcategoryId, TenderCategory);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Tender') AND name=N'CK_Tender_Fees_NonNegative')
    ALTER TABLE dbo.Tender WITH CHECK ADD CONSTRAINT CK_Tender_Fees_NonNegative CHECK
    (
        (EMDAmount IS NULL OR EMDAmount >= 0) AND
        (TenderDocumentFee IS NULL OR TenderDocumentFee >= 0) AND
        (ProcessingFee IS NULL OR ProcessingFee >= 0) AND
        (SecurityDepositAmount IS NULL OR SecurityDepositAmount >= 0)
    );

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
        Title NVARCHAR(500) NOT NULL,
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
        SecurityDepositAmount DECIMAL(18,2) NULL,
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
