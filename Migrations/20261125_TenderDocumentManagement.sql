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
